param([switch]$Apply, [string]$ExpectedPlanHash)

$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$settings=Get-Content (Join-Path $repository 'WebAPI/appsettings.Development.json') -Raw | ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$settings.AppSettings.MSSQLConnectionString)
if($builder.DataSource -ne '192.168.1.8' -or $builder.InitialCatalog -ne 'AssistFlowTest') {
    throw 'Bu işlem yalnız onaylı AssistFlowTest bağlantısıyla çalışır.'
}
$builder['Connect Timeout']=15
$builder['Encrypt']=$false
$rules=Get-Content (Join-Path $repository 'docs/data/collection-customer-classification.json') -Raw | ConvertFrom-Json
$groups=@($rules.explicitGroupCodes + $rules.legacyVerifiedGroupCodes | Sort-Object -Unique)
$ruleRows=@(@($rules.individualCodes | ForEach-Object { [ordered]@{Code=$_;Kind='Individual'} }) + @($groups | ForEach-Object { [ordered]@{Code=$_;Kind='Group'} }))
foreach($row in $ruleRows) {
    if($row.Code -in $rules.excludedCodes -or @($rules.excludedPrefixes | Where-Object {$row.Code.StartsWith($_,[StringComparison]::OrdinalIgnoreCase)}).Count -gt 0) {
        throw "Tahsilat dışı kod sınıflandırmaya eklenemez: $($row.Code)"
    }
}
$rulesJson=ConvertTo-Json -InputObject $ruleRows -Compress
$policyJson=ConvertTo-Json -InputObject $rules -Depth 5 -Compress
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)

function Read-Value([string]$Query) {
    $command=$connection.CreateCommand()
    try {$command.CommandText=$Query; $command.CommandTimeout=60; return $command.ExecuteScalar()}
    finally {$command.Dispose()}
}
function Hash-Text([string]$Value) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value)))
}
function Sql-List($Codes) {
    return ($Codes | ForEach-Object { "N'" + $_.Replace("'","''") + "'" }) -join ','
}

try {
    $connection.Open()
    $types=@((Read-Value 'SELECT Id,Name,Code FROM dbo.CustomerType ORDER BY Id FOR JSON PATH') | ConvertFrom-Json)
    $individual=@($types | Where-Object {$_.Code -in @('N','BRYSL01')})
    $group=@($types | Where-Object {$_.Code -in @('GM','GRP01')})
    if($individual.Count -ne 1 -or $group.Count -ne 1) {
        throw 'Bireysel ve grup tipi tekil olmalıdır; eksik/çakışan tanım önce çözümlenmelidir.'
    }
    $individualId=[long]$individual[0].Id
    $groupId=[long]$group[0].Id
    if($individualId -eq 6 -or $groupId -eq 6){throw 'BANKA tipi sınıflandırmada kullanılamaz.'}
    $individualSql=Sql-List $rules.individualCodes
    $groupSql=Sql-List $groups
    $oldDefinition=[string](Read-Value "SELECT OBJECT_DEFINITION(OBJECT_ID(N'stg.sp_SyncCustomers'))")
    if(-not $oldDefinition){throw 'Senkronizasyon prosedürü okunamadı.'}
    $marker='-- CollectionCustomerClassification: 2026-09-26'
    if($oldDefinition.Contains($marker)) {
        if(-not $oldDefinition.Contains("IN ($individualSql) THEN $individualId") -or
            -not $oldDefinition.Contains("IN ($groupSql) THEN $groupId")) {
            throw 'Uygulanmış senkronizasyon kuralı bu karar listesiyle uyuşmuyor.'
        }
        $newDefinition=$oldDefinition
    } else {
        $pattern='(?is)ISNULL\(\s*TRY_CAST\(c\.\[CustomerTypeId\] AS BIGINT\),\s*0\s*\) AS CustomerTypeId'
        if([regex]::Matches($oldDefinition,$pattern).Count -ne 1) {throw 'Beklenen müşteri tipi ifadesi tekil bulunamadı.'}
        $expression=@"
$marker
                CASE
                    WHEN TRY_CAST(c.[CustomerTypeId] AS BIGINT)=6 THEN 6
                    WHEN UPPER(LTRIM(RTRIM(cgTarget.Code))) COLLATE Turkish_CI_AS IN ($individualSql) THEN $individualId
                    WHEN UPPER(LTRIM(RTRIM(cgTarget.Code))) COLLATE Turkish_CI_AS IN ($groupSql) THEN $groupId
                    ELSE ISNULL(TRY_CAST(c.[CustomerTypeId] AS BIGINT),0)
                END AS CustomerTypeId
"@
        $newDefinition=[regex]::Replace($oldDefinition,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($m) $expression})
        $assignment='Target.\[CustomerTypeId\]\s*=\s*Source.\[CustomerTypeId\]'
        if([regex]::Matches($newDefinition,$assignment).Count -ne 1){throw 'Beklenen müşteri tipi ataması tekil bulunamadı.'}
        $newDefinition=[regex]::Replace($newDefinition,$assignment,'Target.[CustomerTypeId] = CASE WHEN Target.[CustomerTypeId]=6 THEN Target.[CustomerTypeId] ELSE Source.[CustomerTypeId] END')
        $newDefinition=[regex]::Replace($newDefinition,'(?i)^\s*CREATE\s+(OR\s+ALTER\s+)?PROCEDURE','ALTER PROCEDURE')
    }

    $customersQuery=@"
