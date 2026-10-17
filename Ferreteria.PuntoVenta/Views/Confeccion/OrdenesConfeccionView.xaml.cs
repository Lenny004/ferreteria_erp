using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Helpers;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Confection;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Security;
using Ferreteria.PuntoVenta.Views.Shell;
using Microsoft.Extensions.Logging;

namespace Ferreteria.PuntoVenta.Views.Confeccion;

/// <summary>
/// Taller de confección: crea órdenes de ensamble de cables y muestra la bandeja de órdenes con su estado.
/// </summary>
/// <remarks>
/// La orden se registra con <see cref="IOrderService.CreateConfectionOrderAsync"/> en estado PENDIENTE,
/// sin descontar inventario; caja la factura después. Las reglas del borrador viven en
/// <see cref="ConfectionOrderDraft"/>. Atajos: Ctrl+S crear, F2/Ctrl+F buscar producto, Esc limpiar, F5 actualizar.
/// El shell conserva esta vista durante la sesión, así que el borrador sobrevive al cambiar de sección.
/// </remarks>
public partial class OrdenesConfeccionView : UserControl, IPendingWorkView
{
    private const int ProductSearchLimit = 25;
    private const int OrderListLimit = 100;
    private const string StatusSuccessBrushKey = "AppSuccess";
    private const string StatusErrorBrushKey = "AppError";
    private const string StatusWarningBrushKey = "AppWarning";

    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;
    private readonly ICurrentSessionService _currentSession;
    private readonly IAuthorizationGuard _authorizationGuard;
    private readonly ILogger<OrdenesConfeccionView> _logger;
    private readonly ConfectionOrderDraft _draft = new();
    private readonly AsyncSearchCoordinator _productSearch = new();
    private readonly AsyncSearchCoordinator _orderSearch = new();
    private bool _canCreate;
    private bool _isSaving;
    private bool _isInitialized;

    /// <summary>Inicializa la vista; los datos se cargan al mostrarse.</summary>
    public OrdenesConfeccionView(
        IOrderService orderService,
        IInventoryService inventoryService,
        ICurrentSessionService currentSession,
        IAuthorizationGuard authorizationGuard,
        ILogger<OrdenesConfeccionView> logger)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        _authorizationGuard = authorizationGuard ?? throw new ArgumentNullException(nameof(authorizationGuard));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeComponent();
        StatusFilterCombo.SelectedItem = ConfectionOrderStatusOptions.Default;
        RenderDraft();
        Loaded += async (_, _) => await InitializeAsync();
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc />
    public bool HasPendingWork => _draft.HasCapturedData(CustomerNameBox.Text, CustomerPhoneBox.Text, NotesBox.Text);

    /// <inheritdoc />
    public string PendingWorkDescription => "una orden de confección en captura";

    /// <inheritdoc />
    public void DiscardPendingWork()
    {
        ClearDraftFields();
        SetStatusMessage(string.Empty);
    }

    /// <summary>
    /// Resuelve permisos y carga la bandeja y la búsqueda de productos.
    /// </summary>
    /// <remarks>Se ejecuta en cada carga: al volver a la sección se refrescan permisos y bandeja sin tocar el borrador.</remarks>
    /// <remarks>Sin permiso de inventario el formulario queda deshabilitado (fail secure); el servicio vuelve a validarlo.</remarks>
    private async Task InitializeAsync()
    {
        try
        {
            var permissions = await _authorizationGuard.GetGrantedPermissionsAsync();
            _canCreate = InventoryViewPermissionPolicy.CanCreateConfectionOrders(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron resolver los permisos de la vista de confección");
            _canCreate = false;
        }

        EditorPanel.IsEnabled = _canCreate;
        if (!_canCreate)
        {
            SetStatusMessage("Su usuario no tiene permiso para crear órdenes de confección.", isError: true);
        }

        _isInitialized = true;
        await ReloadOrdersAsync();
        await SearchProductsAsync();
    }

    /// <summary>Cancela búsquedas en curso al salir de la vista.</summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _productSearch.Dispose();
        _orderSearch.Dispose();
    }

