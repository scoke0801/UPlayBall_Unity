# 02. 가상 구단, 승강 리그, Club DNA

## 0. 목적

01절에서 Offline Bake한 가상 선수들을 "가상 연도 구단"으로 묶고, 기존 `LeagueGrade` 승강 구조
위에서 자연스럽게 실력을 증명하게 만든다. 실제 KBO 구단·순위를 그대로 대응시키지 않는다.

이 문서는 동시에 **공통 1군 25인 구성, 투수 역할, 외국인 등록, 포지션 불일치 기용 규칙**의
Source of Truth 역할을 한다. 선수 커리어 모드와 감독모드는 동일한 규칙을 사용한다.

## 1. TeamSeason — 핵심 단위

```text
FranchiseId    // 가상 구단 고유 ID (예: SEOUL_COMETS)
OriginYear     // 참조한 시대 (예: 2011) — 실제 그 해 그 팀을 의미하지 않음
TeamSeasonKey  = FranchiseId + "_" + OriginYear
```

`FranchiseId`는 완전 가상 이름/엠블럼/컬러를 사용한다. `OriginYear`는 Reference의 시대 축이며
실제 그 해 특정 KBO 구단의 성적을 뜻하지 않는다.

## 2. Franchise Fingerprint 생성

한 TeamSeason의 기본 성향은 같은 OriginYear의 여러 실제 팀 시즌 특성을 혼합해 Offline에서 만든다.
연도덱이 수집의 핵심 단위이므로 `2011 TeamSeason`은 원칙적으로 2011 데이터 안에서 혼합한다.

```text
Fingerprint = 30% 2011 강한 장타 집단
            + 30% 2011 강한 선발 집단
            + 20% 2011 평균 수비 집단
            + 20% 2011 강한 불펜 집단
```

필요한 포지션/역할 Reference가 부족할 때만 ±1~2년 Era Pool을 보조로 사용하며 최대 혼합 비율은
`EraNormalizationConfig`에 데이터화한다. 서로 멀리 떨어진 시대를 임의로 섞지 않는다.

Fingerprint와 선수의 연도별 원소속 배정은 **Offline Bake 단계에서 확정**한다. Runtime 월드 생성 시
Core25나 OriginFranchise를 다시 추첨하지 않는다.

## 3. TeamSeasonDefinition과 Core25

```text
TeamSeasonDefinition
{
    TeamSeasonKey
    AllNormalCardIds[]   // 시즌 전체 Baked 선수풀, 권장 28~40명
    Core25CardIds[]      // Baked 초기 1군, 정확히 25명
    ReferenceStrength    // Fingerprint 기반 원본 전력 점수, 검증용
}
```

`Core25`는 해당 TeamSeason의 **Baked 초기 1군 기록**이다. 게임 시작 후 실제 1군은
`CurrentRosterState`로 관리하며 트레이드·영입·방출·기용 변화가 가능하다. Core25 자체는 변경하지 않는다.

`SimulatedHistory`와 `OriginalHistory` 모두 같은 TeamSeasonDefinition/Core25를 사용한다.
월드 기록 방식이 선수의 원소속이나 초기 Baked 로스터를 바꾸지 않는다.

## 4. ActiveRosterCompositionRule — 공통 25인 규칙

선수모드/감독모드/AI/Core25 모두 같은 공통 규칙을 사용한다.

```text
ActiveRosterSize = 25

Hitters = 14
  StartingHitters = 9
  BenchHitters    = 5

Pitchers = 11
  StartingPitchers = 5
  BullpenPitchers  = 4
  SetupPitchers    = 1
  CloserPitchers   = 1

MaxForeignPlayers = 3
```

주전 타선의 경기 슬롯은 다음 9개다.

```text
C / 1B / 2B / 3B / SS / LF / CF / RF / DH
```

### 4.1 강제 검증 항목

`ActiveRosterValidator` 또는 동등한 공통 Resolver는 최소 다음을 검증한다.

```text
Total == 25
Hitters == 14
Pitchers == 11
StartingHitters == 9
BenchHitters == 5
StartingPitchers == 5
BullpenPitchers == 4
SetupPitchers == 1
CloserPitchers == 1
ForeignPlayers <= 3
Duplicate PlayerPersonId == false
```

이 수치를 UI, AI, Simulation에 각각 별도로 하드코딩하지 않는다.

### 4.2 외국인 선수

