# UPlayBall_Unity

Unity 6 기반 싱글 플레이 야구 시뮬레이션. 선수 한 명의 커리어를 사는 **선수 모드**와
구단 전체를 운영하는 **구단주 모드**를 별도 진입점과 Save 상태로 제공한다. 두 모드는 동일한
Historical World와 경기 Simulation을 공유하지만, 선수 모드의 라인업·교체·전술 권한은 감독 AI에
남기고 카드 소유 경제와 구단 운영 상태는 구단주 모드에만 둔다. 기획·설계 기준 문서는
`BaseballManager_PROJECT.md`, 작업 지침은 `docs/지침/README.md`를 따른다.

## Historical World 데이터 원칙

```text
실제 Source PlayerSeason / TeamSeason
        ↓ 1:1 Runtime-safe Canonical Bake
        ↓
World별 Player / Franchise 표시 Identity 생성
        ↓
Detailed Match / Season Simulation
        ↓
가상 개인·팀 기록 / 순위 / 포스트시즌 / 수상
```

선수 및 일반 Franchise를 다른 Reference 선수·구단으로 합성하지 않는다. `PlayerSeason`과
`TeamSeason`의 능력 데이터 정체성은 Source와 1:1로 유지하며, World에서 가상화되는 것은
표시 Identity와 Simulation History다. 같은 Baked Content의 `BaseAttributes`, `Cost`,
`TrainingCeiling`, `Origin`은 World Seed나 수상 결과와 무관하게 고정된다.

실제 선수명과 실제 구단명은 Runtime 표시 콘텐츠가 아니다. 새 World가 생성될 때
`WorldIdentityRegistry`에 고유한 가상 이름을 확정해 저장하고, Load에서는 이름 생성이나 과거
시뮬레이션을 다시 실행하지 않는다. 정식 새 게임의 과거 역사는 `DetailedMatchEngine` 계열의
동일 야구 판정 모델로 생성한다. `OriginalHistory`가 남아 있다면 비교·회귀용 Legacy 검증 경로일
뿐 사용자 선택 가능한 정식 모드가 아니다.

**레퍼런스:** 엔트리브소프트의 `프로야구매니저`(약칭 `프야매`)를 핵심 레퍼런스로 삼는다.
기능을 새로 구현할 때는 프야매가 해당 기능을 어떻게 다루는지 웹에서 조사한 뒤,
이 프로젝트의 범위(`BaseballManager_PROJECT.md` 31절 MVP 제외 목록)에 맞게 취사선택해
설계·구현한다.

## 구단주 모드 4종 확장 상태

구단 운영·경기 전 분석·코칭스태프·Condition/Chemistry의 순수 Core/Simulation 계약과
`OwnerModeManager` Production 경로, Save schema 4, 공용 `SharedGameShellView` 기반 uGUI 화면이
연결되어 있다. `DetailedMatchEngine` 경기 결과가 선수 상태와 Familiarity, 팬·관중·수익에 반영되며,
실제 투수-포수 수비 아웃은 `BatteryUsageReport`로 Familiarity에 적립된다. 시설·스태프 보정은 각각
하나의 Recovery/Intel Context에서 합성하고, Staff 훈련 효율도 기존 CardTraining 경로에서만
소비한다. 시즌 종료 transaction은 미정산 급여·계약 기간을 한 번만 처리하고 다음 시즌 일정을
결정론적으로 생성하며, 팬·구장·시설·티켓 정책은 이어 간다. 구단주 uGUI에서는 모든 저장
LineupPreset을 현재 로스터로 재검증해 선택할 수 있고 TeamColor/Tactic 각 2슬롯을 실제 catalog
후보 안에서 순환 적용한다. Condition은 연속 원값 대신 데이터화된 1~10단계와 한글 상태를 표시한다.

09~12절의 조정값은 `Assets/10.Datas/Resources/NewGame/OwnerExpansionBalance.json` 한 곳에서
저작하고, `OwnerExpansionBalanceConfig`가 이를 순수 C# Balance 계약으로 변환한다. Production
구단주 모드는 Config 누락·불완전 상태를 즉시 거부하며, 선수 커리어의 `LoadConfiguration()`과
Balance version/hash에는 이 구단주 전용 표를 합성하지 않는다.

