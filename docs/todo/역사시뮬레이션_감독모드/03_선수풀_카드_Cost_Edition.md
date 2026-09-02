# 03. 선수풀 데이터 모델 — Cost와 Edition

## 0. 목적

이 문서의 `[Reference]`/`[ProjectOriginal]` 표기는 04절 §0.1 규칙을 따른다.

`PlayerPersonDefinition`/`PlayerSeasonDefinition`/카드 원본은 **선수 커리어 모드와 감독모드가 공통으로 사용하는 Baked 콘텐츠**다. 감독모드만 이 공통 카드를 수집·강화·판매하는 `OwnedPlayerCardState`를 추가로 가진다.

Edition은 정확히 4종만 사용한다.

```text
Normal
AllStar
GoldenGlove
Mvp
```

Legend/Classic/Rare 등의 추가 선수 카드 등급은 만들지 않는다.

## 1. 공통 데이터 모델

```text
PlayerPersonDefinition
  PlayerPersonId
  FictionalName
  BirthYear
  Bats
  Throws
  PrimaryPosition
  RegistrationType       // Domestic | Foreign 등. 기존 동등 필드가 있으면 재사용
  CareerSpan
  PersonPotentialTrait

PlayerSeasonDefinition
  PlayerSeasonId
  PlayerPersonId
  OriginYear
  FranchiseId
  TeamSeasonKey
  Position               // 해당 시즌의 본래 수비 포지션
  PitcherRole            // 해당 시즌의 본래 투수 역할
  BaseAttributes
  Cost
  TrainingCeiling

PlayerCardDefinition
  CardId
  PlayerSeasonId
  Edition
  EditionStatModifiers
```

`PlayerPersonDefinition`과 `PlayerSeasonDefinition`은 01절의 Offline Pipeline에서 생성·검증 후 Bake한다. Runtime 월드 생성 시 능력치/Origin을 다시 생성하지 않는다.

선수모드의 일반 구단 선수와 감독모드의 일반 선수는 같은 `PlayerSeasonDefinition`/카드 원본을 사용한다. 선수 커리어 주인공만 기존 성장형 `CareerPlayerState`를 사용한다(07절).

### 1.1 PlayerPersonId와 실제 경기 인스턴스

```text
PlayerPersonId       // 동일 선수 카드 그룹을 묶는 식별자
PlayerSeasonInstance // 실제 로스터·경기·기록·부상·계약에 참여하는 Runtime 개체
```

`PlayerPersonId`를 세계의 유일한 실존 인물 Entity로 취급하지 않는다. 서로 다른 TeamSeason은 같은 Person 계열의 다른 시즌 인스턴스를 동시에 사용할 수 있다.

### 1.1.1 특수 합성팀의 선수 참조

02절의 `AllStarComposite`/`GoldenGloveComposite`/`YearSelectComposite`는 원래
`PlayerSeasonDefinition`/`PlayerCardDefinition`을 복제 참조하여 별도 경기용 로스터 인스턴스를
구성한다. 특수팀 배정은 계약·이적·Origin 변경이 아니다.

따라서 같은 `PlayerSeasonId`가 원래 Franchise 구단과 하나의 특수 합성팀에 동시에 등장하는 것은
허용한다. 다만 02절 규칙에 따라 **세 특수 합성팀끼리는 동일 `PlayerSeasonId`를 공유할 수 없다.**

특수팀에 들어간 카드도 다음 값은 원본 그대로 유지한다.

```text
PlayerSeasonId
PlayerPersonId
OriginYear
OriginFranchiseId
OriginTeamSeasonKey
Cost
Edition
```

특수팀 전용 가짜 Origin을 새로 부여하지 않는다.

### 1.2 등록 포지션과 실제 기용 포지션

`PlayerSeasonDefinition.Position`/`PitcherRole`은 **본래 포지션/역할**이다. 실제 경기의 `AssignedPosition`/`AssignedPitcherRole`은 달라질 수 있다.

- 야수 비주포지션 수비: 허용, 실책 확률 증가 + Condition 하락.
- DH: 어떤 타자도 본래 포지션과 무관하게 배치 가능하며 수비 비주포지션 패널티 없음.
- 투수 비본래 PitcherRole: 허용, Condition 하락.
- 정확한 패널티는 02절 `PositionAssignmentRule`/BalanceTable을 사용한다.

여러 적격/서브포지션을 이미 표현하는 기존 데이터가 있다면 그것을 우선한다. 그런 구조가 없다면 이 단계에서 임의의 복잡한 서브포지션 체계를 추가하지 않고 `Position`을 기본 적격 포지션으로 본다.

## 2. PersonPotentialTrait과 시즌별 TrainingCeiling

Person의 성장 성향과 감독모드 카드의 훈련 상한은 반드시 분리한다.

```text
PersonPotentialTrait
{
    ContactGrowthBias, PowerGrowthBias, SpeedGrowthBias,
    BuntGrowthBias, DefenseGrowthBias, MentalGrowthBias
}

PlayerSeasonDefinition.TrainingCeiling
{
    ContactCeiling, PowerCeiling, SpeedCeiling,
    BuntCeiling, DefenseCeiling, MentalCeiling
}
```

