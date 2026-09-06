# 구종 경기·성장 검증

## 경기 곡선 적용 후 회귀 수정

DetailedTeamGameState의 대타 우위 기준 8, 대주자·수비 교체 우위 기준 14는 원래 표시 능력 단위였다.
경기 능력만 .30으로 압축하면 교체 기준을 만족하지 못하므로, 기준과 배치 불이익도 같은
기울기로 투영하도록 수정했다. 새 기능이나 교체 빈도 보너스를 추가한 것이 아니라 단위를 맞췄다.

- BenchCompetition 20시즌 회귀: 선발 1,188경기, 교체 출전 203회, 5,425타수. Passed=1.
- 56 대 48 팀의 5,000경기 승률: 강팀 54.6%. 초기 기준을 기존 >58%에서 >53%로 개정했다.
  표시 차이 8이 경기 입력 차이 약 2.4로 완화된 계약에 맞춰 통계적으로 분명한 우위를 요구한다.
  한 슬롯 교체에 과도한 승률 효과를 허용하는 이전 분산으로 되돌리지 않는다.
- MatchSimulationStatisticsTests 전체 2 passed. 평균팀 5,000경기와 전력 비교 5,000경기,
  SkillBoard 비교 10,000경기 검증을 포함한다. Epic 교타 +4의 AVG .282 대 기본 .279 방향도 유지했다.
- MiniGameSimulationTests 18 passed, HistoricalMatchIntegrationTests 7 passed 재확인.
- 최종 전체 Headless 회귀는 Passed 628 / Failed 0 / Skipped 7로 통과했다.

## 실행 범위

`dotnet build Tools/HeadlessRegression/EditModeTestRunner/EditModeTestRunner.csproj --no-restore -c Release`:
경고 0, 오류 0. slope .30 클래스별 실행은 PitchArsenalResolverTests 14 passed,
MiniGameSimulationTests 18 passed, HistoricalMatchIntegrationTests 7 passed,
MatchSimulatorTests 11 passed(합계 50).
이는 Unity PlayMode/Presentation 시각 검증 결과가 아니다.

`dotnet Tools/SimulationDiagnostics/bin/Release/net10.0/SimulationDiagnostics.dll controlled-cost 10000`:
2025 Runtime JSON을 직접 소비한 총 40,000경기. 구단과 상대, Seed, 교체 슬롯을 고정한다.
각 Cost/역할 후보를 6능력 단순평균으로 정렬해 중앙 표본 한 명을 선택했다.
RoleAdjustedComposite 중앙값 표본이나 전체 Cost 모집단 평균 효과로 해석하지 않는다.
구속·성장 출력은 아래 실행 결과와 같다.

## 최종 경기 곡선과 채택 근거

표시 BaseStat/Cost는 바꾸지 않았다. 경기 스냅샷만
`50 + (SoftCap/HardCap 처리 결과 - 50) × .30`으로 변환한다.
SoftCap 120, HardCap 140, 초과 기울기 .5를 먼저 처리한다.
Runtime 설정은 `Assets/10.Datas/Resources/NewGame/MatchRatingCurve.json`이며 JSON 내용 해시를
Production ContentHash에 합쳐 기존 시뮬레이션 캐시와 구분한다.

각 후보에서 같은 2025 카드·상대·Seed로 4×10,000경기, league 10,000경기를 실행했다.
팀 Win 차이 초기 Gate는 타자 약 3~5%p, 투수 약 5~8%p로 두었다.
이는 표본 1쌍을 위한 초기 검증 기준이며 전체 리그·역할·시대의 확정 상한이 아니다.

| 효과 기울기 | 타자 Win 차이 | 투수 Win 차이 | 판정 |
|---|---:|---:|---|
| 압축 전 | 9.82%p | 19.81%p | 과도 |
| .50 | 7.46%p | 12.61%p | 추가 완화 필요 |
| **.30** | **3.29%p** | **6.68%p** | 초기 Gate 범위, 채택 |

| 최종 타자 1B | Cost 1 | Cost 10 |
|---|---:|---:|
| AVG / OBP / SLG | .247 / .329 / .362 | .309 / .376 / .519 |
| HR(1만경기) / K% | 414 / 22.97 | 1485 / 13.49 |
| RBI/G / R/G | .323 / .411 | .577 / .564 |
| 팀 RD/G / Win% | .118 / 47.52 | .442 / 50.81 |

| 최종 투수 동일 선발 슬롯 | Cost 1 | Cost 10 |
|---|---:|---:|
| K/9 / BB/9 / HR/9 | 6.675 / 3.492 / .734 | 8.134 / 2.751 / .468 |
| WHIP / RA/G | 1.562 / 2.119 | 1.343 / 1.655 |
| 팀 RD/G / Win% | .065 / 47.48 | .743 / 54.16 |

최종 league 10,000경기: AVG .272, OBP .348, SLG .421, 팀 R/G 3.721,
팀 HR/G .629, BB% 9.04, SO% 18.14, HBP% 1.38, Errors/Game .507.

