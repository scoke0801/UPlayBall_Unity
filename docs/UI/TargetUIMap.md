# 목표 UI 지도

## 최상위 구조

```text
UIRoot
├─ HUDLayer
├─ SceneLayer
│  └─ SharedGameShell
│     ├─ GlobalTopBar
│     ├─ PrimaryNavigation
│     ├─ ContextHeader
│     ├─ MainWorkspaceHost
│     ├─ OptionalRightInspector
│     └─ ContextActionBar
├─ PopupLayer
│  ├─ PlayerCardDetailPopup
│  ├─ Confirm/Result Popup
│  └─ Mode-specific Popup
└─ SystemLayer
   ├─ TooltipLayer
   ├─ ToastLayer
   └─ ClickFeedback
```

`SharedGameShell`은 모드 State를 직접 찾지 않는다. `GameModeUiProfile`, `IUiShellStatusProvider`, `NavigationManifest`, `UiCapabilitySet`을 입력받는다.

## 공용 Route

| Route | Workspace | Mode 차이 |
|---|---|---|
| `League.Standings` | 순위표 + 리그 사다리 | Owner는 상대 분석 action, Player는 읽기 전용 구단 보기. |
| `League.Schedule` | 일정/경기 요약 | Owner는 준비/운영 action, Player는 기용/내 역할 action. |
| `Records.Season` | 선수·구단 기록표 | Player 기본 필터가 내 선수. |
| `Team.Overview` | 구단 요약 | action adapter만 분리. |
| `Team.Roster` | 공용 Roster visual | Owner editable, Player read-only + 내 선수/감독 결정 강조. |
| `Player.Detail` | Large Card + 기록 | Owner owned-card action, Player career/read-only action. |
| `News` | 실제 사건 목록 | 기본 필터와 상세 action만 분리. |
| `Match.Result` | 공용 Score/BoxScore | Owner 팀 운영 요약, Player 개인 결과/감독 평가. |

## Player Workspace

| Route | 핵심 목적 | 기존 연결 |
|---|---|---|
| `Player.Home` | 다음 경기, 기용, 상태, 개인 성적을 한 화면에 설명 | `CareerDashboardView` |
| `Player.Profile` | 내 선수 카드/능력/기록 | `PlayerProfileView` |
| `Player.Growth` | Skill Board와 오프시즌 성장 | `CareerGrowthView` |
| `Player.Contract` | 계약·오퍼·이동 | `CareerContractView` |
| `Player.TeamReadOnly` | 감독 AI의 현재 Lineup/Rotation 이해 | `TeamOverviewView`, 실제 `MatchRosterSnapshot` |
| `Player.MatchOverlay` | 내 선수 입력과 감독 결정 표시 | 기존 Match Session/mini-game presenter |

## Owner Workspace

| Route | 핵심 목적 | 구현 조건 |
|---|---|---|
| `Owner.Home` | 다음 경기, 순위, 최근 결과, Money/SP/DP, 실제 알림 | 실제 Owner Runtime manager 필요 |
| `Owner.Roster` | 25인 편성과 분석 | `CurrentRosterState`, 공용 Resolver 결과 사용 |
| `Owner.Lineup.Hitter` | 9 Starter + Bench 5 | UI가 인원/외국인/중복 규칙 재계산 금지 |
| `Owner.Lineup.Pitcher` | Starter 5 + Bullpen 4 + Setup + Closer | `BullpenUsagePolicy`/Role 결과 사용 |
| `Owner.TeamColor` | 2 Slot과 Resolver 후보/효과 | `TeamColorResolver` 결과 필요 |
| `Owner.Scout` | Pool/실제 확률/Pity | `ScoutPoolDefinition`/Resolver 연결 시에만 활성 |
| `Owner.Tactic` | 감독에게 운영 방침 설정 | `ManagerTacticProfile`, Tactic resolver 유지 |
| `Owner.Club` | 실제 구현된 재정·시설·계약만 표시 | 미구현 기능은 숨김 또는 Locked reason |
| `Owner.MatchOverlay` | 관전 속도와 실제 권한의 운영 action | 공용 `MatchHUDBase` 위 합성 |

## Match 구조

```text
MatchShell
├─ MatchHUDBase
│  ├─ Inning/Score/B-S-O/Bases
│  ├─ Batter/Pitcher
│  ├─ MatchLog
│  └─ EventToast
├─ OwnerMatchOverlay
└─ PlayerMatchOverlay
```

기존 `DetailedMatchEngine`과 `MatchEvent` 스트림은 변경하지 않는다. 모드 차이는 입력 공급자와 표시 Overlay에만 둔다.
