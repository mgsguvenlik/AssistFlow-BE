param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot '../WebAPI/appsettings.Production.json')
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
$privateRoot=(Join-Path $repo '.tools')+[IO.Path]::DirectorySeparatorChar
if(-not $output.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)){throw 'Yalnız yeni .tools prova dizini kullanılabilir.'}
$source=Get-Content -LiteralPath $ConfigurationPath -Raw|ConvertFrom-Json
$cs=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($source.AppSettings.MSSQLConnectionString)
if($cs.InitialCatalog -cne 'AssistFlow'){throw 'Kaynak config katalog kontrolü başarısız.'}
$cs['Initial Catalog']='AssistFlow_Performance_Restore'
$cs['Application Name']='CollectionHostingIsolationRehearsal'
$cs['Connect Timeout']=10
# Metadata observer only; the actual application has all DB-startup paths disabled.
$observer=[System.Data.SqlClient.SqlConnection]::new($cs.ConnectionString)
try{
    $observer.Open();$cmd=$observer.CreateCommand()
    $cmd.CommandText="IF DB_NAME()<>N'AssistFlow_Performance_Restore' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' THROW 51000,N'Yanlış prova hedefi.',1; SELECT COUNT_BIG(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID() AND is_user_process=1 AND session_id<>@@SPID;"
    if([long]$cmd.ExecuteScalar() -ne 0){throw 'Prova DB üzerinde başka oturum var; zorla sonlandırılmaz.'}
}finally{if($cmd){$cmd.Dispose()};$observer.Dispose();[System.Data.SqlClient.SqlConnection]::ClearAllPools()}
$null=New-Item -ItemType Directory -Path $output
$content=Join-Path $output 'host-settings';$null=New-Item -ItemType Directory -Path $content
$config=[ordered]@{
    StartupExecution=@{MigrationsEnabled=$false;SeedsEnabled=$false;ManitouSyncEnabled=$false;IsolatedRehearsalMode=$true}
    CollectionRead=@{Enabled=$false;ContractCreateEnabled=$false}
    AppSettings=@{MSSQLConnectionString=$cs.ConnectionString;PostgresConnectionString='';DbProvider='MSSQL';Issuer='rehearsal';Audience='rehearsal';Key=('rehearsal-only-not-a-production-key-'*2);OpenidConfiguration='https://invalid.invalid';AppUrl='http://127.0.0.1';FrontUrl='http://127.0.0.1';ManitouBaseUrl='https://invalid.invalid';ManitouPassword='unused';CollectionSmsEnabled=$false;SmsSimulationEnabled=$true}
    R2Storage=@{Endpoint='https://invalid.invalid';PublicBaseUrl='https://invalid.invalid';BucketName='unused';AccessKeyId='unused';SecretAccessKey='unused';LegacyLocalFallbackEnabled=$false}
    DataProtection=@{KeysPath=(Join-Path $content 'keys')}
    Serilog=@{Using=@('Serilog.Sinks.Console');MinimumLevel='Information';WriteTo=@(@{Name='Console'})}
}
[IO.File]::WriteAllText((Join-Path $content 'appsettings.json'),($config|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop()
$dll=Join-Path $repo 'WebAPI/bin/Debug/net9.0/WebAPI.dll'
$start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle='Hidden'
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.WorkingDirectory=$content
$hostEncoding=[Text.Encoding]::GetEncoding([Globalization.CultureInfo]::CurrentCulture.TextInfo.OEMCodePage)
$start.StandardOutputEncoding=$hostEncoding
$start.StandardErrorEncoding=$hostEncoding
foreach($key in @($start.Environment.Keys)){if($key -match '^(ASPNETCORE_|DOTNET_|AppSettings__|StartupExecution__|CollectionRead__|Serilog__|R2Storage__|DataProtection__|Kestrel__|URLS$|COLLECTION_RELEASE_CONNECTIONSTRING$)'){$null=$start.Environment.Remove($key)}}
$start.Environment['ASPNETCORE_ENVIRONMENT']='CollectionRehearsal'
$start.Environment['DOTNET_ENVIRONMENT']='CollectionRehearsal'
$start.Environment['ASPNETCORE_URLS']="http://127.0.0.1:$port"
$start.ArgumentList.Add($dll);$start.ArgumentList.Add('--contentRoot');$start.ArgumentList.Add($content)
$process=[Diagnostics.Process]::new();$process.StartInfo=$start
$http=[Net.Http.HttpClient]::new([Net.Http.HttpClientHandler]@{AllowAutoRedirect=$false;UseProxy=$false})
$http.Timeout=[TimeSpan]::FromSeconds(2)
$success=$false;$stopped=$false;$started=$false;$stdout='';$stderr=''
try{
    $null=$process.Start();$started=$true;$outRead=$process.StandardOutput.ReadToEndAsync();$errRead=$process.StandardError.ReadToEndAsync()
    $deadline=[DateTime]::UtcNow.AddSeconds(45);$status=0
    while([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited){
        try{$response=$http.GetAsync("http://127.0.0.1:$port/__collection_rehearsal_probe__").GetAwaiter().GetResult();$status=[int]$response.StatusCode;$response.Dispose();if($status -eq 404){break}}catch{}
        Start-Sleep -Milliseconds 300
    }
    if($status -ne 404){throw 'Prova host HTTP açılışı doğrulanamadı; özel log dosyasını inceleyin.'}
    # SIGINT-equivalent via the target process console isn't available with redirected pipes.
    # Kills ONLY this tool-owned child; never touches another backend or SQL session.
    $process.Kill($true);$process.WaitForExit();$stopped=$true
    $stdout=$outRead.GetAwaiter().GetResult();$stderr=$errRead.GetAwaiter().GetResult()
    foreach($pattern in @('migration=False, seed=False, Manitou=False','DbContext oluşturulmadı','İkinci açılış migration yolu kapalı','Manitou otomatik senkronizasyonu açılış ayarıyla kapalı','MailOutboxDispatcher oluşturulmadı','SlaNotificationDispatcher oluşturulmadı','PeriodicReportScheduler oluşturulmadı','HelpdeskBackgroundWorker oluşturulmadı')){
        if(-not $stdout.Contains($pattern)){throw 'Beklenen açılış izolasyon kanıtı logda yok.'}
    }
    if($stdout.Contains('Manitou staging sync başladı.') -or $stdout.Contains('Seed starting') -or $stderr.Length){throw 'Beklenmeyen host çıktısı; kabul edilmedi.'}
    $success=$true
}finally{
    if($started -and -not $process.HasExited){$process.Kill($true);$process.WaitForExit();$stopped=$true}
    if($outRead){$stdout=$outRead.GetAwaiter().GetResult()};if($errRead){$stderr=$errRead.GetAwaiter().GetResult()}
    [IO.File]::WriteAllText((Join-Path $output 'host-stdout.private.log'),$stdout,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $output 'host-stderr.private.log'),$stderr,[Text.UTF8Encoding]::new($false))
    $codeHashes=[ordered]@{}
    foreach($name in @('WebAPI','Business','Data','Model','Core')){$codeHashes[$name]=(Get-FileHash (Join-Path (Split-Path $dll -Parent) "$name.dll")).Hash}
    $evidence=[ordered]@{Mode='REAL_WEBAPI_LOOPBACK_ISOLATED_REHEARSAL';Passed=$success;Target='sql2022 / AssistFlow_Performance_Restore';Environment='CollectionRehearsal';HttpStatus=$status;ToolOwnedHostStopped=$stopped;UnrelatedWorkersConstructed=$false;ManitouEnabled=$false;MigrationsEnabled=$false;SeedsEnabled=$false;SmsEnabled=$false;ProductionWrites=$false;WebApiHash=(Get-FileHash $dll).Hash;CodeHashes=$codeHashes;AtUtc=[DateTimeOffset]::UtcNow}
    [IO.File]::WriteAllText((Join-Path $output 'result.json'),($evidence|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
    $http.Dispose();$process.Dispose()
}
Write-Output 'Gerçek WebAPI izole prova hostu: HTTP/kapalı migration-seed-Manitou/diğer worker izolasyonu doğrulandı. Yalnız araca ait host kapatıldı; canlı DB işlemi yok.'
