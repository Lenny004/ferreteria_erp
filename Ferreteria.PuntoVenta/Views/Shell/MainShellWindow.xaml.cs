using System.Collections.Frozen;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Security;
using Ferreteria.PuntoVenta.Services.Time;
using Ferreteria.PuntoVenta.Views.Caja;
using InventarioViews = Ferreteria.PuntoVenta.Views.Inventario;
using Ferreteria.PuntoVenta.Views.Inicio;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Views.Shell;

/// <summary>
/// Shell principal post-login: navegación lateral entre módulos Caja e Inventario
/// según el <see cref="OperationalModule"/> de la sesión (Employee autenticado por PIN).
/// </summary>
/// <remarks>Ctrl+1..Ctrl+9 navegan las secciones visibles recalculadas por permisos; F5 actualiza conectividad.</remarks>
public partial class MainShellWindow : Window
{
    /// <summary>Comando enrutado para navegar por la sección visible indicada.</summary>
    public static readonly RoutedCommand NavigateCommand = new("Navegar sección", typeof(MainShellWindow));

    private readonly Dictionary<string, (Button Button, string Title, Func<UserControl> CreateView)> _sections;
    private readonly IConnectivityService _connectivityService;
    private readonly ICurrentSessionService _currentSession;
    private readonly IAuditService _auditService;
    private readonly IServiceProvider _serviceProvider;
    private readonly CashSessionOpeningFlow _cashSessionOpeningFlow;
    private readonly IAuthorizationGuard _authorizationGuard;
    private IReadOnlySet<PosPermission> _grantedPermissions = FrozenSet<PosPermission>.Empty;
    private readonly DispatcherTimer _connectivityTimer;

