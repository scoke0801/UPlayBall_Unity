# 14. 특수 선수 카드 — Rare / EX / Career High / Legend

> 2026-09-09 구현 상태: 발급 Gate·Core 계약·순수 C# 합성/영입 거래·SaveVersion 19 기반을 추가했다.
> 실제 발급은 EX Cost 조건 52건 미달과 레전드 큐레이션 미등록으로 차단된다. Unity 콘텐츠 로드·UI·
> Production E2E는 미완료이며 33절 완료 조건을 충족하지 않았다.
> 상세: `docs/reports/특수카드_발급_획득_구현.md`.

> 상태: 신규 정본 기획안 / Codex 구현 기준  
> 적용 범위: 구단주 모드의 카드 컬렉션·스카우트·합성·팀컬러·선수단 UI  
> 연동 문서: `01_시대보정_가상선수생성.md`, `03_선수풀_카드_Cost_Edition.md`, `04_팀컬러_시스템.md`, `05_구단주모드_경제_스카우트.md`, `07_선수모드_연동_와일드카드.md`, `08_구현_로드맵_검증기준.md`

---

## 0. 문서 목적

현재 프로젝트의 카드 Edition은 `Normal / AllStar / GoldenGlove / Mvp`를 중심으로 구성되어 있다. 이 문서는 다음 네 종류의 특수 카드를 정식 콘텐츠로 추가한다.

```text
Rare
EX
CareerHigh
Legend
```

이 확장은 단순히 enum 네 개를 더하는 작업이 아니다. 다음을 한 Source of Truth로 묶어야 한다.

```text
Canonical PlayerSeason
→ Cost와 최종 발급 스탯 확정
→ 특수카드 대상 선정
→ 특수카드 Definition·Recipe 사전 Bake
→ WorldCardCatalog 활성화
→ 획득 경로
→ Owned Collection
→ TeamColor
→ SkillBlock 효과
→ 선수단/도감/특수 영입 UI
→ Save/Load
```

핵심 목표는 네 카드의 역할을 명확히 분리하는 것이다.

- **Rare**: Cost 4~5 선수의 스킬블록 특화 카드.
- **EX**: 해당 연도의 최고 성적 타자·투수에게 주는 Cost 10 전용 카드이며, 합성에서만 획득한다.
- **CareerHigh**: 한 프랜차이즈에서 최소 8개 유효 시즌을 가진 선수의 최고 발급 시즌을 베이스로 하며, 동일 선수의 서로 다른 연도 카드 8장을 소비해 합성한다.
- **Legend**: 프랜차이즈의 역사적 대표 선수에게 부여하며, 해당 선수의 가장 강하게 발급된 시즌을 베이스로 한다.

CareerHigh와 Legend는 모두 자신이 속한 `TeamColorLineage`의 모든 구단별 연도 팀컬러를 적용받는 프랜차이즈 와일드카드다.

---

## 1. 최종 확정사항

아래 항목은 본 문서의 절대 계약이다.

| 항목 | 확정 규칙 |
|---|---|
| 모든 카드 생성 시점 | **모든 카드 Definition과 획득 Recipe는 Offline/Editor에서 사전 Bake**한다. Runtime 생성 금지. |
| Normal 공존 | Rare와 EX가 존재하는 PlayerSeason에도 **Normal 카드가 별도로 존재**한다. |
| Rare Cost | **Cost 4 또는 5만 가능**하다. |
| EX Cost | **Cost 10만 가능**하다. |
| CareerHigh Cost | 베이스 카드가 **Cost 9 또는 10**이어야 한다. |
| Legend Cost | 베이스 카드가 **Cost 9 또는 10**이어야 한다. |
| EX 선정 수 | OriginYear마다 **타자 1명, 투수 1명**을 선정한다. |
| EX 선정 기준 | 해당 연도의 역할별 최고 성적 선수다. 최고 성적 선수가 Cost 10이 아니면 Bake 실패다. |
| EX 획득 | **선수 합성으로만 획득**한다. Scout, Pity, Award Scout, 보상 직접 지급 금지. |
| Rare/EX 스킬 효과 | 스킬블록의 수치 효과를 **정확히 ×2.0** 적용한다. |
| 스킬블록 해방 | **사용하지 않는다.** 슬롯 해방, 해방 진행도, 전면 개방 보너스 개념을 추가하지 않는다. |
| CareerHigh 최소 경력 | 같은 TeamColorLineage에서 **유효 시즌 8개 이상**이어야 한다. |
| CareerHigh 재료 | 동일 선수·동일 Lineage의 **서로 다른 연도 Normal 카드 8장**을 소비한다. |
| CareerHigh 베이스 | 해당 선수의 Lineage 내 시즌 중 **최종 발급 스탯이 가장 좋은 PlayerSeason**을 사용한다. |
| Legend 베이스 | 해당 선수의 Lineage 내 시즌 중 **최종 발급 스탯이 가장 좋은 PlayerSeason**을 사용한다. |
| CareerHigh 팀컬러 | 해당 TeamColorLineage의 모든 `Franchise` 및 `YearFranchise` 팀컬러에 와일드카드로 인정한다. |
| Legend 팀컬러 | CareerHigh와 동일하게 해당 TeamColorLineage 전체에 와일드카드로 인정한다. |
| 현행 10개 구단 계보 | 현대의 10개 구단을 기준으로 전신 구단 기록을 게임용 TeamColorLineage로 승계한다. |
| 신생 구단 | NC·KT는 전신 구단이 없으므로 자신의 창단 이후 역사에서 Legend와 CareerHigh를 선정한다. |

### 1.1 사용자 확정 외 초기 밸런스값

아래는 시스템을 완결하기 위한 초기값이며, 사용자 확정사항과 구분한다.

```text
Rare EditionStatModifier       = 0
EX EditionStatModifier         = 0
CareerHigh EditionStatModifier = ALL +2   [ProjectInitial]
Legend EditionStatModifier     = ALL +2   [ProjectInitial]
```

CareerHigh·Legend의 `ALL +2`는 기존 설계 제안을 유지한 초기값이다. 실제 경기 영향이 과도하면 `SpecialCardBalanceTable`에서 낮추되, 베이스 PlayerSeason의 `BaseAttributes` 자체는 변경하지 않는다.

---

## 2. 기존 정본 계약과의 관계

### 2.1 Canonical PlayerSeason 불변

한 `PlayerSeasonDefinition`은 한 Source PlayerSeason에서만 파생한다.

```text
Source PlayerSeason
→ BaseAttributes
→ Cost
→ TrainingCeiling
→ OriginYear / OriginFranchiseId / OriginTeamSeasonKey
```

특수카드 추가로 다음 값을 바꾸지 않는다.

```text
PlayerSeasonDefinition.BaseAttributes
PlayerSeasonDefinition.Cost
PlayerSeasonDefinition.TrainingCeiling
PlayerSeasonDefinition.OriginYear
PlayerSeasonDefinition.OriginFranchiseId
PlayerSeasonDefinition.OriginTeamSeasonKey
```

특수카드는 이미 확정된 PlayerSeason을 참조하는 별도 `BakedCardDefinition`이다.

### 2.2 World Identity 불변

Rare·EX·CareerHigh·Legend는 모두 기존 `PlayerPersonId`를 공유한다. 같은 인물의 Normal과 Legend가 서로 다른 표시 이름을 가져서는 안 된다.

```text
PlayerPersonId
→ WorldPlayerIdentity.DisplayName
→ Normal / Rare / EX / CareerHigh / Legend 모두 같은 이름 표시
```

실제 Source 선수명은 Editor provenance와 검수 리포트에서만 사용하며 Runtime 표시 이름으로 사용하지 않는다.

### 2.3 Cost 불변

Cost는 특수카드 자격을 판정하는 입력이지 특수카드가 Cost를 바꾸는 출력이 아니다.

```text
잘못된 처리
EX 선정 → Cost를 10으로 승격
Legend 선정 → Cost를 9로 승격

정식 처리
Cost Bake 완료
→ Cost Gate 검사
→ Gate 통과 카드만 특수카드 대상
```

Cost Gate를 통과하지 못하면 대상에서 제외하거나 필수 콘텐츠의 경우 Bake를 실패시킨다.

### 2.4 동일 인물 경기 중복 금지

한 Collection은 같은 인물의 여러 Edition을 보유할 수 있다. 그러나 실제 ActiveRoster와 경기 라인업에서는 기존 규칙대로 같은 `PlayerPersonId`를 둘 이상 등록하지 않는다.

```text
보유
2010 Normal A + 2010 EX A + CareerHigh A + Legend A → 허용

ActiveRoster
동일 PlayerPersonId 두 장 이상 → 거부
```

---

## 3. 용어 정의

### 3.1 BakedCardDefinition

Offline에서 확정한 실제 카드 Definition이다.

```text
BakedCardDefinition
{
    CardId
    PlayerPersonId
    BasePlayerSeasonId
    Edition
    VariantKey?

    EditionStatModifier
    SkillBlockEffectMultiplier
    TeamColorAffinity
    AcquisitionPolicy

    IsUniqueOwnedCard
    ContentVersion
}
```

