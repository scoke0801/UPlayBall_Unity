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

현재 산출 코드는 **Ability v9 / Cost v14 / DerivationBalance v19**다. 아래 v8/v13 설명은
회귀 보정의 기준식으로 남는다. 기준식 뒤에 `record_calibration.py`의 공통 기록 회귀를 적용하며
학습 계수는 `derivation_balance.json`의 `referenceRecordModels`에 저장한다. 원본 캐시 성적은
수정하지 않는다. 선수명·팀·연도별 카드 수치를 직접 입력하거나 Runtime에서 연구 DB를 읽지 않는다.

레퍼런스는 **2013년 이하 일반 연도 카드**만 사용하고 월별·특수·판본 불명·초과 연도는 제외한다.
특징·범위·정규화 후보·Cost 희귀도 가중·단조 제약은 `reference_calibration_policy.json`에 있다.
`calibrate_annual_reference.py --baseline <v18 Archive> --baseline-balance <v18 JSON> --output <폴더>`로
후보를 만들며 학습 도구에만 NumPy가 필요하다. 같은 인물의 여러 시즌을 하나의 분할에 묶는다.
`verify_annual_calibration.py`로 후보의 실제 베이크와 예측을 대조한다. 기준식이나 특징을 수정하면
계수도 다시 학습·검증한다. 구속 결측 55와 상위 Cost 자격 상한은 유지한다.

새 설정으로 생성한 17,333개 Source 후보는 `.tmp/pm-annual-calibration/candidate`에 보관했다.
공식 Archive와 WorldHistory는 이번 보정 작업에서 교체하지 않았다. 상세 오차, 28,000경기 비교,
Cost1·10의 분포 한계, 기존 테스트/시즌 진단 빌드 실패는
`Research/PyaMaeCardDb/Calibration/README.md`에 기록했다.

현행 평가 코드는 **Ability v8 / Cost v13 / Balance v16 / Roster v6**이며 아래 설계 이력보다
`BaseballManager_PROJECT.md` 42.14절과 `docs/reports/pm_pitcher_position_review_20260906/결과.md`가 우선한다.
공식 데이터는 사용자가 베이크한 v12/v15/v5 상태이며 새 코드의 반영에는 사용자 재베이크가 필요하다.
v13은 `.tmp/pm-sk-calibration/final`에서 전체 검증과 10,000경기를 통과했다.
공식 v12 적용 검증 이력은 `docs/reports/pm_live_review_20260906/검토.md`에 보존한다.
체력은 역대 단일 시즌 이닝 기준, 구속은 실측 170km/h=100, 제구는 BB/9,
구위·변화구·투수 멘탈은 ERA를 쓴다. 교타=타율, 장타=홈런·장타율,
주력=도루 시도율 70%·성공률 30%, 타자 정신=출루율 70%·타율 30%,
수비=수비율·실책이며 결측 지표는 Ability와 Cost의 평가 분모에서 제외한다.
평가 범위와 TrainingCeiling 상한은 100이다. Cost는 성적·출전량·역할과 상위 Cost 자격을 분리한다.
실측 구속이 없는 Source의 구속 55는 중립값이며 실제 구속 추정치가 아니다.
프로필 기준점은 근거가 있는 능력치에만 적용하며 전부 결측이면 여전히 55다.
Cost 기본점은 야수 2.75·선발 역할군 1.75·구원 역할군 1.25이며 JSON으로 조정한다.
타자 출전량 가중치는 4이며 유형별 Cost 경계·상위 자격은 `costValueModel`에서 조정한다.
선발은 `pitcherRoleValueProfiles.Rotation`에서 ERA 90%·BB/9 10%, 출전량 배율 0.7과
양의 성적×출전량 가산 2.0을 사용한다. 충분히 던진 중간 성적 투수와 에이스의 가격을 분리하고,
Cost 8/9 경계 및 9/10 자격도 같은 역할 프로필에서 조정한다. 구원 공식은 유지하되 전체 투수의
역할 백분위가 바뀌므로 일부 구원 Cost도 달라질 수 있다.
주전은 PA·포지션 수비 이닝을 제한적으로 반영하며 Cost를 이용한 사후 교체는 하지 않는다.
원본 수비 기록이 없을 때만 `season_position_evidence.json`의 시즌+Source ID+구단에 해당하는
웹 포지션 근거를 사용한다. 실제 수비 기록을 우선하며 경기·이닝·능력치를 만들어 넣지 않는다.
Cost의 원기록 근거는 `costMetricEvidence`로 독립 저장하여 능력치 프로필 변경에 가격이 간접 종속되지
않게 한다. 이 근거는 Editor 전용이며 Runtime 변환 시 제거한다.
수비 가격 기여도 `defensiveQualityProfiles`의 원기록 Z에서 계산한다. 수비 표시 기준점·변환 폭을
보정해도 가격 가산이 함께 오르지 않는다. Fielding 계수 0.525와 Arm 계수 0.125는 이전 가격 신호의
14×0.75/20, 10×0.25/20에서 나온 무차원 가중치이며, 각 그룹의 결측 지표는 재정규화한다.

