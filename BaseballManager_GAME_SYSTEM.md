# Single Baseball Manager — Game System Design

> **⚠ 2026-08-26 선수 모드 기준으로 후순위화.** 이 문서는 "플레이어가 구단주 겸 감독으로 전체 로스터·라인업·전술을 편성한다"는 전제로 쓰였다.
> 2026-08-26에 선수 커리어 모드(선수 1명의 커리어를 사는 장르)를 우선 구현하기로 하면서 이 문서는 한동안 참고 대상에서 제외됐다 (`BaseballManager_PROJECT.md` 상단 공지, 40절 참고). 이후 2026-08-31에 야구 시뮬레이션 엔진이 고도화되며 **구단주 모드(구단 전체 운영)를 별도의 정식 진입점으로 재도입**했다 — 선수 모드를 대체하는 것이 아니라 병행하는 두 번째 모드다. 구단주 모드의 현재 설계는 `docs/todo/역사시뮬레이션_구단주모드/`와 `BaseballManager_PROJECT.md` 상단 2026-08-31 공지를 따른다.
> 이 문서에서 재사용 가능한 것은 **5절 리그 구조(`LeagueGrade`, 승강 규칙)** 뿐이다. 4절(구단 창단/`AcademyPlayer`/`Rookie Tryout`), 8~10절(`GamePlan`/`TacticCard`/`ManagerAi`에게 플레이어가 지시), 그 외 절은 현재 구단주 모드 구현(`Assets/02.Scripts/Presentation/Owner/`, `docs/todo/역사시뮬레이션_구단주모드/`)과 어긋나면 그쪽을 따르고 이 문서는 참고만 한다.
>
> 상태: Core System Draft v0.1 (선수 모드 우선 구현 기간 동안 참고 보류 — 구단주 모드 재도입 후에는 `docs/todo/역사시뮬레이션_구단주모드/`가 정본)
>
> 기준 문서: `BaseballManager_PROJECT.md`
>
> 목표(구 버전, 더 이상 유효하지 않음): 선수 한 명으로 시작해 구단을 완성하고, `Rookie`에서 `Galaxy`까지 승격하는 싱글 플레이 자동 경기 야구 매니지먼트 게임

---

## 1. 게임 한 줄 정의

플레이어가 감독이 되어 첫 프랜차이즈 선수 한 명을 선택하고,
정해진 리그 시간마다 진행되는 자동 경기의 라인업·운영 방침·전술 카드를 결정하며
구단을 완성해 최하위 `Rookie` 리그부터 최상위 `Galaxy` 리그까지 올라가는 게임.

---

## 2. 핵심 설계 결론

### 2.1 플레이어의 역할

플레이어는 구단주와 현장 감독의 역할을 함께 맡는다.

- 선수 영입과 방출, 성장 방향을 결정한다.
- 매 경기의 라인업, 선발 투수, `GamePlan`을 정한다.
- 상대가 공개되면 제한된 `PreparationPoint`로 `TacticCard`를 선택한다.
- 경기는 직접 조작하지 않고 `ManagerAi`가 사전 지시를 실행한다.
- 경기 후 결과만 보는 것이 아니라 어떤 판단과 전술이 결과에 영향을 주었는지 확인한다.

### 2.2 네 가지 핵심 재미

| 축 | 플레이어가 내리는 결정 | 결과 |
|---|---|---|
| Team Building | 첫 선수와 이후 영입 선수로 어떤 팀을 만들 것인가? | 구단의 전력과 정체성 |
| Match Preparation | 상대에 맞춰 누구를 기용할 것인가? | 매치업 우위와 선수 활용 |
| Tactics | 어느 경기, 어느 상황에 준비 자원을 쓸 것인가? | 감독 판단의 무게 |
| League Climb | 당장 승격할지 장기 육성할지 선택할 것인가? | `Rookie`에서 `Galaxy`까지의 장기 목표 |

### 2.3 반드시 지킬 원칙

- 리그 등급은 상대 AI와 로스터 완성도를 높이지, 숨은 능력치 보너스를 주지 않는다.
- 전술은 승리를 보장하지 않는다. 상황을 만들거나 AI 판단 기준을 바꾼다.
- 한 선수로 시작해도 첫 경기부터 정상적인 9이닝 경기를 진행할 수 있어야 한다.
- 실제 시간을 기다리지 않아도 여러 시즌을 플레이할 수 있어야 한다.
- 같은 Seed와 같은 경기 준비 입력은 항상 같은 결과를 만든다.
- 선수 능력치, 라인업, 피로가 전술 카드보다 경기 결과에 더 큰 영향을 준다.

---

## 3. 전체 게임 플로우

```text
구단 생성
↓
ManagerStyle 선택
↓
첫 FranchisePlayer 한 명 선택
↓
빈 포지션을 AcademyPlayer가 임시로 채움
↓
Rookie League 참가
↓
상대 공개
↓
Lineup / StartingPitcher / GamePlan 설정
↓
선택적으로 TacticCard 사용
↓
리그 경기 시간 도달
↓
모든 팀이 동시에 한 경기씩 자동 시뮬레이션
↓
경기 결과 / 전술 보고 / 선수 성장 / 영입 선택
↓
다음 경기
↓
정규 시즌 / 포스트시즌 / 승격·잔류·강등
↓
상위 리그 도전
```

짧은 세션:

```text
다음 상대 확인
→ 라인업과 전술 설정
→ 1~3경기 진행
→ 결과와 성장 확인
→ 종료
```

긴 세션:

```text
LeagueDay 6경기 진행
→ 선수 영입과 보드 정리
→ 순위 경쟁
→ 포스트시즌
→ 승격
```

---

## 4. 선수 한 명으로 시작하는 구단 창단

### 4.1 첫 선수 선택

구단을 생성하면 동등한 시즌 기대 기여도를 가진 후보 네 명 중 한 명을 선택한다.
단순 `Overall`이 아니라 경기에서 어떤 방식으로 기여하는지를 먼저 보여 준다.

