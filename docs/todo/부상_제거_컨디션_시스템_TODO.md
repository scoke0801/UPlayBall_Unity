# 부상 시스템 제거 + 컨디션 시스템 구현 TODO

## 0. 배경

엔트리브소프트 `프로야구 매니저`(세가 `프로야구팀을 만들자! 온라인 2` 국내 이식작, 서비스 종료)의
컨디션 구조를 레퍼런스로 삼아, 현재 프로젝트의 **부상(Injury) 시스템을 제거하고 그 자리를
컨디션(Condition) 시스템으로 대체·연결**한다.

레퍼런스 게임의 부상 시스템은 공개 자료(나무위키 "프로야구 매니저/게임 설명",
"프로야구 매니저/운영 및 각종 사건들", 관련 커뮤니티)로는 확인되지 않았다. 반면 컨디션 시스템은
아래처럼 비교적 상세히 확인됐다. **따라서 이 문서의 "기준 구조"는 컨디션 쪽만 레퍼런스가 있고,
부상 제거는 레퍼런스 근거가 아니라 사용자 지시에 따른 프로젝트 자체 결정이다.**

## 1. 레퍼런스 요약 — 엔트리브 프로야구 매니저 컨디션 구조

- **등급**: 좋은 순서로 빨강 > 주황 > 노랑 > 파랑 > 보라의 5색, 각 색이 상/하로 나뉘어 총 10단계.
- **효과 대상**: 타자는 교타(contact) 수치, 투수는 제구·구속 수치에 적용.
- **효과 범위**: 컨디션에 따라 제구/교타가 **-30% ~ +10%** 범위로 변동(히든 스탯).
- **상위 리그일수록 중요도 상승**: "컨디션이 좋을 경우 좋은 성적을 낼 확률이 매우 높아, 상위
  리그로 갈수록 컨디션 관리의 중요도가 커진다."
- **팀 궁합 연동**: 타선 그래프가 "좋음"이면 컨디션 1단계 상승, "나쁨"이면 1단계 하락. 배터리
  (투수-포수) 그래프가 좋으면 컨디션 1단계 상승.
- **소모/회복 아이템 성격**: 특정 보상으로 "컨디션이 이틀간 빨강(최상)으로 고정"되는 효과가 존재 —
  즉 컨디션은 시간에 따라 자연 변동하고, 이벤트/아이템으로 일시 고정·개선 가능한 자원이었다.
- **부상과의 관계**: 부상은 별도 시스템으로 문서화되어 있지 않다. 레어카드 설정 텍스트에
  "부상 또는 부진으로 시즌을 말아 먹은 경우"라는 서술만 존재 — 세계관 서사이지 메커니즘 설명이
  아니다.

## 2. 현재 프로젝트 현황 (as-is)

### 2.1 부상(Injury) — 제거 대상

- `Assets/02.Scripts/Core/Growth/InjuryState.cs`
  - `InjurySeverity`(Discomfort/Minor/Serious/Major), `InjuryTreatmentChoice`, `InjuryRecord`,
    `InjuryRiskInput`(나이·피로·최근 출장량·훈련강도·기존부상여부·내구도 입력).
- `Assets/02.Scripts/Simulation/Growth/InjuryResolver.cs`
  - 피로·나이·출장량·훈련강도·내구도로 부상 위험을 계산하고(`EvaluateRisk`), 확률 판정 후
    등급별 결장일(경미 0~3일, 경상 5~14일, 중상 21~60일, 심각 90~240일)을 부여.
  - `DecisionExplanation` 기반 설명 가능성(사유 코드·기여도)을 갖춘 구조 — 제거 시 이 설명
    가능성 계약을 컨디션 쪽으로 이관해야 함(§4.3 참고).
- `Assets/02.Scripts/Core/Balance/InjuryBalanceTable.cs`
  - 부상 확률 계수·등급 분포·전문 치료 비용을 담은 밸런스 테이블.
- `Assets/02.Scripts/Core/Growth/PlayerGrowthState.cs`
  - `InjuryHistory`, `RecordInjury(...)`, `SeasonInjuryRiskReduction` 등 부상 이력·회복 보정 상태.
- 참조처(부상 이력을 소비하는 곳, 제거 시 함께 정리):
  - `Assets/02.Scripts/Game/Career/RetirementRecapService.Archive.cs`,
    `RetirementRecapService.SnapshotBuilder.cs`, `RetirementRecapSnapshot.cs` — 은퇴 회고에서
    부상 이력 요약.
  - `Assets/02.Scripts/Game/Career/WorldOffseasonMarketService.cs` — 오프시즌 시장에서 부상 이력
    참조 가능성.
  - `Assets/Tests/EditMode/Simulation/Growth/InjuryResolverTests.cs`.
  - 뉴스/이벤트 계열(`News/Definitions/DefaultNewsTemplateLibrary.cs`,
    `News/Evaluators/CareerSystemNewsEvaluators.cs`) — 부상 관련 뉴스 템플릿 존재 가능성, 확인 필요.

