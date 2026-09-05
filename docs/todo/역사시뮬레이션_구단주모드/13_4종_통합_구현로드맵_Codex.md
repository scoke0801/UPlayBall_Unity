# 13. 4종 확장 시스템 통합 구현 로드맵 — Codex 멀티 에이전트 작업 지침

> 대상 문서
>
> - [09_구단경영_구장_팬_관중_경제.md](09_구단경영_구장_팬_관중_경제.md)
> - [10_경기전_상대분석_라인업프리셋.md](10_경기전_상대분석_라인업프리셋.md)
> - [11_코칭스태프_시스템.md](11_코칭스태프_시스템.md)
> - [12_컨디션_타선_배터리_궁합.md](12_컨디션_타선_배터리_궁합.md)

## 0. 목표

네 시스템을 각각 독립적으로 만들되, 최종적으로 다음 게임 루프로 닫는다.

```text
구단 경영/시설/스태프
     ↓
회복·분석·SP/DP 효율
     ↓
경기 전 상대 분석
     ↓
라인업 프리셋 + Condition/Chemistry + TeamColor + Tactic 준비
     ↓
경기 Simulation
     ↓
경기 결과
     ↓
Condition/Familiarity/팬/관중/Money 변화
     ↓
주간 운영
```

이 문서는 Codex가 작업을 시작할 때 그대로 전달할 수 있는 구현 지침이다.

---

## 1. 최우선 준수사항

1. 먼저 전체 코드베이스와 기존 01~08 정본 문서를 읽는다.
2. 기존 타입/Resolver/ViewModel이 있으면 재사용한다. 이름만 다른 중복 타입 금지.
3. `Baseball.Core` / `Baseball.Simulation`은 Unity 비의존을 유지한다.
4. Simulation 판정을 Presentation에서 구현하지 않는다.
5. 기존 결정론 RNG 정책을 따른다.
6. Balance 수치는 Definition/Table/Config로 데이터화한다.
7. 플레이어 카드 원본, WorldRecord, TeamColor, Roster 25인 계약을 변경하지 않는다.
8. Manager Mode 플레이어 Owned Economy가 AI/선수 커리어 모드에 유출되지 않게 한다.
9. 컨디션은 기존 Source of Truth를 재사용하고 새 평행 시스템을 만들지 않는다.
10. UI는 저장/판정의 정본이 아니다. ViewModel/RuntimeState를 표시한다.
11. 현재 프로젝트 UI Production Guideline이 별도 존재하면 그것을 최우선으로 따른다.
12. Presentation 표시 용어가 `구단주 모드`로 변경된 프로젝트라면 UI 텍스트는 그 용어를 사용하되 기존 코드 타입명을 무리하게 전면 개명하지 않는다.

---

## 2. 구현 순서

### Phase X0 — 병렬 Repository Audit

아래 Agent를 동시에 실행한다.

```text
Audit-A Economy/Manager Mode
  ManagerEconomyState, weekly tick, rewards, save, facilities 관련 코드

Audit-B Match/PreGame
  Schedule, TeamOverview, Lineup, Rotation, Match start, Tactic loadout

Audit-C Staff/Contract
  Coach/Staff/Manager/Contract/name/stable-id 관련 코드

Audit-D Condition
  Condition, fatigue, injury, off-position, pitcher role, EffectiveRating

Audit-E UI
  UI Toolkit architecture, common shell, card/list/popup components, asset policy

Audit-F Tests
  existing rule/determinism/statistical/persistence test harness
```

각 Agent는 **코드를 수정하지 말고** 다음 형식으로 보고한다.

```text
Existing reusable types
Existing reusable services/resolvers
Existing save schema
Potential duplicates/conflicts
Required new contracts
Files likely to change
Tests that already cover the area
```

### Gate X0

Orchestrator가 결과를 합쳐 `Implementation Contract Note`를 만든다.

이 Gate 이전에는 새로운 Core 타입을 추가하지 않는다.

