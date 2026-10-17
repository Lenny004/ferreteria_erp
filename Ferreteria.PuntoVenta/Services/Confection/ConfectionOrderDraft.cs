using Ferreteria.PuntoVenta.Helpers;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services.Confection;

/// <summary>Línea de producto capturada en una orden de confección antes de guardarla.</summary>
/// <param name="ProductId">Identificador del producto activo.</param>
/// <param name="Code">Código interno del producto.</param>
/// <param name="Description">Descripción visible del producto.</param>
/// <param name="UnitLabel">Etiqueta de la unidad de medida (ej. "m").</param>
/// <param name="UnitPrice">Precio de venta vigente al agregar la línea.</param>
/// <param name="Quantity">Cantidad solicitada.</param>
public sealed record ConfectionDraftLine(
    Guid ProductId,
    string Code,
    string Description,
    string UnitLabel,
    decimal UnitPrice,
    decimal Quantity)
{
    /// <summary>Subtotal estimado de la línea, redondeado como lo hace <see cref="OrderService"/>.</summary>
    public decimal Subtotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);

    /// <summary>Cantidad con su unidad para la UI.</summary>
    public string QuantityText => $"{NumberFormatter.Quantity(Quantity)} {UnitLabel}".Trim();

    /// <summary>Precio unitario formateado.</summary>
    public string UnitPriceText => NumberFormatter.Currency(UnitPrice);

    /// <summary>Subtotal formateado.</summary>
    public string SubtotalText => NumberFormatter.Currency(Subtotal);
}

/// <summary>
/// Borrador de una orden de confección (taller): acumula líneas y arma la solicitud para
/// <see cref="IOrderService.CreateConfectionOrderAsync"/>.
/// </summary>
/// <remarks>
/// Es lógica pura, sin WPF ni base de datos, para poder probarla de forma determinista.
/// Conserva el mismo <c>ClientRequestId</c> mientras el contenido no cambie, de modo que reintentar
/// después de un error técnico no duplica la orden (el servicio es idempotente por ese identificador).
/// El stock no se valida aquí: la orden queda PENDIENTE y el inventario se descuenta al facturarla.
/// </remarks>
public sealed class ConfectionOrderDraft
{
    /// <summary>Longitud máxima del nombre del cliente, igual a <c>public.Customers.name</c>.</summary>
    public const int CustomerNameMaxLength = FieldConstraints.Customers.NameMaxLength;

    /// <summary>Longitud máxima del teléfono del cliente, igual a <c>public.Customers.phone</c>.</summary>
    public const int CustomerPhoneMaxLength = FieldConstraints.Customers.PhoneMaxLength;

    /// <summary>Longitud máxima de las indicaciones del trabajo.</summary>
    public const int NotesMaxLength = 500;

    private readonly List<ConfectionDraftLine> _lines = new();
    private CreateConfectionOrderRequest? _lastRequest;

    /// <summary>Líneas actuales del borrador, en el orden en que se agregaron.</summary>
    public IReadOnlyList<ConfectionDraftLine> Lines => _lines;

    /// <summary>Indica si el borrador tiene al menos una línea.</summary>
    public bool HasLines => _lines.Count > 0;

    /// <summary>Subtotal estimado sin IVA; el total definitivo lo calcula el servicio.</summary>
    public decimal EstimatedSubtotal => _lines.Sum(line => line.Subtotal);

