namespace Ferreteria.PuntoVenta.Services.Security;

/// <summary>Centraliza los permisos de edición de las pantallas de Inventario.</summary>
public static class InventoryViewPermissionPolicy
{
    /// <summary>Indica si el usuario puede editar productos.</summary>
    /// <param name="permissions">Permisos efectivos del usuario.</param>
    /// <returns><see langword="true"/> si tiene administración del catálogo.</returns>
    public static bool CanEditProducts(IReadOnlySet<PosPermission> permissions) =>
        HasPermission(permissions, PosPermission.AdministrarCatalogo);

    /// <summary>Indica si el usuario puede editar proveedores.</summary>
    /// <param name="permissions">Permisos efectivos del usuario.</param>
    /// <returns><see langword="true"/> si puede operar el inventario.</returns>
    public static bool CanEditSuppliers(IReadOnlySet<PosPermission> permissions) =>
        HasPermission(permissions, PosPermission.OperarInventario);

    /// <summary>Indica si el usuario puede editar usuarios.</summary>
    /// <param name="permissions">Permisos efectivos del usuario.</param>
    /// <returns><see langword="true"/> si puede administrar usuarios.</returns>
    public static bool CanEditUsers(IReadOnlySet<PosPermission> permissions) =>
        HasPermission(permissions, PosPermission.AdministrarUsuarios);

    private static bool HasPermission(
        IReadOnlySet<PosPermission> permissions,
        PosPermission requiredPermission)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return permissions.Contains(requiredPermission);
    }
}
