# 03. 선수풀 데이터 모델 — Cost와 Edition

## 0. 목적

이 문서의 `[Reference]`/`[ProjectOriginal]` 표기는 04절 §0.1의 규칙을 그대로 따른다.

감독모드에서 수집·강화·판매의 대상이 되는 카드 데이터 모델을 정의한다. Edition은 정확히
4종(`Normal`, `AllStar`, `GoldenGlove`, `Mvp`)만 두고, 그 이상의 등급 체계(Legend/Classic/Rare
등)는 만들지 않는다 — 카드 시스템을 늘리지 않고 기존 성장/육성 축(강화, DP 훈련, 팀컬러)의
조합으로 깊이를 만든다.

## 1. 3단 데이터 모델

```text
PlayerPersonDefinition   // 가상 인물 1명, 영구
  PlayerPersonId
  FictionalName, BirthYear, Bats, Throws, PrimaryPosition
  CareerSpan                        // 데뷔~은퇴 연도, 01절 §5의 SyntheticCareerGenerator가 결정
  PersonPotentialTrait               // 성장 성향(능력치별 상대적 크는 속도/방향), Person당 1회 생성(§2.1)

PlayerSeasonDefinition    // 인물의 특정 연도·구단 스냅샷
  PlayerPersonId                    // 같은 인물의 여러 시즌을 연결하는 키 — 01절 §5 참고
  OriginYear, FranchiseId, TeamSeasonKey
  Position, PitcherRole
  BaseAttributes           // 01절 §5.1 SyntheticPlayerBlueprint 결과
  Cost                     // 1~10, §2
  TrainingCeiling          // 이 시즌 카드 고유의 능력치별 훈련 상한, §2.1에서 파생

PlayerCardDefinition       // 실제 수집 대상
  CardId
  PlayerSeasonId
  Edition                  // Normal | AllStar | GoldenGlove | Mvp
  EditionStatModifiers      // §3
```

한 `PlayerPersonId`가 여러 `PlayerSeasonDefinition`(연도별)을 갖는 구조는 01절 §5
`SyntheticCareerGenerator`가 생성 시점에 함께 만든다 — `PlayerSeasonDefinition`을 연도마다
독립적으로 생성한 뒤 사후에 짝짓는 것이 아니라, 커리어 생성 자체가 인물 단위로 시작해서
연도별 시즌을 파생시키는 순서다.

**감독모드 카드는 나이를 먹지 않는다.** `2011 김도윤` 카드는 게임이 몇 시즌 진행되어도 항상
2011 시즌의 능력치다. 이는 선수 커리어 모드의 노화/은퇴 시스템과 완전히 별개 규칙이다.

이 규칙은 05절의 DP 훈련과 충돌하지 않는다 — 감독모드 카드의 훈련은 **나이·성장곡선 기반
`NaturalGrowth`/노화 Resolver를 전혀 쓰지 않고**, 시즌 카드마다 고정되는 `TrainingCeiling`
값만으로 `CardTrainingBonus`(고정 성장분)를 계산한다(05절 §5). "나이를 먹지 않는
스냅샷"과 "훈련으로 조금 더 성장할 여지가 있는 카드"는 서로 다른 개념이며, 후자에 나이·
노화 개념을 끌어들이지 않는다.

### 1.1 PersonPotentialTrait과 시즌별 TrainingCeiling — 반드시 분리한다

**이전 초안의 구조적 오류:** 훈련 잠재 상한(`DevelopmentPotentialProfile`)을 Person 단위로
한 번만 만들어 모든 시즌 카드가 공유하게 했었다. 이러면 `2007 김도윤 Cost 3`(저코스트,
전성기 이전)과 `2013 김도윤 Cost 9`(전성기, 리그 최정상급)가 **같은 PowerCeiling**을
갖게 되어, 저코스트 카드를 DP로 밀어붙이면 전성기 카드와 능력치가 비슷해질 수 있다 —
"상위 능력치 선수일수록 얻기 어렵다"는 선수풀 핵심 전제와 정면으로 충돌한다. 게다가 Ceiling을
Person 생성 시점에 먼저 정하고 시즌 BaseStat을 나중에 생성하는 순서였으므로, 특정 시즌의
BaseStat이 우연히 Ceiling을 넘어서는 모순도 가능했다.