SELECT (SELECT c.Id,c.CustomerTypeId,c.CustomerGroupId,c.TenantId,UPPER(LTRIM(RTRIM(g.Code))) AS Code,
 CASE WHEN UPPER(LTRIM(RTRIM(g.Code))) COLLATE Turkish_CI_AS IN ($individualSql) THEN $individualId ELSE $groupId END AS TargetTypeId
FROM dbo.Customers c JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId
WHERE c.IsDeleted=0 AND c.CustomerTypeId IN ($individualId,$groupId)
AND UPPER(LTRIM(RTRIM(g.Code))) COLLATE Turkish_CI_AS IN ($individualSql,$groupSql)
ORDER BY c.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
"@
    $customersJson=[string](Read-Value $customersQuery)
    $sql=Get-Content (Join-Path $repository 'docs/sql/20260926_ClassifyCollectionCustomers.sql') -Raw
    $planHash=Hash-Text ($policyJson + $rulesJson + $customersJson + $oldDefinition + $newDefinition + $sql)
    if($Apply -and $ExpectedPlanHash -ne $planHash){throw "Plan değişti veya hash verilmedi. Güncel hash: $planHash"}

    # Önceki değerler ve prosedür tanımı, yazmadan önce Git dışındaki rapora kaydedilir.
    $report=Join-Path $PSScriptRoot ('Collections.Import/snapshots/customer-classification-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
    [void](New-Item -ItemType Directory -Path $report)
    $oldDefinition | Set-Content (Join-Path $report 'sync-before.sql') -Encoding utf8
    $newDefinition | Set-Content (Join-Path $report 'sync-after.sql') -Encoding utf8
    $customersJson | Set-Content (Join-Path $report 'customers-before.json') -Encoding utf8
    $types | ConvertTo-Json | Set-Content (Join-Path $report 'types-before.json') -Encoding utf8
    $policyJson | Set-Content (Join-Path $report 'policy.json') -Encoding utf8
    @($customersJson | ConvertFrom-Json) | Export-Csv (Join-Path $report 'customers.csv') -NoTypeInformation -Encoding utf8
    $planHash | Set-Content (Join-Path $report 'plan.sha256') -Encoding ascii

    $command=$connection.CreateCommand()
    try {
        $command.CommandTimeout=90
        $command.CommandText=$sql
        [void]$command.Parameters.Add('@Apply',[Data.SqlDbType]::Bit)
        $command.Parameters['@Apply'].Value=[bool]$Apply
        foreach($param in @(@('IndividualId',$individualId),@('GroupId',$groupId))) {
            [void]$command.Parameters.Add('@'+$param[0],[Data.SqlDbType]::BigInt)
            $command.Parameters['@'+$param[0]].Value=$param[1]
        }
        foreach($param in @(@('RulesJson',$rulesJson),@('OldDefinition',$oldDefinition),@('NewDefinition',$newDefinition),@('ExpectedCustomers',$customersJson))) {
            [void]$command.Parameters.Add('@'+$param[0],[Data.SqlDbType]::NVarChar,-1)
            $command.Parameters['@'+$param[0]].Value=$param[1]
        }
        $table=[Data.DataTable]::new()
        $adapter=[Data.SqlClient.SqlDataAdapter]::new($command)
        [void]$adapter.Fill($table)
        $result=$table | Select-Object * -ExcludeProperty RowError,RowState,Table,ItemArray,HasErrors
        $result | ConvertTo-Json | Set-Content (Join-Path $report 'result.json') -Encoding utf8
        $result | Format-Table -AutoSize
        "PlanHash: $planHash"
        "Report: $report"
    } finally {$command.Dispose()}
} finally {$connection.Dispose()}
