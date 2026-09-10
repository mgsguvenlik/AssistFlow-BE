using Data.Concrete.EfCore.Configurations.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Model.Concrete;
using Model.Concrete.Collections;
using Data.Concrete.EfCore.Collections;
using Model.Dtos.Crm.Collections;
using System.ComponentModel.DataAnnotations;
using Business.Services.Crm.Collections;
using Business.Services.Crm.Collections.Calculation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
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
using var baselineContext = new PreCollectionContext(options);
using var draftContext = new AppDataContext(options);
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

Check("Collection is registered in active model and absent from pre-module baseline", contract is not null
    && baseline.FindEntityType(typeof(CollectionContract)) is null);
Check("Draft adds exactly ten entities", draft.GetEntityTypes().Count() == baseline.GetEntityTypes().Count() + 10);
var schemaOperations = draftContext.GetService<IMigrationsModelDiffer>()
    .GetDifferences(baseline.GetRelationalModel(), draft.GetRelationalModel());
Check("Schema delta only creates collection schema, tables and indexes", schemaOperations.All(operation => operation switch
{
    EnsureSchemaOperation schema => schema.Name == "collection",
    CreateTableOperation table => table.Schema == "collection",
    CreateIndexOperation index => index.Schema == "collection",
    _ => false
}));
Check("Schema delta creates exactly ten module tables", schemaOperations.OfType<CreateTableOperation>().Count() == 10);
Check("New foreign keys cannot cascade into shared or module records", schemaOperations.OfType<CreateTableOperation>()
    .SelectMany(table => table.ForeignKeys).All(key => key.OnDelete == ReferentialAction.NoAction));
var schemaCommands = draftContext.GetService<IMigrationsSqlGenerator>().Generate(schemaOperations, draft);
Check("SQL generation has no nontransactional commands", schemaCommands.Count > 0
    && schemaCommands.All(command => !command.TransactionSuppressed));
var migration = new Data.Migrations.AddCollectionFoundation
{
    ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer"
};
var migrationSql = draftContext.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, draft);
Check("Committed migration exactly matches module-only model delta", migrationSql.Select(x => x.CommandText)
    .SequenceEqual(schemaCommands.Select(x => x.CommandText)));
Check("Migration target matches active model", !draftContext.GetService<IMigrationsModelDiffer>()
    .HasDifferences(draftContext.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, true)
        .GetRelationalModel(), draft.GetRelationalModel()));
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
Check("Date range constraint permits anniversary-day starts", contract.GetCheckConstraints().Count() == 1
    && contract.GetCheckConstraints().Single().Name == "CK_Contract_DateRange");
Check("Lookup indexes have stable descending identity", contract.GetIndexes().Count(i => i.Properties.Count == 3) == 2
    && contract.GetIndexes().Where(i => i.Properties.Count == 3).All(i => !i.IsUnique && i.IsDescending!.SequenceEqual(new[] { false, false, true })));
Check("Optional definitions never cascade", contract.GetForeignKeys().Where(fk => fk.Properties.Single().Name is
    "SubscriptionStatusId" or "ContractStatusId" or "PaymentMethodId")
    .All(fk => !fk.IsRequired && fk.DeleteBehavior == DeleteBehavior.NoAction));
Check("Card fields are outside current scope", contract.FindProperty("CreditCardExpirationDate") is null
    && contract.FindProperty("CreditCardNumber") is null && contract.FindProperty("CreditCardBankId") is null);
foreach (var definition in new[] { typeof(CollectionPaymentMethod), typeof(CollectionSubscriptionStatus), typeof(CollectionContractStatus) })
{
    var mappedDefinition = draft.FindEntityType(definition)!;
    Check($"{definition.Name} stays isolated without seeds", mappedDefinition.GetSchema() == "collection"
        && !mappedDefinition.GetSeedData().Any() && baseline.FindEntityType(definition) is null);
}

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
var rate = draft.FindEntityType(typeof(CollectionContractRatePeriod))!;
Check("Rate uses isolated collection table", rate.GetSchema() == "collection" && rate.GetTableName() == "ContractRatePeriod"
    && baseline.FindEntityType(typeof(CollectionContractRatePeriod)) is null);
Check("Rate references never cascade", rate.GetForeignKeys().Count() == 3 && rate.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.NoAction));
Check("Rate uses existing currency entity", rate.GetForeignKeys().Single(x => x.Properties.Single().Name == "CurrencyTypeId").PrincipalEntityType.ClrType == typeof(CurrencyType));
Check("Rate money has explicit precision", rate.FindProperty("Amount")!.GetPrecision() == 18 && rate.FindProperty("Amount")!.GetScale() == 2);
Check("Rate has separate billing anchor", rate.FindProperty("BillingAnchor")!.GetColumnType() == "date" && rate.GetCheckConstraints().Count() == 5);
Check("Rate concurrency and active uniqueness are configured", rate.FindProperty("RowVersion")!.IsConcurrencyToken
    && rate.GetIndexes().Count(x => x.IsUnique) == 2);
