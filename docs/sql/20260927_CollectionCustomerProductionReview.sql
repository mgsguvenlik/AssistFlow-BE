-- Salt okunur: baglanilan hedefte musteri ve grup adaylarini inceler.
SELECT DB_NAME() AS DatabaseName, COUNT_BIG(*) AS CustomerCount,
 SUM(CASE WHEN IsDeleted=0 THEN CAST(1 AS bigint) ELSE 0 END) AS ActiveCustomerCount FROM dbo.Customers;

SELECT p.CustomerID AS LegacyCustomerId, p.GroupCardNo AS GroupCode,
 p.Name AS LegacyName, p.SubscriberNo,
 c.Id AS TargetCustomerId, c.SubscriberCode, c.SubscriberCompany,
 c.IsDeleted, t.Code AS TypeCode, g.Code AS TargetGroupCode,
 CASE WHEN NULLIF(LTRIM(RTRIM(c.SubscriberCode)),'') COLLATE Turkish_CI_AS=NULLIF(LTRIM(RTRIM(p.SubscriberNo)),'') COLLATE Turkish_CI_AS THEN 1 ELSE 0 END AS SubscriberMatch,
 CASE WHEN NULLIF(LTRIM(RTRIM(c.SubscriberCompany)),'') COLLATE Turkish_CI_AS=NULLIF(LTRIM(RTRIM(p.Name)),'') COLLATE Turkish_CI_AS THEN 1 ELSE 0 END AS NameMatch
FROM MGS.Core.Customer p
LEFT JOIN dbo.Customers c ON
 NULLIF(LTRIM(RTRIM(c.SubscriberCode)),'') COLLATE Turkish_CI_AS=NULLIF(LTRIM(RTRIM(p.SubscriberNo)),'') COLLATE Turkish_CI_AS
 OR NULLIF(LTRIM(RTRIM(c.SubscriberCompany)),'') COLLATE Turkish_CI_AS=NULLIF(LTRIM(RTRIM(p.Name)),'') COLLATE Turkish_CI_AS
LEFT JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId
LEFT JOIN dbo.CustomerType t ON t.Id=c.CustomerTypeId
WHERE p.Type='G' ORDER BY p.CustomerID,c.Id;

SELECT g.Id,g.Code,g.GroupName,g.ParentGroupId,COUNT(c.Id) AS CustomerCount
FROM dbo.CustomerGroups g LEFT JOIN dbo.Customers c ON c.CustomerGroupId=g.Id AND c.IsDeleted=0
GROUP BY g.Id,g.Code,g.GroupName,g.ParentGroupId ORDER BY g.Code;

SELECT c.Id,c.SubscriberCode,c.SubscriberCompany,g.Code AS GroupCode,t.Code AS TypeCode,c.IsDeleted
FROM dbo.Customers c
LEFT JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId
LEFT JOIN dbo.CustomerType t ON t.Id=c.CustomerTypeId
WHERE EXISTS(SELECT 1 FROM MGS.Core.Customer p WHERE p.Type='G' AND
 (NULLIF(LTRIM(RTRIM(p.GroupCardNo)),'') COLLATE Turkish_CI_AS=LTRIM(RTRIM(g.Code)) COLLATE Turkish_CI_AS
 OR NULLIF(LTRIM(RTRIM(p.GroupCardNo)),'') COLLATE Turkish_CI_AS=LTRIM(RTRIM(c.SubscriberCode)) COLLATE Turkish_CI_AS));
