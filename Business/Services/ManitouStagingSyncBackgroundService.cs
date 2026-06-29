using Business.Interfaces.Manitou;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Dtos.Manitou;
using System.Data;

namespace Business.Services
{
    public sealed class ManitouStagingSyncBackgroundService : BackgroundService
    {
        private readonly ILogger<ManitouStagingSyncBackgroundService> _log;
        private readonly AppSettings _appSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ManitouStagingSyncBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ManitouStagingSyncBackgroundService> log)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _log = log;

            var appSettings = ServiceTool.ServiceProvider
                .GetService<IOptionsSnapshot<AppSettings>>();

            if (appSettings == null)
                throw new InvalidOperationException("AppSettings servisinden okunamadı.");

            _appSettings = appSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var runEveryMinutes = _appSettings.ManitouRunEveryMinutes <= 0
                ? 60
                : _appSettings.ManitouRunEveryMinutes;

            var delay = TimeSpan.FromMinutes(runEveryMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunSyncAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Manitou staging sync genel hata aldı.");
                }

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task RunSyncAsync(CancellationToken ct)
        {
            _log.LogInformation("Manitou staging sync başladı.");

            await using var scope = _serviceScopeFactory.CreateAsyncScope();

            var manitouApiService = scope.ServiceProvider
                .GetRequiredService<IManitouApiService>();

            var token = await manitouApiService.LoginAsync(ct);

            if (string.IsNullOrWhiteSpace(token))
            {
                _log.LogWarning("Manitou token alınamadı. Sync durduruldu.");
                return;
            }

            var groups = await manitouApiService.GetCustomerGroupsAsync(token, ct);

            _log.LogInformation(
                "Manitou customer group sayısı: {Count}",
                groups.Count);

            await UpsertCustomerGroupsAsync(groups, ct);

            foreach (var group in groups)
            {
                if (string.IsNullOrWhiteSpace(group.Id))
                    continue;

                try
                {
                    var customers = await manitouApiService
                        .GetCustomersByGroupCodeAsync(token, group.Id, ct);

                    _log.LogInformation(
                        "Manitou customer çekildi. GroupCode={GroupCode}, Count={Count}",
                        group.Id,
                        customers.Count);

                    await UpsertCustomersAsync(customers, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exGroup)
                {
                    _log.LogError(
                        exGroup,
                        "Manitou customer çekme/insert hata aldı. GroupCode={GroupCode}",
                        group.Id);
                }
            }

            _log.LogInformation("Manitou staging sync tamamlandı.");
        }

        private async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct)
        {
            var connectionString = _appSettings.MSSQLConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "MSSQLConnectionString connection string bulunamadı.");
            }

            var connection = new SqlConnection(connectionString);

            await connection.OpenAsync(ct);

            return connection;
        }

        private async Task UpsertCustomerGroupsAsync(
            List<ManitouContactResult> groups,
            CancellationToken ct)
        {
            if (groups.Count == 0)
                return;

            await using var connection = await OpenConnectionAsync(ct);

            foreach (var item in groups)
            {
                await using var command = connection.CreateCommand();

                command.CommandText = @"
                    MERGE stg.stg_CustomerGroups AS T
                    USING
                    (
                        SELECT
                            @Id AS Id,
                            @GroupName AS GroupName,
                            @Code AS Code,
                            @ParentGroupId AS ParentGroupId,
                            @DealerSerialNo AS DealerSerialNo
                    ) AS S
                    ON T.Id = S.Id

                    WHEN MATCHED THEN
                        UPDATE SET
                            T.GroupName = S.GroupName,
                            T.Code = S.Code,
                            T.ParentGroupId = S.ParentGroupId,
                            T.DealerSerialNo = S.DealerSerialNo,
                            T.SyncStatus = 0,
                            T.SyncMessage = NULL,
                            T.ProcessedDate = NULL

                    WHEN NOT MATCHED THEN
                        INSERT
                        (
                            Id,
                            GroupName,
                            Code,
                            ParentGroupId,
                            DealerSerialNo,
                            SyncStatus,
                            SyncMessage,
                            ProcessedDate,
                            CreatedAtStg
                        )
                        VALUES
                        (
                            S.Id,
                            S.GroupName,
                            S.Code,
                            S.ParentGroupId,
                            S.DealerSerialNo,
                            0,
                            NULL,
                            NULL,
                            GETDATE()
                        );";

                AddParam(command, "@Id", SqlDbType.Int, item.SerialNo);
                AddParam(command, "@GroupName", SqlDbType.NVarChar, item.Name, 250);
                AddParam(command, "@Code", SqlDbType.NVarChar, item.Id, 100);
                AddParam(command, "@ParentGroupId", SqlDbType.Int, DBNull.Value);
                AddParam(command, "@DealerSerialNo", SqlDbType.Int, item.SerialNo);

                await command.ExecuteNonQueryAsync(ct);
            }
        }

        private async Task UpsertCustomersAsync(
            List<ManitouContactResult> customers,
            CancellationToken ct)
        {
            if (customers.Count == 0)
                return;

            await using var connection = await OpenConnectionAsync(ct);

            foreach (var item in customers)
            {
                var contPoint = NormalizeContPoint(item.ContPoint);

                var phone = IsEmail(contPoint)
                    ? null
                    : contPoint;

                var email = IsEmail(contPoint)
                    ? contPoint
                    : null;

                var dealerCode = item.DealerId;
                var customerTypeId = GetCustomerTypeIdByDealerCode(dealerCode);
                var tenantId = GetTenantIdByDealerCode(dealerCode);

                await using var command = connection.CreateCommand();

                command.CommandText = @"
                    MERGE stg.stg_Customers AS T
                    USING
                    (
                        SELECT
                            @Id AS Id,
                            @SerialNo AS SerialNo,
                            @SubscriberCode AS SubscriberCode,
                            @SubscriberCompany AS SubscriberCompany,
                            @SubscriberAddress AS SubscriberAddress,
                            @City AS City,
                            @District AS District,
                            @LocationCode AS LocationCode,
                            @ContactName1 AS ContactName1,
                            @Phone1 AS Phone1,
                            @Email1 AS Email1,
                            @ContactName2 AS ContactName2,
                            @Phone2 AS Phone2,
                            @Email2 AS Email2,
                            @CustomerShortCode AS CustomerShortCode,
                            @CorporateLocationId AS CorporateLocationId,
                            @Longitude AS Longitude,
                            @Latitude AS Latitude,
                            @InstallationDate AS InstallationDate,
                            @CustomerGroupId AS CustomerGroupId,
                            @CustomerTypeId AS CustomerTypeId,
                            @WarrantyYears AS WarrantyYears,
                            @Note AS Note,
                            @CashCenter AS CashCenter,
                            @LockType AS LockType,
                            @TenantId AS TenantId,
                            @MonitoringStatus AS MonitoringStatus,
                            @IsDeleted AS IsDeleted
                    ) AS S
                    ON T.Id = S.Id

                    WHEN MATCHED THEN
                        UPDATE SET
                            T.SerialNo = S.SerialNo,
                            T.SubscriberCode = S.SubscriberCode,
                            T.SubscriberCompany = S.SubscriberCompany,
                            T.SubscriberAddress = S.SubscriberAddress,
                            T.City = S.City,
                            T.District = S.District,
                            T.LocationCode = S.LocationCode,
                            T.ContactName1 = S.ContactName1,
                            T.Phone1 = S.Phone1,
                            T.Email1 = S.Email1,
                            T.ContactName2 = S.ContactName2,
                            T.Phone2 = S.Phone2,
                            T.Email2 = S.Email2,
                            T.CustomerShortCode = S.CustomerShortCode,
                            T.CorporateLocationId = S.CorporateLocationId,
                            T.Longitude = S.Longitude,
                            T.Latitude = S.Latitude,
                            T.InstallationDate = S.InstallationDate,
                            T.CustomerGroupId = S.CustomerGroupId,
                            T.CustomerTypeId = S.CustomerTypeId,
                            T.WarrantyYears = S.WarrantyYears,
                            T.Note = S.Note,
                            T.CashCenter = S.CashCenter,
                            T.LockType = S.LockType,
                            T.TenantId = S.TenantId,
                            T.MonitoringStatus = S.MonitoringStatus,
                            T.UpdatedDate = GETDATE(),
                            T.UpdatedUser = 'Api',
                            T.IsDeleted = S.IsDeleted,
                            T.SyncStatus = 0,
                            T.SyncMessage = NULL,
                            T.ProcessedDate = NULL

                    WHEN NOT MATCHED THEN
                        INSERT
                        (
                            Id,
                            SerialNo,
                            SubscriberCode,
                            SubscriberCompany,
                            SubscriberAddress,
                            City,
                            District,
                            LocationCode,
                            ContactName1,
                            Phone1,
                            Email1,
                            ContactName2,
                            Phone2,
                            Email2,
                            CustomerShortCode,
                            CorporateLocationId,
                            Longitude,
                            Latitude,
                            InstallationDate,
                            CustomerGroupId,
                            CustomerTypeId,
                            CreatedDate,
                            UpdatedDate,
                            CreatedUser,
                            UpdatedUser,
                            IsDeleted,
                            WarrantyYears,
                            Note,
                            CashCenter,
                            LockType,
                            TenantId,
                            MonitoringStatus,
                            SyncStatus,
                            SyncMessage,
                            ProcessedDate,
                            CreatedAtStg
                        )
                        VALUES
                        (
                            S.Id,
                            S.SerialNo,
                            S.SubscriberCode,
                            S.SubscriberCompany,
                            S.SubscriberAddress,
                            S.City,
                            S.District,
                            S.LocationCode,
                            S.ContactName1,
                            S.Phone1,
                            S.Email1,
                            S.ContactName2,
                            S.Phone2,
                            S.Email2,
                            S.CustomerShortCode,
                            S.CorporateLocationId,
                            S.Longitude,
                            S.Latitude,
                            S.InstallationDate,
                            S.CustomerGroupId,
                            S.CustomerTypeId,
                            GETDATE(),
                            GETDATE(),
                            'Api',
                            'Api',
                            S.IsDeleted,
                            S.WarrantyYears,
                            S.Note,
                            S.CashCenter,
                            S.LockType,
                            S.TenantId,
                            S.MonitoringStatus,
                            0,
                            NULL,
                            NULL,
                            GETDATE()
                        );";

                AddParam(command, "@Id", SqlDbType.Int, item.SerialNo);
                AddParam(command, "@SerialNo", SqlDbType.Int, item.SerialNo);

                AddParam(command, "@SubscriberCode", SqlDbType.NVarChar, item.Id, 100);
                AddParam(command, "@SubscriberCompany", SqlDbType.NVarChar, item.Name, 250);
                AddParam(command, "@SubscriberAddress", SqlDbType.NVarChar, item.Addr1, -1);

                AddParam(command, "@City", SqlDbType.NVarChar, item.City, 100);
                AddParam(command, "@District", SqlDbType.NVarChar, DBNull.Value, 100);
                AddParam(command, "@LocationCode", SqlDbType.NVarChar, item.Region, 100);

                AddParam(command, "@ContactName1", SqlDbType.NVarChar, DBNull.Value, 150);
                AddParam(command, "@Phone1", SqlDbType.NVarChar, phone, 50);
                AddParam(command, "@Email1", SqlDbType.NVarChar, email, 150);

                AddParam(command, "@ContactName2", SqlDbType.NVarChar, DBNull.Value, 150);
                AddParam(command, "@Phone2", SqlDbType.NVarChar, DBNull.Value, 50);
                AddParam(command, "@Email2", SqlDbType.NVarChar, DBNull.Value, 150);

                AddParam(command, "@CustomerShortCode", SqlDbType.NVarChar, dealerCode, 100);
                AddParam(command, "@CorporateLocationId", SqlDbType.Int, DBNull.Value);
                AddParam(command, "@Longitude", SqlDbType.NVarChar, DBNull.Value, 50);
                AddParam(command, "@Latitude", SqlDbType.NVarChar, DBNull.Value, 50);
                AddParam(command, "@InstallationDate", SqlDbType.DateTime, DBNull.Value);

                var customerGroupId = await GetCustomerGroupIdByCodeAsync(
                    connection,
                    dealerCode,
                    ct);

                AddParam(command, "@CustomerGroupId", SqlDbType.Int, customerGroupId);
                AddParam(command, "@CustomerTypeId", SqlDbType.Int, customerTypeId);

                AddParam(command, "@WarrantyYears", SqlDbType.Int, DBNull.Value);
                AddParam(command, "@Note", SqlDbType.NVarChar, DBNull.Value, -1);
                AddParam(command, "@CashCenter", SqlDbType.NVarChar, DBNull.Value, 100);
                AddParam(command, "@LockType", SqlDbType.NVarChar, DBNull.Value, 100);

                AddParam(command, "@TenantId", SqlDbType.Int, tenantId);

                AddParam(
                    command,
                    "@MonitoringStatus",
                    SqlDbType.Int,
                    item.MonitoringStatus.HasValue
                        ? item.MonitoringStatus.Value
                        : DBNull.Value);

                AddParam(command, "@IsDeleted", SqlDbType.Bit, item.Hidden);

                await command.ExecuteNonQueryAsync(ct);
            }
        }

        private static int GetCustomerTypeIdByDealerCode(string? dealerCode)
        {
            var code = NormalizeDealerCode(dealerCode);

            var specialDealerCodes = new HashSet<string>
            {
                "FINA",
                "FINB",
                "FINC",
                "FIND",
                "FIN",
                "YKB",
                "YKBA",
                "YKBB",
                "YKBC",
                "YKBK",
                "YKBD",
                "YKBE",
                "YKBM"
            };

            return specialDealerCodes.Contains(code)
                ? 6
                : 4;
        }

        private static int GetTenantIdByDealerCode(string? dealerCode)
        {
            var code = NormalizeDealerCode(dealerCode);

            var finansDealerCodes = new HashSet<string>
            {
                "FINA",
                "FINB",
                "FINC",
                "FIND",
                "FIN"
            };

            var ykbDealerCodes = new HashSet<string>
            {
                "YKB",
                "YKBA",
                "YKBB",
                "YKBC",
                "YKBD",
                "YKBE",
                "YKBM",
                "YKBK"
            };

            if (finansDealerCodes.Contains(code))
                return 4;

            if (ykbDealerCodes.Contains(code))
                return 2;

            return 3;
        }

        private static string NormalizeDealerCode(string? dealerCode)
        {
            return (dealerCode ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
        }

        private static async Task<object> GetCustomerGroupIdByCodeAsync(
            SqlConnection connection,
            string? dealerId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dealerId))
                return DBNull.Value;

            await using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT TOP 1 Id
                FROM stg.stg_CustomerGroups WITH (NOLOCK)
                WHERE Code = @Code;";

            AddParam(command, "@Code", SqlDbType.NVarChar, dealerId, 100);

            var result = await command.ExecuteScalarAsync(ct);

            return result ?? DBNull.Value;
        }

        private static string? NormalizeContPoint(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value
                .Trim()
                .TrimStart('>')
                .Trim();
        }

        private static bool IsEmail(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains('@');
        }

        private static void AddParam(
            SqlCommand command,
            string name,
            SqlDbType type,
            object? value,
            int? size = null)
        {
            var parameter = command.Parameters.Add(name, type);

            if (size.HasValue)
                parameter.Size = size.Value;

            parameter.Value = value ?? DBNull.Value;
        }
    }
}