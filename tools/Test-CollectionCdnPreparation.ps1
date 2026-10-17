param(
    [string]$BundleRoot = '.tools/collection-cutover-review-2026-10-17-02',
    [string]$ApprovalPath = 'docs/releases/collections-2026-10-17/cdn-approval.template.json'
)

# Offline only: no config loading, SQL/HTTP/SDK, extraction, startup or file writes.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
function Get-LocalFile([string]$candidate) {
    $full = if ([IO.Path]::IsPathRooted($candidate)) { [IO.Path]::GetFullPath($candidate) } else { [IO.Path]::GetFullPath((Join-Path $repoRoot $candidate)) }
    if (-not $full.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Repository dışındaki kanıt kabul edilmez.' }
    $item = Get-Item -LiteralPath $full
    if ($item.PSIsContainer) { throw 'Dosya bekleniyor.' }
    $check = $item
    while ($null -ne $check -and $check.FullName.Length -ge $repoRoot.Length) {
        if ($check.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Link/reparse kanıt yolu kabul edilmez.' }
        $check = if ($check -is [IO.FileInfo]) { $check.Directory } else { $check.Parent }
    }
    return $full
}
function Hash([string]$path) { (Get-FileHash -LiteralPath (Get-LocalFile $path) -Algorithm SHA256).Hash }
function Require([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
$approvalFile = Get-LocalFile $ApprovalPath
$approvalText = Get-Content -LiteralPath $approvalFile -Raw
$approval = $approvalText | ConvertFrom-Json
Require ($approval.Version -eq 'collection-cdn-review-v1') 'Beklenmeyen şablon sürümü.'
Require ($approval.Status -eq 'PENDING_HUMAN_APPROVAL_NOT_EXECUTABLE') 'Bu kontrol yalnız kapalı hazırlık şablonu içindir.'
Require (@([regex]::Matches($approvalText, ':\s*true\b')).Count -eq 0) 'Hazırlık şablonunda açık onay var.'
Require ($approval.Target.Database -eq 'AssistFlow' -and $approval.Target.Server -eq 'sql2022') 'Hedef etiketi farklı.'
Require ($null -eq $approval.Target.BucketName -and $null -eq $approval.Target.KeyPrefix -and $null -eq $approval.Execution.OperatorUserCode) 'Gerçek hedef/operatör onayı bu hazırlığa doldurulmaz.'
Require ((Hash (Join-Path $BundleRoot 'bundle-hashes.json')) -eq $approval.ReviewedBundleHash) 'İnceleme paketi hash farklı.'
$dataRoot = Join-Path $BundleRoot 'data'
$packageFile = Get-LocalFile (Join-Path $dataRoot 'package-hashes.json')
$packageManifest = @(Get-Content -LiteralPath $packageFile -Raw | ConvertFrom-Json)
foreach ($entry in $approval.ExpectedEvidenceHashes.PSObject.Properties) {
    Require ([IO.Path]::GetFileName($entry.Name) -eq $entry.Name) 'Geçersiz kanıt adı.'
    $record = @($packageManifest | Where-Object File -eq $entry.Name)
    Require ($record.Count -eq 1 -and $record[0].Sha256 -eq $entry.Value) 'Veri manifesti/kanıt farklı.'
    Require ((Hash (Join-Path $dataRoot $entry.Name)) -eq $entry.Value) 'Kanıt hash farklı.'
}
$summary = Get-Content -LiteralPath (Get-LocalFile (Join-Path $dataRoot 'file-summary.json')) -Raw | ConvertFrom-Json
$contracts = @(Get-Content -LiteralPath (Get-LocalFile (Join-Path $dataRoot 'contract-file-plan.ndjson')) | ForEach-Object { $_ | ConvertFrom-Json })
$activity = @(Get-Content -LiteralPath (Get-LocalFile (Join-Path $dataRoot 'activity-file-plan.json')) -Raw | ConvertFrom-Json)
$physical = @(Get-Content -LiteralPath (Get-LocalFile (Join-Path $dataRoot 'physical-file-verification.json')) -Raw | ConvertFrom-Json)
$ready = @($contracts | Where-Object Status -eq 'LOCAL_VERIFIED_PENDING_PRODUCTION_CDN')
$activityReady = @($activity | Where-Object Status -eq 'LOCAL_VERIFIED_PENDING_PRODUCTION_CDN')
$actual = [ordered]@{
    ContractPlanRows = $contracts.Count
    ContractRelationshipsPendingCdn = $ready.Count
    DistinctContractPaths = @($ready.RelativePath | Sort-Object -Unique).Count
    DistinctContractContentHashes = @($ready.File.SHA256 | Sort-Object -Unique).Count
    ActivityFilesPendingCdn = $activityReady.Count
    ActivityFilesHeld = @($activity | Where-Object Status -ne 'LOCAL_VERIFIED_PENDING_PRODUCTION_CDN').Count
    MissingContractFiles = @($contracts | Where-Object Status -eq 'MISSING_IN_SUPPLIED_ARCHIVE').Count
    EmptyContractFiles = @($contracts | Where-Object Status -eq 'FILE_EMPTY').Count
}
foreach ($entry in $actual.GetEnumerator()) { Require ($approval.ExpectedBaseline.($entry.Key) -eq $entry.Value) "Adet farklı: $($entry.Key)" }
Require ($physical.Count -eq $summary.VerifiedPhysicalFiles) 'Fiziksel envanter adedi farklı.'
Require (@($contracts | Where-Object ReadyToApply -ne $false).Count -eq 0 -and @($activity | Where-Object ReadyToApply -ne $false).Count -eq 0) 'Kaynak kanıtının kapalı uygulama bayrağı değişmiş.'
foreach ($group in ($ready | Group-Object RelativePath)) {
    Require (@($group.Group.File.SHA256 | Sort-Object -Unique).Count -eq 1) 'Aynı dosya yolu farklı içerik kanıtına sahip.'
}
Require ($ready.Count -eq $summary.ContractResults.LOCAL_VERIFIED_PENDING_PRODUCTION_CDN -and $activityReady.Count -eq $summary.ActivityResults.LOCAL_VERIFIED_PENDING_PRODUCTION_CDN) 'Özet/plan tutarsız.'
[ordered]@{
    Status = 'OFFLINE_PREPARATION_VERIFIED_NOT_PRODUCTION_ACCEPTANCE'
    DatabaseConnected = $false
    ProviderContacted = $false
    FilesWritten = $false
    SourceBytesReverified = $false
    MalwareScanPerformed = $false
    ApprovalTemplateHash = Hash $approvalFile
    Baseline = $actual
    ContractRelationshipBytes = [long](($ready.File.Size | Measure-Object -Sum).Sum)
    ActivityCandidateBytes = [long](($activityReady.Size | Measure-Object -Sum).Sum)
    Note = 'Paket metadata/hash kontrolüdür; fiziksel arşiv, production hedef, CDN ve HTTP kabulü değildir.'
} | ConvertTo-Json -Depth 5
