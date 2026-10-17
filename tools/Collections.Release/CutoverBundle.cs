using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Offline review bundle, not a production importer. No SQL/client/application instance is created.
internal static class CutoverBundle
{
    private static JsonElement Read(string path) { using var d = JsonDocument.Parse(File.ReadAllText(path)); return d.RootElement.Clone(); }
    private static string Hash(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    private static string Text(JsonElement row, string name) => row.GetProperty(name).ToString();
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static string Private(string path)
    {
        var full = Path.GetFullPath(path);
        Require(full.StartsWith(Path.GetFullPath(".tools") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Girdi/çıktı yalnız özel .tools altında olmalıdır.");
        return full;
    }
    private static bool SameState(JsonElement a, JsonElement b) => a.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString()).OrderBy(x => x.Key)
        .SequenceEqual(b.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString()).OrderBy(x => x.Key));
    private static void CopyPackage(string source, string target)
    {
        CustomerDecisionReplay.VerifyPackage(source); Directory.CreateDirectory(target);
        foreach (var row in Read(Path.Combine(source, "package-hashes.json")).EnumerateArray())
            File.Copy(Path.Combine(source, Text(row, "File")), Path.Combine(target, Text(row, "File")));
        File.Copy(Path.Combine(source, "package-hashes.json"), Path.Combine(target, "package-hashes.json"));
        CustomerDecisionReplay.VerifyPackage(target);
    }
    private static Task Save(string root, string name, object value) => File.WriteAllTextAsync(Path.Combine(root, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

    public static async Task RunAsync(string capturePath, string reviewPath, string dataPath, string profilePath, string legacyPath, string schemaPath, string outputPath)
    {
        var capture = Private(capturePath); var review = Private(reviewPath); var data = Private(dataPath); var output = Private(outputPath);
        profilePath = Private(profilePath); legacyPath = Private(legacyPath); schemaPath = Private(schemaPath);
        Require(!Directory.Exists(output) && !File.Exists(output), "Mevcut yayın kanıtı üzerine yazılmaz.");
        foreach (var p in new[] { capture, review, data }) CustomerDecisionReplay.VerifyPackage(p);
        var captureManifest = Read(Path.Combine(capture, "capture-manifest.json"));
        Require(Text(captureManifest, "TargetDatabase") == "AssistFlow" && !captureManifest.GetProperty("RehearsalOnly").GetBoolean()
            && captureManifest.GetProperty("AllObservedHashesStable").GetBoolean(), "Yalnız sabit ve açık canlı referans kesiti paketlenebilir.");
        var manifest = Read(Path.Combine(data, "manifest.json"));
        Require(Text(manifest, "Status") == "BLOCKED_REVIEW_PACKAGE_NOT_APPROVED_FOR_EXECUTION"
            && Text(manifest, "CapturePackageHash") == Hash(Path.Combine(capture, "package-hashes.json"))
            && Text(manifest, "ReviewPackageHash") == Hash(Path.Combine(review, "package-hashes.json"))
            && Text(manifest, "PaymentPlanFormat") == "legacy-reconciled-source-plan-v1"
            && Text(manifest, "RelationReconciliationPackageHash").Length == 64, "Canlı veri paketi zinciri eksik/farklı.");
        var profile = Read(profilePath); var legacy = Read(legacyPath); var schema = Read(Path.Combine(schemaPath, "manifest.json"));
        Require(profile.EnumerateArray().Any(x => Text(x, "Section") == "Environment" && Text(x, "DatabaseName") == "AssistFlow")
            && legacy.EnumerateArray().Any(x => Text(x, "Section") == "LegacyEnvironment" && Text(x, "DatabaseName") == "MGS")
            && Text(schema, "targetDatabase") == "AssistFlow" && Text(schema, "profileSha256") == Hash(profilePath), "Hedef/şema ön kontrol zinciri farklı.");
        Require(schema.GetProperty("pendingNonCollection").GetArrayLength() == 0, "Önce bekleyen ortak migrationlar değerlendirilmelidir.");
        foreach (var f in schema.GetProperty("files").EnumerateArray())
            Require(Path.GetFileName(Text(f, "file")) == Text(f, "file") && Hash(Path.Combine(schemaPath, Text(f, "file"))) == Text(f, "sha256"), "Şema hash/yol hatası.");

        // Reproduce the exact transformations used by Prepare-CollectionRehearsalSchema.ps1.
        // A passed old rehearsal is not evidence for newly changed migration or seed bytes.
        var rehearsedSqlPath = Private(".tools/collection-rehearsal-schema-2026-10-17-01");
        Require(Hash(Path.Combine(rehearsedSqlPath, "manifest.json")) == "CD5DE611AEB1D99CC6D394C2BBA1D10930485DA220899AA3B7ABA64BAD6FDC39", "Prova SQL manifesti değişmiş.");
        var rehearsedSql = Read(Path.Combine(rehearsedSqlPath, "manifest.json")).GetProperty("Files").EnumerateArray().ToDictionary(x => Text(x, "File"));
        var sqlInputs = schema.GetProperty("files").EnumerateArray().Select(x => (Name: Text(x, "file"), Path: Path.Combine(schemaPath, Text(x, "file")), Seed: false))
            .Concat(new[] { "30-definition-seed.sql", "40-menu-sms-seed.sql" }.Select(x => (Name: x, Path: Path.Combine("docs/releases/collections-2026-10-15", x), Seed: true)));
        const string guard = "SET XACT_ABORT ON;\nIF DB_NAME()<>N'AssistFlow_Performance_Restore' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' THROW 51000,N'Yalnız onaylı prova hedefi kullanılabilir.',1;\n";
        foreach (var input in sqlInputs)
        {
            var sql = File.ReadAllText(input.Path);
            if (input.Seed)
            {
                Require(sql.Contains("DECLARE @Apply bit=0;"), "Canlı seed varsayılanı kapalı olmalıdır.");
                sql = sql.Replace("DB_NAME()<>N'AssistFlow'", "DB_NAME()<>N'AssistFlow_Performance_Restore'").Replace("DECLARE @Apply bit=0;", "DECLARE @Apply bit=1; -- Yalnız bu onaylı prova kopyasında açık; production dosyası değişmedi.");
            }
            else sql = sql.Replace("DB_NAME() <> N'AssistFlow'", "DB_NAME() <> N'AssistFlow_Performance_Restore'");
            var transformed = string.Join("\nGO\n", Regex.Split(sql, @"(?im)^\s*GO\s*(?:--[^\r\n]*)?\r?$").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => guard + x));
            Require(rehearsedSql.TryGetValue(input.Name, out var proof) && Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(transformed))) == Text(proof, "Sha256")
                && Hash(Path.Combine(rehearsedSqlPath, input.Name)) == Text(proof, "Sha256"), "SQL/seed gerçekleşmiş provayla aynı değil; yeni prova gerekir.");
        }

        var proofs = new Dictionary<string, (string Path, string Hash)>
        {
            ["rollback.json"] = ("collection-rehearsal-transfer-rollback-2026-10-17-02/result.json", "1D55BEBEAFA0EA217797A33EDB28730561A93BF8154FA390CB59FCEB16093FD3"),
            ["apply.json"] = ("collection-rehearsal-transfer-apply-2026-10-17-01/result.json", "5DC12A7057A5DED4268BBC9C424D0F3B5C84CE155E04318BA0F91FC4023E5207"),
            ["replay.json"] = ("collection-rehearsal-transfer-replay-2026-10-17-01/result.json", "BB78DC4FB0DB6A6F91C14A9ED06C68F47AD10591B2F69946B0EB555345ED1B85"),
            ["resilience.json"] = ("collection-rehearsal-transfer-resilience-2026-10-17-01/result.json", "0A94F16D0CDDE8F6C5C61DBF9AD5A2804C71A4FF1417FF25F82E9423BA3DBCD9"),
            ["postflight.json"] = ("collection-rehearsal-data-after-2026-10-17-01.json", "F656D61F9DBCF9568F8AF149722D2AB09522AAE8517FC2EFE4CF20203FA00A5E")
        };
        foreach (var (_, proof) in proofs) Require(Hash(Private(Path.Combine(".tools", proof.Path))) == proof.Hash, "Gerçekleşmiş prova kanıtı değişmiş.");
        var apply = Read(Path.Combine(".tools", proofs["apply.json"].Path)); var replay = Read(Path.Combine(".tools", proofs["replay.json"].Path));
        var rollback = Read(Path.Combine(".tools", proofs["rollback.json"].Path)); var resilience = Read(Path.Combine(".tools", proofs["resilience.json"].Path));
        Require(Text(apply, "Mode") == "COMMITTED_REHEARSAL_IMPORT" && Text(replay, "Inserted") == "0" && SameState(replay.GetProperty("Before"), replay.GetProperty("After"))
            && SameState(rollback.GetProperty("Before"), rollback.GetProperty("After")) && SameState(apply.GetProperty("After"), resilience.GetProperty("After"))
            && resilience.GetProperty("Checks").GetArrayLength() == 4 && resilience.GetProperty("Checks").EnumerateArray().All(x => x.GetProperty("Passed").GetBoolean() && x.GetProperty("RolledBack").GetBoolean()), "Prova kabul kanıtı tamamlanmamış.");
        var previousCapture = Path.GetFullPath(".tools/collection-rehearsal-source-2026-10-17-01"); CustomerDecisionReplay.VerifyPackage(previousCapture);
        var observedFiles = captureManifest.GetProperty("Files").EnumerateArray().ToArray();
        Require(observedFiles.All(x => Path.GetFileName(Text(x, "File")) == Text(x, "File")), "Kesit dosya yolu geçersiz.");
        var deltas = observedFiles.Select(x => new { File = Text(x, "File"), RehearsedHash = Hash(Path.Combine(previousCapture, Text(x, "File"))), CurrentHash = Hash(Path.Combine(capture, Text(x, "File"))), Rows = x.GetProperty("Rows").GetInt64() }).ToArray();

        Directory.CreateDirectory(output);
        CopyPackage(capture, Path.Combine(output, "source")); CopyPackage(review, Path.Combine(output, "review")); CopyPackage(data, Path.Combine(output, "data"));
        var schemaOutput = Path.Combine(output, "schema"); Directory.CreateDirectory(schemaOutput);
        foreach (var f in schema.GetProperty("files").EnumerateArray()) File.Copy(Path.Combine(schemaPath, Text(f, "file")), Path.Combine(schemaOutput, Text(f, "file")));
        File.Copy(Path.Combine(schemaPath, "manifest.json"), Path.Combine(schemaOutput, "manifest.json"));
        var releaseRoot = "docs/releases/collections-2026-10-15";
        foreach (var name in new[] { "00-preflight.sql", "01-legacy-preflight.sql", "30-definition-seed.sql", "40-menu-sms-seed.sql", "50-postflight.sql" }) File.Copy(Path.Combine(releaseRoot, name), Path.Combine(output, name));
        File.Copy("docs/releases/collections-2026-10-17/cutover-runbook.md", Path.Combine(output, "RUNBOOK.md"));
        var proofOutput = Path.Combine(output, "rehearsal-proof"); Directory.CreateDirectory(proofOutput);
        foreach (var (name, proof) in proofs) File.Copy(Path.Combine(".tools", proof.Path), Path.Combine(proofOutput, name));
        File.Copy(profilePath, Path.Combine(output, "live-preflight.json")); File.Copy(legacyPath, Path.Combine(output, "mgs-preflight.json"));
        await Save(output, "rehearsal-delta.json", deltas);
        var jobs = legacy.EnumerateArray().Where(x => Text(x, "Section") == "AgentJobs").Select(x => new { Evidence = x, StopApproved = false, ResumeApproved = false, Action = "REVIEW_EXACT_STEP_DEPENDENCIES_BEFORE_MAINTENANCE; DO_NOT_DISABLE_ENTIRE_INSTANCE" });
        await Save(output, "job-review-matrix.json", jobs.ToArray());
        var gates = new[] { "FINAL_MAINTENANCE_CAPTURE_AND_TARGET_RECHECK", "PRODUCTION_SPECIFIC_APPLICATOR_AND_REHEARSAL", "SHARED_DBO_AND_SYNC_PROCEDURE_APPROVAL", "OPERATOR_AND_ROLE_CODES",
            "JOB_STEP_QUIESCE_APPROVAL", "BACKEND_STARTUP_GENERAL_SEED_AND_IMMEDIATE_MANITOU_SYNC", "PRODUCTION_CDN_SECURITY_AND_READBACK", "K10_K11_RELEASE_SCOPE_ACCEPTANCE", "BACKUP_PIT_ROLLBACK_AND_USER_ACCEPTANCE" };
        await Save(output, "approvals.required.json", new { ProductionApplyApproved = false, OperatorCode = "", RoleCodes = Array.Empty<string>(), FinalMaintenanceCapture = false, Gates = gates.Select(x => new { Code = x, Approved = false }).ToArray() });
        await Save(output, "manifest.json", new { Status = "SEALED_REVIEW_BUNDLE_NOT_EXECUTION_AUTHORIZATION", TargetDatabase = "AssistFlow", SourceDatabase = "MGS", CreatedAtUtc = DateTimeOffset.UtcNow,
            ProductionApplyAllowed = false, ContainsProductionImporter = false, RehearsalProofVerified = true, SchemaAndSeedsMatchRehearsal = true, FinalMaintenanceSnapshot = false,
            GeneratorSourceHash = Hash("tools/Collections.Release/CutoverBundle.cs"), GeneratorAssemblyHash = Hash(typeof(CutoverBundle).Assembly.Location),
            CapturePackageHash = Hash(Path.Combine(capture, "package-hashes.json")), DataPackageHash = Hash(Path.Combine(data, "package-hashes.json")), SchemaManifestHash = Hash(Path.Combine(schemaPath, "manifest.json")),
            Contracts = manifest.GetProperty("Contracts"), Payments = manifest.GetProperty("Payments"), SourceChangesSinceRehearsal = deltas.Where(x => !x.File.StartsWith("live-") && x.RehearsedHash != x.CurrentHash).Select(x => x.File).ToArray(),
            TargetReferenceChangesSinceRehearsal = deltas.Where(x => x.File.StartsWith("live-") && x.RehearsedHash != x.CurrentHash).Select(x => x.File).ToArray(), Gates = gates });
        await Save(output, "bundle-hashes.json", Directory.GetFiles(output, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(x => new { File = Path.GetRelativePath(output, x).Replace('\\', '/'), Sha256 = Hash(x) }).ToArray());
        Console.WriteLine($"Kapalı canlı inceleme paketi hazır. Manifest SHA-256: {Hash(Path.Combine(output, "bundle-hashes.json"))}. SQL/CDN/Job/deploy çalıştırılmadı; production importer ve son onay kapıları ayrı.");
    }
}
