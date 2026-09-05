# KBO Reference Data Extractor / Canonical Baker

KBO 공개 기록을 Offline에서 수집·정규화하고 Source와 1:1인 Runtime-safe Canonical Content로 Bake한다.
실제 선수·구단 이름은 Editor 검수와 blacklist에만 사용한다. Runtime Game Flow는 이 도구를 호출하지
않으며 Baked Archive만 읽는다.

```text
Raw Snapshot
→ Normalized Source JSON
→ Source Person/PlayerSeason/TeamSeason 1:1 Mapping
→ Ability/Cost/TrainingCeiling/Origin/Core25 Bake
→ Runtime-safe Canonical Archive + World Name Catalog
```

도구 파일명 `synthetic_bake.py`는 기존 호출 호환을 위해 남을 수 있지만, Production Bake의 의미는
Synthetic Player Mixing이 아니다. 여러 Source 선수의 Feature Vector나 여러 Source 구단의
Fingerprint를 섞는 경로는 사용하지 않는다.

## 수집 실행

프로젝트 루트에서 실행한다. `uv`가 Python과 고정 의존성을 준비한다.

```powershell
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --from-year 2025 --to-year 2025
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --from-year 2026 --to-year 2026 --include-current
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --from-year 2011 --to-year 2011 --category hitter
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --validate-only
```

기본 범위는 완료된 1982~2025 정규시즌이다. `--category`는 `hitter`, `pitcher`, `defense`,
`runner`, `team`, `awards`를 지원하며 여러 번 지정할 수 있다. 일부 Category 결과는 완전한 시즌
파일을 덮어쓰지 않고 `Normalized/Partial/`에 저장한다.

## 요청 정책

- 동시성 1, 요청 간 1~2초 지연
- 429/5xx/네트워크 오류에 2초, 5초, 10초 Backoff로 최대 3회 Retry
- Raw Cache가 있으면 요청 생략
- 공식 Selector가 요청 연도를 제공하지 않으면 `Unavailable` Marker 저장
- 미완성 WebForms Snapshot 재개 시 Endpoint별 Bootstrap GET 1회
- 명시적인 `--force`/`--force-aggregate`에서만 좁은 범위 재다운로드
- 로그인, CAPTCHA, 접근 제어, 비공개 API 사용 금지

```powershell
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py `
  --from-year 2024 --to-year 2024 --category pitcher --force-aggregate
```

KBO 기록실은 ASP.NET WebForms PostBack을 사용한다. 공개 페이지의 `__VIEWSTATE`,
`__EVENTVALIDATION`, Select name과 Pager target을 그대로 제출한다. Header/Season/Team Aggregate가
기대와 다르면 `SchemaMismatchError`로 중단하고 기존 JSON을 유지한다.

## 출력과 Cache

```text
Tools/KBOImporter/.cache/KBOImport/
  Raw/
  Normalized/
    1982.json
    Partial/
  Reports/
    KBO_IMPORT_REPORT.md
    KBO_IMPORT_REPORT.json
```

Raw HTML과 Normalized JSON은 Local Cache이므로 Commit하지 않는다. Unity `Temp/`는 장기 Cache로
사용하지 않는다. 필요하면 `--data-root`를 지정한다.

Raw 하나는 `seasonYear + category + page + teamId` Snapshot이므로 전체 Archive를 대체할 수 없다.
동일 선수도 시즌별 기록이 다르며 Hitter/Pitcher/Defense/Runner Page를 공식 PlayerId로 병합해야 한다.

## 정규화 계약

- 선수는 링크의 공식 `playerId`로 병합한다.
- 공식 ID가 없는 Row는 이름을 ID로 승격하지 않고 `UnresolvedComposite`로 격리한다.
- Aggregate Season Total, `teamFilterRecords[]`, `teamStints[]`, `tradeMovements[]`를 분리한다.
- 트레이드 분할 수치가 확인되지 않으면 추정하지 않고 `null`, `dataScope=Unavailable`로 둔다.
- 시즌 종료 뒤 이동은 `tradeMovements[]`에만 기록한다.
- `IP`는 `inningsOuts` 정수로 변환한다.
- `-`/빈 값은 `null`, 실제 0은 숫자 0으로 보존한다.
- 타자와 투수 기록은 상호 배타적으로 가정하지 않는다.
- 수비는 포지션별 `defenseRecords[]`로 보존한다.
- 복수 League 순위표는 모두 결합하고 `standingsGroup`을 보존한다.
- 수상 Row는 `Year + PlayerName + Team` 후보가 정확히 하나일 때만 Source Season에 연결한다.
- `awardAvailabilityStatus`와 `dataAvailabilityStatus`는 Unavailable과 AvailableEmpty를 구분한다.
- Team Code를 얻지 못하면 `unresolved-*`로 격리하고 Origin을 기록한다.

Source 실제 Statistics/Standings/Award는 Offline 검증 자료다. 정식 Runtime World 기록이나 수상으로
복사하지 않는다.

## Canonical Bake

```powershell
uv run python synthetic_bake.py `
  --input-dir .cache/KBOImport/Normalized `
  --years 1982-2025 `
  --seed 20260901 `
  --editor-assets-dir "../../Assets/Editor Default Resources/HistoricalSimulation/1982-2025" `
  --verify-editor-assets
```

