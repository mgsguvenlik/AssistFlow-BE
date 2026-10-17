using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

// Copies customer cards only. Source is always AssistFlow; all writes are scoped to AssistFlowTest.
internal static class CustomerTestCopy
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static string Q(string s) => "[" + s.Replace("]", "]]") + "]";
    private static object? Value(DataRow row, string col) => row[col] is DBNull ? null : row[col];
    private static async Task<DataTable> Read(SqlConnection connection, string sql, SqlTransaction? tx = null)
    {
        using var cmd = new SqlCommand(sql, connection, tx) { CommandTimeout = 120 };
        using var reader = await cmd.ExecuteReaderAsync();
        var data = new DataTable(); data.Load(reader); return data;
    }
    private static async Task Execute(SqlConnection connection, SqlTransaction tx, string sql)
    {
        using var cmd = new SqlCommand(sql, connection, tx) { CommandTimeout = 120 }; await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RunAsync(string settingsPath, string? expectedHash)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var builder = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (builder.DataSource != "192.168.1.8" || builder.InitialCatalog != "AssistFlowTest") throw new InvalidOperationException("Yalnız AssistFlowTest hedefi kabul edilir.");
        await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
        await using var tx = expectedHash is null ? null : (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        if (tx is not null)
            await Execute(connection, tx, "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'AssistFlowTestCustomerCopy',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,N'Aktarım kilidi alınamadı.',1; SELECT COUNT_BIG(*) FROM dbo.Customers WITH(TABLOCKX,HOLDLOCK);");
        var metadata = await Read(connection, """
            SELECT c.name,t.name AS TypeName,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed
            FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id
            WHERE c.object_id=OBJECT_ID(N'dbo.Customers') ORDER BY c.column_id
            """, tx);
        var sourceMetadata = await Read(connection, """
            SELECT c.name,t.name AS TypeName,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed
            FROM AssistFlow.sys.columns c JOIN AssistFlow.sys.types t ON t.user_type_id=c.user_type_id
            WHERE c.object_id=OBJECT_ID(N'AssistFlow.dbo.Customers') ORDER BY c.column_id
            """, tx);
        string RowsJson(DataTable table) => JsonSerializer.Serialize(table.Rows.Cast<DataRow>().Select(r => table.Columns.Cast<DataColumn>().ToDictionary(c => c.ColumnName, c => Value(r, c.ColumnName))), Json);
        if (RowsJson(metadata) != RowsJson(sourceMetadata)) throw new InvalidOperationException("Kaynak ve hedef müşteri şeması farklı; otomatik aktarım durduruldu.");
        if ((await Read(connection, "SELECT name FROM sys.triggers WHERE parent_id=OBJECT_ID(N'dbo.Customers') AND is_disabled=0", tx)).Rows.Count != 0)
            throw new InvalidOperationException("Müşteri trigger'ı var; yan etkileri incelenmelidir.");
        var columns = metadata.Rows.Cast<DataRow>().Where(r => !(bool)r["is_identity"] && !(bool)r["is_computed"] && (string)r["TypeName"] != "timestamp").Select(r => (string)r["name"]).ToArray();
        var select = string.Join(",", columns.Select(c => "p." + Q(c)));
        var candidates = await Read(connection, $"""
            SELECT p.Id AS SourceId,{select},g.Code AS SourceGroupCode,ct.Code AS SourceTypeCode,tn.Code AS SourceTenantCode,
             cu.Code AS SourceCreatedUserCode,uu.Code AS SourceUpdatedUserCode
            FROM AssistFlow.dbo.Customers p
            LEFT JOIN AssistFlow.dbo.CustomerGroups g ON g.Id=p.CustomerGroupId
            LEFT JOIN AssistFlow.dbo.CustomerType ct ON ct.Id=p.CustomerTypeId
            LEFT JOIN AssistFlow.dbo.Tenants tn ON tn.Id=p.TenantId
            LEFT JOIN AssistFlow.dbo.Users cu ON cu.Id=p.CreatedUser
            LEFT JOIN AssistFlow.dbo.Users uu ON uu.Id=p.UpdatedUser
            WHERE p.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(p.SubscriberCode)),N'') IS NOT NULL
            AND NOT EXISTS(SELECT 1 FROM dbo.Customers t WHERE LTRIM(RTRIM(t.SubscriberCode)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS)
            ORDER BY p.Id
            """, tx);
        var duplicate = await Read(connection, """
            SELECT LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS AS Code
            FROM AssistFlow.dbo.Customers p WHERE p.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(p.SubscriberCode)),N'') IS NOT NULL
            AND NOT EXISTS(SELECT 1 FROM dbo.Customers t WHERE LTRIM(RTRIM(t.SubscriberCode)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS)
            GROUP BY LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS HAVING COUNT_BIG(*)>1
            """, tx);
        if (duplicate.Rows.Count != 0) throw new InvalidOperationException("Eksik abonelerde kaynak tekrarları var; müşteri kararı gerekir.");
        var groups = await Read(connection, "SELECT Id,Code,GroupName,ParentGroupId FROM dbo.CustomerGroups ORDER BY Id", tx);
        var sourceGroups = await Read(connection, "SELECT Id,Code,GroupName,ParentGroupId FROM AssistFlow.dbo.CustomerGroups ORDER BY Id", tx);
        var types = await Read(connection, "SELECT Id,Code,Name FROM dbo.CustomerType ORDER BY Id", tx);
        var tenants = await Read(connection, "SELECT Id,Code,IsDeleted FROM dbo.Tenants ORDER BY Id", tx);
        var users = await Read(connection, "SELECT Id,Code,IsDeleted FROM dbo.Users ORDER BY Id", tx);
        using var policy = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "../docs/data/collection-customer-classification.json")));
        var comparer = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true);
        long Match(DataTable table, string code)
        {
            var matches = table.Rows.Cast<DataRow>().Where(r => comparer.Equals(((string)r["Code"]).Trim(), code.Trim())).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"Tekil tanım bulunamadı: {table.TableName}/{code}");
            return (long)matches[0]["Id"];
        }
        bool Has(DataTable table, string code) => table.Rows.Cast<DataRow>().Any(r => comparer.Equals(((string)r["Code"]).Trim(), code.Trim()));
        var missingGroups = candidates.Rows.Cast<DataRow>().Select(r => Value(r,"SourceGroupCode") as string).Where(x => x != null && !Has(groups, x)).Distinct(comparer).ToArray();
        foreach (var code in missingGroups)
        {
            var source = sourceGroups.Rows.Cast<DataRow>().Single(r => comparer.Equals((string)r["Code"], code));
            if (source["ParentGroupId"] is not DBNull) throw new InvalidOperationException("Eksik grubun üst grubu var; ayrı inceleme gereklidir.");
        }
        var individual = policy.RootElement.GetProperty("individualCodes").EnumerateArray().Select(x => x.GetString()!).ToHashSet(comparer);
        var groupCodes = policy.RootElement.GetProperty("explicitGroupCodes").EnumerateArray().Concat(policy.RootElement.GetProperty("legacyVerifiedGroupCodes").EnumerateArray()).Select(x => x.GetString()!).ToHashSet(comparer);
        foreach (DataRow row in candidates.Rows)
        {
            if (Value(row,"TenantId") != null) row["TenantId"] = Match(tenants, (string?)Value(row,"SourceTenantCode") ?? throw new InvalidOperationException("Kaynak tenant eksik."));
            if (Value(row,"CustomerTypeId") != null)
            {
                var code = (string?)Value(row,"SourceTypeCode") ?? throw new InvalidOperationException("Kaynak müşteri tipi eksik.");
                var targetCode = code switch { "BRYSL01" => "N", "KRMSL01" => "G", "GRP01" => "GM", _ => code };
                var groupCode = (string?)Value(row,"SourceGroupCode");
                if (targetCode != "BNK01" && groupCode != null)
                    targetCode = individual.Contains(groupCode) ? "N" : groupCodes.Contains(groupCode) ? "GM" : targetCode;
                row["CustomerTypeId"] = Match(types, targetCode);
            }
            foreach (var field in new[] { "CreatedUser", "UpdatedUser" })
            {
                if (Value(row,field) == null) continue;
                var code = Value(row,"Source" + field + "Code") as string;
                // Source system rows use the audit sentinel 0; retain it without impersonating a user.
                row[field] = (long)row[field] == 0 ? 0L : Match(users, code ?? throw new InvalidOperationException("Kaynak kullanıcı eşlemesi eksik."));
            }
            var sourceGroup = Value(row,"SourceGroupCode") as string;
            if (sourceGroup != null && Has(groups, sourceGroup)) row["CustomerGroupId"] = Match(groups, sourceGroup);
        }
        var planJson = RowsJson(candidates);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(planJson + RowsJson(groups) + RowsJson(types) + RowsJson(tenants) + RowsJson(users) + RowsJson(sourceGroups) + policy.RootElement.GetRawText())));
        var reportDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "../tools/Collections.Import/snapshots", "customer-test-copy-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(reportDir);
        await File.WriteAllTextAsync(Path.Combine(reportDir,"plan.json"),planJson);
        await File.WriteAllTextAsync(Path.Combine(reportDir,"plan.sha256"),hash);
        Console.WriteLine($"Eksik müşteri: {candidates.Rows.Count}; eklenecek grup: {string.Join(", ",missingGroups)}; plan: {hash}; rapor: {reportDir}");
        if (expectedHash is null) return;
        if (expectedHash != hash) throw new InvalidOperationException("Plan değişti; eski hash ile aktarım yapılmadı.");
        var beforeCount = (long)(await Read(connection,"SELECT COUNT_BIG(*) AS Total FROM dbo.Customers",tx)).Rows[0]["Total"];
        foreach (var code in missingGroups)
        {
            var source = sourceGroups.Rows.Cast<DataRow>().Single(r => comparer.Equals((string)r["Code"],code));
            using var cmd = new SqlCommand("INSERT dbo.CustomerGroups(Code,GroupName,ParentGroupId) OUTPUT inserted.Id VALUES(@code,@name,NULL)",connection,tx);
            cmd.Parameters.AddWithValue("@code",code!);cmd.Parameters.AddWithValue("@name",source["GroupName"]);
            var id = (long)(await cmd.ExecuteScalarAsync())!;
            foreach (DataRow row in candidates.Rows) if (comparer.Equals(Value(row,"SourceGroupCode") as string, code)) row["CustomerGroupId"] = id;
        }
        var columnSql = string.Join(",",columns.Select(Q));
        await Execute(connection,tx!,$"SELECT TOP(0) {columnSql} INTO #CustomerCopy FROM dbo.Customers;");
        var transfer = candidates.DefaultView.ToTable(false,columns);
        using (var bulk = new SqlBulkCopy(connection,SqlBulkCopyOptions.CheckConstraints,tx))
        {
            bulk.DestinationTableName="#CustomerCopy";bulk.BulkCopyTimeout=120;
            foreach(var column in columns) bulk.ColumnMappings.Add(column,column);
            await bulk.WriteToServerAsync(transfer);
        }
        var inserted = await Read(connection,$"INSERT dbo.Customers({columnSql}) OUTPUT inserted.Id,inserted.SubscriberCode SELECT {columnSql} FROM #CustomerCopy",tx);
        if(inserted.Rows.Count!=candidates.Rows.Count) throw new InvalidOperationException("Aktarım sayısı uyuşmadı.");
        var mismatch = await Read(connection,$"SELECT {columnSql} FROM #CustomerCopy EXCEPT SELECT {string.Join(",",columns.Select(c=>"t."+Q(c)))} FROM dbo.Customers t JOIN #CustomerCopy p ON t.SubscriberCode=p.SubscriberCode",tx);
        if(mismatch.Rows.Count!=0) throw new InvalidOperationException("Alan doğrulaması başarısız.");
        var remaining = (long)(await Read(connection,"""
            SELECT COUNT_BIG(*) AS Total FROM AssistFlow.dbo.Customers p WHERE p.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(p.SubscriberCode)),N'') IS NOT NULL
            AND NOT EXISTS(SELECT 1 FROM dbo.Customers t WHERE LTRIM(RTRIM(t.SubscriberCode)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS)
            """,tx)).Rows[0]["Total"];
        var afterCount = (long)(await Read(connection,"SELECT COUNT_BIG(*) AS Total FROM dbo.Customers",tx)).Rows[0]["Total"];
        if(remaining!=0 || afterCount!=beforeCount+candidates.Rows.Count) throw new InvalidOperationException("Son sayım uyuşmadı.");
        await File.WriteAllTextAsync(Path.Combine(reportDir,"inserted.json"),RowsJson(inserted));
        await File.WriteAllTextAsync(Path.Combine(reportDir,"transferred-values.json"),RowsJson(transfer));
        await tx!.CommitAsync();
        await File.WriteAllTextAsync(Path.Combine(reportDir,"result.json"),JsonSerializer.Serialize(new { Status="Committed", Inserted=inserted.Rows.Count, NewGroups=missingGroups, Before=beforeCount, After=afterCount, Remaining=remaining, PlanHash=hash },Json));
        Console.WriteLine($"Aktarım tamamlandı: {inserted.Rows.Count} müşteri; test {beforeCount} → {afterCount}; kalan eksik {remaining}; kaynak salt okunur.");
    }
}