따라서 이 둘을 명확히 분리한다.

```text
PersonPotentialTrait   // Person 소유, 능력치별 "성장 성향"만 — 절대 능력치 상한이 아니다
{
    ContactGrowthBias, PowerGrowthBias, SpeedGrowthBias,
    BuntGrowthBias, DefenseGrowthBias, MentalGrowthBias
    // 투수는 대응 6항목. 각 값은 "이 인물은 어느 능력치가 상대적으로 더/덜 자라는 유형인가"를
    // 나타내는 성향 계수일 뿐, 그 자체로는 어떤 시즌의 실제 상한도 결정하지 않는다.
}

PlayerSeasonDefinition.TrainingCeiling   // 시즌 카드 소유, 실제 훈련 상한 (Cost로 스케일)
{
    ContactCeiling, PowerCeiling, SpeedCeiling, BuntCeiling, DefenseCeiling, MentalCeiling
    // 투수는 대응 6항목
}
```

`TrainingCeiling`은 Person 생성이 아니라 **그 시즌의 `BaseStat`과 `Cost`가 확정된 뒤**
파생값으로 계산한다 — Ceiling이 BaseStat보다 낮아지는 모순이 애초에 생기지 않는다.

```text
Headroom(ability) = CostToHeadroomTable[Cost] × PersonPotentialTrait[ability]   // 능력치별 여유폭
TrainingCeiling[ability] = Clamp(BaseStat[ability] + Headroom(ability), max: 99)
```

`CostToHeadroomTable`은 Cost가 낮을수록(약한 시즌 카드일수록) 여유폭을 크게, Cost가
높을수록(이미 전성기) 여유폭을 작게 준다 — 초기값 예:

| Cost | Headroom 범위 |
| ---: | --- |
| 1~3 | +4 ~ +8 |
| 4~6 | +2 ~ +5 |
| 7~8 | +1 ~ +3 |
| 9~10 | 0 ~ +2 |

즉 `2007 김도윤 Cost 3`은 DP로 상당히 클 수 있지만 그래봐야 자기 시즌의 BaseStat 기준
+4~8까지만 크고, `2013 김도윤 Cost 9`의 절대 수준을 따라잡지는 못한다. `CardTrainingResolver`
(05절 §5)는 현재 `CardTrainingBonus` 누적치와 이 시즌 카드의 `TrainingCeiling` 사이의
거리로 훈련 효율을 계산한다.

### 1.2 CardTrainingState는 OwnedPlayerCardState에 귀속

훈련 누적치(`CardTrainingBonus`)는 `PlayerSeasonDefinition`(공통 원본)이 아니라 §5의
`OwnedPlayerCardState`(구단 귀속 세이브)에 저장한다 — 같은 카드를 나중에 다른 구단에서
다시 뽑으면 훈련 누적치는 0부터 새로 쌓인다. 정확한 필드 구성은 §5를 참고한다.

## 2. Cost — 희소도 축

Cost는 카드 성능 등급이자 스카우트 확률을 결정하는 유일한 희소도 지표다. 특수 Edition
여부와 Cost는 독립이다 — 같은 `PlayerSeasonDefinition`에서 파생된 Normal/AllStar/GoldenGlove/
Mvp 카드는 모두 같은 Cost를 공유한다.

Cost는 `BaseAttributes`의 역할 보정 종합 능력치를 해당 `OriginYear` 전체 선수 집단 내
백분위로 변환해 결정한다(포지션 희소성 보정 포함). 초기 구간표:

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

이 구간값은 `CostBalanceTable`로 데이터화하고, 실제 생성 결과 분포를 보고 조정한다.

## 3. Edition