`GenerationSeed`는 Archive 정렬이나 검증 호환에 남을 수 있지만 Source 1:1 Ability를 무작위로
변형하지 않는다. 같은 Source Data/Normalization/Balance Version은 Seed와 World에 무관하게 같은
Canonical 값을 만든다.

### Player 1:1

- Source Player 한 명 → Stable `PlayerPersonId` 하나
- Source PlayerSeason 한 건 → Stable `PlayerSeasonId` 한 건
- 같은 Source Person의 모든 시즌 → 같은 `PlayerPersonId`
- BaseAttributes 직접 입력 → 그 PlayerSeason 자신의 기록만
- 시대·Position/Role aggregate와 Z-Score → normalization/reliability 기준만
- Cost/TrainingCeiling/Origin → Offline 고정

3~7 Reference Mixing, `SyntheticFeatureVector`, covariance sampling, 능력치별 Source 교체,
Similarity Reject 재생성은 Production Player Bake에서 금지한다.

### TeamSeason 1:1

- Source TeamSeason 한 건 → Canonical `TeamSeasonDefinition` 한 건
- 같은 Source Franchise의 연도별 TeamSeason → 같은 Stable `FranchiseId`
- Core25 후보 → 해당 Source TeamSeason에 실제 등록된 PlayerSeason만
- Source 연도별 실제 구단 수 보존
- Hitter14/Pitcher11, SP5/Bullpen4/Setup1/Closer1, Foreign≤3

같은 연도 전체 선수를 고정 10개 가상 Franchise에 Hash/round-robin 배분하지 않는다. 부족 인원을
다른 Source Team이나 covariance Replacement로 채우지 않는다. 누락 데이터/Role 부족은 연도·구단별
Validation Error로 보고한다.

### Ability와 Cost

Source Season을 Era/Position/Role Normalization하고 Reliability Shrinkage한 뒤 결정론적으로 Rating을
변환한다. World Seed, World 성적, Award, 표시 이름, Edition은 BaseAttributes/Cost/TrainingCeiling을
바꾸지 않는다.

파이프라인은 네 판단을 각각 다른 단계에서 답한다. 하나를 Composite 한 값에 몰아넣지 않는다.

```text
Raw Season Stats
      ↓ ReferencePopulationBuilder   비교 기준을 어떻게 세울 것인가
Metric Z
      ↓ Reliability Shrinkage        이 기록을 얼마나 믿을 것인가 (단 한 번)
BaseAttributes
      ↓ RoleComposite                역할 기준으로 얼마나 잘했는가
      ↓ CostEligibilityResolver      얼마나 큰 시즌이었는가
Hitter / Pitcher 별 Percentile
      ↓
Cost 1~10
```

**Reference Population.** 비교 집단의 평균·표준편차는 표본 신뢰도 `n / (n + k)`를 Reference Weight로
쓴 Winsorized 가중 통계다. 소표본 선수를 모집단에서 제거하지 않는다 — 제거하면 희소 포지션과
1980년대처럼 얇은 연도의 기준이 무너진다. 대신 기여도만 줄인다. 한 값이 자기 자신에 대해 갖는
증거력과 모집단에 대해 갖는 증거력을 같은 척도로 두기 위해 개인 Shrinkage와 같은 곡선을 쓴다.
유효 표본이 `minimumEffectiveSampleCount`에 못 미치면 포지션군(`positionFamilies`) → 같은 연도
같은 선수 유형 순으로 연속 혼합한다. **RoleTier는 GroupKey에 넣지 않는다.** 넣으면 규정 미달
선수들끼리 비교하게 되어 소표본만으로 이루어진 비교 기준이 만들어진다.

**Cost Eligibility.** 능력치와 별개로 출전량이 Cost 상한을 정한다. 28이닝 투수의 볼넷 억제력은
능력치로는 사실대로 남기되, 그 시즌이 500타석 주전과 같은 카드 희소도를 갖지는 않는다.
타자는 타석 수, 투수는 상대한 타자 수를 쓰고, 온전한 시즌 기준(`Full`)은 역할군별로 다르다.

```text
상한 = round(minimumCost + (maximumCost - minimumCost) x clamp(표본 / 역할군 Full기준, 0, 1))
```

| 기준 | 타자(타석) | 선발(상대타자) | 구원(상대타자) |
|---|---|---|---|
| Full — 온전한 시즌, 곡선의 분모 | 400 | 560 | 190 |
| Regular — 진단 이름 경계 | 250 | 350 | 110 |
| Limited — 진단 이름 경계 | 100 | 110 | 40 |

