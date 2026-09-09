param([string]$OutputDirectory = 'output/imagegen/front-manager-03/rebuilt')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'chroma-manifest.json') -Raw | ConvertFrom-Json
foreach ($item in $manifest.images) {
    $source = Join-Path $root $item.source
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $item.sourceSha256) { throw "원본 해시 불일치: $source" }
    $output = Join-Path $OutputDirectory ($item.name + '.png')
    & (Join-Path $root 'Tools/ImageBackground/Remove-ImageBackground.ps1') -InputPath $source -OutputPath $output -Mode ChromaKey -KeyColor $manifest.keyColor -KeyTolerance $manifest.keyTolerance -KeyOpaqueDistance $manifest.keyOpaqueDistance -KeyEdgeRadius $manifest.keyEdgeRadius
    if ((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash -ne $item.outputSha256) { throw "결과 해시 불일치: $output" }
}
$paths = $manifest.images | ForEach-Object { Join-Path $OutputDirectory ($_.name + '.png') }
& (Join-Path $root 'Tools/ImageBackground/Review-ImageBackground.ps1') -InputPaths $paths -OutputPath (Join-Path $OutputDirectory 'review.png')
