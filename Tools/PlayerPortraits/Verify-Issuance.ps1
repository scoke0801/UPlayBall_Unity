# 기존 발급 정본을 변경하지 않고 현재 역사 콘텐츠와 Unity 이미지 등록을 대조한다.
param([string]$ReportPath = 'output/portrait-validation/issuance-audit.json')
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$content = Join-Path $repository 'Assets/10.Datas/HistoricalSimulation/1982-2025'
$portraits = Join-Path $repository 'Assets/Resources/UI/Portraits'
$catalog = Get-Content -LiteralPath (Join-Path $portraits 'player_portrait_assignments.json') -Raw | ConvertFrom-Json
$persons = @{}
$counts = New-Object int[] $catalog.portraitCount
foreach ($person in $catalog.persons) {
    if ($persons.ContainsKey($person.id)) { throw "중복 인물: $($person.id)" }
    if ($person.appearance -lt 1 -or $person.appearance -gt $counts.Length) { throw "외형 범위 오류: $($person.id)" }
    $persons.Add($person.id, [int]$person.appearance)
    $counts[$person.appearance - 1]++
}
$sourcePersons = (Get-Content -LiteralPath (Join-Path $content 'player_persons.json') -Raw | ConvertFrom-Json).items
if ($sourcePersons.Count -ne $persons.Count) { throw '현재 콘텐츠와 발급 인물 수 불일치' }
foreach ($person in $sourcePersons) {
    if (-not $persons.ContainsKey($person.playerPersonId)) { throw "미발급 인물: $($person.playerPersonId)" }
}
$aliases = @{}
foreach ($alias in $catalog.aliases) {
    if (-not $persons.ContainsKey($alias.person)) { throw "별칭 인물 누락: $($alias.id)" }
    $aliases.Add($alias.id, $alias.person)
}
$groups = @{}
$lineages = @{}
$seasonIds = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $content 'Years') -Filter '*.json') {
    foreach ($season in (Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json).playerSeasons) {
        $id = $season.playerSeasonId
        $person = $season.playerPersonId
        if ($aliases[$id] -ne $person) { throw "시즌 별칭 누락/불일치: $id" }
        $seasonIds.Add($id, $true)
        $group = $season.originTeamSeasonKey
        if (-not $groups.ContainsKey($group)) { $groups[$group] = @{} }
        $groups[$group][$person] = $persons[$person]
        $lineage = $season.originFranchiseId
        if (-not $lineages.ContainsKey($lineage)) { $lineages[$lineage] = @{} }
        $lineages[$lineage][$person] = $true
    }
}
if ($seasonIds.Count -ne $aliases.Count) { throw '삭제된 시즌 별칭이 남아 있습니다.' }
foreach ($group in $groups.Keys) {
    if (@($groups[$group].Values | Select-Object -Unique).Count -ne $groups[$group].Count) {
        throw "동일 구단·시즌의 서로 다른 인물에게 얼굴 중복: $group"
    }
}
$usage = $counts | Measure-Object -Minimum -Maximum
if ($usage.Minimum -le 0 -or $usage.Maximum - $usage.Minimum -gt 1) { throw '외형 사용 불균등' }
Add-Type -AssemblyName System.Drawing
$guids = @{}
for ($face = 1; $face -le $catalog.portraitCount; $face++) {
    $name = 'face-{0:D4}.png' -f $face
    $path = Join-Path $portraits "Players/$name"
    $metadata = Get-Content -LiteralPath "$path.meta" -Raw
    $guid = [regex]::Match($metadata, '(?m)^guid: (\w+)').Groups[1].Value
    if (-not $guid -or $guids.ContainsKey($guid)) { throw "잘못된 이미지 GUID: $name" }
    $guids.Add($guid, $true)
    if ($metadata -notmatch 'spriteMode: 1' -or $metadata -notmatch 'textureType: 8') { throw "Sprite 임포트 오류: $name" }
    $bitmap = [Drawing.Bitmap]::FromFile($path)
    try {
        if (($bitmap.PixelFormat -band [Drawing.Imaging.PixelFormat]::Alpha) -eq 0) { throw "알파 채널 없음: $name" }
        if ($bitmap.GetPixel(0, 0).A -ne 0 -or $bitmap.GetPixel($bitmap.Width - 1, 0).A -ne 0) { throw "상단 배경 잔여: $name" }
    } finally { $bitmap.Dispose() }
    $revision = if ($face -ge 17 -and $face -le 176) { 'r06' } else { 'issued-v1' }
    $source = Join-Path $repository ("output/imagegen/player-portraits/production-576-v1/faces/transparent/{0}/face-{1:D4}__{0}.png" -f $revision, $face)
    if ((Get-FileHash -LiteralPath $path).Hash -ne (Get-FileHash -LiteralPath $source).Hash) { throw "제작 정본과 등록 이미지 불일치: $name" }
}
$report = [ordered]@{
    persons = $persons.Count
    seasons = $aliases.Count
    portraits = $catalog.portraitCount
    minimumUsage = $usage.Minimum
    maximumUsage = $usage.Maximum
    usageHistogram = @($counts | Group-Object | Sort-Object Name | ForEach-Object { @{ personsPerPortrait = [int]$_.Name; portraits = $_.Count } })
    verifiedTeamSeasons = $groups.Count
    maximumLineagePopulation = ($lineages.Values | ForEach-Object Count | Measure-Object -Maximum).Maximum
    missingAssignments = 0
    duplicateFacesWithinTeamSeason = 0
    verifiedImageSourcesAndImportSettings = $guids.Count
}
$destination = Join-Path $repository $ReportPath
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
$json = $report | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($destination, $json, (New-Object Text.UTF8Encoding($false)))
$json
