$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'NormalizeAssets.cs')
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$root = Join-Path $project 'Assets/10.Datas/Resources/UI/BaseballFrontOffice/V2'
$manifest = Get-Content (Join-Path $PSScriptRoot 'AssetManifest.json') -Raw | ConvertFrom-Json
$alpha = Import-Csv (Join-Path $PSScriptRoot 'AlphaReport.csv')
$results = foreach ($entry in $manifest) {
    $path = Join-Path $root ($entry.Folder + '/' + $entry.File + '.png')
    $bitmap = [Drawing.Bitmap]::new($path)
    $stats = $alpha | Where-Object Asset -eq $entry.Name
    $matches = 0
    if ($entry.Normal) {
        $normal = $manifest | Where-Object Name -eq $entry.Normal
        $reference = Join-Path $root ($normal.Folder + '/' + $normal.File + '.png')
        $matches = [Baseball.Tools.FrontOfficeSkin.NormalizeAssets]::CompareAlpha($path,$reference)
    }
    $valid = $bitmap.Width -eq $entry.Width -and $bitmap.Height -eq $entry.Height -and [int]$stats.TransparentPixels -gt 0 -and [int]$stats.GreenSpillPixels -eq 0 -and $matches -eq 0
    [pscustomobject]@{ Asset=$entry.Name; Width=$bitmap.Width; Height=$bitmap.Height; AlphaDifferenceFromNormal=$matches; TransparentPixels=[int]$stats.TransparentPixels; PartialAlphaPixels=[int]$stats.PartialAlphaPixels; GreenSpillPixels=[int]$stats.GreenSpillPixels; Pass=$valid }
    $bitmap.Dispose()
}
$results | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'Verification.json') -Encoding UTF8
$results | Format-Table Asset,Width,Height,AlphaDifferenceFromNormal,GreenSpillPixels,Pass
if (@($results | Where-Object { !$_.Pass }).Count -gt 0) { throw '이미지 검수 실패. Verification.json을 확인하세요.' }
if ($results.Count -ne 29) { throw '에셋 개수가 29개가 아닙니다.' }
