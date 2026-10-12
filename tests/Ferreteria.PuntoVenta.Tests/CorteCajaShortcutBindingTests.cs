using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica que el XAML de Corte de Caja no reutilice Escape para cerrar el turno.</summary>
public sealed class CorteCajaShortcutBindingTests
{
    /// <summary>Comprueba que solo F9 está enlazado al comando de cierre.</summary>
    [Fact]
    public void Xaml_EnlazaCierreSoloAF9()
    {
        var xamlPath = Path.Combine(
            FindRepositoryRoot(),
            "Ferreteria.PuntoVenta",
            "Views",
            "Caja",
            "CorteCajaView.xaml");
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains(
            "<KeyBinding Key=\"F9\" Command=\"{x:Static input:ApplicationCommands.Save}\" />",
            xaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<KeyBinding Key=\"Escape\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ApplicationCommands.Close",
            xaml,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ferreteria.PuntoVenta", "Ferreteria.PuntoVenta.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException("No se encontró la raíz del repositorio.");
    }
}
