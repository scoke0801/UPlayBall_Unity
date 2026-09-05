# World History Bake 지침

새 게임 시작이 느려지지 않게 유지하기 위한 규칙이다. 대상은 **구단주 모드 시작**과 **선수 모드의 "구단 오퍼 확인"** 두 진입점이다.

## 왜 있는가

두 진입점은 모두 `HistoricalWorldRuntimeBuilder.Build()`로 들어가고, 그 안에서 1982~2025년 **44시즌**을 실제로 시뮬레이션했다. 시즌당 80경기 × 10구단 ÷ 2 = 400경기에 올스타·포스트시즌을 더해 **약 1만 8천 경기**이며, 축약 엔진이 없어 전부 투구 단위 정밀 시뮬레이션이다.

측정값(`WorldRegressionRunner`, .NET 10 Release, 12코어 데스크톱): 3,289경기 2,638ms → **약 1,250경기/초**, 경기당 약 215KB 할당. 1만 8천 경기로 환산하면 **CoreCLR에서도 약 15초, 할당 약 3.9GB**다. Unity의 Mono + Boehm GC는 이보다 느리다.

결과가 결정론적이므로 이 비용은 **매번 낼 이유가 없다.** 같은 Seed·콘텐츠·밸런스에서 나온 결과는 항상 같으므로 빌드 타임에 한 번 굽고 런타임은 읽기만 한다.

## 구조

```text
[Editor 저작]  WorldHistoryBakeTool
                 → WorldHistoryBakeService.Create()   (실제 44시즌 시뮬레이션)
                 → WorldHistoryBakeCodec.Encode()     (이진 산출물)
                 → BakedWorldHistoryCatalog (SO) + *.bytes

[Runtime]      HistoricalWorldRuntimeBuilder.Build()
                 → IBakedWorldHistorySource.TryLoad(key)
                    ├ 적중 → WorldHistorySaveMapper.Restore()   (읽기만)
                    └ 실패 → BuildSimulatedHistory()            (오늘과 동일, 느림)
```

