using System.Runtime.CompilerServices;

// Game 레이어를 Unity 비의존 어셈블리로 분리하면서, 같은 어셈블리에 있던 시절의 internal 접근
// 계약(뷰 조립용 internal setter 등)을 바로 위 Unity 어댑터 레이어에만 그대로 유지한다.
[assembly: InternalsVisibleTo("Baseball.Game.Unity")]
