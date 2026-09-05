using System;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using BaseballPlayer = Baseball.Core.Players.Player;

namespace Baseball.Presentation.Match
{
    /// <summary>구단주 경기를 감독 AI로 확정하고 그 이벤트를 관전용으로 재생한다.</summary>
    public sealed class OwnerMatchSpectatorSession : IOwnerMatchOverlay
    {
        private const string SpectatorPermissionMessage =
            "경기 운영은 감독 AI가 담당합니다. 구단주는 관전 속도만 조절할 수 있습니다.";

        private readonly MatchEvent[] _events;
        private readonly MatchHudPresentationModelBuilder _hudBuilder = new MatchHudPresentationModelBuilder();
        private readonly IMatchHudView _hudView;
        private int _visibleEventCount;
        private bool _isPaused;
        private OwnerMatchPlaybackSpeed _speed = OwnerMatchPlaybackSpeed.Normal;

        private OwnerMatchSpectatorSession(
            ManagerModeMatchResult result,
            MatchEvent[] events,
            IMatchHudView hudView)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            if (_events.Length == 0)
                throw new InvalidOperationException("구단주 관전 경기의 이벤트 스트림이 비어 있습니다.");

            _hudView = hudView;
            CurrentHud = BuildHud();
            _hudView?.Present(CurrentHud);
        }

        public ManagerModeMatchResult Result { get; }
        public MatchHudPresentationModel CurrentHud { get; private set; }
        public OwnerMatchOverlayState State => new OwnerMatchOverlayState(
            _visibleEventCount,
            _events.Length,
            _isPaused,
            _speed,
            SpectatorPermissionMessage);

