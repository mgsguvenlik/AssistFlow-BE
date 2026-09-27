param(
    [Parameter(Mandatory)][string]$SettingsPath,
    [ValidateSet('AssistFlowTest','AssistFlow')][string]$Database='AssistFlowTest',
    [Parameter(Mandatory)][string]$OutputPath
)

# K03: yalnız SELECT. Bu araç müşteri, tanım veya şema oluşturmaz.
$ErrorActionPreference='Stop'
$settings=Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$settings.AppSettings.MSSQLConnectionString)
if($builder.DataSource -ne '192.168.1.8'){throw 'Beklenmeyen kaynak sunucusu.'}
$builder['Initial Catalog']=$Database
$builder['Connect Timeout']=15
$policyPath=Join-Path $PSScriptRoot '../docs/data/collection-customer-classification.json'
$policy=Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
$culture=[Globalization.CultureInfo]::GetCultureInfo('tr-TR')
function Normalize([object]$Value){ return ([string]$Value).Trim().ToUpper($culture) }
function Name-Key([object]$Value){ return [regex]::Replace((Normalize $Value),'[^\p{L}\p{Nd}]','') }
function Hash-Text([string]$Value){ return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value))) }
$groupCodes=@($policy.explicitGroupCodes+$policy.legacyVerifiedGroupCodes | ForEach-Object {Normalize $_} | Sort-Object -Unique)
foreach($code in $groupCodes){
    if($policy.excludedCodes -contains $code -or @($policy.excludedPrefixes | Where-Object {$code.StartsWith($_)}).Count){throw 'Kapsam kararında çakışma var.'}
}
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Read-Rows([string]$Sql){
    if($Sql -match '(?i)\b(UPDATE|INSERT|DELETE|MERGE|ALTER|CREATE|DROP|TRUNCATE|EXEC|INTO)\b'){throw 'Yalnız okuma sorgusu kabul edilir.'}
    $command=$connection.CreateCommand()
    try {
        $command.CommandText=$Sql
        $command.CommandTimeout=90
        $reader=$command.ExecuteReader()
        try {
            while($reader.Read()){
                $row=[ordered]@{}
                for($i=0;$i -lt $reader.FieldCount;$i++){ $row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)} }
                [pscustomobject]$row
            }
        } finally {$reader.Dispose()}
    } finally {$command.Dispose()}
}
try {
    $connection.Open()
    $source=@(Read-Rows @'
SELECT p.CustomerID AS LegacyCustomerId,p.Name,p.SubscriberNo,p.AccountNo,p.GroupCardNo AS GroupCode,
 CASE WHEN EXISTS(SELECT 1 FROM MGS.Definition.[Group] g WHERE g.Code=p.GroupCardNo AND g.GroupType=N'Kurumsal') THEN 1 ELSE 0 END AS IsCorporate,
 (SELECT COUNT(*) FROM MGS.Core.Contract k WHERE k.CustomerID=p.CustomerID) AS ContractCount
FROM MGS.Core.Customer p WHERE p.Type='G' ORDER BY p.CustomerID
'@)
    $groups=@(Read-Rows 'SELECT Id,Code,GroupName FROM dbo.CustomerGroups ORDER BY Id')
    $types=@(Read-Rows 'SELECT Id,Code,Name FROM dbo.CustomerType ORDER BY Id')
    # Silinmiş kayıtlar da yeniden oluşturma çakışmalarının incelemesine dahildir.
    $customers=@(Read-Rows 'SELECT Id,SubscriberCode,SubscriberCompany,CustomerGroupId,CustomerTypeId,TenantId,IsDeleted FROM dbo.Customers ORDER BY Id')
    $customerIndex=@{}
    foreach($customer in $customers){
        $keys=@()
        $subscriber=Normalize $customer.SubscriberCode
        $name=Name-Key $customer.SubscriberCompany
        if($subscriber){$keys+=('S:'+ $subscriber)}
        if($name){$keys+=('N:'+ $name); if($name.Length -ge 8){$keys+=('P:'+ $name.Substring(0,8))}}
        foreach($key in $keys){
            if(!$customerIndex.ContainsKey($key)){$customerIndex[$key]=[Collections.Generic.List[object]]::new()}
            $customerIndex[$key].Add($customer)
        }
    }
    $scoped=@($source | Where-Object {$groupCodes -contains (Normalize $_.GroupCode)})
    $duplicateCodes=@($scoped | Group-Object {Normalize $_.GroupCode} | Where-Object Count -gt 1 | Select-Object -ExpandProperty Name)
    $duplicateSubscribers=@($scoped | Where-Object {Normalize $_.SubscriberNo} | Group-Object {Normalize $_.SubscriberNo} | Where-Object Count -gt 1 | Select-Object -ExpandProperty Name)
    $duplicateNames=@($scoped | Where-Object {Name-Key $_.Name} | Group-Object {Name-Key $_.Name} | Where-Object Count -gt 1 | Select-Object -ExpandProperty Name)
    $rows=@(foreach($parent in $scoped){
        $code=Normalize $parent.GroupCode
        $targetGroups=@($groups | Where-Object {(Normalize $_.Code) -eq $code})
        $keys=@('S:'+ $code)
        $subscriber=Normalize $parent.SubscriberNo
        $name=Name-Key $parent.Name
        if($subscriber){$keys+=('S:'+ $subscriber)}
        if($name){$keys+=('N:'+ $name);if($name.Length -ge 8){$keys+=('P:'+ $name.Substring(0,8))}}
        $candidates=@(foreach($key in $keys){if($customerIndex.ContainsKey($key)){$customerIndex[$key]}}) | Sort-Object Id -Unique
        $reasons=[Collections.Generic.List[string]]::new()
        if($targetGroups.Count -ne 1){$reasons.Add('GROUP_NOT_UNIQUE')}
        if($duplicateCodes -contains $code){$reasons.Add('SOURCE_PARENT_NOT_UNIQUE')}
        if($subscriber -and $duplicateSubscribers -contains $subscriber){$reasons.Add('SOURCE_SUBSCRIBER_NOT_UNIQUE')}
        if($name -and $duplicateNames -contains $name){$reasons.Add('SOURCE_NAME_NOT_UNIQUE')}
        if(!$name){$reasons.Add('SOURCE_NAME_EMPTY')}
        if(@($candidates).Count){$reasons.Add('EXISTING_CUSTOMER_CANDIDATE')}
        [pscustomobject][ordered]@{
            LegacyCustomerId=$parent.LegacyCustomerId; GroupCode=$code; Name=$parent.Name
            SubscriberNo=$parent.SubscriberNo; AccountNo=$parent.AccountNo
            IsCorporate=[bool]$parent.IsCorporate; LegacyContractCount=$parent.ContractCount
            TargetGroupIds=@($targetGroups.Id)
            Status=if($reasons.Count){'NeedsReview'}else{'NewCardCandidate'}
            Reasons=@($reasons)
            ExistingCandidates=@($candidates | Select-Object Id,SubscriberCode,SubscriberCompany,CustomerGroupId,CustomerTypeId,TenantId,IsDeleted)
        }
    })
    $material=[ordered]@{Database=$Database;ToolHash=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;PolicyHash=(Get-FileHash -LiteralPath $policyPath -Algorithm SHA256).Hash;Source=$scoped;Groups=$groups;Types=$types;Customers=$customers;Decisions=$rows}
    $report=[ordered]@{
        Database=$Database;GeneratedAt=[DateTimeOffset]::UtcNow.ToString('O')
        PlanHash=(Hash-Text (ConvertTo-Json -InputObject $material -Depth 8 -Compress))
        ReadyToApply=$false
        Note='Salt-okunur önizleme. Adın noktalama/boşluk normalizasyonu veya ilk sekiz harf benzerliği yalnız inceleme adayıdır; otomatik eşleşme değildir. Uygulama öncesi kaynak ve hedef yeniden okunmalıdır.'
        ParentCount=$rows.Count
        NewCardCandidates=@($rows | Where-Object Status -eq 'NewCardCandidate').Count
        NeedsReview=@($rows | Where-Object Status -eq 'NeedsReview').Count
        Rows=$rows
    }
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding utf8
    Write-Output ("{0}: {1} üst kart; {2} yeni kart adayı, {3} inceleme. Veri değiştirilmedi." -f $Database,$report.ParentCount,$report.NewCardCandidates,$report.NeedsReview)
    Write-Output ("Plan SHA-256: "+$report.PlanHash)
} finally {$connection.Dispose()}
