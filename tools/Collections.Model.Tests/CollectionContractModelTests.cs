using Data.Concrete.EfCore.Configurations.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Model.Concrete;
using Model.Concrete.Collections;
using Data.Concrete.EfCore.Collections;
using Model.Dtos.Crm.Collections;
using System.ComponentModel.DataAnnotations;
using Business.Services.Crm.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using WebAPI.Controllers;
using Core.Settings.Concrete;
using Business.DependencyResolvers.Autofac;
using Business.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebAPI.Authorization;

// No database connection, migration, seed or host startup. SQL Server only supplies model conventions.
var options = new DbContextOptionsBuilder<AppDataContext>()
    .UseSqlServer().Options;
using var baselineContext = new AppDataContext(options);
using var draftContext = new DraftCollectionContext(options);
var baseline = baselineContext.GetService<IDesignTimeModel>().Model;
var draft = draftContext.GetService<IDesignTimeModel>().Model;
var contract = draft.FindEntityType(typeof(CollectionContract))!;
var passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException(name);
    passed++;
    Console.WriteLine($"PASS: {name}");
}

Check("Draft is absent from active application model", baseline.FindEntityType(typeof(CollectionContract)) is null);
Check("Draft adds exactly two entities", draft.GetEntityTypes().Count() == baseline.GetEntityTypes().Count() + 2);
Check("Contract uses collection schema", contract.GetSchema() == "collection" && contract.GetTableName() == "Contract");
Check("Identity is generated bigint", contract.FindPrimaryKey()!.Properties.Single().Name == "Id"
    && contract.FindProperty("Id")!.ClrType == typeof(long)
    && contract.FindProperty("Id")!.ValueGenerated == ValueGenerated.OnAdd);
Check("No tenant or product field", contract.GetProperties().All(p =>
    !p.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase)
    && !p.Name.Contains("Product", StringComparison.OrdinalIgnoreCase)));
foreach (var reference in new[] { ("CustomerId", typeof(Customer), "Customers"), ("ServiceTypeId", typeof(ServiceType), "ServiceType") })
{
    var fk = contract.GetForeignKeys().Single(x => x.Properties.Single().Name == reference.Item1);
    Check($"{reference.Item1} points to existing required principal without cascade",
        fk.PrincipalEntityType.ClrType == reference.Item2 && fk.PrincipalEntityType.GetTableName() == reference.Item3
        && (fk.PrincipalEntityType.GetSchema() ?? "dbo") == "dbo"
        && fk.IsRequired && fk.DeleteBehavior == DeleteBehavior.NoAction && fk.PrincipalToDependent is null);
}
Check("Row version is database generated concurrency token", contract.FindProperty("RowVersion") is
    { IsConcurrencyToken: true, ValueGenerated: ValueGenerated.OnAddOrUpdate });
Check("Date columns preserve nullable inclusive end", contract.FindProperty("StartDate")!.GetColumnType() == "date"
    && !contract.FindProperty("StartDate")!.IsNullable && contract.FindProperty("EndDate")!.GetColumnType() == "date"
    && contract.FindProperty("EndDate")!.IsNullable);
Check("Source references are nullable bounded nonunique values", new[] { "GtsNo", "IvrNo" }.All(name =>
    contract.FindProperty(name) is { IsNullable: true } p && p.GetMaxLength() == 50
    && !contract.GetIndexes().Any(i => i.IsUnique && i.Properties.Any(x => x.Name == name))));
Check("Date constraints exist", contract.GetCheckConstraints().Count() == 2);
Check("Lookup indexes have stable descending identity", contract.GetIndexes().Count() == 2
    && contract.GetIndexes().All(i => !i.IsUnique && i.IsDescending!.SequenceEqual(new[] { false, false, true })));

// Compare every existing entity's columns and indexes, not just the two referenced tables.
string Shape(IEntityType entity) => string.Join("|", new[] { entity.GetSchema(), entity.GetTableName() }
    .Concat(entity.GetProperties().OrderBy(p => p.Name).Select(p =>
        $"{p.Name}:{p.GetColumnType()}:{p.IsNullable}:{p.GetMaxLength()}:{p.ValueGenerated}:{p.IsConcurrencyToken}"))
    .Concat(entity.GetIndexes().OrderBy(i => i.GetDatabaseName()).Select(i =>
        $"{i.GetDatabaseName()}:{i.IsUnique}:{string.Join(',', i.Properties.Select(p => p.Name))}:{i.GetFilter()}")));
Check("Existing entity columns and indexes are unchanged", baseline.GetEntityTypes().All(e =>
    Shape(e) == Shape(draft.FindEntityType(e.Name)!)));
var frequency = draft.FindEntityType(typeof(CollectionPaymentFrequency))!;
Check("Frequency stays outside active model", baseline.FindEntityType(typeof(CollectionPaymentFrequency)) is null);
Check("Frequency uses collection schema", frequency.GetSchema() == "collection" && frequency.GetTableName() == "PaymentFrequency");
Check("Frequency strings are required and bounded", !frequency.FindProperty("Code")!.IsNullable
    && frequency.FindProperty("Code")!.GetMaxLength() == 30 && !frequency.FindProperty("Name")!.IsNullable
    && frequency.FindProperty("Name")!.GetMaxLength() == 100);