외국인 선수는 **ActiveRoster 25인에 최대 3명**까지 등록할 수 있다.

- 3명 모두 동시에 타선/선발/불펜에 기용 가능.
- 4명째 외국인 선수는 보유할 수 있지만 ActiveRoster에는 등록할 수 없다.
- 감독모드 카드 Collection의 외국인 카드 보유 수에는 제한을 두지 않는다.
- Scout Pool에서도 보유 제한을 이유로 외국인 카드를 제거하지 않는다.
- AI 구단과 선수 커리어 모드 구단에도 동일한 ActiveRoster 제한을 적용한다.

외국인 판정에 필요한 필드는 기존 Player 데이터 모델에 이미 존재하면 재사용한다. 없다면 03절의
공통 Baked Player Metadata에 `RegistrationType(Domestic|Foreign)`과 같은 최소 필드를 둔다.
국적별 생성 비율/영입 성향은 이 문서에서 새로 확정하지 않는다.

## 5. 불펜 4명 역할 — BullpenUsagePolicy

일반 불펜 4명은 단순 동일 Reliever 슬롯이 아니라 운영 우선순위를 가진다.

```text
Bullpen 1 / Bullpen 2
  필승조 + 추격조
  접전 리드 보호와 접전 열세 추격 상황의 우선 후보

Bullpen 3
  애매한 상황 + 추격조
  승패가 아직 크게 기울지 않은 중간 레버리지 또는 추격 상황의 우선 후보

Bullpen 4
  패전조 + 비상 대체
  크게 뒤지는 상황의 이닝 소화 우선
  Bullpen 1~3의 체력이 부족하거나 사용 불가일 때 대체 후보
```

정확한 이닝, 점수차, 레버리지, 최소 체력 임계값은 `BullpenUsagePolicy`/BalanceTable로 데이터화한다.
코드에 `if runDiff == ...` 같은 고정 분기를 카드/선수별로 흩뿌리지 않는다.

셋업/마무리는 별도 역할을 유지한다. 감독 AI는 경기 상황, 잔여 체력, 연투 상태, 상대 타선과
`ManagerTacticProfile`을 함께 고려해 투수를 선택한다(06절).

## 6. 포지션 배치 규칙 — 비주포지션은 허용, 패널티 적용

실제 등록 포지션과 다른 포지션에 선수를 배치하는 것을 **하드 금지하지 않는다.**
기용 자유도를 주되 불이익을 Simulation에서 명확하게 반영한다.

### 6.1 야수

야수가 자신의 본래/적격 수비 포지션이 아닌 수비 슬롯에 출전할 수 있다.

```text
OffPosition Hitter
  → Fielding Error Probability 증가
  → Condition 하락
```

정확한 수치는 `OffPositionPenaltyTable` 등 Balance 데이터로 관리한다. 포지션 간 거리/난이도까지
세분화할지는 기존 포지션 자격 시스템을 먼저 조사한 뒤 결정하며, 이 문서에서 임의 수치를 만들지 않는다.

### 6.2 DH 예외

**DH는 어떤 포지션의 타자라도 문제없이 배치할 수 있다.**
DH 배치는 수비 포지션 불일치로 보지 않으며 OffPosition 수비 실책 패널티를 적용하지 않는다.

### 6.3 투수

투수는 자신의 Baked `PitcherRole`과 다른 투수 역할로 기용할 수 있다.

예:

```text
Starter → Bullpen/Setup/Closer
Bullpen → Starter/Setup/Closer
Setup → 일반 Bullpen
Closer → 일반 Bullpen
```

이 경우 기용 자체는 허용하지만 **Condition이 하락**한다. 정확한 감소량은 데이터화한다.

투수를 실제 야수 수비 슬롯에 기용하는 특수 상황을 기존 경기 엔진이 지원한다면 야수와 동일한
수비 포지션 불일치 규칙도 추가 적용한다. 지원하지 않는 기능을 이 문서 때문에 새로 만들 필요는 없다.

### 6.4 PositionAssignmentRule의 성격

포지션 불일치는 Validator가 로스터/교체를 거부하는 오류가 아니라 **경고 + 경기 비용**이다.

```text
Roster Composition Validation → 통과/실패
Position Assignment Evaluation → 적합/비적합 + Penalty
```

UI는 비주포지션 배치를 막지 않고 예상 컨디션/수비 리스크를 표시할 수 있어야 한다.

