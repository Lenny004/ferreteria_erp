using Ferreteria.PuntoVenta.Services;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica el mapeo puro de atajos del menú después de aplicar permisos.</summary>
public sealed class NavigationShortcutMapTests
{
    /// <summary>Comprueba que el índice navega únicamente sobre la lista visible.</summary>
    [Fact]
    public void ResolveSectionKey_UsaElOrdenVisibleYNoAbreOcultas()
    {
        var visible = new[] { NavSections.Stock, NavSections.CorteCaja };

        Assert.Equal(NavSections.Stock, NavigationShortcutMap.ResolveSectionKey(visible, 0));
        Assert.Equal(NavSections.CorteCaja, NavigationShortcutMap.ResolveSectionKey(visible, 1));
        Assert.Null(NavigationShortcutMap.ResolveSectionKey(visible, 2));
        Assert.Null(NavigationShortcutMap.ResolveSectionKey(visible, -1));
    }

    /// <summary>Comprueba que la etiqueta numérica se genera de forma determinista.</summary>
    [Theory]
    [InlineData(0, "Ctrl+1")]
    [InlineData(8, "Ctrl+9")]
    [InlineData(9, "")]
    public void FormatShortcut_GeneraEtiquetaVisible(int index, string expected)
    {
        Assert.Equal(expected, NavigationShortcutMap.FormatShortcut(index));
    }

    /// <summary>Comprueba que no existen índices navegables después de Ctrl+9.</summary>
    [Fact]
    public void ResolveSectionKey_RechazaAtajosSinTeclaReal()
    {
        var visible = Enumerable.Range(0, 11).Select(index => $"Section{index}").ToArray();

        Assert.Equal("Section8", NavigationShortcutMap.ResolveSectionKey(visible, 8));
        Assert.Null(NavigationShortcutMap.ResolveSectionKey(visible, 9));
    }
}
