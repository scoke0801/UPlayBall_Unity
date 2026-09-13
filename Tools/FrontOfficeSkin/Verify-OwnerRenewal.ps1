# 정적 납품 검사. 컴파일·Unity 실행·EditMode/PlayMode 테스트를 수행하지 않는다.
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$root = Join-Path $project 'Assets/10.Datas/Resources/UI/BaseballFrontOffice/V2'
$expectedBorders = @{
    Surface = 'spriteBorder: {x: 24, y: 24, z: 24, w: 24}'
    Speech = 'spriteBorder: {x: 128, y: 208, z: 160, w: 184}'
}
$results = @()
foreach ($name in @('Surface', 'Speech')) {
    $file = Join-Path $root "Frames/UI_Frame_$name.png"
    $bytes = [IO.File]::ReadAllBytes($file)
    $width = [int]$bytes[16] * 16777216 + [int]$bytes[17] * 65536 + [int]$bytes[18] * 256 + [int]$bytes[19]
    $height = [int]$bytes[20] * 16777216 + [int]$bytes[21] * 65536 + [int]$bytes[22] * 256 + [int]$bytes[23]
    $meta = [IO.File]::ReadAllText($file + '.meta')
    foreach ($contract in @('spriteMode: 1', 'spriteMeshType: 0', 'spritePixelsToUnits: 100',
        'enableMipMap: 0', 'wrapU: 1', 'wrapV: 1', 'textureType: 8', $expectedBorders[$name])) {
        if (!$meta.Contains($contract)) { throw "$name : $contract 누락" }
    }
    if ($width -ne 1536 -or $height -ne 1024) { throw "$name 크기 불일치" }
    if ($name -eq 'Speech' -and $bytes[25] -ne 6) { throw '말풍선 RGBA 누락' }
    $results += [pscustomobject]@{ Asset = "UI_Frame_$name"; Size = "$width x $height";
        Border = $expectedBorders[$name]; Sha256 = (Get-FileHash -LiteralPath $file).Hash; Passed = $true }
}
$guidMap = @{}
Get-ChildItem $root -Recurse -Filter '*.png.meta' | ForEach-Object {
    $guid = [regex]::Match([IO.File]::ReadAllText($_.FullName), 'guid: ([a-f0-9]+)').Groups[1].Value
    if ($guidMap.ContainsKey($guid)) { throw '이미지 GUID 중복' }
    $guidMap[$guid] = $_.FullName
}
foreach ($name in @('UI_Surface', 'UI_Control')) {
    $path = Join-Path $root "Prefabs/$name.prefab"
    $source = [IO.File]::ReadAllText($path)
    $guid = [regex]::Match($source, 'm_Sprite: \{fileID: 21300000, guid: ([a-f0-9]+)').Groups[1].Value
    if (!$guidMap.ContainsKey($guid) -or !$source.Contains('m_Type: 1') -or !$source.Contains('m_Father: {fileID: 0}')) {
        throw "$name 직렬화 참조 불일치"
    }
    if (!(Test-Path ($path + '.meta'))) { throw "$name meta 누락" }
    $factory = [IO.File]::ReadAllText((Join-Path $project 'Assets/02.Scripts/Presentation/Owner/OwnerWorkspaceUiFactory.cs'))
    if (!$factory.Contains("Prefabs/$name")) { throw "$name 실제 팩토리 참조 없음" }
    $results += [pscustomobject]@{ Asset = $name; Size = '런타임 LayoutElement'; Border = 'Sliced'; Sha256 = (Get-FileHash $path).Hash; Passed = $true }
}
$destination = Join-Path $project 'docs/reports/owner-ui-renewal/static-assets.json'
$results | ConvertTo-Json -Depth 4 | Set-Content $destination -Encoding UTF8
Write-Output '신규 이미지 2개·실사용 Prefab 2개: 크기/Border/Guid/자원 경로 정적 검사 통과. 렌더 검수는 미진행.'