---

## 3. Core Lane — 4개 병렬 작업

X0 계약 확정 뒤 다음 4 Lane을 병렬 시작한다.

### Lane A — 09 구단 경영

담당:

```text
ClubOperationState
Stadium/Facility Definitions
Attendance/Fan/Popularity Resolver
HomeGameFinance Resolver
Weekly Facility Production
Balance Tables
```

금지:

- Condition/Scouting/Tactic 내부 로직 직접 구현
- BaseStat 버프 시설

### Lane B — 10 경기 전 분석/프리셋

담당:

```text
OpponentScoutingReport
ProbableStarter/ExpectedLineup estimators
Intel confidence
LineupPreset
Preset validation
PreGamePlanSnapshot contract
```

금지:

- AI hidden state 직접 노출
- ActiveRoster 자동 변경

### Lane C — 11 스태프

담당:

```text
StaffDefinition/Catalog
Contract/Assignment
StaffEffectProfile
Market/Salary
AI Staff Effect Profile
```

금지:

- BaseStat 직접 버프
- Scout odds 몰래 변경

### Lane D — 12 Condition/Chemistry

담당:

```text
기존 Condition contract 확정
Presentation 10-level mapping
Familiarity State
Lineup Chemistry
Battery Chemistry
EffectiveMatchCondition composition
```

금지:

- 기존 Condition과 별개 상태 생성
- Chemistry Stat Bonus

---

## 4. 테스트 Agent를 각 Lane에 붙인다

각 Core Agent와 별도로 테스트 Agent를 둔다.

```text
Test-A Economy
Test-B Pregame/Preset
Test-C Staff
Test-D Condition/Chemistry
```

구현 Agent가 자기 테스트만 작성하고 완료 선언하지 않게 한다.

테스트 Agent는 독립적으로 실패 케이스를 설계한다.

---

## 5. Integration Gate 1 — 공통 API 동결

4개 Lane의 Definition/Resolver API가 빌드되고 기본 Rule/Determinism Test를 통과하면 다음 API를 동결한다.

```text
IClubOperationService / equivalent
IOpponentScoutingReportProvider / equivalent
ILineupPresetService / equivalent
ITeamStaffEffectProvider / equivalent
ICondition/MatchCondition contract (existing type preferred)
ILineupChemistryResolver
IBatteryChemistryResolver
```

프로젝트 기존 naming convention이 있으면 그 이름을 따른다. 위 인터페이스 이름을 그대로 강제하지 않는다.

---

## 6. Cross-System Integration — 의존 관계 순서

### 6.1 병렬 가능

```text
ScoutingCenter → SP production
TrainingCenter → DP production
FanShop → revenue
Staff Market → Money
LineupPreset → existing Lineup/TeamColor/Tactic loadouts
```

### 6.2 순서 의존

```text
12 Condition API 확정
  → RecoveryCenter 연결
  → ConditioningCoach 연결

10 Intel Confidence API 확정
  → DataAnalysisCenter 연결
  → ScoutingDirector 연결

06 Tactic Research 기존 API 확인
  → TacticLab 통합
```

시설과 스태프가 각각 Consumer를 직접 호출해 효과가 두 번 적용되지 않게 한다.

권장 합성:

```text
ConditionRecoveryContext
{
    BaseRecovery
    FacilityModifier
    StaffModifier
}

ScoutingConfidenceContext
{
    BaseEvidence
    FacilityModifier
    StaffModifier
}
```

---

## 7. Runtime/Persistence Agent

별도 Agent가 네 시스템의 Save 경계를 통합 검토한다.

플레이어 Manager Mode Save:

```text
ClubOperationState
StaffContractState/Assignment
LineupPreset[]
기존 Condition State
TeamChemistryFamiliarityState
```

원칙:

- Definition 전체 복제 금지.
- Stable ID 참조.
- 파생 Report/Chemistry 점수는 필요 없으면 Save하지 않는다.
- Load 시 홈경기 수익/급여/시설 생산이 중복 실행되지 않는다.
- AI/Career Save에 Owner 전용 경제 타입을 넣지 않는다.

