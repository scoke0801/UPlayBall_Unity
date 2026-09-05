# Historical World — 구현 명세 인덱스

## 0. 최상위 Source of Truth

이 폴더는 실제 Source 선수·구단 시즌을 Runtime-safe Canonical Content로 Bake하고, 그 고정
콘텐츠로 World마다 새로운 이름과 야구 역사를 만드는 계약을 정의한다.

```text
Source PlayerPerson / PlayerSeason / TeamSeason
        ↓ 1:1 Canonical Bake
PlayerPersonDefinition / PlayerSeasonDefinition / TeamSeasonDefinition / Core25
        ↓
WorldIdentityRegistry
        ↓
Detailed Match / Season Simulation
        ↓
Statistics / Standings / Postseason / Awards
        ↓
WorldHistorySnapshot / WorldCardCatalog / SpecialCompositeTeam
```

> 선수 및 일반 Franchise를 다른 Reference 선수·구단으로 합성하지 않는다. PlayerSeason과
> TeamSeason의 능력 데이터 정체성은 Source와 1:1로 유지하며, World에서 가상화되는 것은 표시
> Identity와 Simulation History다.

`가상 선수`는 여러 실존 선수의 능력치를 섞은 새 선수가 아니다. 이 문서에서 정확한 용어는 다음과 같다.

- **Canonical Player**: 한 Source Person/Season을 1:1로 변환한 고정 게임 데이터.
- **World Player Identity**: Canonical Person에 해당 World가 부여하고 저장한 표시 이름.
- **Virtual Statistics / History**: Canonical 선수와 구단을 실제 Match Simulation에 투입해 얻은 결과.

## 1. 절대 계약

1. 같은 Source Person의 여러 시즌은 한 `PlayerPersonId` 아래 연결하고, 다른 Source Person의 시즌을
   이어 인공 커리어를 만들지 않는다.
2. 한 `PlayerSeasonDefinition`은 한 Source PlayerSeason에서 파생한다. 모집단 평균·표준편차·백분위·
   Z-Score는 Source 자신의 값을 정규화하는 기준으로만 쓴다.
3. 한 `TeamSeasonDefinition`은 한 Source TeamSeason에서 파생한다. Core25는 해당 Source TeamSeason
   내부 선수만으로 선정하며 타 구단 선수를 조용히 보충하지 않는다.
4. `BaseAttributes`, `Cost`, `TrainingCeiling`, Position/Role/RegistrationType/Origin은 Offline Bake
   이후 고정한다. World Seed, 표시 이름, Simulation 성적, Award로 다시 계산하지 않는다.
5. 실제 선수명·구단명은 Runtime 표시 콘텐츠가 아니다. 이름은 `PlayerPersonId`와 `FranchiseId`를
   키로 World 생성 시 확정하고 Save한다.
6. 한 World 안에서 Player/Franchise 표시 이름은 각각 고유해야 하고 프로젝트가 보유한 실제 이름과
   exact match해서는 안 된다. 같은 Person의 모든 시즌과 같은 Franchise의 모든 연도는 같은 이름이다.
7. 정식 과거 기록은 Source 기록 복사가 아니라 `DetailedMatchEngine` 계열 경기 Simulation으로
   만든다. 개인·팀 통계, 순위, 포스트시즌, 수상은 World Seed에 따라 달라질 수 있다.
8. Award는 World Statistics를 입력으로 정한다. Source Award나 BaseAttributes 순위로 직접 정하지 않는다.
9. `OriginalHistory`는 Legacy/Debug/Validation 전용이다. 사용자 선택 가능한 정식 새 게임 경로는
   Historical Simulation 하나다.
10. Save/Load에서는 Identity, History, Statistics, Standings, Award를 복원하고 생성기를 재실행하지 않는다.

## 2. Canonical Content와 World State

| 경계 | 소유 데이터 |
|---|---|
| Canonical Bake | `PlayerPersonId`, `PlayerSeasonId`, `BaseAttributes`, `Cost`, `TrainingCeiling`, Position/Role, RegistrationType, Origin, `TeamSeasonDefinition`, Core25 |
| World Identity | `WorldPlayerIdentity`, `WorldFranchiseIdentity`, Generator Version/Seed, 확정 `DisplayName` |
| World History | 개인·팀 Statistics, Standings, Postseason, `WorldAwardRecord`, `WorldHistorySnapshot` |
| World Consumer | `WorldCardCatalog`, TeamColor, Scout, AI, Contract, Roster, Career |

Presentation 외 시스템은 표시 이름 문자열로 선수를 찾거나 판정하지 않는다. 모든 참조는
`PlayerPersonId`, `PlayerSeasonId`, `FranchiseId`, `TeamSeasonKey`, `Edition`을 사용한다.

## 3. 정식 World 생성 순서

```text
Canonical Content 로드
→ WorldIdentityRegistry 생성·확정
→ 정규 Canonical TeamSeason만 Historical Schedule에 투입
→ Detailed Match Simulation
→ 개인/팀 Statistics와 Standings
→ Postseason
→ All-Star / Golden Glove / Regular Season MVP
→ All-Star Game / Postseason MVP
→ WorldHistorySnapshot과 WorldAwardRecord 확정
→ WorldCardCatalog 특수 Edition 활성화
→ AllStarComposite / GoldenGloveComposite / YearSelectComposite 생성
→ Save
```

