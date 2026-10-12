namespace Ferreteria.PuntoVenta.Services;

/// <summary>Resuelve el índice visible de los atajos numéricos del menú lateral.</summary>
/// <remarks>
/// El índice se calcula después de filtrar permisos para que Ctrl+N nunca pueda saltar a una sección oculta.
/// </remarks>
public static class NavigationShortcutMap
{
    /// <summary>Obtiene la clave de sección asociada al índice visible.</summary>
    /// <param name="visibleSectionKeys">Claves en el orden actual del menú.</param>
    /// <param name="shortcutIndex">Índice cero basado en el número del atajo.</param>
    /// <returns>La clave autorizada o <see langword="null"/> si el índice no existe.</returns>
    public static string? ResolveSectionKey(IReadOnlyList<string> visibleSectionKeys, int shortcutIndex)
    {
        ArgumentNullException.ThrowIfNull(visibleSectionKeys);
        return shortcutIndex >= 0 && shortcutIndex < visibleSectionKeys.Count
            ? visibleSectionKeys[shortcutIndex]
            : null;
    }

    /// <summary>Construye la etiqueta visible del atajo para una posición cero basada.</summary>
    /// <param name="shortcutIndex">Índice cero basado en el menú.</param>
    /// <returns>Etiqueta Ctrl+N; una posición negativa produce una etiqueta vacía.</returns>
    public static string FormatShortcut(int shortcutIndex) =>
        shortcutIndex < 0 ? string.Empty : $"Ctrl+{shortcutIndex + 1}";
}
