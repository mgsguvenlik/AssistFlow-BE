-- K03: salt-okunur grup ust kart / cari kimlik envanteri. AssistFlowTest baglaminda calisir.
SELECT p.CustomerID AS LegacyCustomerId, p.Type AS LegacyType, p.GroupCardNo AS GroupCode,
 p.SubscriberNo, p.AccountNo,
 CASE WHEN EXISTS(SELECT 1 FROM MGS.Definition.[Group] d WHERE d.Code=p.GroupCardNo AND d.GroupType=N'Kurumsal') THEN 1 ELSE 0 END AS IsCorporate,
 (SELECT COUNT(*) FROM MGS.Core.Customer m WHERE m.Type='GM' AND m.GroupID=p.CustomerID) AS LegacyMembers,
 (SELECT COUNT(*) FROM MGS.Core.Contract k WHERE k.CustomerID=p.CustomerID) AS LegacyContracts,
 (SELECT COUNT(*) FROM dbo.CustomerGroups g WHERE LTRIM(RTRIM(g.Code)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(p.GroupCardNo)) COLLATE Turkish_CI_AS) AS TargetGroupMatches,
 (SELECT COUNT(*) FROM dbo.Customers c WHERE c.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(c.SubscriberCode)),'') COLLATE Turkish_CI_AS=NULLIF(LTRIM(RTRIM(p.SubscriberNo)),'') COLLATE Turkish_CI_AS) AS TargetCustomerMatches
FROM MGS.Core.Customer p WHERE p.Type='G' ORDER BY p.CustomerID;

SELECT LTRIM(RTRIM(AccountNo)) AS AccountNo, COUNT(*) AS LegacyMatches
FROM MGS.Core.Customer WHERE NULLIF(LTRIM(RTRIM(AccountNo)),'') IS NOT NULL
GROUP BY LTRIM(RTRIM(AccountNo)) HAVING COUNT(*)>1;
