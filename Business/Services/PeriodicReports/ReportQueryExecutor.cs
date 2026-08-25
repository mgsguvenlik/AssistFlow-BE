using Business.Interfaces.PeriodicReports;
using Business.Models;
using Core.Settings.Concrete;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Globalization;
using System.Text;

namespace Business.Services.PeriodicReports
{
    public sealed class ReportQueryExecutor : IReportQueryExecutor
    {
        private readonly IReportSqlValidator _validator;
        private readonly PeriodicReportOptions _options;
        private readonly AppSettings _appSettings;
        private readonly ILogger<ReportQueryExecutor> _logger;

        public ReportQueryExecutor(
            IReportSqlValidator validator,
            IOptions<PeriodicReportOptions> options,
            IOptions<AppSettings> appSettings,
            ILogger<ReportQueryExecutor> logger)
        {
            _validator = validator;
            _options = options.Value;
            _appSettings = appSettings.Value;
            _logger = logger;
        }

        public async Task<ReportData> ExecuteAsync(
            string sqlQuery,
            int maxRows,
            bool allowTruncation,
            CancellationToken cancellationToken)
        {
            var validation = _validator.Validate(sqlQuery);
            if (!validation.IsValid)
                throw new InvalidOperationException(string.Join(" ", validation.Errors));

            var connectionString = _options.ReportingConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = _appSettings.MSSQLConnectionString;
                _logger.LogWarning(
                    "PeriodicReport ReportingConnectionString tanımlı değil; ana bağlantı kullanılıyor. Üretimde salt-okunur ayrı kullanıcı yapılandırılmalıdır.");
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = sqlQuery;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = Math.Max(1, _options.QueryTimeoutSeconds);

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess | CommandBehavior.SingleResult,
                cancellationToken);

            var columns = BuildUniqueColumnNames(reader);
            var rows = new List<Dictionary<string, object?>>(Math.Min(maxRows, 1024));
            var approximateBytes = 0L;
            var maxResultBytes = Math.Max(1, _options.MaxResultSizeMb) * 1024L * 1024L;
            var isTruncated = false;

            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= maxRows)
                {
                    if (allowTruncation)
                    {
                        isTruncated = true;
                        break;
                    }

                    throw new InvalidOperationException($"Rapor maksimum {maxRows:N0} satır sınırını aşıyor.");
                }

                var row = new Dictionary<string, object?>(columns.Count, StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < columns.Count; index++)
                {
                    var rawValue = await reader.IsDBNullAsync(index, cancellationToken)
                        ? null
                        : reader.GetValue(index);
                    var value = NormalizeValue(rawValue);
                    approximateBytes += EstimateSize(value);
                    if (approximateBytes > maxResultBytes)
                        throw new InvalidOperationException("Rapor sonucu yapılandırılmış maksimum bellek/çıktı sınırını aşıyor.");

                    row[columns[index]] = value;
                }

                rows.Add(row);
            }

            return new ReportData
            {
                Columns = columns,
                Rows = rows,
                IsTruncated = isTruncated
            };
        }

        private static List<string> BuildUniqueColumnNames(SqlDataReader reader)
        {
            var columns = new List<string>(reader.FieldCount);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < reader.FieldCount; index++)
            {
                var baseName = string.IsNullOrWhiteSpace(reader.GetName(index))
                    ? $"Column{index + 1}"
                    : reader.GetName(index).Trim();

                counts.TryGetValue(baseName, out var count);
                count++;
                counts[baseName] = count;
                columns.Add(count == 1 ? baseName : $"{baseName}_{count}");
            }

            return columns;
        }

        private static object? NormalizeValue(object? value) => value switch
        {
            null or DBNull => null,
            byte[] bytes => Convert.ToBase64String(bytes),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            string or bool or byte or short or int or long or float or double or decimal or DateTime or DateTimeOffset or Guid => value,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };

        private static long EstimateSize(object? value) => value switch
        {
            null => 1,
            string text => Encoding.UTF8.GetByteCount(text),
            byte[] bytes => bytes.LongLength,
            _ => 32
        };
    }
}
