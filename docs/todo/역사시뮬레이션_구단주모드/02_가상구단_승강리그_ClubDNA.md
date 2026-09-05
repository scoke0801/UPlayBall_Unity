# 02. Canonical TeamSeason, 승강 리그, Club DNA

## 0. 목적

실제 Source TeamSeason의 선수 구성을 1:1 정본으로 유지하면서 게임 공통 Roster Rule에 맞는 Core25를
만들고, World별 구단 표시 Identity와 Simulation History를 분리한다.

```text
Source TeamSeason
→ Canonical TeamSeasonDefinition 1:1
→ 해당 Source Team 소속 PlayerSeason Pool
→ Core25
```

여러 실제 구단의 장타·선발·수비·불펜 성향을 비율로 섞어 일반 Franchise를 만드는 Franchise
Fingerprint Mixing은 폐기한다.

## 1. TeamSeason과 Franchise

```text
FranchiseId
  ├─ TeamSeasonKey 2010
  ├─ TeamSeasonKey 2011
  └─ TeamSeasonKey 2012
```

- 한 Source TeamSeason은 정확히 하나의 Canonical `TeamSeasonDefinition`이 된다.
- 같은 Source Franchise의 연도별 TeamSeason은 같은 Stable `FranchiseId` 아래 연결한다.
- Source TeamSeason Key와 Runtime-safe `TeamSeasonKey`의 1:1 provenance를 Offline에서 검증한다.
- 실제 Source 팀명은 Editor provenance/blacklist에만 두고 Runtime 표시 이름으로 사용하지 않는다.

Canonical Definition은 최소한 다음을 가진다.

```text
TeamSeasonKey
FranchiseId
OriginYear
Core25 PlayerSeasonId[]
ReferenceStrength
InitialClubDna
```

`ReferenceStrength`가 필요하면 그 TeamSeason의 Baked 선수 전력에서 결정론적으로 파생한다. 여러 팀의
Fingerprint를 혼합한 값이 아니다. `InitialClubDna`도 가능한 한 같은 Canonical TeamSeason의 실제
구성 특성에서 파생하고, World 성적은 이후 시즌 진행에만 영향을 준다.

## 2. Source TeamSeason 1:1 Mapping

한 TeamSeason의 후보 선수풀은 다음 조건을 모두 만족해야 한다.

```text
PlayerSeason.OriginYear == TeamSeason.OriginYear
PlayerSeason.OriginFranchiseId == TeamSeason.FranchiseId
PlayerSeason.OriginTeamSeasonKey == TeamSeason.TeamSeasonKey
```

같은 연도 전체 선수를 고정 10개 가상 팀에 Hash/round-robin으로 재배분하지 않는다. Source에 6개,
7개, 8개, 9개, 10개 구단이 있었다면 Canonical TeamSeason 수도 그 Source의 정본을 따른다. 게임의
특정 리그가 10 Franchise를 요구하는 경우에는 그 리그 진입 정책에서 명시적으로 해결하며, Canonical
Bake에서 타 팀 선수를 섞어 팀 수를 맞추지 않는다.

## 3. Core25

공통 ActiveRoster 계약은 다음과 같다.

```text
ActiveRoster = 25

Hitter 14
  Starter 9
  Bench 5

Pitcher 11
  StartingPitcher 5
  Bullpen 4
  Setup 1
  Closer 1

Foreign <= 3
```

Source 선수풀이 25명보다 많으면 해당 TeamSeason 내부에서만 Core25를 선정한다. 선정 기준은
Position/Role 적합성, Baked Ability, 경기 표본 Reliability, Roster 균형을 사용하며 결정론적 tie-break
순서를 고정한다.

원본 데이터가 부족해 25명 또는 역할 quota를 충족할 수 없다면 다음 순서를 따른다.

1. 누락된 Source 데이터나 TeamSeason 연결 오류인지 조사한다.
2. Validation Report에 연도·구단·부족 Role/Position과 인원수를 기록한다.
3. 같은 Source TeamSeason 내부의 제한 표본 선수까지 포함하는 보수적 fallback을 검토한다.
4. 그래도 불가능하면 결손 슬롯을 `ReplacementGenerated`로 명시하고, 동일 연도·역할 모집단의
   고정 20백분위 aggregate baseline을 사용한다. 개별 선수 조합, 공분산 표본, RNG는 사용하지 않는다.

현재 1982~2025 Source에서는 투수 quota가 부족한 20개 TeamSeason에 총 54석이 이 fallback을 쓴다.
연도별 수는 1982 27명, 1983 12명, 1984 9명, 1985 2명, 1986 1명, 1987 1명, 1988 2명이다.
모든 fallback은 부족한 바로 그 Canonical TeamSeason의 Origin을 가지며 Validation Report에 남는다.

다른 Source Team의 선수를 조용히 가져오거나 covariance 기반 Replacement 선수를 생성하지 않는다.

## 4. Position, DH, PitcherRole

등록 포지션은 Core25 구성의 균형 기준이고, 실제 경기 배치는 `PositionAssignmentRule`과 감독 AI가
소유한다. 비주포지션 기용은 하드 금지가 아니라 명시적 패널티와 함께 허용한다.

- 야수 주전 9명은 해당 시즌 DH 규칙을 반영한다.
- Bench 5명은 대타·대주자·수비 교체를 감당하도록 구성한다.
- 투수 11명은 SP5/Bullpen4/Setup1/Closer1 슬롯을 채운다.
- Bullpen 4명은 Swingman/MiddleRelief/LongRelief/Specialist 등 현행 `BullpenUsagePolicy` 역할을
  유지하되 Source 팀 내부 후보만 사용한다.