| Archetype | PrimaryPosition | 강점 | 약점 | 추천 플레이 |
|---|---|---|---|---|
| Five-Tool Prospect | `CF` 또는 `SS` | 매 경기 공수 기여 | 압도적인 한 방 부족 | 안정적인 첫 구단 |
| Cleanup Slugger | `1B` 또는 `LF` | 장타와 득점 생산 | 수비·주루 약점 | 공격 중심 구단 |
| Field General | `C` | 수비와 투수진 안정 | 직접 타격 기여가 낮음 | 투수 운영 중심 |
| Future Ace | `SP` | 등판 경기 영향력이 큼 | 5경기마다 한 번 등판 | 선발 중심 구단 |

- 후보의 이름, 외형, 투타, 현재 능력, `Potential` 등급은 새 게임 Seed로 생성한다.
- 네 후보의 예상 시즌 기여도는 비슷하게 맞춘다.
- 첫 선수는 `FranchisePlayer` 태그를 얻지만 숨은 능력치 보너스는 받지 않는다.
- `Rookie` 시즌 동안은 방출과 트레이드가 잠긴다. 이후에는 일반 선수와 같은 커리어 규칙을 따른다.
- 선택하지 않은 후보는 사라지지 않고 다른 구단이나 자유계약 시장에 들어간다.

### 4.2 AcademyPlayer

한 명으로는 야구 경기를 할 수 없으므로 빈 로스터는 `AcademyPlayer`가 임시로 채운다.

```text
OwnedPlayer 1명
+ AcademyPlayer 24명
= 25인 경기 가능 로스터
```

`AcademyPlayer` 규칙:

- 포지션별 최소 인원을 만족하도록 자동 생성한다.
- `Rookie` 리그 평균보다 낮지만 경기가 무너지지 않는 능력치를 가진다.
- 성장, 유학, Skill Board, 팀 컬러, 트레이드 대상이 아니다.
- 실제 경기 기록은 남지만 커리어 역사와 수상 후보에는 포함하지 않는다.
- `OwnedPlayer`를 영입하면 같은 역할의 `AcademyPlayer`가 자동으로 교체 후보가 된다.
- 교체 전에는 어떤 역할이 빠지는지 보여 주고 플레이어가 확정한다.
- 시즌 종료 시 남은 `AcademyPlayer`는 모두 계약 종료된다.

이 구조의 목적은 약한 선수를 24명 떠넘기는 것이 아니라,
첫 선수의 활약을 중심으로 구단이 실제로 채워지는 과정을 보여 주는 것이다.

### 4.3 Rookie Tryout

`Rookie` 첫 8경기는 경기 후 `TryoutChoice`를 제공한다.

- 한 번의 선택에서 선수 3명으로 구성된 후보 묶음 세 개를 보여 준다.
- 플레이어는 묶음 하나를 선택해 세 선수를 모두 영입한다.
- `1 + 8 × 3 = 25`이므로 8번째 경기 후 정식 25인 로스터가 완성된다.
- 후보 묶음은 남은 포지션과 투수 역할을 검사해 로스터가 불완전해지지 않게 생성한다.
- 선택하지 않은 선수는 AI 구단 또는 자유계약 시장으로 이동한다.

후보 묶음은 단순 능력치 합이 아니라 팀 방향을 고르는 선택으로 구성한다.

```text
Contact Package
출루형 2B / 수비형 SS / 빠른 CF

Power Package
장타형 1B / 강견 RF / 대타 전문 DH

Pitching Package
제구형 SP / Ground Ball RP / 수비형 C
```

각 묶음의 예상 주전 자리, 기존 선수와의 경쟁, 팀 공격·수비 변화량을 선택 전에 보여 준다.

### 4.4 첫 8경기의 기능 개방

| 시점 | 개방 기능 | 학습 목표 |
|---|---|---|
| 창단 | 첫 선수, 자동 Lineup | 선수 카드 읽기 |
| Game 1 | 직접 Lineup 수정 | 포지션과 타순 |
| Game 2 | StartingRotation | 선발 등판 주기 |
| Game 3 | BullpenRole | 투수 피로와 역할 |
| Game 4 | GamePlan | 경기 전 운영 방향 |
| Game 5 | TacticCard 한 슬롯 | 조건부 전술 |
| Game 6 | OpponentReport | 상대 강약점 해석 |
| Game 7 | Condition / Fatigue | 기용의 기회비용 |
| Game 8 | 정식 25인 로스터 | 구단 창단 완료 |

기능은 뒤늦게 지급하는 보상이 아니라 복잡도를 나눠 가르치는 장치다.
Game 8 이후 `Rookie`의 나머지 시즌은 모든 기본 기능을 사용한다.

---

## 5. 리그 구조

### 5.1 LeagueGrade

초기 목표는 다음 10단계다.

```text
Rookie
↓
Minor
↓
Major
↓
World
↓
AllStar
↓
Classic
↓
Winners
↓
Champion
↓
Master
↓
Galaxy
```

`Beginner`는 별도 리그로 만들지 않는다. 첫 8경기의 `Rookie Tryout`이 튜토리얼 역할을 담당한다.
최상위 목표는 `Galaxy`이며 그 위에 능력치 인플레이션을 위한 등급을 계속 추가하지 않는다.

### 5.2 한 리그 그룹

```text
10 Teams
5 Matches per Round
72 Regular Season Matches per Team
8 Matches against each Opponent
12 LeagueDays
6 Rounds per LeagueDay
```

- 한 `LeagueRound`에서 모든 팀은 정확히 한 경기만 치른다.
- 10팀이므로 한 슬롯에 5경기가 생성된다.
- 플레이어 경기뿐 아니라 나머지 4경기도 같은 `MatchSimulator`로 계산한다.
- 9개 상대와 8경기씩 치러 총 72경기를 만든다.
- Home/Away는 상대별 4경기씩 균등하게 배정한다.
- 동일 상대와 세 경기 이상 연속으로 만나지 않도록 일정을 생성한다.

