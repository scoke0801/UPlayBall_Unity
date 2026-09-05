# 03. Canonical 선수풀, World Identity, Cost와 Edition

## 0. 목적

선수의 고정 Canonical 데이터, World별 표시 Identity, World Award에 따라 활성화되는 Card Edition을
서로 다른 수명 주기로 관리한다.

## 1. Canonical 데이터 모델

```text
PlayerPersonDefinition
  PlayerPersonId
  BirthYear
  Bats / Throws
  PrimaryPosition
  RegistrationType
  CareerSpan

PlayerSeasonDefinition
  PlayerSeasonId
  PlayerPersonId
  OriginYear
  OriginFranchiseId
  OriginTeamSeasonKey
  Position / PitcherRole
  BaseAttributes
  Cost
  TrainingCeiling
```

`PlayerPersonDefinition`에는 Runtime 표시용 `FictionalName`을 저장하지 않는다. 같은 Source Person의
모든 시즌은 같은 `PlayerPersonId`를 공유하고, 각 시즌은 자기 Source PlayerSeason의 기록에서만
BaseAttributes를 얻는다.

`PlayerSeasonDefinition`은 읽기 전용 Canonical Definition이다. World 성적, 수상, 훈련 진행,
계약·소유 상태를 넣지 않는다.

## 2. World Identity Registry

표시 이름은 World State가 소유한다.

```text
WorldIdentityRegistry
  IdentityGeneratorVersion
  IdentitySeed
  WorldPlayerIdentity[]
  WorldFranchiseIdentity[]

WorldPlayerIdentity
  PlayerPersonId
  DisplayName

WorldFranchiseIdentity
  FranchiseId
  DisplayName
```

Player 이름의 Mapping Key는 `PlayerPersonId`다. 한 World에서 동일 Person의 여러 시즌은 같은 이름을
표시한다. 다른 Person끼리는 이름을 공유하지 않는다.

이름 생성기는 다음을 강제한다.

- Domestic/Foreign은 신뢰 가능한 `RegistrationType` 범위에서 분리
- 실제 Source 선수명과 구단명 exact match Reject
- World 내부 Player/Franchise 각각 uniqueness
- null/empty, 숫자, 제어문자, 비정상 길이, 금칙어 Reject
- 충분한 데이터 기반 이름 공간과 결정론적 후보 선택

확정된 Registry 자체를 Save한다. Load에서 IdentitySeed로 다시 생성하지 않는다. Generator Version은
새 World 생성 규칙의 추적용이지 기존 Save 이름을 바꾸는 migration 명분이 아니다.

DisplayName은 Presentation Identity다. TeamColor, Scout, AI, Award, Statistics, Contract, Trade,
Roster, SpecialCompositeTeam은 Stable ID를 사용한다.

## 3. Cost와 TrainingCeiling

Cost는 Source PlayerSeason에서 결정론적으로 변환한 BaseAttributes가 확정된 뒤 Offline에서 계산한다.
역할별 종합 능력치에 고정 Cost 1~10 구간을 적용한다. 백분위·시즌 출전량은 진단용이며,
능력치의 신뢰도 보정 뒤 가격에서 출전량을 다시 할인하지 않는다. 비교 지표·소표본 사전값·
시즌 길이 보정과 구간은 `Tools/KBOImporter/derivation_balance.json`으로 버전 관리한다.

다음은 Cost를 변경하지 않는다.

```text
World Seed
Historical Simulation 결과
Award 결과
현재 시즌 성적
World Player/Franchise 이름
Card Edition
```

`TrainingCeiling`도 Source와 Versioned Balance에 의해 Bake되는 고정값이다. 구단주 모드 카드 훈련으로
오르는 현재 값은 별도 `CardTrainingState`가 소유하며 Definition을 수정하지 않는다.
현행 훈련 여유는 Cost에 무관하게 모든 능력치 +3(상한 99)이다. 저Cost 추가 성장 보너스는 없다.

## 4. Card 구조

```text
CardId = PlayerSeasonId + Edition

Edition
  Normal
  AllStar
  GoldenGlove
  Mvp
```

모든 `PlayerSeasonDefinition`은 Normal 자격을 가진다. 특수 Edition은 현재 World의
`WorldAwardRecord`에 해당 `PlayerSeasonId`가 있을 때만 `WorldCardCatalog`에서 활성화한다.

```text
Fixed PlayerSeasonDefinition
+ WorldAwardRecord
→ WorldCardCatalog
```

같은 PlayerSeason이라도 World A에는 MVP Edition이 있고 World B에는 없을 수 있다. Edition 활성화가
달라도 Cost, BaseAttributes, TrainingCeiling, Origin은 같아야 한다.

## 5. Award 기반 Edition

특수 Edition의 유일한 Production Source는 Historical Simulation Statistics에서 계산된
`WorldAwardRecord`다.

- `AllStar`: `AllStarSelectionResolver` 선정 결과
- `GoldenGlove`: `GoldenGloveAwardResolver` 결과
- `Mvp`: Regular Season, All-Star Game, Postseason MVP 규칙에서 자격 부여

