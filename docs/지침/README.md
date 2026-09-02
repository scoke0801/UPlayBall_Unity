# 지침 목차

이 폴더는 `BaseballManager_PROJECT.md`(기획 문서)에서 분리한 **개발/작업 지침**과, UI 제작 전용 지침을 모아둔다. 기획·게임 시스템 세부 내용은 계속 `BaseballManager_PROJECT.md`가 기준 문서다.

**레퍼런스:** 엔트리브소프트의 `프로야구매니저`(약칭 `프야매`)를 핵심 레퍼런스로 삼는다. 새 기능을 구현할 때는 프야매가 해당 기능을 어떻게 다루는지 먼저 웹에서 조사하고, 이 프로젝트의 범위(`BaseballManager_PROJECT.md` 31절 MVP 제외 목록, [[Project_Principles_UPlayBall]] 7원칙)에 맞게 취사선택해 설계·구현한다.

- [Simulation_Architecture_Guidelines_UPlayBall.md](Simulation_Architecture_Guidelines_UPlayBall.md) — 어셈블리 레이어 분리, 시뮬레이션/표현 분리, 결정론적 시뮬레이션
- [Balance_Testing_Guidelines_UPlayBall.md](Balance_Testing_Guidelines_UPlayBall.md) — 대량 시뮬레이션 밸런스 테스트 도구 지침
- [Headless_Regression_Guidelines_UPlayBall.md](Headless_Regression_Guidelines_UPlayBall.md) — Unity 밖 .NET Release 장기 회귀 실행 구조
- [Project_Principles_UPlayBall.md](Project_Principles_UPlayBall.md) — 프로젝트 7대 원칙
- [Unity_UI_Production_Guidelines_UPlayBall.md](Unity_UI_Production_Guidelines_UPlayBall.md) — Unity UI 제작 지침