Persistence Agent는 Migration이 필요한지 기존 Save schema를 조사하고, 필요하면 명시적 버전 업/기본값 전략을 작성한다.

---

## 8. UI Agent 운영

### 8.1 UI 작업 시작 조건

Core 계약과 ViewModel shape가 확정되기 전에 실제 화면을 크게 만들지 않는다.

Mock 화면은 가능하지만 최종 UI 코드는 Gate 1 이후 시작한다.

### 8.2 UI 화면

```text
09: 구단 경영/구장/시설/재무
10: 경기 준비/상대 분석/프리셋/전술
11: 스태프 오피스/시장/계약
12: 라인업 Condition/Chemistry + 배터리 표시
```

### 8.3 공통 UX

- 기존 게임 공통 셸 사용.
- 25인 카드/미니카드 UI 재사용.
- 상세 정보는 Popup/Side Panel로 Drill-down.
- 수치가 변하면 이유를 설명하는 Breakdown을 제공.
- 비주포지션은 막지 않고 Warning.
- Unknown intel은 빈 값이 아니라 `정보 부족` 상태로 표현.

### 8.4 ImageGen 사용

UI Agent는 기존 에셋으로 충분하면 ImageGen을 사용하지 않는다.

필요한 경우 Codex에서 ImageGen을 호출해 다음을 생성한다.

```text
구장/시설 배경
스태프 가상 초상화
분석실/오피스 분위기 일러스트
Condition/시설용 비기능성 심볼 컨셉
```

ImageGen 프롬프트 요구:

- 엔트리브 프로야구매니저의 **관리형 2D 야구 게임 감성**을 레퍼런스 수준으로만 사용.
- 특정 원작 UI/캐릭터/로고/팀을 직접 복제하지 않음.
- 팀 컬러로 오해될 단일 강색을 화면 전체 기본색으로 고정하지 않음.
- 실제 텍스트/숫자/버튼 라벨을 이미지에 포함하지 않음.
- 투명 PNG가 필요한 아이콘/초상화는 배경 분리 가능하게 생성.

생성 이미지 적용 후 실제 Unity UI에서 16:9 주요 해상도 기준 시인성을 검증한다.

---

## 9. 통합 E2E 플로우

최소 한 개의 Production Manager Mode 시나리오가 다음을 전부 통과해야 한다.

```text
1. Save 생성
2. 구단 경영 화면 진입
3. 시설 업그레이드
4. 스태프 계약
5. 주간 진행 → SP/DP/급여 반영
6. 다음 홈경기 선택
7. 상대 분석 리포트 확인
8. 라인업 프리셋 적용
9. Condition/Chemistry/TeamColor/Position Warning 확인
10. 전술카드 2장 확정
11. 경기 실행
12. Battery/Condition 적용 확인
13. 경기 종료
14. Familiarity/Condition 갱신
15. 홈 관중/Money/Popularity 갱신
16. Save
17. Load
18. 모든 상태 복원 및 중복 지급/차감 없음
```

---

## 10. 장기 검증

기존 08절의 Fast / Statistical / Long-running 구분을 유지한다.

### 10.1 Fast

- Rule tests
- Determinism
- Serialization
- ViewModel smoke

### 10.2 Statistical

```text
Attendance distribution
Money income/expense
Facility ROI
Staff efficiency
Intel confidence distribution
Condition distribution
Chemistry delta distribution
AI lineup changes
```

### 10.3 Long-running

여러 시즌/리그 Seed 반복.

검증 질문:

- Money가 무한 인플레이션하지 않는가?
- 시설 최고 레벨이 너무 빨리 열리지 않는가?
- Staff 최고 등급 5명 고정이 유일한 정답인가?
- 상대 분석이 사실상 치트가 되지 않는가?
- 프리셋이 Validator를 우회하지 않는가?
- Chemistry 때문에 약한 선수가 과도하게 기용되지 않는가?
- Condition이 항상 최고/최악으로 고착되지 않는가?
- RecoveryCenter + ConditioningCoach가 중복 회복 버그를 만들지 않는가?

