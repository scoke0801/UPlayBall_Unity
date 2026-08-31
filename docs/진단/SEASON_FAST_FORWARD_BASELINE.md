# 시즌 자동완료 성능 기준선

## 목적

시즌 자동완료를 재개 가능한 상태 머신과 무관전자 경기 실행 경로로 바꾸기 전에, 결과 결정론과
성능 측정 환경을 고정한다. 자동완료 최적화는 경기 확률이나 시즌 결과를 바꾸지 않는다.

## 2026-08-30 호출 경로

```text
UI_Scene_CareerDashboard.ConfirmSeasonAutoCompletion
→ CareerManager.BeginSeasonFastForward
→ UI Update 다음 프레임부터 AdvanceSeasonFastForwardFrame 반복
→ SeasonFastForwardSession.AdvanceNextStep
→ CareerSeasonService.AdvanceNextRound
→ CareerGameRunner.SimulateGame
→ MatchSimulator.Simulate(input, NullMatchEventSink.Instance)
→ DetailedMatchEngine.Simulate
```

- 월드는 10개 리그, 리그당 8구단, 구단당 80경기를 같은 `DetailedMatchEngine`으로 진행한다.
- `NullMatchEventSink`는 이벤트를 보관하지 않지만 투구 선택·타격 실행용 내부 데이터는 현재 생성한다.
- 저장소 루트의 `Tools/SimulationDiagnostics`는 match 밸런스와 결정론 진단을 소유한다.
- 실제 월드 시즌과 Unity GC/프레임 측정은 Game 레이어가 필요하므로 Unity Player 진단으로 분리한다.

## 측정 메타데이터

모든 결과에는 다음 값을 함께 남긴다.

```text
Git Commit SHA
Working Tree Dirty 여부
Runtime / OS / Process Architecture / CPU
Build Configuration
BalanceVersion / EngineVersion / RulesVersion / RngVersion / ContentHash
EngineKind / OutputProfile
Seed 시작값과 표본 수
```

Dirty 작업 트리에서 측정한 결과는 탐색용으로만 사용한다. 회귀 기준선으로 승격할 때는 clean commit과
데이터 콘텐츠 hash를 함께 고정한다.

## 검증 기준 PC

출시 플랫폼이 확정되기 전까지 다음 Windows PC를 임시 최소 사양 기준으로 사용한다.

```text
CPU: Intel Core i5-8250U 또는 동급 4코어 x64
Memory: 8 GB
Build: Windows x64 IL2CPP Release, 1920×1080, 60 Hz
```

## 1차 성능 목표

```text
확인 입력 뒤 진행 화면 첫 표시: 다음 렌더 프레임
메인 스레드 단일 무응답 구간: p95 8 ms 이하, 최대 33 ms 이하
정규시즌 자동완료 전체 시간: p95 15초 이하
진행 Snapshot: 100 ms보다 자주 발행하지 않고 최신 값만 표시
결정론: 같은 입력·Seed·VersionStamp의 canonical CareerState checksum 완전 일치
```

총 소요 시간 목표는 최소 사양 Unity Player 20회 측정의 중앙값·p95·최댓값으로 판정한다. 콘솔
Release 결과는 알고리즘 기준선이며 출시 성능 판정값으로 사용하지 않는다.

## 진단 명령

```text
SimulationDiagnostics balance-match 10000
SimulationDiagnostics benchmark-match 10000 full
SimulationDiagnostics benchmark-match 10000 background
```

월드 시즌 benchmark와 Unity GC pause는 Player 진단이 추가될 때 별도 명령으로 기록한다.

## Unity Player 계측

자동 진행 뒤 `CareerManager.CreateLastFastForwardRuntimeReport()`를 호출하면 Unity 버전,
Mono/IL2CPP, CPU, 버전 스탬프, 총 시간, 최대 라운드 시간, 관리 할당량, Gen0 횟수를 한 번에 얻는다.
Profiler Timeline에서는 다음 마커를 함께 비교한다.

```text
Career.FastForward.Step
Career.MatchInput.Build
Career.Match.Simulate
Career.BackgroundLeague.Commit
Career.Round.World.Before
Career.Round.ActiveLeague.Commit
Career.Round.World.After
Career.Round.Services
Career.FastForward.Finalize
```

Editor Mono, Player Mono, Player IL2CPP 각각 시즌 20회 이상을 기록하고 중앙값·p95·최댓값을
별도 집계한다. 이번 변경에서는 Player 빌드와 실행 측정을 수행하지 않았다.

## 백그라운드 실행 활성화 게이트

현재 실행 모드는 `CooperativeMainThread`다. `CareerState` 전체를 왕복하는 정식 저장 DTO 또는
IL2CPP 호환 깊은 복사 계약이 아직 없으므로 라이브 상태를 `Task.Run`에서 변경하지 않는다.

다음 조건을 모두 만족할 때만 `WorkingCopyBackground`를 추가한다.

```text
최소 사양 Player에서 최대 라운드 시간이 33 ms를 넘음
CareerState Working Copy 생성과 원자적 교체 경로가 존재함
Fault 시 Live State 유지와 마지막 안전 라운드 중단 정책을 검증할 수 있음
Unity 객체·Resources 로딩을 작업 스레드 전에 순수 DTO로 변환함
```

리플렉션 기반 객체 복제는 IL2CPP/AOT와 저장 마이그레이션 계약을 우회하므로 사용하지 않는다.

## C2 할당 최적화

`DetailedMatchState`가 경기 동안 `PitchOption[]`과 최근 구종 버퍼를 독점 소유한다. 각 투구는
피로가 반영된 옵션 값을 같은 버퍼에 다시 채우며, AI는 유효 개수까지만 읽는다. 외부 미니게임
입력이 실제로 연결된 경우에만 요청이 버퍼 수명 밖으로 나갈 수 있으므로 독립 배열을 복사한다.

정량 절감값은 `benchmark-match ... background`와 Unity Memory Profiler를 다시 실행한 뒤 기록한다.
이번 변경에서는 요청에 따라 빌드, 결정론 회귀, 10,000경기 회귀를 실행하지 않았다.
