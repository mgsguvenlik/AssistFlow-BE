-- AssistFlowTest: aktarım sonrası bağımsız salt-okunur mutabakat.
SELECT DB_NAME() AS DatabaseName,
    (SELECT COUNT_BIG(*) FROM collection.Contract) AS Contracts,
    (SELECT COUNT_BIG(*) FROM collection.ContractRatePeriod) AS RatePeriods,
    (SELECT COUNT_BIG(*) FROM collection.ContractAttachment WHERE IsDeleted=0) AS Attachments,
    (SELECT COUNT_BIG(*) FROM collection.Payment) AS Payments;
SELECT CurrencyTypeId,COUNT_BIG(*) AS PaymentCount,SUM(Amount) AS Amount,
    SUM(CASE WHEN IsFree=1 THEN 1 ELSE 0 END) AS FreeCount,
    SUM(CASE WHEN Amount<0 THEN 1 ELSE 0 END) AS NegativeCount,
    SUM(CASE WHEN Amount=0 THEN 1 ELSE 0 END) AS ZeroCount
FROM collection.Payment GROUP BY CurrencyTypeId ORDER BY CurrencyTypeId;
SELECT b.Id,b.SnapshotKey,b.Status,COUNT_BIG(s.Id) AS SourceRows
FROM collection.MigrationBatch b LEFT JOIN collection.MigrationSourceRow s ON s.BatchId=b.Id AND s.EntityCode='Payment'
WHERE b.RuleVersion='payment-transfer-v1' GROUP BY b.Id,b.SnapshotKey,b.Status;
SELECT COUNT_BIG(*) AS ImportReceipts,
    SUM(CASE WHEN p.Id IS NULL THEN 1 ELSE 0 END) AS DeletedPayments
FROM collection.PaymentOperation o LEFT JOIN collection.Payment p ON p.Id=o.PaymentId
WHERE o.ActorUserId=0 AND o.Kind=0;
SELECT COUNT_BIG(*) AS PaymentForeignKeyMaps FROM collection.MigrationMap WHERE EntityCode='Payment';
