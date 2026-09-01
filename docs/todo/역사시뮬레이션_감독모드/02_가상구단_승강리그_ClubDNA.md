# 02. 가상 구단, 승강 리그, Club DNA

## 0. 목적

01절에서 생성한 가상 선수들을 "가상 연도 구단"으로 묶고, 기존 `LeagueGrade` 승강 구조
위에서 자연스럽게 실력을 증명하게 만든다. 실제 KBO 구단·순위를 그대로 대응시키지 않는다.

## 1. TeamSeason — 핵심 단위

```text
FranchiseId    // 가상 구단 고유 ID (예: SEOUL_COMETS)
OriginYear     // 참조한 시대 (예: 2011) — 실제 연도지만 실제 그 해 그 팀을 의미하지 않음
TeamSeasonKey  = FranchiseId + "_" + OriginYear
```

`FranchiseId`는 완전 가상 이름/엠블럼/컬러를 쓴다(실제 구단명 대응 금지). `OriginYear`는
01절 Reference Pool의 시대 축을 의미할 뿐, "그 해 그 팀의 실제 성적"과는 무관하다.

## 2. Franchise Fingerprint 생성

한 가상 구단의 한 연도 스냅샷은 여러 실제 팀 시즌의 특성을 혼합해서 만든다(실제 팀 하나를
그대로 옮기지 않는다). **연도덱(04절 YearFranchise/Year 팀컬러)이 수집의 핵심 단위이므로,
`2011 TeamSeason`은 원칙적으로 `2011` 시즌 데이터 안에서만 여러 실제 팀·선수를 혼합한다.**
임의로 다른 연도의 특성을 섞으면 "2011"이라는 연도 라벨의 정체성이 희석된다. 예:

```text
Fingerprint = 30% 2011년 강한장타팀 + 30% 2011년 강한선발팀 + 20% 2011년 평균수비팀 + 20% 2011년 강한불펜팀
```

동일 연도 안에서 필요한 포지션·역할의 Reference가 부족할 때만 **보조로 ±1~2년 Era Pool**을
허용한다(예: 2011년 좌완 마무리 표본이 지나치게 적을 때만 2010·2012 표본을 소량 섞는 식).
이 보조 혼합 비율은 상한(예: 전체 혼합의 20% 이하)을 두고 `EraNormalizationConfig`(01절)에
데이터화한다. "1998년 공격 + 2016년 불펜"처럼 서로 다른 시대를 임의로 결합하는 방식은
쓰지 않는다.

Fingerprint는 01절 `SyntheticTeamGenerator`의 입력이 되어 해당 TeamSeason의 Core25(§3) 선수
분포(포지션별 Cost 분포 포함)를 결정한다.

## 3. Core25 — TeamSeason의 초기 로스터 (AI는 이후 계속 갱신한다)

```text
TeamSeasonDefinition
{
    TeamSeasonKey
    AllNormalCardIds[]   // 시즌 전체 선수풀, 권장 28~40명
    Core25CardIds[]      // 월드 생성 시점의 초기 1군, 정확히 25명
    ReferenceStrength     // Fingerprint 기반 원본 전력 점수 (검증용, 승격 강제에 쓰지 않음)
}
```