- 계약은 `IBakedWorldHistorySource`(순수 C#, `Baseball.Game`)이고, TextAsset을 읽는 구현만 `Baseball.Game.Unity`에 있다. Core/Simulation/Game의 Unity 비의존은 유지된다.
- Editor 도구는 `Baseball.Simulation`을 참조하지 않는다. 그래서 `WorldHistoryBakeService`가 Game 경계의 진입점 역할을 한다.

## Bake Key

`BakedWorldHistoryKey`는 다음을 모두 포함한다.

| 항목 | 이유 |
| --- | --- |
| `RecordMode` | OriginalHistory는 Bake 대상이 아니다 |
| `WorldHistorySeed` | Seed가 다르면 다른 월드다 |
| `ContentHash` | 역사 원본 Bake가 바뀌면 경기 입력이 바뀐다 |
| `BalanceVersion` + `BalanceContentHash` | 경기 결과를 좌우한다 |
| (파일 헤더의 `FormatVersion`) | 산출물 포맷이 바뀌면 읽지 않는다 |

**Key가 하나라도 어긋나면 Bake를 조용히 무시하고 실제 시뮬레이션으로 되돌아간다.** 결과가 틀리지는 않고 **느려질 뿐**이다. 그래서 다시 굽는 것을 잊어도 게임은 깨지지 않지만, 새 게임 시작이 다시 60~90초가 된다.

## 언제 다시 구워야 하는가

아래 중 **하나라도** 하면 기존 Bake는 버려진다. 반드시 다시 굽는다.

### A. Key가 자동으로 어긋나는 경우 — 안전하지만 느려진다

1. **역사 원본 아카이브를 다시 생성했을 때.** `Tools/KBOImporter`의 importer·`synthetic_bake.py`·`derivation_cost.py` 등을 고쳐 `Assets/10.Datas/HistoricalSimulation/1982-2025/`를 다시 구우면 `manifest.json`의 `sourceManifest.contentHash`가 바뀐다. **이 프로젝트에서 가장 자주 발생하는 재굽기 사유다.**
2. **`UnityHistoricalContentProvider`의 `Supported*Version` 상수를 올렸을 때.** 이 상수들은 아카이브 재생성과 짝을 이루므로 1번을 동반한다. (`SupportedAbilityFormulaVersion`, `SupportedRosterBuilderVersion`, `SupportedCostFormulaVersion`, `SupportedDerivationBalanceVersion` 등)
3. **`BalanceTable`의 `Version` 또는 `ContentHash`가 바뀌었을 때.**
4. **`WorldHistoryBakeCodec.FormatVersion`을 올렸을 때.**
5. **`_ownerWorldSeed`나 `_careerWorldSeedPool`을 바꿨을 때.** 새로 넣은 Seed에는 대응하는 Bake가 없다.

### B. Key가 어긋나지 않는 경우 — 위험하다

**경기 시뮬레이션 코드를 고치면 Key는 그대로인데 결과만 달라진다.** 이때 낡은 Bake가 그대로 적중해서, 배경 역사만 옛 엔진 결과로 남고 이후 진행은 새 엔진으로 돌아간다. 조용히 어긋나므로 A보다 나쁘다.

해당하는 것:

- `MatchSimulator`, `DetailedMatchEngine`, 타석·주루·수비 판정
- `BakedHistoricalDetailedSeasonSource`(일정 생성, 올스타, 포스트시즌 시리즈)
- `WorldHistoryInitializer`, `AwardScoringPolicy`, `WorldAwardResolver`
- `MatchRandomStreams`, `DeterministicSeed` 등 난수 파생

**이 코드들을 고쳤다면 `BalanceTable.Version`을 올리거나 `ContentHash`를 바꿔서 Key가 어긋나게 만든 뒤 다시 굽는다.** 밸런스 수치를 바꾸지 않았더라도 그렇게 한다. Key를 일부러 깨는 것이 유일하게 안전한 방법이다.

### 다시 굽지 않아도 되는 것

표현·UI·커리어 진행 로직처럼 **44시즌 배경 역사 생성에 관여하지 않는** 변경은 무관하다. `WorldCardCatalog`·`SpecialCompositeTeams`는 Bake 대상이 아니라 런타임에 파생되므로, 그쪽 계수만 바꿨다면 다시 구울 필요가 없다.

## 지금 Bake가 유효한지 확인하는 법

가장 빠른 신호는 **새 게임 시작 시간**이다. 즉시 열리면 적중, 60~90초 걸리면 어긋난 것이다.

파일로 직접 확인하려면 `.bytes` 헤더의 `ContentHash`를 `manifest.json`의 `sourceManifest.contentHash`와 비교한다. 헤더 구조는 `WorldHistoryBakeCodec`에 있고, 순서는 다음과 같다.

```text
magic "UPWH"(u32) / FormatVersion(i32) / RecordMode(i32) / Seed(u64)
/ ContentHash(string) / BalanceVersion(i32) / BalanceContentHash(string)
```

문자열은 `BinaryWriter.Write(string)` 규칙(7비트 길이 접두사 + UTF-8)을 따른다.

## 굽는 방법

1. `Baseball/툴 런처` → **데이터 → World History Bake** 실행.
2. 산출물은 `Assets/10.Datas/HistoricalSimulation/BakedWorldHistory/`에 생기고, 같은 위치의 `BakedWorldHistoryCatalog.asset`이 `NewGameDefinition`에 자동 연결된다.
3. 이 경로는 역사 원본과 함께 `.gitignore` 대상이다. **파생 산출물이므로 커밋하지 않으며, 각자 로컬에서 굽는다.** 저장소를 새로 받은 사람은 한 번 구워야 한다.

Editor를 띄우지 않고 굽는 배치모드 경로도 있다. Editor가 프로젝트를 잠그고 있으면 실행되지 않으므로 먼저 닫는다.

```text
"C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe" -batchmode -nographics ^
  -projectPath "<프로젝트 경로>" ^
  -executeMethod Baseball.Editor.HistoricalDatabase.WorldHistoryBakeTool.BakeFromCommandLine ^
  -logFile bake.log
```

`-careerWorldSeeds 20260905,19820327,...`를 덧붙이면 Pool을 먼저 확정한 뒤 굽는다. 생략하면 `NewGameDefinition`에 저장된 Pool을 그대로 쓴다.

한 Seed를 굽는 데 걸리는 시간은 이 프로젝트 실측으로 **65~87초**다(Unity Mono 약 172경기/초, 시즌 44개 약 1만 5천 경기). 구단주 Seed 1개 + 커리어 Pool 개수만큼 구우므로, Pool이 4개면 5개를 굽고 약 6~7분이 든다. 산출물은 Seed당 약 630KB다.

## 두 개의 Seed — 반드시 구분할 것

커리어 새 게임은 **서로 다른 두 Seed**를 갖는다. 이 둘을 합치면 안 된다.

| Seed | 무엇을 정하는가 | Bake 대상 |
| --- | --- | --- |
| `NewGameFlowState.RandomSeed` | 커리어 진행 전체 — 리그 시드, 일정, 계약 오퍼 RNG, 성장 | **아니오.** 매 플레이스루마다 새로 뽑는다 |
| `NewGameFlowState.WorldHistorySeed` | 44시즌 배경 역사와 표시 이름(`WorldIdentityGenerator`)만 | 예 |

`RandomSeed`까지 Pool에 묶으면 같은 선수 빌드에서 **커리어 전체가 Pool 크기만큼의 경우의 수로 줄어든다.** 오퍼 구성도, 일정도, 성장도 몇 가지 변주로 고정된다. 배경 역사를 미리 구우려다 게임의 다양성을 버리는 셈이므로, 분리는 타협 대상이 아니다.

구단주 모드의 월드 Seed는 `NewGameDefinition._ownerWorldSeed`로 고정이라 한 번 구우면 항상 적중한다.

커리어의 `WorldHistorySeed`는 `NewGameDefinition._careerWorldSeedPool`에서 고른다.

- Pool이 **비어 있으면** 커리어 Seed를 그대로 써서 매번 새 월드를 시뮬레이션한다(기본값, 동작 변화 없음).
- Pool에 Seed를 넣으면 그중 하나를 골라 쓴다. **배경 역사만** Pool 크기만큼으로 제한되고, 커리어 진행의 다양성은 그대로다.

Pool 크기는 "44시즌 배경 역사와 선수 이름이 플레이스루마다 달라야 하는가"에 대한 답이다. 게임 디자인 결정이므로 기본값을 비워 두었다.

## 워밍업 — Boot → Loading 구간에서 미리 만든다

Bake를 써도 남는 비용(23.6MB 파싱, Identity 생성, 카드 카탈로그)이 있고, 그것을 버튼 누른 뒤에 내면 여전히 화면이 멈춘다. 그래서 **로딩 화면이 도는 동안** 미리 만든다.

```text
Boot   BootSceneController.Start()
         → HistoricalWarmupManager.BeginWarmup()
              메인 스레드: TextAsset 바이트 확보 (TextAsset은 메인 스레드 전용)
              워커 스레드: Content 파싱 → 구단주 World → 커리어 Content
         → SceneLoadManager.LoadScene(..., LoadingScreen)

Load   LoadingSceneController
         진행률 = min(Scene Load, 워밍업)
         둘 다 끝나야 대상 Scene을 활성화한다

Title  두 모드 모두 이미 만들어 둔 결과를 그대로 쓴다
```

- 파싱과 World 생성이 워커 스레드에서 도는 것은 **Core/Simulation/Game이 Unity에 의존하지 않기 때문**이다. 이 경계가 실제로 값을 만들어 내는 지점이므로 깨뜨리지 않는다.
- 미리 만든 World를 진입점이 그대로 쓰려면 Builder 인스턴스가 유지되어야 한다. `HistoricalWorldRuntimeBuilder.GetOrBuild()`가 `(Content, RecordMode, Seed)`로 메모이제이션하고, `OwnerModeManager`는 Builder를 하나만 들고 있는다.
- **Bake가 없으면 World 준비를 하지 않는다.** 그 경우 준비란 곧 44시즌을 실제로 돌린다는 뜻인데, 그것을 백그라운드에 띄우면 Editor에서 Domain Reload를 붙잡고 빌드에서도 로딩이 수십 초 길어진다. Bake Catalog가 없을 때는 콘텐츠 파싱까지만 미리 하고 World는 진입 시점에 만든다.
- **워밍업은 취소 가능해야 한다.** Play Mode 종료와 Domain Reload는 모두 `OnDisable`을 지나므로 거기서 취소를 알리고 최대 2초만 기다린다. 취소가 실제로 먹히도록 `HistoricalWorldRuntimeBuilder`의 시즌 루프가 `CancellationToken`을 확인한다 — 워커가 44시즌 중간에 멈출 수 있어야 Editor가 멈추지 않는다.
- **워밍업은 실패해도 게임을 막지 않는다.** 실패하면 경고를 남기고 넘어가며, 각 진입점은 캐시가 없을 때 스스로 만드는 경로를 그대로 갖고 있다. 느려질 뿐이다.
- 타이틀로 돌아갔다가 다시 시작하면 새 커리어 Seed를 뽑는다. `WorldHistorySeed`가 Pool의 다른 항목으로 바뀌면 그 월드는 다시 만들어야 하는데, Bake가 있으면 복원이라 비용이 작다.

## 검증

- `WorldHistoryBakeCodecTests` — 이진 왕복, 결정론적 인코딩, 손상 감지 (헤드리스).
- `HistoricalWorldRuntimeBuilderTests`의 `BakedHistory_*` — 구운 결과가 실제 시뮬레이션과 같은 값을 복원하는지, Seed·Balance가 어긋나면 Bake를 무시하는지 (헤드리스).

두 스위트 모두 `dotnet run --project Tools/HeadlessRegression/EditModeTestRunner/EditModeTestRunner.csproj -c Release`로 돈다.

## 함께 적용한 시작 비용 절감

Bake만으로는 남는 비용이 있어 같은 작업에서 정리했다.

1. **연도 콘텐츠 지연 파싱** — `Assets/10.Datas/HistoricalSimulation/`의 JSON은 46개 23.6MB다. `HistoricalBakedContent`가 `IHistoricalYearContentSource`를 통해 **요청받은 연도만** 파싱하고, 전체를 훑는 집계·역참조 검증은 그것을 요구하는 첫 호출에서 한 번에 수행한다.
2. **월드 파생물 지연 생성** — `WorldCardCatalog`와 `SpecialCompositeTeams`는 실제로 읽힐 때 만든다. 구단주 모드 시작은 시작 연도 하나만 필요하므로 `GetSpecialCompositeTeamSet(year)`로 그 해만 만든다.
3. **Provider 공유** — `NewGameDefinition.CreateHistoricalContentProvider()`가 같은 Catalog에 같은 인스턴스를 돌려준다. 이전에는 구단주 모드와 선수 모드가 같은 23.6MB를 각자 한 번씩 파싱했다.
4. **런타임 검증 축소** — `HistoricalContentVerificationMode.Fast`(런타임 기본)는 스키마·버전·연도별 개수만 본다. 파일별 SHA-256, Archive Hash, Runtime 안전 필드 스캔은 **저작 무결성 검사**이므로 `Full` 모드(저작 도구·검증 테스트)에서만 수행한다.

`Fast`가 건너뛰는 검증은 반드시 `Full`을 쓰는 경로가 대신 수행해야 한다. 새 저작 도구를 만들 때 `UnityHistoricalContentProvider`를 `Full`로 생성할 것.
