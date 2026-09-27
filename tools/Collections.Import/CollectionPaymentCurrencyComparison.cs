using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Services.Crm.Collections.Calculation;

/// <summary>İncelenmiş legacy view davranışını sabit kesitte karşılaştırır; SQL bağlantısı/yazması yoktur.</summary>
internal static class CollectionPaymentCurrencyComparison
{
    // Tanımlar değişirse eski SQL davranışını sessizce taklit etme: yeniden kod incelemesi gerekir.
    private static readonly Dictionary<string, string> DefinitionHashes = new()
    {
        ["Core.vContract"] = "1E1A43394410B1A33979E94B39777B0EAEB9F6F077633700AD44C5139CF96E54",
        ["Core.vContractHistory"] = "6AED7E0BE88A8DB3A2EC65A8257C0EAAB125A464312D76B48B2833C5EA9B5A58",
        ["Core.vCustomer"] = "4BD9CD415C15A8D930DCCBD691CEF3AD9D9048FD8D9497BF108DD6027CADF4D6",
        ["Core.vCustomerContractHistory"] = "7CD1853A97C767AA824A9B58C1CDF13F26AD8412CCE16C7874BBAE982A124ADA",
        ["Core.vPayment"] = "74C54F0719AE6890421BC2005D69DD3565CC99CDDF7AFC427BE932B69F6C8B3E",
        ["dbo.TarihTablosu"] = "7AE2059684555EF9EF8AF3695758D970C6C596176AA3D03BB04E35A27DFDE507",
        ["dbo.vPaymentCurrency"] = "7CCDDD7363305E121E22D6028196B95416D592A07E04A9ECCB564C1E5421CAA2",
        ["dbo.fnTahsilatTakibi"] = "D00B5878EF16E97805957C8D3483DDA3D2DFAFCA7E1C76B6B3FCDA7FB4A6B956",
        ["dbo.fnTahsilatTakibiGrup"] = "DE48A3626C31036AB23DDEEE9ADABB32EE611DF8FECF4797F483E911FB378F9D"
    };
    private static readonly CompareInfo Turkish = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
    private static readonly HashSet<string> HistoricalAmountObservations =
        ["FREE_PAYMENT_REVIEW", "NEGATIVE_AMOUNT_REVIEW", "ZERO_AMOUNT_REVIEW"];

