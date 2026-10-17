param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot '../WebAPI/appsettings.Production.json'),
    [string]$OutputPath,
    [switch]$SourceLegacy
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$connectionText = [Environment]::GetEnvironmentVariable('COLLECTION_RELEASE_CONNECTIONSTRING')
if ([string]::IsNullOrWhiteSpace($connectionText)) {
    $configuration = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
    $connectionText = $configuration.AppSettings.MSSQLConnectionString
}
if ([string]::IsNullOrWhiteSpace($connectionText)) { throw 'Canlı bağlantı ayarı bulunamadı. Bağlantıyı güvenli ortam değişkeniyle sağlayın.' }
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionText)
if ($builder.InitialCatalog -ne 'AssistFlow') { throw 'Ön kontrol yalnız AssistFlow canlı veritabanında çalışır.' }
if ($SourceLegacy) { $builder['Initial Catalog'] = 'MGS' }
$builder['Connect Timeout'] = 10
$builder['ApplicationIntent'] = 'ReadOnly'
$builder['Application Name'] = 'CollectionReleaseReadOnlyPreflight'
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$command = $connection.CreateCommand()
$command.CommandTimeout = 60
$sqlFile = if ($SourceLegacy) { '01-legacy-preflight.sql' } else { '00-preflight.sql' }
$command.CommandText = Get-Content -LiteralPath (Join-Path $repository "docs/releases/collections-2026-10-15/$sqlFile") -Raw
try {
    $connection.Open()
    $reader = $command.ExecuteReader()
    try {
        $results = [System.Collections.Generic.List[object]]::new()
        do {
            while ($reader.Read()) {
                $record = [ordered]@{}
                for ($i=0; $i -lt $reader.FieldCount; $i++) {
                    $record[$reader.GetName($i)] = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
                }
                $results.Add($record)
            }
        } while ($reader.NextResult())
        $json = ConvertTo-Json -InputObject $results.ToArray() -Depth 8
        if ($OutputPath) {
            if (Test-Path -LiteralPath $OutputPath) { throw 'Mevcut ön kontrol raporu üzerine yazılmaz.' }
            [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($OutputPath), $json, [System.Text.UTF8Encoding]::new($false))
            Write-Output 'Salt-okunur canlı ön kontrol tamamlandı. Rapor kaydedildi; DB değişmedi.'
        } else { Write-Output $json }
    } finally { $reader.Dispose() }
} catch {
    # Do not emit connection strings or full provider exception text.
    throw "Canlı ön kontrol tamamlanamadı. Hata türü: $($_.Exception.GetType().Name). Kimlik bilgileri raporlanmadı."
} finally { $command.Dispose(); $connection.Dispose() }