적용 범위는 `DetailedMatchState`에 들어가는 전체 로스터(선발·타선·벤치·불펜)다.
따라서 즉시 결과·백그라운드·상세 중계·선수 미니게임이 같은 경기 입력을 소비한다.
`ManagerModeMatchService.CreatePlayer`는 원래의 100 조기 clamp를 제거하고
Base+Edition+Training+Enhancement+TeamColor 합계를 HardCap/SoftCap 이후 압축한다.
Player의 HasResolvedMatchRatings로 중복 압축을 방지한다. 원본 Bake/선수 상태는 보존한다.

남은 Gate: 여러 OriginYear/역할·카드 조합 표본, Endgame 누적 효과, 실제 Unity UI 검증.
물리 구속은 마지막에 별도 원본 입력으로 분리했다(아래 최종 재검증 참조).
이하 표는 **구종 통합 후 경기 분산 압축 전** 진단을 근거로 보존한다.

### 물리 구속 분리 후 최종 재검증

`Player.UncurvedPitcherAttributes`는 TeamColor를 포함한 곡선 전 실효 입력이고,
`BakedPitcherAttributes`와 `PermanentPitcherAttributes`는 성장 baseline/영구 변화 전용으로 유지한다.
`PitchExecutionResolver`의 실제 구속은 곡선 전 Velocity에 경기 중 condition/fatigue 변화만 더한다.
따라서 기본 컨디션에서 카드 대표 km/h와 투구 옵션 중심값이 일치하고,
Velocity +10이면 포심은 실제 +3.2 km/h다. 새 테스트를 포함한
PitchArsenalResolverTests **15 passed**, MiniGameSimulationTests **18 passed**, 재빌드 오류 0.

이 최종 변경 뒤 1,000경기씩 controlled 4개 + league 1개, 총 5,000경기 smoke를 재실행했다.

| 최종 smoke | Cost 1 | Cost 10 |
|---|---:|---:|
| 타자 AVG/OBP/SLG | .250/.336/.372 | .302/.370/.511 |
| 타자 교체 팀 Win% | 48.10 | 51.70 |
| 투수 K9/BB9/HR9 | 6.869/3.504/.687 | 8.095/2.730/.451 |
| 투수 WHIP | 1.538 | 1.326 |
| 투수 교체 팀 Win% | 46.70 | 53.70 |

Win 차이 타자 +3.60%p, 투수 +7.00%p로 초기 Gate 방향을 유지했다.
최종 league smoke AVG .271, OBP .348, SLG .418, 팀 R/G 3.684, HR/G .620,
BB% 9.12, SO% 18.03, HBP% 1.46이다.
앞의 10,000경기 표는 물리 구속 분리 직전 결과이며, 마지막 코드의 전체 10,000경기 결과로
잘못 표시하지 않는다.

## 압축 전 한 슬롯 Cost 비교

| 타자(동일 1B 슬롯) | Cost 1 | Cost 10 |
|---|---:|---:|
| Base 6능력 | 30,31,35,55,54,34 | 73,100,40,60,62,70 |
| AVG | .194 | .380 |
| OBP | .278 | .711 |
| SLG | .259 | .713 |
| HR(10,000경기) | 45 | 1558 |
| K% | 34.96 | 2.47 |
| RBI/G | .244 | .589 |
| 개인 R/G | .298 | .843 |
| 팀 RD/G | .050 | 1.066 |
| 팀 Win% | 46.77 | 56.59 |

| 투수(동일 선발 슬롯) | Cost 1 | Cost 10 |
|---|---:|---:|
| 원래 역할 | MiddleRelief | Starter |
| Base 6능력 | 25,55,29,33,29,33 | 57,55,80,75,71,75 |
| Arsenal 수 | 5 | 3 |
| K/9 | 7.270 | 9.836 |
| BB/9 | 5.423 | .320 |
| HR/9 | .836 | .257 |
| WHIP | 1.710 | .946 |
| 개인 RA/G | 1.958 | 1.254 |
| 팀 RD/G | -.105 | 1.717 |
| 팀 Win% | 45.24 | 65.05 |

2025 Cost 1 Starter 표본이 없어 투수는 같은 선발 슬롯에 강제로 배치했다.
기존 역할·Stamina·Arsenal이 다르므로 동등 역할 간 인과 추정이 아니다.
구종은 재추첨 없이 Bake의 구종을 로드했다. 표본 식별자는 도구 출력에 남는다.

**Balance Gate 미통과:** 높은 Cost의 .711 OBP와 .320 BB/9는 타당한 분포라고 결론 낼 수 없다.
한 장 교체의 승률 차이는 타자 +9.82%p, 투수 +19.81%p다.
이 결과는 현재 엔진의 Cost 간 비교이며 변경 전/후 엔진 대조 결과가 아니다.
Cost 경계나 BaseStat은 이 도구에서 변경하지 않았다.

