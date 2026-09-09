# 외곽·내부 배경, 복수 보호 영역, 기존 알파, 덮어쓰기 방지를 검증한다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('image-background-test-' + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testDirectory) | Out-Null
$sourcePath = Join-Path $testDirectory 'source.png'
$source = [System.Drawing.Bitmap]::new(64,64,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($source)
$graphics.Clear([System.Drawing.Color]::LightGray)
$graphics.FillRectangle([System.Drawing.Brushes]::SaddleBrown,8,8,48,48)
$graphics.FillRectangle([System.Drawing.Brushes]::LightGray,38,14,10,10)
$graphics.FillRectangle([System.Drawing.Brushes]::White,12,12,8,8)
$graphics.FillRectangle([System.Drawing.Brushes]::White,12,28,18,20)
$graphics.Dispose()
$source.SetPixel(31,20,[System.Drawing.Color]::FromArgb(128,139,69,19))
$source.Save($sourcePath,[System.Drawing.Imaging.ImageFormat]::Png)
$source.Dispose()
$protectionPath = Join-Path $testDirectory 'protect.json'
Set-Content -LiteralPath $protectionPath -Value '[[[11,11],[21,11],[21,21],[11,21]],[[11,27],[31,27],[31,49],[11,49]]]' -Encoding ascii
$singlePath = Join-Path $testDirectory 'single.json'
Set-Content -LiteralPath $singlePath -Value '[[11,11],[21,11],[21,21],[11,21]]' -Encoding ascii
$toolPath = Join-Path $PSScriptRoot 'Remove-ImageBackground.ps1'
$defaultPath = Join-Path $testDirectory 'default.png'
$enclosedPath = Join-Path $testDirectory 'enclosed.png'
& powershell -NoProfile -File $toolPath -InputPath $sourcePath -OutputPath $defaultPath -ProtectPolygonPath $singlePath
if ($LASTEXITCODE -ne 0) { throw '기존 단일 보호 영역 변환 실패' }
& powershell -NoProfile -File $toolPath -InputPath $sourcePath -OutputPath $enclosedPath -ProtectPolygonPath $protectionPath -RemoveEnclosedBackground
if ($LASTEXITCODE -ne 0) { throw '복수 보호 영역 변환 실패' }
$default = [System.Drawing.Bitmap]::new($defaultPath)
$enclosed = [System.Drawing.Bitmap]::new($enclosedPath)
try {
    if ($enclosed.PixelFormat -ne [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) { throw 'RGBA 형식 아님' }
    if ($enclosed.GetPixel(0,0).A -ne 0) { throw '외곽 배경이 남음' }
    if ($default.GetPixel(42,18).A -ne 255) { throw '기본 모드가 내부 영역을 제거함' }
    if ($enclosed.GetPixel(42,18).A -ne 0) { throw '내부 배경이 남음' }
    if ($enclosed.GetPixel(15,15).A -ne 255 -or $enclosed.GetPixel(20,35).A -ne 255) { throw '보호 영역 손실' }
    if ($enclosed.GetPixel(31,20).A -ne 128) { throw '기존 알파가 변경됨' }
} finally {
    $default.Dispose()
    $enclosed.Dispose()
}
$originalHash = (Get-FileHash -LiteralPath $enclosedPath).Hash
$failureLog = Join-Path $testDirectory 'expected-failures.log'
$ErrorActionPreference = 'Continue'
& powershell -NoProfile -File $toolPath -InputPath $sourcePath -OutputPath $enclosedPath *> $failureLog
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -eq 0 -or (Get-FileHash -LiteralPath $enclosedPath).Hash -ne $originalHash) { throw '기존 파일 덮어쓰기 방지 실패' }
$unsafePath = Join-Path $testDirectory 'unprotected.png'
$ErrorActionPreference = 'Continue'
& powershell -NoProfile -File $toolPath -InputPath $sourcePath -OutputPath $unsafePath -RemoveEnclosedBackground *> $failureLog
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath $unsafePath)) { throw '보호 없는 내부 제거가 허용됨' }
Write-Output 'PASS: RGBA, 외곽/내부 배경, 단일/복수 보호 영역, 기존 알파, 덮어쓰기 방지, 보호 필수 검사'

