-- Salt-okunur kapsam profili; aktarim onayi veya sabit kesit degildir.
-- AssistFlowTest baglantisi ve ayni sunucudaki MGS kaynagi icin.
-- Para birimi cozulmeden tutarlar toplanmaz. Kisi bilgisi dondurulmez.
SELECT DB_NAME() AS TargetDatabase,
    (SELECT COUNT_BIG(*) FROM MGS.Core.Payment) AS SourcePayments,
    (SELECT COUNT_BIG(*) FROM collection.Contract) AS TargetContracts,
    (SELECT COUNT_BIG(*) FROM collection.ContractRatePeriod) AS TargetRates,
    (SELECT COUNT_BIG(*) FROM collection.Payment) AS TargetPayments,
    (SELECT COUNT_BIG(*) FROM collection.ContractAttachment WHERE IsDeleted = 0) AS TargetAttachments
WHERE DB_NAME() = N'AssistFlowTest';

WITH sourceState AS (
    SELECT TRY_CONVERT(bigint, src.SourceId) AS LegacyContractId, stage.Status,
        map.TargetContractId, target.Id AS ExistingTargetId, target.IsDeleted AS TargetDeleted
    FROM collection.MigrationSourceRow src
    JOIN collection.MigrationContractStage stage ON stage.SourceRowId = src.Id
    LEFT JOIN collection.MigrationMap map ON map.SourceSystem = N'MGS'
        AND map.EntityCode = N'Contract' AND map.SourceId = src.SourceId
    LEFT JOIN collection.Contract target ON target.Id = map.TargetContractId
    WHERE src.BatchId = 1 AND src.EntityCode = N'Contract'
), payments AS (
    SELECT p.PaymentID, p.ContractID, p.CustomerID, p.Amount, p.Date,
        p.PeriodMonth, p.PeriodYear, p.Free,
        c.CustomerID AS ContractCustomerId,
        CASE
            WHEN c.ContractID IS NULL THEN 'MissingLegacyContract'
            WHEN s.Status = 5 THEN 'ExcludedContract'
            WHEN s.Status = 3 AND s.ExistingTargetId IS NOT NULL AND s.TargetDeleted = 0 THEN 'RetainedMappedContract'
            WHEN s.Status = 3 THEN 'AppliedTargetMismatch'
            WHEN s.LegacyContractId IS NOT NULL THEN 'WaitingContract'
            ELSE 'OutsideContractSnapshot'
        END AS Scope
    FROM MGS.Core.Payment p
    LEFT JOIN MGS.Core.Contract c ON c.ContractID = p.ContractID
    LEFT JOIN sourceState s ON s.LegacyContractId = p.ContractID
    WHERE DB_NAME() = N'AssistFlowTest'
)
SELECT Scope, COUNT_BIG(*) AS Payments, COUNT(DISTINCT ContractID) AS Contracts,
    SUM(CONVERT(bigint, CASE WHEN CustomerID IS NULL OR ContractCustomerId IS NULL OR CustomerID <> ContractCustomerId THEN 1 ELSE 0 END)) AS OwnershipIssues,
    SUM(CONVERT(bigint, CASE WHEN Amount IS NULL THEN 1 ELSE 0 END)) AS NullAmounts,
    SUM(CONVERT(bigint, CASE WHEN Amount = 0 THEN 1 ELSE 0 END)) AS ZeroAmounts,
    SUM(CONVERT(bigint, CASE WHEN Amount < 0 THEN 1 ELSE 0 END)) AS NegativeAmounts,
    SUM(CONVERT(bigint, CASE WHEN Date IS NULL THEN 1 ELSE 0 END)) AS MissingPaymentDates,
    SUM(CONVERT(bigint, CASE WHEN TRY_CONVERT(int, PeriodMonth) BETWEEN 1 AND 12
        AND TRY_CONVERT(int, PeriodYear) BETWEEN 1 AND 9998 THEN 0 ELSE 1 END)) AS InvalidPeriods
FROM payments GROUP BY Scope ORDER BY Scope;

SELECT CONVERT(nvarchar(100), p.Free) AS FreeValue, COUNT_BIG(*) AS Payments
FROM MGS.Core.Payment p
JOIN collection.MigrationSourceRow src ON src.BatchId = 1 AND src.EntityCode = N'Contract'
    AND TRY_CONVERT(bigint, src.SourceId) = p.ContractID
JOIN collection.MigrationContractStage stage ON stage.SourceRowId = src.Id AND stage.Status = 3
JOIN collection.MigrationMap map ON map.SourceSystem = N'MGS' AND map.EntityCode = N'Contract' AND map.SourceId = src.SourceId
JOIN collection.Contract target ON target.Id = map.TargetContractId AND target.IsDeleted = 0
WHERE DB_NAME() = N'AssistFlowTest'
GROUP BY CONVERT(nvarchar(100), p.Free);

-- Kimlik tekilligi, kapsam sayimlarinda coklama olmadigini kontrol eder.
SELECT COUNT_BIG(*) AS StageContracts, COUNT(DISTINCT TRY_CONVERT(bigint, src.SourceId)) AS UniqueLegacyIds
FROM collection.MigrationSourceRow src
JOIN collection.MigrationContractStage stage ON stage.SourceRowId = src.Id
WHERE src.BatchId = 1 AND src.EntityCode = N'Contract' AND DB_NAME() = N'AssistFlowTest';
