using System.Net;
using Ferreteria.PuntoVenta.Services.Dte;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica que las respuestas de error del firmador no filtren su contenido.</summary>
public sealed class FirmadorResponseTests
{
    /// <summary>Comprueba que el mensaje conserva el código HTTP y omite el cuerpo sensible.</summary>
    [Fact]
    public void GetSignedDocument_NoExponeElCuerpoYConservaElCodigoHttp()
    {
        const string sensitiveBody = "detalle interno del firmador";
        var response = new FirmadorResponse
        {
            Status = "ERROR",
            Body = JToken.FromObject(new { detalle = sensitiveBody })
        };

        var exception = Assert.Throws<DteSigningException>(
            () => response.GetSignedDocument(HttpStatusCode.BadGateway));

        Assert.Contains("Código HTTP: 502", exception.Message);
        Assert.DoesNotContain(sensitiveBody, exception.Message);
        Assert.DoesNotContain("detalle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
