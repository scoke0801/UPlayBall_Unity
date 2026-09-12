param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'NormalizeAssets.cs')
$work = Join-Path $ProjectRoot 'output/imagegen/baseball-front-office-v2'
$destination = Join-Path $ProjectRoot 'Assets/10.Datas/Resources/UI/BaseballFrontOffice/V2'
$entries = @()
function Register($name, $folder, $file, $width, $height, $border, $normal = '', $ratio = 0, $opacity = 1, $panel = $false) {
    $script:entries += [pscustomobject]@{Name=$name; Folder=$folder; File=$file; Width=$width; Height=$height; Border=$border; Normal=$normal; Ratio=$ratio; Opacity=$opacity; Panel=$panel}
}
Register MainDashboard Frames UI_Frame_MainDashboard 1536 768 48 '' 0 .90 $true
Register ManagerCard Frames UI_Frame_ManagerCard 1536 768 48 '' 0 .92 $true
Register ManagerReport Frames UI_Frame_ManagerReport 1536 1280 48 '' 0 .94 $true
Register CompactStrip Frames UI_Frame_CompactStrip 1536 384 32 '' 0 .88 $true
foreach ($family in @('Primary','Secondary','Utility','ListItem','Tab')) {
    $states = @('Normal','Hover','Pressed','Disabled')
    $folder = 'Buttons'; $prefix = "UI_Button_$family"; $w = 512; $h = 144; $b = 24
    if ($family -eq 'Primary') { $w = 768 }
    if ($family -eq 'Secondary') { $states += 'Selected'; $w = 768 }
    if ($family -eq 'Utility') { $w = 128; $h = 128; $b = 16 }
    if ($family -eq 'ListItem') { $states = @('Normal','Hover','Selected','Unread','Disabled'); $folder='ListItems'; $prefix='UI_ListItem'; $w=1536; $h=160; $b=16 }
    if ($family -eq 'Tab') { $states = @('Normal','Hover','Selected','Disabled'); $folder='Tabs'; $prefix='UI_Tab'; $w=512; $h=8; $b=8 }
    foreach ($state in $states) {
        $normal = if ($state -eq 'Normal') { '' } else { "${family}_Normal" }
        $ratio = 0
        if ($state -eq 'Hover' -and $family -ne 'Tab') { $ratio = 1.10 }
        if ($state -eq 'Pressed') { $ratio = .88 }
        if ($state -eq 'Unread') { $ratio = 1.04 }
        Register "${family}_$state" $folder "${prefix}_$state" $w $h $b $normal $ratio
    }
}
Register Badge_Count Badges UI_Badge_Count 96 96 0
Register Badge_Unread Badges UI_Badge_Unread 32 32 0
Register Badge_Important Badges UI_Badge_Important 96 96 0
$template = Get-Content (Join-Path $ProjectRoot 'Assets/10.Datas/Resources/UI/OwnerSkin/owner_button_primary_v1.png.meta') -Raw
$template = $template -replace 'spriteMeshType: 1', 'spriteMeshType: 0'
$template = $template -replace 'spriteMeshType: 1', 'spriteMeshType: 0'
$report = @('Asset,Width,Height,TransparentPixels,PartialAlphaPixels,GreenSpillPixels,CenterLuminance')
foreach ($entry in $entries) {
    $source = Join-Path $work ($entry.Name + '-source.png')
    $keyed = Join-Path $work ('Keyed/' + $entry.Name + '.png')
    if (!(Test-Path $keyed)) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($keyed)) | Out-Null
        $bitmap = [Drawing.Bitmap]::new($source)
        $hasAlpha = $bitmap.GetPixel(0,0).A -eq 0
        $bitmap.Dispose()
        if ($hasAlpha) { Copy-Item -LiteralPath $source -Destination $keyed }
        else { & (Join-Path $ProjectRoot 'Tools/ImageBackground/Remove-ImageBackground.ps1') -InputPath $source -OutputPath $keyed -Mode ChromaKey -KeyColor '#00FF00' -KeyTolerance 96 -KeyOpaqueDistance 220 -KeyEdgeRadius 6 | Out-Null }
    }
    $normal = if ($entry.Normal) { Join-Path $work ('Canonical/' + $entry.Normal + '.png') } else { '' }
    $canonical = Join-Path $work ('Canonical/' + $entry.Name + '.png')
    [Baseball.Tools.FrontOfficeSkin.NormalizeAssets]::Process($keyed,$canonical,$entry.Width,$entry.Height,$entry.Border,$normal,$entry.Ratio,1,$false) | Out-Null
    $output = Join-Path $destination ($entry.Folder + '/' + $entry.File + '.png')
    # 동일 좌표로 이미 정렬한 상태를 다시 자르지 않도록 canonical 알파를 기준으로 고정한다.
    $stats = [Baseball.Tools.FrontOfficeSkin.NormalizeAssets]::Process($canonical,$output,$entry.Width,$entry.Height,$entry.Border,$canonical,1,$entry.Opacity,$entry.Panel)
    $report += $entry.Name + ',' + $stats
    $meta = $template -replace 'guid: [a-f0-9]+', ('guid: ' + [guid]::NewGuid().ToString('N'))
    if (Test-Path ($output + '.meta')) { $old = Get-Content ($output + '.meta') -Raw; $meta = $meta -replace 'guid: [a-f0-9]+', ([regex]::Match($old,'guid: [a-f0-9]+').Value) }
    $vertical = if ($entry.Folder -eq 'Tabs') { 0 } else { $entry.Border }
    $top = if ($entry.Name -in @('MainDashboard','ManagerCard')) { 144 } else { $vertical }
    $entry | Add-Member -NotePropertyName Left -NotePropertyValue $entry.Border
    $entry | Add-Member -NotePropertyName Right -NotePropertyValue $entry.Border
    $entry | Add-Member -NotePropertyName Top -NotePropertyValue $top
    $entry | Add-Member -NotePropertyName Bottom -NotePropertyValue $vertical
    $meta = $meta -replace 'spriteBorder: \{[^}]+\}', "spriteBorder: {x: $($entry.Border), y: $vertical, z: $($entry.Border), w: $top}"
    [IO.File]::WriteAllText($output + '.meta', $meta)
    Write-Output ($entry.Name + ': ' + $stats)
}
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'AlphaReport.csv'), $report)
$entries | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $PSScriptRoot 'AssetManifest.json') -Encoding UTF8
