-- Yalnız kaynak MGS ve SQL Agent metadata okuması. Job durdurmaz/değiştirmez.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
IF DB_NAME()<>N'MGS' THROW 51000,N'Kaynak MGS olmalıdır.',1;
SELECT N'LegacyEnvironment' AS Section,DB_NAME() AS DatabaseName,@@SERVERNAME AS ServerName,SYSUTCDATETIME() AS CheckedAtUtc;
SELECT N'LegacyTables' AS Section,s.name AS SchemaName,t.name AS TableName,SUM(p.rows) AS ApproximateRows
 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
 JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1)
 WHERE (s.name=N'Core' AND (t.name LIKE N'%Contract%' OR t.name LIKE N'%Customer%' OR t.name LIKE N'%Payment%' OR t.name LIKE N'%Comment%' OR t.name LIKE N'%Invoice%'))
 OR (s.name=N'Report' AND t.name LIKE N'%Follow%')
 GROUP BY s.name,t.name ORDER BY s.name,t.name;
BEGIN TRY
 SELECT N'AgentJobs' AS Section,j.job_id,j.name AS JobName,j.enabled AS JobEnabled,
    st.step_id,st.step_name,st.database_name,
    CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),st.command)),2) AS CommandHash,
    sc.name AS ScheduleName,sc.enabled AS ScheduleEnabled,sc.freq_type,sc.freq_interval,
    sc.freq_subday_type,sc.freq_subday_interval,sc.active_start_time
 FROM msdb.dbo.sysjobs j JOIN msdb.dbo.sysjobsteps st ON st.job_id=j.job_id
 LEFT JOIN msdb.dbo.sysjobschedules js ON js.job_id=j.job_id
 LEFT JOIN msdb.dbo.sysschedules sc ON sc.schedule_id=js.schedule_id
 WHERE st.database_name IN(N'MGS',N'AssistFlow') OR st.command LIKE N'%RunFollow%'
    OR st.command LIKE N'%SIMSTOMGS%' OR st.command LIKE N'%spCollectiveMailControl%'
 ORDER BY j.name,st.step_id,sc.name;
END TRY
BEGIN CATCH
 SELECT N'AgentJobsUnavailable' AS Section,ERROR_NUMBER() AS ErrorNumber;
END CATCH;