관련 headless 규칙·결정론·Persistence·Production 경로의 대상별 실행과 Explicit 장기 통계 6건은
통과했다. 장기 표본은 8 Seed × 10시즌 × 홈 72경기(총 80시즌·재무 이벤트 5,760건)다. 최신
Balance/시즌 lifecycle/UI 병합 전 Unity 대상 실행은 39/39 통과했지만, 최신 통합본의 집계 Unity
Test Runner와 Player Build는 사용자 지시에 따라 생략했으며 실제 16:9 UI 시인성도 확인하지 않았다.
현재 상태는 **Production 코드가 연결됐지만 최종 실행 검증과 일부 Consumer가 남은 부분 완료**다.
승강·로스터 이월, 임의 선수 교체/전체 Tactic Inventory 편집, 실제 효과를 소비하지 않는
`TacticLab` 연구 Consumer도 남아 있다. 상세 Gate는
`docs/todo/역사시뮬레이션_구단주모드/08_구현_로드맵_검증기준.md`와
`13_4종_통합_구현로드맵_Codex.md`를 따른다.

## 프야매 Cost 연구 상태

[PMReference 연구 보고서](Tools/PMReference/reports/PM_REFERENCE_RESEARCH.md)에 카드628개 관측과
출시·재평가·후속 수정의 근거를 보존한다. 후기 Normal·Source 연결 확인 표본은 여전히 부족하므로
PM 최종판을 정확히 복원했다는 Calibration Gate는 미통과다. 사용자의 명시적 진행 결정에 따라
현행 코드의 Cost는 `historical-season-value-v9`이며 역할별 Composite의 고정 경계를 쓴다.
OriginYear 백분위는 진단값이다. 기존 v8/백분위 설명과 최신 구현의 차이를 이번 구종 작업에서
임의로 되돌리지 않았으며, percentile 정책 채택 여부는 별도 미해결 Gate다.
1982~2025 Canonical Archive와 Runtime을 재Bake했다. Reference는 게임 빌드에 포함하지 않는
`Tools/PMReference/` 연구 전용이다.

## 투수 구종과 카드 뒷면

아래 생성 통계는 작업 중 확보한 **사전 검증값**이다. 최종 통합 코드 기준 Bake와 ContentHash
확정은 사용자가 수행하며, 이번 보고서를 최종 정본 Bake 승인으로 해석하지 않는다.

일반 투수의 2~6구종은 관측 자료가 없는 **Synthetic** 선수 개성 데이터다. 구종은 Offline에서
`PlayerSeasonId + pitchGenerationSeed + PitchBalanceVersion`으로 확정하고, World Load나 Edition
변경으로 재추첨하지 않는다. 실제 이름·구종 실측치를 수집한 것으로 표현하지 않는다.
계수 정본은 `Assets/10.Datas/Resources/NewGame/PitchArsenalBalance.json`이며 Python Bake와
Production C#이 함께 읽는다. Headless 기본값은 이 JSON에서 자동 생성한다.

`PitchRepertoireEntry.BaseMastery`는 고정하고, 현재 Stuff/Breaking/Control과 영구 성장 적성은
공통 Resolver가 계산한다. 5~6구종의 보조 성장만 분산하며 현재 성능은 깎지 않는다.
카드 뒷면은 기존 프레임과 하단 4×4 스킬 블록 영역을 유지한다. 투수는 기존 중앙 정보 영역에만
구종 등급·km/h를 추가하고, 타자는 기존 수비 포지션 표시를 유지한다.
실제 Unity 시각 검증과 전체 경기 밸런스 Gate는 구현 완료와 구분한다.

사전 생성 결과는 `docs/reports/pitch_arsenal_generation_v1.json`을 따른다. 7,646투수/27,509구종의
동일 Seed 재생성 일치, Python 구종 규칙 3건 및 기존 Bake 회귀 27건을 확인했다.
ContentHash/BalanceContentHash 변경으로 이전 WorldHistory Bake는 사용하지 않고 기존 Detailed
Simulation fallback을 사용하므로 사전 History 재Bake 전에는 새 게임 준비 시간이 늘어난다.
