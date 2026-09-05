# Shared Screens — Career Adapter 연결 요청

## 구현 상태

Player Career 입력 Adapter는 `Presentation/SharedScreens/Adapters/Career`에 구현되었다.

- `CareerLeagueSnapshotAdapter`
- `CareerScheduleSnapshotAdapter`
- `CareerRecordsSnapshotAdapter`
- `CareerTeamOverviewSnapshotAdapter`
- `CareerPlayerDetailSnapshotAdapter`

`UI_Scene_League`의 8개 구단 순위는 `CareerLeagueSnapshotAdapter`와
`CompactRecordTableView`를 실제로 소비한다. 이 View는 전체 Row를 생성하는 대신 계약을 소규모 표
최대 12행으로 제한한다. 12행을 넘을 수 있는 전체 기록 리더보드는 이 View에 연결하지 않았으며,
재사용 Row Pool 또는 Viewport 기반 Virtualization View가 추가되어야 한다.

## 결정

현재 `CareerScheduleView`, `LeagueHubView`, `CareerRecordsView`, `TeamOverviewView`,
`PlayerProfileView`를 공용 화면이 직접 참조하지 않는다.

이 타입들은 값 자체는 유용하지만 `PlayerTeam`, `MyPlayer`, Career 성장·계약·트레이드처럼
선수 모드 관점이 계약에 포함되어 있다. Owner 화면이 이를 직접 소비하면 공용 화면이 선수 모드의
권한과 상태를 알게 되므로, 모드별 Adapter가 `Baseball.Presentation.SharedScreens`의 순수 Snapshot으로
변환해야 한다.

## Player Adapter 요청

Player 전용 영역 또는 통합 Lead 소유 영역에 다음 Adapter를 추가한다.

| Adapter 후보 | 현재 입력 | 공용 출력 | 핵심 매핑 |
|---|---|---|---|
| `CareerScheduleSharedSnapshotAdapter` | `CareerScheduleView` | `ScheduleScreenSnapshot` | `PlayerTeamId`는 `FocusTeamId`, `IsPlayerHome`은 `ScheduleFocusSide`, `CareerScheduleOutcome`은 `ScheduleFocusOutcome`으로 변환한다. |
| `CareerLeagueSharedSnapshotAdapter` | `LeagueHubView` | `LeagueScreenSnapshot` | `Standings`, `RecentResults`, `NextRoundGames`를 각각 `RecordTableModel`로 변환하고 `IsMyTeam`은 Row 강조로만 전달한다. |
| `CareerRecordsSharedSnapshotAdapter` | `CareerRecordsView` | `RecordsScreenSnapshot` | Metric formatter가 DisplayValue와 원시 Number sort value를 함께 만든다. `IsMyPlayer`는 Row 강조와 `FocusedRowId`로 전달한다. |
| `CareerTeamSharedSnapshotAdapter` | `TeamOverviewView` | `TeamOverviewSnapshot` | `Roster`, `StartingLineup`, `StartingRotation`, `Bullpen`을 `ReadOnlyRosterGroupModel`로 변환한다. Career Trade 상태는 Snapshot에 넣지 않고 Player Action Provider 또는 Career Workspace에 남긴다. |
| `CareerPlayerDetailSharedSnapshotAdapter` | `PlayerProfileView` | `PlayerDetailSnapshot` | 공통 신상·능력·시즌/통산 기록만 변환한다. Skill Board, 보유 Block, 계약 변경은 Player 전용 Workspace/Action Provider에 남긴다. |

모든 ID는 기존 정수 값을 `InvariantCulture` 문자열로 변환하거나 프로젝트의 안정 ID Formatter를
한 곳에서 사용한다. 표시 문자열과 숫자 정렬 값은 분리한다.

## Owner Adapter 요청

Owner Adapter는 동일 Snapshot을 만들되 `MyPlayer`, `PlayerTeam`을 만들지 않는다.

- 현재 선택 구단은 `SharedScreenContext.SubjectId` / `FocusedEntityId`로 공급한다.
- Roster 편집, 상대 분석, 선수 배치는 `ISharedScreenActionProvider` 구현으로 공급한다.
- `OwnedPlayerCardState`, SP, DP, Enhancement, CardTraining은 공용 Snapshot에 넣지 않는다.
- Owner 편집 Workspace는 `ReadOnlyRosterModel`을 명령 모델로 확장하지 말고 별도 Owner 모델을 사용한다.

## Action Provider 요청

같은 `SharedScreenProfile`, `SharedScreenContext`, Snapshot을 사용하고 Provider만 교체한다.

```text
공용 Records Snapshot
  + OwnerRecordsActionProvider  → 상대 분석 / 선수단 배치
  + PlayerRecordsActionProvider → 내 선수 보기 / 기용 이유
```

Provider의 Action에는 필요한 `UiCapability`를 지정한다. 공용
`SharedScreenPresentationModel<TSnapshot>`이 Capability가 없는 Action을 제거하므로 화면 코드에서
`if (OwnerMode)` / `if (PlayerMode)`를 추가하지 않는다.

## 현재 데이터 공백

- `TeamOverviewView`는 현재 기용 결과와 `PlannedPlayerRole`은 제공하지만 감독의 상세 판단 이유 문구는
  제공하지 않는다. 임의 추론하지 말고 Game/Simulation이 확정한 설명 Snapshot이 추가될 때까지
  해당 Action을 비활성화한다.
- 기존 `TeamColor`와 Emblem 정수 ID는 Presentation Adapter에서 제한적 Accent Hex와 Asset Key로
  변환해야 한다.
- `LeagueDefinition` 전체를 공용 Snapshot으로 넘기지 않는다. 화면에 필요한 League Grade, 승강 구역,
  진행률만 형식화하거나 기록표 셀로 전달한다.

## 통합 완료 조건

1. Player와 Owner가 동일한 Snapshot fixture로 동일한 표/선수단 데이터를 그린다.
2. 모드 차이는 Action Provider와 Capability뿐이다.
3. 공용 화면 namespace에서 `Baseball.Game.Career`, `OwnedPlayerCardState`, `CareerPlayerState` 참조가 없다.
4. 숫자 열은 표시 문자열이 아니라 `RecordSortValue.FromNumber`로 정렬한다.
5. Empty/Loading/Error는 `UiContentStateModel`을 통해 표시한다.