## 7. 벤치 교체와 감독 판단

감독은 경기 상황에 따라 벤치 5명을 주전 9명과 교체할 수 있다.

가능한 판단 요소:

- 대타/대주자
- 컨디션 저하
- 상대 투수 손잡이
- 수비 강화 또는 공격 강화
- 부상/피로
- 경기 후반 상황

다만 교체 후 수비 포지션을 반드시 다시 평가한다. 비주포지션 배치는 허용되므로 교체를 무조건
거부하지 않지만, `OffPositionPenalty`를 포함한 기대 이득/손실을 감독 AI가 비교해야 한다.

선수 커리어 모드에서는 감독 AI가 이 판단을 소유한다. 감독모드에서는 플레이어의 직접 교체 입력과
자동 운영 AI 모두 같은 PositionAssignmentRule을 사용한다(06절).

## 8. Rookie → Galaxy 승강

기존 `LeagueGrade`를 다음 구조로 확장한다.

```text
Rookie → Minor → Major → World → All-Star → Classic → Winners → Champion → Master → Galaxy
```

- 모든 TeamSeason은 Rookie에서 시작한다.
- `ReferenceStrength`가 높다고 시작 리그를 올리지 않는다.
- 승강 구간은 `LeagueDefinition` 데이터로 관리한다.
- 리그 등급 자체가 능력치에 직접 배율을 곱하지 않는다.
- 상위 리그 보상은 시설 상한, 계약 오퍼, 감독 명성 등 경제/운영 축으로 준다.

### 8.1 LeagueInstance — 정규 Franchise 구단은 정확히 10개

한 `LeagueInstance`의 **정규 Franchise 구단 슬롯은 정확히 10개**로 고정한다.

```text
RegularFranchiseTeamCount = 10
```

정규 구단은 Offline Bake된 `TeamSeasonDefinition`을 사용한다. `WorldRecordMode`나
`WorldHistorySeed`가 정규 구단의 원소속/선수풀을 재배치하지 않는다.

특수 합성팀은 아래 §8.2처럼 별도 참가팀으로 추가하며, `RegularFranchiseTeamCount = 10`에
포함하지 않는다.

### 8.2 연도별 특수 합성팀 3종

World의 초기 기록/수상이 확정된 뒤, 같은 `OriginYear`의 정규 구단 선수풀 전체를 합산하여
다음 세 팀을 생성한다.

```text
SpecialCompositeTeamType
  AllStarComposite
  GoldenGloveComposite
  YearSelectComposite
```

- `AllStarComposite`: 해당 World/연도의 `WorldAwardRecord(AllStar)` 선수를 우선 후보로 사용한다.
- `GoldenGloveComposite`: 해당 World/연도의 `WorldAwardRecord(GoldenGlove)` 선수를 우선 후보로 사용한다.
- `YearSelectComposite`: 같은 OriginYear 전체 선수풀에서 결정론적 RNG로 선발한다.
- 각 팀의 최종 25인은 §4 `ActiveRosterCompositionRule`을 모두 만족해야 한다.
- Award 핵심 후보만으로 역할별 25인을 완성할 수 없으면 같은 연도의 남은 적격 선수로 보충한다.

세 특수 합성팀의 **최종 25인 사이에는 동일 `PlayerSeasonId` 중복을 허용하지 않는다.**
중복 해결 기본 우선순위는 다음과 같다.

```text
AllStarComposite
→ GoldenGloveComposite
→ YearSelectComposite
```

앞 팀에 이미 배정된 `PlayerSeasonId`는 뒤 팀 후보에서 제외하고 다음 적격 후보로 채운다.
병렬 처리 순서나 컬렉션 열거 순서에 따라 결과가 달라지지 않도록 Stable Sort + 결정론 RNG를 사용한다.

이 중복 금지는 세 특수 합성팀 상호 간에만 적용한다. 특수팀 선수가 원래 Baked
`TeamSeasonDefinition`의 소속 로스터에도 존재하는 것은 허용한다. 특수팀 편성은 선수 이동/
트레이드가 아니며 다음 원본 값은 절대 바꾸지 않는다.

```text
OriginYear
OriginFranchiseId
OriginTeamSeasonKey
PlayerSeasonId
```

따라서 특수팀에서도 TeamColor 자격은 카드 자신의 원래 Origin/Edition을 사용한다.

### 8.3 최초 기록 시뮬레이션에서는 특수 합성팀 제외