**Core25는 게임 시작(월드 생성) 시점의 초기값일 뿐이다.** 시즌이 진행되면 AI 구단의 실제
1군은 `CurrentRosterState`(05절 §7)로 별도 관리하며, AI는 트레이드·영입·방출로 로스터를
계속 바꾼다 — Core25 필드 자체를 갱신하지 않는다(Core25는 "이 TeamSeason이 처음에 어떻게
생성됐는가"의 기록으로 고정 보존한다).

Core25는 포지션이 정상적으로 구성되어야 한다(야수 14/투수 11 등 세부는 현재 로스터 규칙에
맞춘다). AI는 초기값 Core25에서 출발해 `CurrentRosterState`로 로스터를 계속 최적화하지만,
어느 시점에도 카드 강화·중복·스카우트는 존재하지 않는다(05절 §7 원칙).

## 4. Rookie → Galaxy 승강

기존 `LeagueGrade`(`BaseballManager_GAME_SYSTEM.md`에서 재사용 가능하다고 명시된 부분,
`BaseballManager_PROJECT.md` 7행 참고)를 그대로 확장한다.

```text
Rookie → Minor → Major → World → All-Star → Classic → Winners → Champion → Master → Galaxy
```

- **모든 TeamSeason은 Rookie에서 시작한다.** `ReferenceStrength`가 높다고 상위 리그에서
  시작시키지 않는다 — 실제 시뮬레이션 결과로만 승격한다.
- 승강 규칙(승격/유지/강등 순위 구간)은 `LeagueDefinition` 데이터로 관리하고 코드에
  하드코딩하지 않는다.
- 리그 등급 자체가 능력치를 보정하지 않는다(`Rookie니까 -30%` 금지). 상위 리그의 보상은
  시설 상한, 계약 오���, 감독 명성 등 **경제적 보상**으로만 준다(05절).

## 5. Club DNA — 두 개의 소유 단위로 분리

**주의:** 이 프로젝트의 세계에는 `2008 Comets`, `2011 Comets`, `2019 Comets`처럼 같은
Franchise의 서로 다른 TeamSeason이 **동시에** 존재하고 각자 독립적으로 시즌을 치른다.
"DNA를 Franchise(연도 무관) 하나가 소유하고 시즌마다 갱신한다"고 하면, 여러 TeamSeason의
결과가 하나의 DNA 값에 뒤섞여버린다. 따라서 소유 단위를 둘로 나눈다.

```text
FranchiseIdentityProfile   // Franchise 소유, 매우 느리게(수년 단위) 변하는 브랜드 성향
{
    Contact, Power, Running, Defense,
    Rotation, Bullpen, Development, Experience   // 0~100, 강함/약함이 아니라 성향
}

TeamSeasonClubState        // 개별 TeamSeason 소유, 그 TeamSeason의 현재 운영 DNA
{
    TeamSeasonKey
    Contact, Power, Running, Defense,
    Rotation, Bullpen, Development, Experience
}
```

- `TeamSeasonClubState`가 실제로 매 시즌 갱신되는 대상이다: `새 DNA = 기존 DNA × 0.6 +
  해당 TeamSeason 최근 3년 성적 반영 × 0.25 + 감독 철학 × 0.15`, 단 한 시즌 최대 변화폭
  ±5로 제한. 초기값은 §2 Fingerprint에서 파생한다.
- `FranchiseIdentityProfile`은 그 Franchise에 속한 모든 TeamSeason의 `TeamSeasonClubState`를
  장기 평균한 값으로, 시즌 단위가 아니라 수년 단위로만 완만하게 갱신한다(브랜드 이미지 —
  "Comets는 원래 장타 육성으로 유명한 프랜차이즈"라는 서술에만 쓰고, 개별 TeamSeason의
  실시간 판단에는 쓰지 않는다).
- AI 감독의 영입/드래프트/트레이드/기용 가중치는 **자신이 속한 `TeamSeasonClubState`**를
  참조한다(Franchise 전체 평균이 아니라). 예: Power DNA 높은 `2011 Comets`는 트레이드·
  이적시장에서 장타 선수 영입 우선순위가 오르지만, 같은 Franchise라도 Power DNA가 낮은
  `1994 Comets`는 그렇지 않다 — UI 숫자로만 존재하는 장식이 아니어야 한다. 여기서 말하는
  "영입"은 05절 §7의 `AiRosterOptimizer`(트레이드·이적시장)이며, 05절의 SP 스카우트(가챠)는
  플레이어 전용이라 AI 경로에 포함되지 않는다.
- 04절 팀컬러 판정(구단덱 Franchise, 구단별 연도덱 YearFranchise)은 `FranchiseId`/
  `TeamSeasonKey`만 보고 `ClubDNA` 값 자체는 참조하지 않는다 — 이 분리가 팀컬러 판정에
  영향을 주지 않는다.

## 6. Signature 후보 (Golden Generation / Dynasty)

이 절은 `04_팀컬러_시스템.md`의 TeamColor와는 다른 개념이다 — TeamColor는 카드 수집 결과로
즉시 계산되는 수치 보너스이고, 여기서 다루는 것은 **게임 세계 안에서 실제로 발생한 서사적
이정표를 이름 붙여 기록실에 남기는 것**이다. 능력치 보너스는 주지 않는다.

```text
Golden Generation 조건 예: 구단 육성 선수 7명 이상 + 3시즌 이상 함께 활동 + 포스트시즌 진출
Dynasty 조건 예: 정규리그 1위 또는 우승 + 특정 시즌 지표 상위권
```

달성 시 구단 역사 기록(뉴스, 기록실, 감독/선수 업적)에만 반영한다. 이 항목은 Phase 우선순위상
가장 마지막(08절 Phase 6)이다 — 승강·팀컬러·경제가 먼저 안정되어야 의미가 생긴다.

## 7. 데이터 모델 배치

```text
Baseball.Core
  TeamSeasonDefinition
  FranchiseIdentityProfile   // Franchise 소유, §5
  TeamSeasonClubState        // TeamSeason 소유, §5
  ClubLegacyDefinition (Golden Generation / Dynasty 조건 정의)

Baseball.Simulation
  SyntheticTeamGenerator      // 01절 결과 소비, TeamSeasonDefinition 생성
  TeamSeasonClubStateResolver // 시즌 종료 시 TeamSeasonClubState 갱신 (§5, 빠른 주기)
  FranchiseIdentityResolver   // 여러 TeamSeasonClubState의 장기 평균 갱신 (§5, 느린 주기)
  ClubLegacyResolver          // 서사적 이정표 판정
  LeagueGrade 승강 Resolver   // 기존 구조 확장
```

## 8. 검증 기준

- 10,000 TeamSeason 규모로 승강 시뮬레이션을 돌려, `ReferenceStrength` 상위 구단이 통계적으로
  더 빨리/자주 상위 리그에 도달하는 상관관계를 확인한다(강제 순위가 아니라 확률적 경향).
- `TeamSeasonClubState`가 한 시즌에 ±5를 초과해 변하지 않는지 테스트한다.
- 같은 Franchise의 서로 다른 TeamSeason(예: 2008 Comets와 2011 Comets)의 `TeamSeasonClubState`
  갱신이 서로 간섭하지 않는지(한쪽 시즌 결과가 다른 쪽 값에 섞이지 않는지) 테스트한다.
- `FranchiseIdentityProfile`이 개별 시즌 단위가 아니라 여러 시즌에 걸쳐서만 완만하게
  변하는지, 그리고 개별 TeamSeason의 AI 판단(§7 `AiRosterOptimizer`)이
  `FranchiseIdentityProfile`이 아니라 자신의 `TeamSeasonClubState`를 참조하는지 테스트한다.
- 동일 Seed로 두 번 돌린 승강 결과와 DNA 변화가 완전히 일치하는지 결정론 테스트로 확인한다.
- Fingerprint 혼합에서 보조 Era Pool(±1~2년) 비중이 설정 상한(예: 20%)을 넘지 않는지
  테스트한다.