인벤 86864/86866 원문의 판본·기본/목표 등급은 `extract_pm_thresholds.py`, 카드 막대 판독은
`read_pm_card_bars.py`, 등급 역산 한계 검사는 `analyze_pm_thresholds.py`, 선수 단위 분할과
실제 Bake 비교는 `study_pm_expanded.py`를 사용한다. 이미지 판독 도구에만 Pillow가 추가로 필요하며
`uv run --with pillow==12.3.0`으로 실행했다. 연결된 현행 이미지와 2010년 기사의 게시 시점을 구분한다.
판독한 452장 중 유일 Source 연결 438장으로 공통 기준점·변환 폭을 보정했으며 실제 선수 이름과
원작 등급 표는 Production 입력으로 사용하지 않는다.

참조 후보 탐색은 `study_pm_calibration.py`와 `study_pm_abilities.py`, 실제 Bake 전후 비교는
`report_pm_calibration.py`로 재실행한다. 기준 Bake와 Balance 파일 경로를 명시해야 하며,
로컬 `Tools/PMReference/reports` 코퍼스와 `docs/reports/pm_reference_review_20260906/workbook_extracted.json`이
필요하다. 원본 시점·카드 판본이 불명확한 관측은 최종 Normal의 정답으로 간주하지 않는다.

SK 2007~2009 선발 15명의 원문 이미지 판독값은 새 보고서의 `sk_reference.json`에 보존한다.
`study_pm_pitcher_cost.py`는 v12 기준 Archive와 `--balance balance_before.json`,
`--focus-reference sk_reference.json`을 명시하는 연구 도구다. 최신 Archive에 반복 적용하는 자동
보정기가 아니다. SK 표본은 후보 선택에 사용했으며, 독립 평가에서는 같은 인물을 모두 제외한다.
이번 SK 평균 오차는 1.000→0.333이지만 별도 2013 투수 62건에서는 1.032→1.274로 악화됐다.
관측 시점의 차이를 보존하며 모든 서비스 판본을 복원했다고 해석하지 않는다.

아래는 기존 v5/v7 설계 이력이다.

Source 기록 → 지표별 비교 집단 → 표본 신뢰도 보정 → BaseAttributes → 역할별 종합 능력치 → 고정 Cost
구간 순서로 계산한다. World Seed·World 성적·Award·이름·Edition은 이 값을 바꾸지 않는다.
설정은 `derivation_balance.json`, Source/Replacement 공용 가격 함수는 `derivation_cost.py`에 둔다.

