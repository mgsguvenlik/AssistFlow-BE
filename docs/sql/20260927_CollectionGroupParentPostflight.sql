-- AssistFlowTest: yalniz okuma, K03 sonrasi mutabakat.
SELECT DB_NAME() AS DatabaseName,
 (SELECT COUNT(*) FROM collection.GroupParent) AS ParentCount,
 (SELECT COUNT(*) FROM collection.Contract) AS ContractCount,
 (SELECT COUNT(*) FROM collection.ContractRatePeriod) AS RateCount,
 (SELECT COUNT(*) FROM collection.Payment) AS PaymentCount,
 (SELECT COUNT(*) FROM collection.ContractAttachment) AS AttachmentCount;
SELECT t.Code,COUNT(*) AS CustomerCount FROM dbo.Customers c
JOIN dbo.CustomerType t ON t.Id=c.CustomerTypeId
JOIN collection.GroupParent p ON p.CustomerId=c.Id GROUP BY t.Code;
SELECT COUNT(*) AS OwnershipMismatch FROM collection.GroupParent p
JOIN dbo.Customers c ON c.Id=p.CustomerId
JOIN dbo.CustomerType t ON t.Id=c.CustomerTypeId
WHERE c.IsDeleted=1 OR c.CustomerGroupId<>p.CustomerGroupId OR t.Code<>'G' OR c.TenantId IS NOT NULL;