기준 문서의 8팀·80경기는 Phase 1 시뮬레이션 검증용 규모로 유지한다.
실제 승강 리그 콘텐츠는 10팀·72경기를 목표값으로 사용한다.

### 5.3 LeagueGroup 구성과 AI 구단 지속성

`LeagueGrade`는 경쟁 등급이고, 매 시즌 같은 등급의 10팀으로 `LeagueGroup`을 구성한다.

```text
Player Team 1
+ 같은 등급 AI Team 9
= ActiveLeagueGroup 10 Teams
```

- 한 번 만난 AI 구단과 선수는 시즌이 끝나도 사라지지 않는 `TeamState`와 `PlayerState`다.
- 승격한 AI 구단은 다음 등급에서도 플레이어와 다시 만날 수 있다.
- 강등·잔류한 구단은 해당 등급의 Rival Pool에 남는다.
- 다음 등급에 팀이 부족하면 `LeagueGroupSeed`로 신규 AI 구단을 생성하고 이후 영구 보존한다.
- 신규 AI 구단의 전력은 해당 등급 목표 범위 안에서 만들되 선수 이름이나 팀 이름으로 보정하지 않는다.
- 같은 AI 구단이 동시에 두 `LeagueGroup`에 배치될 수 없다.
- 이미 만난 라이벌을 우선 배치하되 매 시즌 최소 세 팀은 새로운 상대가 되도록 후보 가중치를 둔다.

초기 구현은 플레이어가 속한 `ActiveLeagueGroup`만 72경기 전체를 시뮬레이션한다.
화면 밖 등급의 구단은 `BackgroundSeasonSimulator`로 시즌 요약, 성장, 노쇠, 승강만 처리한다.
해당 구단이 플레이어와 같은 그룹에 들어오면 다시 전체 `MatchSimulator`를 사용한다.

Background 결과도 구단 역사와 선수 시즌 기록에 반영하되,
기록 정밀도가 필요한 수상·통산 기록 시스템을 구현하기 전에는 `SummaryRecord`로 명확히 구분한다.

### 5.4 순위

정규 시즌 순위 기준:

1. 승률
2. 상대 전적
3. 득실점 차
4. 다득점
5. `SeasonTiebreakSeed`

마지막 항목까지 같은 경우에도 추가 랜덤 추첨을 하지 않는다.
시즌 생성 때 저장한 `SeasonTiebreakSeed`로 순서를 고정한다.

### 5.5 승격·잔류·강등

| LeagueGrade | 승격 | 잔류 | 강등 |
|---|---:|---:|---:|
| Rookie | 1~6위 | 7~10위 | 없음 |
| Minor | 1~4위 | 5~8위 | 9~10위 |
| Major | 1~4위 | 5~8위 | 9~10위 |
| World | 1~3위 | 4~7위 | 8~10위 |
| AllStar | 1~3위 | 4~7위 | 8~10위 |
| Classic | 1~2위 | 3~7위 | 8~10위 |
| Winners | 1~2위 | 3~7위 | 8~10위 |
| Champion | 1~2위 | 3~7위 | 8~10위 |
| Master | 1~2위 | 3~7위 | 8~10위 |
| Galaxy | 없음 | 1~8위 | 9~10위 |

- 첫 `Rookie` 시즌은 강등이 없다.
- 승격은 정규 시즌 성적으로 결정해 짧은 시리즈의 운이 장기 진행을 막지 않게 한다.
- 강등 시 선수 능력치나 획득 콘텐츠를 빼앗지 않는다.
- 반복 강등을 막기 위한 숨은 보정은 사용하지 않는다. 대신 다음 시즌 상대 구성과 영입 기회를 개선한다.

### 5.6 Postseason

- 정규 시즌 상위 4팀이 진출한다.
- Semi Final은 `1위 vs 4위`, `2위 vs 3위` Best-of-3이다.
- Championship Series는 Best-of-5이다.
- 정규 시즌 상위 팀은 첫 경기 Home과 마지막 경기 Home을 보장받는다.
- 포스트시즌 우승은 트로피, 역사, 추가 영입 선택권을 주지만 승격 조건을 대체하지 않는다.
- 포스트시즌 경기 슬롯에는 진출 팀만 경기하며 나머지 팀은 정규 시즌을 마친 상태다.

### 5.7 상위 리그의 난이도

상위 리그는 아래 요소로 어려워진다.

- AI 선수의 현재 능력과 로스터 깊이
- 포지션 중복을 줄인 로스터 구성
- 피로와 불펜 역할 관리 정확도
- 상대 `ManagerStyle`에 맞는 `GamePlan` 선택
- `TacticCard`를 유리한 경기와 상황에 아껴 쓰는 판단
- Skill Board, 유학, 연도 팀 컬러의 완성도

다음 방식은 사용하지 않는다.

```text
Galaxy 보정으로 AI Contact +10
상위 리그라서 플레이어 Home Run 확률 감소
승격전이므로 상대에게 숨은 승률 보너스
```

---

## 6. LeagueClock과 경기 시간

### 6.1 기본 일정

한 `LeagueDay`에는 여섯 개의 경기 슬롯이 있다.

```text
09:00
12:00
15:00
18:00
21:00
23:00
```

시간은 `LeagueScheduleDefinition` 데이터로 관리하며 코드에 고정하지 않는다.
각 슬롯마다 현재 리그 그룹의 10팀이 한 경기씩 치른다.

### 6.2 진행 모드

#### Standard Mode — 기본 권장

- 시간은 세이브 내부의 가상 `LeagueClock`이다.
- 플레이어가 `Next Match` 또는 `Next LeagueDay`를 누르면 다음 슬롯으로 이동한다.
- 지정 시간에 경기한다는 리듬은 유지하지만 현실 시간 대기를 강요하지 않는다.
- 여러 시즌을 연속으로 플레이하는 본 프로젝트의 기본 모드다.