### 2.2 컨디션(Condition) — 이미 존재하지만 부분적으로만 연결됨

- `PlayerGrowthState.Condition`(0~100 정수)이 이미 존재하며 **훈련·성장 게이트**로 쓰이는 중:
  - `Simulation/Growth/GrowthResolver.cs` — 훈련 최소 컨디션 요구치, 컨디션 배율로 성장량 보정.
  - `Simulation/Growth/OffseasonScheduler.cs` — 오프시즌 프로그램의 최소 컨디션 요구치.
  - `Simulation/Growth/ManagerRoleEvaluator.cs` — 기용 판단 가중치 중 하나로 컨디션 반영.
  - `Core/Balance/GrowthBalanceTable.cs`의 `ConditionBalance.GetMultiplier(int condition)` —
    `NormalMinimum` 미만이면 페널티 배율 적용하는 2구간 구조. **레퍼런스의 10단계·±30%~+10%
    구조보다 단순함.**
- `Simulation/Match/MatchRosterSnapshot.cs.Condition`(int)이 **경기 로스터 스냅샷**에도 존재하지만,
  실제로 소비하는 곳은 `Simulation/Match/PitcherFatigueResolver.cs`뿐:
  - `conditionMultiplier = 0.85 + entry.Condition * 0.0015` → 투수의 유효 투구 용량(capacity)에만
    반영.
  - **타자 쪽 컨디션은 경기 시뮬레이션에 전혀 연결돼 있지 않다.** (교타/컨택에 미반영 — 레퍼런스
    구조의 핵심 대칭성이 현재 프로젝트엔 없음.)
  - 투수 쪽도 레퍼런스처럼 제구·구속에 직접 배율로 붙는 게 아니라 "용량(체력)"에만 곱해지는
    간접 경로 — `PitcherFatigueResolver.Resolve`의 `overload` 계산과는 별개.

### 2.3 결론

컨디션 시스템의 "골격"(0~100 정수, 훈련 게이트, 로스터 스냅샷 필드)은 이미 있지만
① 등급/구간이 레퍼런스보다 거칠고, ② 경기 중 타자 스탯에는 전혀 연결되지 않았으며,
③ 부상 시스템과 별개 트랙으로 존재해 서로 상호작용하지 않는다. 이번 작업은 **신규 구축이 아니라
기존 골격을 레퍼런스 구조로 확장하고, 부상 시스템 자리를 대체하도록 재배선하는 작업**이다.

## 3. 설계 결정이 필요한 지점 (구현 착수 전 확정 필요)

부상(기간 결장)과 컨디션(확률적 스탯 보정)은 성격이 다른 리스크다. 부상을 완전히 없애면
"결장으로 시즌이 꼬이는" 굵직한 사건이 사라지고, 모든 리스크가 "오늘 컨디션이 나빠 못 친다"는
단기 변동으로 수렴한다. 이건 게임의 의사결정 무게감에 영향을 주므로, 다음을 먼저 정해야 한다.

1. **결장(며칠 못 뛰는 이벤트) 자체를 완전히 없앨 것인가, 아니면 "컨디션 최하위 지속" 형태로
   약하게 남길 것인가?** — 레퍼런스에는 결장 메커니즘 근거가 없으므로 이 문서는 "완전 제거"를
   기본안으로 하되, 대안(§5.5)을 함께 적어둔다.
2. **컨디션의 자연 변동 규칙**(경기 출전/휴식/이동 등에 따라 매일 어떻게 오르내리는가)을 무엇으로
   삼을 것인가 — 레퍼런스는 "타선/배터리 그래프 궁합"만 확인됐고 일별 변동 곡선은 불명.
   프로젝트 자체 곡선을 설계해야 한다.
3. **훈련 게이트용 컨디션(§2.2, 0~100 연속값)과 경기용 컨디션(10단계 이산값)을 하나의 상태로
   통합할 것인가, 두 스케일을 유지하고 변환 함수만 둘 것인가.**

## 4. 목표 구조 (to-be)

### 4.1 컨디션 등급 — `Baseball.Core.Growth`

- `ConditionGrade` enum 10단계 (예: `Purple`, `BlueLow`, `BlueHigh`, `YellowLow`, `YellowHigh`,
  `OrangeLow`, `OrangeHigh`, `RedLow`, `RedHigh`, 그리고 최상위 별도값이 필요하면 조정).
  - 명명은 프로젝트 컨벤션(야구 표준 용어·기능 이름)에 맞춰 색상 대신 의미 기반으로 검토
    (예: `Poor`, `BelowAverage`, `Average`, `AboveAverage`, `Peak`를 각 2단계로) — CLAUDE.md의
    "자체 조어를 만들지 않는다" 원칙과 색상 은유 사이에서 팀 결정 필요.
