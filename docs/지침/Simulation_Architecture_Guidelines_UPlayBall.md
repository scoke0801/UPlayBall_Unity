# 시뮬레이션 아키텍처 지침

> 출처: `BaseballManager_PROJECT.md` 26·27·34절 (2026-08-28 `docs/지침`로 이동)

## 어셈블리 레이어

Unity를 사용하되, 경기 시뮬레이션 Core는 Unity API 의존성을 최소화한다.

권장 구조:

```text
Baseball.Core
Baseball.Simulation
Baseball.Game
Baseball.Presentation
Baseball.Editor
```

Simulation 레이어에서는 가능하면 다음에 의존하지 않는다.

```text
MonoBehaviour
GameObject
Coroutine
Time
UnityEngine.Random
```

순수 C# 형태를 우선한다.

### 이유

- Unit Test가 쉬움
- 대량 시뮬레이션이 빠름
- 에디터 없이 테스트 가능
- 경기 로직과 화면 분리 가능
- 향후 서버 또는 별도 툴로 옮기기 쉬움

---

## 경기 시뮬레이션과 표현의 분리

경기 로직과 화면을 강하게 분리한다.

```text
MatchSimulator
      ↓
MatchEvent
      ↓
Presentation
```

예:

```text
MatchSimulator

→ PitchEvent
→ ContactEvent
→ HitEvent
→ RunnerAdvanceEvent
→ ScoreEvent
→ OutEvent
```

UI는 이벤트를 받아 화면에 표현한다.

이를 통해 다음 관전 모드가 모두 동일한 경기 시뮬레이션 코드를 사용할 수 있다.

```text
Instant Simulation
Fast Simulation
Full Presentation
```

---

## 결정론적 시뮬레이션

가능하면 경기마다 Seed를 저장한다.

```text
SeasonId
GameId
RandomSeed
```

동일한 Seed와 동일한 입력이면 동일한 경기 결과가 나오도록 설계한다.

장점:

- 버그 재현
- 밸런스 테스트
- 자동 테스트
- 경기 리플레이
- 시뮬레이션 분석

## 관련 문서

- [[Balance_Testing_Guidelines_UPlayBall]]
- [[Project_Principles_UPlayBall]]
