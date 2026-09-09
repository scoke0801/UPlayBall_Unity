# 구속 결측 보완

실측 구속이 없는 모든 투수를 55로 평가하던 경로를 일반 카드 표시 구속의 기록 기반 추정으로 대체했다. 원본 `fastballVelocityKph`의 빈칸을 가짜 실측값으로 채우지 않는다. **실측 구속 자료 자체가 모두 확보된 것은 아니며, 이번 변경은 게임 표시 구속의 결측 평가를 개선한다.**

## 적용 규칙

- 실측 FastballVelocityKph가 있으면 기존 절대 구속 변환을 사용하며 추정을 건너뛴다.
- 미실측 투수는 K/9, BB/9, ERA, 경기당 이닝, 세이브·홀드 비율, 시즌 이닝·등판 수로 일반 카드 표시 구속을 추정한다. 기록이 전혀 없으면 학습 사전값 약 59.64를 사용한다.
- 확보한 일반 카드 표시 구속 3,159건은 기존 Reference Override가 우선한다. Research 보충 카드의 관측 구속도 유지한다.
- EX·레전드·올스타를 추정 학습 정답으로 섞지 않는다. 선수명·구단·선수 ID·Cost는 회귀 입력이 아니다.
- Editor Ability Trace에 `EstimatedNormalCardVelocity` 또는 `EstimatedVelocityPrior`, 모델 버전, 사용·대체한 기록 목록과 `measuredVelocity=false`를 저장한다.

고정 계수·범위·학습 검증 결과는 [velocity_estimation.json](velocity_estimation.json), 실행기는 [velocity_estimation.py](velocity_estimation.py)에 있다. `derivation_balance.json`이 사용 여부와 파일 SHA-256을 고정한다. 모델 출력은 학습 입력 범위와 관측 표시값 범위로 제한하고 최종 공통 Rating 범위도 적용한다.

평가식은 `historical-ability-v11`, 밸런스는 `historical-derivation-balance-v24`다. 기존 Cost 식 `historical-season-value-v17`은 유지한다.

## 선수·시대 분리 검증

일반 카드 구속 3,159건을 선수 단위로 Train 2,000 / Validation 536 / Holdout 623으로 분리했다. Validation으로 Ridge 계수를 선택한 뒤 Train+Validation만 재적합했으며, Holdout 선수는 배포 모델 학습에서도 제외했다. 별도의 시대 검증은 2009년까지 학습하고 2010~2013년 543건을 평가한다. 이 시대 검증은 선수 분리 검증과 다른 실험으로, 시대 간 동일 선수의 재등장을 허용한다.

| 평균 절대 오차 | 기존 55 | 추정 모델 |
|---|---:|---:|
| 미학습 선수 623건 | 8.2022 | 7.2215 |
| 이후 시대 543건 | 11.1510 | 8.2891 |

Holdout 평균 부호 오차는 -4.0482 → +0.3965다. 다만 ±5 이내 비율은 45.59% → 42.05%로 낮아졌다. 평균 오차와 과소평가 편향이 개선된 모델이지 모든 선수의 오차가 줄어든 모델은 아니다. 실제로 빠른 공·느린 공을 던지는 개별 투수의 구속을 확정하려면 실측 또는 해당 일반 카드 자료가 여전히 필요하다.

외부 학습 라이브러리 없이 작은 정규방정식을 푸는 결정론적 Ridge 모델이다. 두 번 학습한 배포 JSON의 SHA-256이 일치했다. 모델 학습 도구는 `fit_velocity_estimation.py`이며 원본 캐시·참조 파일·학습 코드의 해시를 보고서에 기록한다.

## 전체 콘텐츠 영향

44개 연도와 18,326개 선수 시즌을 재베이크했다. 기존 Source ID·Runtime ID·선수 구성·원기록·Cost와 구속 이외 기본 능력치는 보존한다.

