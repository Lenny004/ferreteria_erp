using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>
/// Restricciones adicionales utilizadas por las pantallas migradas en la fase E3.
/// </summary>
public static partial class FieldConstraints
{
    /// <summary>Longitud máxima del texto de búsqueda de productos.</summary>
    public const int ProductSearchMaxLength = 200;

    /// <summary>Longitud máxima del filtro de historial de ventas.</summary>
    public const int SalesHistorySearchMaxLength = 200;

    /// <summary>Longitud máxima del puerto de una impresora de red.</summary>
    public const int PrinterPortMaxLength = 5;

    /// <summary>Longitud máxima de la caja asignada al empleado.</summary>
    public const int EmployeeCashRegisterCodeMaxLength = 50;

    /// <summary>Longitud máxima del motivo de desbloqueo de un usuario.</summary>
    public const int EmployeeUnlockReasonMaxLength = 300;

    /// <summary>Longitud máxima de los días de crédito del proveedor.</summary>
    public const int SupplierCreditDaysMaxLength = 5;

    /// <summary>Longitud máxima del nombre de impresora para XAML.</summary>
    public static int PrinterNameMaxLength => Printers.NameMaxLength;

    /// <summary>Longitud máxima de la dirección IP de impresora para XAML.</summary>
    public static int PrinterIpAddressMaxLength => Printers.IpAddressMaxLength;

    /// <summary>Precisión de la cantidad de movimiento de inventario para XAML.</summary>
    public static int InventoryMovementQuantityPrecision => InventoryMovements.QuantityPrecision;

    /// <summary>Escala de la cantidad de movimiento de inventario para XAML.</summary>
    public static int InventoryMovementQuantityScale => InventoryMovements.QuantityScale;

    /// <summary>Precisión del costo unitario de movimiento para XAML.</summary>
    public static int InventoryMovementUnitCostPrecision => InventoryMovements.UnitCostPrecision;

    /// <summary>Escala del costo unitario de movimiento para XAML.</summary>
    public static int InventoryMovementUnitCostScale => InventoryMovements.UnitCostScale;

    /// <summary>Longitud máxima del motivo de movimiento para XAML.</summary>
    public static int InventoryMovementReasonMaxLength => InventoryMovements.ReasonMaxLength;

    /// <summary>Longitud máxima del código de producto para XAML.</summary>
    public static int ProductCodeMaxLength => Products.CodeMaxLength;

    /// <summary>Longitud máxima del código de barras para XAML.</summary>
    public static int ProductBarcodeMaxLength => Products.BarcodeMaxLength;

    /// <summary>Longitud máxima de la descripción de producto para XAML.</summary>
    public static int ProductDescriptionMaxLength => Products.DescriptionMaxLength;

    /// <summary>Precisión del precio de venta para XAML.</summary>
    public static int ProductSalePricePrecision => Products.SalePricePrecision;

    /// <summary>Escala del precio de venta para XAML.</summary>
    public static int ProductSalePriceScale => Products.SalePriceScale;

    /// <summary>Precisión del costo de producto para XAML.</summary>
    public static int ProductCostPricePrecision => Products.CostPricePrecision;

    /// <summary>Escala del costo de producto para XAML.</summary>
    public static int ProductCostPriceScale => Products.CostPriceScale;

    /// <summary>Precisión del stock de producto para XAML.</summary>
    public static int ProductCurrentStockPrecision => Products.CurrentStockPrecision;

    /// <summary>Escala del stock de producto para XAML.</summary>
    public static int ProductCurrentStockScale => Products.CurrentStockScale;

    /// <summary>Precisión del stock mínimo para XAML.</summary>
    public static int ProductMinStockPrecision => Products.MinStockPrecision;

    /// <summary>Escala del stock mínimo para XAML.</summary>
    public static int ProductMinStockScale => Products.MinStockScale;

    /// <summary>Longitud máxima de la razón social para XAML.</summary>
    public static int SupplierNameMaxLength => Suppliers.NameMaxLength;

    /// <summary>Longitud máxima del nombre comercial para XAML.</summary>
    public static int SupplierTradeNameMaxLength => Suppliers.TradeNameMaxLength;

