# 역사 시뮬레이션 감독모드 — 구현 명세 인덱스

## 0. 이 문서의 위치

이 폴더는 KBO 통계 기반의 익명 Reference를 이용해 미리 Bake한 가상 선수·가상 연도 구단을,
선수 커리어 모드와 감독모드가 함께 사용하는 역사 시뮬레이션 세계로 구성하기 위한 구현 명세다.

**2026-09-01 최신 확정:** 다음 원칙을 이 폴더의 최상위 Source of Truth로 사용한다.

1. **감독모드는 선수 커리어 모드와 별개의 두 번째 진입점이다.**
2. **가상 선수의 인물·연도별 능력치·연도별 원소속 구단·Cost·TrainingCeiling은 Offline/Editor에서 미리 생성하여 Bake한다.** Runtime에서 가상 선수를 새로 생성하거나 소속을 다시 추첨하지 않는다.
3. **선수 커리어 모드와 감독모드는 같은 `PlayerPersonDefinition`/`PlayerSeasonDefinition`/카드 원본/`TeamSeasonDefinition`을 사용한다.** 모드마다 별도 선수풀을 만들지 않는다.
4. **감독모드 플레이어 구단만** `OwnedPlayerCardState`, SP 스카우트, 중복 강화, 카드 DP 훈련 경제를 사용한다. AI 구단과 선수 커리어 모드에는 이 소유 경제를 적용하지 않는다.
5. **시즌 성적은 Simulation 결과다.** All-Star/Golden Glove/MVP 계열 수상도 능력치 자체가 아니라 해당 월드의 실제 시즌·올스타전·포스트시즌 기록으로 판정한다.
6. 새 게임에서 사용자는 **월드 기록 방식**을 고른다.
   - `SimulatedHistory` — Bake된 선수/구단으로 과거 기록을 다시 시뮬레이션한다.
   - `OriginalHistory` — 미리 준비된 Runtime-safe 고유 기록을 사용하고 과거 기록 시뮬레이션을 생략한다.
   두 경로는 모두 동일한 `WorldHistorySnapshot`/`WorldAwardRecord` 형식으로 수렴한다.
7. 1군 `ActiveRoster`는 정확히 25명이다: **야수 14(주전 9+벤치 5), 투수 11(선발 5+불펜 4+셋업 1+마무리 1)**. 외국인 선수는 ActiveRoster에 최대 3명까지 등록할 수 있으며, 카드 보유 수에는 제한을 두지 않는다.
8. 실제 등록 포지션과 다른 포지션 기용은 **허용**한다. 야수의 비주포지션 수비는 실책 확률 증가+컨디션 하락, 투수의 비본래 역할 기용은 컨디션 하락을 적용한다. DH는 어떤 타자도 패널티 없이 맡을 수 있다.
9. **리그의 정규 Franchise 구단 슬롯은 10개로 고정한다.** World의 기록/수상이 확정된 뒤 같은 OriginYear의 정규 구단 전체 선수풀을 합산하여 `AllStarComposite`, `GoldenGloveComposite`, `YearSelectComposite` 3개 특수 합성팀을 추가 배치한다. 세 합성팀의 최종 25인 사이에는 동일 `PlayerSeasonId`가 겹치지 않는다. `SimulatedHistory`의 최초 과거 기록 시뮬레이션에는 특수 합성팀을 참가시키지 않고 원래 정규 구단만 사용한다.

기존 `BaseballManager_PROJECT.md`의 과거 결정과 충돌하는 경우, 이 폴더 범위에서는 위 2026-09-01 확정을 우선한다.

## 0.1 2026-09-02 구현 스냅샷

이 절은 위 설계를 변경하는 새 규칙이 아니라, 현재 코드와 검증의 도달 지점을 기록한다.
`완료`는 해당 계약과 집중 테스트가 구현됐다는 뜻이며, 실제 새 게임 경로·Save·장기 Simulation까지
연결되지 않은 항목은 `부분 완료`로 구분한다.

