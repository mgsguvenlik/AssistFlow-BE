SELECT p.Id,p.SubscriberCode,p.SubscriberCompany,p.CreatedDate,p.UpdatedDate,
g.Code AS SourceGroupCode,g.GroupName AS SourceGroupName,tg.Id AS TargetGroupId,
ct.Code AS SourceTypeCode,ct.Name AS SourceTypeName,tt.Id AS TargetTypeId,
tn.Code AS SourceTenantCode,tn.Name AS SourceTenantName,ttn.Id AS TargetTenantId,
p.CreatedUser,cu.Code AS CreatedUserCode,tcu.Id AS TargetCreatedUser,
p.UpdatedUser,uu.Code AS UpdatedUserCode,tuu.Id AS TargetUpdatedUser
FROM AssistFlow.dbo.Customers p
LEFT JOIN AssistFlow.dbo.CustomerGroups g ON g.Id=p.CustomerGroupId
LEFT JOIN AssistFlowTest.dbo.CustomerGroups tg ON LTRIM(RTRIM(tg.Code)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(g.Code)) COLLATE Turkish_CI_AS
LEFT JOIN AssistFlow.dbo.CustomerType ct ON ct.Id=p.CustomerTypeId
LEFT JOIN AssistFlowTest.dbo.CustomerType tt ON LTRIM(RTRIM(tt.Code)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(ct.Code)) COLLATE Turkish_CI_AS
LEFT JOIN AssistFlow.dbo.Tenants tn ON tn.Id=p.TenantId
LEFT JOIN AssistFlowTest.dbo.Tenants ttn ON LTRIM(RTRIM(ttn.Code)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(tn.Code)) COLLATE Turkish_CI_AS
LEFT JOIN AssistFlow.dbo.Users cu ON cu.Id=p.CreatedUser
LEFT JOIN AssistFlowTest.dbo.Users tcu ON LTRIM(RTRIM(tcu.Code)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(cu.Code)) COLLATE Turkish_CI_AS
LEFT JOIN AssistFlow.dbo.Users uu ON uu.Id=p.UpdatedUser
LEFT JOIN AssistFlowTest.dbo.Users tuu ON LTRIM(RTRIM(tuu.Code)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(uu.Code)) COLLATE Turkish_CI_AS
WHERE NULLIF(LTRIM(RTRIM(p.SubscriberCode)),N'') IS NOT NULL AND p.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM AssistFlowTest.dbo.Customers c WHERE LTRIM(RTRIM(c.SubscriberCode)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS)
ORDER BY p.Id;
SELECT LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS AS SubscriberCode,COUNT_BIG(*) AS Count
FROM AssistFlow.dbo.Customers p WHERE p.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(p.SubscriberCode)),N'') IS NOT NULL
AND NOT EXISTS(SELECT 1 FROM AssistFlowTest.dbo.Customers c WHERE LTRIM(RTRIM(c.SubscriberCode)) COLLATE Turkish_CI_AS=LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS)
GROUP BY LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS HAVING COUNT_BIG(*)>1;
SELECT DB_NAME() AS TargetDatabase,c.name AS SourceColumn,t.name AS TypeName,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed
FROM AssistFlow.sys.columns c JOIN AssistFlow.sys.types t ON t.user_type_id=c.user_type_id
WHERE c.object_id=OBJECT_ID('AssistFlow.dbo.Customers') ORDER BY c.column_id;
SELECT p.Id,p.SubscriberCode,p.IsDeleted FROM AssistFlow.dbo.Customers p WHERE NULLIF(LTRIM(RTRIM(p.SubscriberCode)),N'') IS NULL;
