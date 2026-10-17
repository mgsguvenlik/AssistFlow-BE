SELECT 'AssistFlow' AS Db, COUNT_BIG(*) AS Total, SUM(CASE WHEN IsDeleted=0 THEN 1 ELSE 0 END) AS Active FROM AssistFlow.dbo.Customers
UNION ALL SELECT 'AssistFlowTest',COUNT_BIG(*),SUM(CASE WHEN IsDeleted=0 THEN 1 ELSE 0 END) FROM AssistFlowTest.dbo.Customers;
SELECT c.name,t.name AS TypeName,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed
FROM AssistFlowTest.sys.columns c JOIN AssistFlowTest.sys.types t ON t.user_type_id=c.user_type_id
WHERE c.object_id=OBJECT_ID('AssistFlowTest.dbo.Customers') ORDER BY c.column_id;
SELECT fk.name,pc.name AS ColumnName,rs.name AS RefSchema,rt.name AS RefTable,rc.name AS RefColumn
FROM AssistFlowTest.sys.foreign_keys fk
JOIN AssistFlowTest.sys.foreign_key_columns fc ON fc.constraint_object_id=fk.object_id
JOIN AssistFlowTest.sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id
JOIN AssistFlowTest.sys.tables rt ON rt.object_id=fc.referenced_object_id
JOIN AssistFlowTest.sys.schemas rs ON rs.schema_id=rt.schema_id
JOIN AssistFlowTest.sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id
WHERE fk.parent_object_id=OBJECT_ID('AssistFlowTest.dbo.Customers');
SELECT tr.name,tr.is_disabled FROM AssistFlowTest.sys.triggers tr WHERE tr.parent_id=OBJECT_ID('AssistFlowTest.dbo.Customers');
SELECT p.Id,p.SubscriberCode,p.SubscriberCompany,p.IsDeleted,p.CustomerGroupId,p.CustomerTypeId,p.TenantId,p.CreatedUser,p.UpdatedUser
FROM AssistFlow.dbo.Customers p
WHERE NULLIF(LTRIM(RTRIM(p.SubscriberCode)),N'') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM AssistFlowTest.dbo.Customers t
WHERE NULLIF(LTRIM(RTRIM(t.SubscriberCode)),N'') COLLATE Turkish_CI_AS=LTRIM(RTRIM(p.SubscriberCode)) COLLATE Turkish_CI_AS)
ORDER BY p.Id;