- 기존 `PlayerGrowthState.Condition`(0~100 연속값)을 **소스 오브 트루스**로 유지하고,
  `ConditionGrade`는 이 값을 10구간으로 매핑한 파생값으로 둔다(§3-3 결정에 따름).

### 4.2 경기 스탯 보정 — `Baseball.Simulation.Match`

- 신규 `ConditionEffectResolver`(가칭): 등급 → 배율(`-30% ~ +10%`, 레퍼런스 범위를 초기값으로
  채택하고 `MatchBalanceTable`에 계수로 뺀다).
  - 타자: 컨택/교타 관련 유효 능력치에 배율 적용.
  - 투수: 제구·구속(Control·Velocity)에 배율 적용 — 기존 `PitcherFatigueResolver`의 "용량"
    경로와는 분리된, 명시적인 컨디션 배율 항을 `Resolve(...)`에 추가.
- `MatchRosterSnapshot.Condition`을 타자 로직에도 실제로 연결(현재 미사용 → 사용).

### 4.3 컨디션 변동 — `Baseball.Simulation.Growth` / `Baseball.Simulation.Match`

- 팀 궁합 연동: 기존 타선/배터리 시너지 평가 로직(있다면 재사용, 없다면 최소 스펙으로 신설)의
  결과가 "좋음/보통/나쁨"일 때 컨디션 등급 ±1단계.
- 시즌 중 자연 변동 곡선(§3-2에서 결정된 규칙)을 `ConditionResolver`(가칭)로 구현하고,
  `InjuryResolver`가 갖고 있던 `DecisionExplanation` 기반 설명 가능성 계약(사유 코드, 기여도,
  권장 액션)을 그대로 승계한다 — 부상 제거로 "왜 이렇게 됐는지 설명 가능한 리스크 시스템"이라는
  속성 자체를 잃지 않도록 한다.

### 4.4 밸런스 테이블

- `InjuryBalanceTable`을 제거하고 `ConditionBalanceTable`(가칭)을 신설하거나, 기존
  `GrowthBalanceTable.ConditionBalance`를 10단계·타/투 배율 구조로 확장한다(중복 테이블을
  만들지 않도록 우선 기존 `ConditionBalance` 확장을 먼저 검토).

## 5. 작업 순서 (체크리스트)

1. [ ] §3 설계 결정 3가지 확정 (문서화하고 이 TODO에 반영)
2. [ ] `ConditionGrade`·10단계 매핑 함수 설계 및 `Core.Growth`에 추가
3. [ ] `MatchBalanceTable`에 컨디션 배율 계수 추가 (`-30%~+10%` 범위를 초기값으로)
4. [ ] `PitcherFatigueResolver.Resolve`에 컨디션 배율 항 추가 (기존 용량 경로와 별개로)
5. [ ] 타자 컨택 계산 경로 파악 후 `MatchRosterSnapshot.Condition` 실제 연결
6. [ ] 팀 궁합 → 컨디션 ±1단계 연동 로직 구현
7. [ ] 컨디션 자연 변동(`ConditionResolver`) 구현, `DecisionExplanation` 계약 승계
8. [ ] `InjuryResolver`/`InjuryState`/`InjuryBalanceTable`/`InjuryResolverTests` 제거
9. [ ] `PlayerGrowthState`에서 `InjuryHistory`/`RecordInjury`/`SeasonInjuryRiskReduction` 제거,
   대체 상태(컨디션 이력 등 필요 시) 추가
10. [ ] 부상 이력을 참조하던 은퇴 회고·오프시즌 시장·뉴스 템플릿 정리 또는 컨디션 기반으로 대체
11. [ ] EditMode 테스트 갱신: 부상 테스트 제거, 컨디션 배율·등급 전이·팀 궁합 연동 테스트 신설
12. [ ] 대량 시뮬레이션으로 컨디션 도입 전/후 리그 타율·ERA·득점 분포 비교, 밸런스 계수 보고
   (`Balance_Testing_Guidelines_UPlayBall.md` 절차 준수)

## 6. 참고

- 레퍼런스 조사 출처: 나무위키 "프로야구 매니저/게임 설명", "프로야구 매니저",
  "프로야구 매니저/운영 및 각종 사건들" 문서 (조사일 2026-08-31). 부상 시스템 관련 공개 자료는
  찾지 못했음 — §0 참고.
- 관련 프로젝트 지침: `docs/지침/Simulation_Architecture_Guidelines_UPlayBall.md`(시뮬레이션/표현
  분리, 결정론), `docs/지침/Balance_Testing_Guidelines_UPlayBall.md`(밸런스 계수 변경 시 대량
  시뮬레이션 근거 의무).
