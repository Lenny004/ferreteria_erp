using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>
/// Filtros reutilizables para consultas de <see cref="Order"/> en EF Core.
/// </summary>
internal static class OrderQueryFilters
{
    /// <summary>
    /// Filtra por UUID de orden o por coincidencia en notas / nombre del empleado.
    /// </summary>
    /// <remarks>Los comodines escritos por el usuario (<c>%</c>, <c>_</c>, <c>\</c>) se buscan como texto literal.</remarks>
    public static IQueryable<Order> ApplySearchTextFilter(this IQueryable<Order> orders, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return orders;
        }

        var normalizedSearch = searchText.Trim().TrimStart('#');
        if (Guid.TryParse(normalizedSearch, out var orderId))
        {
            return orders.Where(order => order.Id == orderId);
        }

        var likePattern = ILikePatterns.CreateContainsPattern(normalizedSearch);

        return orders.Where(order =>
            (order.Notes != null && EF.Functions.ILike(order.Notes, likePattern, ILikePatterns.EscapeCharacter)) ||
            EF.Functions.ILike(order.Employee.FirstName, likePattern, ILikePatterns.EscapeCharacter) ||
            EF.Functions.ILike(order.Employee.LastName, likePattern, ILikePatterns.EscapeCharacter));
    }
}