`SimulatedHistory`에서 게임 시작 전 과거 기록을 처음 생성할 때는 **원래 정규 Franchise
구단만** 시뮬레이션한다. `AllStarComposite`/`GoldenGloveComposite`/`YearSelectComposite`는
통계·순위·수상 계산에 포함하지 않는다.

```text
Baked Regular TeamSeason
→ Historical Season Simulation
→ Statistics / Standings / Postseason
→ All-Star / Golden Glove / MVP
→ WorldHistorySnapshot
→ SpecialCompositeTeamBuilder
→ 특수 합성팀 3종 리그 배치
```

이 순서를 고정해 합성팀이 자신을 생성하는 Award에 영향을 주는 순환 의존을 차단한다.

`OriginalHistory`에서는 Historical Simulation을 실행하지 않지만 순서는 동일한 의미를 가진다.

```text
Runtime-safe Original Records
→ WorldHistorySnapshot / WorldAwardRecord
→ SpecialCompositeTeamBuilder
→ 특수 합성팀 3종 리그 배치
```

특수팀 생성 이후의 실제 게임 리그 진행에서는 해당 리그 참가팀으로 취급할 수 있다.
단 과거 초기 기록을 소급 재계산하지 않는다.

## 9. Club DNA — 두 개의 소유 단위

```text
FranchiseIdentityProfile
{
    Contact, Power, Running, Defense,
    Rotation, Bullpen, Development, Experience
}

TeamSeasonClubState
{
    TeamSeasonKey
    Contact, Power, Running, Defense,
    Rotation, Bullpen, Development, Experience
}
```

- `TeamSeasonClubState`: 개별 TeamSeason 소유. 매 시즌 갱신되는 운영 DNA.
- 초기값은 Offline Fingerprint에서 파생한다.
- 갱신 예: `기존 × 0.6 + 최근 3년 성적 × 0.25 + 감독 철학 × 0.15`, 시즌당 변화폭 최대 ±5.
- `FranchiseIdentityProfile`: 여러 TeamSeasonClubState의 장기 평균. 브랜드 서술/장기 성향용.
- AI 영입/트레이드/기용은 자신의 `TeamSeasonClubState`를 참조한다.
- TeamColor 자격은 DNA 값이 아니라 Origin/Edition만 참조한다.

## 10. Signature 후보 (Golden Generation / Dynasty)

TeamColor와 별개인 World History 이정표다. 능력치 보너스를 주지 않는다.

```text
Golden Generation 예:
  구단 육성 선수 7명 이상 + 3시즌 이상 함께 활동 + 포스트시즌 진출

Dynasty 예:
  정규리그 1위 또는 우승 + 특정 시즌 지표 상위권
```

실제 World Simulation 결과로만 판정해 기록실/뉴스/업적에 남긴다. 사전 Baked 능력치만 보고
미리 부여하지 않는다.

## 11. 데이터 모델 배치

```text
Baseball.Core
  TeamSeasonDefinition
  ActiveRosterCompositionRule
  BullpenUsagePolicy / Balance Definition
  PositionAssignmentRule / OffPositionPenaltyDefinition
  LeagueInstanceDefinition
  SpecialCompositeTeamDefinition
  SpecialCompositeTeamType
  CompositeTeamOverlapPolicy
  FranchiseIdentityProfile
  TeamSeasonClubState
  ClubLegacyDefinition

Baseball.Simulation
  ActiveRosterValidator
  PositionAssignmentPenaltyResolver
  SpecialCompositeTeamBuilder
  TeamSeasonClubStateResolver
  FranchiseIdentityResolver
  ClubLegacyResolver
  LeagueGrade Promotion/Relegation Resolver
```

`SyntheticTeamGenerator`는 01절에 따라 Offline Pipeline 책임이다. Runtime Simulation 목록에 넣지 않는다.

## 12. 검증 기준

