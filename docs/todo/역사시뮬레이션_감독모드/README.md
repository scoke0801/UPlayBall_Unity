# 역사 시뮬레이션 감독모드 — 구현 명세 인덱스

## 0. 이 문서의 위치

이 폴더는 사용자가 제시한 대형 설계 제안(KBO 통계 기반 가상 리그, 가상 연도 구단, 팀컬러,
감독모드 스카우트 경제, 전술카드)을 이 프로젝트에 넣을 수 있는 형태로 재구성한 것이다.

**2026-08-31 확정:** 다음 세 가지가 프로젝트의 확정된 방향이다.

1. **감독모드를 선수 커리어 모드와 별개의 두 번째 진입점으로 신설한다.**
2. **가챠(스카우트)와 카드 중복강화 경제는 감독모드에 한해서만 예외로 허용한다.** AI 구단과
   선수 커리어 모드에는 적용하지 않는다.
3. **기존 선수 커리어 모드(로스터·라인업·전술은 감독 AI 소유)는 이 결정과 무관하게 그대로
   유지한다.**

이는 `BaseballManager_PROJECT.md`의 2026-08-26 결정("감독모드는 범위 밖", 2263행)과 23절
"과금형 카드 뽑기를 사용하지 않는다"(946~971행)를 **감독모드에 한해서만** 뒤집는 것이다.
`BaseballManager_PROJECT.md` 최상단의 2026-08-31 메모가 이 확정을 반영한다. Phase 1은 이
확정을 전제로 바로 착수한다(08절 참고).

## 1. 두 모드의 경계

```text
선수 커리어 모드 (기존, 변경 없음)
  플레이어 = 선수 1명
  로스터/라인업/전술 = 감독 AI 소유
  카드 = 선수 정보의 UI 표현일 뿐, 수집/강화 대상 아님

감독모드 (신규, 이 폴더의 범위)
  플레이어 = 가상 연도 구단의 감독
  로스터 구성 = 플레이어가 스카우트로 수집한 카드로 편성
  카드 = 수집·중복강화·판매 대상 (감독모드 전용 세이브에만 존재)
  AI 구단 = 카드 강화 없음, Base Card + TeamColor + 감독 AI 전술만 사용
```

두 모드는 완전히 분리된 세이브 슬롯을 쓴다. 감독모드의 `OwnedPlayerCardState`
(강화 단계, 중복 수)는 `PlayerDefinition`/`PlayerState` 원본에 절대 기록하지 않는다
(04절, 05절 참고). 선수 커리어 모드의 `CareerPlayerWildcard`는 감독모드 카드 시스템과
무관한 별도 판정 규칙이다(07절).

## 2. 문서 구성과 읽는 순서

| 순서 | 문서 | 내용 |
| --- | --- | --- |
| 1 | [01_시대보정_가상선수생성.md](01_시대보정_가상선수생성.md) | KBO Reference → 시대/포지션 정규화 → Synthetic Player/Team 생성 |
| 2 | [02_가상구단_승강리그_ClubDNA.md](02_가상구단_승강리그_ClubDNA.md) | TeamSeason, Rookie~Galaxy 승강, Club DNA |
| 3 | [03_선수풀_카드_Cost_Edition.md](03_선수풀_카드_Cost_Edition.md) | PlayerPerson/Season/Card 데이터 모델, Cost, Edition |
| 4 | [04_팀컬러_시스템.md](04_팀컬러_시스템.md) | TeamColorFamily, BaseStat/EffectiveStat 분리, Rating Curve |
| 5 | [05_감독모드_경제_스카우트.md](05_감독모드_경제_스카우트.md) | SP/Money/DP, Scout Pool, 중복 강화/판매, Pity Gauge |
| 6 | [06_전술카드_시스템.md](06_전술카드_시스템.md) | ManagerTacticProfile, TacticCard, 조건부 발동, 카운터 |
| 7 | [07_선수모드_연동_와일드카드.md](07_선수모드_연동_와일드카드.md) | 선수 커리어 모드와의 접점, Wildcard 판정 범위 |
| 8 | [08_구현_로드맵_검증기준.md](08_구현_로드맵_검증기준.md) | Phase 순서, 각 Phase 완료 기준, 자동 테스트 목록 |

## 3. 설계 전반의 공통 원칙

- **Core/Simulation은 Unity를 참조하지 않는다.** TeamColorResolver, ScoutRoller, SyntheticPlayerGenerator,
  TacticCardResolver는 전부 `Baseball.Simulation` 순수 C#이고 난수는 주입받은 결정론적 RNG만 쓴다.
- **실제 KBO 선수·구단은 런타임 콘텐츠로 노출하지 않는다.** `docs/KBO_REFERENCE_DATA_PIPELINE.md`의
  Editor 전용 Raw 경계를 넘겨 실명·실제 구단명을 Runtime Definition에 복사하는 코드는 만들지 않는다.
- **AI 구단에는 감독모드 전용 강화·가챠 시스템을 적용하지 않는다.** AI는 Base Card + TeamColor +
  감독 AI 전술만으로 경쟁한다(05절 80번대 원칙과 동일).
- **밸런스 수치는 전부 데이터화한다.** Cost 확률, 팀컬러 수치, 전술카드 효과는 `BalanceTable`류
  구조체로 빼고 코드에 하드코딩하지 않는다.
- **모든 수치 변경은 대량 시뮬레이션 근거를 요구한다.** 이 폴더의 수치는 전부 초기 추정값이며
  실제 구현 후 10,000경기/수백 시즌 시뮬레이션으로 재조정한다.
