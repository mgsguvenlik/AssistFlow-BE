-- Salt-okuma metadata on kontrolu; bu hazirlikta calistirilmadi.
-- Sonuclarin yol/LSN/oturum bilgileri Git disinda korunur.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
IF DB_NAME()<>N'master' OR UPPER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))<>N'SQL2022'
 THROW 51000,N'Yanlış Job/yedek ön kontrol hedefi; sql2022/master gerekli.',1;

SELECT N'Environment' AS Section,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
 DB_NAME() AS ObserverDatabase,SYSUTCDATETIME() AS ObservedAtUtc;
SELECT N'DatabaseRecovery' AS Section,name,state_desc,user_access_desc,is_read_only,
 recovery_model_desc,log_reuse_wait_desc,is_encrypted
 FROM sys.databases WHERE name IN(N'AssistFlow',N'MGS');
SELECT N'AgentService' AS Section,servicename,status_desc,startup_type_desc,last_startup_time
 FROM sys.dm_server_services;
SELECT N'JobSteps' AS Section,j.job_id,j.name AS JobName,j.enabled AS JobEnabled,
 st.step_id,st.step_name,st.subsystem,st.database_name,st.on_success_action,
 st.on_success_step_id,st.on_fail_action,st.on_fail_step_id,st.proxy_id,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),st.command)),2) AS CommandHash
 FROM msdb.dbo.sysjobs j LEFT JOIN msdb.dbo.sysjobsteps st ON st.job_id=j.job_id
 ORDER BY j.name,st.step_id;
-- Tum adimlar: master/Mgs_Corporate/CmdExec gibi capraz etkiler DB filtresiyle gizlenmez.
SELECT N'JobSchedules' AS Section,j.job_id,j.name AS JobName,s.schedule_id,s.schedule_uid,
 s.name AS ScheduleName,s.enabled AS ScheduleEnabled,s.freq_type,s.freq_interval,
 s.freq_subday_type,s.freq_subday_interval,s.freq_relative_interval,s.freq_recurrence_factor,
 s.active_start_date,s.active_end_date,s.active_start_time,s.active_end_time
 FROM msdb.dbo.sysjobs j LEFT JOIN msdb.dbo.sysjobschedules js ON js.job_id=j.job_id
 LEFT JOIN msdb.dbo.sysschedules s ON s.schedule_id=js.schedule_id ORDER BY j.name,s.schedule_id;
DECLARE @AgentSessionId int=(SELECT MAX(session_id) FROM msdb.dbo.syssessions);
SELECT N'AgentSession' AS Section,@AgentSessionId AS AgentSessionId,
 (SELECT agent_start_date FROM msdb.dbo.syssessions WHERE session_id=@AgentSessionId) AS AgentStartDate;
SELECT N'CurrentAgentActivity' AS Section,j.job_id,j.name AS JobName,a.session_id,
 a.run_requested_date,a.start_execution_date,a.stop_execution_date,a.last_executed_step_id,
 a.last_executed_step_date,a.next_scheduled_run_date,
 CASE WHEN a.start_execution_date IS NOT NULL AND a.stop_execution_date IS NULL THEN 1 ELSE 0 END AS ExecutionNotCompleted
 FROM msdb.dbo.sysjobs j LEFT JOIN msdb.dbo.sysjobactivity a ON a.job_id=j.job_id AND a.session_id=@AgentSessionId
 ORDER BY j.name;
SELECT TOP(100) N'RecentJobOutcomes' AS Section,j.job_id,j.name AS JobName,h.instance_id,
 h.run_date,h.run_time,h.run_duration,h.run_status
 FROM msdb.dbo.sysjobhistory h JOIN msdb.dbo.sysjobs j ON j.job_id=h.job_id
 WHERE h.step_id=0 ORDER BY h.instance_id DESC;

-- Metadata satiri basarili yedek/okunabilir cihaz veya log zinciri kaniti degildir.
SELECT TOP(60) N'BackupSets' AS Section,b.database_name,b.backup_set_id,b.backup_set_uuid,
 b.type,b.position,b.backup_start_date,b.backup_finish_date,b.recovery_model,
 b.is_copy_only,b.has_backup_checksums,b.is_damaged,b.first_lsn,b.last_lsn,
 b.checkpoint_lsn,b.database_backup_lsn,b.first_recovery_fork_guid,b.last_recovery_fork_guid,
 b.key_algorithm,b.encryptor_thumbprint,b.media_set_id,b.backup_size,b.compressed_backup_size
 FROM msdb.dbo.backupset b WHERE b.database_name IN(N'AssistFlow',N'MGS')
 ORDER BY b.backup_finish_date DESC,b.backup_set_id DESC;
SELECT TOP(120) N'BackupDevices' AS Section,b.database_name,b.backup_set_id,b.position,
 mf.family_sequence_number,mf.device_type,mf.physical_device_name
 FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily mf ON mf.media_set_id=b.media_set_id
 WHERE b.database_name IN(N'AssistFlow',N'MGS')
 ORDER BY b.backup_finish_date DESC,b.backup_set_id DESC,mf.family_sequence_number;
SELECT N'DataLogVolumes' AS Section,DB_NAME(f.database_id) AS DatabaseName,f.file_id,f.type_desc,
 f.physical_name,f.size*8./1024 AS AllocatedMb,v.volume_mount_point,v.available_bytes,v.total_bytes
 FROM sys.master_files f CROSS APPLY sys.dm_os_volume_stats(f.database_id,f.file_id) v
 WHERE DB_NAME(f.database_id) IN(N'AssistFlow',N'MGS') ORDER BY f.database_id,f.file_id;
SELECT TOP(20) N'RestoreHistory' AS Section,r.destination_database_name,r.restore_date,r.restore_type,
 r.backup_set_id,b.database_name AS SourceDatabase,b.backup_finish_date
 FROM msdb.dbo.restorehistory r LEFT JOIN msdb.dbo.backupset b ON b.backup_set_id=r.backup_set_id
 WHERE r.destination_database_name IN(N'AssistFlow',N'AssistFlow_Performance_Restore',N'MGS')
 ORDER BY r.restore_date DESC;

-- Tek DB filtresi kaynak yazar yoklugu ispatlamaz; master uzerinden capraz SQL olabilir.
SELECT N'UserSessions' AS Section,s.session_id,s.status,s.database_id,DB_NAME(s.database_id) AS DatabaseName,
 s.program_name,s.host_name,s.login_name,r.command,r.status AS RequestStatus,r.blocking_session_id
 FROM sys.dm_exec_sessions s LEFT JOIN sys.dm_exec_requests r ON r.session_id=s.session_id
 WHERE s.is_user_process=1 AND s.session_id<>@@SPID ORDER BY s.session_id;