---

## 11. 문서 업데이트 Agent

구현 완료 후 별도 Agent가 문서를 수정한다.

필수 작업:

```text
README.md
05_구단주모드_경제_스카우트.md
06_전술카드_시스템.md
08_구현_로드맵_검증기준.md
09~12 신규 문서
```

필요 시 02/04/07도 Cross-reference를 추가한다.

주의:

- 구현되지 않은 것을 완료로 쓰지 않는다.
- Resolver 존재만으로 Phase 완료 처리하지 않는다.
- 실제 Runtime/Save/E2E/Long Simulation 상태를 구분한다.

---

## 12. 독립 검증 Agent

마지막에 구현에 참여하지 않은 Agent에게 다음을 요청한다.

```text
1. 01~12 문서와 코드 계약 충돌 검색
2. 중복 타입/Resolver 검색
3. Unity 참조 경계 검사
4. Owner Economy의 Career/AI 유출 검사
5. Condition 이중 적용 검사
6. Facility/Staff Modifier 이중 적용 검사
7. Intel hidden-state leak 검사
8. Save 중복 Tick 검사
9. UI에서 Presentation 판정 로직 검사
10. 테스트 누락/장기 Gate 누락 정리
```

이 Review에서 Critical/Major가 남으면 완료 선언 금지.

---

## 13. 최종 완료 정의

네 시스템은 다음을 모두 만족해야 `완료`다.

```text
Core Contract Complete
Simulation Integration Complete
Manager Mode Runtime Flow Complete
Persistence Complete
UI Complete
Rule Tests Complete
Determinism Complete
Statistical Validation Complete
Long-running Validation Executed
Independent Review: no Critical/Major
Documentation Updated
```

부분 완료 항목이 있으면 08절 Phase Gate처럼 정확히 `부분 완료`로 남긴다.

---

## 14. Codex에 전달할 최종 실행 지시문

아래 지시를 이 문서 및 09~12 문서와 함께 전달한다.

```text
첨부된 09~13 기획 문서를 Source of Truth로 사용해 4종 확장 시스템을 구현하라.

먼저 코드를 수정하지 말고 Phase X0 Repository Audit을 멀티 에이전트로 병렬 수행한다.
각 Audit 결과를 합쳐 재사용 타입, 신규 계약, 충돌 가능성, 변경 파일, 테스트 계획을 확정한다.

그 후 Lane A(구단 경영), Lane B(경기 전 분석/프리셋), Lane C(스태프), Lane D(Condition/Chemistry)를
서로 독립된 Agent로 병렬 구현한다. 각 Lane에는 별도 Test Agent를 붙이고, 구현 Agent의 자기검증만으로
완료 처리하지 않는다.

기존 코드에 동등한 Definition/Resolver가 있으면 반드시 재사용/확장하고 이름만 다른 중복 구현을 만들지 마라.
특히 Condition은 기존 Source of Truth를 확인하기 전 새 타입을 만들지 마라.
Baseball.Core/Baseball.Simulation은 Unity 비의존을 유지하고, Presentation에서 Simulation 판정을 하지 마라.
모든 Balance 값은 데이터화하고 기존 결정론 RNG 정책을 지켜라.

Core API Gate가 닫히면 Cross-System Integration을 진행한다.
RecoveryCenter와 ConditioningCoach는 하나의 Condition Recovery Context에 Modifier로 들어가야 하며,
DataAnalysisCenter와 ScoutingDirector도 하나의 Intel Confidence Context에 들어가야 한다.
효과가 두 번 적용되지 않도록 테스트하라.

UI는 ViewModel 계약 후 UI Toolkit으로 구현한다. 프로젝트의 기존 UI 제작 지침과 공통 셸을 우선한다.
기존 에셋으로 표현이 부족할 때만 ImageGen을 사용하라. ImageGen 결과에는 실제 UI 텍스트/수치를 굽지 말고,
원작 에셋을 직접 복제하지 않으며, 생성 이미지는 비기능성 배경/장식/가상 인물/심볼로만 사용하라.

마지막에는 Production Manager Mode에서
구단 경영 → 스태프 → 주간 진행 → 상대 분석 → 프리셋 → Condition/Chemistry → 전술 → 경기 →
관중/경제/회복 → Save/Load까지 이어지는 E2E를 실행하라.

Fast Rule/Determinism/Persistence 테스트와 Statistical/Long-running 검증을 분리해 수행하고,
독립 Review Agent가 문서/코드 충돌, 중복 타입, Condition 이중 적용, Modifier 이중 적용,
hidden-state 정보 누출, Save 중복 Tick, AI/Career Owner-Economy 유출을 검사하게 하라.

완료되지 않은 항목은 완료라고 쓰지 말고 08절 Phase Gate 형식으로 부분 완료와 남은 이유를 기록하라.
구현 결과에 맞춰 README, 05, 06, 08, 09~12 문서를 갱신하라.
```