#### Classic Schedule Mode — MVP 이후 선택 기능

- 가상 리그 시간을 현실 시간표에 매핑한다.
- 앱이 꺼져 있어도 다음 실행 시 지난 슬롯을 순서대로 Catch-up Simulation한다.
- 하루의 상대와 일정을 미리 공개해 모든 경기의 전략을 사전 예약할 수 있다.
- 시즌 중에는 진행 모드를 바꿀 수 없다.
- 시스템 시각은 어떤 Round가 만료됐는지만 판단한다. 경기 결과 Seed에는 사용하지 않는다.

두 모드는 동일한 `LeagueRound`와 `MatchSimulator`를 사용한다.
차이는 다음 Round를 소비하는 시점뿐이다.

### 6.3 Round 상태

```text
Scheduled
→ PreparationOpen
→ InputLocked
→ Simulated
→ Presented
```

- `PreparationOpen`: 상대와 예상 선발이 공개된다.
- `InputLocked`: Lineup, GamePlan, TacticCard, Condition을 `MatchInputSnapshot`으로 저장한다.
- `Simulated`: 리그의 5경기를 고정 순서로 계산한다.
- `Presented`: 결과, 기록, 전술 보고서를 볼 수 있다.
- 이미 `InputLocked`가 된 경기는 불러오기로 전술을 바꿀 수 없다.

### 6.4 플레이어 부재 처리

플레이어가 준비하지 못한 경우:

1. 마지막으로 저장된 유효 Lineup을 사용한다.
2. 부상·유학·피로로 무효가 된 자리는 `AutoLineupAi`가 교체한다.
3. 미리 지정한 `StandingGamePlan`을 사용한다.
4. `TacticCard`는 자동 소비하지 않는 것이 기본이다.
5. 플레이어가 별도로 만든 `AutoTacticRule`이 있을 때만 조건에 맞춰 사용한다.

현실 시간 모드가 하루 여러 번 접속을 강요하지 않도록 하루 여섯 경기의 준비를 한 번에 저장할 수 있다.

---

## 7. 선수 데이터

### 7.1 공통 정보

```text
PlayerId
이름 / 나이 / 투타
PrimaryPosition
SecondaryPositions
PositionProficiency
TeamId
Condition / Fatigue
Potential / WorkEthic / InjuryProne
Career / Contract / SeasonRecords
```

### 7.2 타격 능력치

| Attribute | 의미 | 주요 결과 |
|---|---|---|
| Contact | 공을 맞히는 능력 | Contact, Hit, Strikeout |
| Power | 강한 타구를 만드는 능력 | Double, Triple, Home Run |
| Eye | Ball과 Strike 판단 | Walk, Chase, Count Advantage |

초기 `BuntExecution`은 별도 능력치를 추가하지 않고 다음 고정식으로 파생한다.

```text
BuntExecution = Contact × 0.50 + Eye × 0.20 + Speed × 0.30
```

대량 시뮬레이션에서 번트 전문성 표현이 부족한 것이 확인될 때만 `Bunt` 능력치를 분리한다.

### 7.3 수비·주루 능력치

| Attribute | 의미 | 주요 결과 |
|---|---|---|
| Speed | 주루 속도와 수비 범위 보조 | Steal, Extra Base, Range |
| Defense | 타구 판단과 포구 | Out Conversion, Error |
| Arm | 송구 강도와 정확도 | Assist, Runner Hold |

실제 수비 기여는 포지션 적응도를 함께 사용한다.

```text
EffectiveDefense = Defense × PositionProficiencyModifier
```

좋은 수비 선수를 익숙하지 않은 포지션에 넣으면 정상 능력을 전부 발휘하지 못한다.

### 7.4 투수 능력치

| Attribute | 의미 | 주요 결과 |
|---|---|---|
| Stuff | 타자가 대응하기 어려운 공 | Strikeout, Weak Contact |
| Control | 원하는 위치에 던지는 능력 | Ball, Walk, Count |
| Movement | 장타를 억제하는 변화 | Ground Ball, Extra-Base Hit 억제 |
| Stamina | 투구 능력 유지 시간 | Pitch Count Fatigue |
| Composure | 위기 상황의 기복 | Runner on Base 상황 편차 |

### 7.5 포지션

```text
C
1B
2B
3B
SS
LF
CF
RF
DH
SP
RP
```

- `PrimaryPosition`은 100% 효율이다.
- `SecondaryPosition`은 개별 `PositionProficiency`를 가진다.
- 등록되지 않은 포지션은 큰 수비 페널티를 받되 긴급 기용은 가능하다.
- `SP`와 `RP`는 투수 역할이며 투수 능력과 회복 규칙을 공유한다.
- Two-Way Player는 MVP 이후로 제외한다.

---

## 8. 경기 준비

### 8.1 OpponentReport

상대가 확정되면 다음 정보를 보여 준다.

```text
예상 StartingPitcher와 투타
최근 10경기 공격 / 수비 / 투수 지표
주요 타자 3명
불펜 피로도
상대 ManagerStyle
자주 사용한 GamePlan
확인된 TacticCard 성향
```

- 하위 리그에서는 핵심 정보가 대부분 공개된다.
- 상위 리그에서는 정보량을 숨기는 대신 `ScoutingAccuracy`에 따라 예상 범위를 보여 준다.
- 숨은 정보가 경기 결과를 조작하지 않는다. 플레이어에게 보이는 정확도만 달라진다.

### 8.2 MatchPlanState

각 경기는 독립된 준비 상태를 가진다.

```text
LineupId
StartingPitcherId
BullpenRoles
BenchRoles
GamePlanId
SelectedTacticCardIds
PlayerOverrides
```

한 경기의 설정을 바꿔도 다음 경기의 기본 설정을 임의로 덮어쓰지 않는다.
원하면 현재 설정을 `StandingPlan` 또는 Preset으로 저장할 수 있다.

### 8.3 GamePlan

