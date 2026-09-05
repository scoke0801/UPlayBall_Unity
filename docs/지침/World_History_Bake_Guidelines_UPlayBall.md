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

## Bake Key — 밸런스를 바꿨으면 반드시 다시 구울 것

`BakedWorldHistoryKey`는 다음을 모두 포함한다.

| 항목 | 이유 |
| --- | --- |
| `RecordMode` | OriginalHistory는 Bake 대상이 아니다 |
| `WorldHistorySeed` | Seed가 다르면 다른 월드다 |
| `ContentHash` | 역사 원본 Bake가 바뀌면 경기 입력이 바뀐다 |
| `BalanceVersion` + `BalanceContentHash` | **경기 결과를 좌우한다** |

**Key가 하나라도 어긋나면 Bake를 조용히 무시하고 실제 시뮬레이션으로 되돌아간다.** 즉 다시 굽는 것을 잊어도 결과가 틀리지는 않고 **느려질 뿐**이다. 반대로 말하면, 새 게임이 갑자기 다시 느려졌다면 Key가 깨진 것이므로 다시 구우면 된다.

`BalanceTable`의 `Version`/`ContentHash`를 올리지 않고 경기 계수만 바꾸면 Key가 그대로라 낡은 Bake가 적중한다. **경기 결과에 영향을 주는 밸런스를 바꿀 때는 `BalanceTable.Version`이나 `ContentHash`를 함께 올린다.**

## 굽는 방법

1. `Baseball/툴 런처` → **데이터 → World History Bake** 실행.
2. 산출물은 `Assets/10.Datas/HistoricalSimulation/BakedWorldHistory/`에 생기고, 같은 위치의 `BakedWorldHistoryCatalog.asset`이 `NewGameDefinition`에 자동 연결된다.
3. 이 경로는 역사 원본과 함께 `.gitignore` 대상이다. **파생 산출물이므로 커밋하지 않으며, 각자 로컬에서 굽는다.**

## 커리어 모드의 Seed 정책 — 판단이 남아 있는 부분

구단주 모드의 월드 Seed는 `NewGameDefinition._ownerWorldSeed`로 고정이라 한 번 구우면 항상 적중한다.

반면 **커리어 모드는 `DateTime.UtcNow.Ticks`로 매 새 게임마다 다른 Seed를 뽑는다.** 그래서 Bake가 원리적으로 적중할 수 없다. 이를 위해 `NewGameDefinition._careerWorldSeedPool`을 두었다.

- Pool이 **비어 있으면** 지금까지와 똑같이 매번 새 월드를 시뮬레이션한다(기본값, 동작 변화 없음).
- Pool에 Seed를 넣으면 커리어 새 게임이 그중 하나를 골라 쓴다. **Pool 크기만큼의 월드 다양성**을 유지하면서 전부 미리 구울 수 있다.

여기서 정해야 할 것은 "44시즌 배경 역사가 플레이스루마다 달라야 하는가"이다. Seed는 배경 역사뿐 아니라 `WorldIdentityGenerator`(선수·프랜차이즈 표시 이름)도 좌우하므로, Pool 크기가 곧 플레이어가 만날 수 있는 서로 다른 세계의 수가 된다. 이 값은 게임 디자인 결정이므로 기본값을 비워 두었다.

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
