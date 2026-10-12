using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Helpers;
using Ferreteria.PuntoVenta.Services;

namespace Ferreteria.PuntoVenta.Views.Inventario;

/// <summary>
/// Alertas de stock bajo / agotado sobre Product (módulo Inventario).
/// Solo lectura: lista las alertas activas y permite refrescar.
/// </summary>
/// <remarks>Atajos: F2/Ctrl+F filtra por código, descripción o estado y F5 actualiza.</remarks>
public partial class AlertasView : UserControl
{
    private readonly IInventoryService _inventory;
    private List<AlertDisplay> _alerts = [];

    /// <summary>Inicializa la vista y carga alertas al Loaded.</summary>
    public AlertasView(IInventoryService inventory)
    {
        _inventory = inventory;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    /// <summary>Consulta alertas activas y actualiza la lista.</summary>
    private async Task ReloadAsync()
    {
        try
        {
            var items = await _inventory.GetActiveAlertsAsync();
            _alerts = items.Select(item => new AlertDisplay(item)).ToList();
            ApplyFilter();
        }
        catch (Exception)
        {
            MessageBox.Show("No se pudieron cargar las alertas. Intente nuevamente.", "Alertas",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Botón Actualizar: vuelve a cargar alertas.</summary>
    private async void OnActualizarClick(object sender, RoutedEventArgs e) => await ReloadAsync();

    /// <summary>Enfoca y selecciona el filtro de alertas.</summary>
    private void OnFindCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    /// <summary>Aplica el filtro visible sin volver a consultar el servicio.</summary>
    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    /// <summary>Actualiza la lista de alertas desde el teclado.</summary>
    private async void OnRefreshCommandExecuted(object sender, ExecutedRoutedEventArgs e) => await ReloadAsync();

    /// <summary>Filtra por código, descripción o estado de la alerta.</summary>
    private void ApplyFilter()
    {
        var filter = SearchTextBox?.Text.Trim() ?? string.Empty;
        var visible = string.IsNullOrEmpty(filter)
            ? _alerts
            : _alerts.Where(alert => alert.ProductCode.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || alert.ProductDescription.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || alert.AlertType.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        ItemsList.ItemsSource = visible;
        EmptyStateText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private sealed class AlertDisplay
    {
        public AlertDisplay(StockAlertResult item)
        {
            ProductCode = item.ProductCode;
            ProductDescription = item.ProductDescription;
            CurrentStockText = $"{NumberFormatter.Quantity(item.CurrentStock)} {item.UnitLabel}".Trim();
            MinStockText = $"{NumberFormatter.Quantity(item.MinStock)} {item.UnitLabel}".Trim();
            AlertType = item.AlertType;
        }

        public string ProductCode { get; }
        public string ProductDescription { get; }
        public string CurrentStockText { get; }
        public string MinStockText { get; }
        public string AlertType { get; }
    }
}