투수는 대응 6능력치를 사용한다.

`TrainingCeiling`은 시즌 BaseStat과 Cost가 확정된 뒤 파생한다.

```text
Headroom(ability) = CostToHeadroomTable[Cost] × PersonPotentialTrait[ability]
TrainingCeiling[ability] = Clamp(BaseStat[ability] + Headroom(ability), max: 99)
```

초기 Headroom 범위:

| Cost | Headroom 범위 |
| ---: | --- |
| 1~3 | +4 ~ +8 |
| 4~6 | +2 ~ +5 |
| 7~8 | +1 ~ +3 |
| 9~10 | 0 ~ +2 |

저Cost 시즌 카드를 DP로 성장시켜도 동일 인물의 고Cost 전성기 카드 절대 수준을 따라잡지 못하도록 한다.

### 2.1 CardTrainingState는 감독모드 Owned State

```text
OwnedPlayerCardState
{
    CardId
    EnhancementLevel
    DuplicateCount
    IsLocked
    IsFavorite
    CardTrainingState
}
```

훈련 누적치는 공통 `PlayerSeasonDefinition`에 저장하지 않는다. 감독모드 플레이어 구단의 Save State에만 저장한다.

## 3. Cost — 고정 희소도 축

Cost는 Baked `BaseAttributes`의 역할 보정 종합 능력치를 해당 OriginYear 집단 내 백분위로 변환해 결정한다. 수상 여부와 무관하게 고정된다.

| 백분위 | Cost |
| --- | ---: |
| 하위 5% | 1 |
| 5~15% | 2 |
| 15~30% | 3 |
| 30~45% | 4 |
| 45~60% | 5 |
| 60~72% | 6 |
| 72~82% | 7 |
| 82~90% | 8 |
| 90~97% | 9 |
| 상위 3% | 10 |

구간은 `CostBalanceTable`로 데이터화한다.

같은 `PlayerSeasonId`에서 파생된 모든 Edition은 같은 Cost를 공유한다.

## 4. Edition 자격 — World Award에서 파생

### 4.1 Normal

Normal은 모든 Baked PlayerSeason의 기본 카드다. 보너스 없음.

### 4.2 특수 Edition의 생성/활성 조건

AllStar/GoldenGlove/Mvp **수상자는 Offline 능력치 순위로 확정하지 않는다.** 새 게임 초기화 후 만들어진 공통 `WorldAwardRecord`를 기준으로 해당 World의 특수 Edition을 활성화한다.

```text
PlayerSeasonDefinition
    +
WorldAwardRecord
    ↓
WorldCardCatalog
    ↓
Normal / AllStar / GoldenGlove / Mvp 사용 가능 카드
```

`WorldAwardRecord`의 Source는 두 가지일 수 있다.

- `SimulatedHistory`: 실제 Historical Season Statistics 기반 Award Resolver 결과.
- `OriginalHistory`: Runtime-safe 고유 기록을 공통 Award Record로 변환한 결과.

카드/TeamColor/Scout는 이 Source 차이를 직접 분기하지 않는다.

### 4.3 CardId 안정성

특수 Edition의 존재 여부는 World마다 달라질 수 있지만 ID 규칙은 안정적이어야 한다. 예:

```text
CardId = Stable(PlayerSeasonId + Edition)
```

프로젝트 기존 Stable ID 정책이 있으면 그것을 사용한다. World마다 무작위 GUID를 새로 생성하지 않는다.

## 5. Edition 능력치 보너스

수상 여부는 World 결과지만 **Edition Modifier 수치 자체는 고정 Balance**다.

### AllStar

해당 World에서 All-Star로 선정된 PlayerSeason에 활성화한다.

| Cost | 타자 | 투수 |
| ---: | --- | --- |
| 1~4 | Contact +5 / Speed +5 | Velocity +5 / Control +5 |
| 5~6 | Contact +4 / Speed +4 | Velocity +4 / Control +4 |
| 7~8 | Contact +3 / Speed +3 | Velocity +3 / Control +3 |
| 9~10 | Contact +2 / Speed +2 | Velocity +2 / Control +2 |

`[Reference]`.

### GoldenGlove

해당 World의 Golden Glove 수상 PlayerSeason에 활성화한다. 포지션 수상 인원은 P/C/1B/2B/3B/SS 각 1명 + OF 3명 + DH 1명, 총 10명을 기본으로 한다.

- 야수: `Power +2 / Defense +2`
- 투수: `Stuff +2 / Breaking +2`

`[Reference]`.

### Mvp

정규시즌 MVP, 올스타전 MVP, 포스트시즌 MVP 중 하나 이상을 수상한 PlayerSeason에 활성화한다. 한 PlayerSeason이 복수 MVP를 받아도 Mvp Edition 카드는 1장이며 수상 기록은 `WorldAwardRecord`에 각각 남긴다.

| Cost | 보너스 |
| ---: | --- |
| 1~5 | ALL +5 |
| 6~8 | ALL +4 |
| 9~10 | ALL +3 |

`[Reference]`.

## 6. Origin은 Edition과 무관하게 유지