- `RegistrationType == Foreign` 제한은 Core25와 경기 등록 양쪽에서 Stable ID/enum으로 검증한다.

## 5. World Franchise Identity

Canonical Franchise와 표시 Identity를 분리한다.

```text
WorldFranchiseIdentity
{
    FranchiseId
    DisplayName
}
```

`WorldIdentityRegistry`는 한 World에서 `FranchiseId`마다 이름 하나를 확정한다. 따라서 같은
Franchise의 2010~2012 TeamSeason은 모두 같은 DisplayName을 쓴다.

- 자연스러운 지역 Identity + Nickname/Brand Identity를 데이터 기반으로 조합한다.
- 실제 KBO 구단명과 exact match하면 Reject한다.
- 서로 다른 Franchise의 이름 중복을 금지한다.
- 이름 생성 결과 자체를 Save하며 Load에서 재생성하지 않는다.
- Simulation, Standings, TeamColor, Scout, AI, Contract, Roster는 DisplayName으로 구단을 찾지 않는다.

## 6. Historical Team Statistics와 Standings

Source 실제 승패·순위·우승은 정식 Runtime World History가 아니다.

```text
Canonical TeamSeason
→ Schedule
→ Detailed Match Simulation
→ Wins / Losses / Runs / RunsAllowed / Team AVG / Team ERA
→ Standings
→ Postseason
→ Champion
```

다른 World Seed에서는 순위, 승률, 포스트시즌 진출팀, 우승팀, 공격·투수 기록이 달라질 수 있다.
Source Standings를 Runtime Standings에 복사하거나 `ReferenceStrength` 순서로 확정하지 않는다.

## 7. 승강 리그

Rookie~Galaxy 등 World의 리그 상태는 Runtime `LeagueInstance`가 소유한다. Canonical OriginYear나
ReferenceStrength는 팀의 초기 전력·역사를 설명하는 데이터일 뿐 높은 리그를 강제하는 열쇠가 아니다.
승강은 해당 World의 Simulation 결과와 명시적인 승강 규칙으로 결정한다.

Canonical Archive의 연도별 실제 구단 수와 Runtime 리그의 정원 10개를 혼동하지 않는다. 현재
Career 진입 정책은 `NewGameDefinition._historicalLeagueSeasonYears`가 LeagueGrade 순서로 지정한
2016~2025의 10구단 TeamSeason만 배치한다. 코드는 "최근 10년"을 추론하지 않고 정확히 이 데이터
목록을 소비하며, 연도 누락·중복·10구단 미달이면 명시적으로 실패한다. 같은 Franchise의 여러
TeamSeason은 같은 `WorldFranchiseIdentity`를 공유하고 `TeamSeasonKey`로 시즌 인스턴스를 구분한다.
정책 밖의 임의 재배치나 Synthetic Team fallback은 사용하지 않는다.

## 8. Club DNA

Club DNA는 두 층으로 나눈다.

- `TeamSeasonDefinition.InitialClubDna`: 해당 Canonical 선수 구성에서 파생한 고정 초기 특성.
- Runtime `FranchiseState.ClubDna`: World 시즌 진행, 감독, 육성, 영입 결과로 변화하는 상태.

DisplayName은 DNA 계산에 사용하지 않는다. 같은 Canonical 입력과 World Seed는 같은 변화 순서를
만들어야 한다. `ReferenceStrength`가 높다는 이유만으로 승격·우승을 강제하지 않는다.

## 9. 특수 합성팀

다음 팀은 일반 Franchise TeamSeason과 다른 게임용 로스터다.

```text
AllStarComposite
GoldenGloveComposite
YearSelectComposite
```

생성 순서는 고정한다.

```text
정규 Canonical TeamSeason만 Historical Simulation
→ Statistics / Standings / Postseason
→ Awards / WorldHistorySnapshot
→ SpecialCompositeTeamBuilder
→ 특수 합성팀 3종
```

특수팀은 기존 PlayerSeason을 참조하며 선수 능력치를 혼합하지 않는다. 세 특수팀의 최종 25인 사이
동일 `PlayerSeasonId` 중복 금지를 유지한다. 원 Franchise의 Core25/Origin을 이동·변경하지 않고,
자신을 생성하는 Award 계산에 역으로 참가하지 않는다.

## 10. 필수 검증

- Canonical TeamSeason마다 Source TeamSeason provenance 정확히 1건
- Core25 전원의 `OriginTeamSeasonKey`가 해당 TeamSeason과 일치
- 타 Source Team 선수 Mixing 0건
- Source 연도별 TeamSeason 수와 Canonical 수 일치
- Core25 25명, Hitter14/Pitcher11, SP5/Bullpen4/Setup1/Closer1, Foreign≤3
- 데이터 부족 시 Silent Fallback 0건과 구체적인 Validation Report
- 같은 Franchise 다년도 DisplayName 일치, 다른 Franchise 이름 중복 0건
- Source 실제 구단명 exact match 0건
- Source Standings/Champion 복사 없이 Match 결과에서 순위 생성
- DisplayName만 변경해도 Schedule/Match/Standings hash 불변
- 같은 Seed 결정론, 다른 Seed에서 Standings/Champion 변화 가능
- Award 확정 전 특수 합성팀 참가 0개와 특수팀 사이 PlayerSeason 중복 0건

## 11. 완료 판정

새 1:1 TeamSeason Schema를 추가했더라도 Production Archive가 실제로 같은 연도 선수 전체를 임의
가상 Franchise에 재배분하면 미완료다. Canonical TeamSeason의 Source provenance와 Core25 내부
소속 일치 검증이 Runtime Catalog 생성 Gate를 통과해야 한다.