`GamePlan`은 매 경기 반드시 하나를 선택하는 무료 감독 지시다.

| GamePlan | 주요 판단 변화 | 대가 |
|---|---|---|
| Balanced | 기본 기대값 중심 | 특화 없음 |
| Early Pressure | 1~3회 공격과 대주자 판단 적극화 | 후반 대타·주루 자원 감소 |
| Work the Starter | 초반 Chase와 Swing 억제 | 초구 좋은 공을 놓칠 수 있음 |
| One-Run Baseball | 진루타·번트·도루 판단 적극화 | Big Inning 기대값 감소 |
| Trust the Starter | 선발 교체 기준 완화 | 급격한 실점 위험 |
| Bullpen Pressure | 선발 교체 기준 강화, 강한 RP 조기 사용 | 다음 경기 불펜 피로 증가 |

`GamePlan`은 능력치에 일괄 `+N`을 주지 않는다.
`ManagerAi`의 선택 임계값과 일부 명시된 상황 확률만 바꾼다.

---

## 9. TacticCard

### 9.1 역할

`TacticCard`는 상대가 공개된 뒤 선택하는 제한된 경기별 준비 수단이다.

- 카드는 한 번 얻으면 영구 해금된다.
- 카드를 사용할 때 카드 자체를 소모하지 않는다.
- 사용 시 `PreparationPoint`를 소비한다.
- 한 `LeagueDay`의 여섯 경기에 자원을 어떻게 나눌지가 핵심 선택이다.
- `Rookie`는 경기당 한 장, `Major`부터 경기당 두 장까지 장착할 수 있다.
- 같은 `TacticGroup`의 카드는 한 경기에 중복 장착할 수 없다.

### 9.2 PreparationPoint

초기 목표값:

```text
LeagueDay 시작: 6 Point
다음 날 이월: 최대 2 Point
일반 카드: 1 Point
강한 상황 카드: 2 Point
Signature 카드: 3 Point
```

여섯 경기에 모두 강한 카드를 사용할 수 없기 때문에 상대 전력, 선발 매치업, 순위 중요도를 비교해야 한다.
수치는 `BalanceTable`에서 조정한다.

### 9.3 초기 카드

| TacticCard | Cost | Trigger | 효과 | 대가 또는 실패 조건 |
|---|---:|---|---|---|
| Promise of Eighth | 3 | 8회 시작, 2점 차 이내 | 주요 대타·Setup을 8회 승부처에 집중 | 이전 위기에서도 핵심 자원을 아낄 수 있음 |
| Aggressive Steal | 1 | 1루 주자, 도루 가능 | Steal 시도 기준을 낮춤 | 성공률 자체는 오르지 않아 느린 주자는 손해 |
| Aggressive Attack | 1 | 유리한 Count의 Strike | 초반 Swing과 강한 타구 선택 증가 | Walk 감소, 헛스윙 증가 가능 |
| Aggressive Bunt | 1 | 무사 1루 또는 1·2루, 2점 차 이내 | Bunt 시도 기준을 낮춤 | Out 하나를 지불하고 Big Inning 확률 감소 |
| Wait Them Out | 1 | Control이 낮거나 피로한 Pitcher | Chase 감소, 투구 수 증가 | Called Strikeout과 좋은 초구를 놓칠 위험 |
| Quick Hook | 2 | Starter Fatigue 또는 연속 출루 | 교체 기준을 빠르게 적용 | Bullpen 피로와 다음 경기 부담 증가 |
| Guard the Lead | 2 | 7회 이후 리드 | 수비 교체와 강한 RP 우선 | 추격 시 사용할 공격 Bench 감소 |
| Platoon Ambush | 1 | 상대 선발과 반대 손 타자 | Platoon 대타를 일찍 사용 | 후반 대타 선택 감소 |

### 9.4 Promise of Eighth 상세

`Promise of Eighth`는 단순 8회 능력치 증가 카드가 아니다.

```text
선택 시
→ ManagerAi가 최고 Setup과 1순위 PinchHitter를 가능한 한 8회까지 보존

8회 시작 시 2점 차 이내
→ TacticActivatedEvent 생성
→ 공격이면 대타·대주자 교체 임계값 강화
→ 수비이면 Leverage가 가장 높은 RP 사용 허용
→ 해당 이닝 Composure의 부정적 편차만 소폭 완화

조건 불충족
→ 미발동
→ 이미 지불한 PreparationPoint는 반환하지 않음
```

따라서 6회에 무너지거나 8회 전에 큰 점수 차가 나면 손해가 된다.
플레이어는 접전 가능성이 높은 경기인지 판단해야 한다.

### 9.5 카드 획득

- 확률형 팩을 사용하지 않는다.
- `LeagueGrade` 승격, 시즌 업적, 특정 감독 과제 달성으로 정의를 해금한다.
- 해금 전 카드의 조건과 효과를 미리 볼 수 있다.
- 같은 카드의 등급별 중복 버전을 만들지 않는다.
- 상위 카드는 더 큰 숫자가 아니라 더 좁은 조건과 새로운 판단을 제공한다.

### 9.6 전술 설명

경기 중 다음 이벤트를 남긴다.

```text
TacticReservedEvent
TacticActivatedEvent
TacticDecisionEvent
TacticExpiredEvent
ManagerDecisionEvent
```

경기 후 보고 예:

```text
Promise of Eighth — 발동

7회말: Setup 김민석을 보존하고 Middle Relief 박준호를 기용
8회초: 1점 차, PinchHitter 이도윤 기용
예상 Contact 61 → 상황 보정 64
결과: 1타점 Double

주의: 이 카드는 결과를 보장하지 않으며 당시 판단의 근거만 표시한다.
```

---

## 10. ManagerStyle과 ManagerAi

### 10.1 ManagerStyle

창단 시 플레이어는 감독 성향 하나를 선택한다.