    /// <summary>Longitud máxima del NIT del proveedor para XAML.</summary>
    public static int SupplierNitMaxLength => Suppliers.NitMaxLength;

    /// <summary>Longitud máxima del NRC del proveedor para XAML.</summary>
    public static int SupplierNrcMaxLength => Suppliers.NrcMaxLength;

    /// <summary>Longitud máxima del contacto del proveedor para XAML.</summary>
    public static int SupplierContactNameMaxLength => Suppliers.ContactNameMaxLength;

    /// <summary>Longitud máxima del teléfono del proveedor para XAML.</summary>
    public static int SupplierPhoneMaxLength => Suppliers.PhoneMaxLength;

    /// <summary>Longitud máxima del correo del proveedor para XAML.</summary>
    public static int SupplierEmailMaxLength => Suppliers.EmailMaxLength;

    /// <summary>Longitud máxima de la dirección del proveedor para XAML.</summary>
    public static int SupplierAddressMaxLength => Suppliers.AddressMaxLength;

    /// <summary>Longitud máxima del nombre del empleado para XAML.</summary>
    public static int EmployeeFirstNameMaxLength => Employees.FirstNameMaxLength;

    /// <summary>Longitud máxima del apellido del empleado para XAML.</summary>
    public static int EmployeeLastNameMaxLength => Employees.LastNameMaxLength;

    /// <summary>Longitud máxima del DUI del empleado para XAML.</summary>
    public static int EmployeeDuiMaxLength => Employees.DuiMaxLength;

    /// <summary>Precisión del salario base para XAML.</summary>
    public static int EmployeeBaseSalaryPrecision => Employees.BaseSalaryPrecision;

    /// <summary>Escala del salario base para XAML.</summary>
    public static int EmployeeBaseSalaryScale => Employees.BaseSalaryScale;

    /// <summary>Longitud máxima del teléfono del empleado para XAML.</summary>
    public static int EmployeePhoneMaxLength => Employees.PhoneMaxLength;

    /// <summary>Longitud máxima del correo del empleado para XAML.</summary>
    public static int EmployeeEmailMaxLength => Employees.EmailMaxLength;

    /// <summary>Restricciones de impresoras configurables desde el POS.</summary>
    public static class Printers
    {
        /// <summary>Longitud máxima del nombre visible de la impresora.</summary>
        [FieldConstraint("system", "Printers", "Name", FieldConstraintKind.MaxLength, typeof(Printer), nameof(Printer.Name))]
        public const int NameMaxLength = 200;

        /// <summary>Longitud máxima del tipo de conexión.</summary>
        [FieldConstraint("system", "Printers", "ConnectionType", FieldConstraintKind.MaxLength, typeof(Printer), nameof(Printer.ConnectionType))]
        public const int ConnectionTypeMaxLength = 10;

        /// <summary>Longitud máxima de la dirección IP.</summary>
        [FieldConstraint("system", "Printers", "IpAddress", FieldConstraintKind.MaxLength, typeof(Printer), nameof(Printer.IpAddress))]
        public const int IpAddressMaxLength = 15;
    }

    /// <summary>Restricciones de la cabecera de devoluciones POS.</summary>
    public static class SaleReturns
    {
        /// <summary>Longitud máxima del tipo de devolución.</summary>
        [FieldConstraint("sales", "Returns", "ReturnType", FieldConstraintKind.MaxLength, typeof(SaleReturn), nameof(SaleReturn.ReturnType))]
        public const int ReturnTypeMaxLength = 10;

        /// <summary>Longitud máxima del código de motivo.</summary>
        [FieldConstraint("sales", "Returns", "ReasonCode", FieldConstraintKind.MaxLength, typeof(SaleReturn), nameof(SaleReturn.ReasonCode))]
        public const int ReasonCodeMaxLength = 30;

        /// <summary>Longitud máxima del método de reintegro.</summary>
        [FieldConstraint("sales", "Returns", "RefundMethod", FieldConstraintKind.MaxLength, typeof(SaleReturn), nameof(SaleReturn.RefundMethod))]
        public const int RefundMethodMaxLength = 20;
    }
}