### 3.2 BasePlayerSeasonId

카드의 기본 능력치·Cost·Origin을 제공하는 Canonical PlayerSeason이다.

- Normal/Rare/EX/수상 Edition: 그 카드가 속한 PlayerSeason 자체.
- CareerHigh/Legend: 해당 인물·Lineage에서 최종 발급 스탯이 가장 좋은 시즌.

### 3.3 Peak Issued Card

Source 성적 원본이 아니라 **Offline Bake를 거쳐 실제 카드로 발급된 최종 스탯이 가장 강한 시즌**이다.

```text
PlayerPerson의 Lineage 내 PlayerSeason들
→ 최종 BaseAttributes와 Cost 확인
→ IssuedCardStrengthScore 비교
→ 최고 시즌을 BasePlayerSeasonId로 선택
```

### 3.4 TeamColorLineage

실제 Source Franchise의 법적·행정적 동일성을 표현하는 값이 아니라, 현재 10개 구단 기준으로 과거 구단 카드를 연결하기 위한 **게임용 팀컬러 계보**다.

### 3.5 Definition과 Availability

카드 Definition이 존재하는 것과 현재 플레이어가 획득할 수 있는 것은 다르다.

```text
Definition = 사전 Bake된 카드 정체성
Availability = 현재 World/획득 시스템에서 사용할 수 있는 상태
Ownership = 플레이어가 실제로 보유한 카드 Instance
```

---

## 4. 전체 데이터 흐름

```text
Source Normalize
→ Canonical PlayerPerson / PlayerSeason Bake
→ BaseAttributes / Cost / TrainingCeiling 확정
→ Source Franchise → TeamColorLineage Mapping
→ IssuedCardStrengthScore Bake
→ Special Card Candidate Build
→ Rare Selection
→ EX Year/Role Selection
→ CareerHigh Eligibility + Base Season + Recipe
→ Legend Curation + Base Season + Recipe
→ 모든 BakedCardDefinition 생성
→ 모든 Acquisition Recipe 생성
→ Validation Report / Content Hash
→ Runtime BakedCardCatalog 로드
→ WorldAwardRecord로 수상 Edition Availability만 활성화
→ Scout / Combination / SpecialRecruit
→ OwnedPlayerCardState
```

Runtime에서는 다음을 절대 수행하지 않는다.

- 특수카드 대상 선수 재선정.
- EX 최고 성적 재계산.
- CareerHigh/Legend 베이스 시즌 재계산.
- CareerHigh 경력 8년 판정 재계산.
- Legend 재료 그룹 생성.
- 카드 Cost·Origin 수정.
- TeamColorLineage 추론.

---

## 5. 카드 Edition과 ID

```text
CardEdition
{
    Normal,
    Rare,
    Ex,
    AllStar,
    GoldenGlove,
    Mvp,
    CareerHigh,
    Legend
}
```

### 5.1 CardId

한 PlayerSeason에 Normal·Rare·EX가 동시에 존재하므로 Edition을 포함한다.

```text
CardId = Hash(BasePlayerSeasonId, Edition, VariantKey)
```

`VariantKey`는 일반적으로 비어 있다. 같은 인물이 서로 다른 TeamColorLineage에 대해 별도 Legend 또는 CareerHigh를 가질 수 있는 예외에서는 `TeamColorLineageId`를 사용한다.

```text
Legend:<BasePlayerSeasonId>:<TeamColorLineageId>
CareerHigh:<BasePlayerSeasonId>:<TeamColorLineageId>
```

### 5.2 사전 Bake 범위

모든 카드가 사전 Bake 대상이라는 계약은 다음을 의미한다.

- 모든 Normal Definition을 Bake한다.
- 최종 선정된 Rare/EX/CareerHigh/Legend Definition을 Bake한다.
- AllStar/GoldenGlove/Mvp도 Runtime에서 새 Definition을 만드는 대신, 가능한 카드 Definition을 사전에 Bake하고 WorldAwardRecord는 활성 여부만 결정한다.
- 모든 Legend 재료 그룹과 CareerHigh 8시즌 후보 목록도 사전 Bake한다.
- Runtime은 존재하지 않는 CardId를 임의 생성할 수 없다.

---

## 6. 최종 발급 스탯 기준 베이스 시즌 선정

CareerHigh와 Legend는 같은 공통 Resolver를 사용한다.

```text
PeakIssuedCardSelector.Select(
    PlayerPersonId,
    TeamColorLineageId,
    PlayerSeasonDefinition[])
```

### 6.1 후보 범위

후보 PlayerSeason은 다음을 모두 만족해야 한다.

```text
PlayerSeason.PlayerPersonId == target PlayerPersonId
TeamColorLineageMap(PlayerSeason.OriginFranchiseId) == target Lineage
PlayerSeason이 Production Canonical Archive에 존재
PlayerSeason이 유효한 Position/PitcherRole과 BaseAttributes를 가짐
```

### 6.2 IssuedCardStrengthScore

새 임의 공식을 Presentation에 만들지 않는다. Cost 산정에서 이미 사용하는 최종 카드 전력 Composite 또는 그 직전 연속 점수를 재사용한다.

권장 우선순위:

```text
1. CostDerivationComposite 또는 동등한 연속형 Baked Strength Score
2. 역할별 최종 BaseAttributes 가중합
3. Reliability
4. 표본 크기
5. OriginYear 오름차순
6. PlayerSeasonId Stable 정렬
```

`Cost` 숫자만 비교하면 같은 Cost 10 내부의 차이를 구분할 수 없으므로 연속형 Strength Score를 반드시 보존한다.

### 6.3 Cost Gate

Peak 시즌을 먼저 선택한 뒤 Gate를 검사한다.

```text
Peak Cost 9 또는 10 → CareerHigh/Legend 가능
Peak Cost 8 이하    → 해당 특수카드 자격 없음
```

더 약한 다른 시즌이 Cost 9라는 이유로 Peak 시즌 대신 선택하지 않는다. “가장 강하게 발급된 시즌”과 “Cost Gate”를 동시에 만족하지 못하면 해당 카드 자체를 만들지 않는다.

---

## 7. Rare 카드

## 7.1 역할

Rare는 낮은 Cost를 스킬블록 세팅으로 재발견하는 카드다.

```text
Cost 범위                 4~5
BasePlayerSeasonId        같은 시즌 Normal과 동일
EditionStatModifier       0
SkillBlockEffectMultiplier 2.0
TeamColorAffinity         FixedOrigin
Normal 공존               필수
```

### 7.2 선정

모든 Cost 4~5가 자동 Rare가 되는 것은 아니다. 모든 Rare 대상은 Offline에서 명시적으로 확정한다.

```text
RareCandidate
  = Cost in {4, 5}
  + 유효한 표본/Reliability
  + 최종 Curation 또는 Versioned Selection Policy 통과
```

권장 초기 저작 방식:

1. Bake가 Cost 4~5 후보를 전부 출력한다.
2. 연도·구단·포지션·역할 분포를 보여준다.
3. Editor에서 최종 `RareEligibility`를 승인한다.
4. 승인 결과를 Versioned Content로 Bake한다.

초기 추천 쿼터는 데이터로 관리한다.

```text
RareSelectionBalance
{
    TargetPerTeamSeason = 1
    YearRoleBalanceTarget = Hitter : Pitcher = 1 : 1
    AllowZeroWhenNoQualifiedCandidate = true
}
```

이는 초기 추천값이며, 최종 Rare 수량은 콘텐츠 검수 결과로 결정한다.

### 7.3 획득

초기 Production 정책:

- General/Franchise/Year/YearFranchise Scout에서 낮은 확률로 등장 가능.
- 선수 합성 결과로 등장 가능.
- Award Scout에서는 등장하지 않음.
- Pity의 Cost 7+ 확정 후보에는 들어가지 않음.

정확한 Weight는 `ScoutPoolDefinition.EditionWeights`에서 데이터화한다.

### 7.4 팀컬러

Rare는 연도를 뛰어넘지 않는다.

```text
2009 Lineage-A Rare
→ 2009 Lineage-A YearFranchise: 인정
→ 2010 Lineage-A YearFranchise: 불인정
→ Lineage-A Franchise: 인정
```

---

## 8. EX 카드

## 8.1 역할

EX는 매 OriginYear의 최고 성적 타자 1명과 투수 1명에게 주는 최고급 시즌 카드다.

```text
연도당 EX 수              정확히 2장
타자                       1장
투수                       1장
Cost                       정확히 10
BasePlayerSeasonId         선정된 그 시즌
EditionStatModifier        0
SkillBlockEffectMultiplier 2.0
TeamColorAffinity          FixedOrigin
Normal 공존                필수
획득                       합성 전용
```

## 8.2 선정 순서

```text
OriginYear별 전체 PlayerSeason 수집
→ Hitter / Pitcher 분리
→ 유효 표본 필터
→ ExSeasonPerformanceIndex 계산
→ 역할별 1위 결정
→ 해당 1위의 Cost == 10 검증
→ EX Definition Bake
```

### 8.3 최고 성적의 의미

