# KBO Reference Data Pipeline

## 목적과 Runtime 경계

KBO Source는 선수 능력치와 구단 선수 구성의 정본이다. 하지만 실제 선수명·구단명, 실제 시즌 결과와
수상은 Runtime World의 표시 콘텐츠나 역사로 복사하지 않는다.

```text
Raw Snapshot
→ Normalized Source JSON
→ Source Person / PlayerSeason / TeamSeason 1:1 Canonical Bake
→ Runtime-safe Stable ID + Ability / Cost / TrainingCeiling / Origin / Core25
→ World Identity Generation
→ Detailed Historical Simulation
→ 가상 Statistics / Standings / Postseason / Awards
```

한 선수의 Contact/Power/Speed를 서로 다른 Reference 선수에게서 가져오거나, 여러 구단의 강점을
섞어 일반 Franchise를 만들지 않는다. 평균·표준편차·백분위·Z-Score·리그 환경은 Source 자신의
수치를 시대와 역할 안에서 평가하는 normalization 기준으로만 사용한다.

## 데이터 계층

### Raw Snapshot

수집 당시 응답과 Source Version을 보존한다. 재현 가능한 Offline 입력이며 Runtime Build에 포함하지
않는다.

### Normalized Source JSON

Editor/검수 전용이다. 원본 Player/Team ID와 이름, 시즌 기록, TeamSeason 구성, 수상 자료를 검증할
수 있다. 이름 정보는 blacklist 생성과 provenance 검수에 사용한다.

### Canonical Runtime Archive

Runtime Archive는 원본 ID를 노출하지 않는 Stable ID를 사용한다.

```text
SourcePlayerKey → PlayerPersonId
SourcePlayerKey + SeasonYear → PlayerSeasonId
SourceTeamSeasonKey → TeamSeasonKey
SourceFranchiseKey → FranchiseId
```

PlayerSeason과 TeamSeason은 Source와 1:1이다. Archive에는 `BaseAttributes`, `Cost`,
`TrainingCeiling`, Position/Role/RegistrationType, Origin, Source Team 내부 Core25를 넣는다.
Person별 고정 `fictionalName`이나 실제 Source 이름은 넣지 않는다.

World용 이름 후보 Catalog와 blacklist 검증 결과는 Canonical 선수 데이터와 분리해 배포할 수 있다.
최종 Person/Franchise DisplayName은 World 생성 시 `WorldIdentityRegistry`에 확정한다.

## Source Person / Season 연결

- 동일 Source Player ID의 모든 시즌은 같은 `PlayerPersonId`다.
- 다른 Source Player ID는 다른 `PlayerPersonId`다.
- 한 `PlayerSeasonId`는 한 Source PlayerSeason에만 대응한다.
- `CareerSpan`은 동일 Source Person의 실제 시즌 범위다.
- Source 누락을 다른 선수 시즌 연결로 보충하지 않는다.

Offline Validation Report에는 Runtime Stable ID에서 원본으로 추적 가능한 1:1 provenance를 기록하되,
그 원본 식별 정보 파일은 Player Build에 넣지 않는다.

## Source TeamSeason 연결과 Core25

- 한 Source TeamSeason은 한 Canonical `TeamSeasonDefinition`이다.
- 후보는 그 Source TeamSeason에 실제 등록된 Source PlayerSeason만 사용한다.
- Source에 존재한 연도별 구단 수를 보존한다. 모든 선수를 고정 10개 가상 팀에 재분배하지 않는다.
- Core25는 해당 팀 내부에서 Hitter14/Pitcher11, SP5/Bullpen4/Setup1/Closer1, Foreign≤3을 맞춘다.
- 데이터 부족은 연도·팀·Role별 오류로 보고한다. 다른 팀 선수나 covariance Replacement로 조용히
  보충하지 않는다.

`ReferenceStrength`와 초기 Club DNA가 필요하면 해당 Canonical TeamSeason의 Baked 선수 구성에서
결정론적으로 파생한다.

## Ability, Cost, TrainingCeiling

Source PlayerSeason 자신의 기록을 시대·포지션·역할 집단으로 Normalization하고 표본 Reliability를
적용한 뒤 결정론적으로 Rating을 변환한다. 같은 Source Data/Normalization/Balance Version이면 항상
같은 Baked 값이 나와야 한다.

World Seed, World DisplayName, Historical 성적, Standings, Award는 `BaseAttributes`, `Cost`,
`TrainingCeiling`, Origin을 변경하지 않는다. 특수 Edition도 같은 Cost를 공유한다.

## Name Catalog와 blacklist

Normalized Archive가 보유한 실제 선수명과 구단명 전체를 exact-match blacklist로 사용한다.

- Domestic 이름은 성씨 빈도와 자연스러운 이름 음절/세대 경향을 반영할 수 있는 충분한 공간을 둔다.
- Foreign은 신뢰 가능한 RegistrationType 범위 안에서 별도 후보를 사용한다.
- 구단명은 자연스러운 지역+Nickname/Brand 형태를 사용한다.
- 실제 이름 exact match, World 내부 중복, null/empty, 숫자, 제어문자, 과도한 길이, 금칙어를 거부한다.
- 작은 이름 목록의 반복 순환이나 닉네임식 문자열 조합을 사용하지 않는다.

최종 생성 이름은 Source Archive에 쓰지 않고 World State에 저장한다.

## Historical World 결과

Source 시즌 개인 기록, 실제 팀 승패·순위·우승, 실제 수상은 정식 Runtime History로 복사하지 않는다.
Canonical TeamSeason을 `DetailedMatchSimulator`와 동일한 판정 모델에 투입해 개인·팀 Statistics,
Standings, Postseason, Awards를 새로 만든다. `OriginalHistory` 데이터가 남아 있으면 Offline 비교와
Legacy 회귀 검증 전용이다.

## 도구 실행 개요

정확한 CLI는 `Tools/KBOImporter/README.md`를 따른다. 산출 단계는 다음 책임으로 분리한다.

1. Extract/Normalize
2. Stable 1:1 Player/Team Mapping
3. Rating/Cost/TrainingCeiling Bake
4. Source Team 내부 Core25
5. Runtime-safe Archive + Name Catalog
6. Validation Report/Manifest/Hash

기존 도구 파일명에 `synthetic_bake.py`가 남아 있더라도 이름은 호환용 Legacy일 수 있다. Production
출력은 반드시 위 1:1 계약을 따라야 하며 다중 Reference Mixing 경로는 호출하지 않는다.

## Validation Gate

- Source Person/PlayerSeason/TeamSeason 1:1과 Stable ID 결정론
- 타 Person 개별 기록 및 타 Team 선수 Mixing 0건
- World Seed 변화에도 Canonical hash 동일
- Source 실제 이름의 Runtime Archive 노출/exact-match generated name 0건
- Core25 구성·소속·Role/Position/Foreign 규칙
- Source 통계와 Rating 간 의도된 상관
- 다른 Seed Historical Simulation의 개인·팀 분포 및 결정론
- Source Statistics/Standings/Award 복사 Production 경로 0건

이 Gate를 통과하지 않은 Archive는 `HistoricalRuntimeContentCatalog`에 등록하지 않는다. 과거 3~7
Reference 혼합, 가상 10구단 재배분, 고정 `fictionalName` Archive의 Hash와 Count는 현행 검증 근거가
아니다.
