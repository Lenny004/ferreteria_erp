using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services.Confection;

/// <summary>Opción del filtro de estado de la bandeja de confección.</summary>
/// <param name="Label">Texto visible en la UI.</param>
/// <param name="Status">Valor enviado a <see cref="IOrderService.GetConfectionOrdersAsync"/>.</param>
public sealed record ConfectionOrderStatusOption(string Label, string Status);

/// <summary>Catálogo de filtros de estado para las órdenes de confección.</summary>
/// <remarks>
/// Los valores salen de <see cref="SalesDomainConstants.OrderStatuses"/> para no repetir textos
/// mágicos en la vista. La bandeja abre en Pendientes porque es la cola de trabajo del taller.
/// </remarks>
public static class ConfectionOrderStatusOptions
{
    /// <summary>Opciones en el orden en que se muestran.</summary>
    public static IReadOnlyList<ConfectionOrderStatusOption> All { get; } =
    [
        new("Pendientes", SalesDomainConstants.OrderStatuses.Pending),
        new("Completadas", SalesDomainConstants.OrderStatuses.Completed),
        new("Canceladas", SalesDomainConstants.OrderStatuses.Cancelled),
        new("Todas", SalesDomainConstants.OrderStatuses.All)
    ];

    /// <summary>Opción seleccionada al abrir la pantalla.</summary>
    public static ConfectionOrderStatusOption Default => All[0];
}
