# 참조 엑셀을 수정하지 않고 OOXML 셀 값을 연구용 JSON으로 추출한다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$bookPath = Join-Path $projectRoot 'docs/ref/프로야구매니저_연도_Cost_스탯_웹복원_자료.xlsx'
$zip = [IO.Compression.ZipFile]::OpenRead($bookPath)
function Read-Part([string]$path) {
    $entry = $zip.GetEntry($path)
    if ($null -eq $entry) { throw "엑셀 구성 파일 누락: $path" }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { return [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
}
try {
    $workbook = Read-Part 'xl/workbook.xml'
    $relations = Read-Part 'xl/_rels/workbook.xml.rels'
    $strings = Read-Part 'xl/sharedStrings.xml'
    $shared = @($strings.SelectNodes('//*[local-name()="si"]') | ForEach-Object { $_.InnerText })
    $sheets = @(foreach ($sheet in $workbook.SelectNodes('//*[local-name()="sheet"]')) {
        $relationId = $sheet.GetAttribute('id','http://schemas.openxmlformats.org/officeDocument/2006/relationships')
        $target = ($relations.Relationships.Relationship | Where-Object Id -eq $relationId).Target
        $partPath = if ($target.StartsWith('/')) { $target.TrimStart('/') } else { 'xl/' + $target }
        $part = Read-Part $partPath
        $rows = @(foreach ($row in $part.SelectNodes('//*[local-name()="sheetData"]/*[local-name()="row"]')) {
            $cells = [ordered]@{}
            $formulas = [ordered]@{}
            foreach ($cell in $row.SelectNodes('./*[local-name()="c"]')) {
                $valueNode = $cell.SelectSingleNode('./*[local-name()="v"]')
                $value = if ($valueNode) { $valueNode.InnerText } else { $null }
                if ($cell.t -eq 's') { $value = $shared[[int]$value] }
                elseif ($cell.t -eq 'inlineStr') { $value = $cell.SelectSingleNode('./*[local-name()="is"]').InnerText }
                $formula = $cell.SelectSingleNode('./*[local-name()="f"]')
                if ($formula) { $formulas[$cell.r] = $formula.InnerText }
                if ($null -ne $value -and $value -ne '') { $cells[$cell.r -replace '\d',''] = $value }
            }
            if ($cells.Count -gt 0 -or $formulas.Count -gt 0) { [pscustomobject]@{ Row = [int]$row.r; Cells = $cells; Formulas = $formulas } }
        })
        [pscustomobject]@{ Name = $sheet.name; Rows = $rows }
    })
    @{ Workbook = 'docs/ref/' + [IO.Path]::GetFileName($bookPath); SHA256 = (Get-FileHash $bookPath -Algorithm SHA256).Hash; Sheets = $sheets } |
        ConvertTo-Json -Depth 12 | Set-Content (Join-Path $PSScriptRoot 'workbook_extracted.json') -Encoding UTF8
    foreach ($sheet in $sheets) {
        Write-Output "$($sheet.Name): $($sheet.Rows.Count) nonempty rows"
        $sheet.Rows | Select-Object -First 3 | ConvertTo-Json -Depth 6 -Compress
    }
} finally { $zip.Dispose() }
