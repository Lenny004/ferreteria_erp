using Ferreteria.PuntoVenta.Services.Security;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica los permisos que habilitan la edición de cada pantalla de Inventario.</summary>
public sealed class InventoryViewPermissionPolicyTests
{
    /// <summary>Exige administración del catálogo para editar productos.</summary>
    [Fact]
    public void CanEditProducts_ExigeAdministrarCatalogo()
    {
        Assert.True(InventoryViewPermissionPolicy.CanEditProducts(
            new HashSet<PosPermission> { PosPermission.AdministrarCatalogo }));
        Assert.False(InventoryViewPermissionPolicy.CanEditProducts(
            new HashSet<PosPermission> { PosPermission.OperarInventario }));
    }

    /// <summary>Exige operación de inventario para editar proveedores.</summary>
    [Fact]
    public void CanEditSuppliers_ExigeOperarInventario()
    {
        Assert.True(InventoryViewPermissionPolicy.CanEditSuppliers(
            new HashSet<PosPermission> { PosPermission.OperarInventario }));
        Assert.False(InventoryViewPermissionPolicy.CanEditSuppliers(
            new HashSet<PosPermission> { PosPermission.AdministrarUsuarios }));
    }

    /// <summary>Exige administración de usuarios para editar usuarios.</summary>
    [Fact]
    public void CanEditUsers_ExigeAdministrarUsuarios()
    {
        Assert.True(InventoryViewPermissionPolicy.CanEditUsers(
            new HashSet<PosPermission> { PosPermission.AdministrarUsuarios }));
        Assert.False(InventoryViewPermissionPolicy.CanEditUsers(
            new HashSet<PosPermission> { PosPermission.AdministrarCatalogo }));
    }
}