| ManagerStyle | 기본 성향 | SignatureGamePlan |
|---|---|---|
| Analyst | 상대 Matchup과 Platoon 중시 | Target the Weakness |
| Aggressor | 빠른 승부와 주루 압박 | Relentless Pressure |
| Traditionalist | 선발, 수비, 한 점 운영 중시 | Fundamental Baseball |
| Developer | 젊은 선수 출장과 성장 중시 | Prospect Opportunity |

- 성향은 자동 설정과 추천에 영향을 주며 숨은 승률 보너스를 주지 않는다.
- 승격 시 새로운 `ManagerTrait` 선택지가 열린다.
- `ManagerTrait`은 전술 비용, Scouting 정보, AutoLineup 규칙을 바꾸며 선수 모든 능력치를 올리지 않는다.
- 오프시즌에 한 번 `ManagerStyle`을 재선택할 수 있다.
- AI 구단 감독도 같은 데이터 구조와 규칙을 사용한다.

### 10.2 ManagerAi 우선순위

```text
야구 규칙상 가능한가?
↓
플레이어의 명시적 PlayerOverride가 있는가?
↓
활성 TacticCard의 강제 규칙이 있는가?
↓
선택한 GamePlan의 판단 기준
↓
ManagerStyle의 기본 성향
↓
기본 Run Expectancy 판단
```

플레이어의 직접 지시는 항상 성향보다 우선한다.
다만 불가능한 지시나 이미 소진된 선수 기용은 실행하지 않고 이유를 보고한다.

---

## 11. 경기 시뮬레이션

### 11.1 MatchInputSnapshot

경기 잠금 시 다음 입력을 불변 Snapshot으로 만든다.

```text
SeasonId / LeagueRoundId / GameId / RandomSeed
HomeTeam / AwayTeam
Lineup / Bench / StartingPitcher / BullpenRoles
Player Attributes / Condition / Fatigue
ManagerStyle / GamePlan / TacticCards
YearTeamColor / SkillBoard Modifiers
BalanceTableVersion
```

`MatchSimulator`는 Snapshot만 읽고 런타임 구단 상태나 UI를 직접 참조하지 않는다.

### 11.2 Round 처리

```text
LeagueRoundSimulator
→ GameId 순서로 5개 MatchInputSnapshot 생성
→ 각 MatchSimulator 실행
→ MatchEvent와 BoxScore 수집
→ 모든 경기 성공 시 LeagueRoundResult 확정
→ 순위와 선수 기록 반영
```

- 한 경기 오류 때문에 일부 팀만 경기를 더 치른 상태를 저장하지 않는다.
- Round 결과 반영은 원자적으로 처리한다.
- 병렬화하더라도 각 경기 RNG와 결과 정렬 순서는 `GameId`로 고정한다.
- AI 경기와 플레이어 경기는 같은 야구 규칙과 확률 모델을 쓴다.

### 11.3 효과 적용 순서

```text
Base Attributes
→ 성장·노쇠·유학
→ Condition / Fatigue
→ Skill Board
→ Year Team Color
→ GamePlan
→ TacticCard
→ ManagerAi Decision
→ PlateAppearanceSimulator
```

같은 확률 채널에 여러 효과가 들어오면 `BalanceTable`의 상한을 적용한다.
전술이 실제 선수 능력보다 큰 영향을 주지 않도록 모든 전술을 ON/OFF 대량 시뮬레이션으로 비교한다.

---

## 12. 경기 결과와 다음 결정

### 12.1 결과 화면 우선순위

1. 최종 Score와 승패
2. 승부처 3개
3. 내 GamePlan 평가
4. TacticCard 발동·미발동 이유
5. Lineup과 투수 교체의 주요 영향
6. Condition / Fatigue 변화
7. 선수 기록과 성장
8. 다음 상대와 순위 변화

### 12.2 패배 설명

패배 이유는 하나로 단정하지 않는다.

```text
주요 원인
- 5회까지 상대 선발에게 Strikeout 8개
- 좌타자 4명이 상대 좌완 선발에게 평균 Contact -4
- Promise of Eighth를 위해 보존한 Setup이 6회 위기에서 등판하지 않음

다음 경기 제안
- 우타 Bench 기용 검토
- Work the Starter 대신 Early Pressure 비교
- 불펜 피로가 높아 Quick Hook 비추천
```

제안은 사용 가능한 선택지를 알려 줄 뿐 자동 정답 버튼이 되지 않는다.

### 12.3 전술의 기대 효과

경기 후 실제 점수에 억지로 기여도를 배분하지 않는다.
대신 전술이 개입한 시점의 선택, 당시 성공 확률, 실제 결과를 기록한다.

같은 Seed로 전술만 제거한 가상 재경기를 공식 결과처럼 제시하지 않는다.
RNG 소비 순서가 달라지는 Counterfactual은 플레이어를 오도할 수 있기 때문이다.

---

## 13. 선수 영입과 성장

### 13.1 Rookie 이후 영입

정식 로스터 완성 후에는 기준 문서의 경로를 사용한다.

```text
신인 Draft
Trade
FA
자유계약
Scouting
```

- 선수는 확률형 카드 팩이 아니라 실제 리그의 한 인물이다.
- UI는 카드 형태를 사용할 수 있지만 같은 선수의 등급별 복제 카드를 만들지 않는다.
- `TryoutChoice`는 첫 구단 창단을 위한 한시적 시스템이며 매 시즌 반복하지 않는다.
- 상위 리그 보상은 선수 자체보다 더 정확한 정보와 더 넓은 후보 선택권을 제공한다.

### 13.2 성장 콘텐츠 개방

| 시점 | 콘텐츠 |
|---|---|
| Rookie Game 8 | 기본 구단 운영 완전 개방 |
| Minor 진입 | Short 유학과 성장 계획 |
| Major 진입 | 4×4 Skill Board, TacticCard 두 번째 슬롯 |
| World 진입 | 첫 연도 팀 컬러 선택 |
| AllStar 이상 | 고급 감독 Trait과 전문 TacticCard |
| Galaxy | 구단 역사, Dynasty 목표, 반복 우승 도전 |