EX는 카드 스탯 합계가 아니라 **해당 Source 시즌의 실제 성적을 시대·포지션·역할·표본 신뢰도로 정규화한 성과**로 선정한다.

새로운 임의 지표를 두 개 만들지 말고 Ability Bake에서 이미 계산하는 정규화 Trace를 재사용한다.

개념:

```text
Hitter ExSeasonPerformanceIndex
  = 시대보정 공격 가치
  + 수비/주루 가치
  + 출장량 Reliability

Pitcher ExSeasonPerformanceIndex
  = 시대·역할보정 실점 억제
  + 탈삼진/제구 가치
  + 이닝·출장량 Reliability
```

정확한 Weight는 `ExSelectionBalanceTable`에 버전 관리한다.

### 8.4 Cost 10 Gate

역할별 성적 1위가 Cost 10이 아니면 2위의 Cost 10 선수를 대신 뽑지 않는다.

```text
잘못된 처리
성적 1위 Cost 9
→ Cost 10인 성적 2위를 EX 선정

정식 처리
성적 1위 Cost 9
→ Cost/Rating Bake 왜곡 또는 기준 충돌로 Validation Fail
```

이 Gate는 “해당 연도 최고 성적 선수”와 “EX는 Cost 10만 가능”을 동시에 보장한다.

### 8.5 연도별 필수 수량

지원 OriginYear가 1982~2025라면 필수 EX Definition 수는 원칙적으로 다음과 같다.

```text
44개 연도 × 2장 = 88장
```

향후 연도 추가 시 동일 규칙으로 2장을 더 Bake한다.

### 8.6 선수 합성 전용 획득

EX는 다음 경로에서 반드시 제외한다.

```text
General Scout
Franchise Scout
Year Scout
YearFranchise Scout
Award Scout
Pity
미션 직접 카드 지급
상점 직접 구매
Legend/CareerHigh 합성 결과
```

유일한 Production 획득 경로:

```text
CardCombinationResolver
```

### 8.7 선수 합성 기본 계약

초기 합성은 재료 5장을 사용한다.

```text
CardCombinationInput
{
    MaterialCardInstanceIds[5]
    CombinationType = GeneralPowerUp
    RngSequence
}
```

허용 재료 기본값:

```text
Normal
Rare
AllStar
GoldenGlove
Mvp
```

금지 재료:

```text
EX
Legend
CareerHigh
현재 ActiveRoster/Lineup 사용 중 카드
잠금 카드
특수 영입에 예약된 카드
```

Rare·수상 카드는 첫 획득 시 자동 잠금하며, 사용자가 명시적으로 잠금을 해제한 경우에만 합성 재료로 선택할 수 있다.

### 8.8 합성 결과

```text
Material Score 계산
→ 결과 Cost Bucket 계산
→ Edition Bucket 계산
→ 실제 존재하는 BakedCardDefinition만 후보화
→ Normal/Rare/EX 중 결과 결정
```

EX 확률은 재료 품질이 높을수록 증가할 수 있으나 모든 수치는 데이터화한다.

```text
CardCombinationBalanceTable
{
    RequiredMaterialCount = 5
    MaterialEditionWeights
    ResultCostWeights
    RareChanceCurve
    ExChanceCurve
    MinimumScoreForExChance
}
```

EX Pity는 두지 않는다. EX는 확정 영입형 Legend/CareerHigh와 달리 합성의 Jackpot 역할을 유지한다.

---

## 9. CareerHigh 카드

## 9.1 역할

CareerHigh는 한 프랜차이즈 계보에서 장기간 활약한 선수의 최고 발급 시즌을 베이스로 만드는 카드다.

```text
최소 유효 시즌            8개
재료                      같은 선수의 서로 다른 연도 Normal 8장
BasePlayerSeasonId         Lineage 내 Peak Issued Card
Base Cost                  9 또는 10
EditionStatModifier        ALL +2 [ProjectInitial]
SkillBlockEffectMultiplier 1.0
TeamColorAffinity          FranchiseWildcard
획득                       CareerHigh 전용 합성
Normal 공존                필수
```

## 9.2 최소 8년 판정

단순히 Source에 한 경기라도 출전한 시즌을 1년으로 세지 않는다. “실제로 뛴 시즌”은 Versioned Eligibility Rule을 통과해야 한다.

```text
CareerHighQualifiedSeasonRule
{
    MinimumReliability
    MinimumRoleSample
    RequireValidCanonicalPlayerSeason = true
}
```

권장 구현은 기존 Reliability와 역할별 표본 판단을 재사용하는 것이다. 별도의 숨은 Runtime 경력 판정을 만들지 않는다.

같은 `TeamColorLineageId` 안에서 서로 다른 `OriginYear`가 8개 이상이어야 한다.

```text
QualifiedDistinctYears >= 8
```

다른 Lineage에서 뛴 시즌을 합산하지 않는다.

예:

```text
Lineage-A 6년 + Lineage-B 4년
→ 어느 Lineage에서도 CareerHigh 불가

Lineage-A 8년 + Lineage-B 2년
→ Lineage-A CareerHigh 가능
```

## 9.3 베이스 시즌

CareerHigh의 베이스는 커리어 평균이나 여러 시즌 합성 능력치가 아니다.

```text
같은 PlayerPersonId
+ 같은 TeamColorLineageId
→ PeakIssuedCardSelector
→ 가장 강하게 발급된 단일 PlayerSeason
```

Peak 카드의 Cost가 9 또는 10이어야 한다.

## 9.4 8장 합성 재료

CareerHigh Recipe는 다음과 같다.

```text
CareerHighSynthesisRecipe
{
    TargetCardId
    PlayerPersonId
    TeamColorLineageId
    QualifiedNormalCardIds[]
    RequiredDistinctYears = 8
}
```

재료 규칙:

1. `Edition == Normal`만 허용한다.
2. 모두 같은 `PlayerPersonId`여야 한다.
3. 모두 같은 `TeamColorLineageId`에 속해야 한다.
4. `OriginYear`가 서로 달라야 한다.
5. 정확히 8장을 소비한다.
6. 같은 연도 Normal 카드 중복 2장은 1개 연도로만 취급하며 두 슬롯을 채우지 못한다.
7. Peak 기준 연도 카드를 반드시 소비할 필요는 없다.
8. 유효 시즌이 정확히 8개면 그 8개가 모두 필요하다.
9. 유효 시즌이 9개 이상이면 사용자가 소비할 8개 연도를 선택한다.

### 9.5 재료 보호

- ActiveRoster, Lineup, Preset에서 사용 중인 카드는 선택할 수 없다.
- 잠금·위시리스트·Legend 재료 예약 카드도 선택할 수 없다.
- 강화·CardTraining이 적용된 Normal 카드는 선택 가능하지만 최종 확인에서 손실 경고를 표시한다.
- 자동 선택은 항상 강화/훈련 값이 가장 낮은 Copy를 우선한다.
- 최종 합성 확인 전에는 카드가 삭제되지 않고 `ReservedForCareerHigh` 상태로 잠긴다.
- 최종 커밋에서 8장을 한 Transaction으로 소모하고 CareerHigh 1장을 지급한다.

## 9.6 소유 제한

CareerHigh는 `UniqueSpecialCard`다.

- 같은 TeamSeason 소유 컬렉션에서 같은 CareerHigh CardId를 2장 이상 보유하지 않는다.
- 중복 강화 재료로 사용하지 않는다.
- 자동 판매·합성 재료로 사용할 수 없다.
- 실수 방지를 위해 기본 영구 잠금 상태로 지급한다.

---

## 10. Legend 카드

## 10.1 역할

Legend는 현재 10개 TeamColorLineage 각각의 역사를 대표하는 선수다.

```text
대상 선정                  Offline Curation
BasePlayerSeasonId         Lineage 내 Peak Issued Card
Base Cost                  9 또는 10
EditionStatModifier        ALL +2 [ProjectInitial]
SkillBlockEffectMultiplier 1.0
TeamColorAffinity          FranchiseWildcard
획득                       Legend 전용 영입
Normal 공존                필수
```

## 10.2 대상 선정

Legend는 단순 자동 성적 순위로만 선정하지 않는다. 역사성·프랜차이즈 상징성은 기획 판단이 필요하므로 `LegendCurationDefinition`에서 명시한다.

```text
LegendCurationDefinition
{
    PlayerPersonId
    TeamColorLineageId
    CuratedReasonTag[]
    Enabled
}
```

Bake는 Curation을 그대로 믿지 않고 다음을 검증한다.

- 해당 Person이 Lineage의 Source PlayerSeason을 하나 이상 가짐.
- Peak Issued Card가 존재함.
- Peak Cost가 9 또는 10임.
- 동일 `(PlayerPersonId, TeamColorLineageId)` 중복 없음.
- 실제 Runtime 이름을 Curation에 저장하지 않음.

Cost 8 이하인 선수를 상징성만으로 Legend로 승격하지 않는다.

## 10.3 NC·KT Legend

NC와 KT는 이전 구단 Lineage가 없으므로 자신의 창단 이후 선수만 대상으로 한다.