    /// <summary>Agrega un producto o acumula la cantidad si ya estaba en el borrador.</summary>
    /// <param name="product">Producto activo elegido en la búsqueda.</param>
    /// <param name="quantity">Cantidad a agregar.</param>
    /// <returns>La línea resultante con la cantidad acumulada.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="product"/> es <c>null</c>.</exception>
    /// <exception cref="InvalidInventoryQuantityException">
    /// La cantidad no es positiva o excede los decimales de la unidad de medida.
    /// </exception>
    public ConfectionDraftLine AddLine(InventoryProductResult product, decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        InventoryQuantityValidator.ValidatePositiveQuantity(quantity, product.Decimals);

        var index = _lines.FindIndex(line => line.ProductId == product.Id);
        var line = index < 0
            ? new ConfectionDraftLine(product.Id, product.Code, product.Description, product.UnitLabel, product.SalePrice, quantity)
            : _lines[index] with { Quantity = _lines[index].Quantity + quantity };

        if (index < 0)
        {
            _lines.Add(line);
        }
        else
        {
            _lines[index] = line;
        }

        return line;
    }

    /// <summary>Quita la línea del producto indicado.</summary>
    /// <param name="productId">Producto a quitar.</param>
    /// <returns><see langword="true"/> si existía una línea para el producto.</returns>
    public bool RemoveLine(Guid productId) => _lines.RemoveAll(line => line.ProductId == productId) > 0;

    /// <summary>Vacía el borrador y descarta el identificador de intento.</summary>
    public void Clear()
    {
        _lines.Clear();
        _lastRequest = null;
    }

    /// <summary>
    /// Valida los datos capturados y arma la solicitud de creación.
    /// </summary>
    /// <param name="employeeId">Empleado de la sesión que registra la orden.</param>
    /// <param name="customerName">Nombre del cliente (opcional).</param>
    /// <param name="customerPhone">Teléfono del cliente (opcional).</param>
    /// <param name="notes">Indicaciones del trabajo (opcional).</param>
    /// <returns>
    /// Solicitud lista para el servicio. Si el contenido es igual al del intento anterior,
    /// reutiliza su <c>ClientRequestId</c>.
    /// </returns>
    /// <exception cref="InvalidOrderException">Falta el empleado, no hay líneas o un texto no es válido.</exception>
    public CreateConfectionOrderRequest BuildRequest(
        Guid employeeId,
        string? customerName,
        string? customerPhone,
        string? notes)
    {
        if (employeeId == Guid.Empty)
        {
            throw new InvalidOrderException("La orden requiere un empleado autenticado.");
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOrderException("Agregue al menos un producto a la orden.");
        }

        var name = NormalizeOptional(customerName, CustomerNameMaxLength, "El nombre del cliente");
        var phone = NormalizeOptional(customerPhone, CustomerPhoneMaxLength, "El teléfono");
        var normalizedNotes = NormalizeOptional(notes, NotesMaxLength, "Las indicaciones");
        if (phone is not null && !IsValidPhone(phone))
        {
            throw new InvalidOrderException("El teléfono solo puede contener dígitos, espacios, guiones, paréntesis y '+'.");
        }

        var lines = _lines.Select(line => new CashSaleLineRequest(line.ProductId, line.Quantity)).ToArray();
        var clientRequestId = _lastRequest is not null && IsSameContent(_lastRequest, employeeId, name, phone, normalizedNotes, lines)
            ? _lastRequest.ClientRequestId
            : Guid.NewGuid();

        _lastRequest = new CreateConfectionOrderRequest(employeeId, null, clientRequestId, name, phone, lines, normalizedNotes);
        return _lastRequest;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOrderException($"{fieldName} admite máximo {maxLength} caracteres.");
        }

        return trimmed;
    }

    private static bool IsValidPhone(string phone) =>
        phone.All(character => char.IsAsciiDigit(character) || character is ' ' or '-' or '+' or '(' or ')');

    private static bool IsSameContent(
        CreateConfectionOrderRequest previous,
        Guid employeeId,
        string? customerName,
        string? customerPhone,
        string? notes,
        IReadOnlyList<CashSaleLineRequest> lines) =>
        previous.EmployeeId == employeeId
        && previous.CustomerName == customerName
        && previous.CustomerPhone == customerPhone
        && previous.Notes == notes
        && previous.Lines.SequenceEqual(lines);
}
