namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Determina si una acción del shell puede abandonar una venta que todavía tiene líneas.
/// </summary>
/// <remarks>
/// Cambiar de sección no descarta la venta (el shell conserva Facturación); se consulta al cerrar sesión
/// y al cerrar el punto de venta.
/// </remarks>
public static class PendingSaleNavigationGuard
{
    /// <summary>
    /// Indica si la navegación requiere confirmación por tener una venta pendiente.
    /// </summary>
    /// <param name="hasPendingSale">Indica si el carrito contiene al menos una línea.</param>
    /// <returns><see langword="true"/> cuando debe mostrarse confirmación.</returns>
    public static bool RequiresConfirmation(bool hasPendingSale) => hasPendingSale;

    /// <summary>
    /// Decide si se permite abandonar la venta según la respuesta de confirmación.
    /// </summary>
    /// <param name="hasPendingSale">Indica si el carrito contiene al menos una línea.</param>
    /// <param name="confirmationAccepted">Indica si el usuario confirmó perder la venta.</param>
    /// <returns><see langword="true"/> si la navegación puede continuar.</returns>
    public static bool ShouldNavigate(bool hasPendingSale, bool confirmationAccepted) =>
        !RequiresConfirmation(hasPendingSale) || confirmationAccepted;
}