```text
NC Legend 후보
→ NC Lineage에서 Peak Cost 9~10을 가진 현대 선수 중 Curation

KT Legend 후보
→ KT Lineage에서 Peak Cost 9~10을 가진 현대 선수 중 Curation
```

역사가 짧다는 이유로 타 구단의 옛 선수를 가져오지 않는다.

## 10.4 Legend 영입 Recipe

Legend는 8개의 재료 그룹을 완성해 영입한다.

```text
LegendRecruitRecipe
{
    TargetLegendCardId
    TeamColorLineageId
    MaterialGroups[8]
}

LegendMaterialGroup
{
    GroupId
    CandidateNormalCardIds[]
}
```

각 그룹은 후보 중 한 장만 등록하면 충족된다.

```text
1성구: Candidate A 또는 B 또는 C 중 1장
2성구: Candidate D 또는 E 중 1장
...
8성구: Candidate X 중 1장
```

재료 그룹 작성 규칙:

- 재료는 Normal Edition만 사용한다.
- 같은 Owned Card Instance가 두 그룹을 동시에 만족할 수 없다.
- 같은 PlayerPerson을 여러 그룹에 반복 배치하지 않는 것을 원칙으로 한다.
- 대상 Legend 본인의 Normal 카드 사용 여부는 Recipe별로 명시한다.
- 가능한 경우 서로 다른 시대·포지션·역할을 분산한다.
- 모든 Candidate는 해당 TeamColorLineage의 역사 안에서 선정한다.
- 후보가 한 장뿐인 필수 슬롯은 UI에 `지정 재료`로 표시한다.
- Recipe는 모두 Offline에서 Bake하고 Runtime 교체·랜덤 생성하지 않는다.

## 10.5 소유 제한

Legend도 `UniqueSpecialCard`다.

- 동일 CardId 중복 보유 금지.
- 중복 강화, 자동 판매, 일반 합성 재료 사용 금지.
- 기본 영구 잠금.

---

## 11. TeamColorLineage

## 11.1 목적

현재 `FranchiseId`는 Source provenance와 Simulation Identity를 보존한다. 전신 구단을 현재 10개 구단으로 묶기 위해 `FranchiseId`를 덮어쓰지 않고 별도 `TeamColorLineageId`를 둔다.

```text
Canonical FranchiseId
  = Source 구단 정체성

TeamColorLineageId
  = 팀컬러 판정용 현대 10개 계보
```

## 11.2 현대 10개 구단 계보

아래는 게임용 정본 매핑이다.

| TeamColorLineage | 포함하는 Source 구단 역사 |
|---|---|
| `KiaLineage` | 해태 → KIA |
| `LgLineage` | MBC → LG |
| `SsgLineage` | 쌍방울 → SK → SSG |
| `DoosanLineage` | OB → 두산 |
| `LotteLineage` | 롯데 |
| `SamsungLineage` | 삼성 |
| `HanwhaLineage` | 빙그레 → 한화 |
| `KiwoomLineage` | 삼미 → 청보 → 태평양 → 현대 → 히어로즈 → 넥센 → 키움 |
| `NcLineage` | NC |
| `KtLineage` | KT |

이 매핑은 게임 팀컬러 규칙이다. Source 데이터의 Franchise provenance를 병합하거나 실제 역사·법인 승계를 선언하는 용도로 사용하지 않는다.

## 11.3 Bake 검증

- Production에 포함되는 모든 Source TeamSeason은 정확히 하나의 TeamColorLineage에 매핑되어야 한다.
- 한 Source Franchise가 여러 Lineage에 중복 매핑되면 실패한다.
- NC·KT에 창단 이전 임의 연도를 생성하지 않는다.
- 팀명 문자열이 아니라 Stable `FranchiseId`/`TeamSeasonKey`로 매핑한다.

---

## 12. 팀컬러 판정 확장

기존 `TeamColorEligibilityKey`를 다음처럼 확장한다.

```text
CardTeamColorEligibility
{
    OriginYear
    OriginFranchiseId
    OriginTeamSeasonKey
    TeamColorLineageId
    Edition

    AffinityMode
      FixedOrigin
      FranchiseWildcard
}
```

### 12.1 카드별 Affinity

| Edition | AffinityMode |
|---|---|
| Normal | FixedOrigin |
| Rare | FixedOrigin |
| EX | FixedOrigin |
| AllStar | FixedOrigin |
| GoldenGlove | FixedOrigin |
| Mvp | FixedOrigin |
| CareerHigh | FranchiseWildcard |
| Legend | FranchiseWildcard |

### 12.2 Franchise 팀컬러

모든 카드는 자신의 `TeamColorLineageId`와 같은 Franchise 팀컬러 조건에 포함된다.

```text
KiaLineage의 해태 카드
→ KiaLineage Franchise TeamColor 인정

KiaLineage의 KIA 카드
→ 동일하게 인정
```

### 12.3 YearFranchise 팀컬러

FixedOrigin 카드:

```text
TeamColorLineageId 일치
AND OriginYear 일치
```

Legend/CareerHigh:

```text
TeamColorLineageId 일치
→ 해당 Lineage의 어느 OriginYear YearFranchise에도 1명으로 인정
```

예:

```text
KiaLineage Legend
→ 1988 해태 YearFranchise 인정
→ 2009 KIA YearFranchise 인정
→ 2017 KIA YearFranchise 인정
→ LgLineage YearFranchise 불인정
```

### 12.4 순수 Year 팀컬러

Legend/CareerHigh가 모든 연도의 순수 Year TeamColor까지 만족해서는 안 된다.

```text
Year Family 판정
→ BasePlayerSeasonId.OriginYear만 사용
```

### 12.5 수상 Edition 팀컬러

Legend/CareerHigh는 자동으로 AllStar/GoldenGlove/Mvp TeamColor 인원에 포함되지 않는다.

```text
Edition == AllStar      일 때만 AllStar 인원
Edition == GoldenGlove  일 때만 GG 인원
Edition == Mvp          일 때만 MVP 인원
```

프랜차이즈 와일드카드는 정체성 계열 팀컬러에만 적용한다.

### 12.6 Origin 불변

팀컬러를 위해 다음을 절대 변경하지 않는다.

```text
OriginYear
OriginFranchiseId
OriginTeamSeasonKey
```

Wildcard는 Resolver가 해석하는 별도 Eligibility다.

---

## 13. 스킬블록 ×2 계약

## 13.1 적용 대상

```text
Rare → 2.0
EX   → 2.0
그 외 Edition → 1.0
```

## 13.2 적용 범위

스킬블록의 **수치형 능력치 보너스**를 배율 적용한다.

```text
SkillBlock.AbilityBonuses[]
→ Rare/EX일 때 각 수치 ×2
```

예:

```text
Contact +2 / Power +1

Normal 적용
Contact +2 / Power +1

Rare·EX 적용
Contact +4 / Power +2
```

배율 적용 대상이 아닌 것:

- 블록 모양.
- 회전 가능 여부.
- 판매 가치.
- 고유 보상 여부.
- 소켓 규칙.
- Trait의 존재 자체.
- Trigger 횟수.
- 슬롯 수 또는 슬롯 해방.

Trait에 수치 Magnitude가 있다면 `IsSkillMagnitudeScalable`로 명시된 값만 배율 적용한다. 모든 Trait를 무조건 두 배로 실행하지 않는다.

## 13.3 적용 순서

```text
장착 SkillBlock 수집
→ 중복/배치 유효성 검사
→ 블록별 수치 보너스 계산
→ Card Edition SkillMultiplier 적용
→ Skill 총합 확정
→ 다른 EffectiveRating 레이어와 합성
```

한 블록에 배율이 두 번 적용되지 않도록 단일 `CardSkillEffectResolver`에서 처리한다.

## 13.4 스킬블록 해방 금지

본 기능에 다음 타입이나 State를 추가하지 않는다.

```text
SkillSlotUnlockState
SkillBlockUnlockProgress
AllSkillSlotsUnlocked
UnlockLevel
UnlockExperience
```

기존 코드에 Legacy 필드가 있더라도 Rare/EX 효과 계산에 사용하지 않는다.

---

## 14. 능력치 레이어

구단주 모드 Owned 카드의 권장 합성 순서:

```text
BasePlayerSeason.BaseAttributes
+ EditionStatModifier
+ CardTrainingBonus
+ CardEnhancement
+ ScaledSkillBlockBonus
+ TeamColorBonus
+ ConditionBonus
+ TacticCardBonus
→ SoftCap Curve
→ HardCap
→ EffectiveRating
```

중요:

- Rare/EX ×2는 `ScaledSkillBlockBonus`에만 적용한다.
- CareerHigh/Legend FranchiseWildcard는 TeamColor 자격에만 적용한다.
- EditionStatModifier는 BaseAttributes를 직접 수정하지 않는다.
- Cost는 위 합성 결과로 재계산하지 않는다.

---

## 15. WorldCardCatalog와 획득 정책

## 15.1 Catalog 계층

```text
BakedCardCatalog
├─ ContentDefined
│  ├─ Normal
│  ├─ Rare
│  ├─ EX
│  ├─ CareerHigh
│  └─ Legend
└─ WorldAwardDefined
   ├─ AllStar
   ├─ GoldenGlove
   └─ Mvp
```