특수 합성팀은 여러 선수의 능력치를 합쳐 새 선수를 만드는 시스템이 아니다. Award 확정 뒤 기존
`PlayerSeasonId`를 참조해 만드는 별도 게임 로스터이며, 세 팀 사이 동일 PlayerSeason 중복 금지를
유지한다. 최초 Historical Simulation과 그 Award 계산에는 참가하지 않는다.

## 4. 문서 구성

| 순서 | 문서 | 역할 |
|---:|---|---|
| 1 | [01_시대보정_가상선수생성.md](01_시대보정_가상선수생성.md) | Source Person/Season 1:1, 정규화, Ability/Cost/TrainingCeiling Bake, provenance |
| 2 | [02_가상구단_승강리그_ClubDNA.md](02_가상구단_승강리그_ClubDNA.md) | Source TeamSeason 1:1, Core25, Franchise Identity, 승강/DNA |
| 3 | [03_선수풀_카드_Cost_Edition.md](03_선수풀_카드_Cost_Edition.md) | Canonical 선수/카드, World Award 기반 Edition |
| 4 | [04_팀컬러_시스템.md](04_팀컬러_시스템.md) | Stable ID와 Canonical Origin 기반 TeamColor |
| 5 | [05_구단주모드_경제_스카우트.md](05_구단주모드_경제_스카우트.md) | `WorldCardCatalog` 기반 구단주 경제·Scout·AI |
| 6 | [06_전술카드_시스템.md](06_전술카드_시스템.md) | 실제 Match Simulation과 전술/특수팀 경계 |
| 7 | [07_선수모드_연동_와일드카드.md](07_선수모드_연동_와일드카드.md) | Career와 공유하는 Canonical/World Identity |
| 8 | [08_구현_로드맵_검증기준.md](08_구현_로드맵_검증기준.md) | 구현 의존 순서와 절대 완료 기준 |
| 9 | [09_구단경영_구장_팬_관중_경제.md](09_구단경영_구장_팬_관중_경제.md) | 구장·시설·팬·관중·홈 경기 경제 |
| 10 | [10_경기전_상대분석_라인업프리셋.md](10_경기전_상대분석_라인업프리셋.md) | 공개 정보 기반 상대 분석과 재검증되는 프리셋 |
| 11 | [11_코칭스태프_시스템.md](11_코칭스태프_시스템.md) | 5개 Staff 역할, 계약·급여와 효율 Profile |
| 12 | [12_컨디션_타선_배터리_궁합.md](12_컨디션_타선_배터리_궁합.md) | 연속 Condition 원본과 타선·배터리 Chemistry |
| 13 | [13_4종_통합_구현로드맵_Codex.md](13_4종_통합_구현로드맵_Codex.md) | 09~12 통합 Gate, Production E2E와 장기 검증 |

세부 기획보다 `BaseballManager_PROJECT.md` 42절이 우선한다. Source 수집·정규화·익명화 경계는
`docs/KBO_REFERENCE_DATA_PIPELINE.md`와 `Tools/KBOImporter/README.md`를 함께 따른다.

## 5. 현재 완료 판정 원칙

새 Schema나 문서만 추가하고 기존 Synthetic Mixing 또는 임의 10구단 재배분 경로를 Production
새 게임에서 계속 호출하면 미완료다. 반대로 코드만 바꾸고 이 문서군이 Baked `FictionalName`,
Franchise Fingerprint, `OriginalHistory` 정식 선택지를 설명해도 미완료다.

밸런스 계수나 Rating 변환이 바뀌면 다수 World Seed 대량 Simulation에서 AVG/OBP/SLG, HR, BB/K,
ERA, WHIP, R/G와 팀 승률 분포를 검증한다. 문서의 과거 스냅샷 수치는 새 Canonical Bake와 현행
Simulation으로 다시 측정하기 전에는 최신 완료 근거로 인용하지 않는다.

## 6. 4종 확장 현재 상태

09~12절은 순수 Core/Simulation 계약, 구단주 Production Runtime, Save schema 4와 기존
`SharedGameShellView` 기반 uGUI 화면까지 연결됐다. 시즌 종료 급여·계약 처리와 결정론적 다음 시즌
일정 생성, 모든 저장 프리셋의 현재 Validator 표시·선택, 실제 catalog 기반 TeamColor/Tactic 2슬롯
순환 적용, Condition 1~10단계 한글 표시가 Production 코드에 포함된다. 관련 headless E2E와 Explicit
장기 통계 6건은 통과했다. 09~12 조정값은 `OwnerExpansionBalance.json`에서 저작해 구단주 전용
Balance에만 합성하며 선수 커리어 Balance와 Save에는 유출하지 않는다. 최신 통합 전 Unity 대상
실행은 39/39 통과했으나 최신 Balance/시즌 lifecycle/UI 병합본의 집계 Unity Test Runner와 Player
Build는 사용자 지시에 따라 생략했고 실제 16:9 UI도 검증하지 않았다. Definition/Resolver와
Runtime/UI 검증을 구분하기 위해 전체 판정은 **부분 완료**이며, 상세 상태와 승강·로스터 이월,
임의 선수 교체/전체 Tactic Inventory 편집, 효과 Consumer가 없는 `TacticLab` 등 잔여 Gate는 08절
§11과 13절 §16에 기록한다.