- 모든 Baked Core25가 정확히 25명이며 야수14/투수11, 주전9/벤치5, 선발5/불펜4/셋업1/마무리1을 만족하는지 검증.
- ActiveRoster 외국인 3명은 허용하고 4명은 거부하는지 검증.
- 감독모드 Collection에 외국인 카드가 4장 이상 있어도 보유 자체는 허용되는지 검증.
- 동일 ActiveRoster에 같은 `PlayerPersonId`가 중복 등록되지 않는지 검증.
- 비주포지션 야수 기용이 거부되지 않고 실책 확률 증가+Condition 하락으로 연결되는지 검증.
- 모든 타자 포지션이 DH에 배치될 때 OffPosition 수비 패널티가 발생하지 않는지 검증.
- 비본래 PitcherRole 기용이 허용되고 Condition 패널티가 발생하는지 검증.
- 감독 AI의 벤치 교체가 포지션 불일치 비용을 고려하며 비주포지션을 무조건 금지하지 않는지 검증.
- Bullpen1/2, Bullpen3, Bullpen4의 후보 우선순위가 데이터화된 `BullpenUsagePolicy`에 따라 달라지는지 검증.
- Bullpen4가 패전 상황 또는 상위 불펜 체력 부족 시 대체 후보로 선택될 수 있는지 검증.
- 모든 `LeagueInstance`의 정규 Franchise 구단 슬롯이 정확히 10개인지 검증.
- 같은 OriginYear의 특수 합성팀이 AllStar/GoldenGlove/YearSelect 각 1팀씩 생성되는지 검증.
- 세 특수 합성팀의 최종 로스터 사이에 동일 `PlayerSeasonId`가 중복되지 않는지 검증.
- 특수 합성팀 편성이 원래 TeamSeason의 Core25/Origin을 변경하지 않는지 검증.
- `SimulatedHistory` 최초 Historical Simulation 참가팀에 특수 합성팀이 0개인지 검증.
- Award 확정 전에 `SpecialCompositeTeamBuilder`가 실행되지 않는지 순서/의존성 테스트.
- `OriginalHistory`에서도 WorldAwardRecord 로드 후 동일 Builder 경로로 특수 합성팀을 만드는지 검증.
- 10,000 TeamSeason 승강 Simulation에서 `ReferenceStrength`와 승격 속도에 통계적 상관이 있으나 강제 승격은 없는지 확인.
- `TeamSeasonClubState` 변화폭이 시즌당 ±5를 넘지 않는지 검증.
- 같은 Franchise의 서로 다른 TeamSeason DNA가 서로 간섭하지 않는지 검증.
- 동일 Runtime Seed에서 승강/DNA/AI 로스터 판단 결과가 재현되는지 검증.

## 13. 2026-09-02 구현 현황

### 완료된 계약과 집중 검증

- `TeamSeasonDefinition`, `CurrentRosterState`, `ActiveRosterCompositionRule`,
  `ActiveRosterValidator`를 공통 Core/Simulation 계약으로 구현했다.
- 25명, 야수 14/투수 11, 선발 5/불펜 4/Setup 1/Closer 1, Foreign 최대 3명,
  `PlayerPersonId` 중복 금지를 테스트했다.
- `PositionAssignmentPenaltyResolver`가 비주포지션과 비본래 `PitcherRole`을 허용하면서 비용을
  반환하고, DH는 무패널티로 처리하도록 구현·테스트했다.
- `BullpenUsagePolicy`/Resolver에 Bullpen 1~4의 상황별 우선순위와 상위 불펜 소진 시
  Bullpen 4 대체를 구현했다.
- `LeagueGrade`, 정규 Franchise 10구단 계약, 승강 Resolver, TeamSeason/Franchise DNA의 독립성과
  시즌당 변화폭 제한을 구현·테스트했다.
- `DetailedMatchEngine`의 Assignment/수비 실책/불펜 후보 경로가 위 공통 Resolver를 소비하는
  집중 통합 테스트를 통과했다.

### 부분 완료 또는 미완료

- `SpecialCompositeTeamBuilder`의 세 팀 구성, 우선순위, 상호 `PlayerSeasonId` 중복 제거와 원본
  불변성은 테스트했다. 그러나 원 Franchise와 합성팀에 같은 시즌 선수를 동시에 출전시키는
  별도 `PlayerSeasonInstance` Runtime 소유 구조는 아직 없다.
- 벤치 교체와 전체 감독 AI가 Position/Bullpen 비용을 장기 시즌에서 최적화하는 경로는 미완료다.
- 10,000 TeamSeason 승강 상관 검증은 실행하지 않았다.
- Golden Generation/Dynasty Resolver와 기록실 연결은 미완료다.

따라서 Phase 2는 **규칙/Resolver/집중 Match 통합은 구현, 실제 리그 장기 진행과 Instance 경계는
부분 완료**로 기록한다.