상위 리그까지 기본 재미를 잠그지 않는다.
`Rookie`에서 Lineup·GamePlan·TacticCard의 완성된 작은 루프를 경험하게 하고,
이후 리그는 성장 깊이와 상대 판단을 추가한다.

### 13.3 패배해도 남는 것

- 선수 출장과 시즌 기록
- 선수 성장과 SkillPoint
- 상대 정보
- 감독 과제 진행도
- 구단 역사

승리 보상만 성장의 유일한 수단으로 만들지 않는다.
그렇지 않으면 약한 구단이 더 약해지는 Snowball이 발생한다.

---

## 14. 화면 흐름

### 14.1 Home

```text
현재 LeagueGrade / 순위
다음 경기 시간 또는 Next Match
상대 Team / 예상 StartingPitcher
준비 완료 여부
남은 PreparationPoint
오늘의 6경기 일정
최근 경기 결과
로스터 긴급 알림
```

### 14.2 Match Preparation

```text
OpponentReport
내 Lineup과 상대 예상 Lineup 비교
StartingPitcher Matchup
GamePlan 선택
TacticCard 선택
변경 전후 예상 영향
Input Lock 시간
```

### 14.3 League

```text
순위
승격 / 잔류 / 강등선
남은 일정
상대 전적
팀별 최근 흐름
Postseason 진출 확률
```

### 14.4 Rookie Club Building

```text
FranchisePlayer 중심 화면
AcademyPlayer 교체 현황
남은 정식 로스터 자리
TryoutChoice 비교
현재 팀 정체성 변화
25인 완성 진행도
```

---

## 15. 데이터와 레이어

### 15.1 Baseball.Core

```text
LeagueGrade
LeagueSeasonState
LeagueRoundState
LeagueScheduleDefinition
TeamState
RosterState
PlayerState
AcademyPlayerState
ManagerProfile
MatchPlanState
GamePlanDefinition
TacticCardDefinition
TacticLoadoutState
PreparationPointState
OpponentReportData
TryoutChoiceState
```

모두 순수 C#이며 `UnityEngine`을 참조하지 않는다.

### 15.2 Baseball.Simulation

```text
ScheduleGenerator
LeagueRoundSimulator
BackgroundSeasonSimulator
MatchSimulator
PlateAppearanceSimulator
ManagerAi
AutoLineupAi
TacticResolver
RosterStrengthEvaluator
StandingsCalculator
```

### 15.3 Baseball.Game

```text
LeagueClockService
LeagueProgressionService
MatchPreparationService
OfflineCatchUpService
TryoutService
RecruitmentService
SaveMigrationService
```

- 시스템 시각 접근은 `LeagueClockService`에만 허용한다.
- Simulation에는 현재 시각을 전달하지 않고 확정된 `LeagueRoundId`만 전달한다.
- ScriptableObject 정의는 Game 레이어에서 순수 C# 구조로 변환한다.

### 15.4 Baseball.Presentation

```text
UI_Scene_Home
UI_Scene_League
UI_Scene_Roster
UI_Scene_MatchPreparation
UI_Scene_MatchResult
UI_Popup_TryoutChoice
UI_Popup_TacticReport
```

Presentation은 전술 효과, 순위, 승격 여부를 계산하지 않는다.

### 15.5 세이브 필수 항목

```text
SaveVersion
LeagueClockMode
CurrentLeagueGrade
SeasonId / LeagueDay / LeagueRoundId
LastProcessedRoundId
전체 LeagueSchedule
모든 Team / Player / Record
Rival Pool과 AI 구단의 현재 LeagueGrade
AcademyPlayer 교체 상태
ManagerProfile
MatchPlan과 Input Lock 상태
PreparationPoint
해금한 TacticCard
Tryout 진행도와 선택하지 않은 선수
RandomSeed
BalanceTableVersion
```

---

## 16. 밸런스 목표

아래 수치는 구현 전 가설이며 대량 시뮬레이션으로 확정한다.

| 항목 | 초기 목표 |
|---|---:|
| 첫 선수 Archetype 간 Rookie 시즌 기대 승수 차이 | 2승 이내 |
| AcademyPlayer만으로 구성된 팀 승률 | 35~45% |
| 정식 25인 완성 직후 Rookie 평균 승률 | 45~55% |
| 적절한 GamePlan vs Balanced | +1~3%p |
| 조건에 맞는 일반 TacticCard | +1~2%p |
| 조건에 맞는 Signature TacticCard | +2~4%p |
| 잘못 사용한 TacticCard | 0 이하도 가능 |
| 동일 전력에서 전술 전체 영향 상한 | +6%p 이내 |

전술 수치를 바꿀 때 비교할 항목:

```text
승률
경기당 득점
AVG / OBP / SLG
BB / SO
Steal 시도와 성공률
Bunt 시도와 득점 기대값
선발 평균 IP
불펜 Fatigue
카드 발동률 / 미발동률
리그별 승격률
```

최소 10,000경기와 1,000시즌을 비교한다.
전술을 켜고 끈 결과뿐 아니라 Contact형, Power형, Speed형, Pitching형 로스터별 결과를 분리한다.

---

## 17. 필수 테스트

### League

- 10팀·72경기 일정에서 모든 팀의 경기 수와 Home/Away 수가 일치한다.
- 한 Round에 같은 팀이 두 경기를 치르거나 쉬지 않는다.
- 같은 상대와 세 경기 이상 연속 배정되지 않는다.
- 순위 Tie Break와 승격·강등 결과가 결정론적이다.
- Catch-up Simulation과 한 경기씩 진행한 결과가 완전히 같다.
- 승격한 AI 구단이 다음 LeagueGroup 후보로 유지되고 신규 팀이 중복 생성되지 않는다.

### Rookie Start

