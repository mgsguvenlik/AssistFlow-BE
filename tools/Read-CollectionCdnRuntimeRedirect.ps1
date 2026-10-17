param([Parameter(Mandatory=$true)][string]$ReportRoot)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$root=[IO.Path]::GetFullPath((Join-Path $repo $ReportRoot))
if(-not $root.StartsWith((Join-Path $repo '.tools')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Özel rapor kökü gerekli.'}
$output=Join-Path $root 'runtime-redirects.json'
if(Test-Path -LiteralPath $output){throw 'Mevcut rapor üzerine yazılmaz.'}
$line=Get-Content -LiteralPath (Join-Path $repo '../AssistFlow-FE/.env.production')|Where-Object {$_ -match '^VITE_API_PREFIX='}|Select-Object -First 1
$api=[uri]($line.Substring('VITE_API_PREFIX='.Length).Trim())
if($api.Scheme -ne 'https' -or $api.Host -ne 'flowassistapi.mgs.com.tr'){throw 'Onaylı production API hostu gerekli.'}
$settings=Get-Content -LiteralPath (Join-Path $root 'settings.json') -Raw|ConvertFrom-Json
$samples=@(Get-Content -LiteralPath (Join-Path $root 'samples.private.json') -Raw|ConvertFrom-Json)
if($samples.Count -gt 3 -or $settings.KeyPrefix -ne 'uploads' -or $settings.PublicBaseUrl -ne 'https://cdn.flowassist.mgs.com.tr'){throw 'Beklenmeyen örnek/namespace kapsamı.'}
$handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false
$http=[Net.Http.HttpClient]::new($handler);$http.Timeout=[timespan]::FromSeconds(20)
try{
 $results=@(foreach($sample in $samples){
  $name=[string]$sample.StoredName
  if([IO.Path]::GetFileName($name) -ne $name -or $name.Contains('\') -or $name.Contains(':')){throw 'Güvensiz dosya adı.'}
  $expected=$settings.PublicBaseUrl.TrimEnd('/')+'/uploads/'+[uri]::EscapeDataString($name)
  $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get,($api.GetLeftPart([UriPartial]::Authority)+'/uploads/'+[uri]::EscapeDataString($name)))
  $status=$null;$matched=$false;$hostName=$null;$prefix=$false;$failure=$null
  try{
   $response=$http.SendAsync($request,[Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
   try{
    $status=[int]$response.StatusCode
    if($response.Headers.Location -and $response.Headers.Location.IsAbsoluteUri){$hostName=$response.Headers.Location.Host;$prefix=$response.Headers.Location.AbsolutePath.StartsWith('/uploads/');$matched=$status -in @(301,302,307,308) -and $response.Headers.Location.AbsoluteUri -eq $expected}
   }finally{$response.Dispose()}
  }catch{$failure=$_.Exception.GetType().Name}finally{$request.Dispose()}
  [ordered]@{SourceTable=$sample.Table;Id=$sample.Id;ApiStatus=$status;RedirectHost=$hostName;ProductionPrefixMatched=$prefix;ExactExpectedRedirectMatched=$matched;FollowedRedirect=$false;ReadResponseBody=$false;FailureClass=$failure}
 })
 [ordered]@{ObservedAtUtc=[datetimeoffset]::UtcNow;ApiHost=$api.Host;Results=$results;Methods=@('GET');Requests=$samples.Count;DatabaseWrites=0;Uploads=0;Deletes=0;FullDeployedConfigurationVerified=$false}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $output -Encoding utf8NoBOM
 'Production dosya yönlendirmeleri salt-okunur kontrol edildi; response body okunmadı, redirect izlenmedi.'
}finally{$http.Dispose()}
