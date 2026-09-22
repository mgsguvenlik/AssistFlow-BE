using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

const string connectionVariable = "COLLECTION_LEGACY_CONNECTION";
if (args.Length is not (2 or 3) || !string.Equals(args[0], "export", StringComparison.OrdinalIgnoreCase)
    || (args.Length == 3 && !string.Equals(args[2], "--inactive-source", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException(
        $"Kullanım: {connectionVariable} ortam değişkenini tanımlayın ve export <boş kesit klasörü> [--inactive-source] komutunu çalıştırın.");

var connectionString = Environment.GetEnvironmentVariable(connectionVariable);
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException($"{connectionVariable} ortam değişkeni bulunamadı.");

var builder = new SqlConnectionStringBuilder(connectionString)
{
    ApplicationName = "AssistFlow Collection Snapshot",
    ApplicationIntent = ApplicationIntent.ReadOnly
};
if (!string.Equals(builder.InitialCatalog, "MGS", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Kaynak veritabanı yalnız MGS olabilir.");

var outputDirectory = Path.GetFullPath(args[1]);
if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
    throw new InvalidOperationException("Kesit klasörü boş olmalıdır; mevcut dosyaların üzerine yazılmaz.");
Directory.CreateDirectory(outputDirectory);

var snapshotKey = $"mgs-{DateTime.UtcNow:yyyyMMdd-HHmmss}Z";
var exports = new[]
{
    new ExportDefinition("Core", "Customer", "CustomerID", "customers.ndjson"),
    new ExportDefinition("Core", "Contract", "ContractID", "contracts.ndjson"),
    new ExportDefinition("Core", "ContractHistory", "ContractHistoryID", "contract-history.ndjson")
};

await using var connection = new SqlConnection(builder.ConnectionString);
await connection.OpenAsync();
var isolationLevel = args.Length == 3 ? IsolationLevel.Serializable : IsolationLevel.Snapshot;
await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(isolationLevel);
try
{
    foreach (var export in exports)
        await ExportAsync(connection, transaction, export, outputDirectory);

    await transaction.CommitAsync();
    var manifest = JsonSerializer.Serialize(new
    {
        sourceSystem = "MGS",
        snapshotKey,
        ruleVersion = "contract-v1",
        normalizationVersion = "subscriber-v1"
    }, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(outputDirectory, "manifest.json"), manifest, new UTF8Encoding(false));
    Console.WriteLine($"Kesit başarıyla oluşturuldu: {snapshotKey}");
}
catch (SqlException exception) when (exception.Number == 3952)
{
    await transaction.RollbackAsync();
    Cleanup(outputDirectory, exports);
    throw new InvalidOperationException(
        "Kaynak MGS veritabanında Snapshot Isolation kapalı. Canlı ayar değiştirilmedi; DBA tarafından alınmış tutarlı bir kopya kullanılmalıdır.",
        exception);
}
catch
{
    await transaction.RollbackAsync();
    Cleanup(outputDirectory, exports);
    throw;
}

static void Cleanup(string outputDirectory, IEnumerable<ExportDefinition> exports)
{
    foreach (var export in exports)
    {
        var path = Path.Combine(outputDirectory, export.FileName);
        if (File.Exists(path)) File.Delete(path);
    }
    var manifest = Path.Combine(outputDirectory, "manifest.json");
    if (File.Exists(manifest)) File.Delete(manifest);
}

static async Task ExportAsync(SqlConnection connection, SqlTransaction transaction,
    ExportDefinition export, string outputDirectory)
{
    var target = Path.Combine(outputDirectory, export.FileName);
    await using var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None,
        64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandTimeout = 0;
    command.CommandText = $"SELECT * FROM [{export.Schema}].[{export.Table}] ORDER BY [{export.Key}]";
    await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
    var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
    long count = 0;
    while (await reader.ReadAsync())
    {
        using var row = new MemoryStream();
        using (var json = new Utf8JsonWriter(row))
        {
            json.WriteStartObject();
            for (var i = 0; i < names.Length; i++)
            {
                json.WritePropertyName(names[i]);
                if (await reader.IsDBNullAsync(i)) json.WriteNullValue();
                else WriteValue(json, reader.GetValue(i));
            }
            json.WriteEndObject();
        }
        row.Position = 0;
        await row.CopyToAsync(stream);
        await stream.WriteAsync("\n"u8.ToArray());
        count++;
    }
    Console.WriteLine($"{export.Table}: {count} kayıt");
}

static void WriteValue(Utf8JsonWriter writer, object value)
{
    switch (value)
    {
        case string text: writer.WriteStringValue(text); break;
        case bool boolean: writer.WriteBooleanValue(boolean); break;
        case byte number: writer.WriteNumberValue(number); break;
        case short number: writer.WriteNumberValue(number); break;
        case int number: writer.WriteNumberValue(number); break;
        case long number: writer.WriteNumberValue(number); break;
        case decimal number: writer.WriteNumberValue(number); break;
        case double number: writer.WriteNumberValue(number); break;
        case float number: writer.WriteNumberValue(number); break;
        case DateTime date: writer.WriteStringValue(date.ToString("O", CultureInfo.InvariantCulture)); break;
        case DateTimeOffset date: writer.WriteStringValue(date.ToString("O", CultureInfo.InvariantCulture)); break;
        case Guid guid: writer.WriteStringValue(guid); break;
        case byte[] bytes: writer.WriteBase64StringValue(bytes); break;
        default: writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
    }
}

internal sealed record ExportDefinition(string Schema, string Table, string Key, string FileName);
