param(
    [Parameter(Mandatory=$true)][string]$BundleDirectory,
    [Parameter(Mandatory=$true)][string]$ExpectedBundleHash,
    [Parameter(Mandatory=$true)][string]$LivePlanDirectory,
    [Parameter(Mandatory=$true)][string]$ExpectedLivePlanHash,
    [Parameter(Mandatory=$true)][string]$RehearsalPlanDirectory,
    [Parameter(Mandatory=$true)][string]$AcceptancePath,
    [Parameter(Mandatory=$true)][string]$ExpectedAcceptanceHash,
    [Parameter(Mandatory=$true)][string]$FinalProfilePath,
    [Parameter(Mandatory=$true)][string]$ExpectedFinalProfileHash,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$HostProofDirectory,
    [string]$ExpectedHostProofHash
)
# Offline packaging only: no SQL, configuration loading, host or outbound call.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$privateRoot = (Join-Path $repo '.tools') + [IO.Path]::DirectorySeparatorChar
function NoReparse([string]$full) {
    $probe = $full
    while (-not (Test-Path -LiteralPath $probe)) {
        $parent = Split-Path $probe -Parent
        if (-not $parent -or $parent -eq $probe) { throw 'Dosya üst yolu bulunamadı.' }
        $probe = $parent
    }
    $item = Get-Item -LiteralPath $probe
    while ($item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Link/reparse yolu kabul edilmez.' }
        $item = if ($item.PSIsContainer) { $item.Parent } else { $item.Directory }
    }
}
function PrivatePath([string]$value) {
    $full = [IO.Path]::GetFullPath($value)
    if (-not $full.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Yalnız .tools kapsamı kullanılabilir.' }
    NoReparse $full
    return $full
}
function Hash([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function ReadVerified([string]$path,[string]$expected) {
    if ((Hash $path) -cne $expected) { throw "Kanıt hash'i farklı: $([IO.Path]::GetFileName($path))" }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}
function SameCode($a,$b) {
    foreach ($key in @('Adapter','EngineSource','Business','Data','Model','Core')) {
        if (-not $a.$key -or $a.$key -cne $b.$key) { throw "Kod/bağımlılık farkı: $key" }
    }
}
$bundle = PrivatePath $BundleDirectory
$liveRoot = PrivatePath $LivePlanDirectory
$acceptanceFile = PrivatePath $AcceptancePath
$rehearsalRoot = PrivatePath $RehearsalPlanDirectory
$profileFile = PrivatePath $FinalProfilePath
$output = PrivatePath $OutputDirectory
if (Test-Path -LiteralPath $output) { throw 'Mevcut çıktı üzerine yazılmaz.' }
$index = ReadVerified (Join-Path $bundle 'bundle-hashes.json') $ExpectedBundleHash
$live = ReadVerified (Join-Path $liveRoot 'plan.json') $ExpectedLivePlanHash
$acceptance = ReadVerified $acceptanceFile $ExpectedAcceptanceHash
$rehearsal = ReadVerified (Join-Path $rehearsalRoot 'plan.json') $acceptance.PlanHash
$profile = ReadVerified $profileFile $ExpectedFinalProfileHash
if ($live.Scope -cne 'LIVE' -or $live.Target -cne 'AssistFlow' -or $live.Server -cne 'sql2022' -or
    $live.BundleHash -cne $ExpectedBundleHash -or $acceptance.BundleHash -cne $ExpectedBundleHash -or
    $rehearsal.BundleHash -cne $ExpectedBundleHash -or $rehearsal.Scope -cne 'REHEARSAL' -or
    $rehearsal.Target -cne 'AssistFlow_Performance_Restore' -or -not $acceptance.WritePathVerified -or
    $acceptance.ProductionAuthorizationGranted -ne $false) { throw 'Hedef/paket/prova bağlaması uygun değil.' }
SameCode $live.CodeHashes $acceptance.CodeHashes
SameCode $rehearsal.CodeHashes $acceptance.CodeHashes
$liveApproval = Get-Content (Join-Path $liveRoot 'approval.template.json') -Raw | ConvertFrom-Json
if ($liveApproval.Authorization -cne 'NOT_APPROVED' -or $liveApproval.PlanHash -cne $ExpectedLivePlanHash) { throw 'Teslim şablonu kapalı olmalıdır.' }
if (-not @($profile | Where-Object { $_.Section -eq 'CheckDb' -and $_.DatabaseName -eq 'AssistFlow_Performance_Restore' -and $_.Completed -eq $true -and $_.ErrorRows -eq 0 }).Count) { throw 'Son prova CHECKDB kanıtı eksik.' }
$copies = [Collections.Generic.List[object]]::new()
function AddCopy([string]$source,[string]$relative) {
    $source = [IO.Path]::GetFullPath($source)
    if (-not $source.StartsWith($repo + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Repository dışı girdi.' }
    NoReparse $source
    $dest = [IO.Path]::GetFullPath((Join-Path $output $relative))
    if (-not $dest.StartsWith($output + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Paket dışı yol.' }
    if ((Split-Path $source -Leaf) -match '^(appsettings|\.env)' -or $source -match '\.(pfx|key|pem)$') { throw 'Config/gizli dosya paketlenmez.' }
    if (@($copies | Where-Object Destination -eq $dest).Count) { throw 'Paket dosya yolu tekrarı.' }
    $copies.Add([pscustomobject]@{Source=$source;Destination=$dest;File=$relative;Sha256=(Hash $source)})
}
foreach ($entry in $index) {
    $source = [IO.Path]::GetFullPath((Join-Path $bundle $entry.File))
    if (-not $source.StartsWith($bundle + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Bundle dışı dosya.' }
    $null = PrivatePath $source
    if ((Hash $source) -cne $entry.Sha256) { throw 'Bundle dosyası değişmiş.' }
    AddCopy $source (Join-Path 'review-bundle' $entry.File)
}
AddCopy (Join-Path $bundle 'bundle-hashes.json') 'review-bundle/bundle-hashes.json'
foreach ($pair in @(@($liveRoot,'live-plan'),@($rehearsalRoot,'rehearsal-plan'))) {
    foreach ($name in @('plan.json','approval.template.json')) { AddCopy (Join-Path $pair[0] $name) (Join-Path $pair[1] $name) }
}
AddCopy $acceptanceFile 'adapter-proof/acceptance.json'
AddCopy $profileFile 'adapter-proof/final-profile.json'
$n=0
foreach ($e in $acceptance.Evidence) {
    $source = PrivatePath $e.Path
    if ((Hash $source) -cne $e.Sha256) { throw 'Alt prova kanıtı değişmiş.' }
    AddCopy $source ("adapter-proof/{0:D2}-{1}" -f $n,([IO.Path]::GetFileName($source)))
    $n++
}
$runtime = Join-Path $repo 'tools/Collections.Production/bin/Debug/net9.0'
foreach ($key in @('Adapter','Business','Data','Model','Core')) {
    $name = if ($key -eq 'Adapter') {'Collections.Production'} else {$key}
    if ((Hash (Join-Path $runtime "$name.dll")) -cne $acceptance.CodeHashes.$key) { throw 'Prova runtime sürümü farklı.' }
}
if ((Hash (Join-Path $repo 'tools/Collections.Transfer/CollectionTransferCore.cs')) -cne $acceptance.CodeHashes.EngineSource) { throw 'Çekirdek kod değişmiş.' }
$hostProofHash=$null
if($HostProofDirectory){
    $hostRoot=PrivatePath $HostProofDirectory
    $hostProof=ReadVerified (Join-Path $hostRoot 'result.json') $ExpectedHostProofHash
    if(-not $hostProof.Passed -or -not $hostProof.ToolOwnedHostStopped -or $hostProof.Mode -cne 'REAL_WEBAPI_LOOPBACK_ISOLATED_REHEARSAL' -or $hostProof.Target -cne 'sql2022 / AssistFlow_Performance_Restore' -or $hostProof.Environment -cne 'CollectionRehearsal' -or $hostProof.HttpStatus -ne 404){throw 'Gerçek host prova kabulü eksik.'}
    foreach($key in @('Business','Data','Model','Core')){
        if($hostProof.CodeHashes.$key -cne $acceptance.CodeHashes.$key){throw 'Host ve aktarıcı farklı backend sürümü.'}
    }
    $webRuntime=Join-Path $repo 'WebAPI/bin/Debug/net9.0'
    if((Hash (Join-Path $webRuntime 'WebAPI.dll')) -cne $hostProof.WebApiHash){throw 'WebAPI host sürümü farklı.'}
    AddCopy (Join-Path $hostRoot 'result.json') 'host-proof/result.json'
    AddCopy (Join-Path $hostRoot 'host-stdout.private.log') 'host-proof/host-stdout.private.log'
    foreach($name in @('WebAPI.dll','WebAPI.deps.json','WebAPI.runtimeconfig.json')){AddCopy (Join-Path $webRuntime $name) (Join-Path 'host-runtime' $name)}
    foreach($name in @('Core/Settings/Concrete/StartupExecutionOptions.cs','Business/Services/RehearsalManagedWorker.cs','Business/Services/ManitouStagingSyncBackgroundService.cs','Business/DependencyResolvers/Autofac/AutofacBusinessModule.cs','Data/Seeding/Infrastructure/SeedingExtensions.cs','WebAPI/Middleware/ApplyMigrations.cs','WebAPI/Program.cs','WebAPI/WebAPI.csproj','tools/Invoke-CollectionHostingRehearsal.ps1')){AddCopy (Join-Path $repo $name) (Join-Path 'source-code' $name)}
    foreach($name in @('startup-isolation.md','startup-isolation.example.json','hosting-and-refresh.md')){AddCopy (Join-Path $repo "docs/releases/collections-2026-10-17/$name") (Join-Path 'instructions' $name)}
    $hostProofHash=$ExpectedHostProofHash
}
foreach ($file in Get-ChildItem -LiteralPath $runtime -File -Recurse) {
    if ($file.Extension -eq '.dll' -or $file.Name -in @('Collections.Production.deps.json','Collections.Production.runtimeconfig.json')) {
        AddCopy $file.FullName (Join-Path 'runtime' $file.FullName.Substring($runtime.Length+1))
    }
}
foreach ($name in @('tools/Collections.Production/Program.cs','tools/Collections.Production/ProductionTransfer.cs','tools/Collections.Production/Collections.Production.csproj','tools/Collections.Transfer/CollectionTransferCore.cs','tools/Collections.Import/CollectionPaymentImportIdentity.cs','tools/Collections.Release/LiveTransferPreview.cs','tools/Collections.Production/README.md','tools/Prepare-CollectionCutoverHandoff.ps1')) { AddCopy (Join-Path $repo $name) (Join-Path 'source-code' $name) }
foreach ($name in @('cutover-runbook.md','production-adapter-rehearsal.md','execution-handoff.md')) { AddCopy (Join-Path $repo "docs/releases/collections-2026-10-17/$name") (Join-Path 'instructions' $name) }
# Everything above is validated before creating any package output.
$null = New-Item -ItemType Directory -Path $output
foreach ($copy in $copies) {
    $null = New-Item -ItemType Directory -Path (Split-Path $copy.Destination -Parent) -Force
    Copy-Item -LiteralPath $copy.Source -Destination $copy.Destination
    if ((Hash $copy.Destination) -cne $copy.Sha256) { throw 'Kopyalama doğrulaması başarısız; çıktı teslim kabul edilmez.' }
}
$manifest = [ordered]@{
    Version='collection-cutover-handoff-v1';Status='REVIEW_ONLY_NO_PRODUCTION_AUTHORIZATION';CreatedAtUtc=[DateTimeOffset]::UtcNow
    Target='sql2022 / AssistFlow';Source='MGS';ProductionApplyAllowed=$false;DatabaseConnectionsOpened=0
    BundleHash=$ExpectedBundleHash;LivePlanHash=$ExpectedLivePlanHash;AcceptanceHash=$ExpectedAcceptanceHash;FinalProfileHash=$ExpectedFinalProfileHash
    HostProofHash=$hostProofHash
    WritePathVerified=$true;OriginalRepositoryRoot=$repo;PathBoundPlans=$true;PortableExecutionValidated=$false
    Note='İnceleme/teslim paketi; eski kesit yürütülemez. Başka host/yol veya son kesit/paket/kod değişiminde yeniden plan ve aynı pakete bağlı prova gerekir. Config/credential ve gerçek arşiv/CDN nesneleri dahil değildir.'
    Files=@($copies | Select-Object File,Sha256)
}
$manifestFile = Join-Path $output 'handoff-manifest.json'
[IO.File]::WriteAllText($manifestFile,($manifest | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Write-Output "Offline teslim paketi hazır: $($copies.Count) doğrulanmış dosya. Manifest SHA-256: $(Hash $manifestFile). Canlı uygulama kapalı; DB bağlantısı yok."