---

## 15. Gate X0 — Repository Audit 기반 구현 계약

> 이 절은 실제 Repository를 읽은 뒤 고정한 구현 계약이다. 이후 구현은 기획 예시보다 이 계약의
> 재사용·소유권 결정을 우선한다.

### 15.1 재사용할 기존 계약

```text
ManagerEconomyState                         Money / SP / DP의 단일 잔액 원본
CurrentRosterState / ActiveRosterValidator  25인, 포지션, 외국인, Person 중복 검증
Lineup / LineupSlot                         9인 타순과 수비 위치 계약
PositionAssignmentPenaltyResolver           비주포지션·투수 역할 경고와 경기 비용
BullpenUsagePolicy / Resolver                Bullpen 4 + Setup 1 + Closer 1
MatchRosterSnapshot / MatchInput             DetailedMatchEngine 입력 경계
SeasonScheduleGenerator                      결정론적 일정 생성
TacticLoadoutState                           2슬롯·중복·확정 1회 소비
TeamColorDefinition / TeamColorResolver      TeamColor 2슬롯 효과
PitcherFatigueResolver                       경기 중 투구 수 기반 피로
RecentPitchingWorkload                       경기 사이 투구 부하 값 객체
SharedGameShellView 계열                     현재 공용 Production UI Shell
```

`Career.PlayerState.Condition`과 `PlayerGrowthState.Condition`은 선수 커리어 내부에 두 쓰기 가능한
복제본으로 존재한다. 구단주 모드에는 선수별 Condition 원본이 전혀 없으므로 둘 중 하나를 구단주
모드로 유출하지 않는다. 공통 Simulation 입력 계약과 Resolver만 공유한다.

### 15.2 확장할 기존 계약

```text
ManagerHistoricalRuntimeState  구단주 live season, 운영, 스태프, 프리셋, 선수 상태 접근점
ManagerHistoricalSaveData      위 원본 상태와 처리 영수증 저장
ManagerHistoricalSaveAdapter   기존 v1~v3 명시적 migration
MatchRosterSnapshot             선수별 EffectiveMatchCondition snapshot 조회
DetailedMatchEngine             Condition을 경기 능력에 한 번만 적용
Owner presentation model        운영·경기 준비 immutable ViewModel
OwnerModeUiProfileFactory       실제 Route와 Capability 연결
GameBootstrap / NewGame         구단주 Production 진입점
```

### 15.3 필요한 신규 계약

