# Cost와 구종 작업 전 능력치 감사

> 상태: 사전감사 완료. 아래 미완료 항목은 후속 구현 Gate이며 이 감사의 측정 범위에는 포함되지 않는다.

## 측정 범위

`Assets/10.Datas/HistoricalSimulation/1982-2025/Years`의 44년 Runtime JSON을 전수 조사했다.
SourceBacked 17,333명과 ReplacementGenerated 54명, 합계 17,387명이다. 역할별 Composite는
`Tools/KBOImporter/derivation_balance.json`의 `roleCompositeProfiles` 가중치로 재계산했다.
아래 값은 시즌 가치 Cost 점수가 아닌 6능력 가중 평균이다.

| Cost | 타자 수 | 타자 Composite 중앙값 | 투수 수 | 투수 Composite 중앙값 |
|---|---:|---:|---:|---:|
| 1 | 2294 | 33.84 | 801 | 33.32 |
| 2 | 1643 | 33.82 | 1171 | 33.28 |
| 3 | 1131 | 35.24 | 830 | 35.94 |
| 4 | 969 | 38.30 | 776 | 39.00 |
| 5 | 878 | 42.74 | 824 | 42.23 |
| 6 | 798 | 46.58 | 778 | 46.05 |
| 7 | 548 | 51.73 | 583 | 50.20 |
| 8 | 1250 | 60.04 | 1187 | 53.40 |
| 9 | 31 | 71.94 | 569 | 60.82 |
| 10 | 199 | 74.98 | 127 | 68.72 |

Cost1→10 중앙값 차이는 타자 41.14, 투수 35.40이다. Cost1 P90과 Cost10 P10은 각각
타자 38.40/70.52, 투수 36.06/66.49로 떨어져 있다. 타자 포지션별 중앙값 차이도 32.93~45.48이다.
현재 전역 Z scale을 넓힐 근거가 없으므로 기존 scale과 Cost v9를 유지한다. Cost1/2의
큰 중첩은 시즌 가치와 종합 능력치가 다른 축이라는 현행 정책의 결과이며 UI로 역보정하지 않는다.

## 개성이 약해지는 실제 원인

- 투수 7,646명 모두 Velocity 55다. 실제 구속 기록이 없어 중립값을 소비한다.
- Stuff/Breaking/PitcherMental은 ERA 하나를 각각 scale 24/20/20으로 변환한다.
  Breaking과 Mental이 같은 것은 관측 Source의 제약이다.
- 타자 Arm 55는 4,872/9,741명, Defense 55는 4,733/9,741명이다.
- Cost10 투수 중앙값은 Stamina 58, Velocity 55, Stuff 76, Breaking 72, Control 72, Mental 72다.
- Reliability는 성과 prior Z=-1로 저표본을 당기며, 없는 관측값은 중립 55로 둔다.
  없는 관측값의 다양성을 Z scale 확대나 Cost 역보정으로 만들어서는 안 된다.

## Source of Truth 충돌과 유지 결정

현행 balance v12 / ability v6 / Cost `historical-season-value-v9`는 시즌 quality·workload와
제한된 역할 보정 이후 절대 가치 구간과 elite 자격을 사용한다. `OriginYear` 백분위는 진단값이다.
`assign_origin_year_costs`라는 이름만 보고 백분위 정책으로 오해하지 않는다. 과거 03절의
능력 Composite 고정 구간 설명과 최신 v8 도입 부록도 함께 정리할 필요가 있다.
이번 감사에서는 기존 Cost 정책을 변경하지 않는다.

## MatchRatingCurve와 Endgame Gate

`MatchRatingCurve.Resolve`는 기존 `EffectiveRatingCapTable`을 소비한다. 기본 SoftCap 120,
HardCap 140, 이후 기울기 0.5를 유지하며 `EffectiveRatingResolver`의 기존 공식을 공용 경계로
추출했다. 1~120의 수치와 140→130 변환 결과는 변경되지 않는다. 출력은 확률이 아니라
개별 경기 Resolver 계수가 소비할 수치다.

**경기 전체의 140 입력 연결은 아직 미완료다.** `ManagerModeMatchService.CreatePlayer`가
합산 능력치를 100으로 자르고, `BatterAttributes`와 `PitcherAttributes` 생성자도 기본 능력치
0~100을 검증한다. `DetailedMatchEngine.ApplyHistoricalPitcherModifiers`도 다시 100으로 제한한다.
BaseAttributes 생성자의 상한만 넓히면 정적 선수 능력 계약이 깨지고, 일부 clamp만 제거하면
타격·투구·수비 입력의 일관성이 깨진다.

이를 완료하려면 BaseAttributes와 별도의 경기 Effective 입력 전달, Condition/Tactic 이후 단일
curve 적용, 모든 수비·주루 소비 경로의 검증이 필요하다. 이후 Cost1→10 한 슬롯 교체의 paired seed
Detailed Match와 장기 Endgame 분포를 실행해야 한다. **공식 수식 단위 테스트 통과를 Production
Endgame 검증 완료로 보고하지 않는다.**

`MatchRatingCurveTests`는 96개 영구/일시 보정 조합(상·하위 표본 총 192건)의 순서, 120 변곡점의 연속성,
140 상한, 외부 CapTable 주입, 비유한 입력 거부를 검증한다. Edition 수치는 테스트 입력이며
실제 Edition catalog 전체를 검증했다고 해석하지 않는다. Controlled Match 수치와 장기 승률은
이번 사전 감사에서 측정하지 않았다.