특수 Edition도 원본 Normal과 같은 다음 값을 사용한다.

```text
OriginYear
FranchiseId
TeamSeasonKey
Cost
```

Award가 Origin을 변경하지 않는다. 다른 World에서 Award 결과가 달라도 `2011 COMETS` 선수는 계속 `COMETS_2011` Origin을 가진다.

## 7. 두 모드의 Card Definition 공유와 Owned State 분리

### 공통

```text
PlayerPersonDefinition
PlayerSeasonDefinition
Normal Card Base
Edition Modifier Definition
WorldCardCatalog
TeamSeasonDefinition
```

### 감독모드 플레이어 구단 전용

```text
OwnedPlayerCardState
DuplicateCount
EnhancementLevel
CardTrainingState
Scout / Sale / Pity / SP / DP
```

선수 커리어 모드의 일반 구단 선수도 `WorldCardCatalog`의 공통 카드 데이터를 사용하지만 `OwnedPlayerCardState`를 만들지 않는다.

## 8. 동일 인물 중복 출전 금지

한 `ActiveRoster`에는 `PlayerPersonId` 기준 한 명만 등록한다.

```text
2010 김도윤 Normal + 2011 김도윤 Mvp 동시 등록 → 금지
```

다른 TeamSeason의 로스터가 같은 Person 계열의 다른 시즌 카드를 각각 사용하는 것은 허용한다.

## 9. 외국인 카드와 등록 제한

`RegistrationType == Foreign`인 카드의 **보유 수에는 제한이 없다.** 감독모드 Collection에 여러 장을 보관/스카우트할 수 있다.

제한은 02절 `ActiveRosterCompositionRule`의 `ForeignPlayers <= 3`에만 적용한다.

## 10. 검증 기준

- 모든 Baked PlayerSeason이 유효한 Cost 1~10, Origin, Position/PitcherRole을 가지는지 검증.
- 다른 World Seed에서도 BaseAttributes/Cost/TrainingCeiling/Origin이 변하지 않는지 검증.
- 특수 Edition은 해당 World의 `WorldAwardRecord`가 있을 때만 `WorldCardCatalog`에 활성화되는지 검증.
- `OriginalHistory`/`SimulatedHistory`가 동일한 WorldCardCatalog 생성 경로를 사용하는지 검증.
- 같은 PlayerSeason의 모든 Edition Cost와 Origin이 동일한지 검증.
- Award가 없는 PlayerSeason에 특수 Edition이 생성되지 않는지 검증.
- `TrainingCeiling >= BaseStat`이 항상 성립하는지 검증.
- 저Cost 카드 최대 훈련치가 동일 Person 고Cost 시즌 BaseStat을 부당하게 넘지 않는지 검증.
- 동일 ActiveRoster의 PlayerPersonId 중복을 거부하고 다른 TeamSeason 간 동시 사용은 허용하는지 검증.
- 특수 합성팀 배정이 카드의 Origin/Cost/Edition을 변조하지 않는지 검증.
- 같은 PlayerSeasonId가 원래 Franchise 구단과 특수 합성팀에 동시에 참조될 수 있으나 세 특수 합성팀 상호 간에는 중복되지 않는지 검증.
- 외국인 카드 4장 이상 보유는 허용하지만 ActiveRoster 4명 등록은 02절 Validator가 거부하는지 검증.
- 비주포지션/비본래 PitcherRole 기용이 Definition을 변조하지 않고 Runtime Assignment + Penalty로만 처리되는지 검증.

## 11. 2026-09-02 구현 현황

### 완료된 계약과 집중 검증

- `PlayerPersonDefinition`, `PlayerSeasonDefinition`, `PlayerCardDefinition`과 정확히 네 가지
  `PlayerCardEdition`을 공통 Core 모델로 구현했다.
- Stable `CardId`, Cost/Origin 유지, `TrainingCeiling >= BaseStat`, Definition 입력 복사에 따른
  불변성을 테스트했다.
- `WorldCardCatalog`가 Normal을 항상 만들고 `WorldAwardRecord`가 존재하는 경우에만
  AllStar/GoldenGlove/Mvp Edition을 활성화하도록 구현·테스트했다.
- Edition Modifier, Cost/Origin 동일성, 동일 Person의 ActiveRoster 중복 금지는 공통
  Resolver/Validator에서 검증한다.

### 부분 완료 또는 미완료

- Editor Bake에는 위 공통 Definition에 필요한 원본 데이터가 생성되지만, 이를 실제 새 게임에
  공급하는 Runtime `ICareerBakedContentProvider` 구현은 아직 없다.
- Career의 일반 선수 Save에 `PlayerSeasonId`, Natural `PitcherRole`, 공통 World Card 참조를
  영속화하는 통합은 미완료다.
- 특수 합성팀이 원 구단과 같은 `PlayerSeasonId`를 동시에 사용할 경기용 Instance 모델은
  미완료다.

따라서 공통 Card/Edition Definition과 Resolver는 완료됐지만 **두 모드가 실제 Save/Season에서
하나의 원본을 끝까지 소비하는 Gate는 부분 완료**다.