- 첫 선수의 Position과 관계없이 유효한 25인 AcademyRoster가 만들어진다.
- 8번의 TryoutChoice 후 정확히 25명의 OwnedPlayer가 된다.
- 모든 필수 Position과 PitcherRole이 채워진다.
- 선택하지 않은 후보가 중복 생성되거나 사라지지 않는다.

### Match Preparation

- Input Lock 이후 Lineup과 전술이 바뀌지 않는다.
- 부상·유학 선수의 자동 교체가 유효한 Lineup을 만든다.
- 플레이어 Override가 ManagerStyle과 GamePlan보다 우선한다.
- PreparationPoint 부족, Card Group 충돌, 발동 조건을 검증한다.

### Simulation

- 같은 Seed와 Snapshot의 MatchEvent가 완전히 일치한다.
- TacticCard를 사용하지 않으면 기존 기준 통계가 변하지 않는다.
- TacticEvent와 실제 ManagerDecision의 연결이 일치한다.
- Round 도중 실패해도 일부 경기만 기록에 반영되지 않는다.

---

## 18. 구현 순서

현재 기준 문서의 Phase 1 성공 기준을 먼저 만족한다.

### System Slice 1 — Single Player Start

```text
FranchisePlayer 후보 4명
AcademyRoster 자동 생성
첫 경기
TryoutChoice 1회
```

완료 기준:

> 선수 한 명을 선택한 뒤 추가 수동 편성 없이 유효한 9이닝 경기를 진행하고, 첫 영입 결정을 내릴 수 있다.

### System Slice 2 — Rookie Round

```text
10 Team
18 Round 축약 시즌
Round Schedule
Standing
Tryout 8회
```

완료 기준:

> 모든 팀이 매 Round 한 경기씩 치르고, 8경기 후 정식 25인 로스터가 완성된다.

### System Slice 3 — Match Preparation

```text
OpponentReport
GamePlan 3종
TacticCard 4종
PreparationPoint
TacticReport
```

초기 카드는 다음 네 장만 구현한다.

```text
Promise of Eighth
Aggressive Steal
Aggressive Attack
Aggressive Bunt
```

### System Slice 4 — Full Rookie Season

```text
72 Regular Season Games
Postseason
Promotion
Season Reward
Save / Load / Catch-up
```

### System Slice 5 — League Climb

```text
Minor → Galaxy
AI Roster Strength Curve
System Unlock
Relegation
Long-Term History
```

10개 등급을 한 번에 만들지 않는다.
`Rookie → Minor → Major`의 세 등급에서 승강과 밸런스를 먼저 검증한 뒤 나머지를 데이터로 확장한다.

---

## 19. 초기 콘텐츠 수량

```text
LeagueGrade                 10
Team per LeagueGroup        10
Starting Archetype           4
ManagerStyle                 4
GamePlan                     6
Initial TacticCard           8
Rookie Tryout Round          8
Regular Season Match        72 per Team
Postseason Team              4
```

실제 구현 첫 Slice에서는 다음만 필요하다.

```text
LeagueGrade                  1 (Rookie)
Team                        10
Starting Archetype           4
GamePlan                     3
TacticCard                   4
Regular Season Match        18 축약 검증
```

---

## 20. 만들지 않을 것

- 현실 시간에 접속하지 않았다는 이유로 손해를 주는 출석 구조.
- 상대 공개 뒤 짧은 시간 안에 접속을 강요하는 설계.
- 상위 리그의 숨은 확률 보정과 승격전 조작.
- 전술 카드의 확률형 획득, 카드 강화, 실패 파괴.
- 전술 카드를 사용하지 않으면 이길 수 없는 수치 인플레이션.
- AcademyPlayer를 의도적으로 형편없게 만들어 초반 결제를 유도하는 구조.
- 한 선수만 직접 타격·투구하는 액션 게임 전환.
- 플레이어 경기만 계산하고 다른 팀 순위를 임의 생성하는 방식.
- 결과 이유를 남기지 않는 자동 감독 판단.

---

## 21. 참고한 레퍼런스 해석

과거 `프로야구 매니저`에서 가져오는 것은 다음 경험이다.

- 사용자가 접속하지 않아도 정해진 리그 일정에 따라 자동 경기가 진행되는 기대감.
- 경기 결과와 Replay를 보고 선수 구성, 전략, 스킬을 다시 고민하는 루프.
- `Rookie`, `Minor`, `Major`에서 시작해 장기간 상위 리그로 진출하는 목표.
- 상위 리그가 열리며 선수 육성과 구단 구성의 깊이가 증가하는 흐름.

그대로 가져오지 않는 것은 실제 시간 강제, 온라인 상대 의존, 선수 카드 가챠, 지속적인 최고 등급 추가다.

참고 자료:

- [프로야구 매니저 강상용 팀장 인터뷰](https://www.gamemeca.com/view.php?gid=82139)
- [프로야구 매니저 리그 등급과 승강 요건 정리](https://librewiki.net/wiki/%ED%94%84%EB%A1%9C%EC%95%BC%EA%B5%AC_%EB%A7%A4%EB%8B%88%EC%A0%80)
- [프로야구 매니저 초기 플레이 리뷰](https://www.inven.co.kr/webzine/news/?news=27535)

---

## 22. 최종 성공 기준

첫 번째 플레이 가능한 게임 시스템은 다음 경험을 완성해야 한다.

> 플레이어가 마음에 드는 선수 한 명을 선택한다.
>
> 부족한 자리는 AcademyPlayer와 함께 첫 경기를 치른다.
>
> 상대 선발과 팀 성향을 보고 GamePlan과 TacticCard를 정한다.
>
> 자동 경기 결과에서 그 선택이 언제, 왜 작동했는지 확인한다.
>
> 경기 후 새 선수들을 영입해 자기 팀의 빈자리를 하나씩 채운다.
>
> 완성된 구단으로 Rookie 리그를 통과하고 다음 리그를 누른다.

이 흐름이 재미있지 않다면 `Galaxy`까지의 등급, 추가 카드, 성장 콘텐츠를 늘리지 않는다.