    /// <summary>
    /// Construye el shell, registra secciones de navegación y muestra la sección inicial de la sesión.
    /// </summary>
    public MainShellWindow(
        IConnectivityService connectivityService,
        ICurrentSessionService currentSession,
        IAuditService auditService,
        IServiceProvider serviceProvider,
        CashSessionOpeningFlow cashSessionOpeningFlow,
        IAuthorizationGuard authorizationGuard)
    {
        _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _cashSessionOpeningFlow = cashSessionOpeningFlow ?? throw new ArgumentNullException(nameof(cashSessionOpeningFlow));
        _authorizationGuard = authorizationGuard ?? throw new ArgumentNullException(nameof(authorizationGuard));

        InitializeComponent();

        _sections = new Dictionary<string, (Button Button, string Title, Func<UserControl> CreateView)>
        {
            [NavSections.Stock] = (BtnStock, "Consultar Stock", () => _serviceProvider.GetRequiredService<ConsultarStockView>()),
            [NavSections.Facturacion] = (BtnFacturacion, "Facturacion", () => _serviceProvider.GetRequiredService<FacturacionView>()),
            [NavSections.HistorialFacturas] = (BtnHistorialFacturas, "Historial de Facturas", () => _serviceProvider.GetRequiredService<HistorialFacturasView>()),
            [NavSections.Impresoras] = (BtnImpresoras, "Impresoras", () => _serviceProvider.GetRequiredService<ImpresorasView>()),
            [NavSections.Devoluciones] = (BtnDevoluciones, "Devoluciones", () => _serviceProvider.GetRequiredService<DevolucionesView>()),
            [NavSections.CorteCaja] = (BtnCorteCaja, "Corte de Caja", () => _serviceProvider.GetRequiredService<CorteCajaView>()),
            [NavSections.Productos] = (BtnProductos, "Catalogo de Productos", () => _serviceProvider.GetRequiredService<InventarioViews.ProductosView>()),
            [NavSections.Proveedores] = (BtnProveedores, "Proveedores", () => _serviceProvider.GetRequiredService<InventarioViews.ProveedoresView>()),
            [NavSections.Movimientos] = (BtnMovimientos, "Entradas y Kardex", () => _serviceProvider.GetRequiredService<InventarioViews.MovimientosView>()),
            [NavSections.Alertas] = (BtnAlertas, "Alertas de Stock", () => _serviceProvider.GetRequiredService<InventarioViews.AlertasView>()),
            [NavSections.Usuarios] = (BtnUsuarios, "Usuarios y RRHH", () => _serviceProvider.GetRequiredService<InventarioViews.UsuariosView>())
        };

        ApplyModuleNavigation();
        RenderCurrentSession();

        _connectivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _connectivityTimer.Tick += OnConnectivityTimerTick;
        _connectivityTimer.Start();

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// Muestra u oculta botones de navegación según el módulo activo (Caja u Inventario).
    /// </summary>
    private void ApplyModuleNavigation()
    {
        if (_currentSession.ActiveModule is not OperationalModule activeModule)
        {
            foreach (var section in _sections.Values)
            {
                section.Button.Visibility = Visibility.Collapsed;
            }

            TxtNavSectionCaja.Visibility = Visibility.Collapsed;
            NavSectionDivider.Visibility = Visibility.Collapsed;
            TxtNavSectionInventario.Visibility = Visibility.Collapsed;
            return;
        }

        var allowedSections = NavSections.ForModule(activeModule, _grantedPermissions);
        var allowedSet = allowedSections.ToHashSet(StringComparer.Ordinal);

        var cajaVisible = false;
        var inventarioVisible = false;

        foreach (var (sectionKey, section) in _sections)
        {
            var isAllowed = allowedSet.Contains(sectionKey);
            section.Button.Visibility = isAllowed ? Visibility.Visible : Visibility.Collapsed;

            if (!isAllowed)
            {
                continue;
            }

            if (NavSections.ForModule(OperationalModule.Caja).Contains(sectionKey, StringComparer.Ordinal))
            {
                cajaVisible = true;
            }

            if (NavSections.ForModule(OperationalModule.Inventario).Contains(sectionKey, StringComparer.Ordinal))
            {
                inventarioVisible = true;
            }
        }

        var visibleSections = _sections.Values
            .Where(section => section.Button.Visibility == Visibility.Visible)
            .ToList();
        for (var index = 0; index < visibleSections.Count; index++)
        {
            var section = visibleSections[index];
            var shortcut = NavigationShortcutMap.FormatShortcut(index);
            section.Button.TabIndex = index;
            if (shortcut.Length == 0)
            {
                AutomationProperties.SetAcceleratorKey(section.Button, string.Empty);
                section.Button.ToolTip = null;
                continue;
            }

            AutomationProperties.SetAcceleratorKey(section.Button, shortcut);
            section.Button.ToolTip = $"{section.Title} ({shortcut})";
        }

        TxtNavSectionCaja.Visibility = cajaVisible ? Visibility.Visible : Visibility.Collapsed;
        TxtNavSectionInventario.Visibility = inventarioVisible ? Visibility.Visible : Visibility.Collapsed;
        NavSectionDivider.Visibility = cajaVisible && inventarioVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>Al cargar, consulta el estado de conectividad con la base de datos.</summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _grantedPermissions = await _authorizationGuard.GetGrantedPermissionsAsync();
        ApplyModuleNavigation();
        if (_currentSession.ActiveModule is OperationalModule module)
        {
            var initialSection = _currentSession.ResolveInitialSection();
            if (!NavSections.IsAllowed(initialSection, _grantedPermissions))
            {
                initialSection = NavSections.ForModule(module, _grantedPermissions).FirstOrDefault() ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(initialSection))
            {
                ShowSection(initialSection);
            }
        }

        await RefreshConnectivityStatusAsync();
        await EnsureCashSessionAsync();
    }

    /// <summary>
    /// Recupera la sesión ABIERTA de la caja configurada o solicita el fondo inicial al entrar a Caja.
    /// </summary>
    /// <remarks>
    /// La apertura se confirma en un diálogo táctil y la persistencia se delega al servicio serializable.
    /// Si el acceso a datos falla, la sesión local permanece sin caja activa y el cobro queda bloqueado.
    /// </remarks>
    private async Task EnsureCashSessionAsync()
    {
        if (_currentSession.ActiveModule != OperationalModule.Caja
            || _currentSession.CurrentEmployee is null)
        {
            return;
        }

        await _cashSessionOpeningFlow.EnsureOpenAsync(this);
    }

    /// <summary>Timer periódico de conectividad (cada 15 s).</summary>
    private async void OnConnectivityTimerTick(object? sender, EventArgs e)
    {
        await RefreshConnectivityStatusAsync();
    }

    /// <summary>Actualiza el estado de conectividad desde F5.</summary>
    private async void OnRefreshCommandExecuted(object sender, ExecutedRoutedEventArgs e) => await RefreshConnectivityStatusAsync();

    /// <summary>Ejecuta un atajo de navegación sobre el conjunto permitido y visible.</summary>
    private void OnNavigateCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (TryReadShortcutIndex(e.Parameter, out var shortcutIndex))
        {
            NavigateToVisibleSection(shortcutIndex);
        }
    }

