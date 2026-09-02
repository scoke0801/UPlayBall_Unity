# Headless 회귀 실행 지침 (UPlayBall)

Unity 에디터를 켜지 않고 **실제 Production Career/World 진행 코드를 그대로** .NET Release로 돌리기
위한 지침이다. 장기 회귀가 느린 원인은 시뮬레이션 자체가 아니라 Unity EditMode 실행 경로(Mono,
Debug IL, `ENABLE_UNITY_COLLECTIONS_CHECKS`, 에디터/NUnit 오버헤드)이므로, 병렬화가 아니라
**실행 환경을 분리**하는 것이 해법이다.

## 1. 어셈블리 경계

```text
Baseball.Core          순수 C# (noEngineReferences)
      ↑
Baseball.Simulation    순수 C# (noEngineReferences)
      ↑
Baseball.Game          순수 C# (noEngineReferences) — Career/World 진행 로직 전체
      ↑
Baseball.Game.Unity    Unity 의존 — MonoBehaviour 매니저, ScriptableObject, SceneFlow, Input, Sound
      ↑
Baseball.Presentation  Unity 의존
```

`Baseball.Game`은 Unity를 참조하지 않는다. Unity 전용 구현이 필요하면 **Game 레이어에 순수 계약을
두고 `Baseball.Game.Unity`가 주입**한다. 현재 두 지점이 그렇게 되어 있다.

| Game 레이어 계약 | Unity 구현 | 등록 위치 |
| --- | --- | --- |
| `IProfilerSectionSink` / `ProfilerSectionSink.Current` | `UnityProfilerSectionSink` (ProfilerMarker) | `GameBootstrap.RegisterUnityAdapters` |
| `CareerNewsConfigurationProvider.SetLoader` | `CareerNewsDefinition.LoadConfiguration` (Resources) | `GameBootstrap.RegisterUnityAdapters` |

어댑터를 등록하지 않은 프로세스(EditMode 테스트, Headless 러너)에서는 각각 no-op과 코드 기본값으로
동작하므로 **같은 로직이 그대로 돈다.**

`Baseball.Game`의 `internal` 접근은 `AssemblyInfo.cs`의 `InternalsVisibleTo("Baseball.Game.Unity")`로
분리 이전과 동일하게 유지한다. 그 아래 레이어에는 열지 않는다.

## 2. Headless 프로젝트 구성

`Tools/HeadlessRegression/`은 **Assets의 소스를 직접 컴파일**한다. Unity가 만든
`Temp/Bin/Debug/*.dll`을 참조하지 않는다 — 그 방식은 Unity를 먼저 켜야 하고 Debug IL이라 측정값이
왜곡된다.

| 프로젝트 | 역할 |
| --- | --- |
| `Baseball.Core.Headless.csproj` | `Assets/02.Scripts/Core` 컴파일 |
| `Baseball.Simulation.Headless.csproj` | `Assets/02.Scripts/Simulation` 컴파일 |
| `Baseball.Game.Headless.csproj` | `Assets/02.Scripts/Game` 컴파일 (`Game/Unity/**` 제외) |
| `Baseball.{Core,Simulation,Game}.Tests.Headless.csproj` | EditMode 테스트 컴파일 |
| `WorldRegressionRunner` | 다중 리그 장기 월드 회귀 |
| `EditModeTestRunner` | EditMode NUnit 스위트 실행 |

`Baseball.Game.Headless.csproj`가 `Game/Unity/**` 제외만으로 컴파일된다는 사실 자체가 **경계가
살아 있다는 회귀 검사**다. 제외 목록을 늘려야 컴파일된다면 경계가 깨진 것이다.

## 3. 실행

```bash
# 다중 리그 장기 회귀 (기본: 10리그 × 10시즌, 같은 Seed 2회 실행 후 checksum 비교)
dotnet run --project Tools/HeadlessRegression/WorldRegressionRunner/WorldRegressionRunner.csproj -c Release

# 옵션
#   --seed <ulong>   월드 Seed
#   --seasons <int>  시즌 수
#   --runs <int>     반복 실행 수 (2 이상이면 결정론 검증)
#   --no-stages      구간별 계측 출력 생략

# EditMode 테스트 (전체 / 클래스 / 클래스+메서드)
dotnet run --project Tools/HeadlessRegression/EditModeTestRunner/EditModeTestRunner.csproj -c Release
dotnet run --project Tools/HeadlessRegression/EditModeTestRunner/EditModeTestRunner.csproj -c Release -- MultiLeagueWorldTests
```

`WorldRegressionRunner`는 반복 실행의 시즌별·최종 checksum이 모두 일치하면 exit code 0, 하나라도
어긋나면 1을 돌려준다. `EditModeTestRunner`는 실패 테스트가 없으면 0이다. 그대로 CI 판정에 쓴다.

## 4. 지켜야 할 것

- **Benchmark 전용 축약 시뮬레이션을 만들지 않는다.** 러너는 게임이 실제로 쓰는
  `NewGameFlow` → `CareerSeasonAutoCompletionService` → `CareerGrowthService` →
  `CareerSeasonTransitionService`만 호출한다.
- **같은 World 내부를 병렬화하지 않는다.** World는 DomainEvents·통계·순위·계약 원장·RNG 진행 상태를
  공유하므로 병렬화는 결정론을 깬다. 독립적인 World Seed 여러 개를 Harness 레벨에서 병렬로 돌리는
  것만 허용한다.
- **계측이 도메인 로직을 오염시키지 않는다.** 구간 시간은 `ProfilerSection`(어댑터 주입)으로만 잰다.
  checksum 계산 시간은 `TotalSeconds`에서 제외해 게임 진행 시간과 섞지 않는다.
- **측정 없이 최적화하지 않는다.** Hotspot 후보가 보여도 회귀가 성능 예산을 넘거나 프로파일에서
  병목이 확인되기 전에는 건드리지 않는다.

## 5. 성능 예산

절대 시간은 머신 편차가 크므로 실패 기준으로 쓰지 않는다. Baseline을 기록하고 다음으로 판단한다.

```text
Warning              = baseline × 1.15
Regression candidate = baseline × 1.30
```

기능 CI의 실패 기준은 **결정론(checksum 일치)과 테스트 통과**이고, 절대 시간은 Benchmark Report로만
남긴다.
