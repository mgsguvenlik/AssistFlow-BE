using Business.Mapper;
using Data.Concrete.EfCore.Context;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Ekb;

// Offline smoke checks: no database connection, migration, seeding or external calls.
var config = new TypeAdapterConfig();
config.Scan(typeof(EkbMapsterConfig).Assembly);
config.Compile();
Console.WriteLine("Mapster registration and compilation: OK");

using var context = new AppDataContextDesignFactory().CreateDbContext([]);
var model = context.Model;
var sourceEntities = model.GetEntityTypes()
    .Where(entity => entity.ClrType.Namespace == "Model.Concrete.Ykb").ToList();
foreach (var source in sourceEntities)
{
    var expectedName = source.Name.Replace("Ykb", "Ekb");
    var target = model.FindEntityType(expectedName)
        ?? throw new InvalidOperationException($"Missing EKB entity: {expectedName}");
    if (target.GetSchema() != "ekb" || target.GetTableName() != source.GetTableName()!.Replace("Ykb", "Ekb"))
        throw new InvalidOperationException($"Schema/table mismatch: {expectedName}");
    var expectedProperties = source.GetProperties().Select(p => (p.Name.Replace("Ykb", "Ekb"), p.ClrType.FullName!.Replace("Ykb", "Ekb"), p.IsNullable)).ToHashSet();
    var actualProperties = target.GetProperties().Select(p => (p.Name, p.ClrType.FullName!, p.IsNullable)).ToHashSet();
    if (!expectedProperties.SetEquals(actualProperties))
        throw new InvalidOperationException($"Property mismatch: {expectedName}");
}
Console.WriteLine($"YKB/EKB model parity: {sourceEntities.Count} entities OK");

var sql = context.Set<EkbWorkFlow>().Include(x => x.CurrentStep).ToQueryString();
if (!sql.Contains("[ekb]") || sql.Contains("[ykb]"))
    throw new InvalidOperationException("EKB query targets the wrong schema");
Console.WriteLine("EKB query schema: OK");