Source 실제 수상 기록이나 BaseAttributes 순위를 곧바로 Edition 자격으로 사용하지 않는다.
`OriginalHistory` 기반 Edition 변환은 Legacy/Debug/Validation 전용이며 정식 New Game Catalog 생성에
사용하지 않는다.

Edition별 Ability Bonus가 존재해도 원본 `PlayerSeasonDefinition.BaseAttributes`를 변경하지 않는다.
경기 입력의 Effective Rating을 조립할 때 명시적으로 적용한다.

## 6. Origin과 Edition

모든 Edition은 같은 PlayerSeason의 다음 Origin을 공유한다.

```text
OriginYear
OriginFranchiseId
OriginTeamSeasonKey
```

AllStar/GG/MVP를 수상했다고 Origin이 특수 합성팀으로 바뀌지 않는다. TeamColor와 구단 연도덱 판정은
표시 이름이나 특수팀 소속이 아니라 Canonical Origin과 Edition을 사용한다.

## 7. Person 중복과 경기 인스턴스

같은 `PlayerPersonId`의 서로 다른 연도 Card를 한 ActiveRoster가 동시에 보유할 수 있는지는 게임
모드의 컬렉션 규칙이 결정하지만, 실제 경기 라인업에는 동일 Person을 중복 출전시키지 않는다.
판정은 이름이 아니라 `PlayerPersonId`로 한다.

특수 합성팀은 기존 `PlayerSeasonId`를 참조한다. 원 Franchise와 특수팀에 같은 PlayerSeason 참조가
존재할 수 있지만, 세 특수 합성팀의 최종 로스터 사이에는 같은 PlayerSeason이 중복될 수 없다.
이는 선수 합성이나 Canonical Definition 복제가 아니다.

## 8. 모드별 State

공통으로 사용하는 것:

- Canonical Person/Season/TeamSeason Definition
- WorldIdentityRegistry
- WorldHistorySnapshot / WorldAwardRecord
- WorldCardCatalog
- Match/Season Simulation

구단주 모드 플레이어 구단이 별도로 소유하는 것:

- Owned Card Collection
- CardTrainingState
- Duplication/Enhancement/Sale 상태
- Scout Pity와 경제 원장

AI 구단과 선수 커리어 모드에 구단주 모드 Owned State를 유출하지 않는다.

## 9. Save / Load

Save는 Definition 전체를 복제하지 않고 Stable ID와 Content Reference, World별 확정 상태를 기록한다.

```text
WorldIdentityRegistry
WorldHistorySnapshot
WorldAwardRecord
Historical Statistics / Standings
WorldCardCatalog 활성 자격 또는 재구성 가능한 Award 참조
Special Composite Team State
모드별 Owned/Progress State
```

Load에서는 저장된 DisplayName, Statistics, Awards를 그대로 복원한다. Name Generator, Historical
Simulation, Award Resolver를 다시 호출하지 않는다.

## 10. 필수 검증

- Canonical Person에 World-independent DisplayName 필드가 없음
- 한 `PlayerSeasonId`의 Cost/BaseAttributes/TrainingCeiling/Origin이 모든 World와 Edition에서 동일
- Award가 다른 Seed의 WorldCardCatalog 특수 Edition 구성이 달라질 수 있음
- Award 없이 특수 Edition 활성화 0건
- Source Award/BaseAttributes-only Edition 경로가 Production에서 호출되지 않음
- 같은 Person 다년도 DisplayName 일치와 다른 Person 이름 중복 0건
- Reference 선수 이름 exact match 0건과 이름 품질 규칙 통과
- DisplayName을 바꿔도 CardId/TeamColor/Scout/Match 결과 불변
- Save/Load 전후 Identity, WorldCardCatalog, Statistics, Award 일치
- Load 중 Identity/Historical/Award 생성기 호출 0회

## 11. 프야매 Reference 연구 상태

3절의 Cost 구현은 v8이다. `BaseballManager_PROJECT.md` 42.8에 따라 Source 성과 quality,
출전량, 수비 기회, 역할 내 상대가치를 별도 구성 요소로 계산한 뒤 ordinal 구간과 elite 자격으로
1~10을 확정한다. PMReference628개 관측 중 후기 Normal·Source 연결 확인 후보는1개이므로 원작
최종판 Calibration 완료로 해석하지 않는다. subtype 미확인 초기 시뮬레이터 화면도 Normal 최종판과
혼합하지 않는다.

ReferenceCost는 `Tools/PMReference` 연구/검증 전용이며 Runtime Definition 필드가 아니다. 2012 SK를
맞추기 위한 개별 보정·World 결과 환류·Edition별 가격 변경은 금지한다. 자료·수집 실패·전사 충돌·
변경 전/후 전체 분포와 남은 불확실성은 `Tools/PMReference/reports/PM_REFERENCE_RESEARCH.md`에 기록한다.
