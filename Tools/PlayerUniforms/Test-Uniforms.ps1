$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'UniformConverter.cs')
$root = Join-Path ([IO.Path]::GetTempPath()) ('baseball-uniform-test-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
$source = Join-Path $root 'source.png'; $mask = Join-Path $root 'mask.png'; $result = Join-Path $root 'result.png'
$bitmap = New-Object Drawing.Bitmap(100,100)
$g = [Drawing.Graphics]::FromImage($bitmap)
try {
    $g.Clear([Drawing.Color]::Transparent)
    $g.FillRectangle([Drawing.Brushes]::Navy,20,5,60,30)
    $skin=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255,221,166))
    try { $g.FillRectangle($skin,20,40,60,40) } finally { $skin.Dispose() }
    $g.FillRectangle([Drawing.Brushes]::White,25,50,10,5)
    $g.FillRectangle([Drawing.Brushes]::White,20,80,60,20)
    $bitmap.Save($source,[Drawing.Imaging.ImageFormat]::Png)
} finally { $g.Dispose(); $bitmap.Dispose() }
[Baseball.Tools.PlayerUniforms.UniformConverter]::CreateMask($source,$mask)
[Baseball.Tools.PlayerUniforms.UniformConverter]::Convert($source,$mask,$result,'#74334C','#285DC0','#FFFFFF',$true) | Out-Null
$before=[Drawing.Bitmap]::FromFile($source); $after=[Drawing.Bitmap]::FromFile($result)
try {
    if($before.GetPixel(30,52).ToArgb() -ne $after.GetPixel(30,52).ToArgb()){throw '흰 눈 변형'}
    if($before.GetPixel(50,77).ToArgb() -ne $after.GetPixel(50,77).ToArgb()){throw '목 피부 변형'}
    if($before.GetPixel(50,15).ToArgb() -eq $after.GetPixel(50,15).ToArgb()){throw '모자 색 미변경'}
    if($after.GetPixel(50,85).B -le $after.GetPixel(50,85).R){throw '상의 색 미변경'}
    if($after.GetPixel(0,0).A -ne 0){throw '알파 미보존'}
} finally { $before.Dispose(); $after.Dispose() }
$bad=[Drawing.Bitmap]::FromFile($mask)
try { $bad.SetPixel(50,60,[Drawing.Color]::Red); $bad.Save((Join-Path $root 'bad-mask.png'),[Drawing.Imaging.ImageFormat]::Png) } finally { $bad.Dispose() }
$rejected=$false
try { [Baseball.Tools.PlayerUniforms.UniformConverter]::Convert($source,(Join-Path $root 'bad-mask.png'),(Join-Path $root 'bad.png'),'#000000','#000000','#000000',$false) | Out-Null }
catch { if($_.Exception.Message -notmatch '보존 검증 실패'){throw}; $rejected=$true }
if(-not $rejected){throw '얼굴 침범 마스크가 차단되지 않음'}
Write-Output '통과: 눈·목 피부·알파 보존, 모자·상의 색 변환, 얼굴 침범 마스크 차단'
