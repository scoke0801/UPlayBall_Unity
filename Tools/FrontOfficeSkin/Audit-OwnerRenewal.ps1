# UI 코드·진입·상태·프리팹 역참조를 파일만 읽어 기록한다. Unity나 테스트 러너를 실행하지 않는다.
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$report = Join-Path $project 'docs/reports/owner-ui-renewal'
[IO.Directory]::CreateDirectory($report) | Out-Null
$files = @(Get-ChildItem (Join-Path $project 'Assets/02.Scripts') -Recurse -Filter '*.cs')
$sources = @{}
foreach ($file in $files) { $sources[$file.FullName] = [IO.File]::ReadAllText($file.FullName) }
$presentation = @($files | Where-Object { $_.FullName -match '\\Presentation\\' })
$entryPattern = '\\Owner\\|\\Shop\\|\\Encyclopedia\\|\\Match\\UI_Scene_Owner|\\SharedUI\\Shell\\|UI_Scene_NewGame|UI_Popup_CareerSettings|\\Guide\\UI_System_FrontManagerGuide|UIStatusHint.cs|LoadingSceneController.cs|RecordTableView.cs|ReadOnlyRosterListView.cs'
$scope = @($presentation | Where-Object { $_.FullName -match $entryPattern })
$inventory = @()
$actions = @()
$states = @()
$resources = @()
$routes = @()
foreach ($file in $scope) {
    $source = $sources[$file.FullName]
    $path = $file.FullName.Substring($project.Length + 1).Replace('\', '/')
    $id = $file.BaseName.Split('.')[0]
    $lines = $source -split '\r?\n'
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()
        if ($line -match 'onClick.AddListener|CreateButton\(|Button\(|Requested\?\.Invoke|Confirmed\?\.Invoke') {
            $actions += [pscustomobject]@{ View = $id; Source = $path; Line = $i + 1; Binding = $line }
        }
        if ($line -match 'interactable|[Ee]mpty|[Ee]rror|[Ll]oading|[Ll]ocked|[Dd]isabled|[Ss]elected|[Ff]ocus|[Rr]eward|TryHandleCancel|TryGoBack') {
            $states += [pscustomobject]@{ View = $id; Source = $path; Line = $i + 1; Contract = $line }
        }
        if ($line -match 'Resources.Load|FrontOfficeSkin.Load|FrontOfficePanel.Apply|OwnerUiButtonSkin.Apply|CreatePanel\(') {
            $resources += [pscustomobject]@{ View = $id; Source = $path; Line = $i + 1; Resource = $line }
        }
        if ($line -match 'new NavigationEntry|case OwnerNavigationRoutes|case OwnerManagementRoutes|case .*RouteId|RouteId =|const string .* = "(Owner|Shared)\.') {
            $routes += [pscustomobject]@{ Source = $path; Line = $i + 1; Route = $line }
        }
    }
    if (($file.BaseName -match '^UI_(Scene|Popup|System)_' -or $file.BaseName -in 'SharedGameShellView', 'UIStatusHint', 'LoadingSceneController', 'OwnerManagementWorkspaceView', 'RecordTableView', 'CompactRecordTableView', 'ReadOnlyRosterListView') -and $file.BaseName -notmatch '\.') {
        $callers = @($files | Where-Object { $_.FullName -ne $file.FullName -and $sources[$_.FullName] -match ('\b' + [regex]::Escape($id) + '\b') } |
            ForEach-Object { $_.FullName.Substring($project.Length + 1).Replace('\', '/') })
        $status = if ($id -eq 'UI_System_OwnerCheat') { '제외: 개발 전용 IMGUI, 플레이어 화면 아님' }
            elseif ($id -eq 'UI_System_FrontManagerGuide') { '제외: OwnerCareer에서 Hide, UI_System_OwnerGuide가 정규 소비자' }
            elseif ($id -in 'CompactRecordTableView', 'ReadOnlyRosterListView') { '제외: 소비자가 선수 모드에만 있음; 구단주는 RecordTableView 사용' }
            elseif ($callers.Count -eq 0) { '보류: 코드 진입 참조 없음, 고아 후보' }
            else { '정적 반영: 공용 스킨 및 상태 경로 / 실행 검수 미진행' }
        $inventory += [pscustomobject]@{ View = $id; Source = $path; Entry = ($callers -join '; ');
            Prefab = '런타임 생성 + UI_Surface/UI_Control + V2 상태 자원';
            Resolution = '1920x1080 기준, 2560x1440 및 가로 확장 설계 / 실행 미검수'; Status = $status }
    }
}
$inventory | Export-Csv (Join-Path $report 'screens.csv') -NoTypeInformation -Encoding UTF8
$actions | Export-Csv (Join-Path $report 'actions.csv') -NoTypeInformation -Encoding UTF8
$states | Export-Csv (Join-Path $report 'states.csv') -NoTypeInformation -Encoding UTF8
$resources | Export-Csv (Join-Path $report 'resources.csv') -NoTypeInformation -Encoding UTF8
$routes | Export-Csv (Join-Path $report 'routes.csv') -NoTypeInformation -Encoding UTF8
$subscriptions = foreach ($file in $files) {
    $lines = $sources[$file.FullName] -split '\r?\n'
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '\.(\w+(?:Requested|Confirmed|Selected|Changed))\s*[+-]=') {
            [pscustomobject]@{ Event = $Matches[1]; Source = $file.FullName.Substring($project.Length + 1);
                Line = $i + 1; Subscriber = $lines[$i].Trim() }
        }
    }
}
$subscriptions | Export-Csv (Join-Path $report 'event-subscriptions.csv') -NoTypeInformation -Encoding UTF8

# 실제 씬·프리팹에서 컴포넌트 GUID를 역추적한다. 미참조는 삭제하지 않는다.
$assets = @(Get-ChildItem (Join-Path $project 'Assets') -Recurse -File | Where-Object { $_.Extension -in '.prefab', '.unity', '.spriteatlas', '.asset' })
$prefabRows = foreach ($asset in $assets | Where-Object { $_.Extension -in '.prefab', '.unity' }) {
    $meta = [IO.File]::ReadAllText($asset.FullName + '.meta')
    $guid = [regex]::Match($meta, 'guid: ([a-f0-9]+)').Groups[1].Value
    $users = @($assets | Where-Object { $_.FullName -ne $asset.FullName -and [IO.File]::ReadAllText($_.FullName).Contains($guid) } |
        ForEach-Object { $_.FullName.Substring($project.Length + 1) })
    $codeUsers = @($scope | Where-Object { $sources[$_.FullName].Contains($asset.BaseName) } |
        ForEach-Object { $_.FullName.Substring($project.Length + 1) })
    [pscustomobject]@{ Asset = $asset.FullName.Substring($project.Length + 1); Guid = $guid;
        AssetUsers = ($users -join '; '); CodeUsers = ($codeUsers -join '; ');
        Status = if ($asset.BaseName -eq 'FrontOfficeSkinPreview') { '제외: 검수 전용 프리팹' }
            elseif ($users.Count + $codeUsers.Count -eq 0) { '씬 등록·동적 경로 별도 확인; 고아 후보, 삭제하지 않음' }
            else { '참조 확인' } }
}
$prefabRows | Export-Csv (Join-Path $report 'scene-prefab-references.csv') -NoTypeInformation -Encoding UTF8
Write-Output ("화면 {0}, 버튼·의도 {1}, 상태 근거 {2}, 자원 연결 {3}, Route 근거 {4}, 씬·프리팹 {5}. 파일 기반 조사만 수행." -f
    $inventory.Count, $actions.Count, $states.Count, $resources.Count, $routes.Count, @($prefabRows).Count)