## 구종별 성장 측정

BaseMastery 60, 시작 Ability 50, affinity 1인 동일 입력에서 한 능력만 변경했다.
Velocity 열은 +1/+5/+10의 km/h 차이, Breaking 열은 실전 품질 차이다.
마지막 세 열은 비주력 구종의 성장 효율(3/5/6구종)이다.

| Pitch | Velocity +1/+5/+10 | Breaking +1/+5/+10 | 성장효율 3/5/6 |
|---|---|---|---|
| FourSeamFastball | .320/1.600/3.200 | .005/.025/.050 | .900/.702/.504 |
| TwoSeamFastball | .320/1.600/3.200 | .050/.250/.500 | .814/.635/.456 |
| Cutter | .224/1.120/2.240 | .160/.800/1.600 | .763/.595/.427 |
| Slider | .112/.560/1.120 | .380/1.900/3.800 | 1.035/.807/.580 |
| Curveball | .048/.240/.480 | .400/2.000/4.000 | .960/.749/.538 |
| Changeup | .070/.352/.704 | .240/1.200/2.400 | .783/.610/.438 |
| Splitter | .128/.640/1.280 | .300/1.500/3.000 | .767/.598/.429 |
| Sinker | .256/1.280/2.560 | .180/.900/1.800 | .802/.625/.449 |
| Sweeper | .090/.448/.896 | .420/2.100/4.200 | .727/.567/.407 |
| Slurve | .064/.320/.640 | .400/2.000/4.000 | .798/.623/.447 |
| KnuckleCurve | .051/.256/.512 | .440/2.200/4.400 | .695/.542/.389 |
| CircleChangeup | .064/.320/.640 | .240/1.200/2.400 | .750/.585/.420 |
| Forkball | .102/.512/1.024 | .340/1.700/3.400 | .678/.529/.380 |
| Screwball | .058/.288/.576 | .400/2.000/4.000 | .480/.374/.269 |
| Knuckleball | .026/.128/.256 | .460/2.300/4.600 | .325/.254/.182 |

주력/준주력은 breadth modifier 1을 유지한다. 보조구종은 5개 .78, 6개 .56이다.
성장 효율은 type efficiency / difficulty × affinity × focus × breadth로 계산한다.
현 구종 품질 자체에는 arsenal count 패널티가 없다.
Breaking만 바꿨을 때 제구 타원의 X/Y 반경이 같음을 테스트했다.

## 소비 경로

- PitchEffectivenessResolver는 실제 구속과 구종별 품질·움직임을 계산한다.
- Stable 품질은 BaseMastery + 영구 능력 변화분 × 성장 효율이다. 초기 BaseMastery를 보존한다.
- Player는 baked/permanent 투수 능력을 별도로 전달할 수 있다. ManagerModeMatchService는
  영구 보너스와 TeamColor를 구분해 전달한다. 저장 BaseMastery는 변경하지 않는다.
- 기존 커리어 생성 입력에는 과거 능력 baseline이 없을 경우 현재 능력을 baseline으로 사용한다.
  그런 경로의 NaturalGrowth는 공통 능력 기여로 반영되지만 affinity/breadth가 과거 성장분에
  소급 적용되지 않는다. 별도 저장 기반의 커리어 최초 능력 연결은 후속 Gate다.
- AI는 usage/quality/제구 위험/카운트/이전 구속차/반복/손/approach를 가중 선택한다.
  제구·품질 입력은 현재 피로를 포함한다. AI 선택 가중 일부는 기존 스타일의 휴리스틱이며
  전체 선택계수의 Balance JSON 이관은 남아 있다.
- 투수 미니게임은 같은 PitchOption 목록을 최대 6개까지 표시하고 1~6 키 입력을 지원한다.
  Grade 및 대표 km/h와 모든 15구종 한국어 이름을 표시한다. 시각 검증은 별도다.

## 진단 기본 로스터 경기

새 코드 10,000경기: AVG .271, OBP .343, SLG .420, 팀 R/G 3.606,
팀 HR/G .618, BB% 8.66, SO% 18.29, HBP% 1.22, Errors/Game .505.
변경 전 기존 Release 바이너리 1,000경기: AVG .276, OBP .348, SLG .427,
팀 R/G 3.829, 팀 HR/G .619, BB% 9.05, SO% 17.34.
표본 크기와 빌드 시점이 다르므로 엄밀한 전후 paired comparison은 아니다.

초기 구현에서 확인된 enum 검증 박싱은 기존 빈 Arsenal 호환 프로필을 캐시해 제거했다.
1,000경기 background 측정은 기존 876.5μs/game·55,588 bytes/game,
변경 중간 시점 910.4μs/game·58,385 bytes/game이었다. 이후 baseline metadata 통합이 있어
최종 성능 Gate는 같은 커밋/ContentHash에서 다시 측정해야 한다.
