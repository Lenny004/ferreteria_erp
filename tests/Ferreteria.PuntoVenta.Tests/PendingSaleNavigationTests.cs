using Ferreteria.PuntoVenta.Services;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica la decisión de navegación cuando Facturación tiene un carrito pendiente.</summary>
public sealed class PendingSaleNavigationTests
{
    /// <summary>Permite navegar sin confirmación cuando el carrito está vacío.</summary>
    [Fact]
    public void ShouldNavigate_SinCarrito_PermiteSalir()
    {
        Assert.True(PendingSaleNavigationGuard.ShouldNavigate(hasPendingSale: false, confirmationAccepted: false));
    }

    /// <summary>Conserva la pantalla si el usuario cancela la confirmación.</summary>
    [Fact]
    public void ShouldNavigate_ConCarritoYCancelacion_BloqueaSalida()
    {
        Assert.False(PendingSaleNavigationGuard.ShouldNavigate(hasPendingSale: true, confirmationAccepted: false));
    }

    /// <summary>Permite abandonar la venta solo después de confirmar.</summary>
    [Fact]
    public void ShouldNavigate_ConCarritoYConfirmacion_PermiteSalir()
    {
        Assert.True(PendingSaleNavigationGuard.ShouldNavigate(hasPendingSale: true, confirmationAccepted: true));
    }
}
