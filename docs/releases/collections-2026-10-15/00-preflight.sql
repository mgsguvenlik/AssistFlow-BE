-- Salt okunur. Uygulama/seed/DDL çalıştırmaz. Sonuç müşteri kişisel verisi içermez.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Canlı ön kontrol hedefi AssistFlow olmalıdır.', 1;
SELECT N'Environment' AS Section, DB_NAME() AS DatabaseName, @@SERVERNAME AS ServerName,
    SYSUTCDATETIME() AS CheckedAtUtc, compatibility_level, snapshot_isolation_state_desc,
    is_read_committed_snapshot_on FROM sys.databases WHERE name=DB_NAME();
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
    SELECT N'Migrations' AS Section, MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT N'CollectionTables' AS Section, t.name AS TableName,
    COALESCE(SUM(p.rows),0) AS ApproximateRows
    FROM sys.tables t LEFT JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN (0,1)
    WHERE t.schema_id=SCHEMA_ID(N'collection') GROUP BY t.name ORDER BY t.name;
SELECT N'CollectionColumns' AS Section, t.name AS TableName, c.name AS ColumnName,
    TYPE_NAME(c.user_type_id) AS TypeName, c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity
    FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
    WHERE t.schema_id=SCHEMA_ID(N'collection') ORDER BY t.name,c.column_id;
SELECT N'CollectionConstraints' AS Section, OBJECT_SCHEMA_NAME(parent_object_id) AS SchemaName,
    OBJECT_NAME(parent_object_id) AS TableName, name, is_disabled, is_not_trusted
    FROM sys.foreign_keys WHERE schema_id=SCHEMA_ID(N'collection')
    UNION ALL SELECT N'CollectionConstraints',OBJECT_SCHEMA_NAME(parent_object_id),
    OBJECT_NAME(parent_object_id),name,is_disabled,is_not_trusted FROM sys.check_constraints
    WHERE schema_id=SCHEMA_ID(N'collection');
SELECT N'CollectionIndexes' AS Section,t.name AS TableName,i.name,i.is_unique,i.is_disabled,i.filter_definition,
    c.name AS ColumnName,ic.key_ordinal,ic.is_included_column
    FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id
    JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
    JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
    WHERE t.schema_id=SCHEMA_ID(N'collection') ORDER BY t.name,i.name,ic.index_column_id;
SELECT N'CustomerTypes' AS Section, Id,Code,Name FROM dbo.CustomerType ORDER BY Code;
SELECT N'ServiceTypes' AS Section, Id,Name,IsDeleted FROM dbo.ServiceType ORDER BY Name;
SELECT N'Currencies' AS Section, Id,Code,Name FROM dbo.CurrencyType ORDER BY Code;
SELECT N'CustomerScope' AS Section,g.Code AS GroupCode,ct.Code AS TypeCode,COUNT_BIG(*) AS CustomerCount
    FROM dbo.Customers c LEFT JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId
    LEFT JOIN dbo.CustomerType ct ON ct.Id=c.CustomerTypeId
    WHERE c.IsDeleted=0 GROUP BY g.Code,ct.Code ORDER BY g.Code,ct.Code;
SELECT N'SubscriberQuality' AS Section,
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(SubscriberCode)),N'') IS NULL THEN CONVERT(bigint,1) ELSE 0 END) AS EmptySubscriberCount,
    COUNT_BIG(*) AS ActiveCustomers FROM dbo.Customers WHERE IsDeleted=0;
SELECT N'SubscriberDuplicates' AS Section,COUNT_BIG(*) AS DuplicateCodeCount,COALESCE(SUM(n),0) AS AffectedCustomers
    FROM (SELECT COUNT_BIG(*) n FROM dbo.Customers WHERE IsDeleted=0
    AND NULLIF(LTRIM(RTRIM(SubscriberCode)),N'') IS NOT NULL
    GROUP BY LTRIM(RTRIM(SubscriberCode)) HAVING COUNT_BIG(*)>1) d;
SELECT N'CollectionMenu' AS Section,m.Id,m.Name,m.Description,mr.RoleId,r.Code AS RoleCode,mr.HasView,mr.HasEdit
    FROM dbo.Menus m LEFT JOIN dbo.MenuRole mr ON mr.ModulId=m.Id LEFT JOIN dbo.Roles r ON r.Id=mr.RoleId
    WHERE m.Name=N'CollectionFollowUp';
SELECT N'SmsTemplate' AS Section,Name,COUNT_BIG(*) AS MatchCount,MAX(LEN(Value)) AS TemplateLength
    FROM dbo.Configurations WHERE Name=N'CollectionRateChangeSmsTemplate' GROUP BY Name;
SELECT N'SyncProcedure' AS Section,s.name AS SchemaName,p.name,p.modify_date,
    CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2) AS DefinitionHash
    FROM sys.procedures p JOIN sys.schemas s ON s.schema_id=p.schema_id
    LEFT JOIN sys.sql_modules m ON m.object_id=p.object_id WHERE p.name=N'sp_SyncCustomers';
