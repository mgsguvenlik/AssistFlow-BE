param([Parameter(Mandatory=$true)][string]$ProfilePath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$root=[IO.Path]::GetFullPath($OutputDirectory)
$privateRoot=[IO.Path]::GetFullPath((Join-Path $repo '.tools'))+[IO.Path]::DirectorySeparatorChar
if(-not $root.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $root)){throw 'Yeni özel çıktı klasörü gerekir.'}
$profile=Get-Content -LiteralPath $ProfilePath -Raw|ConvertFrom-Json
$env=@($profile|Where-Object Section -eq 'Environment')
if($env.Count -ne 1 -or $env[0].DatabaseName -ne 'AssistFlow_Performance_Restore' -or $env[0].ServerName -ne 'sql2022' -or -not @($profile|Where-Object {$_.Section -eq 'CheckDb' -and $_.Completed}).Count){throw 'Doğrulanmış restore/CHECKDB profili gerekir.'}
if(@($profile|Where-Object {($_.Section -in 'Triggers','ServerTriggers') -and -not $_.is_disabled}).Count){throw 'Etkin trigger için ayrı inceleme gerekir.'}
$applied=@($profile|Where-Object Section -eq 'Migrations'|ForEach-Object MigrationId|Sort-Object)
$schemaRoot=Join-Path $repo 'docs/releases/collections-2026-10-15/schema'
$schema=Get-Content (Join-Path $schemaRoot 'manifest.json') -Raw|ConvertFrom-Json
$planned=[Collections.Generic.HashSet[string]]::new([string[]]$applied)
$null=New-Item -ItemType Directory -Path $root
$outputs=[Collections.Generic.List[object]]::new()
$guard="SET XACT_ABORT ON;`nIF DB_NAME()<>N'AssistFlow_Performance_Restore' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' THROW 51000,N'Yalnız onaylı prova hedefi kullanılabilir.',1;`n"
function SaveSql([string]$name,[string]$sql,[string]$kind,[string]$migration){
    $batches=@([regex]::Split($sql,'(?im)^\s*GO\s*(?:--[^\r\n]*)?\r?$')|Where-Object {-not [string]::IsNullOrWhiteSpace($_)})
    $safe=($batches|ForEach-Object {$guard+$_}) -join "`nGO`n"
    [IO.File]::WriteAllText((Join-Path $root $name),$safe,[Text.UTF8Encoding]::new($false))
    $outputs.Add([pscustomobject]@{File=$name;Kind=$kind;MigrationId=$migration;Batches=$batches.Count;Sha256=(Get-FileHash (Join-Path $root $name) -Algorithm SHA256).Hash})
}
foreach($f in $schema.files){
    if([IO.Path]::GetFileName($f.file) -ne $f.file -or (Get-FileHash (Join-Path $schemaRoot $f.file) -Algorithm SHA256).Hash -ne $f.sha256){throw 'Kaynak SQL hash/yol uyuşmazlığı.'}
    if(-not $planned.Contains($f.previousMigrationId)){throw 'Önceki migration eksik; hedef yanlış baseline olabilir.'}
    $sql=Get-Content (Join-Path $schemaRoot $f.file) -Raw
    if(([regex]::Matches($sql,"DB_NAME\(\) <> N'AssistFlow'")).Count -ne 1){throw 'Şema hedef guardı beklenenden farklı.'}
    $sql=$sql.Replace("DB_NAME() <> N'AssistFlow'","DB_NAME() <> N'AssistFlow_Performance_Restore'")
    SaveSql $f.file $sql 'CollectionMigration' $f.migrationId
    $null=$planned.Add($f.migrationId)
}
foreach($name in @('30-definition-seed.sql','40-menu-sms-seed.sql')){
    $sql=Get-Content (Join-Path (Split-Path $schemaRoot -Parent) $name) -Raw
    if(([regex]::Matches($sql,"DB_NAME\(\)<>N'AssistFlow'")).Count -ne 1 -or ([regex]::Matches($sql,'DECLARE @Apply bit=0;')).Count -ne 1){throw 'Seed güvenlik guardı beklenenden farklı.'}
    $sql=$sql.Replace("DB_NAME()<>N'AssistFlow'","DB_NAME()<>N'AssistFlow_Performance_Restore'").Replace('DECLARE @Apply bit=0;','DECLARE @Apply bit=1; -- Yalnız bu onaylı prova kopyasında açık; production dosyası değişmedi.')
    SaveSql $name $sql 'RehearsalSeed' ''
}
$manifest=[ordered]@{Status='REHEARSAL_ONLY_NO_PRODUCTION_APPLY';Target='AssistFlow_Performance_Restore';Server='sql2022';ProfileSha256=(Get-FileHash $ProfilePath -Algorithm SHA256).Hash;ExpectedBaselineMigrations=$applied;ExpectedAfterMigrations=@($planned|Sort-Object);Files=$outputs.ToArray();ProductionSourceModified=$false}
[IO.File]::WriteAllText((Join-Path $root 'manifest.json'),(ConvertTo-Json -InputObject $manifest -Depth 8),[Text.UTF8Encoding]::new($false))
Write-Output "Prova SQL paketi: $($outputs.Count) dosya. Manifest SHA-256: $((Get-FileHash (Join-Path $root 'manifest.json') -Algorithm SHA256).Hash). DB bağlantısı/yazması yok."