```text
Economy
  ClubOperationState, StadiumState, FacilityState, TicketPolicy
  HomeGameFinanceResult, SeasonFinanceSummary, OperationReceipt

Pregame
  IntelState, ScoutedValue<T>, OpponentScoutingReport
  LineupPresetState, LineupPresetValidationResult, PreGamePlanSnapshot

Staff
  StaffDefinition, StaffContractState, TeamStaffAssignmentState
  TeamStaffEffectProfile, StaffMarket

Condition
  TeamSeasonPlayerStatusState                  구단주 모드의 유일한 저장 Condition 원본
  EffectiveMatchCondition                      저장하지 않는 파생 breakdown
  ConditionRecoveryContext, ConditionPresentationTable
  TeamChemistryFamiliarityState, stable sorted Person pair
```

`TeamSeasonPlayerStatusState`는 `OwnedPlayerCardState`와 분리한다. AI 팀도 같은 상태를 가질 수 있지만
선수 커리어의 `PlayerState`를 소유하거나 구단주 경제에 참여하지 않는다.

### 15.4 Save와 Runtime 계약

- Save schema를 명시적으로 올리고 기존 v1~v3은 빈 운영 상태·기본 Condition으로 migration한다.
- Definition, 파생 Scouting report, 파생 Chemistry 점수, 선수 객체는 저장하지 않는다.
- 시설 생산, 급여, 홈 경기 수익, 경기 결과는 stable receipt/game ID를 저장해 Load 후 재적용을 막는다.
- 실제 경기 직전 현재 ActiveRoster와 availability를 다시 검증한 뒤 `PreGamePlanSnapshot`을 동결한다.
- 실제 경기 결과 커밋 한 번에서 Condition, workload, Familiarity, 팬, 관중, 수익을 함께 반영한다.
- Historical 일괄 시즌 결과나 미래 AI 상태를 상대 분석 evidence로 사용하지 않는다.

### 15.5 Cross-System 단일 적용점

```text
ConditionRecoveryContext
  = BaseRecovery + RecoveryCenter modifier + ConditioningCoach modifier

ScoutingConfidenceContext
  = BaseEvidence + DataAnalysisCenter modifier + ScoutingDirector modifier
```

시설과 스태프는 Consumer를 직접 두 번 호출하지 않는다. Assignment 비용은 기존
`PositionAssignmentPenaltyResolver`가 이미 경기 능력에 적용하므로 Chemistry 합성에서 같은 비용을
다시 넣지 않는다. Battery modifier는 투수/포수 교체 때 새 snapshot으로 교체하며 누적하지 않는다.

### 15.6 UI 계약

현재 Production UI는 UI Toolkit이 아니라 programmatic uGUI 기반 `SharedGameShellView`다. 프로젝트의
최신 UI Production Guideline이 기존 framework와 공용 Shell 재사용을 우선하므로, 이번 화면도 같은
Shell과 immutable ViewModel로 구현한다. 한 화면에서 UI Toolkit을 혼합하지 않는다. 기존 구단주 배경
에셋이 있어 기능 구현에 ImageGen은 필수가 아니다.

### 15.7 테스트 계약과 발견된 기준선 결함

- Core/Simulation/Game과 세 Test assembly의 headless compile은 Audit 시점에 통과했다.
- 기존 headless 회귀에는 낮은 `NaturalRoleConfidence`의 투수 역할 비용 기대값과 현재 반올림 계약이
  어긋난 테스트 2건이 있다. 이번 Condition 경로에서 구현과 기대값 중 어느 쪽이 정본인지 확정하고
  회귀로 닫는다.
- 신규 검증은 Rule, Determinism, Persistence, Production E2E, Statistical, Long-running을 분리한다.
- 특히 stale preset, hidden-state leak, Condition 0, 중복 receipt, TeamSeason familiarity 격리,
  투수·포수 교체, Career/AI 경제 격리를 공격적으로 검증한다.

### 15.8 예상 충돌과 파일 소유권

공통 충돌 가능성이 큰 아래 파일은 Integration 단계에서 Orchestrator가 소유한다.