- 타격 효율은 같은 연도 전체 타자, 투구 효율은 같은 연도 전체 투수와 비교한다. 수비는 실제 수비
  포지션, Stamina는 선발/구원 역할군을 쓴다. 포지션별 공격 상대평가로 타격 우수자가 저평가되는
  현상을 줄인다. 수비 소집단은 기존 포지션군·유형 전체 통계와 연속 혼합한다.
- 신뢰도는 `r = n / (n + k × 당시 시즌 경기 수 / 144)`다. 시즌 경기 수는 당시 구단 순위표를
  우선하며 불가피한 대체 근거도 Trace에 남긴다. 짧은 옛 시즌에 현대 시즌의 출전 기준을 강제하지 않는다.
- 성과 지표는 `adjustedZ = clamp(rawZ, -3, 3) × r + (-1) × (1-r)`로 보정한다.
  극소표본을 평균 주전으로 자동 복원하지 않기 위한 보수적 사전값이다. 수비·송구 근거 부족은
  못한다는 증거가 아니므로 해당 지표의 사전 Z는 0이다.
- Rating 기준점은 55, 범위는 25~95다. 주요 타격·투구 성과의 변환 폭과 역할 가중치를 조정하고,
  Power에 ISO, Breaking에 피홈런 억제율을 사용한다. 이는 실측 구속·구종 정보를 대체하는
  게임용 지표이며 선수의 실제 구속이나 구종 자체를 추정했다고 해석하지 않는다.
- 100타석 이상이며 실제 수비 기록이 있으나 경기당 수비 이닝이 4.5 미만인 타자는 시즌 주역할을
  DH로 추론한다. 실제 주수비 포지션은 파생 Trace에 남겨 수비 능력치 계산에 사용한다.
  공식 DH 출전 기록의 복원이 아니라 기용 형태에 대한 명시적 추론이다.
- Cost는 역할별 능력치 가중평균에 고정 경계를 적용한다. 같은 종합 능력치는 같은 가격이며,
  백분위와 Full/Regular/Limited/Tiny는 진단일 뿐 출전량으로 Cost를 추가 할인하지 않는다.
  Cost별 선수 수를 일정 비율로 강제하지 않는다.

| Cost | 종합 능력치 구간 |
|---|---|
| 1 | 32 미만 |
| 2 | 32 이상 36 미만 |
| 3 | 36 이상 40 미만 |
| 4 | 40 이상 44 미만 |
| 5 | 44 이상 48 미만 |
| 6 | 48 이상 52 미만 |
| 7 | 52 이상 56 미만 |
| 8 | 56 이상 60 미만 |
| 9 | 60 이상 64 미만 |
| 10 | 64 이상 |

TrainingCeiling은 최종 Runtime Bake에서 BaseAttributes에 동일한 +3을 부여한다(최대 99).
저Cost에만 +4~8을 주던 추가 성장 역전 요인을 제거한다. 역할별 장단점은 유지하므로 고Cost 선수가
모든 개별 능력치에서 우월함을 보장하지는 않으며, 인접 Cost 경계의 최소 능력차도 보장하지 않는다.

현재 버전은 Ability v8 / Cost v13 / PositionRole v6 / DerivationBalance v18 / RosterBuilder v8다.
사전 Z, Rating 폭, 역할 가중치와 가격 경계는 밸런스 초안이다. 데이터 일관성 확인과 실제 경기
밸런스 검증은 별개이며, 대량 경기·경제·훈련 검증 없이 승률이나 리그 평균 개선을 확정하지 않는다.

### 대표 로스터 배치

`ability-fit-core25-v8`은 수비 8자리와 DH의 합산 점수를 동시에 최대화한다. 주포지션의 과도한
고정 가산점을 제거하고 적격 부포지션에만 -4를 적용한다. 부포지션 자격은 실제 주수비 위치 또는
5경기/45아웃 이상의 반복 기용 근거다. 적격 배치 불가능 시에만 OffPosition 경고를 남긴다.

