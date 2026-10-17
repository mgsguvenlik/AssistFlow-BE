param(
    [Parameter(Mandatory=$true)][string]$PlanPath,
    [Parameter(Mandatory=$true)][string]$AcceptancePath,
    [Parameter(Mandatory=$true)][string]$FinalProfilePath,
    [Parameter(Mandatory=$true)][string]$OutputPath
)

# Offline packaging only. No config, SQL, HTTP, object archive or upload operations.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$privateRoot = (Join-Path $repo '.tools') + [IO.Path]::DirectorySeparatorChar
function Full([string]$path) {
    $full = if ([IO.Path]::IsPathRooted($path)) { [IO.Path]::GetFullPath($path) } else { [IO.Path]::GetFullPath((Join-Path $repo $path)) }
    if (-not $full.StartsWith($repo + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Repository dışı paket girdisi reddedildi.' }
    for ($p=$full; $p; $p=[IO.Path]::GetDirectoryName($p)) {
        if ((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Link/reparse yolu reddedildi.' }
    }
    return $full
}
function Private([string]$path) { $full=Full $path; if(-not $full.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Özel .tools yolu gerekli.'}; return $full }
function Sha([string]$path) { (Get-FileHash -LiteralPath (Full $path) -Algorithm SHA256).Hash }
$planFile=Private $PlanPath; $acceptanceFile=Private $AcceptancePath
$plan=Get-Content -LiteralPath $planFile -Raw|ConvertFrom-Json
$acceptance=Get-Content -LiteralPath $acceptanceFile -Raw|ConvertFrom-Json
if($plan.Scope -ne 'REHEARSAL' -or -not $acceptance.WritePathVerified -or $acceptance.ProductionAuthorizationGranted -or $acceptance.RealCdnReadbackVerified){throw 'Yalnız kapalı canlı yetkili başarılı izole dosya kabulü paketlenir.'}
foreach($key in @('BundleHash','CodeHash','SourceProjectionHash')) { if($plan.$key -ne $acceptance.$key){throw "Plan/kabul bağı farklı: $key"} }
$proofRoot=Split-Path $acceptanceFile -Parent
$finalProfileFile=Private $FinalProfilePath
$finalProfile=@(Get-Content -LiteralPath $finalProfileFile -Raw|ConvertFrom-Json)
if(@($finalProfile|Where-Object { $_.Section -eq 'Environment' -and $_.DatabaseName -eq 'AssistFlow_Performance_Restore' }).Count -ne 1 -or @($finalProfile|Where-Object { $_.Section -eq 'CheckDb' -and $_.DatabaseName -eq 'AssistFlow_Performance_Restore' -and $_.Completed -and $_.ErrorRows -eq 0 -and -not $_.RepairExecuted }).Count -ne 1){throw 'Son prova hedefi/CHECKDB kanıtı eksik.'}
foreach($entry in $acceptance.Evidence){ if((Sha (Private $entry.Path)) -ne $entry.Sha256){throw 'Prova kanıtı değişmiş.'} }
$resultFile=Join-Path $proofRoot 'result.json'
if(@($acceptance.Evidence|Where-Object { (Private $_.Path) -eq $resultFile }).Count -ne 1){throw 'Result kabul kanıtına bağlı değil.'}
$result=Get-Content -LiteralPath $resultFile -Raw|ConvertFrom-Json
if($result.PlanHash -ne (Sha $planFile) -or $result.Target -ne 'AssistFlow_Performance_Restore' -or $result.RealProviderCalls -ne 0 -or $result.ProductionWrites -or -not $result.LocalSimulationOnly -or -not $result.FinancialAndOriginalAuditUnchanged -or $result.ReplayCreated -ne 0 -or $result.ReplayPutCount -ne 0 -or @($result.Checks|Where-Object { -not $_.Passed }).Count){throw 'Prova sonucu kabul koşullarını sağlamıyor.'}
if($result.Contracts+$result.Customers -ne $plan.Files.Count -or $result.Receipts -ne $plan.Files.Count -or $result.ObjectsWrittenLocally -ne $plan.Files.Count){throw 'Dosya/receipt adetleri uyuşmuyor.'}
$binary=Full 'tools/Collections.Files/bin/Debug/net9.0'
$libs=@('Collections.Files','Business','Data','Model','Core','AWSSDK.S3')|ForEach-Object { Sha (Join-Path $binary "$_.dll") }
$code=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($libs -join '|'))))
if($code -ne $plan.CodeHash){throw 'Runtime prova planından farklı.'}
$approvalFile=Join-Path (Split-Path $planFile -Parent) 'approval.template.json'
$approval=Get-Content -LiteralPath $approvalFile -Raw|ConvertFrom-Json
if($approval.Authorization -ne 'NOT_APPROVED' -or $approval.MaxFiles -ne 0 -or $approval.PlanHash -ne (Sha $planFile)){throw 'Kapalı inceleme şablonu gerekli.'}
$bundle=Private $plan.Bundle
if((Sha (Join-Path $bundle 'bundle-hashes.json')) -ne $plan.BundleHash){throw 'Orijinal bundle değişmiş.'}
$destination=Private $OutputPath
if(Test-Path -LiteralPath $destination){throw 'Mevcut paket üzerine yazılmaz.'}
$inputs=[Collections.Generic.List[object]]::new()
function AddInput([string]$path,[string]$relative) {
    if($relative.Split('/') -contains '..'){throw 'Paket hedefi güvensiz.'}
    $inputs.Add([pscustomobject]@{Source=(Full $path);Relative=$relative;Sha256=(Sha $path)})
}
Get-ChildItem (Full 'tools/Collections.Files') -File | Where-Object { $_.Extension -in '.cs','.csproj','.md' } | ForEach-Object { AddInput $_.FullName ('source/'+$_.Name) }
Get-ChildItem $binary -File -Recurse | ForEach-Object {
    $relative=[IO.Path]::GetRelativePath($binary,$_.FullName).Replace('\','/')
    if($_.Name -match 'appsettings|secret|credential|\.pfx$|\.pem$'){throw 'Hassas runtime dosyası paketlenemez.'}
    AddInput $_.FullName ('runtime/'+$relative)
}
AddInput $planFile 'plan/plan.json'; AddInput $approvalFile 'plan/approval.template.json'
AddInput $acceptanceFile 'proof/acceptance.json'
AddInput $finalProfileFile 'proof/final-profile.json'
foreach($entry in $acceptance.Evidence){AddInput $entry.Path ('proof/'+[IO.Path]::GetFileName($entry.Path))}
foreach($name in @('cdn-transfer-and-access.md','cdn-adapter-rehearsal.md','cdn-approval.template.json')){AddInput ('docs/releases/collections-2026-10-17/'+$name) ('docs/'+$name)}
AddInput 'tools/Prepare-CollectionCdnHandoff.ps1' 'source/Prepare-CollectionCdnHandoff.ps1'
if(@($inputs.Relative|Sort-Object -Unique).Count -ne $inputs.Count){throw 'Paket dosyası tekrar ediyor.'}
[void](New-Item -ItemType Directory -Path $destination)
$manifest=@(foreach($inputItem in $inputs){
    $target=Join-Path $destination $inputItem.Relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    Copy-Item -LiteralPath $inputItem.Source -Destination $target
    if((Sha $target) -ne $inputItem.Sha256){throw 'Paket kopyası hash farklı.'}
    [ordered]@{File=$inputItem.Relative;Sha256=$inputItem.Sha256;Bytes=(Get-Item -LiteralPath $target).Length}
})
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $destination 'manifest.json') -Encoding utf8NoBOM
[ordered]@{Status='SEALED_OFFLINE_FILE_ADAPTER_REVIEW_NOT_PRODUCTION_APPROVAL';Files=$manifest.Count;Bytes=($manifest.Bytes|Measure-Object -Sum).Sum;ManifestHash=(Sha (Join-Path $destination 'manifest.json'));ProductionApplyAllowed=$false;ArchiveObjectsIncluded=$false}|ConvertTo-Json
