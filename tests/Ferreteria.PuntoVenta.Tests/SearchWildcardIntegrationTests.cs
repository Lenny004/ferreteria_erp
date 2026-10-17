using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>
/// Verifica contra PostgreSQL que las búsquedas de catálogos tratan <c>%</c>, <c>_</c> y <c>\</c> como texto literal.
/// </summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class SearchWildcardIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Clientes: el porcentaje, el guion bajo y la barra invertida no actúan como comodines.</summary>
    [Fact]
    public async Task CustomerSearch_ComodinesDelUsuario_SeBuscanLiteralmente()
    {
        var token = NewToken();
        await SeedCustomersAsync(token, "50% PVC", "500 PVC", "a_b", "axb", "c\\d", "cd");
        var service = new CustomerService(fixture.Services.GetRequiredService<IServiceScopeFactory>(), new NoOpAuditService(), new AllowAllGuard());

        Assert.Equal(new[] { $"{token} 50% PVC" }, await SearchCustomerNamesAsync(service, $"{token} 50%"));
        Assert.Equal(new[] { $"{token} a_b" }, await SearchCustomerNamesAsync(service, $"{token} a_b"));
        Assert.Equal(new[] { $"{token} c\\d" }, await SearchCustomerNamesAsync(service, $"{token} c\\d"));
    }

    /// <summary>Proveedores: un guion bajo no coincide con cualquier carácter.</summary>
    [Fact]
    public async Task SupplierSearch_GuionBajo_NoActuaComoComodin()
    {
        var token = NewToken();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            db.Suppliers.AddRange(
                new Supplier { Id = Guid.NewGuid(), Name = $"{token} tubo_pvc" },
                new Supplier { Id = Guid.NewGuid(), Name = $"{token} tuboXpvc" });
            await db.SaveChangesAsync();
        }

        var service = new SupplierService(fixture.Services.GetRequiredService<IServiceScopeFactory>(), new NoOpAuditService(), new AllowAllGuard());
        var results = await service.GetSuppliersAsync($"{token} tubo_pvc");

        Assert.Equal(new[] { $"{token} tubo_pvc" }, results.Select(item => item.Name).ToArray());
    }

    private static string NewToken() => $"lk{Guid.NewGuid():N}"[..12];

    private async Task SeedCustomersAsync(string token, params string[] suffixes)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        db.Customers.AddRange(suffixes.Select(suffix => new Customer { Id = Guid.NewGuid(), Name = $"{token} {suffix}" }));
        await db.SaveChangesAsync();
    }

    private static async Task<string[]> SearchCustomerNamesAsync(CustomerService service, string searchText) =>
        (await service.GetCustomersAsync(searchText)).Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();

    /// <summary>Guard que autoriza todo: estas pruebas validan la búsqueda, no RBAC.</summary>
    private sealed class AllowAllGuard : IAuthorizationGuard
    {
        /// <inheritdoc />
        public Task<AuthorizedEmployee> RequireAsync(PosPermission permission, Guid? actingEmployeeId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthorizedEmployee(actingEmployeeId ?? Guid.NewGuid(), true, true, true, "Administrador"));

        /// <inheritdoc />
        public Task<IReadOnlySet<PosPermission>> GetGrantedPermissionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<PosPermission>>(Enum.GetValues<PosPermission>().ToHashSet());
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task RecordLoginAsync(Employee employee, string module, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordLogoutAsync(Employee employee, string? module, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordChangeAsync(string action, string tableName, string recordId, object? oldData, object? newData, Guid? userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