모든 Definition은 사전 Bake된다. `WorldAwardRecord`는 Award Edition의 Availability만 활성화한다.

## 15.2 획득 경로 매트릭스

| Edition | 일반 Scout | Award Scout | Pity | 일반 합성 | 전용 영입/합성 |
|---|---:|---:|---:|---:|---:|
| Normal | O | X | O | O | X |
| Rare | O | X | X | O | X |
| EX | X | X | X | **O** | X |
| AllStar | 기존 정책 | O | 기존 정책 | 데이터 정책 | X |
| GoldenGlove | 기존 정책 | O | 기존 정책 | 데이터 정책 | X |
| Mvp | 기존 정책 | O | 기존 정책 | 데이터 정책 | X |
| CareerHigh | X | X | X | X | **CareerHigh 합성** |
| Legend | X | X | X | X | **Legend 영입** |

`AcquisitionPolicy`는 UI가 아니라 Core/Simulation Resolver가 강제한다.

---

## 16. Collection·중복·보호 정책

### 16.1 Normal/Rare/EX

- 같은 CardId 중복 획득 가능.
- 기존 강화 +0~+5 규칙 사용 가능.
- 첫 Rare/EX 획득은 자동 잠금.
- MAX 이후 자동 판매 정책은 별도 설정에서 허용할 수 있으나 기본값은 보호.

### 16.2 CareerHigh/Legend

- `UniqueSpecialCard = true`.
- 동일 CardId 중복 획득 불가.
- 강화 재료, 판매, 일반 합성 재료 사용 불가.
- 소유 상태에서 영구 보호.
- CardTraining은 기존 TrainingCeiling을 넘지 않는 범위에서 허용한다.

### 16.3 위시리스트·재료 보호

도감 위시리스트에 등록된 카드는 자동 합성·자동 판매·자동 재료 배치에서 제외한다.

Legend 재료 후보 카드에는 `L` 재료 배지를, CareerHigh 가능 시즌 카드에는 `CH` 재료 배지를 표시한다.

한 카드가 여러 Recipe의 후보라면 Tooltip에 모두 표시하되, 하나의 Owned Instance를 동시에 두 Recipe에 예약하지 못한다.

---

## 17. 메뉴 구조

기존 공용 `SharedGameShellView`와 구단주 모드 Navigation을 재사용한다.

```text
구단주 모드
└─ 선수
   ├─ 선수단
   ├─ 도감
   ├─ 스카우트
   ├─ 전력 보강
   │  └─ 선수 합성          // EX 유일 획득 경로
   └─ 특수 영입
      ├─ 레전드
      └─ 커리어 하이
```

탭 이동 시 화면별 상태는 유지하되, 재료 보유 수와 Recipe 유효성은 현재 Collection 기준으로 다시 계산한다.

---

## 18. 특수 영입 공통 UI Shell

Legend와 CareerHigh는 하나의 `SpecialRecruitScene`을 공유하고 내부 탭만 분리한다.

```text
┌ 상단 공용 Shell ─────────────────────────────────────────┐
│ 구단 / 리그 / Money / SP / DP / 알림                     │
├───────────────────────────────────────────────────────────┤
│ [레전드] [커리어 하이]                                   │
├──────────────┬──────────────────────┬─────────────────────┤
│ Lineage 목록 │ 대상 카드 Preview    │ 재료/진행 패널      │
│ 대상 목록    │ 능력치/효과/설명     │                     │
├──────────────┴──────────────────────┴─────────────────────┤
│ 하단 안내 / 자동 배치 / 취소 / 최종 영입                 │
└───────────────────────────────────────────────────────────┘
```

공통 원칙:

- 실제 판정은 ViewModel/Service에서 수행하고 UI는 표시·명령 전달만 한다.
- 카드 상세는 기존 카드 Popup을 재사용한다.
- 보유 수, 잠금, 장착, 강화, 훈련 여부를 동시에 보여준다.
- 재료가 부족한 이유를 텍스트로 설명한다.
- 색상만으로 완료 여부를 표시하지 않고 아이콘·텍스트·채움 상태를 함께 사용한다.

---

## 19. Legend 영입 UI

## 19.1 화면 구성

```text
┌ 특수 영입 > 레전드 ───────────────────────────────────────┐
│ [KIA] [LG] [SSG] [두산] [롯데] [삼성] [한화] [키움] ... │
├──────────────┬──────────────────────┬─────────────────────┤
│ 레전드 목록  │   큰 Legend 카드     │  ① ●  ② ●  ③ ○    │
│              │                      │  ④ ●  ⑤ ○  ⑥ ○    │
│ 준비 가능    │   베이스 연도        │  ⑦ ●  ⑧ ○          │
│ 진행 중      │   Cost               │                     │
│ 미보유       │   ALL +2             │ 선택 성구 후보      │
│ 보유 완료    │   Lineage Wildcard   │ [카드][카드][카드]  │
│              │                      │ 보유 5 / 8          │
├──────────────┴──────────────────────┴─────────────────────┤
│ [자동 배치] [예약 해제]                   [레전드 영입]     │
└───────────────────────────────────────────────────────────┘
```

### 19.2 8성구 표시

- 8개의 야구공/메달 슬롯을 사용한다.
- 빈 슬롯, 후보 보유, 예약 완료, 소모 완료를 구분한다.
- 슬롯 클릭 시 해당 Group의 Candidate Drawer를 연다.
- 후보별로 `보유 수 / 사용 가능 수 / 잠금 / 라인업 사용 / 훈련치`를 표시한다.

### 19.3 자동 배치

자동 배치는 다음 우선순위로 한 장을 선택한다.

```text
미사용 Normal
→ 강화 수치 낮음
→ CardTrainingBonus 낮음
→ 획득 시점 오래됨
→ OwnedCardInstanceId Stable 순서
```

위시리스트, 잠금, 장착, 다른 Recipe 예약 카드는 제외한다.

### 19.4 최종 확인

```text
소모 카드 8장
획득 Legend 1장
되돌릴 수 없음
```

을 한 화면에서 보여주고 명시적 확인을 요구한다.

---

## 20. CareerHigh 합성 UI

## 20.1 화면 구성

```text
┌ 특수 영입 > 커리어 하이 ──────────────────────────────────┐
│ Lineage: KIA                                             │
├──────────────┬──────────────────────┬─────────────────────┤
│ 대상 선수    │ CareerHigh Preview   │ 커리어 시즌 타임라인│
│              │                      │ 05 06 07 08 09 ... │
│ 8년 충족     │ 베이스: 2010 Cost 10 │ [●][●][○][●] ...   │
│ 9/10 Cost    │ ALL +2               │                     │
│ 준비 가능    │ Lineage Wildcard     │ 선택 6 / 8          │
│ 보유 완료    │                      │                     │
├──────────────┴──────────────────────┴─────────────────────┤
│ 선택 재료: 8개의 서로 다른 연도 Normal 카드              │
│ [자동 선택] [전체 해제]                 [커리어하이 합성]  │
└───────────────────────────────────────────────────────────┘
```

### 20.2 커리어 타임라인

각 연도를 다음 상태로 표시한다.

```text
유효 시즌 + 카드 보유
유효 시즌 + 카드 미보유
유효하지 않은 짧은 표본 시즌
다른 Lineage 시즌
Peak Base 시즌
이미 선택한 재료 시즌
```

Peak Base 시즌은 별도 왕관/`BASE` 아이콘으로 표시한다.

### 20.3 8장 선택

- 같은 연도 Copy가 여러 장이면 세부 선택 Popup을 제공한다.
- 자동 선택은 투자 값이 가장 낮은 Copy를 고른다.
- 8개 미만이면 버튼 비활성화와 함께 부족 연도를 안내한다.
- 8개를 넘겨 선택할 수 없다.
- 동일 연도 중복 선택은 즉시 거부한다.

### 20.4 프랜차이즈 팀컬러 Preview

상세 패널에 다음을 표시한다.

```text
프랜차이즈 와일드카드
적용 Lineage: KiaLineage

적용 가능
- 해태/KIA Franchise 팀컬러
- 모든 해태/KIA YearFranchise 팀컬러

적용 불가
- 다른 Lineage 팀컬러
- 모든 연도의 순수 Year 팀컬러
- AllStar/GG/MVP 인원 조건
```

---

## 21. EX 선수 합성 UI

기존 `전력 보강` 메뉴를 사용한다.

```text
┌ 전력 보강 > 선수 합성 ────────────────────────────────────┐
│ 보유 카드 필터                                            │
│ [Normal] [Rare] [수상] [잠금 제외] [위시 제외]            │
├───────────────────────────────────────────────────────────┤
│ 재료 1  재료 2  재료 3  재료 4  재료 5                   │
│ [카드]  [카드]  [카드]  [카드]  [카드]                   │
├───────────────────────────────────────────────────────────┤
│ 재료 점수       78                                        │
│ 결과 Cost 범위  7~10                                      │
│ Rare 가능       2.4%                                      │
│ EX 가능         0.15%                                     │
│ ※ 실제 수치는 현재 비어 있지 않은 Bucket으로 재정규화     │
├───────────────────────────────────────────────────────────┤
│                                            [선수 합성]     │
└───────────────────────────────────────────────────────────┘
```

