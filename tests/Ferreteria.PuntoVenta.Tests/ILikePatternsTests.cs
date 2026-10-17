using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica el escape de comodines de los patrones ILIKE construidos con texto del usuario.</summary>
public sealed class ILikePatternsTests
{
    /// <summary>Escapa cada comodín y el carácter de escape para que se busquen como texto literal.</summary>
    /// <param name="input">Texto escrito por el usuario.</param>
    /// <param name="expected">Texto escapado esperado.</param>
    [Theory]
    [InlineData("50%", "50\\%")]
    [InlineData("tubo_pvc", "tubo\\_pvc")]
    [InlineData("c:\\ruta", "c:\\\\ruta")]
    [InlineData("\\%", "\\\\\\%")]
    [InlineData("tornillo", "tornillo")]
    public void Escape_ComodinesYEscape_SeTratanComoLiterales(string input, string expected)
    {
        Assert.Equal(expected, ILikePatterns.Escape(input));
    }

    /// <summary>El patrón "contiene" recorta espacios y envuelve el texto escapado entre comodines.</summary>
    [Fact]
    public void CreateContainsPattern_RecortaYEscapa()
    {
        Assert.Equal("%10\\% desc\\_%", ILikePatterns.CreateContainsPattern("  10% desc_  "));
    }

    /// <summary>El helper histórico del historial de ventas mantiene el mismo resultado.</summary>
    [Fact]
    public void SalesHistoryFilter_EscapeILikePattern_DelegaEnElMismoEscape()
    {
        const string input = "a%b_c\\d";
        Assert.Equal(ILikePatterns.Escape(input), SalesHistoryFilter.EscapeILikePattern(input));
    }

    /// <summary>Rechaza texto nulo en lugar de producir un patrón que coincida con todo.</summary>
    [Fact]
    public void Escape_Nulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ILikePatterns.Escape(null!));
        Assert.Throws<ArgumentNullException>(() => ILikePatterns.CreateContainsPattern(null!));
    }
}
