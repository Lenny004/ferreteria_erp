using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Helpers;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Time;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Consulta rápida de inventario desde el módulo Caja: lista Product con filtros por estado de stock
/// (disponible / bajo / agotado) para el cajero antes de vender.
/// </summary>
/// <remarks>Atajos: F2/Ctrl+F enfoca la búsqueda y F5 actualiza la consulta.</remarks>
public partial class ConsultarStockView : UserControl
{
    private readonly IInventoryService _inventoryService;
    private readonly AsyncSearchCoordinator _searchCoordinator = new();
    private readonly BusinessCalendar _calendar;

    /// <summary>Inicializa la consulta de stock con el servicio de inventario.</summary>
    /// <param name="inventoryService">Servicio de lectura de existencias.</param>
    /// <param name="calendar">Calendario de negocio para la hora visible.</param>
    public ConsultarStockView(IInventoryService inventoryService, BusinessCalendar calendar)
    {
        _inventoryService = inventoryService;
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Carga el listado al entrar a la vista.</summary>
    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SearchTextBox.Focus();
        await LoadProductsAsync();
    }

    /// <summary>Libera el coordinador de búsquedas al salir.</summary>
    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _searchCoordinator.Dispose();
    }

    /// <summary>Botón Actualizar: vuelve a consultar productos.</summary>
    private async void OnActualizarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        await LoadProductsAsync();
    }

    /// <summary>Enfoca y selecciona el texto del filtro de stock.</summary>
    private void OnFindCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    /// <summary>Actualiza la consulta de stock desde el teclado.</summary>
    private async void OnRefreshCommandExecuted(object sender, ExecutedRoutedEventArgs e) => await LoadProductsAsync();

    /// <summary>Handler de texto de búsqueda.</summary>
    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadProductsAsync();
    }

    /// <summary>Handler del filtro por estado de stock.</summary>
    private async void OnStatusFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadProductsAsync();
    }

    /// <summary>
    /// Consulta productos y actualiza contadores (activos, bajo stock, agotados).
    /// </summary>
    private async Task LoadProductsAsync()
    {
        var cancellationToken = _searchCoordinator.BeginNewSearch();

        try
        {
            var selectedStockFilter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                ?? SalesDomainConstants.StockFilters.All;

            var products = await _inventoryService.SearchProductsAsync(
                SearchTextBox.Text,
                selectedStockFilter,
                cancellationToken: cancellationToken);

            ProductsItemsControl.ItemsSource = products;
            ActiveProductsText.Text = products.Count.ToString();
            StockLowText.Text = products.Count(product => product.Status == SalesDomainConstants.StockFilters.Low).ToString();
            OutOfStockText.Text = products.Count(product => product.Status == SalesDomainConstants.StockFilters.Depleted).ToString();
            LastUpdatedText.Text = NumberFormatter.Time(_calendar.UtcNow());
            EmptyStateText.Visibility = products.Count == 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            EmptyStateText.Text = "No se pudo consultar el inventario. Intente nuevamente.";
            EmptyStateText.Visibility = System.Windows.Visibility.Visible;
        }
    }
}