| 영역 | 현재 상태 | 근거와 남은 경계 |
| --- | --- | --- |
| Offline Synthetic Bake | 완료(에디터 산출물) | 1982~2025 44시즌, `PlayerPerson` 1,757명, `PlayerSeason` 13,200개, `TeamSeason` 440개를 생성했다. 구단별 Pool 30명/Core25 25명, Original Record 13,200개, Award 1,672개다. 동일 입력/Seed 2회 결과의 Stable ID, `ContentHash`, 파일 SHA-256이 일치했다. |
| Editor Asset 보관 | 완료(에디터 전용) | `1982-2025/manifest.json`, `player_persons.json`, `Years/1982.json`~`2025.json`을 `Assets/Editor Default Resources/HistoricalSimulation/`에 생성했다(46 JSON, 12,908,847 bytes). 파일 Hash/재조립과 Unity Import/Compile을 검증했으며 Archive Hash는 `dc6e328f8468bd18ae7147eedd53561853703b5f19521019ce70c29dfc1fd030`이다. 일반 `Assets/**/Resources`에는 두지 않으며 Runtime 배포 Provider가 아니다. |
| 공통 Core/Simulation 계약 | 완료(집중 테스트) | Player/Season/Card, Roster/Position/Bullpen, League/DNA, TeamColor, Economy/Scout, WorldHistory/Award/Composite, Tactic, Wildcard의 Definition/Resolver를 추가·확장했다. |
| Match 연결 | 부분 완료 | `DetailedMatchEngine`에 전술·Assignment·Fielding Error·Bullpen 공통 규칙을 연결하고 집중 테스트했다. 전술의 모든 `BehaviorModifiers`, Stamina/BatterMental, 전체 벤치·감독 AI 운영은 미완료다. |
| Historical World 실행 | 부분 완료 | `OriginalHistory`/`SimulatedHistory` 공통 Schema, Save/Load, Award/Composite 및 DetailedMatch 통계 Adapter 계약은 있다. 실제 시즌 Schedule/Lineup을 공급하는 `IHistoricalDetailedSeasonSource` 구현과 다중 Seed 장기 실행은 없다. |
| Career 공통 콘텐츠 | 부분 완료 | Baked Provider 주입 경로와 회귀 테스트는 있다. 실제 `ICareerBakedContentProvider` 구현이 없고 기본 새 게임은 아직 `LegacyRuntimeSynthetic`이며, Career Save의 공통 World/PlayerSeason 영속화도 미완료다. |
| Special Composite Runtime | 부분 완료 | 결정론적 Builder와 상호 중복 제거는 테스트했다. 원 구단과 합성팀이 같은 `PlayerSeason`을 동시에 출전시킬 별도 Runtime Instance 구조는 미완료다. |
| 장기 검증 | 보류 | 신규 집중 EditMode 77/77, Offline Python 3/3은 통과했다. 전체 Career 장기 회귀와 Historical multi-seed, Tactic 수천 경기, 승강 10,000 TeamSeason, Endgame EffectiveRating 검증은 코드 개선 후 수행한다. |

따라서 현재는 **에디터용 Bake 에셋 생성과 공통 계약 검증까지** 진행된 상태다. 이를 Runtime에서
사용 가능한 완성 콘텐츠 또는 Phase 1~6 전체 통과로 해석하지 않는다.

## 1. 두 모드의 경계

```text
공통 콘텐츠 / 공통 월드
  Baked PlayerPersonDefinition
  Baked PlayerSeasonDefinition
  Baked Normal Card Base / Edition Modifier Definition
  Baked TeamSeasonDefinition
  WorldHistorySnapshot
  WorldAwardRecord
  WorldCardCatalog
  ActiveRosterCompositionRule

선수 커리어 모드
  플레이어 = 성장·노화하는 Career Player 1명
  일반 구단 선수 = 공통 Baked 카드/선수 데이터 사용
  로스터/라인업/교체/전술 = 감독 AI 소유
  카드 수집·중복강화·SP Scout·카드 DP 훈련 = 없음
  주인공만 TeamColor에서 CareerPlayerWildcard 사용

감독모드
  플레이어 = 가상 연도 구단의 감독
  일반 선수 원본 = 선수 커리어 모드와 동일
  플레이어 구단 = OwnedPlayerCardState + Scout/강화/판매/CardTraining 사용
  AI 구단 = 공통 카드 원본 + TeamColor + Club DNA + 감독 AI, Scout/강화/CardTraining 없음
```