```text
ManagerHistoricalRuntimeState.cs
ManagerHistoricalSaveData.cs
ManagerHistoricalSaveAdapter.cs
CardEconomyDefinitions.cs
MatchRosterSnapshot.cs
DetailedMatchEngine*.cs
OwnerHomePresentationModel.cs
OwnerModeUiProfileFactory.cs
GameBootstrap.cs
UI_Scene_NewGame.Title.cs
```

각 Lane은 우선 신규 Core/Simulation 파일로 독립 구현하고, 위 공통 파일 연결은 API Gate 이후 수행한다.
기존 Tactic 연구·인벤토리 Runtime은 아직 없으므로 `TacticLab`은 시설 상태와 효과 계약까지만 만들고,
새 전술 효과 계층을 만들어 연결 완료로 가장하지 않는다.

---

## 16. 2026-09-05 통합 구현 결과

### 16.1 시스템별 Gate

| 시스템 | Core/Simulation | Runtime | Persistence | uGUI | Headless/장기 |
|---|---|---|---|---|---|
| 09 운영·관중·시설 | 완료 | 부분 완료 — `TacticLab` Consumer 없음 | 완료 | 코드 연결 | 경제·관중 완료, TacticLab 소비 검증 없음 |
| 10 분석·프리셋 | 완료 | 완료 | 완료 | 모든 저장 프리셋 현재 검증·선택, 역할 swap, 실제 catalog TeamColor/Tactic 2슬롯 순환 연결. 임의 선수/전체 Inventory 편집 남음 | 완료 |
| 11 Staff | 완료 | 완료 | 완료 | 코드 연결 | 완료 |
| 12 Condition·Chemistry | 완료 | 완료 | 완료 | 코드 연결 | 규칙·분포 완료, 승률 영향 추가 검증 남음 |

Production 연결은 `OwnerModeManager`가 소유하며 `ManagerModeCoordinator`,
`ManagerPregameService`, `ManagerModeMatchService`, `DetailedMatchEngine` 순으로 이어진다. 경기 종료
commit은 Condition/workload, `BatteryUsageReport`의 실제 투수-포수 수비 아웃 Familiarity와 홈
팬·관중·Money를 같은 Runtime에 반영한다. 비주포지션은 단일 EffectiveMatchCondition 경로에서
합성된다. TeamColor 2슬롯도 Production 콘텐츠 ID 검증 뒤 실제 경기 Snapshot에 주입된다. Save
schema 4는 v1~v3 migration과 주간 생산·시설·급여·홈 경기 receipt를 포함한다.

플레이어 구단의 남은 경기가 없으면 한 시즌 종료 transaction이 Staff 미정산 급여를 1회 차감하고
계약 기간·만료 슬롯을 갱신한 뒤 다음 시즌 재무 Summary와 결정론적 일정을 생성한다. 일정 경기 수는
`CareerSeason.RegularSeasonGamesPerTeam`을 따르되 홀수 팀은 기존 Generator 계약에 맞게 하향
정규화한다. 팬·구장·시설·TicketPolicy와 Historical TeamSeason/roster snapshot은 유지한다. 승강과
새 시즌 로스터 이월은 아직 없다. 주간 회복은 플레이어·AI 모든 팀에 단일 Context로 한 번 적용한다.

09~12절의 Condition, 운영·관중·시설, Staff, Scouting Confidence 조정값은
`Assets/10.Datas/Resources/NewGame/OwnerExpansionBalance.json` 한 곳에서 저작한다.
`OwnerExpansionBalanceConfig`는 schema와 필수 섹션을 엄격 검증해
`NewGameDefinition.LoadOwnerModeBalanceTable()`에서만 순수 Balance 계약으로 변환한다. 구단주
Balance는 공통 경기 표에 전용 content hash/version을 합성하지만, 선수 커리어의
`LoadConfiguration()`과 Balance hash/version에는 영향을 주지 않는다.

Staff의 Hitting/Pitching/Development 훈련 효율은 `ManagerModeCoordinator`와 `OwnerModeManager`의
기존 CardTraining 경로에서 한 번만 소비한다. 별도 성장 Tick이나 BaseStat 직접 버프는 없으며
Production E2E가 DP 효율과 TrainingCeiling을 검증한다.