    /// <summary>Habilita solo los índices que corresponden a secciones visibles.</summary>
    private void OnNavigateCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = TryReadShortcutIndex(e.Parameter, out var index)
            && NavigationShortcutMap.ResolveSectionKey(GetVisibleSectionKeys(), index) is not null;
        e.Handled = true;
    }

    /// <summary>Convierte el parámetro XAML del atajo a un índice de sección.</summary>
    private static bool TryReadShortcutIndex(object? parameter, out int index)
    {
        if (parameter is int intIndex)
        {
            index = intIndex;
            return true;
        }

        return int.TryParse(parameter?.ToString(), out index);
    }

    /// <summary>Obtiene las claves visibles en el mismo orden usado por los atajos.</summary>
    private List<string> GetVisibleSectionKeys() =>
        _sections.Where(pair => pair.Value.Button.Visibility == Visibility.Visible)
            .Select(pair => pair.Key)
            .ToList();

    /// <summary>Abre la sección visible indicada sin omitir las validaciones de permisos.</summary>
    private void NavigateToVisibleSection(int shortcutIndex)
    {
        var sectionKey = NavigationShortcutMap.ResolveSectionKey(GetVisibleSectionKeys(), shortcutIndex);
        if (sectionKey is null)
        {
            return;
        }

        if (ShowSection(sectionKey))
        {
            _sections[sectionKey].Button.Focus();
        }
    }

    /// <summary>Detiene el timer al cerrar la ventana.</summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        _connectivityTimer.Stop();
    }

    /// <summary>Actualiza el indicador visual de conexión online/offline.</summary>
    private async Task RefreshConnectivityStatusAsync()
    {
        var status = await _connectivityService.GetStatusAsync();
        ConnectivityText.Text = status.Message;
        ConnectivityText.Foreground = (Brush)FindResource(status.IsOnline ? "AppSuccess" : "AppError");
    }

    /// <summary>Handler de clic en un botón del menú lateral (Tag = clave de sección).</summary>
    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sectionKey })
        {
            ShowSection(sectionKey);
        }
    }

    /// <summary>
    /// Cambia el contenido central a la vista de la sección indicada, si pertenece al módulo activo.
    /// </summary>
    private bool ShowSection(string sectionKey)
    {
        if (_currentSession.ActiveModule is OperationalModule activeModule
            && !NavSections.BelongsToModule(sectionKey, activeModule))
        {
            return false;
        }

        if (!NavSections.IsAllowed(sectionKey, _grantedPermissions))
        {
            return false;
        }

        if (!_sections.TryGetValue(sectionKey, out var activeSection))
        {
            return false;
        }

        if (activeSection.Button.Visibility != Visibility.Visible)
        {
            return false;
        }

        if (!CanNavigateFromCurrentSection(sectionKey))
        {
            return false;
        }

        foreach (var section in _sections.Values)
        {
            section.Button.Style = (Style)FindResource("AppNavButtonStyle");
        }

        activeSection.Button.Style = (Style)FindResource("AppNavButtonActiveStyle");
        HeaderTitle.Text = activeSection.Title;
        MainContent.Content = activeSection.CreateView();
        return true;
    }

    /// <summary>Confirma la pérdida del carrito al salir de Facturación.</summary>
    /// <param name="targetSectionKey">Clave de la sección destino.</param>
    /// <returns><see langword="true"/> si la navegación puede continuar.</returns>
    private bool CanNavigateFromCurrentSection(string targetSectionKey)
    {
        if (string.Equals(targetSectionKey, NavSections.Facturacion, StringComparison.Ordinal))
        {
            return true;
        }

        return MainContent.Content is not FacturacionView facturacionView
            || facturacionView.ConfirmNavigationAway();
    }

    /// <summary>
    /// Cierra sesión del Employee, registra auditoría de logout y vuelve a InicioWindow.
    /// </summary>
    private async void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        if (_currentSession.CurrentEmployee is not null)
        {
            await _auditService.RecordLogoutAsync(_currentSession.CurrentEmployee, _currentSession.CurrentModule);
        }

        _currentSession.EndSession();
        var inicio = App.Services.GetRequiredService<InicioWindow>();
        inicio.Show();
        Close();
    }

    /// <summary>Minimiza la ventana del shell.</summary>
    private void OnMinimizarClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>Alterna maximizar / restaurar.</summary>
    private void OnMaximizarClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>Cierra la aplicación (ventana shell).</summary>
    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>Muestra en el encabezado el rol, nombre del empleado y hora de inicio de sesión.</summary>
    private void RenderCurrentSession()
    {
        if (!_currentSession.IsActive || _currentSession.CurrentEmployee is null)
        {
            SessionText.Text = "Sin sesion activa";
            return;
        }

        var employee = _currentSession.CurrentEmployee;
        var startedAt = _currentSession.StartedAtUtc is { } startedAtUtc
            ? TimeZoneSupport.ToLocalTime(startedAtUtc).ToString("HH:mm")
            : "--:--";
        var roleLabel = _currentSession.ActiveModule switch
        {
            OperationalModule.Caja => "Cajero",
            OperationalModule.Inventario => "Encargado",
            _ => "Usuario"
        };

        SessionText.Text = $"{roleLabel}: {employee.FirstName} {employee.LastName} | Inicio: {startedAt}";
    }
}
