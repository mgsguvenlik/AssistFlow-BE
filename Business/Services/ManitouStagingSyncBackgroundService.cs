using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Dtos.Manitou;
using System.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Business.Services
{
    public sealed class ManitouStagingSyncBackgroundService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ManitouStagingSyncBackgroundService> _log;
        private readonly AppSettings _appSettings;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ManitouStagingSyncBackgroundService(
                IHttpClientFactory httpClientFactory,
                ILogger<ManitouStagingSyncBackgroundService> log)
        {
            _httpClientFactory = httpClientFactory;
            _log = log;

            var appSettings = ServiceTool.ServiceProvider
                .GetService<IOptionsSnapshot<AppSettings>>();

            if (appSettings == null)
                throw new InvalidOperationException("AppSettings servisinden okunamadı.");

            _appSettings = appSettings.Value;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {


            var delay = TimeSpan.FromMinutes(_appSettings.ManitouRunEveryMinutes <= 0 ? 60 : _appSettings.ManitouRunEveryMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunSyncAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Manitou staging sync genel hata aldı.");
                }

                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task RunSyncAsync(CancellationToken ct)
        {
            _log.LogInformation("Manitou staging sync başladı.");

            var token = await LoginAsync(ct);

            if (string.IsNullOrWhiteSpace(token))
            {
                _log.LogWarning("Manitou token alınamadı. Sync durduruldu.");
                return;
            }

            var groups = await GetCustomerGroupsAsync(token, ct);

            _log.LogInformation("Manitou customer group sayısı: {Count}", groups.Count);

            await UpsertCustomerGroupsAsync(groups, ct);

            foreach (var group in groups)
            {
                if (string.IsNullOrWhiteSpace(group.Id))
                    continue;

                try
                {
                    var customers = await GetCustomersByGroupCodeAsync(token, group.Id, ct);

                    _log.LogInformation(
                        "Manitou customer çekildi. GroupCode={GroupCode}, Count={Count}",
                        group.Id,
                        customers.Count);

                    await UpsertCustomersAsync(customers, ct);
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

        private HttpClient CreateClient(string? token = null)
        {
            var client = _httpClientFactory.CreateClient();

            client.BaseAddress = new Uri(_appSettings.ManitouBaseUrl.TrimEnd('/'));
            client.Timeout = TimeSpan.FromMinutes(5);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }

        private async Task<string?> LoginAsync(CancellationToken ct)
        {
            using var client = CreateClient();

            var request = new ManitouLoginRequest
            {
                Name = _appSettings.ManitouUserName,
                Password = _appSettings.ManitouPassword,
                AuthenticationType = _appSettings.ManitouAuthenticationType,
                AcceptedEula = false,
                NewPassword = ""
            };

            using var response = await client.PostAsJsonAsync(
                "/manitou/account/login",
                request,
                JsonOptions,
                ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogError(
                    "Manitou login başarısız. StatusCode={StatusCode}, Response={Response}",
                    response.StatusCode,
                    content);

                return null;
            }

            var loginResponse = JsonSerializer.Deserialize<ManitouLoginResponse>(content, JsonOptions);

            if (loginResponse == null || string.IsNullOrWhiteSpace(loginResponse.AccessToken))
            {
                _log.LogError("Manitou login response içinde access_token yok. Response={Response}", content);
                return null;
            }

            return loginResponse.AccessToken;
        }

        private async Task<List<ManitouContactResult>> GetCustomerGroupsAsync(
            string token,
            CancellationToken ct)
        {
            using var client = CreateClient(token);

            var body = new List<ManitouSearchRequestItem>
            {
                new()
                {
                    ContactType = 2,
                    Display = 110,
                    FieldNo = 21,
                    Table = 25,
                    Value = "*"
                }
            };

            var url = $"/api/contactsearch/2/search?maxRows={_appSettings.ManitouCustomerGroupMaxRows}&includeCancelled=false";

            using var response = await client.PostAsJsonAsync(url, body, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"CustomerGroups endpoint hata aldı. StatusCode={response.StatusCode}, Response={content}");
            }

            var result = JsonSerializer.Deserialize<ManitouSearchResponse>(content, JsonOptions);

            return result?.Results ?? new List<ManitouContactResult>();
        }

        private async Task<List<ManitouContactResult>> GetCustomersByGroupCodeAsync(
            string token,
            string groupCode,
            CancellationToken ct)
        {
            using var client = CreateClient(token);

            var body = new List<ManitouSearchRequestItem>
            {
                new()
                {
                    ContactType = 1,
                    Display = 180,
                    FieldNo = 4,
                    Table = 26,
                    Value = groupCode
                }
            };

            var url = $"/api/contactsearch/1/search?maxRows={_appSettings.ManitouCustomerMaxRows}&includeCancelled=false";

            using var response = await client.PostAsJsonAsync(url, body, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Customers endpoint hata aldı. GroupCode={groupCode}, StatusCode={response.StatusCode}, Response={content}");
            }

            var result = JsonSerializer.Deserialize<ManitouSearchResponse>(content, JsonOptions);

            return result?.Results ?? new List<ManitouContactResult>();
        }

        private async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct)
        {
            var connectionString = _appSettings.MSSQLConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("DefaultConnection connection string bulunamadı.");

            var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            return conn;
        }


        private async Task UpsertCustomerGroupsAsync(
            List<ManitouContactResult> groups,
            CancellationToken ct)
        {
            if (groups.Count == 0)
                return;

            await using var conn = await OpenConnectionAsync(ct);

            foreach (var item in groups)
            {
                await using var cmd = conn.CreateCommand();

                cmd.CommandText = @"
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

                AddParam(cmd, "@Id", SqlDbType.Int, item.SerialNo);
                AddParam(cmd, "@GroupName", SqlDbType.NVarChar, item.Name, 250);
                AddParam(cmd, "@Code", SqlDbType.NVarChar, item.Id, 100);
                AddParam(cmd, "@ParentGroupId", SqlDbType.Int, DBNull.Value);
                AddParam(cmd, "@DealerSerialNo", SqlDbType.Int, item.SerialNo);

                try
                {
                    await cmd.ExecuteNonQueryAsync(ct);

                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private async Task UpsertCustomersAsync(
            List<ManitouContactResult> customers,
            CancellationToken ct)
        {
            if (customers.Count == 0)
                return;

            await using var conn = await OpenConnectionAsync(ct);

            foreach (var item in customers)
            {
                var contPoint = NormalizeContPoint(item.ContPoint);
                var phone = IsEmail(contPoint) ? null : contPoint;
                var email = IsEmail(contPoint) ? contPoint : null;

                var dealerCode = item.DealerId;
                var customerTypeId = GetCustomerTypeIdByDealerCode(dealerCode);
                var tenantId = GetTenantIdByDealerCode(dealerCode);

                await using var cmd = conn.CreateCommand();

                cmd.CommandText = @"
                        MERGE stg.stg_Customers AS T
                        USING
                        (
                            SELECT
                                @Id AS Id,
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

                AddParam(cmd, "@Id", SqlDbType.Int, item.SerialNo);
                AddParam(cmd, "@SubscriberCode", SqlDbType.NVarChar, item.Id, 100);
                AddParam(cmd, "@SubscriberCompany", SqlDbType.NVarChar, item.Name, 250);
                AddParam(cmd, "@SubscriberAddress", SqlDbType.NVarChar, item.Addr1, -1);
                AddParam(cmd, "@City", SqlDbType.NVarChar, item.City, 100);
                AddParam(cmd, "@District", SqlDbType.NVarChar, DBNull.Value, 100);
                AddParam(cmd, "@LocationCode", SqlDbType.NVarChar, item.Region, 100);

                AddParam(cmd, "@ContactName1", SqlDbType.NVarChar, DBNull.Value, 150);
                AddParam(cmd, "@Phone1", SqlDbType.NVarChar, phone, 50);
                AddParam(cmd, "@Email1", SqlDbType.NVarChar, email, 150);

                AddParam(cmd, "@ContactName2", SqlDbType.NVarChar, DBNull.Value, 150);
                AddParam(cmd, "@Phone2", SqlDbType.NVarChar, DBNull.Value, 50);
                AddParam(cmd, "@Email2", SqlDbType.NVarChar, DBNull.Value, 150);

                AddParam(cmd, "@CustomerShortCode", SqlDbType.NVarChar, dealerCode, 100);
                AddParam(cmd, "@CorporateLocationId", SqlDbType.Int, DBNull.Value);
                AddParam(cmd, "@Longitude", SqlDbType.NVarChar, DBNull.Value, 50);
                AddParam(cmd, "@Latitude", SqlDbType.NVarChar, DBNull.Value, 50);
                AddParam(cmd, "@InstallationDate", SqlDbType.DateTime, DBNull.Value);

                AddParam(cmd, "@CustomerGroupId", SqlDbType.Int, await GetCustomerGroupIdByCodeAsync(conn, dealerCode, ct));
                AddParam(cmd, "@CustomerTypeId", SqlDbType.Int, customerTypeId);

                AddParam(cmd, "@WarrantyYears", SqlDbType.Int, DBNull.Value);
                AddParam(cmd, "@Note", SqlDbType.NVarChar, DBNull.Value, -1);
                AddParam(cmd, "@CashCenter", SqlDbType.NVarChar, DBNull.Value, 100);
                AddParam(cmd, "@LockType", SqlDbType.NVarChar, DBNull.Value, 100);

                AddParam(cmd, "@TenantId", SqlDbType.Int, tenantId);
                AddParam(cmd, "@MonitoringStatus", SqlDbType.Int, item.MonitoringStatus.HasValue ? item.MonitoringStatus.Value : DBNull.Value);
                AddParam(cmd, "@IsDeleted", SqlDbType.Bit, item.Hidden);

                await cmd.ExecuteNonQueryAsync(ct);
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

            return specialDealerCodes.Contains(code) ? 6 : 4;
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
            return (dealerCode ?? "").Trim().ToUpperInvariant();
        }

        private static async Task<object> GetCustomerGroupIdByCodeAsync(
            SqlConnection conn,
            string? dealerId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dealerId))
                return DBNull.Value;

            await using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT TOP 1 Id
                FROM stg.stg_CustomerGroups WITH (NOLOCK)
                WHERE Code = @Code;";

            AddParam(cmd, "@Code", SqlDbType.NVarChar, dealerId, 100);

            var result = await cmd.ExecuteScalarAsync(ct);

            return result ?? DBNull.Value;
        }

        private static string? NormalizeContPoint(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim().TrimStart('>').Trim();
        }

        private static bool IsEmail(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Contains('@');
        }

        private static void AddParam(
            SqlCommand cmd,
            string name,
            SqlDbType type,
            object? value,
            int? size = null)
        {
            var p = cmd.Parameters.Add(name, type);

            if (size.HasValue)
                p.Size = size.Value;

            p.Value = value ?? DBNull.Value;
        }
    }
}