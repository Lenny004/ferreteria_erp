namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>
/// Construye patrones seguros para <c>ILIKE</c> de PostgreSQL a partir de texto escrito por el usuario.
/// </summary>
/// <remarks>
/// Los comodines <c>%</c> y <c>_</c> y el carácter de escape <c>\</c> se escapan para que la búsqueda
/// los trate como texto literal. Los patrones deben usarse con <see cref="EscapeCharacter"/> en
/// <c>EF.Functions.ILike(columna, patron, ILikePatterns.EscapeCharacter)</c>.
/// </remarks>
public static class ILikePatterns
{
    /// <summary>Carácter de escape que acompaña a los patrones generados por esta clase.</summary>
    public const string EscapeCharacter = "\\";

    /// <summary>Escapa los comodines de PostgreSQL para incorporar el texto a un patrón ILIKE.</summary>
    /// <param name="value">Texto que se incorporará al patrón.</param>
    /// <returns>Texto escapado para usar con <see cref="EscapeCharacter"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> es <c>null</c>.</exception>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // El escape debe ir primero para no duplicar las barras agregadas por los comodines.
        return value.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
    }

    /// <summary>Crea un patrón "contiene" con el texto recortado y escapado.</summary>
    /// <param name="searchText">Texto de búsqueda escrito por el usuario.</param>
    /// <returns>Patrón <c>%texto%</c> donde los comodines del usuario se tratan como literales.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="searchText"/> es <c>null</c>.</exception>
    public static string CreateContainsPattern(string searchText)
    {
        ArgumentNullException.ThrowIfNull(searchText);
        return $"%{Escape(searchText.Trim())}%";
    }
}