        /// <summary>실시간 명령 없이 감독 AI가 경기 전체를 확정하고 Full 이벤트를 관전 세션에 연결한다.</summary>
        public static OwnerMatchSpectatorSession PlayNextGame(
            OwnerModeManager manager,
            IMatchHudView hudView = null)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            var eventBuffer = new MatchEventBuffer();
            ManagerModeMatchResult result = manager.PlayNextGame(
                eventBuffer,
                CreateSpectatorExecutionProfile());
            return new OwnerMatchSpectatorSession(result, eventBuffer.ToArray(), hudView);
        }

        public bool TryTogglePause()
        {
            if (!State.CanTogglePause)
                return false;

            _isPaused = !_isPaused;
            return true;
        }

        public bool TrySetPlaybackSpeed(OwnerMatchPlaybackSpeed speed)
        {
            if (!State.CanChangeSpeed || !Enum.IsDefined(typeof(OwnerMatchPlaybackSpeed), speed))
                return false;

            _speed = speed;
            return true;
        }

        /// <summary>다음 타석 또는 공수 교대까지 이미 확정된 이벤트만 공개한다.</summary>
        public bool TryAdvance()
        {
            if (!State.CanAdvance)
                return false;

            while (_visibleEventCount < _events.Length)
            {
                MatchEvent matchEvent = _events[_visibleEventCount++];
                if (matchEvent.EventType is MatchEventType.PlateAppearanceEnded or
                    MatchEventType.HalfInningEnded or MatchEventType.MatchEnded)
                {
                    break;
                }
            }

            PresentCurrentHud();
            return true;
        }

        public bool TryRevealAll()
        {
            if (!State.CanAdvance)
                return false;

            _visibleEventCount = _events.Length;
            _isPaused = false;
            PresentCurrentHud();
            return true;
        }

        private static MatchExecutionProfile CreateSpectatorExecutionProfile()
        {
            return new MatchExecutionProfile(
                SimulationEngineKind.Detailed,
                MatchDecisionMode.InternalAiOnly,
                MatchEventMode.Full,
                MatchDecisionTraceMode.None,
                MatchStatisticsMode.FullBoxScore);
        }

        private void PresentCurrentHud()
        {
            CurrentHud = BuildHud();
            _hudView?.Present(CurrentHud);
        }

        private MatchHudPresentationModel BuildHud()
        {
            MatchEvent latest = _events[_visibleEventCount == 0 ? 0 : _visibleEventCount - 1];
            int firstRunnerId = 0;
            int secondRunnerId = 0;
            int thirdRunnerId = 0;
            for (int index = 0; index < _visibleEventCount; index++)
            {
                MatchEvent matchEvent = _events[index];
                if (matchEvent.EventType == MatchEventType.RunnerAdvance)
                {
                    ClearRunner(
                        matchEvent.PlayerId,
                        matchEvent.FromBase,
                        ref firstRunnerId,
                        ref secondRunnerId,
                        ref thirdRunnerId);
                    PlaceRunner(
                        matchEvent.PlayerId,
                        matchEvent.ToBase,
                        ref firstRunnerId,
                        ref secondRunnerId,
                        ref thirdRunnerId);
                }
                else if (matchEvent.EventType == MatchEventType.Out)
                {
                    ClearRunnerById(
                        matchEvent.PlayerId,
                        ref firstRunnerId,
                        ref secondRunnerId,
                        ref thirdRunnerId);
                }
                else if (matchEvent.EventType == MatchEventType.HalfInningEnded)
                {
                    firstRunnerId = 0;
                    secondRunnerId = 0;
                    thirdRunnerId = 0;
                }
            }

            MatchInput input = Result.Match.Input;
            bool isTop = latest.Half == InningHalf.Top;
            bool isBetweenInnings = _visibleEventCount > 0 &&
                                    latest.EventType == MatchEventType.HalfInningEnded;
            return _hudBuilder.Build(
                Math.Max(1, latest.Inning),
                isTop ? MatchHudHalf.Top : MatchHudHalf.Bottom,
                new MatchHudTeamModel(input.AwayRoster.TeamName, latest.AwayScore, isTop),
                new MatchHudTeamModel(input.HomeRoster.TeamName, latest.HomeScore, !isTop),
                new MatchHudCountModel(latest.Balls, latest.Strikes, latest.Outs),
                new MatchHudBaseStateModel(
                    CreateParticipant(input, firstRunnerId),
                    CreateParticipant(input, secondRunnerId),
                    CreateParticipant(input, thirdRunnerId)),
                CreateParticipant(input, latest.BatterId),
                CreateParticipant(input, latest.PitcherId),
                isBetweenInnings);
        }

        private static MatchHudParticipantModel CreateParticipant(MatchInput input, int playerId)
        {
            if (playerId <= 0)
                return MatchHudParticipantModel.Empty;

            BaseballPlayer player = FindPlayer(input.AwayRoster, playerId) ?? FindPlayer(input.HomeRoster, playerId);
            return new MatchHudParticipantModel(playerId, player?.Name ?? string.Empty);
        }

        private static BaseballPlayer FindPlayer(MatchRosterSnapshot roster, int playerId)
        {
            for (int index = 0; index < roster.StartingLineup.Count; index++)
            {
                BaseballPlayer player = roster.StartingLineup[index].Player;
                if (player.PlayerId == playerId)
                    return player;
            }
            if (roster.StartingPitcher.Player.PlayerId == playerId)
                return roster.StartingPitcher.Player;
            for (int index = 0; index < roster.Bullpen.Count; index++)
            {
                BaseballPlayer player = roster.Bullpen[index].Player;
                if (player.PlayerId == playerId)
                    return player;
            }
            for (int index = 0; index < roster.Bench.Count; index++)
            {
                BaseballPlayer player = roster.Bench[index];
                if (player.PlayerId == playerId)
                    return player;
            }
            return null;
        }

        private static void ClearRunner(
            int playerId,
            int fromBase,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (fromBase == 1) firstRunnerId = 0;
            else if (fromBase == 2) secondRunnerId = 0;
            else if (fromBase == 3) thirdRunnerId = 0;
            else ClearRunnerById(playerId, ref firstRunnerId, ref secondRunnerId, ref thirdRunnerId);
        }

        private static void PlaceRunner(
            int playerId,
            int toBase,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (toBase == 1) firstRunnerId = playerId;
            else if (toBase == 2) secondRunnerId = playerId;
            else if (toBase == 3) thirdRunnerId = playerId;
        }

        private static void ClearRunnerById(
            int playerId,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (firstRunnerId == playerId) firstRunnerId = 0;
            if (secondRunnerId == playerId) secondRunnerId = 0;
            if (thirdRunnerId == playerId) thirdRunnerId = 0;
        }
    }
}