과거 시드 연도의 AllStar/GoldenGlove/Mvp 선정 자체(어떤 가상 선수가 그 Edition을 받는가)는
01절 §5.2 `SyntheticAwardResolver`가 결정론적으로 계산한다. 이 절은 선정된 카드가 받는
**능력치 보너스**만 정의한다.

### Normal

기본 카드. 보너스 없음.

### AllStar

해당 `OriginYear`의 시즌 올스타로 선정된 카드(연도당 약 24~30명). Cost 구간별 보너스(약한
선수일수록 보정이 크다, `[Reference]`):

| Cost | 타자 | 투수 |
| ---: | --- | --- |
| 1~4 | Contact +5 / Speed +5 | Velocity +5 / Control +5 |
| 5~6 | Contact +4 / Speed +4 | Velocity +4 / Control +4 |
| 7~8 | Contact +3 / Speed +3 | Velocity +3 / Control +3 |
| 9~10 | Contact +2 / Speed +2 | Velocity +2 / Control +2 |

### GoldenGlove

해당 `OriginYear`의 포지션별 최고 선수(연도당 10명: P/C/1B/2B/3B/SS/OF×3/DH). Cost와 무관한
고정 보너스: 야수 `Power +2 / Defense +2`, 투수 `Stuff +2 / Breaking +2`(`[Reference]`).

이 Edition 자체 보너스는 04절의 GoldenGlove **TeamColor**(덱 보너스, Contact/Mental 위주)와
별개 레이어다. 두 수치를 섞어서 하나로 취급하지 않는다 — 04절 §8 참고.

### Mvp

연도당 최대 3명(정규시즌 MVP, 올스타전 MVP, 포스트시즌 MVP). 한 인물이 복수 수상해도 Mvp
Edition 카드는 1장이며, 수상 이력만 별도 필드(`MvpAwardRecords[]`)로 여러 개 저장한다. Cost
구간별 전 능력치 보너스(`[Reference]`):

| Cost | 보너스 |
| ---: | --- |
| 1~5 | ALL +5 |
| 6~8 | ALL +4 |
| 9~10 | ALL +3 |

## 4. Origin은 Edition과 무관하게 유지된다

MVP·올스타·골든글러브 카드도 `OriginYear`/`FranchiseId`/`TeamSeasonKey`를 그대로 가진다.
04절 TeamColorResolver가 "구단별 연도덱"/"구단덱"/"연도덱" 조건을 판정할 때 이 카드들도
정상적으로 포함된다. Edition 전용 팀컬러(All-Star덱/Golden Glove덱/MVP덱)는 `Edition` 필드로
별도 판정한다.

## 5. 보유 상태와 Definition의 분리

```text
OwnedPlayerCardState   // 감독모드 세이브 전용, Definition과 별개 저장소
{
    CardId
    EnhancementLevel        // 0~5, 강화는 05절 §4
    DuplicateCount
    IsLocked
    IsFavorite
    CardTrainingState       // { AccumulatedBonus[6], PendingProgramId? } — 04절 §1.1의
                             // CardTrainingBonus 누적치, DP 훈련은 05절 §5
}
```

`PlayerCardDefinition`은 게임 전체 공통 원본(글로벌 유일 자원이 아니다 — AI TeamSeason과
플레이어가 같은 Definition을 동시에 참조할 수 있다). `OwnedPlayerCardState`는 감독모드로
플레이한 구단에만 귀속된다(감독이 다른 구단으로 이적하면 컬렉션은 원래 구단에 남는다 —
05절 참고).

## 6. `PlayerPersonId`의 의미 — "세계의 유일한 실존 인물"이 아니라 "카드 그룹 식별자"

