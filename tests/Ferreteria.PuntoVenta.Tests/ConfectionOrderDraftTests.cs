using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Confection;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Security;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica las reglas puras del borrador de órdenes de confección.</summary>
public sealed class ConfectionOrderDraftTests
{
    private static readonly Guid EmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Agregar el mismo producto acumula la cantidad en una sola línea.</summary>
    [Fact]
    public void AddLine_MismoProducto_AcumulaCantidad()
    {
        var draft = new ConfectionOrderDraft();
        var cable = Product("CAB-01", 1.25m, decimals: 2);

        draft.AddLine(cable, 2m);
        var line = draft.AddLine(cable, 1.5m);

        Assert.Single(draft.Lines);
        Assert.Equal(3.5m, line.Quantity);
        Assert.Equal(4.38m, draft.EstimatedSubtotal);
    }

    /// <summary>Rechaza cantidades no positivas o con más decimales que la unidad.</summary>
    /// <param name="quantity">Cantidad inválida.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.5)]
    public void AddLine_CantidadInvalida_Lanza(double quantity)
    {
        var draft = new ConfectionOrderDraft();

        Assert.Throws<InvalidInventoryQuantityException>(() => draft.AddLine(Product("TERM", 0.5m, decimals: 0), (decimal)quantity));
        Assert.False(draft.HasLines);
    }

    /// <summary>No bloquea por stock: la orden queda pendiente y el stock se valida al facturar.</summary>
    [Fact]
    public void AddLine_CantidadMayorAlStock_SePermite()
    {
        var draft = new ConfectionOrderDraft();

        var line = draft.AddLine(Product("CAB-02", 2m, decimals: 2, stock: 1m), 5m);

        Assert.Equal(5m, line.Quantity);
    }

    /// <summary>Hay datos que confirmar si existen líneas o cualquier texto capturado.</summary>
    [Fact]
    public void HasCapturedData_DetectaLineasOTextos()
    {
        var draft = new ConfectionOrderDraft();

        Assert.False(draft.HasCapturedData(null, "  ", string.Empty));
        Assert.True(draft.HasCapturedData("Ana", null, null));
        Assert.True(draft.HasCapturedData(null, "7000-0000", null));
        Assert.True(draft.HasCapturedData(null, null, "cable 3 m"));

        draft.AddLine(Product("CAB-08", 1m), 1m);
        Assert.True(draft.HasCapturedData(null, null, null));

        draft.Clear();
        Assert.False(draft.HasCapturedData(null, null, null));
    }

    /// <summary>Quita solo la línea del producto indicado.</summary>
    [Fact]
    public void RemoveLine_QuitaElProducto()
    {
        var draft = new ConfectionOrderDraft();
        var first = Product("A", 1m);
        var second = Product("B", 1m);
        draft.AddLine(first, 1m);
        draft.AddLine(second, 1m);

        Assert.True(draft.RemoveLine(first.Id));
        Assert.False(draft.RemoveLine(first.Id));
        Assert.Equal(second.Id, Assert.Single(draft.Lines).ProductId);
    }

    /// <summary>Arma la solicitud con textos recortados, vacíos como nulos y cliente consumidor final.</summary>
    [Fact]
    public void BuildRequest_NormalizaDatos()
    {
        var draft = new ConfectionOrderDraft();
        var product = Product("CAB-03", 3m);
        draft.AddLine(product, 2m);

        var request = draft.BuildRequest(EmployeeId, "  Juan Pérez  ", "   ", " cable 3 m ");

        Assert.Equal(EmployeeId, request.EmployeeId);
        Assert.Null(request.CustomerId);
        Assert.Equal("Juan Pérez", request.CustomerName);
        Assert.Null(request.CustomerPhone);
        Assert.Equal("cable 3 m", request.Notes);
        Assert.NotNull(request.ClientRequestId);
        var line = Assert.Single(request.Lines);
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(2m, line.Quantity);
    }

    /// <summary>Un reintento con el mismo contenido conserva el identificador idempotente.</summary>
    [Fact]
    public void BuildRequest_MismoContenido_ReutilizaClientRequestId()
    {
        var draft = new ConfectionOrderDraft();
        draft.AddLine(Product("CAB-04", 1m), 1m);

        var first = draft.BuildRequest(EmployeeId, "Ana", "7000-0000", null);
        var retry = draft.BuildRequest(EmployeeId, "Ana", "7000-0000", null);

        Assert.Equal(first.ClientRequestId, retry.ClientRequestId);
    }