벤치는 백업 포수 확보를 우선하며, 나머지는 교체 능력치와 새 백업 수비 범위(포지션당 +6)로
순차 선택한다. 주전 9명은 공동 최적화지만, 벤치 포함 14명 전체의 전역 최적화를 의미하지 않는다.

선발 5명은 Natural Starter 중 선발용 능력치 순서다. 선발 결손은 실제 GS 양수, GS 결측,
확인된 GS=0 순으로 보충한다. GS 결측에 추정 Natural Role을 다시 확정 근거로 가산하지 않는다.
전문 셋업·마무리를 확보한 뒤 남은 투수는 Natural Role에 관계없이 일반 불펜에서 경쟁한다.
역할 결손 fallback은 별도 경고로 남긴다. Cost는 배치 점수의 입력이 아니다. 포지션 근거가
바뀌면 역할별 Cost 비교 모집단은 바뀔 수 있지만 선수·구단별 가격 예외는 사용하지 않는다.
원본 후보 점수와 벤치 선택 근거를 Editor Trace에서 확인할 수 있다.

### 수비 자료 결측과 선발 순환

`season_position_evidence.json`은 236개의 시즌·Source ID·구단·포지션 근거를 보존한다.
KBO 공식 수상 부문과 구단 역사(A), 해당 시즌 선수단 목록(B)을 구분하며 실제 수비 기록이
있으면 원기록을 우선한다. 이 자료로 출전 경기, 수비 이닝이나 능력치를 만들지 않는다.
수집 중 충돌·제외된 후보는 `unresolved`에 남긴다. 추가 출처로 보완된 후보도 있으므로
이 로그 개수와 현재 결측 선수 수는 다르다.

원기록과 보조 출처가 모두 없으면 `isPositionEvidenceMissing`을 저장한다. 실제 DH 전담과
구분하며, 주전 공동 최적화에서 `unknownPositionPenalty=8`의 비용을 적용해 비교한다.
경기에서는 위치 적응도·비주포지션 추가 패널티만 중립으로 처리하고 수비·송구 능력은 유지한다.
알려진 수비 부적격 선수의 기존 패널티와 부포지션 자격은 유지한다. 보조 위치의 주전 출전량
사전 가중치는 `starterUsage.supplementalPositionWeight=6`이며 수비 이닝 추정값이 아니다.

선발 등판은 팀별 실제 경기마다 **1→2→3→4→5→1** 순서다. 휴식일·올스타는 순번을 소비하지
않고, 과거 GS나 투구 이닝으로 등판 횟수를 가중 배분하지 않는다. 원기록의 GS는 선발 후보를
선택하는 근거로만 사용한다. 경기마다 소화하는 이닝은 체력·실점·투구 수·교체 판단에 따라 달라진다.
검토 중 추가했던 `historicalRotation` 데이터와 가중 스케줄러는 제거했다.

표시 능력치를 경기 입력으로 바꾸는 곡선은 Importer 밖의
`Assets/10.Datas/Resources/NewGame/MatchRatingCurve.json`이 소유한다. 현재 center 45 / slope .45다.
공식 데이터와 WorldHistory 반영은 검증 후 재베이크해야 한다. 실제 경기 경로의 재현 도구와
최신 비교는 `Tools/HistoricalSeasonDiagnostics/README.md`,
`docs/reports/team_strength_improvement_20260906/결과.md`를 참조한다.

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

현재 프로젝트의 Catalog 참조 대상은 `Assets/10.Datas/HistoricalSimulation/1982-2025/`다.
Bake 후 검증된 `Runtime/`의 manifest·player_persons·Years JSON을 이 경로에도 동기화해야 실제
게임에 반영된다. 기존 `.meta`와 Catalog GUID는 보존한다. 정본 ContentHash가 바뀌면 과거 Hash로
Bake한 WorldHistory를 새 콘텐츠의 경기 결과로 재사용하지 않는다.

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