상한 수치는 레퍼런스인 엔트리브 프로야구 매니저의 Cost 구간 의미를 기준으로 잡았다. 그 게임에서
1~2 Cost는 신고선수·부상선수처럼 출장이 극히 적은 카드, 3~4는 표본이 작지만 효율이 좋은 카드,
5~6은 가장 두꺼운 선수 풀, 7~9는 팀 핵심, 10은 그 해를 지배한 선수다.

**상한은 구간이 아니라 연속 함수다.** Full/Regular/Limited/Tiny는 사람이 읽는 진단 이름으로만
남기고 상한을 정하지 않는다. 구간마다 고정 상한을 주면 문턱 하나를 사이에 두고 상한이 여러 계단
갈라져서, 83이닝을 던진 부분 선발이 56이닝 마무리보다 낮은 상한을 받는 역전이 생긴다. 표본이
몇 타자 모자라 한 계단 깎이는 경계 인공물도 없어진다.

백분위는 상한에 관여하지 않는다. "얼마나 잘했는가"는 Composite 백분위가, "얼마나 큰 시즌인가"는
출전 비율이 각각 답하고, 최종 Cost는 둘 중 낮은 쪽이다.

투수 기준을 역할군별로 나눈 이유는 마무리 한 시즌이 선발보다 상대 타자가 훨씬 적기 때문이다.
50경기 56이닝을 던진 마무리는 온전한 시즌이지 부분 출장이 아니다. 단일 기준을 쓰면 이런 시즌이
전부 부분 출장으로 떨어진다. 타자 Full 기준 400타석과 선발 Full 기준 560(약 140이닝)은 KBO
규정타석·규정이닝 근처다.

**Cost 모집단.** 백분위 구간(`costPercentileThresholds`)은 그대로 두고 모집단만 OriginYear의 같은
선수 유형으로 나눈다. 타자와 투수는 입력 지표도 Composite 분산 구조도 달라서, 합치면 분산이 큰
쪽이 상위 Cost를 독점한다.

## Editor Audit와 Runtime Archive

공식 위치:

```text
Assets/Editor Default Resources/HistoricalSimulation/1982-2025/
  manifest.json
  player_persons.json
  Years/{year}.json
  Runtime/
    manifest.json
    player_persons.json
    Years/{year}.json
```

루트 Archive는 Editor 원본 검수용이며 실제 Source ID/Name과 provenance를 포함할 수 있다. Player
Build에 포함하지 않는다. `Runtime/`은 Runtime-safe Stable ID와 Canonical 값만 포함한다.

Runtime `player_persons.json`에는 Person별 고정 `fictionalName`을 저장하지 않는다. 대신 실제 Source
이름과 exact match하지 않는 충분한 Domestic/Foreign Player Name 및 Franchise Name 후보 Catalog를
Canonical 선수 데이터와 분리해 제공한다. 최종 이름은 World 생성 시 `WorldIdentityRegistry`에
확정하고 Save한다.

실제 선수명/구단명, `sourceReferenceNames`, Editor provenance는 Runtime Archive에 넣지 않는다.
`HistoricalRuntimeContentCatalog`는 검증된 Runtime 하위 Archive만 읽는다.

## Manifest와 검증

Manifest는 Source/Normalization/Balance/Generator Version, Content/Archive Hash, 파일별 SHA-256,
Count와 Validation Report를 기록한다. `--verify-editor-assets`는 저장 직후 모든 파일을 다시 읽어
다음을 검증한다.

- Source Person/PlayerSeason/TeamSeason과 Canonical Definition 1:1
- Stable ID 결정론과 동일 Person/Franchise 다년도 연결
- 타 Person 기록 및 타 Team 선수 Mixing 0건
- Core25 소속, Roster quota, Foreign 제한
- 실제 Source 이름 Runtime 노출 0건과 Name Catalog blacklist/quality/공간
- World Seed와 무관한 BaseAttributes/Cost/TrainingCeiling/Origin
- Source Statistics/Standings/Award의 정식 World 결과 복사 경로 0건

Player Build용 단일 JSON이 필요할 때만 `--output`을 사용한다. Archive Gate가 실패하면 기존 검증본을
교체하거나 Catalog에 등록하지 않는다.

## 테스트

```powershell
uv run python -m unittest -v test_synthetic_bake.py
```

테스트 파일명의 `synthetic`도 호환 이름일 수 있다. Assertion은 Source 1:1과 Runtime-safe 경계를
검증해야 하며 과거 Synthetic 분포 유사성이나 covariance Replacement 생성을 정답으로 삼지 않는다.

새 Canonical Archive를 실제로 재생성·검증하기 전에는 기존 3~7 Mixing/고정 10팀 배분/
Replacement/fixed `fictionalName` Archive의 Hash, Count, 파일 크기, Simulation 수치를 최신 완료 근거로
인용하지 않는다. 최신 Validation Snapshot은 Report에서 생성하고 실행하지 않은 수치를 추정해
문서에 쓰지 않는다.
