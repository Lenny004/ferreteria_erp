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

    /// <summary>Facturación y Confección conservan su vista para no perder el carrito o el borrador al cambiar de sección.</summary>
    /// <param name="sectionKey">Clave de sección.</param>
    /// <param name="expected">Resultado esperado.</param>
    [Theory]
    [InlineData(NavSections.Facturacion, true)]
    [InlineData(NavSections.Confeccion, true)]
    [InlineData(NavSections.Stock, false)]
    [InlineData(NavSections.HistorialFacturas, false)]
    [InlineData(NavSections.CorteCaja, false)]
    [InlineData(NavSections.Productos, false)]
    [InlineData("facturacion", false)]
    public void KeepsViewDuringSession_SoloFacturacionYConfeccion(string sectionKey, bool expected)
    {
        Assert.Equal(expected, NavSections.KeepsViewDuringSession(sectionKey));
    }
}