- 구속 변경 4,318장: 원본 기록 선수 4,265장, 구속 분포를 사용하는 생성 대체 선수 53장.
- 전체 투수의 표시값 55는 4,505 → 187건. 남은 55는 관측값 또는 계산 결과가 55인 경우이며, 모두 결측 기본값이라는 뜻이 아니다.
- 2014 밴헤켄 기본 구속은 55 → 64. 이는 추정 일반 카드 표시값이며 첨부 EX의 66을 강제로 복사한 것이 아니다.
- 구속을 반영하는 자동 평가에 따라 94개 구단 시즌의 Core25 구성, 246개 로스터 역할, 68개 구종 구성 자료와 변경 선수의 훈련 상한이 파생 갱신됐다.

전체 재베이크 과정에서 밸런스 버전이 신원 생성 Seed에도 사용되어 생년·손잡이·잠재 특성을 다시 추첨하는 기존 문제가 발견됐다. `runtimePersonMetadata.seedSalt`를 독립 설정으로 분리하면서 기존 v23 Salt를 유지해 **3,565명의 신원과 이름풀을 그대로 보존**한다. 평가식 버전만 바꿔도 신원이 달라지지 않는 회귀 테스트를 추가했다.

## 경기 검증

1982·1988·2000·2014·2025년을 각각 4시즌, 같은 Seed 집합으로 실행했다. 전후 각각 정규시즌 **11,520경기**, 전체 11,713 / 11,720경기다. 전체 경기 수 차이는 포스트시즌 진행 차이다. 각 연도의 첫 시즌을 재실행한 **결정론 검사 5개씩**을 통과했다.

| 정규시즌 지표 | 변경 전 | 변경 후 |
|---|---:|---:|
| 타율 | .27742 | .27639 |
| ERA | 3.49352 | 3.46947 |
| 양 팀 합계 득점 / 경기 | 7.20495 | 7.16059 |
| 홈런 / 경기 | 1.37995 | 1.38342 |
| 볼넷 / 삼진 | .48279 | .47750 |
| 실제 팀 승률과의 연도 내 상관 | .69932 | .77962 |
| 실제 팀 승률과의 MAE | .06240 | .06193 |

위 결과는 `BakedHistoricalDetailedSeasonSource → DetailedMatchEngine`의 실제 상세 경기 경로와 기본 BalanceTable / Rating Curve 45·0.45 기준이다. Unity PlayMode 또는 모든 게임 모드의 E2E 검증은 아니다. 원작 카드 표시 수치의 재현 오차와 경기 밸런스 결과를 구분한다.

최종 Python 평가·베이크·보충 카드 테스트 **87개 통과**, .NET 진단 도구 빌드 **오류·경고 0개**다. 구속 외 변경, 확정된 카드 구속 손실, 신원 변경, 1만 경기 미달 또는 리그 지표 5% 초과 변화는 게시 도구가 거부한다.

## 재현·반영

```powershell
python Tools/KBOImporter/fit_velocity_estimation.py --output .tmp/velocity-model-check
python Tools/KBOImporter/synthetic_bake.py --input-dir Tools/KBOImporter/.cache/KBOImport/Normalized --years 1982-2025 --research-supplement Tools/KBOImporter/research_roster_supplement.json --editor-assets-dir .tmp/velocity-candidate --verify-editor-assets
```

이번 검증 작업 폴더는 `.tmp/velocity-fallback-v1/`이다. `Before/`, `Candidate/`, `ModelReproduction/fit-report.json`, `before-simulation.json`, `after-simulation.json`, `team-comparison.json`, `scope.json`에 근거가 있다. 최초 후보의 신원 재추첨이 섞인 경기 결과는 `discarded-identity-reroll-simulation.json`이며 최종 근거로 사용하지 않는다.

`publish_velocity_estimation.py --work .tmp/velocity-fallback-v1 --publish`는 검증 대상과 현재 파일의 동일성을 확인하고 `PublicationBackup/`에 백업한 뒤 Editor 원본·Editor Runtime·게임 Runtime에 동기화한다. `.meta`, Catalog GUID, 원본 Normalized 캐시, 관련 없는 자산은 수정하지 않는다. 반영 후 세 Archive를 다시 로드해 해시·스키마·콘텐츠 검증을 수행한다.

기존에 실행 중인 세이브나 이전 ContentHash의 역사 캐시를 새 데이터로 간주하지 않는다. 특수 카드 Peak 및 레시피 평가도 새 ContentHash 기준으로 다시 검증해야 한다.