    /// <summary>Recarga la bandeja según el estado y el texto de búsqueda.</summary>
    /// <param name="reportErrors">Si es <see langword="false"/>, el llamador decide qué mensaje mostrar al fallar.</param>
    /// <returns><see langword="true"/> si la bandeja se actualizó o la búsqueda fue reemplazada por otra más reciente.</returns>
    private async Task<bool> ReloadOrdersAsync(bool reportErrors = true)
    {
        if (!_isInitialized)
        {
            return true;
        }

        var cancellationToken = _orderSearch.BeginNewSearch();
        var status = (StatusFilterCombo.SelectedItem as ConfectionOrderStatusOption ?? ConfectionOrderStatusOptions.Default).Status;
        try
        {
            var orders = await _orderService.GetConfectionOrdersAsync(
                status,
                OrderSearchTextBox.Text,
                OrderListLimit,
                cancellationToken);
            OrdersList.ItemsSource = orders;
            EmptyOrdersText.Visibility = orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cargar la bandeja de órdenes de confección");
            if (reportErrors)
            {
                SetStatusMessage("No se pudieron cargar las órdenes. Presione F5 para reintentar.", isError: true);
            }

            return false;
        }
    }

    /// <summary>Busca productos activos para agregarlos a la orden.</summary>
    private async Task SearchProductsAsync()
    {
        if (!_isInitialized || !_canCreate)
        {
            return;
        }

        var cancellationToken = _productSearch.BeginNewSearch();
        try
        {
            ProductResultsListBox.ItemsSource = await _inventoryService.SearchProductsAsync(
                ProductSearchTextBox.Text,
                stockStatus: null,
                take: ProductSearchLimit,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (UnauthorizedOperationException ex)
        {
            SetStatusMessage(ex.Message, isError: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo buscar productos en la vista de confección");
            SetStatusMessage("No se pudo buscar productos. Intente nuevamente.", isError: true);
        }
    }

    private async void OnOrderSearchChanged(object sender, TextChangedEventArgs e) => await ReloadOrdersAsync();

    private async void OnStatusFilterChanged(object sender, SelectionChangedEventArgs e) => await ReloadOrdersAsync();

    private async void OnProductSearchChanged(object sender, TextChangedEventArgs e) => await SearchProductsAsync();

    private async void OnRefreshCommandExecuted(object sender, ExecutedRoutedEventArgs e) => await ReloadOrdersAsync();

    private void OnFindCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ProductSearchTextBox.Focus();
        ProductSearchTextBox.SelectAll();
    }

    private void OnSaveCommandExecuted(object sender, ExecutedRoutedEventArgs e) => OnCrearClick(sender, e);

    /// <summary>Habilita Ctrl+S solo con permiso y sin un guardado en curso.</summary>
    private void OnSaveCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _canCreate && !_isSaving;
        e.Handled = true;
    }

    private void OnClearCommandExecuted(object sender, ExecutedRoutedEventArgs e) => OnLimpiarClick(sender, e);

    /// <summary>Agrega el producto seleccionado con la cantidad indicada.</summary>
    /// <remarks>
    /// No bloquea por stock porque la orden no descuenta inventario hasta facturarse; solo avisa
    /// cuando la cantidad supera el stock actual.
    /// </remarks>
    private void OnAgregarClick(object sender, RoutedEventArgs e)
    {
        if (ProductResultsListBox.SelectedItem is not InventoryProductResult product)
        {
            SetStatusMessage("Seleccione un producto de los resultados.", isError: true);
            return;
        }

        var quantityResult = DecimalInputParser.Parse(
            QuantityTextBox.Text,
            precision: FieldConstraints.OrderDetailQuantityPrecision,
            scale: FieldConstraints.OrderDetailQuantityScale,
            allowNegative: false);
        if (!quantityResult.IsValid)
        {
            SetStatusMessage(quantityResult.ErrorMessage!, isError: true);
            return;
        }

        try
        {
            var line = _draft.AddLine(product, quantityResult.Value!.Value);
            QuantityTextBox.Text = "1";
            RenderDraft();
            if (line.Quantity > product.CurrentStock)
            {
                SetStatusMessage($"Producto agregado. El stock actual ({product.FormattedStock}) no alcanza; se validará al facturar.", isError: true);
                return;
            }

            SetStatusMessage("Producto agregado.");
        }
        catch (InvalidInventoryQuantityException ex)
        {
            SetStatusMessage(ex.Message, isError: true);
        }
    }

    /// <summary>Quita una línea del borrador.</summary>
    private void OnRemoveLineClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid productId } && _draft.RemoveLine(productId))
        {
            RenderDraft();
        }
    }

    /// <summary>Crea la orden de confección con los datos capturados.</summary>
    /// <remarks>
    /// Un reintento con el mismo contenido reutiliza el <c>ClientRequestId</c>, así que un error técnico
    /// después de guardar no duplica la orden.
    /// </remarks>
    private async void OnCrearClick(object sender, RoutedEventArgs e)
    {
        if (!_canCreate || _isSaving)
        {
            return;
        }

        CreateConfectionOrderRequest request;
        try
        {
            request = _draft.BuildRequest(
                _currentSession.CurrentEmployee?.Id ?? Guid.Empty,
                CustomerNameBox.Text,
                CustomerPhoneBox.Text,
                NotesBox.Text);
        }
        catch (InvalidOrderException ex)
        {
            SetStatusMessage(ex.Message, isError: true);
            return;
        }

        SetSaving(true);
        try
        {
            var result = await _orderService.CreateConfectionOrderAsync(request);
            ResetForm();
            var createdMessage = $"Orden creada y pendiente de facturar. Total: {NumberFormatter.Currency(result.Total)}.";
            SetStatusMessage(createdMessage);

            // La orden ya quedó guardada: si falla la recarga se conserva el éxito y se avisa aparte.
            if (!await ReloadOrdersAsync(reportErrors: false))
            {
                SetStatusMessage($"{createdMessage} No se pudo actualizar la bandeja; presione F5.", StatusWarningBrushKey);
            }
        }
        catch (Exception ex) when (ex is InvalidOrderException or ProductNotFoundException
            or InvalidInventoryQuantityException or UnauthorizedOperationException)
        {
            SetStatusMessage(ex.Message, isError: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error técnico al crear una orden de confección");
            SetStatusMessage("Ocurrió un error técnico. Puede reintentar sin duplicar la orden.", isError: true);
        }
        finally
        {
            SetSaving(false);
        }
    }

    /// <summary>Limpia el formulario, pidiendo confirmación si hay productos agregados.</summary>
    private void OnLimpiarClick(object sender, RoutedEventArgs e)
    {
        if (_draft.HasLines && MessageBox.Show(
                Window.GetWindow(this),
                "Se perderán los productos y datos de la orden en captura. ¿Desea limpiarla?",
                "Confirmar limpiar orden",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        ResetForm();
        SetStatusMessage(string.Empty);
    }

    private void ResetForm()
    {
        ClearDraftFields();
        CustomerNameBox.Focus();
    }

    private void ClearDraftFields()
    {
        _draft.Clear();
        CustomerNameBox.Clear();
        CustomerPhoneBox.Clear();
        NotesBox.Clear();
        QuantityTextBox.Text = "1";
        RenderDraft();
    }

    private void RenderDraft()
    {
        LinesList.ItemsSource = _draft.Lines.ToList();
        EmptyLinesText.Visibility = _draft.HasLines ? Visibility.Collapsed : Visibility.Visible;
        SubtotalText.Text = $"Subtotal estimado (sin IVA): {NumberFormatter.Currency(_draft.EstimatedSubtotal)}";
    }

    private void SetSaving(bool isSaving)
    {
        _isSaving = isSaving;
        CreateButton.IsEnabled = !isSaving;
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetStatusMessage(string message, bool isError = false) =>
        SetStatusMessage(message, isError ? StatusErrorBrushKey : StatusSuccessBrushKey);

    private void SetStatusMessage(string message, string brushKey)
    {
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(brushKey);
    }
}