    public static async Task<string> RunAsync(string reviewDirectory, string referencePath, string referenceHash)
    {
        reviewDirectory = Path.GetFullPath(reviewDirectory);
        var snapshotDirectory = Directory.GetParent(reviewDirectory)!.FullName;
        // İçerik hash doğrulamasından sonra da aynı byte dizileri okunur; TOCTOU yoktur.
        var referenceBytes = await File.ReadAllBytesAsync(referencePath);
        Require(Hash(referenceBytes) == referenceHash.ToUpperInvariant(), "Legacy kanıt dosyasının hash'i uyuşmuyor.");
        using var referenceDoc = JsonDocument.Parse(referenceBytes);
        var reference = referenceDoc.RootElement;
        Require(reference.GetArrayLength() == 5, "Legacy kanıtında beş sorgu sonucu gereklidir.");
        var definitions = reference[3].EnumerateArray().ToDictionary(x => Text(x, "SchemaName") + "." + Text(x, "ObjectName"));
        foreach (var (name, hash) in DefinitionHashes)
            Require(definitions.TryGetValue(name, out var definition) && Hash(Encoding.UTF8.GetBytes(Text(definition, "Definition")!)) == hash,
                $"{name} tanımı incelenen sürümden farklı; karşılaştırma durduruldu.");
        Require(Text(reference[2][0], "DatabaseCollation") == "Turkish_CI_AS", "Legacy karşılaştırma düzeni desteklenmiyor.");
        var sourceYear = checked((int)Id(reference[2][0], "SourceYear"));
        Require(sourceYear is >= 1753 and < 9998, "Legacy kaynak yılı geçersiz.");
        var openEnd = new DateOnly(sourceYear + 1, 12, 31);
        var currencies = reference[0].EnumerateArray().ToDictionary(x => Id(x, "CurrencyID"), x => Text(x, "Name"));
        var paymentTypes = reference[1].EnumerateArray().ToDictionary(x => Id(x, "PaymentTypeID"), x => Text(x, "Name"));
        using var summaryDoc = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(reviewDirectory, "summary.json")));
        var summary = summaryDoc.RootElement;
        Require(Text(summary, "ReviewVersion") == "payment-review-v1" && !summary.GetProperty("ReadyToApply").GetBoolean(), "Ön inceleme sürümü/geçiş kapısı geçersiz.");
        var batchId = Id(summary, "ContractBatchId");
        var maps = reference[4].EnumerateArray().Where(x => Id(x, "Status") == 1 && Id(x, "BatchId") == batchId)
            .ToDictionary(x => Id(x, "SourceId"), x => checked((int)Id(x, "TargetCurrencyTypeId")));
        Require(maps.Count > 0, "İnceleme batch'i için onaylı para birimi eşlemesi yok.");
        var manifestBytes = await File.ReadAllBytesAsync(Path.Combine(snapshotDirectory, "manifest.json"));
        Require(Hash(manifestBytes) == Text(summary, "ManifestHash"), "Ön inceleme ve kesit manifesti uyuşmuyor.");
        using var manifestDoc = JsonDocument.Parse(manifestBytes);
        var manifestFiles = manifestDoc.RootElement.GetProperty("files").EnumerateArray().ToDictionary(x => Text(x, "FileName")!);
        var sourceRows = new Dictionary<string, Dictionary<long, JsonElement>>();
        foreach (var (file, key) in new[] { ("customers.ndjson", "CustomerID"), ("contract-history.ndjson", "ContractHistoryID") })
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(snapshotDirectory, file));
            Require(Hash(bytes) == Text(manifestFiles[file], "Sha256"), "Karşılaştırma kaynak dosyası değişmiş.");
            var rows = new Dictionary<long, JsonElement>();
            using var reader = new StringReader(Encoding.UTF8.GetString(bytes));
            while (reader.ReadLine() is { } line)
            {
                using var row = JsonDocument.Parse(line);
                var id = Id(row.RootElement, key);
                Require(id > 0 && rows.TryAdd(id, row.RootElement.Clone()), "Kaynak kimliği geçersiz veya mükerrer.");
            }
            Require(rows.Count == Id(manifestFiles[file], "Count"), "Kaynak satır sayısı uyuşmuyor.");
            sourceRows[file] = rows;
        }
        var customers = sourceRows["customers.ndjson"];
        var histories = sourceRows["contract-history.ndjson"].Values.ToLookup(x => Id(x, "ContractID"));
        var cache = new Dictionary<(long Contract, DateOnly Period), LegacyResult>();
        LegacyResult Resolve(long contractId, DateOnly period)
        {
            if (cache.TryGetValue((contractId, period), out var cached)) return cached;
            var candidates = new List<PaymentCurrencyCandidate>();
            var sourceIds = new List<long>();
            var invalid = false;
            var shifted = false;
            foreach (var row in histories[contractId])
            {
                // vCustomerContractHistory'nin tek iç birleştirmesi tarihçenin müşterisidir.
                if (!customers.ContainsKey(Id(row, "CustomerID"))) continue;
                var process = Text(row, "ProcessType");
                if (process is null || SqlEqual(process, "Ücretsiz") || SqlEqual(process, "Hizmet Dondurma")) continue;
                var year = Id(row, "StartingDateYear");
                var month = Id(row, "StartingDateMonth");
                if (year is < 1753 or > 9999 || month is < 1 or > 12) { invalid = true; continue; }
                var from = new DateOnly((int)year, (int)month, 1);
                var name = paymentTypes.GetValueOrDefault(Id(row, "PaymentTypeID"));
                var interval = Interval(name);
                // Bilinmeyen SQL günlük/saatlik fallback'i otomatik ödeme adayı olarak kabul edilmez.
                if (interval is null) { invalid = true; continue; }
                if (from.Year < 2016)
                {
                    from = new DateOnly(2016, interval is 2 or 3 or 4 or 6 or 12 or 36 ? from.Month : 1, 1);
                    shifted = true;
                }
                var endText = Text(row, "EndDate");
                var end = openEnd;
                if (endText is not null)
                {
                    if (!DateTime.TryParseExact(endText, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endDate))
                    { invalid = true; continue; }
                    end = DateOnly.FromDateTime(endDate);
                }
                var distance = (period.Year - from.Year) * 12 + period.Month - from.Month;
                // TarihTablosu başlangıcı daima ayın 1'i; bitiş günü dahildir.
                if (distance < 0 || distance % interval.Value != 0 || period > end) continue;
                var sourceId = Id(row, "ContractHistoryID");
                var currencyId = Id(row, "CurrencyID");
                int? targetCurrency = currencies.TryGetValue(currencyId, out var currencyName) && !string.IsNullOrWhiteSpace(currencyName)
                    && maps.TryGetValue(currencyId, out var targetId) && targetId > 0 ? targetId : null;
                candidates.Add(new(sourceId, targetCurrency));
                sourceIds.Add(sourceId);
            }
            return cache[(contractId, period)] = new(PaymentCurrencyCandidateRules.Evaluate(candidates), sourceIds.ToArray(), invalid, shifted);
        }

        // Büyük ödeme raporu akışla okunur; hash doğrulamasından sona kadar başka yazara kapalıdır.
        await using var input = new FileStream(Path.Combine(reviewDirectory, "payments.ndjson"), FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(Convert.ToHexString(await SHA256.HashDataAsync(input)) == Text(summary, "PaymentReportHash"), "Ön inceleme satır raporu hash'i uyuşmuyor.");
        input.Position = 0;
        var outputDirectory = Path.Combine(reviewDirectory, $"currency-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var counts = new SortedDictionary<string, long>();
        var priorCounts = new SortedDictionary<string, long>();
        var totals = new SortedDictionary<int, (long Count, decimal Amount)>();
        long read = 0, compared = 0, candidatesCount = 0, newlyCleared = 0;
        await using (var writer = new StreamWriter(Path.Combine(outputDirectory, "comparison.ndjson"), false, new UTF8Encoding(false)))
        using (var reader = new StreamReader(input, new UTF8Encoding(false, true), leaveOpen: true))
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                read++;
                using var doc = JsonDocument.Parse(line);
                var row = doc.RootElement;
                if (Text(row, "Scope") != "RetainedMappedContract") continue;
                compared++;
                var periodText = Text(row, "Period");
                LegacyResult? legacy = null;
                var status = "InvalidPeriod";
                var currency = row.GetProperty("Currency");
                if (DateOnly.TryParseExact(periodText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var period))
                {
                    legacy = Resolve(Id(row, "SourceContractId"), period);
                    status = legacy.InvalidHistory ? "LegacyInvalidHistory"
                        : legacy.Result.Status != PaymentCurrencyCandidateStatus.SingleRateCurrency ? "Legacy" + legacy.Result.Status
                        : currency.ValueKind == JsonValueKind.Null || Id(currency, "CandidateCount") != 1 || Id(currency, "ResolvedCurrencyTypeId") == 0 ? "TargetNotSingleCurrency"
                        : Id(currency, "ResolvedCurrencyTypeId") == legacy.Result.ResolvedCurrencyTypeId ? "SingleCurrencyMatch" : "CurrencyMismatch";
                }
                var originalIssues = row.GetProperty("Issues").EnumerateArray().Select(x => x.GetString()!).ToArray();
                var remainingIssues = originalIssues.Where(x => !HistoricalAmountObservations.Contains(x)).ToArray();
                var prior = row.GetProperty("PreliminaryCandidate").GetBoolean();
                var candidate = status == "SingleCurrencyMatch" && remainingIssues.Length == 0;
                Increment(counts, status);
                if (prior) Increment(priorCounts, status);
                if (candidate)
                {
                    candidatesCount++;
                    if (!prior) newlyCleared++;
                    var id = legacy!.Result.ResolvedCurrencyTypeId!.Value;
                    var before = totals.GetValueOrDefault(id);
                    totals[id] = (before.Count + 1, before.Amount + row.GetProperty("Amount").GetDecimal());
                }
                await writer.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    PaymentId = Id(row, "PaymentId"), SourceContractId = Id(row, "SourceContractId"), Period = periodText,
                    Comparison = status, Legacy = legacy, PriorPreliminaryCandidate = prior,
                    HistoricalObservations = originalIssues.Where(HistoricalAmountObservations.Contains),
                    RemainingIssues = remainingIssues, PreliminaryCandidate = candidate, ReadyToApply = false
                }));
            }
        }
        Require(read == Id(summary, "PaymentCount") && compared == Id(summary.GetProperty("ScopeCounts"), "RetainedMappedContract"), "Karşılaştırma satır sayısı uyuşmuyor.");
        await using var report = File.OpenRead(Path.Combine(outputDirectory, "comparison.ndjson"));
        var result = new
        {
            Version = "legacy-payment-currency-v1", ReferenceHash = Hash(referenceBytes), SourceYear = sourceYear,
            InputReportHash = Text(summary, "PaymentReportHash"), ManifestHash = Text(summary, "ManifestHash"),
            ComparedPayments = compared, ComparisonCounts = counts, PriorCandidateComparisonCounts = priorCounts,
            PreliminaryCandidates = candidatesCount, NewlyClearedHistoricalObservations = newlyCleared,
            PreliminaryTotals = totals.Select(x => new { CurrencyTypeId = x.Key, x.Value.Count, x.Value.Amount }),
            ReportHash = Convert.ToHexString(await SHA256.HashDataAsync(report)), ReadyToApply = false,
            RemainingGates = new[] { "Fiziksel silme / kalıcı kaynak kimliği", "Uygulama öncesi güncel kaynak, tanım, eşleme ve hedef mutabakatı", "Legacy karşılaştırma istisnaları" }
        };
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "summary.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Legacy para birimi karşılaştırması: {compared} ödeme; ön aday {candidatesCount}. Veritabanına yazılmadı.");
        Console.WriteLine($"Rapor: {outputDirectory}");
        return outputDirectory;
    }

    private sealed record LegacyResult(PaymentCurrencyCandidateResult Result, long[] SourceHistoryIds, bool InvalidHistory, bool HasPre2016History);
    private static int? Interval(string? name)
    {
        foreach (var (label, months) in new[] { ("Aylık", 1), ("2 Aylık", 2), ("3 Aylık", 3), ("4 Aylık", 4), ("6 Aylık", 6), ("Yıllık", 12), ("2 Yıllık", 24), ("3 Yıllık", 36) })
            if (name is not null && SqlEqual(name, label)) return months;
        return null;
    }
    private static bool SqlEqual(string a, string b) => Turkish.Compare(a.TrimEnd(' '), b, CompareOptions.IgnoreCase) == 0;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string? Text(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
    private static long Id(JsonElement row, string key) => long.TryParse(Text(row, key)?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0;
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private static void Increment(IDictionary<string, long> counts, string key) => counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
}