var contracts = draftContext.Set<CollectionContract>();
var receipt = draft.FindEntityType(typeof(CollectionPaymentOperation))!;
Check("Receipt survives physical deletion of payment", !receipt.GetForeignKeys().Any() && receipt.FindProperty("IsDeleted") is null);
Check("Request identifier is globally unique within payment operations", receipt.GetIndexes().Any(i => i.IsUnique
    && i.Properties.Single().Name == "RequestId"));
Check("Audit snapshots and hash have structural constraints", receipt.GetCheckConstraints().Count() == 5
    && receipt.FindProperty("PayloadHash")!.GetColumnType() == "varbinary(32)");
var payment = draft.FindEntityType(typeof(CollectionPayment))!;
Check("Payment is physically deletable without soft-delete field", payment.FindProperty("IsDeleted") is null
    && !typeof(Model.Interfaces.ISoftDeletable).IsAssignableFrom(typeof(CollectionPayment)));
Check("Payment preserves source description capacity and money precision", payment.FindProperty("Description")!.GetMaxLength() == 1000
    && payment.FindProperty("Amount")!.GetPrecision() == 18 && payment.FindProperty("Amount")!.GetScale() == 2);
Check("Payment allows multiple partial payments without cascade", payment.GetIndexes().All(i => !i.IsUnique)
    && payment.GetForeignKeys().All(fk => fk.DeleteBehavior == DeleteBehavior.NoAction));
Check("Payment currency is explicit and required", !payment.FindProperty("CurrencyTypeId")!.IsNullable);
var followUp = draft.FindEntityType(typeof(CollectionContractPeriodFollowUp))!;
Check("Group follow-up has unique active contract/month and no payment relation", followUp.GetIndexes().Any(i => i.IsUnique
    && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ContractId", "Period" }))
    && followUp.GetForeignKeys().All(fk => fk.PrincipalEntityType.ClrType != typeof(CollectionPayment)));
Check("Group source description and concurrency are retained", followUp.FindProperty("Description")!.GetMaxLength() == 500
    && followUp.FindProperty("RowVersion")!.IsConcurrencyToken);
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
var validationFilter = new CollectionValidationAttribute();
var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
actionContext.ModelState.AddModelError("Page", "The value 'abc' is not valid.");
var executionContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
    new Dictionary<string, object?>(), disabledController);
validationFilter.OnActionExecuting(executionContext);
var validationResponse = (Core.Common.ResponseModel)((BadRequestObjectResult)executionContext.Result!).Value!;
Check("Binding errors return Turkish envelope without raw framework text", validationResponse.Message ==
    "Gönderilen bilgiler geçersiz. Lütfen alanları kontrol edin."
    && validationResponse.ValidationErrors!["Page"].Single().StartsWith("Girilen değer geçersiz."));
Check("Turkish validation precedes automatic API validation", validationFilter.Order < -2000);
var validExecutionContext = new ActionExecutingContext(
    new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
    new List<IFilterMetadata>(), new Dictionary<string, object?>(), disabledController);
validationFilter.OnActionExecuting(validExecutionContext);
Check("Valid requests pass through localization filter", validExecutionContext.Result is null);
var validationResults = new List<ValidationResult>();
var invalidQuery = new CollectionContractQuery { PageSize = 101 };
Validator.TryValidateObject(invalidQuery,
    new ValidationContext(invalidQuery), validationResults, true);
Check("Contract page-size validation is Turkish", validationResults.Single().ErrorMessage ==
    "Sayfa başına kayıt sayısı 1 ile 100 arasında olmalıdır.");
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
var paymentCommand = new CollectionPaymentCommand(CollectionPaymentOperationKind.Create, null, null, 1,
    new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), 1000m, 1, "Tahsilat", false);
var originalHash = CollectionPaymentCommandRules.ComputeHash(paymentCommand);
Check("Canonical amount ignores insignificant decimal scale", originalHash.SequenceEqual(
    CollectionPaymentCommandRules.ComputeHash(paymentCommand with { Amount = 1000.00m })));
Check("Canonical hash is culture independent", RunHashInCulture("tr-TR", paymentCommand).SequenceEqual(RunHashInCulture("en-US", paymentCommand)));
foreach (var changed in new[] { paymentCommand with { ContractId = 2 }, paymentCommand with { CurrencyTypeId = 2 },
    paymentCommand with { Amount = 1001m }, paymentCommand with { Description = "Tahsilat " },
    paymentCommand with { Period = new DateOnly(2026, 10, 1) }, paymentCommand with { IsFree = true } })
    Check("Changed command field changes hash", !originalHash.SequenceEqual(CollectionPaymentCommandRules.ComputeHash(changed)));
