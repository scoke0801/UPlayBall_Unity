param([string]$OutputRoot = 'output/imagegen/player-portraits/uniforms-v3')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'UniformConverter.cs')
$root = [IO.Path]::GetFullPath($OutputRoot)
$definitions = Get-Content (Join-Path $PSScriptRoot 'uniforms.json') -Raw
$gallery = (Get-Content (Join-Path $PSScriptRoot 'gallery.html') -Raw).Replace('__UNIFORMS_JSON__',$definitions)
$gallery | Set-Content (Join-Path $root 'index.html') -Encoding UTF8
Copy-Item (Join-Path $PSScriptRoot 'uniforms.json') (Join-Path $root 'uniforms.json')
$font = New-Object Drawing.Font('Arial',9)
try {
    # 모든 얼굴을 한 번씩 서로 다른 유니폼과 밝고 어두운 배경에서 확인한다.
    foreach ($page in 0..5) {
        $canvas = New-Object Drawing.Bitmap(1536,1248)
        $g = [Drawing.Graphics]::FromImage($canvas)
        try {
            foreach ($cell in 0..95) {
                $number = $page*96+$cell+1
                $face = 'face-{0:D4}' -f $number
                $uniform = 'uniform-{0:D2}' -f (($number-1)%10+1)
                $x = ($cell%12)*128; $y = [int][Math]::Floor($cell/12)*156
                $light = ($cell+[int][Math]::Floor($cell/12))%2 -eq 0
                $brush = New-Object Drawing.SolidBrush($(if($light){[Drawing.Color]::FromArgb(245,242,235)}else{[Drawing.Color]::FromArgb(24,35,55)}))
                try { $g.FillRectangle($brush,$x,$y,128,156) } finally { $brush.Dispose() }
                $labelBrush = if($light){[Drawing.Brushes]::Black}else{[Drawing.Brushes]::White}
                $g.DrawString(('F{0:D4} / U{1:D2}' -f $number,(($number-1)%10+1)),$font,$labelBrush,$x,($y+2))
                $image = [Drawing.Bitmap]::FromFile((Join-Path $root "portraits/$uniform/${face}__$uniform.png"))
                try { $g.DrawImage($image,$x,($y+23),128,128) } finally { $image.Dispose() }
            }
            $canvas.Save((Join-Path $root ('previews/review-{0:D2}.png' -f ($page+1))),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $g.Dispose(); $canvas.Dispose() }
    }
    $canvas = New-Object Drawing.Bitmap(1600,540)
    $g = [Drawing.Graphics]::FromImage($canvas)
    try {
        $g.Clear([Drawing.Color]::FromArgb(24,35,55))
        foreach($u in 1..10) {
            $uniform='uniform-{0:D2}' -f $u
            $g.DrawString($uniform,$font,[Drawing.Brushes]::White,($u-1)*160,0)
            foreach($r in 0..2) {
                $face='face-{0:D4}' -f (@(1,288,576)[$r])
                $image=[Drawing.Bitmap]::FromFile((Join-Path $root "portraits/$uniform/${face}__$uniform.png"))
                try { $g.DrawImage($image,($u-1)*160,(30+$r*170),160,160) } finally { $image.Dispose() }
            }
        }
        $canvas.Save((Join-Path $root 'previews/uniforms-10.png'),[Drawing.Imaging.ImageFormat]::Png)
    } finally { $g.Dispose(); $canvas.Dispose() }
} finally { $font.Dispose() }

$all = @()
foreach($n in 1..36) {
    $manifest = Get-Content (Join-Path $root ('manifests/sheet-{0:D3}.json' -f $n)) -Raw | ConvertFrom-Json
    $all += $manifest.Portraits
}
if($all.Count -ne 5760) { throw '조합 수 불일치' }
$sourceAudit = @()
foreach($number in 1..576) {
    $face = 'face-{0:D4}' -f $number
    $stats = [Baseball.Tools.PlayerUniforms.UniformConverter]::Inspect((Join-Path $root "sources/transparent/$face.png"))
    if($stats[0] -eq 0 -or $stats[1] -eq 0 -or $stats[2] -gt 0 -or $stats[3] -ne 1) { throw "투명 원본 검사 실패: $face $stats" }
    $sourceAudit += [ordered]@{AppearanceId=$face;SourceSha256=(Get-FileHash (Join-Path $root "sources/transparent/$face.png")).Hash;MaskSha256=(Get-FileHash (Join-Path $root "masks/$face.png")).Hash;TransparentPixels=$stats[0];OpaquePixels=$stats[1];GreenResiduePixels=$stats[2];TransparentCorners=$true}
}
$sourceAudit | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root 'manifests/source-alpha-audit.json') -Encoding UTF8
$keys = @{}
foreach($portrait in $all) {
    $key = "$($portrait.AppearanceId)__$($portrait.UniformId)"
    if($keys.ContainsKey($key)) { throw "중복 조합: $key" }
    $keys[$key]=$true
    if((Get-FileHash (Join-Path $root $portrait.File)).Hash -ne $portrait.Sha256) { throw "해시 불일치: $key" }
}
foreach($number in 1..576) {
    foreach($uniform in 1..10) {
        $expected = 'face-{0:D4}__uniform-{1:D2}' -f $number,$uniform
        if(-not $keys.ContainsKey($expected)){throw "누락 조합: $expected"}
    }
}
[ordered]@{ArtVersion='figurine-v1';UniformVersion='uniforms-v3';AppearanceCount=576;UniformCount=10;PortraitCount=$all.Count;ProtectedPixelChanges=0;AlphaChanges=0;Status='asset-conversion-verified';GameIntegrationVerified=$false;Portraits=$all} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'catalog.json') -Encoding UTF8
Write-Output '5760 PNG 해시·키 유일성 검증 및 검수 미리보기 완료'
