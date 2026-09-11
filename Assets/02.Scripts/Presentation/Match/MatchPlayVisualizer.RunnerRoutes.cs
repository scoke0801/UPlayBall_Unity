using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;

namespace Baseball.Presentation.Match
{
    public sealed partial class MatchPlayVisualizer
    {
        private readonly OwnerMatchRunnerRoute[] _runnerRoutes = new OwnerMatchRunnerRoute[3];
        private readonly float[] _routeProgress = new float[3];
        private readonly float[] _routeEventStart = new float[3];
        private int _runnerRouteCount;
        private float _batterEventStart;

        /// <summary>같은 타구의 공식 진루 경로를 고정 버퍼에 복사하며 공개 베이스 상태는 바꾸지 않는다.</summary>
        public void PrepareRunnerRoutes(OwnerMatchRunnerRoute[] routes, int count)
        {
            _runnerRouteCount = Mathf.Clamp(count, 0, Mathf.Min(routes.Length, _runnerRoutes.Length));
            for (int index = 0; index < _runnerRouteCount; index++)
            {
                _runnerRoutes[index] = routes[index];
                _routeProgress[index] = _routeEventStart[index] = 0;
            }
        }

        private void RenderUpcomingRunners(float progress)
        {
            bool isRunnerEvent = _event.EventType is MatchEventType.RunnerAdvance or MatchEventType.RunnerThrownOut or MatchEventType.Out;
            bool isContact = _event.EventType == MatchEventType.Contact;
            bool isMovementWindow = isRunnerEvent || _event.EventType == MatchEventType.Hit;
            var layout = _spriteStage.Projection.Layout;
            if (_isBatterApproaching && isMovementWindow && (!isRunnerEvent || _event.PlayerId != _batterId))
            {
                _batterApproachProgress = Mathf.Lerp(_batterEventStart, Mathf.Clamp(layout.runnerPendingProgress, 0f, 0.95f), progress);
                _spriteStage.RenderRunner(Register(_batterId, 0), 0, 1, _batterApproachProgress);
            }
            for (int index = 0; index < _runnerRouteCount; index++)
            {
                OwnerMatchRunnerRoute route = _runnerRoutes[index];
                if (route.PlayerId <= 0) continue;
                if (isRunnerEvent && route.PlayerId == _event.PlayerId)
                {
                    if (progress >= 1f) _runnerRoutes[index] = default;
                    continue;
                }
                if (isContact)
                {
                    // 포구되는 공중 타구에서는 태그업 전 출발을 지어내지 않는다.
                    bool waitsForCatch = _play.Fielding.HasValue && _play.Fielding.Result is
                        PlateAppearanceResult.FlyOut or PlateAppearanceResult.BuntPopOut;
                    _routeProgress[index] = waitsForCatch ? 0f : Mathf.Clamp(layout.batterRunLeadProgress, 0, 0.95f) *
                        Mathf.InverseLerp(layout.batterRunStartProgress, 1, progress);
                }
                else if (isMovementWindow)
                {
                    // 아직 자기 판정 차례가 아닌 주자는 목적 베이스 직전까지만 함께 움직인다.
                    _routeProgress[index] = Mathf.Lerp(_routeEventStart[index],
                        Mathf.Clamp(layout.runnerPendingProgress, 0f, 0.95f), progress);
                }
            }
            RestoreUpcomingRunners();
        }

        private void RestoreUpcomingRunners()
        {
            if (_spriteStage == null || _field.gameObject.activeSelf) return;
            for (int index = 0; index < _runnerRouteCount; index++)
            {
                OwnerMatchRunnerRoute route = _runnerRoutes[index];
                if (route.PlayerId <= 0 || _routeProgress[index] <= 0) continue;
                bool isOwnEvent = _event.PlayerId == route.PlayerId && _event.EventType is
                    MatchEventType.RunnerAdvance or MatchEventType.RunnerThrownOut or MatchEventType.Out;
                if (isOwnEvent) continue;
                int slot = Register(route.PlayerId, route.FromBase);
                _spriteStage.RenderRunner(slot, route.FromBase, route.FromBase + 1, _routeProgress[index]);
            }
        }
    }
}
