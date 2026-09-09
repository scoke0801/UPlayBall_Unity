param([int]$FirstSheet = 1, [int]$LastSheet = 36, [string]$Revision = 'issued-v1', [int]$KeyTolerance = 48, [int]$KeyOpaqueDistance = 128)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$productionRoot = (Resolve-Path "output/imagegen/player-portraits/production-576-v1").Path
$removalTool = (Resolve-Path (Join-Path $productionRoot 'manifests/tool-snapshot-r06/Remove-ImageBackground.ps1')).Path
foreach ($folder in @('faces/raw',"faces/transparent/$Revision",'sheets/transparent','previews')) {
    [IO.Directory]::CreateDirectory((Join-Path $productionRoot $folder)) | Out-Null
}
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
namespace Baseball.Tools.PortraitBatch {
    public sealed class PixelReport {
        public int Transparent, Partial, Opaque, GreenResidue, InteriorChanged, InteriorChecked;
        public bool TransparentCorners;
    }
    public static class Verify {
        /// <summary>배경 키에서 충분히 떨어진 전경의 보존과 알파를 측정한다.</summary>
        public static PixelReport Check(Bitmap source, Bitmap result) {
            var report = new PixelReport();
            int w=source.Width,h=source.Height;
            var backgroundSum=new int[(w+1)*(h+1)];
            for(int sy=0;sy<h;sy++) for(int sx=0;sx<w;sx++) {
                Color s=source.GetPixel(sx,sy);
                int key=s.G>Math.Max(s.R,s.B)+12 ? 1 : 0;
                int p=(sy+1)*(w+1)+sx+1;
                backgroundSum[p]=key+backgroundSum[p-1]+backgroundSum[p-w-1]-backgroundSum[p-w-2];
            }
            for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
                Color c=result.GetPixel(x,y);
                if(c.A==0) report.Transparent++; else if(c.A==255) report.Opaque++; else report.Partial++;
                if(c.A>16 && c.G>c.R+40 && c.G>c.B+40) report.GreenResidue++;
                if(x<7 || y<7 || x>=w-7 || y>=h-7) continue;
                int left=x-6,top=y-6,right=x+7,bottom=y+7;
                bool interior=backgroundSum[bottom*(w+1)+right]-backgroundSum[top*(w+1)+right]-backgroundSum[bottom*(w+1)+left]+backgroundSum[top*(w+1)+left]==0;
                if(interior) {
                    report.InteriorChecked++;
                    Color s=source.GetPixel(x,y);
                    if(c.A!=255 || c.R!=s.R || c.G!=s.G || c.B!=s.B) report.InteriorChanged++;
                }
            }
            report.TransparentCorners=result.GetPixel(0,0).A==0 && result.GetPixel(w-1,0).A==0 && result.GetPixel(0,h-1).A==0 && result.GetPixel(w-1,h-1).A==0;
            return report;
        }
    }
}
'@
for ($sheetNumber=$FirstSheet; $sheetNumber -le $LastSheet; $sheetNumber++) {
    if ($sheetNumber -ge 2 -and $sheetNumber -le 11) { continue }
    $sheetId = 'sheet-{0:D3}' -f $sheetNumber
    $sourceRevision = if ($sheetNumber -eq 1) { 'r02' } else { 'r01' }
    $sheet = [Drawing.Bitmap]::FromFile((Join-Path $productionRoot "sheets/raw/${sheetId}__$sourceRevision.png"))
    $assembled = New-Object Drawing.Bitmap($sheet.Width,$sheet.Height)
    $assemblyGraphics = [Drawing.Graphics]::FromImage($assembled)
    $assemblyGraphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $preview = New-Object Drawing.Bitmap(1024,576)
    $previewGraphics = [Drawing.Graphics]::FromImage($preview)
    $font = New-Object Drawing.Font('Arial',9)
    $reports = @()
    try {
        for ($i=0; $i -lt 16; $i++) {
            $row=[int][Math]::Floor($i/4); $column=$i%4
            $x=[int][Math]::Floor($column*$sheet.Width/4); $y=[int][Math]::Floor($row*$sheet.Height/4)
            $right=[int][Math]::Floor(($column+1)*$sheet.Width/4); $bottom=[int][Math]::Floor(($row+1)*$sheet.Height/4)
            if ($row -gt 0) { $y += 3 }
            $rect=New-Object Drawing.Rectangle($x,$y,($right-$x),($bottom-$y))
            $faceId='face-{0:D4}' -f (($sheetNumber-1)*16+$i+1)
            $rawPath=Join-Path $productionRoot "faces/raw/${faceId}__$Revision.png"
            $resultPath=Join-Path $productionRoot "faces/transparent/$Revision/${faceId}__$Revision.png"
            if (Test-Path -LiteralPath $resultPath) { throw "기존 파일: $faceId" }
            $crop=$sheet.Clone($rect,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try { if (-not (Test-Path -LiteralPath $rawPath)) { $crop.Save($rawPath,[Drawing.Imaging.ImageFormat]::Png) } } finally { $crop.Dispose() }
            $toolOutput = & powershell -NoProfile -File $removalTool -InputPath $rawPath -OutputPath $resultPath -Mode ChromaKey -KeyColor '#00FF00' -KeyTolerance $KeyTolerance -KeyOpaqueDistance $KeyOpaqueDistance
            if ($LASTEXITCODE -ne 0) { throw "배경 제거 실패: $faceId $toolOutput" }
            $source=[Drawing.Bitmap]::FromFile($rawPath)
            $result=[Drawing.Bitmap]::FromFile($resultPath)
            try {
                $stats=[Baseball.Tools.PortraitBatch.Verify]::Check($source,$result)
                if ($stats.Transparent -eq 0 -or $stats.Opaque -eq 0 -or -not $stats.TransparentCorners -or $stats.InteriorChanged -ne 0 -or $stats.GreenResidue -ne 0) { throw "알파/전경 검증 실패: $faceId $($stats | ConvertTo-Json -Compress)" }
                $assemblyGraphics.DrawImageUnscaled($result,$x,$y)
                for ($background=0; $background -lt 2; $background++) {
                    $px=$column*256+$background*128; $py=$row*144
                    $color=if ($background -eq 0) {[Drawing.Color]::FromArgb(245,242,235)} else {[Drawing.Color]::FromArgb(24,35,55)}
                    $brush=New-Object Drawing.SolidBrush($color)
                    try { $previewGraphics.FillRectangle($brush,$px,$py,128,144) } finally { $brush.Dispose() }
                    $previewGraphics.DrawImage($result,$px,($py+16),128,128)
                    $labelBrush=if ($background -eq 0) {[Drawing.Brushes]::Black} else {[Drawing.Brushes]::White}
                    $previewGraphics.DrawString($faceId,$font,$labelBrush,$px,$py)
                }
                $reports += [ordered]@{ AppearanceId=$faceId; SheetId=$sheetId; CropRect=@($x,$y,($right-$x),($bottom-$y)); RawFile="faces/raw/${faceId}__$Revision.png"; FinalFile="faces/transparent/$Revision/${faceId}__$Revision.png"; FinalSha256=(Get-FileHash -LiteralPath $resultPath -Algorithm SHA256).Hash; Alpha=$stats; Status='draft'; BackgroundRemovalCommand="$removalTool -InputPath $rawPath -OutputPath $resultPath -Mode ChromaKey -KeyColor '#00FF00' -KeyTolerance $KeyTolerance -KeyOpaqueDistance $KeyOpaqueDistance" }
            } finally { $source.Dispose(); $result.Dispose() }
        }
        $assembledPath=Join-Path $productionRoot "sheets/transparent/${sheetId}__$Revision.png"
        $previewPath=Join-Path $productionRoot "previews/${sheetId}__alpha-review-$Revision.png"
        if ((Test-Path -LiteralPath $assembledPath) -or (Test-Path -LiteralPath $previewPath)) { throw '검수 출력이 이미 있습니다.' }
        $assembled.Save($assembledPath,[Drawing.Imaging.ImageFormat]::Png)
        $preview.Save($previewPath,[Drawing.Imaging.ImageFormat]::Png)
        $reportPath=Join-Path $productionRoot "manifests/${sheetId}__background-$Revision.json"
        [IO.File]::WriteAllText($reportPath,($reports | ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
        Write-Output "$sheetId : 16 RGBA faces saved and pixel checks passed."
    } finally {
        $sheet.Dispose(); $assemblyGraphics.Dispose(); $assembled.Dispose(); $previewGraphics.Dispose(); $preview.Dispose(); $font.Dispose()
    }
}