두 모드는 Save 진행 상태를 분리한다. 같은 콘텐츠 Definition을 공유한다는 것은 두 Save가
`CurrentRosterState`, 순위, 이적 결과까지 공유한다는 뜻이 아니다. 감독모드의
`OwnedPlayerCardState`는 공통 Definition이나 선수 커리어 Save에 기록하지 않는다.

## 2. 월드 기록 초기화

새 게임의 `WorldRecordMode`는 **게임 시작 이전 역사 데이터의 초기화 방식**만 결정한다.
게임 시작 이후 시즌은 두 모드 모두 실제 Match/Season Simulation으로 진행한다.

```text
                    ┌─ SimulatedHistory
Baked Content ──────┤   Historical Season Simulation → Statistics → Awards
                    │
                    └─ OriginalHistory
                        Runtime-safe Original Records Load

                                  ↓
                        WorldHistorySnapshot
                                  ↓
                   WorldAwardRecord / WorldCardCatalog
                                  ↓
       TeamColor / Scout / AI Edition / 기록실 / 뉴스 / Wildcard
```

Consumer 시스템은 `WorldRecordMode`를 직접 분기하지 않고 공통 World Record만 읽는다.
`SimulatedHistory` 결과는 최초 생성 후 Save에 저장하며 Load 때 다시 시뮬레이션하지 않는다.

## 3. 공통 ActiveRoster 규칙

```text
ActiveRoster = 25

야수 14
  주전 9: C / 1B / 2B / 3B / SS / LF / CF / RF / DH
  벤치 5

투수 11
  선발 5
  불펜 1~4번 4
  셋업 1
  마무리 1

외국인 등록 <= 3
동일 PlayerPersonId 중복 등록 금지
```

포지션 적합성은 ActiveRoster 등록의 하드 금지 조건이 아니다. 경기 라인업/교체 시 비주포지션
기용을 허용하고 `PositionAssignmentRule`의 패널티를 적용한다. DH는 타자의 본래 수비 포지션과
무관하게 자유롭게 배치한다.

불펜 4명의 운영 의미는 다음과 같다.

```text
Bullpen 1 / 2  — 필승조 + 추격조
Bullpen 3      — 애매한 상황 + 추격조
Bullpen 4      — 패전조 + 앞선 불펜의 체력이 부족할 때 대체
```

정확한 이닝/점수차 임계값은 `BullpenUsagePolicy` 등 밸런스 데이터로 둔다.


## 4. 리그 10개 정규 구단과 연도별 특수 합성팀

리그의 **정규 Franchise 구단 수는 정확히 10개**다. 이 10개는 Offline Bake된 원래
`TeamSeasonDefinition`으로 구성하며, World 기록 방식이나 Seed가 원소속/선수풀을 다시 뽑지 않는다.

World의 초기 기록과 수상이 확정된 뒤에는 같은 `OriginYear`의 정규 구단 전체 선수풀을 합산하여
다음 세 특수 합성팀을 별도 참가팀으로 추가한다.

```text
AllStarCompositeTeam      — 해당 World/연도의 All-Star 결과를 핵심 후보로 구성
GoldenGloveCompositeTeam  — 해당 World/연도의 Golden Glove 결과를 핵심 후보로 구성
YearSelectCompositeTeam   — 해당 OriginYear 전체 선수풀에서 결정론적 RNG로 구성
```

특수 합성팀은 정규 Franchise 10개 슬롯에 산입하지 않는 **비-Franchise 합성 참가팀**이다.
각 팀의 최종 로스터는 공통 `ActiveRosterCompositionRule`(25인, 야수14/투수11, 투수 역할,
외국인 최대3)을 따른다.

세 특수 합성팀의 최종 25인끼리는 동일 `PlayerSeasonId`가 중복될 수 없다. 중복 후보가 발생하면
`AllStar → GoldenGlove → YearSelect` 순서로 먼저 배정된 팀을 유지하고 뒤 팀은 같은 연도의
다음 적격 후보로 보충한다. 이 중복 금지는 **세 특수 합성팀 사이**에 적용한다.
원래 Franchise 구단의 선수를 합성팀이 참조하는 것은 허용하며, 원소속 로스터에서 선수를 제거하거나
`OriginFranchiseId`/`OriginTeamSeasonKey`를 변경하지 않는다.

