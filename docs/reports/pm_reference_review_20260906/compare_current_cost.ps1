# 기존 연구 표본을 현재 배포 Archive와 ID로 대조한다. 입력 파일은 수정하지 않는다.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$runtimeRoot = Join-Path $projectRoot 'Assets/10.Datas/HistoricalSimulation/1982-2025'
$referencePath = Join-Path $projectRoot 'Tools/PMReference/reports/COST_REFERENCE_VALIDATION.csv'
$costById = @{}
$inputHashes = @()
foreach ($year in 1982..2025) {
    $yearPath = Join-Path $runtimeRoot "Years/$year.json"
    $archive = Get-Content -LiteralPath $yearPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($player in $archive.playerSeasons) {
        if ($costById.ContainsKey($player.playerSeasonId)) { throw '중복 PlayerSeasonId' }
        $costById[$player.playerSeasonId] = [int]$player.cost
    }
    $inputHashes += @{ Path = "Years/$year.json"; SHA256 = (Get-FileHash -LiteralPath $yearPath -Algorithm SHA256).Hash }
}
$reference = @(Import-Csv -LiteralPath $referencePath -Encoding UTF8 | Where-Object {
    $_.DataVersion -eq 'OriginalObserved2011' -and $_.ProjectCost -match '^\d+$' -and $_.ReferenceCost -match '^\d+$'
})
$comparisons = @(foreach ($row in $reference) {
    if (-not $costById.ContainsKey($row.PlayerSeasonId)) { throw "현재 Archive 연결 실패: $($row.PlayerSeasonId)" }
    $current = $costById[$row.PlayerSeasonId]
    [pscustomobject]@{
        ReferenceCardId = $row.ReferenceCardId; Year = $row.Year; PlayerName = $row.PlayerName
        CardType = $row.CardType; DataVersion = $row.DataVersion; PlayerSeasonId = $row.PlayerSeasonId
        ReferenceCost = [int]$row.ReferenceCost; PreviousReportCost = [int]$row.ProjectCost
        CurrentCost = $current; Delta = $current - [int]$row.ReferenceCost
        YearSplit = $row.YearSplit; TeamSplit = $row.TeamSplit; SourceUrl = $row.SourceUrl
    }
})
$metrics = @(foreach ($field in @('All','YearSplit','TeamSplit')) {
    $splits = if ($field -eq 'All') { @('All') } else { @('Train','Validation','Holdout') }
    foreach ($split in $splits) {
        $subset = @($comparisons | Where-Object { $field -eq 'All' -or $_.$field -eq $split })
        if ($subset.Count -eq 0) { continue }
        $absolute = @($subset | ForEach-Object { [math]::Abs($_.Delta) })
        [pscustomobject]@{
            Scope = $field; Split = $split; Count = $subset.Count
            ExactMatchRate = @($subset | Where-Object Delta -eq 0).Count / $subset.Count
            Within1Rate = @($absolute | Where-Object { $_ -le 1 }).Count / $subset.Count
            MAE = ($absolute | Measure-Object -Average).Average
            MeanSignedDelta = ($subset | Measure-Object Delta -Average).Average
            Status = 'Exploratory_CardSubtypeAndFinalVersionUnknown_NotAcceptance'
        }
    }
})
$manifest = Get-Content (Join-Path $runtimeRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$comparisons | Export-Csv (Join-Path $PSScriptRoot 'current_cost_comparison.csv') -NoTypeInformation -Encoding UTF8
@{
    SourceManifest = $manifest.sourceManifest
    ReferenceSHA256 = (Get-FileHash $referencePath -Algorithm SHA256).Hash
    RuntimePlayerCount = $costById.Count
    InputHashes = $inputHashes
    Metrics = $metrics
} | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $PSScriptRoot 'current_cost_metrics.json') -Encoding UTF8
$metrics | Format-Table -AutoSize