표시 확률은 예시가 아니라 현재 Balance와 후보 Bucket을 이용해 계산한 실제 확률만 표시한다.

EX가 등장하면 별도 결과 연출을 사용한다.

```text
일반 결과 → 표준 카드 플립
Rare 결과 → RARE 강조
EX 결과   → EX 전용 지연·조명·엠블럼 후 카드 공개
```

연출이 결과 RNG를 다시 굴리거나 Skip 여부에 따라 결과가 바뀌어서는 안 된다.

---

## 22. 카드 UI 표시 규칙

## 22.1 전면

공통 표시:

```text
World DisplayName
OriginYear
Position/Role
Cost
Edition Emblem
TeamColorLineage Emblem
```

Edition별 추가 표시:

- Rare: `RARE`, `SKILL ×2`.
- EX: `EX`, `SKILL ×2`, `합성 전용` Tooltip.
- CareerHigh: `CH`, `Franchise Wildcard`, Peak Base 연도.
- Legend: `LEGEND`, `Franchise Wildcard`, Peak Base 연도.

## 22.2 후면/상세

```text
베이스 PlayerSeason
베이스 OriginYear
Cost
Edition 효과
SkillBlock 배율
적용 TeamColorLineage
획득 경로
합성/영입 보호 상태
동일 인물 다른 Edition 바로가기
```

CareerHigh 상세에는 유효 커리어 시즌 수를, Legend 상세에는 Recipe 완료 상태를 표시한다.

## 22.3 재료 배지

- Legend Recipe 후보: `L` 배지.
- CareerHigh 유효 시즌 카드: `CH` 배지.
- 둘 다 해당하면 복합 Tooltip을 표시한다.
- 배지 때문에 카드 핵심 Cost·연도·포지션이 가려지지 않게 우측 상단/하단 고정 영역을 사용한다.

---

## 23. Reference UI Assets

본 패키지에는 다음 프로젝트 보유 레퍼런스를 포함한다.

### 23.1 공용 Shell·탭 밀도

![구단 정보 화면 레퍼런스](references/구단정보_ref2.png)

활용 포인트:

- 상단 1차 탭 + 내부 2차 탭 구조.
- 좌/중/우 정보 패널 구획.
- PC 16:9 관리형 UI의 높은 정보 밀도.

### 23.2 특수 영입 결과 Popup

![영입 결과 화면 레퍼런스](references/스카우트_ref.png)

활용 포인트:

- 좌측 대형 카드, 우측 결과·효과 요약.
- 획득 전/후 변화 비교.
- 확인 버튼이 분리된 명확한 완료 상태.

### 23.3 카드 전면·후면

![카드 전후면 레퍼런스](references/카드_디자인_ref.png)

![타자 카드 레퍼런스](references/타자_카드_ref.png)

활용 포인트:

- 전면은 인물·Cost·주요 능력치 중심.
- 후면은 기록, 스킬블록, 특성, 상세 정보 중심.
- 원작 픽셀을 직접 복제하지 않고 정보 계층과 조작 흐름만 참고한다.

### 23.4 ImageGen 사용 조건

기존 프레임으로 구분이 부족한 경우에만 다음 비기능성 에셋을 생성한다.

```text
Rare/EX/CH/Legend 엠블럼 컨셉
Legend 영입실 배경
CareerHigh 커리어 타임라인 장식
EX 합성 결과 조명/입자 Sprite
```

금지:

- 실제 선수 얼굴·팀 로고 직접 생성.
- 원작 카드 프레임의 픽셀 단위 복제.
- 버튼 텍스트와 수치를 이미지에 굽기.
- 팀 컬러로 오해될 단일 강색을 모든 Edition에 공통 사용.

---

## 24. Save / Persistence

## 24.1 Save 대상

```text
Owned Card Instance IDs / CardIds
Enhancement / CardTraining State
LegendRecruitProgress
CareerHighSynthesisProgress
Reserved Material Card Instance IDs
Combination RNG Sequence / Receipt
획득 완료 Unique Card IDs
```

Definition과 Recipe 전체를 Save에 복제하지 않는다. Content Hash와 Stable ID를 저장한다.

## 24.2 Legend 진행 상태

```text
LegendRecruitProgress
{
    TargetLegendCardId
    AssignedMaterialByGroup[8]
    Completed
}
```

## 24.3 CareerHigh 진행 상태

```text
CareerHighSynthesisProgress
{
    TargetCareerHighCardId
    SelectedMaterialInstanceIds[0..8]
    Completed
}
```

## 24.4 Transaction

Legend/CH 최종 영입과 EX 합성은 원자적 Transaction이다.

```text
입력 검증
→ 재료 잠금 확인
→ 결과 Card Definition 확인
→ 재료 소모
→ 결과 지급
→ Receipt 기록
→ Save 가능 상태 커밋
```

중간 실패 시 재료와 결과가 모두 원상복구되어야 한다.

## 24.5 Load

Load에서 다음을 재실행하지 않는다.

- Bake.
- EX 선정.
- Peak Base 시즌 선정.
- Legend Recipe 생성.
- CareerHigh 유효 시즌 계산.
- 이미 완료한 합성 RNG.

저장된 CardId와 Progress를 Baked Catalog에 다시 연결한다.

## 24.6 Content Version 변경

Recipe가 바뀐 경우 기존 예약 재료를 조용히 삭제하지 않는다.

```text
기존 Recipe Version 불일치
→ 진행 상태 Migration 시도
→ 더 이상 유효하지 않은 예약은 잠금 해제
→ 사용자에게 재료 변경 안내
→ 이미 소모 완료된 영입은 유지
```

---

## 25. Offline Bake와 Editor 도구

## 25.1 Special Card Baker

```text
SpecialCardCatalogBaker
{
    BuildTeamColorLineages()
    BuildIssuedCardStrengthScores()
    BuildRareCandidates()
    SelectExByYearAndRole()
    BuildCareerHighCandidatesAndRecipes()
    BuildLegendDefinitionsAndRecipes()
    BuildBakedCardDefinitions()
    Validate()
    WriteReports()
}
```

## 25.2 Editor Browser

UI Toolkit 기반 Editor 도구를 별도로 제공한다. 이는 Runtime uGUI와 혼합하지 않는다.

탭:

```text
Overview
Rare Candidates
EX by Year
CareerHigh by Lineage
Legend Curation
Recipes
Validation
Diff
```

필수 기능:

- Cost, BaseAttributes, Strength Score, Source 시즌 성적 Trace 표시.
- 같은 Person의 연도별 카드 비교.
- Peak Base 자동 강조.
- CareerHigh 유효 시즌 수와 8년 Gate 표시.
- EX 연도별 타자/투수 1위와 2위 비교.
- Legend Curation 후보 승인/해제.
- Recipe 재료 중복·부족 검사.
- 이전 Content Version과 결과 Diff.

## 25.3 필수 Bake 산출물

```text
Generated/SpecialCards/BakedCardCatalog.json
Generated/SpecialCards/SpecialCardRecipes.json
Generated/SpecialCards/TeamColorLineages.json
Generated/SpecialCards/SpecialCardManifest.json
Generated/SpecialCards/SpecialCardValidationReport.json

Generated/SpecialCards/EX_By_Year.csv
Generated/SpecialCards/EX_By_Year.md
Generated/SpecialCards/Rare_By_Year_Team.csv
Generated/SpecialCards/Legend_By_Lineage.csv
Generated/SpecialCards/Legend_By_Lineage.md
Generated/SpecialCards/CareerHigh_By_Lineage.csv
Generated/SpecialCards/CareerHigh_By_Lineage.md
```

### 25.4 선수 목록을 문서에 수기로 고정하지 않는 이유

최종 EX·Legend·CareerHigh 목록은 현재 프로젝트의 **실제 Baked Cost와 최종 발급 스탯**에 의존한다. 본 문서에 첨부된 자료에는 모든 PlayerSeason의 최신 Cost/Strength Trace가 포함되어 있지 않다.

따라서 실명을 추정해 정본 목록으로 적지 않는다. 위 Bake 산출물이 다음 목록의 유일한 Production Source of Truth가 된다.

- 연도별 EX 타자 1명·투수 1명.
- Lineage별 Legend 목록.
- Lineage별 CareerHigh 목록.
- 연도·구단별 Rare 목록.

Editor Report에는 검수용 Source 이름을 표시할 수 있으나 Runtime Catalog에는 Source 이름을 넣지 않는다.

---

## 26. 콘텐츠 선정 세부 Gate

## 26.1 Rare

```text
Cost in {4,5}
AND explicit RareEligibility
```

## 26.2 EX

```text
Role별 ExSeasonPerformanceIndex 1위
AND Cost == 10
AND 연도별 Hitter/Pitcher 각각 정확히 1명
```

## 26.3 CareerHigh

```text
같은 Person + 같은 Lineage
AND QualifiedDistinctYears >= 8
AND Peak Issued Card Cost in {9,10}
```

