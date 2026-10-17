SELECT 'Source' AS Db,Id,Name,Code FROM AssistFlow.dbo.CustomerType
UNION ALL SELECT 'Target',Id,Name,Code FROM AssistFlowTest.dbo.CustomerType;
SELECT 'Source' AS Db,Id,Code,GroupName,ParentGroupId FROM AssistFlow.dbo.CustomerGroups
UNION ALL SELECT 'Target',Id,Code,GroupName,ParentGroupId FROM AssistFlowTest.dbo.CustomerGroups;
SELECT 'Source' AS Db,Id,Code,Name,IsDeleted,IsActive FROM AssistFlow.dbo.Tenants
UNION ALL SELECT 'Target',Id,Code,Name,IsDeleted,IsActive FROM AssistFlowTest.dbo.Tenants;
SELECT 'Source' AS Db,Id,Code,Name,IsDeleted FROM AssistFlow.dbo.Users WHERE Id IN(0,1)
UNION ALL SELECT 'Target',Id,Code,Name,IsDeleted FROM AssistFlowTest.dbo.Users WHERE Id IN(0,1);
SELECT s.name AS SchemaName,t.name AS TableName,c.name AS ColumnName FROM AssistFlowTest.sys.foreign_key_columns fk
JOIN AssistFlowTest.sys.tables t ON t.object_id=fk.parent_object_id
JOIN AssistFlowTest.sys.schemas s ON s.schema_id=t.schema_id
JOIN AssistFlowTest.sys.columns c ON c.object_id=fk.parent_object_id AND c.column_id=fk.parent_column_id
WHERE fk.referenced_object_id=OBJECT_ID('AssistFlowTest.dbo.CustomerGroups');