**명확히 해야 한다.** 이 세계에는 `2009 Comets 김도윤`과 `2010 Tides 김도윤`이 동시에
서로 다른 TeamSeason 소속으로 존재하고, 극단적으로는 같은 시점에 서로 다른 팀의 경기에
"참여"할 수 있다(02·07절이 여러 TeamSeason을 동시에 굴리는 세계관이기 때문). 감독 카드
게임 관점에서는 문제가 없다 — 프야매도 같은 선수의 여러 연도 카드가 각각 독립된 카드로
존재했다. 하지만 코드에서 `PlayerPersonId`를 "세계에 실제로 존재하는 유일한 사람"으로
취급하면 계약·부상·기록 집계 로직이 꼬인다.

따라서 다음을 확정한다.

```text
PlayerPersonId              // "동일 선수 카드 그룹" 식별자. 세계 안의 유일 개체를 의미하지 않는다.
PlayerSeasonInstance         // 실제 경기·로스터·기록에 참여하는 개체. PlayerSeasonDefinition 1개당 1개.
```

경기 시뮬레이션, 로스터 배치, 계약/이적, 부상, 기록 집계는 전부 `PlayerSeasonInstance`
(또는 그 카드 인스턴스) 단위로 이뤄지고, `PlayerPersonId`는 어디까지나 "여러
`PlayerSeasonInstance`를 하나의 카드 수집 계열로 묶는 라벨"일 뿐이다. `PlayerPersonId` 자체를
계약·출전·부상의 주체로 삼는 코드를 만들지 않는다.

## 7. 동일 인물 중복 출전 금지 — 카드 덱 구성 규칙이지 세계 유일성 규칙이 아니다

`ActiveRoster`(1군 25인)에는 `PlayerPersonId` 기준으로 한 명만 등록할 수 있다. `2010 김도윤`과
`2011 김도윤 Mvp`를 동시에 출전시킬 수 없다. **이는 한 구단의 카드 덱 구성 규칙이다** — "이
세계에 김도윤이라는 사람이 동시에 한 명만 존재해야 한다"는 뜻이 아니다. 다른 구단
(`CurrentRosterState`가 다른 TeamSeason)이 같은 `PlayerPersonId`의 다른 시즌 카드를 동시에
기용하는 것은 전혀 제한하지 않는다.

## 8. 검증 기준

- 모든 `PlayerSeasonDefinition`이 유효한 Cost(1~10)를 가지는지 테스트.
- 같은 `PlayerSeasonId`에서 파생된 Normal/AllStar/GoldenGlove/Mvp 카드가 동일한 Cost를
  갖는지 테스트.
- 특수 Edition 카드의 `OriginYear`/`FranchiseId`/`TeamSeasonKey`가 원본 Normal 카드와 항상
  일치하는지 테스트(변조 방지).
- `ActiveRoster`에 동일 `PlayerPersonId`가 두 번 등록되면 검증 실패하는 테스트(§7).
- 서로 다른 TeamSeason의 `CurrentRosterState`/`ActiveRoster`가 같은 `PlayerPersonId`의 다른
  시즌 카드를 동시에 기용해도 아무 제약에 걸리지 않는지 테스트(§6 — 세계 유일성 규칙이
  아님을 검증).
- 같은 `PlayerPersonId`의 여러 `PlayerSeasonDefinition`이 01절 §5 `SyntheticCareerGenerator`
  없이 서로 무관하게 생성되지 않는지(사후 짝짓기가 아니라 생성 시점부터 연결되는지) 테스트.
- `PersonPotentialTrait`이 Person당 1회만 생성되고, 그 인물의 서로 다른 시즌 카드가 서로
  다른 `TrainingCeiling`(BaseStat+Cost 기반 파생값)을 갖는지 테스트 — 특히 저코스트 시즌의
  `TrainingCeiling`이 동일 인물의 고코스트 시즌 BaseStat을 넘어서지 않는지 확인.
- 모든 시즌 카드에서 `TrainingCeiling ≥ BaseStat`이 항상 성립하는지(파생 순서상 모순이
  생기지 않는지) 테스트.
- `OwnedPlayerCardState.CardTrainingState`가 감독이 구단을 옮기면 이전 구단에 남고 새 구단의
  같은 카드는 0부터 시작하는지 테스트.