`SimulatedHistory`의 최초 과거 기록 생성은 반드시 다음 순서를 따른다.

```text
원래 정규 구단만 Historical Simulation
→ Season Statistics
→ All-Star / Golden Glove / MVP 등 Award 확정
→ WorldHistorySnapshot 확정
→ 특수 합성팀 3종 구성
→ 리그 참가팀에 추가
```

즉 특수 합성팀은 자신을 만드는 Statistics/Award의 입력이 될 수 없다. `OriginalHistory`는
고유 기록을 `WorldHistorySnapshot`으로 먼저 로드한 뒤 동일한 합성팀 생성 단계를 수행한다.

## 5. 문서 구성과 읽는 순서

| 순서 | 문서 | 내용 |
| --- | --- | --- |
| 1 | [01_시대보정_가상선수생성.md](01_시대보정_가상선수생성.md) | KBO Reference → Offline Synthetic 선수/소속/원기록 → Runtime-safe Bake |
| 2 | [02_가상구단_승강리그_ClubDNA.md](02_가상구단_승강리그_ClubDNA.md) | TeamSeason, 공통 25인 로스터, 포지션/불펜 규칙, Rookie~Galaxy, Club DNA |
| 3 | [03_선수풀_카드_Cost_Edition.md](03_선수풀_카드_Cost_Edition.md) | 공통 Player/Season/Card 모델, Cost, World Award 기반 Edition |
| 4 | [04_팀컬러_시스템.md](04_팀컬러_시스템.md) | TeamColorFamily, 모드별 EffectiveRating 레이어, Rating Curve |
| 5 | [05_감독모드_경제_스카우트.md](05_감독모드_경제_스카우트.md) | 감독모드 전용 SP/Money/DP, Scout, 중복 강화/판매, AI 로스터 |
| 6 | [06_전술카드_시스템.md](06_전술카드_시스템.md) | 감독 전술, 불펜/교체 운영, 전술카드, 조건부 발동/카운터 |
| 7 | [07_선수모드_연동_와일드카드.md](07_선수모드_연동_와일드카드.md) | 공통 카드/로스터를 쓰는 선수모드와 Career Player Wildcard |
| 8 | [08_구현_로드맵_검증기준.md](08_구현_로드맵_검증기준.md) | Offline Bake → 공통 Runtime → World Record → 모드별 기능의 구현 순서와 검증 |

## 6. 설계 전반의 공통 원칙

- **Core/Simulation은 Unity를 참조하지 않는다.** Runtime Resolver는 순수 C#이며 난수는 주입받은 결정론 RNG만 사용한다.
- **Synthetic 선수 생성기는 Runtime Game Flow에 존재하지 않는다.** 생성/유사도 검사/원소속 배정은 Editor/Offline Pipeline 책임이다.
- **실제 KBO 선수·구단은 Runtime 콘텐츠로 노출하지 않는다.** 실명·실제 구단 식별자·Raw JSON을 Runtime Definition에 복사하지 않는다.
- **선수 데이터 원본은 두 모드가 공유한다.** 선수모드 전용 일반 선수 Definition과 감독모드 전용 카드 Definition을 이중으로 만들지 않는다.
- **Award Source와 Award Consumer를 분리한다.** `OriginalHistory`와 `SimulatedHistory` 모두 `WorldAwardRecord`로 수렴하고 TeamColor/Scout/AI는 그 결과만 소비한다.
- **AI 구단에는 감독모드 전용 강화·가챠·DP 카드훈련을 적용하지 않는다.**
- **라인업 비주포지션은 허용하되 비용이 있다.** UI에서 금지하지 말고 예상 컨디션/수비 리스크를 보여주며 Simulation에서 동일 규칙을 적용한다.
- **밸런스 수치는 전부 데이터화한다.** Cost, TeamColor, Scout 확률, 포지션 패널티, 불펜 사용 임계값, 전술카드 효과를 코드에 하드코딩하지 않는다.
- **모든 주요 밸런스 변경은 대량 Simulation 근거를 요구한다.**