UI는 Repository Audit에서 확인한 Production Guideline에 따라 UI Toolkit이 아니라 기존
`SharedGameShellView` 기반 programmatic uGUI를 재사용했다. 구단 재정·시설, Staff Office, 상대
분석·프리셋, Condition·Chemistry 화면을 같은 Shell에 연결했다. 모든 저장 프리셋은 현재 Validator
상태와 함께 선택할 수 있고, TeamColor/Tactic 각 2슬롯은 실제 catalog 후보를 순환해 원자 적용한다.
Home 외 command 실패는 활성 화면 feedback 영역에 표시하며 Condition은 1~10단계·한글 상태가
중심이다. 기존 배경 에셋으로 요구를 충족해 ImageGen 생성 에셋은 없다.

### 16.2 검증 결과

- 관련 Rule, Determinism, Persistence, Match integration과 Production E2E의 대상별 headless 실행을
  통과했다. 최신 시즌 lifecycle 집중 합계는 52/0(Owner E2E 5, Persistence 6, Staff 16, Match 6,
  Manager Historical Save 19)이다. E2E는 시설 업그레이드, Staff 계약·급여, 주간 생산·회복,
  분석·프리셋·Warning,
  TeamColor 2슬롯, Staff CardTraining 효율, `DetailedMatchEngine`, 실제 Battery 수비 아웃
  Familiarity, 경기 후 상태·관중·수익, Save/Load 중복 방지를 관통한다.
- 별도 Explicit 장기 통계는 **6 passed, 0 failed**다. 표본은 8 Seed × 10시즌 × 홈 72경기,
  총 80시즌·5,760 재무 이벤트이며 Production 경기 경로는 18경기 시즌을 동일 입력으로 두 번 실행해
  총 36회 Match 결과가 같은지 확인했다.
- 대표값은 FanShop 회수 1.06시즌, Premium 최적 12/24(50%), 평균 Staff 급여 비율 6.41%, Elite
  25.74%, Condition 평균 58.71, Intel Confidence 평균 0.364다.
- Core/Simulation/Game 순수 C# compile은 이전 대상 실행에서 통과했다. 최신 Balance/시즌
  lifecycle/UI 병합 전 Unity 대상 실행 결과는 **39 passed, 0 failed, 0 skipped**다. 이 결과는 최신
  병합본 전체의 검증 결과로 간주하지 않는다.
- 시즌 lifecycle 집중 검증은 Owner E2E 5/0, Persistence 6/0, Staff 16/0, Match 6/0, Save 19/0이다.
  신규 프리셋/장착 후보 UI EditMode 테스트는 추가했지만 최신 통합본의 집계 Unity Test Runner는
  사용자 지시에 따라 중단·생략했다. Player Build와 실제 16:9 UI 검증도 같은 지시에 따라 실행하지
  않았다.

### 16.3 남은 Gate

- Unity Editor/Player에서 Owner title 진입, 실제 16:9 시인성, 모든 버튼과 Save 파일 I/O를 다시
  검증한다.
- 역할 swap 밖의 임의 선수 Lineup 수정과 전체 Tactic Inventory 선택 UI를 Production command에 연결한다.
- 기존 Tactic 연구/Inventory가 구현되면 `TacticLab` modifier를 그 Consumer에만 연결한다.
- 승강과 새 시즌 로스터 이월 정책을 구현한다.
- 최신 통합본을 대상으로 독립 Review를 다시 실행해 Critical 0/Major 0을 확인한다.

따라서 전체 판정은 **부분 완료**다. Definition/Resolver 존재를 Production 완료로 혼동하지 않으며,
최신 Unity Test Runner·Player Build·독립 Review와 위 잔여 항목을 통과하기 전에는 §13의 전체 완료
조건을 만족했다고 기록하지 않는다.