## 26.4 Legend

```text
Explicit Legend Curation
AND 같은 Lineage Peak Issued Card 존재
AND Peak Cost in {9,10}
```

---

## 27. 필수 자동 테스트

### A. 사전 Bake

1. 모든 Runtime CardId가 BakedCardCatalog에 존재한다.
2. Runtime에서 Card Definition 생성 호출이 0회다.
3. 동일 Source/Balance/Content Version에서 Catalog Hash가 동일하다.
4. World Seed 변경으로 Rare/EX/CH/Legend Definition이 변하지 않는다.

### B. Normal 공존

5. Rare가 존재하는 PlayerSeason에 Normal이 존재한다.
6. EX가 존재하는 PlayerSeason에 Normal이 존재한다.
7. Normal과 Rare/EX CardId가 서로 다르다.

### C. Cost Gate

8. 모든 Rare Base Cost가 4 또는 5다.
9. 모든 EX Base Cost가 정확히 10이다.
10. 모든 CareerHigh Base Cost가 9 또는 10이다.
11. 모든 Legend Base Cost가 9 또는 10이다.
12. Edition 때문에 Cost를 재계산하거나 변경한 사례가 0건이다.

### D. EX 선정

13. 각 지원 연도에 EX Hitter가 정확히 1명이다.
14. 각 지원 연도에 EX Pitcher가 정확히 1명이다.
15. 선택된 선수는 역할별 ExSeasonPerformanceIndex 1위다.
16. 1위가 Cost 10이 아니면 Bake가 실패한다.
17. 2위 Cost 10으로 조용히 대체하지 않는다.
18. Source Award/MVP 플래그만으로 EX를 선정하지 않는다.

### E. CareerHigh

19. 모든 CareerHigh는 한 Lineage에서 유효 시즌 8개 이상이다.
20. 다른 Lineage 시즌 합산으로 8년을 채우지 않는다.
21. PeakIssuedCardSelector 결과가 BasePlayerSeasonId와 일치한다.
22. Peak Cost 8 이하 Person의 CareerHigh Definition이 0건이다.
23. 합성 재료는 같은 Person이다.
24. 합성 재료는 같은 Lineage다.
25. 합성 재료 OriginYear가 8개 모두 다르다.
26. 같은 연도 Copy 두 장으로 두 슬롯을 채울 수 없다.
27. Normal 이외 Edition을 재료로 사용할 수 없다.
28. 7장 상태에서는 합성할 수 없다.
29. 정확히 8장 소모 후 1장을 지급한다.

### F. Legend

30. Legend Curation 없는 카드 생성이 0건이다.
31. PeakIssuedCardSelector 결과를 베이스로 사용한다.
32. 8 Material Group이 모두 존재한다.
33. Group마다 후보가 최소 1장 존재한다.
34. 같은 Owned Instance가 두 Group을 충족하지 못한다.
35. 7/8 상태에서는 영입할 수 없다.
36. 8/8 완료 후 8장 소모와 1장 지급이 원자적이다.

### G. SkillBlock

37. Rare의 수치형 SkillBlock 효과가 Normal의 정확히 2배다.
38. EX의 수치형 SkillBlock 효과가 Normal의 정확히 2배다.
39. 배율이 두 번 적용되지 않는다.
40. TeamColor/CardTraining/Enhancement가 SkillMultiplier에 곱해지지 않는다.
41. 비확장 Trait가 두 번 발동하지 않는다.
42. Skill Unlock State를 새로 생성하지 않는다.

### H. 획득 경로

43. 모든 Scout 결과에서 EX가 0건이다.
44. Pity 결과에서 EX가 0건이다.
45. Award Scout 결과에서 EX가 0건이다.
46. Mission/direct reward 경로에서 EX가 0건이다.
47. Production에서 EX 획득 Entry Point가 CardCombinationResolver뿐이다.
48. Legend/CH가 일반 Scout와 일반 합성에 등장하지 않는다.

### I. TeamColorLineage

49. 모든 Production Source Franchise가 정확히 한 Lineage에 매핑된다.
50. KIA Lineage가 해태/KIA를 포함한다.
51. LG Lineage가 MBC/LG를 포함한다.
52. SSG Lineage가 쌍방울/SK/SSG를 포함한다.
53. 두산 Lineage가 OB/두산을 포함한다.
54. 한화 Lineage가 빙그레/한화를 포함한다.
55. 키움 Lineage가 삼미/청보/태평양/현대/히어로즈 계열을 포함한다.
56. NC·KT에 창단 이전 Source TeamSeason을 임의 매핑하지 않는다.

### J. Wildcard

57. Legend가 같은 Lineage의 모든 YearFranchise 조건에 포함된다.
58. CareerHigh도 동일하게 포함된다.
59. 다른 Lineage 조건에는 포함되지 않는다.
60. 순수 Year TeamColor에는 Base OriginYear만 사용한다.
61. AllStar/GG/MVP 조건에 자동 포함되지 않는다.
62. Wildcard 적용으로 Canonical Origin이 변경되지 않는다.

### K. Ownership/Persistence

63. 동일 Person 여러 Edition 보유는 가능하다.
64. 동일 Person 여러 Edition을 ActiveRoster에 동시에 등록할 수 없다.
65. Legend/CH 중복 소유가 거부된다.
66. Legend/CH 자동 판매·일반 합성 재료 사용이 거부된다.
67. Save/Load 후 Recipe 예약 상태가 동일하다.
68. Save/Load 후 합성 RNG 결과를 다시 굴리지 않는다.
69. 중복 Receipt로 재료가 두 번 소모되지 않는다.

### L. Identity

70. World DisplayName 변경으로 선정, Cost, TeamColor, 합성 결과가 변하지 않는다.
71. 같은 Person의 모든 Edition이 같은 World DisplayName을 표시한다.
72. Source 실제 이름이 Runtime Catalog에 포함되지 않는다.

---

## 28. 밸런스 검증

### 28.1 Rare

- Cost 4~5 Rare가 Skill ×2로 Cost 9~10 기본 카드를 상시 압도하지 않는지 검증한다.
- 적절한 스킬 세팅 시 특정 역할에서 경쟁력이 생기되 모든 상황의 정답이 되지 않게 한다.
- Rare 과다 공급으로 Normal 4~5 Cost가 무가치해지지 않는지 확인한다.

### 28.2 EX

- Cost 10 + Skill ×2의 EffectiveRating 분포가 HardCap에 과도하게 포화되지 않는지 검증한다.
- EX 한 장이 TeamColor·라인업 선택을 무시하고 승률을 자동 결정하지 않게 한다.
- 합성 기대 비용과 등장 확률을 측정한다.

### 28.3 CareerHigh/Legend

- `ALL +2 + FranchiseWildcard`의 합이 단일연도 덱 약점을 지나치게 제거하지 않는지 확인한다.
- ActiveRoster에 몇 장까지 배치될 수 있는지 장기 전력 분포를 본다.
- 본 문서에서는 별도 편성 수량 제한을 두지 않지만, 테스트 결과 필요하면 `SpecialWildcardRosterCap`을 Balance로 추가한다.
- 제한 추가 시 CareerHigh와 Legend를 합산해 판정하고 Source 문서에 명시한다.

### 28.4 경제

- CareerHigh 8년 카드 소비가 너무 쉽거나 사실상 불가능하지 않은지 확인한다.
- Legend 8성구 Recipe의 평균 완료 기간을 측정한다.
- EX 합성 때문에 카드 인벤토리가 지나치게 빨리 소각되지 않는지 확인한다.
- Rare/EX 자동 보호가 Collection 관리 피로를 과도하게 만들지 않는지 확인한다.

---

## 29. 구현 단계

## Phase S0 — Repository Audit

멀티 에이전트로 다음을 병렬 조사한다.

```text
Audit-A Card/Edition/CardId/WorldCardCatalog
Audit-B Cost Bake/Strength Trace/PlayerSeason
Audit-C SkillBlock 적용 경로
Audit-D TeamColorEligibility/FranchiseId
Audit-E Scout/Combination/Owned Economy
Audit-F Save/Persistence
Audit-G Production uGUI/도감/선수단/스카우트 화면
Audit-H Editor Browser/Bake Pipeline
```

산출물:

```text
재사용 타입
변경해야 할 enum/schema
중복 위험
Production 호출 그래프
Save version
변경 파일 목록
테스트 기준선
```

## Phase S1 — Canonical Contract와 Schema

- `BakedCardDefinition`.
- `CardEdition` 확장.
- `AcquisitionPolicy`.
- `CardTeamColorAffinity`.
- `TeamColorLineageDefinition`.
- `IssuedCardStrengthScore` 보존.
- `CardId` VariantKey 계약.

Gate:

- Core/Simulation Unity 비의존.
- Cost/Origin 불변 테스트 통과.

## Phase S2 — Offline Bake

병렬 Lane:

```text
Lane-R Rare Candidate/Curation
Lane-X EX Selection
Lane-C CareerHigh Eligibility/Recipe
Lane-L Legend Curation/Recipe
Lane-T TeamColorLineage Mapping
```

