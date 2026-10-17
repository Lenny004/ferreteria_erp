namespace Ferreteria.PuntoVenta.Views.Shell;

/// <summary>
/// Vista que el shell conserva durante la sesión y que puede tener trabajo sin guardar
/// (por ejemplo, el carrito de Facturación o el borrador de una orden de confección).
/// </summary>
/// <remarks>
/// El shell la consulta antes de cerrar sesión o cerrar el POS para pedir confirmación y,
/// si el usuario acepta, llama a <see cref="DiscardPendingWork"/> para que el siguiente empleado no herede los datos.
/// </remarks>
public interface IPendingWorkView
{
    /// <summary>Indica si hay datos capturados que se perderían.</summary>
    bool HasPendingWork { get; }

    /// <summary>Descripción para el diálogo de confirmación, p. ej. "una venta en curso con productos en el carrito".</summary>
    string PendingWorkDescription { get; }

    /// <summary>Descarta el trabajo sin guardar sin pedir confirmación; el llamador ya la obtuvo.</summary>
    void DiscardPendingWork();
}