    /// <summary>Cambiar datos o líneas genera un intento nuevo para no chocar con la orden anterior.</summary>
    [Fact]
    public void BuildRequest_ContenidoDistinto_GeneraNuevoClientRequestId()
    {
        var draft = new ConfectionOrderDraft();
        var product = Product("CAB-05", 1m);
        draft.AddLine(product, 1m);
        var first = draft.BuildRequest(EmployeeId, "Ana", null, null);

        var otherName = draft.BuildRequest(EmployeeId, "Ana María", null, null);
        draft.AddLine(product, 1m);
        var otherLines = draft.BuildRequest(EmployeeId, "Ana María", null, null);

        Assert.NotEqual(first.ClientRequestId, otherName.ClientRequestId);
        Assert.NotEqual(otherName.ClientRequestId, otherLines.ClientRequestId);
    }

    /// <summary>Limpiar el borrador descarta líneas e identificador.</summary>
    [Fact]
    public void Clear_DescartaLineasEIntento()
    {
        var draft = new ConfectionOrderDraft();
        var product = Product("CAB-06", 1m);
        draft.AddLine(product, 1m);
        var first = draft.BuildRequest(EmployeeId, null, null, null);

        draft.Clear();
        draft.AddLine(product, 1m);
        var second = draft.BuildRequest(EmployeeId, null, null, null);

        Assert.NotEqual(first.ClientRequestId, second.ClientRequestId);
    }

    /// <summary>Valida empleado, líneas, longitudes y caracteres del teléfono.</summary>
    [Fact]
    public void BuildRequest_DatosInvalidos_LanzaInvalidOrderException()
    {
        var empty = new ConfectionOrderDraft();
        Assert.Throws<InvalidOrderException>(() => empty.BuildRequest(EmployeeId, null, null, null));

        var draft = new ConfectionOrderDraft();
        draft.AddLine(Product("CAB-07", 1m), 1m);
        Assert.Throws<InvalidOrderException>(() => draft.BuildRequest(Guid.Empty, null, null, null));
        Assert.Throws<InvalidOrderException>(() => draft.BuildRequest(EmployeeId, new string('a', ConfectionOrderDraft.CustomerNameMaxLength + 1), null, null));
        Assert.Throws<InvalidOrderException>(() => draft.BuildRequest(EmployeeId, null, new string('1', ConfectionOrderDraft.CustomerPhoneMaxLength + 1), null));
        Assert.Throws<InvalidOrderException>(() => draft.BuildRequest(EmployeeId, null, "7000-ABCD", null));
        Assert.Throws<InvalidOrderException>(() => draft.BuildRequest(EmployeeId, null, null, new string('n', ConfectionOrderDraft.NotesMaxLength + 1)));
        Assert.Equal("+503 (7000) 0000", draft.BuildRequest(EmployeeId, null, "+503 (7000) 0000", null).CustomerPhone);
    }

    /// <summary>La bandeja abre en Pendientes y ofrece todos los estados persistidos.</summary>
    [Fact]
    public void StatusOptions_IncluyenEstadosYAbrenEnPendientes()
    {
        Assert.Equal(SalesDomainConstants.OrderStatuses.Pending, ConfectionOrderStatusOptions.Default.Status);
        Assert.Equal(
            new[]
            {
                SalesDomainConstants.OrderStatuses.Pending,
                SalesDomainConstants.OrderStatuses.Completed,
                SalesDomainConstants.OrderStatuses.Cancelled,
                SalesDomainConstants.OrderStatuses.All
            },
            ConfectionOrderStatusOptions.All.Select(option => option.Status).ToArray());
    }

    /// <summary>Confección pertenece al módulo Inventario y exige operar inventario.</summary>
    [Fact]
    public void NavSections_Confeccion_ExigeOperarInventario()
    {
        Assert.True(NavSections.BelongsToModule(NavSections.Confeccion, OperationalModule.Inventario));
        Assert.False(NavSections.BelongsToModule(NavSections.Confeccion, OperationalModule.Caja));
        Assert.True(NavSections.IsAllowed(NavSections.Confeccion, new HashSet<PosPermission> { PosPermission.OperarInventario }));
        Assert.False(NavSections.IsAllowed(NavSections.Confeccion, new HashSet<PosPermission> { PosPermission.OperarCaja }));
    }

    private static InventoryProductResult Product(string code, decimal price, short decimals = 2, decimal stock = 100m) =>
        new(Guid.NewGuid(), code, $"Producto {code}", "Cables", null, "Metro", "m", decimals, stock, 0m, price, true);
}