foreach (var invalid in new[] { paymentCommand with { Amount = 1.001m }, paymentCommand with { Amount = decimal.MinValue },
    paymentCommand with { Kind = (CollectionPaymentOperationKind)255 }, paymentCommand with { Period = new DateOnly(2026, 9, 2) },
    paymentCommand with { Kind = CollectionPaymentOperationKind.Update, PaymentId = 1, ExpectedRowVersion = "invalid" } })
{
    var failed = false;
    try { CollectionPaymentCommandRules.ComputeHash(invalid); } catch (ValidationException) { failed = true; }
    Check("Invalid command content is rejected", failed);
}
var deleteCommand = new CollectionPaymentCommand(CollectionPaymentOperationKind.Delete, 1,
    Convert.ToBase64String(new byte[8]), null, null, null, null, null, null, null);
Check("Delete hash includes expected version", !CollectionPaymentCommandRules.ComputeHash(deleteCommand).SequenceEqual(
    CollectionPaymentCommandRules.ComputeHash(deleteCommand with { ExpectedRowVersion = Convert.ToBase64String(new byte[] { 1,0,0,0,0,0,0,0 }) })));
using (var snapshot = System.Text.Json.JsonDocument.Parse(CollectionPaymentSnapshotRules.Serialize(new CollectionPayment
{
    Id = 9, ContractId = 2, Period = new DateOnly(2026, 9, 1), PaymentDate = new DateOnly(2026, 9, 10),
    Amount = 123.45m, CurrencyTypeId = 1, Description = "Ödeme", RowVersion = new byte[8]
})))
{
    Check("Audit snapshot preserves financial fields", snapshot.RootElement.GetProperty("Amount").GetDecimal() == 123.45m
        && snapshot.RootElement.GetProperty("Id").GetInt64() == 9);
    Check("Audit snapshot does not serialize navigation graphs", !snapshot.RootElement.TryGetProperty("Contract", out _)
        && !snapshot.RootElement.TryGetProperty("CurrencyType", out _));
    Check("Audit snapshot has schema version and row version", snapshot.RootElement.GetProperty("SchemaVersion").GetInt32() == 1
        && snapshot.RootElement.GetProperty("RowVersion").GetString() == Convert.ToBase64String(new byte[8]));
}
var paymentTransaction = new CollectionPaymentTransaction(baselineContext);
Check("Payment transaction rejects empty request key before database access", (int)(await paymentTransaction.ExecuteAsync(
    Guid.Empty, 1, paymentCommand)).StatusCode == 400);
Check("Payment transaction rejects invalid actor before database access", (int)(await paymentTransaction.ExecuteAsync(
    Guid.NewGuid(), 0, paymentCommand)).StatusCode == 400);
Check("Payment transaction validates payload before database access", (int)(await paymentTransaction.ExecuteAsync(
    Guid.NewGuid(), 1, paymentCommand with { Amount = 1.001m })).StatusCode == 400);
Check("Payment transaction remains disabled with inactive model", (int)(await paymentTransaction.ExecuteAsync(
    Guid.NewGuid(), 1, paymentCommand)).StatusCode == 503);
var canceled = false;
try { await paymentTransaction.ExecuteAsync(Guid.NewGuid(), 1, paymentCommand, new CancellationToken(true)); }
catch (OperationCanceledException) { canceled = true; }
Check("Payment transaction observes cancellation before database access", canceled);
Console.WriteLine($"{passed} collection model/query/API checks passed. No database access.");

static byte[] RunHashInCulture(string name, CollectionPaymentCommand command)
{
    var previous = System.Globalization.CultureInfo.CurrentCulture;
    try
    {
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(name);
        return CollectionPaymentCommandRules.ComputeHash(command);
    }
    finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
}

sealed class PreCollectionContext(DbContextOptions<AppDataContext> options) : AppDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Ignore<CollectionContract>();
        modelBuilder.Ignore<CollectionPaymentFrequency>();
        modelBuilder.Ignore<CollectionContractRatePeriod>();
        modelBuilder.Ignore<CollectionPaymentMethod>();
        modelBuilder.Ignore<CollectionSubscriptionStatus>();
        modelBuilder.Ignore<CollectionContractStatus>();
        modelBuilder.Ignore<CollectionPayment>();
        modelBuilder.Ignore<CollectionGroupStatus>();
        modelBuilder.Ignore<CollectionContractPeriodFollowUp>();
        modelBuilder.Ignore<CollectionPaymentOperation>();
    }
}
