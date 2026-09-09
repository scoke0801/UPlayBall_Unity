param(
    [int]$FirstSheet = 1,
    [int]$LastSheet = 36,
    [string]$OutputRoot = 'output/imagegen/player-portraits/uniforms-v3'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'UniformConverter.cs')
$converterHash = (Get-FileHash (Join-Path $PSScriptRoot 'UniformConverter.cs')).Hash
$definitionsHash = (Get-FileHash (Join-Path $PSScriptRoot 'uniforms.json')).Hash
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$productionRoot = Join-Path $projectRoot 'output/imagegen/player-portraits/production-576-v1'
$output = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputRoot))
$definitions = Get-Content (Join-Path $PSScriptRoot 'uniforms.json') -Raw | ConvertFrom-Json
foreach ($folder in @('sources/raw','sources/transparent','masks','portraits','manifests','previews')) {
    [IO.Directory]::CreateDirectory((Join-Path $output $folder)) | Out-Null
}
$selection = (Get-Content (Join-Path $productionRoot 'manifests/generation-576-resume-audit.json') -Raw | ConvertFrom-Json).Sheets
foreach ($number in $FirstSheet..$LastSheet) {
    $sheetId = 'sheet-{0:D3}' -f $number
    $selected = $selection | Where-Object SheetId -eq $sheetId
    $sourcePath = Join-Path $productionRoot $selected.SourceFile
    if ((Get-FileHash $sourcePath).Hash -ne $selected.SourceSha256) { throw "원본 해시 불일치: $sheetId" }
    $reportPath = Join-Path $output "manifests/$sheetId.json"
    if (Test-Path $reportPath) { throw "완료된 시트입니다. 새 출력 폴더를 지정하세요: $sheetId" }
    $sheet = [Drawing.Bitmap]::FromFile($sourcePath)
    $report = @()
    try {
        foreach ($cell in 0..15) {
            $faceId = 'face-{0:D4}' -f (($number-1)*16+$cell+1)
            $row = [int][Math]::Floor($cell/4); $column = $cell%4
            $x = [int][Math]::Floor($column*$sheet.Width/4)
            $y = [int][Math]::Floor($row*$sheet.Height/4)
            $right = [int][Math]::Floor(($column+1)*$sheet.Width/4)
            $bottom = [int][Math]::Floor(($row+1)*$sheet.Height/4)
            # 이전 줄의 상의 끝이 다음 셀에 섞이는 생성 시트의 경계 오차를 제거한다.
            if ($row -gt 0) { $y += 3 }
            $raw = Join-Path $output "sources/raw/$faceId.png"
            $transparent = Join-Path $output "sources/transparent/$faceId.png"
            $mask = Join-Path $output "masks/$faceId.png"
            if (Test-Path $raw) { throw "기존 작업 파일을 덮어쓰지 않습니다: $faceId" }
            $crop = $sheet.Clone((New-Object Drawing.Rectangle($x,$y,($right-$x),($bottom-$y))),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try { $crop.Save($raw,[Drawing.Imaging.ImageFormat]::Png) } finally { $crop.Dispose() }
            & (Join-Path $projectRoot 'Tools/ImageBackground/Remove-ImageBackground.ps1') -InputPath $raw -OutputPath $transparent -Mode ChromaKey -KeyColor '#00FF00' -KeyTolerance 48 -KeyOpaqueDistance 255 -KeyEdgeRadius 12
            [Baseball.Tools.PlayerUniforms.UniformConverter]::CreateMask($transparent,$mask)
            foreach ($uniform in $definitions) {
                $relative = "portraits/$($uniform.Id)/${faceId}__$($uniform.Id).png"
                $target = Join-Path $output $relative
                [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
                $preserved = [Baseball.Tools.PlayerUniforms.UniformConverter]::Convert($transparent,$mask,$target,$uniform.Cap,$uniform.Jersey,$uniform.Trim,$uniform.Pinstripes)
                $report += [ordered]@{AppearanceId=$faceId;UniformId=$uniform.Id;File=$relative;Sha256=(Get-FileHash $target).Hash;PreservedPixels=$preserved;ProtectedPixelChanges=0;AlphaChanges=0;CropRect=@($x,$y,($right-$x),($bottom-$y));Status='draft'}
            }
        }
        [ordered]@{SheetId=$sheetId;SourceFile=$selected.SourceFile;SourceSha256=$selected.SourceSha256;DefinitionsSha256=$definitionsHash;ConverterSha256=$converterHash;Portraits=$report} | ConvertTo-Json -Depth 8 | Set-Content $reportPath -Encoding UTF8
        Write-Output "$sheetId : 160 PNG, 보호 영역/알파 검증 통과"
    } finally { $sheet.Dispose() }
}
