# 기존 관측과의 중복 후보, 현재 선수 데이터와의 차이를 연구용으로만 집계한다.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$document = Get-Content (Join-Path $PSScriptRoot 'workbook_extracted.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$existing = Get-Content (Join-Path $projectRoot 'Tools/PMReference/corpus/PMReferenceCorpus.json') -Raw -Encoding UTF8 | ConvertFrom-Json
function Get-Rows([string]$name) { @(($document.Sheets | Where-Object Name -eq $name).Rows | Where-Object Row -gt 1) }
function Get-ArticleId([string]$url) {
    if ($url -match '[?&](?:news|idx)=(\d+)') { return $Matches[1] }
    return $url
}
$costRows = @(Get-Rows 'Cost_근거')
$overlap = @(foreach ($row in $costRows) {
    $c = $row.Cells
    # 팀 문맥/날짜가 불확실하므로 동일 선수 시즌 후보로만 표시하고 자동 병합하지 않는다.
    $candidates = @($existing.Cards | Where-Object { $_.SourceYear -eq [int]$c.A -and $_.PlayerName -eq $c.C })
    $sameSource = @($candidates | Where-Object { (Get-ArticleId $_.SourceUrl) -eq (Get-ArticleId $c.K) })
    [pscustomobject]@{
        WorkbookRow = $row.Row; Year = $c.A; TeamContext = $c.B; PlayerName = $c.C
        Cost = $c.E; SourceDate = $c.G; SourceUrl = $c.K
        SameYearNameCandidates = $candidates.Count
        SameArticleSameCostCandidates = @($sameSource | Where-Object Cost -eq ([int]$c.E)).Count
        SameArticleOtherCostCandidates = @($sameSource | Where-Object Cost -ne ([int]$c.E)).Count
        ReviewStatus = 'CandidateOnly_NoAutomaticMerge'
    }
})
$cards = @(Get-Rows '정확스탯_카드이미지')
$archiveByYear = @{}
foreach ($year in @($cards | ForEach-Object { $_.Cells.A } | Select-Object -Unique)) {
    $archiveByYear[$year] = Get-Content (Join-Path $projectRoot "Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Years/$year.json") -Raw -Encoding UTF8 | ConvertFrom-Json
}
$cardComparison = @(foreach ($row in $cards) {
    $c = $row.Cells
    $type = if ($c.D -eq '타자') { 'Hitter' } else { 'Pitcher' }
    $matches = @($archiveByYear[$c.A].playerSeasons | Where-Object {
        $_.sourceReferenceNames -contains $c.C -and $_.originFranchiseId -eq $c.B -and $_.playerType -eq $type
    })
    if ($matches.Count -ne 1) {
        [pscustomobject]@{ WorkbookRow = $row.Row; Year = [int]$c.A; Team = $c.B; PlayerName = $c.C; PlayerType = $type; Variant = $c.E; ReferenceCost = [int]$c.F; ImageUrl = $c.V; JoinStatus = "Unresolved:$($matches.Count)"; CalibrationEligible = $false }
        continue
    }
    $player = $matches[0]
    $attributeMap = if ($type -eq 'Hitter') {
        @{ Contact = @('G',0); Power = @('H',1); Speed = @('I',2); Defense = @('K',4); BatterMental = @('L',5) }
    } else {
        @{ Stamina = @('M',6); Velocity = @('N',7); Stuff = @('O',8); Breaking = @('P',9); Control = @('Q',10); PitcherMental = @('R',11) }
    }
    $differences = [ordered]@{}
    foreach ($name in @($attributeMap.Keys | Sort-Object)) {
        $column,$index = $attributeMap[$name]
        $differences[$name] = @{ Reference = [int]$c.$column; Current = $player.baseAttributes[$index]; Delta = $player.baseAttributes[$index] - [int]$c.$column }
    }
    [pscustomobject]@{
        WorkbookRow = $row.Row; Year = [int]$c.A; Team = $c.B; PlayerName = $c.C; PlayerType = $type
        EditorPlayerSeasonId = $player.playerSeasonId; JoinStatus = 'ExactYearTeamNameType'; Variant = $c.E; SnapshotStatus = 'Unknown'
        ReferenceCost = [int]$c.F; CurrentCost = $player.cost; Delta = $player.cost - [int]$c.F
        ReferenceBunt = $c.J; ArmMapping = 'Excluded_BuntIsNotArm'; Attributes = $differences
        ImageUrl = $c.V; EvidenceStatus = 'UserTranscription_ImageNotReverified'; CalibrationEligible = $false
    }
})
$summaryRows = @(( $document.Sheets | Where-Object Name -eq '요약').Rows | Where-Object { $_.Row -ge 13 })
foreach ($row in $summaryRows) {
    $actual = @($costRows | Where-Object { $_.Cells.E -eq $row.Cells.A }).Count
    if ($actual -ne [int]$row.Cells.B) { throw "요약 수식 캐시 불일치: $($row.Row)행" }
}
$summary = [ordered]@{
    WorkbookSHA256 = $document.SHA256
    Counts = @{ Cost = $costRows.Count; CardAttributes = $cards.Count; DefenseThreshold = @(Get-Rows '수비등급_추가필요치').Count; PitchThreshold = @(Get-Rows '구종등급_추가필요치').Count; Fragments = @(Get-Rows '스탯단편_성적').Count; ConflictCases = @(Get-Rows 'Cost_충돌사례').Count }
    SameArticleSameCostCandidateRows = @($overlap | Where-Object SameArticleSameCostCandidates -gt 0).Count
    SameArticleOtherCostCandidateRows = @($overlap | Where-Object SameArticleOtherCostCandidates -gt 0).Count
    NoYearNameCandidateRows = @($overlap | Where-Object SameYearNameCandidates -eq 0).Count
    PitchThresholdUniqueYearPlayers = @((Get-Rows '구종등급_추가필요치') | ForEach-Object { $_.Cells.A + '|' + $_.Cells.B } | Select-Object -Unique).Count
    Cost7PlusRows = @($costRows | Where-Object { [int]$_.Cells.E -ge 7 }).Count
    NormalImageRows = @($cards | Where-Object { $_.Cells.E -eq 'Normal' }).Count
    CardYearCount = $archiveByYear.Count
    CardsSourceJoined = @($cardComparison | Where-Object JoinStatus -eq 'ExactYearTeamNameType').Count
    CardsSourceUnresolved = @($cardComparison | Where-Object JoinStatus -ne 'ExactYearTeamNameType').Count
    SummaryCountIfCachesVerified = 10
    ProductionModified = $false
}
$overlap | Export-Csv (Join-Path $PSScriptRoot 'workbook_overlap_candidates.csv') -NoTypeInformation -Encoding UTF8
$cardComparison | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $PSScriptRoot 'workbook_card_comparison.json') -Encoding UTF8
$summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $PSScriptRoot 'workbook_audit_summary.json') -Encoding UTF8
$summary | ConvertTo-Json -Depth 8