Check("Frequency interval and code are independently unique", frequency.GetIndexes().Count() == 2
    && frequency.GetIndexes().All(i => i.IsUnique && i.Properties.Count == 1)
    && frequency.GetIndexes().Select(i => i.Properties.Single().Name).Order().SequenceEqual(new[] { "Code", "IntervalMonths" }));
Check("Frequency retains all known historical intervals", frequency.GetCheckConstraints().Single().Sql ==
    "[IntervalMonths] IN (1,2,3,4,6,12,24,36)");
Check("Frequency has no automatic activation or seed", !frequency.GetSeedData().Any()
    && frequency.FindProperty("IsActive")!.ValueGenerated == ValueGenerated.Never
    && !new CollectionPaymentFrequency().IsActive);
var contracts = draftContext.Set<CollectionContract>();
foreach (var sort in Enum.GetValues<CollectionContractSort>())
{
    foreach (var desc in new[] { false, true })
    {
        var sql = CollectionContractReadQuery.Page(contracts, new() { SortBy = sort, Desc = desc, Page = 2 }).ToQueryString();
        Check($"SQL translation for {sort}/{desc}", sql.Contains("[collection].[Contract]")
            && sql.Contains("OFFSET") && sql.Contains("FETCH NEXT") && sql.Contains("ORDER BY"));
    }
}
var filteredSql = CollectionContractReadQuery.Filter(contracts,
    new() { CustomerId = 5, ServiceTypeId = 2, Search = "O'Brien%_" }).ToQueryString();
Check("Filter SQL is unpaged and parameterized", !filteredSql.Contains("OFFSET")
    && filteredSql.Contains("DECLARE @") && filteredSql.Contains("[IsDeleted]"));
var detailSql = CollectionContractReadQuery.Detail(contracts, 7).ToQueryString();
Check("Detail projects only bounded identity fields", !detailSql.Contains("[Phone1]")
    && !detailSql.Contains("[RowVersion]") && !detailSql.Contains("OFFSET") && detailSql.Contains("[Id] ="));
var rejected = false;
try { CollectionContractReadQuery.Page(contracts, new() { PageSize = 101 }); }
catch (ValidationException) { rejected = true; }
Check("Read query rejects oversized pages before SQL", rejected);
var disabledController = new CollectionContractsController(Options.Create(new CollectionReadOptions()));
Check("Disabled list never invokes service", (await disabledController.GetPage(new(), null!, default)) is ObjectResult { StatusCode: 503 });
Check("Disabled detail never invokes service", (await disabledController.GetDetail(1, null!, default)) is ObjectResult { StatusCode: 503 });
var unconfiguredService = new CollectionContractReadService(baselineContext, null!);
Check("Unconfigured model refuses list without database", (int)(await unconfiguredService.GetPageAsync(new())).StatusCode == 503);
Check("Unconfigured model refuses detail without database", (int)(await unconfiguredService.GetDetailAsync(1)).StatusCode == 503);
Check("Controller requires authentication", Attribute.IsDefined(typeof(CollectionContractsController), typeof(AuthorizeAttribute)));
var actions = typeof(CollectionContractsController).GetMethods().Where(m => Attribute.IsDefined(m, typeof(HttpGetAttribute))).ToArray();
Check("Only two explicit read actions exist", actions.Length == 2);
Check("Every read action requires collection View", actions.All(m =>
{
    var permission = (MenuAuthorizeAttribute?)Attribute.GetCustomAttribute(m, typeof(MenuAuthorizeAttribute));
    return permission?.Arguments is [string[] keys, MenuPermission.View] && keys.SequenceEqual(new[] { "CollectionFollowUp" });
}));
var registrations = new ServiceCollection();
registrations.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
new AutofacBusinessModule().Load(registrations);
var readRegistration = registrations.Single(x => x.ServiceType == typeof(ICollectionContractReadService));
Check("Business module registers read service as scoped", readRegistration.Lifetime == ServiceLifetime.Scoped
    && readRegistration.ImplementationType == typeof(CollectionContractReadService));
using (var provider = registrations.BuildServiceProvider())
    Check("Business module options default to disabled", !provider.GetRequiredService<IOptions<CollectionReadOptions>>().Value.Enabled);
var configuredRegistrations = new ServiceCollection();
configuredRegistrations.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string, string?> { ["CollectionRead:Enabled"] = "true" }).Build());
new AutofacBusinessModule().Load(configuredRegistrations);
using (var provider = configuredRegistrations.BuildServiceProvider())
    Check("Business module binds explicit option", provider.GetRequiredService<IOptions<CollectionReadOptions>>().Value.Enabled);
Console.WriteLine($"{passed} collection model/query/API checks passed. No database access.");

sealed class DraftCollectionContext(DbContextOptions<AppDataContext> options) : AppDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new CollectionContractConfiguration());
        modelBuilder.ApplyConfiguration(new CollectionPaymentFrequencyConfiguration());
    }
}
