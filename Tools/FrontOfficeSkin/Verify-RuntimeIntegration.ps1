# Unity를 실행하지 않고 납품 자원의 임포트 계약과 런타임 연결을 확인한다.
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$assetRoot = Join-Path $project 'Assets/10.Datas/Resources/UI/BaseballFrontOffice/V2'
$entries = Get-Content (Join-Path $PSScriptRoot 'AssetManifest.json') -Raw | ConvertFrom-Json
$results = foreach ($entry in $entries) {
    $path = Join-Path $assetRoot ($entry.Folder + '/' + $entry.File + '.png')
    $meta = Get-Content ($path + '.meta') -Raw
    $bytes = [IO.File]::ReadAllBytes($path)
    $width = [int]$bytes[16] * 16777216 + [int]$bytes[17] * 65536 + [int]$bytes[18] * 256 + [int]$bytes[19]
    $height = [int]$bytes[20] * 16777216 + [int]$bytes[21] * 65536 + [int]$bytes[22] * 256 + [int]$bytes[23]
    $border = 'spriteBorder: {x: ' + $entry.Left + ', y: ' + $entry.Bottom + ', z: ' + $entry.Right + ', w: ' + $entry.Top + '}'
    $expected = @('spriteMode: 1', 'spriteMeshType: 0', 'spritePixelsToUnits: 100', 'enableMipMap: 0',
        'filterMode: 1', 'wrapU: 1', 'wrapV: 1', 'wrapW: 1', 'alphaIsTransparency: 1', 'textureType: 8', $border)
    $missing = @($expected | Where-Object { !$meta.Contains($_) })
    $valid = $missing.Count -eq 0 -and $width -eq $entry.Width -and $height -eq $entry.Height -and $bytes[25] -eq 6
    [pscustomobject]@{ Asset = $entry.File; Width = $width; Height = $height; Rgba = ($bytes[25] -eq 6)
        ImportContract = $valid; Missing = $missing; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
}
if ($results.Count -ne 29 -or @($results | Where-Object { !$_.ImportContract }).Count -gt 0) {
    $results | ConvertTo-Json -Depth 4
    throw 'V2 이미지 또는 임포트 계약 불일치'
}
$destination = Join-Path $project 'output/front-office-v2-runtime'
[IO.Directory]::CreateDirectory($destination) | Out-Null
$results | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $destination 'asset-verification.json') -Encoding UTF8
$sources = Get-ChildItem (Join-Path $project 'Assets/02.Scripts/Presentation') -Recurse -Filter '*.cs'
$sources | Select-String 'OwnerWorkspaceUiFactory.CreatePanel|OwnerRuntimeUiFactory.CreatePanel|UIOwnerFrontOfficePanel.Apply|OwnerUiButtonSkin.Apply' |
    ForEach-Object { $_.Path.Substring($project.Length + 1) + ':' + $_.LineNumber + ': ' + $_.Line.Trim() } |
    Set-Content (Join-Path $destination 'runtime-call-sites.txt') -Encoding UTF8
Write-Output ('PNG 크기·RGBA·Border·임포트 설정: ' + $results.Count + '/29 통과. Unity 테스트·렌더링은 실행하지 않음.')