# 밝은 의상 확장, 무채색 경계 역합성, 분리된 보호 소품을 작은 독립 입력으로 검증한다.
$edgeSource=Join-Path $testDirectory 'edge-source.png'
$edgeOutput=Join-Path $testDirectory 'edge.png'
$edgeProtection=Join-Path $testDirectory 'edge-protect.json'
$bitmap=[Drawing.Bitmap]::new(32,32,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[Drawing.Graphics]::FromImage($bitmap)
$g.Clear([Drawing.Color]::FromArgb(180,180,180))
$g.FillRectangle([Drawing.Brushes]::SaddleBrown,8,8,16,16)
$g.FillRectangle([Drawing.Brushes]::White,12,12,4,4)
$g.FillRectangle([Drawing.Brushes]::Red,2,12,3,4)
$g.Dispose()
$bitmap.SetPixel(7,15,[Drawing.Color]::FromArgb(165,150,140))
$bitmap.Save($edgeSource,[Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Set-Content $edgeProtection '[[[12,12],[14,12],[14,14],[12,14]],[[2,12],[3,12],[3,13],[2,13]]]' -Encoding ascii
& powershell -NoProfile -File $toolPath -InputPath $edgeSource -OutputPath $edgeOutput -ProtectPolygonPath $edgeProtection -RemoveEnclosedBackground -ProtectBrightThreshold 215 -MaxBrightness 220 -EdgeMatteRadius 2
if($LASTEXITCODE -ne 0) {throw '경계 보정 실행 실패'}
$bitmap=[Drawing.Bitmap]::new($edgeOutput)
try {
    if($bitmap.GetPixel(15,15).A -ne 255) {throw '보호 다각형 밖 흰 의상 확장 실패'}
    if($bitmap.GetPixel(4,15).A -ne 255) {throw '분리된 보호 소품의 연결 성분 손실'}
    $edge=$bitmap.GetPixel(7,15)
    if($edge.A -le 0 -or $edge.A -ge 255) {throw '혼합색 경계 알파 복원 실패'}
    if($edge.R -le $edge.B) {throw '경계 전경색 복원 실패'}
    if($bitmap.GetPixel(10,10).ToArgb() -ne [Drawing.Color]::SaddleBrown.ToArgb()) {throw '불투명 전경색 변경'}
} finally {$bitmap.Dispose()}
$invalidPath=Join-Path $testDirectory 'invalid.png'
$ErrorActionPreference='Continue'
& powershell -NoProfile -File $toolPath -InputPath $edgeSource -OutputPath $invalidPath -MinBrightness 220 -MaxBrightness 100 *> $failureLog
$ErrorActionPreference='Stop'
if($LASTEXITCODE -eq 0 -or (Test-Path $invalidPath)) {throw '잘못된 밝기 범위 허용'}
$ErrorActionPreference='Continue'
& powershell -NoProfile -File $toolPath -InputPath $edgeSource -OutputPath $invalidPath -ProtectBrightThreshold 215 *> $failureLog
$ErrorActionPreference='Stop'
if($LASTEXITCODE -eq 0 -or (Test-Path $invalidPath)) {throw '보호 영역 없는 밝기 확장 허용'}
Write-Output 'PASS: 밝은 의상 확장, 경계 알파·색 복원, 분리된 보호 소품, 불투명 전경, 밝기 범위·보호 영역 검사'

foreach($keyName in @('Magenta','Lime')) {
    $key=[Drawing.Color]::FromName($keyName)
    $keyHex=if($keyName -eq 'Magenta') {'#FF00FF'} else {'#00FF00'}
    $chromaSource=Join-Path $testDirectory "$keyName-source.png"
    $chromaOutput=Join-Path $testDirectory "$keyName.png"
    $bitmap=[Drawing.Bitmap]::new(32,32,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($bitmap)
    $g.Clear($key)
    $g.FillRectangle([Drawing.Brushes]::White,10,4,12,20)
    $g.FillRectangle([Drawing.Brushes]::SaddleBrown,4,12,5,8)
    $g.FillRectangle([Drawing.Brushes]::Red,25,20,3,3)
    $keyBrush=[Drawing.SolidBrush]::new($key)
    $g.FillRectangle($keyBrush,16,12,3,3)
    $keyBrush.Dispose()
    $g.Dispose()
    $bitmap.SetPixel(9,8,[Drawing.Color]::FromArgb([int][Math]::Round((255+$key.R)/2.0),[int][Math]::Round((255+$key.G)/2.0),[int][Math]::Round((255+$key.B)/2.0)))
    $bitmap.SetPixel(3,15,[Drawing.Color]::FromArgb([int][Math]::Round((139+$key.R)/2.0),[int][Math]::Round((69+$key.G)/2.0),[int][Math]::Round((19+$key.B)/2.0)))
    $bitmap.SetPixel(14,8,[Drawing.Color]::FromArgb(80,220,180))
    $bitmap.SetPixel(15,6,[Drawing.Color]::FromArgb(128,139,69,19))
    $bitmap.Save($chromaSource,[Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    & powershell -NoProfile -File $toolPath -InputPath $chromaSource -OutputPath $chromaOutput -Mode ChromaKey -KeyColor $keyHex
    if($LASTEXITCODE -ne 0) {throw "$keyName 크로마키 실행 실패"}
    $bitmap=[Drawing.Bitmap]::new($chromaOutput)
    try {
        if($bitmap.GetPixel(0,0).A -ne 0 -or $bitmap.GetPixel(17,13).A -ne 0) {throw '크로마키 외곽·내부 틈 제거 실패'}
        if($bitmap.GetPixel(26,21).A -ne 255) {throw '크로마키 분리된 소품 손실'}
        if($bitmap.GetPixel(15,6).A -ne 128) {throw '크로마키 기존 알파 손실'}
        if($bitmap.GetPixel(14,8).ToArgb() -ne [Drawing.Color]::FromArgb(80,220,180).ToArgb()) {throw '민트색 의상 손실'}
        $white=$bitmap.GetPixel(9,8)
        if([Math]::Abs($white.A-128) -gt 2 -or $white.R -lt 252 -or $white.G -lt 252 -or $white.B -lt 252) {throw '흰 의상 혼합 경계 복원 실패'}
        $hair=$bitmap.GetPixel(3,15)
        if([Math]::Abs($hair.A-128) -gt 3 -or [Math]::Abs($hair.R-139) -gt 4 -or [Math]::Abs($hair.G-69) -gt 4 -or [Math]::Abs($hair.B-19) -gt 4) {throw '머리카락 혼합 경계 복원 실패'}
    } finally {$bitmap.Dispose()}
}
$ErrorActionPreference='Continue'
& powershell -NoProfile -File $toolPath -InputPath $chromaSource -OutputPath $invalidPath -Mode ChromaKey -KeyColor '#FF00FF' *> $failureLog
$ErrorActionPreference='Stop'
if($LASTEXITCODE -eq 0 -or (Test-Path $invalidPath)) {throw '입력과 다른 크로마키 색 허용'}
Write-Output 'PASS: 마젠타·초록 키, 내부 틈, 분리된 소품, 기존 알파, 민트색·흰 의상, 머리카락의 알파·색 복원, 잘못된 키 거부'

$repeatOutput = Join-Path $testDirectory 'chroma-repeat.png'
$sourceHash = (Get-FileHash -LiteralPath $chromaSource).Hash
$resultHash = (Get-FileHash -LiteralPath $chromaOutput).Hash
& powershell -NoProfile -File $toolPath -InputPath $chromaSource -OutputPath $repeatOutput -Mode ChromaKey -KeyColor '#00FF00'
if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $repeatOutput).Hash -ne $resultHash) { throw '크로마키 재실행 결정론 실패' }
$ErrorActionPreference = 'Continue'
& powershell -NoProfile -File $toolPath -InputPath $chromaSource -OutputPath $chromaOutput -Mode ChromaKey -KeyColor '#00FF00' *> $failureLog
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -eq 0 -or (Get-FileHash -LiteralPath $chromaOutput).Hash -ne $resultHash -or (Get-FileHash -LiteralPath $chromaSource).Hash -ne $sourceHash) { throw '크로마키 원본·기존 출력 보존 실패' }
Write-Output 'PASS: 크로마키 출력 결정론, 원본·기존 출력 보존'