Orchestrator가 최종 Catalog와 Manifest를 병합한다.

## Phase S3 — Runtime Resolver

- Card Edition Effect Resolver.
- SkillBlock ×2 단일 적용.
- TeamColor Wildcard 판정.
- AcquisitionPolicy 강제.
- Unique ownership.

## Phase S4 — Acquisition

병렬:

```text
EX CardCombinationResolver
LegendRecruitService
CareerHighSynthesisService
Reservation/Transaction Service
```

## Phase S5 — Persistence

- Save schema 명시적 버전 업.
- Recipe progress.
- Reservation.
- Combination receipt.
- v1~기존 Save migration.

## Phase S6 — UI

- 선수 합성.
- Legend 영입.
- CareerHigh 합성.
- 도감/카드 상세 Edition 필터.
- Recipe 배지.
- 결과 Popup.

Production UI는 기존 `SharedGameShellView` 기반 uGUI를 재사용한다. Editor Browser만 UI Toolkit을 사용한다.

## Phase S7 — Content Bake

- 실제 1982~현재 Archive 실행.
- EX 연도별 2장 검증.
- Legend Curation Cost Gate.
- CareerHigh 8년·Cost Gate.
- Rare 분포 검수.
- MD/CSV 목록 출력.

## Phase S8 — Validation

- Fast Rule.
- Determinism.
- Persistence.
- Statistical balance.
- Production E2E.
- 16:9 UI 조작.
- 독립 Review.

---

## 30. Production E2E

최소 다음 시나리오를 통과해야 한다.

### 30.1 EX

```text
Normal 카드 5장 선택
→ 확률 Preview
→ 합성 확정
→ 재료 5장 소모
→ 결과 CardId 지급
→ EX 결과일 때 Baked EX Definition 확인
→ Save
→ Load
→ 같은 결과와 Receipt 유지
```

### 30.2 Legend

```text
특수 영입 > Legend
→ Lineage 선택
→ 대상 Legend 선택
→ 8 Material Group 확인
→ 후보 카드 배치
→ 7/8 영입 거부
→ 8/8 최종 확인
→ 8장 소모
→ Legend 지급
→ 같은 Lineage YearFranchise 팀컬러 확인
→ Save/Load
```

### 30.3 CareerHigh

```text
특수 영입 > CareerHigh
→ 유효 시즌 8년 이상 대상 선택
→ Peak Base 시즌 확인
→ 서로 다른 8개 연도 Normal 카드 선택
→ 중복 연도 거부
→ 8장 소모
→ CareerHigh 지급
→ 같은 Lineage 모든 YearFranchise 팀컬러 확인
→ Save/Load
```

### 30.4 SkillBlock

```text
같은 PlayerSeason Normal/Rare 또는 Normal/EX에 같은 블록 장착
→ Numeric Bonus 비교
→ 정확히 2배
→ 경기 Snapshot
→ EffectiveRating에서 한 번만 반영
```

---

## 31. 문서 동시 수정 범위

본 기능을 구현할 때 다음 문서를 한 변경 단위로 갱신한다.

```text
README.md
03_선수풀_카드_Cost_Edition.md
04_팀컬러_시스템.md
05_구단주모드_경제_스카우트.md
07_선수모드_연동_와일드카드.md
08_구현_로드맵_검증기준.md
14_특수카드_EX_Rare_CareerHigh_Legend.md
```

필수 변경:

- 03: Edition과 BakedCardCatalog 구조 확장.
- 04: `TeamColorLineageId`와 `FranchiseWildcard` 추가.
- 05: Scout에서 EX/Legend/CH 제외, Rare Weight와 합성 연결.
- 07: 선수 커리어 모드 일반 선수에게 Owner Owned Card State가 유출되지 않음을 유지.
- 08: Offline Bake → Acquisition → UI → E2E Gate 추가.

---

## 32. 절대 실패 조건

아래 하나라도 참이면 완료가 아니다.

- Runtime에서 Rare/EX/CH/Legend Definition을 생성한다.
- EX 선정 때문에 Cost를 10으로 바꾼다.
- CareerHigh/Legend 선정 때문에 Peak가 아닌 다른 9/10 Cost 시즌을 베이스로 쓴다.
- Rare에 Cost 3 또는 6 이상 카드가 존재한다.
- EX에 Cost 9 이하 카드가 존재한다.
- CareerHigh/Legend Base Cost가 8 이하이다.
- 한 연도에 EX 타자 또는 투수가 0명/2명 이상이다.
- 최고 성적 1위가 아닌 Cost 10 선수를 EX로 대체한다.
- Scout/Pity에서 EX가 나온다.
- Rare/EX SkillBlock 효과가 2배가 아니거나 두 번 적용된다.
- Skill Unlock 시스템을 새로 도입한다.
- CareerHigh가 서로 다른 연도 8장을 요구하지 않는다.
- 다른 Lineage의 시즌으로 CareerHigh 8년을 채운다.
- Legend/CH 팀컬러를 위해 Canonical Origin을 변조한다.
- Legend/CH가 모든 순수 Year 팀컬러나 수상 팀컬러에 자동 포함된다.
- Source 이름을 Runtime 카드 Definition에 저장한다.
- Save/Load 후 재료가 중복 소모되거나 합성 결과가 다시 굴러간다.

---

## 33. 최종 완료 정의

다음을 모두 만족해야 완료다.

```text
모든 카드 사전 Bake 완료
Cost Gate 완료
Peak Base 선정 완료
EX 연도별 Hitter/Pitcher 1명 완료
CareerHigh 8년·8장 Recipe 완료
Legend 8성구 Recipe 완료
TeamColorLineage 완료
FranchiseWildcard 완료
Rare/EX Skill ×2 완료
EX 합성 전용 경로 완료
Legend/CH UI 완료
도감/선수단 표시 완료
Persistence 완료
Fast/Determinism/E2E 완료
장기 밸런스 실행 완료
실제 Baked 선수 목록 MD/CSV 출력 완료
독립 Review Critical 0 / Major 0
관련 문서 갱신 완료
```

Definition만 추가하고 실제 Bake 목록·획득·팀컬러·UI가 연결되지 않았으면 부분 완료다.

---

## 부록 A. 개념 데이터 예시

```json
{
  "cardId": "card:ps_2010_0001:ex",
  "playerPersonId": "person_0001",
  "basePlayerSeasonId": "ps_2010_0001",
  "edition": "Ex",
  "variantKey": "",
  "editionStatModifier": 0,
  "skillBlockEffectMultiplier": 2.0,
  "teamColorAffinity": {
    "mode": "FixedOrigin",
    "teamColorLineageId": "LotteLineage"
  },
  "acquisitionPolicy": ["CombinationOnly"],
  "isUniqueOwnedCard": false
}
```

```json
{
  "cardId": "card:ps_2012_0142:career_high:HanwhaLineage",
  "playerPersonId": "person_0142",
  "basePlayerSeasonId": "ps_2012_0142",
  "edition": "CareerHigh",
  "variantKey": "HanwhaLineage",
  "editionStatModifier": 2,
  "skillBlockEffectMultiplier": 1.0,
  "teamColorAffinity": {
    "mode": "FranchiseWildcard",
    "teamColorLineageId": "HanwhaLineage"
  },
  "acquisitionPolicy": ["CareerHighSynthesisOnly"],
  "isUniqueOwnedCard": true
}
```

```json
{
  "targetCareerHighCardId": "card:ps_2012_0142:career_high:HanwhaLineage",
  "playerPersonId": "person_0142",
  "teamColorLineageId": "HanwhaLineage",
  "requiredDistinctYears": 8,
  "qualifiedNormalCardIds": [
    "card:ps_2005_0142:normal",
    "card:ps_2006_0142:normal",
    "card:ps_2007_0142:normal",
    "card:ps_2008_0142:normal",
    "card:ps_2009_0142:normal",
    "card:ps_2010_0142:normal",
    "card:ps_2011_0142:normal",
    "card:ps_2012_0142:normal",
    "card:ps_2013_0142:normal"
  ]
}
```

---

## 부록 B. 레퍼런스와 프로젝트 변경 구분

```text
[Reference]
- EX가 전력보강/합성 계통의 희귀 결과였던 방향.
- Rare와 EX의 스킬 효과 배율 우대.
- CareerHigh가 개인 최고 시즌을 기반으로 한 카드였던 방향.
- Legend가 프랜차이즈 연도덱의 와일드카드 역할을 한 방향.

[ProjectDecision]
- EX에도 Normal 카드가 별도 존재.
- Rare는 Cost 4~5만 가능.
- EX는 Cost 10만 가능.
- 연도마다 EX 타자 1명·투수 1명 고정.
- CareerHigh/Legend는 Cost 9~10 Peak 카드만 가능.
- CareerHigh는 같은 선수의 서로 다른 연도 Normal 8장을 소비.
- CareerHigh도 Legend와 동일한 전체 Franchise Wildcard.
- 모든 Card Definition과 Recipe를 사전 Bake.
- SkillBlock Unlock 개념 완전 제외.
- 현대 10개 구단 중심 TeamColorLineage.
```
