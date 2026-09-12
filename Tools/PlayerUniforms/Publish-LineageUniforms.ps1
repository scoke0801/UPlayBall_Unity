# 완성된 유니폼 이미지를 그대로 등록하고 시즌의 원본 Franchise ID에서 의상을 발급한다.
param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Publish-MatchUniformColors.ps1') -VerifyOnly:$VerifyOnly
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$production = Join-Path $repository 'output/imagegen/player-portraits/uniforms-v3'
$target = Join-Path $repository 'Assets/Resources/UI/Portraits'
$policy = Get-Content (Join-Path $PSScriptRoot 'lineage-uniforms.json') -Raw | ConvertFrom-Json
$catalog = Get-Content (Join-Path $production 'catalog.json') -Raw | ConvertFrom-Json
$faces = Get-Content (Join-Path $target 'player_portrait_assignments.json') -Raw | ConvertFrom-Json
$utf8 = New-Object Text.UTF8Encoding($false)
$franchises = @{}
$uniforms = @{}
foreach ($lineage in $policy.lineages) {
    $uniforms.Add($lineage.uniform, $lineage.id)
    foreach ($id in $lineage.franchises) { $franchises.Add($id, $lineage.uniform) }
}
if ($uniforms.Count -ne 10 -or $catalog.AppearanceCount -ne $faces.portraitCount) { throw '유니폼/얼굴 카탈로그 불일치' }
$aliases = @{}
foreach ($alias in $faces.aliases) { $aliases.Add($alias.id, $alias.person) }
$seasons = @{}
foreach ($file in Get-ChildItem (Join-Path $repository 'Assets/10.Datas/HistoricalSimulation/1982-2025/Years') -Filter '*.json' | Sort-Object Name) {
    foreach ($season in (Get-Content $file.FullName -Raw | ConvertFrom-Json).playerSeasons) {
        if (-not $franchises.ContainsKey($season.originFranchiseId)) { throw "계보 미등록: $($season.originFranchiseId)" }
        if ($aliases[$season.playerSeasonId] -ne $season.playerPersonId) { throw "얼굴 발급 불일치: $($season.playerSeasonId)" }
        $seasons.Add($season.playerSeasonId, $season.originFranchiseId)
    }
}
if ($seasons.Count -ne $aliases.Count) { throw '시즌 누락' }
$payload = [ordered]@{
    artVersion = $policy.artVersion
    lineages = $policy.lineages
    seasons = @($seasons.Keys | Sort-Object | ForEach-Object { @{ id = $_; franchise = $seasons[$_] } })
}
$json = $payload | ConvertTo-Json -Depth 6 -Compress
$catalogPath = Join-Path $target 'player_uniform_assignments.json'
if ($VerifyOnly -and (Get-Content $catalogPath -Raw).Trim() -ne $json) { throw '현재 시즌/정책과 런타임 유니폼 카탈로그 불일치' }
$template = Get-Content (Join-Path $target 'Players/face-0001.png.meta') -Raw
function Write-Metadata([string]$Path, [string]$Template) {
    if (Test-Path -LiteralPath "$Path.meta") { return }
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $guid = ([BitConverter]::ToString($hash.ComputeHash($utf8.GetBytes($Path.Substring($repository.Length).Replace('\','/'))))).Replace('-', '').Substring(0,32).ToLowerInvariant() }
    finally { $hash.Dispose() }
    $text = if ($Template) { [regex]::Replace($Template, '(?m)^guid: \w+', "guid: $guid") } else { "fileFormatVersion: 2`nguid: $guid`n" }
    [IO.File]::WriteAllText("$Path.meta", $text, $utf8)
}
$keys = @{}
$guids = @{}
foreach ($portrait in $catalog.Portraits) {
    $key = "$($portrait.UniformId)/$($portrait.AppearanceId)"
    $keys.Add($key, $true)
    if (-not $uniforms.ContainsKey($portrait.UniformId) -or $portrait.ProtectedPixelChanges -ne 0 -or $portrait.AlphaChanges -ne 0) { throw "미검증 유니폼: $key" }
    $source = Join-Path $production $portrait.File
    if ((Get-FileHash -LiteralPath $source).Hash -ne $portrait.Sha256) { throw "원본 해시 불일치: $key" }
    $destination = Join-Path $target "Uniforms/$key.png"
    if (-not $VerifyOnly) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        if (-not (Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath $source -Destination $destination }
        Write-Metadata $destination $template
    }
    if ((Get-FileHash -LiteralPath $destination).Hash -ne $portrait.Sha256) { throw "등록 이미지 불일치: $key" }
    $metadata = Get-Content -LiteralPath "$destination.meta" -Raw
    $guid = [regex]::Match($metadata, '(?m)^guid: (\w+)').Groups[1].Value
    if (-not $guid -or $metadata -notmatch 'spriteMode: 1' -or $metadata -notmatch 'textureType: 8') { throw "Sprite 등록 오류: $key" }
    $guids.Add($guid, $true)
}
foreach ($uniform in $uniforms.Keys) {
    for ($face = 1; $face -le $faces.portraitCount; $face++) {
        if (-not $keys.ContainsKey(('{0}/face-{1:D4}' -f $uniform, $face))) { throw '외형·유니폼 조합 누락' }
    }
}
if ($keys.Count -ne $faces.portraitCount * $uniforms.Count) { throw '외형·유니폼 조합 수 불일치' }
if (-not $VerifyOnly) {
    Write-Metadata (Join-Path $target 'Uniforms') ''
    foreach ($uniform in $uniforms.Keys) { Write-Metadata (Join-Path $target "Uniforms/$uniform") '' }
    [IO.File]::WriteAllText($catalogPath, $json, $utf8)
    Write-Metadata $catalogPath ''
}
$report = [ordered]@{
    lineages = $uniforms.Count; franchises = $franchises.Count; seasons = $seasons.Count
    portraits = $keys.Count; missingCombinations = 0; sourceAndPublishedHashesVerified = $keys.Count
    usage = @($policy.lineages | ForEach-Object {
        $lineage = $_
        @{ lineage = $lineage.id; uniform = $lineage.uniform; seasons = @($seasons.Values | Where-Object { $_ -in $lineage.franchises }).Count }
    })
}
$reportPath = Join-Path $repository 'output/portrait-validation/uniform-issuance-audit.json'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportPath)) | Out-Null
$reportJson = $report | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($reportPath, $reportJson, $utf8)
$reportJson
