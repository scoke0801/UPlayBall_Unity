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
        private readonly int _playerTeamId;
        private int _visibleEventCount;
        private bool _isPaused;
        private OwnerMatchPlaybackSpeed _speed = OwnerMatchPlaybackSpeed.Normal;
        private OwnerMatchViewingMode _viewingMode = OwnerMatchViewingMode.EveryMoment;

        private OwnerMatchSpectatorSession(
            ManagerModeMatchResult result,
            MatchEvent[] events,
            IMatchHudView hudView,
            int playerTeamId)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            if (_events.Length == 0)
                throw new InvalidOperationException("구단주 관전 경기의 이벤트 스트림이 비어 있습니다.");

            _hudView = hudView;
            _playerTeamId = playerTeamId;
            CurrentHud = BuildHud();
            _hudView?.Present(CurrentHud);
        }

        public ManagerModeMatchResult Result { get; }
        public MatchHudPresentationModel CurrentHud { get; private set; }
        /// <summary>관전 화면은 이 경계 안의 이벤트만 읽어 최종 결과가 먼저 노출되지 않게 한다.</summary>
        public MatchEvent GetVisibleEvent(int index)
        {
            if (index < 0 || index >= _visibleEventCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _events[index];
        }

        /// <summary>현재 공개 구간에서만 이닝별 득점을 복원한다.</summary>
        public Baseball.Game.Career.MatchLineScore CreateVisibleLineScore()
        {
            return Baseball.Game.Career.MatchLineScore.Create(_events, _visibleEventCount);
        }

        /// <summary>경기에 등록된 선수의 표시 이름을 조회한다.</summary>
        public string GetParticipantName(int playerId)
        {
            return CreateParticipant(Result.Match.Input, playerId).Name;
        }

        /// <summary>판단 Trace의 Actor가 플레이어 구단 경기 로스터에 속하는지 확인한다.</summary>
        public bool IsPlayerTeamParticipant(int playerId)
        {
            MatchInput input = Result.Match.Input;
            MatchRosterSnapshot roster = input.AwayRoster.TeamId == _playerTeamId
                ? input.AwayRoster
                : input.HomeRoster;
            return FindPlayer(roster, playerId) != null;
        }

        /// <summary>현재 투수와 타자의 실제 투타 방향을 연출용으로 해석한다.</summary>
        public OwnerMatchHandedness GetHandedness(int pitcherId, int batterId)
        {
            MatchInput input = Result.Match.Input;
            BaseballPlayer pitcher = FindPlayer(input.AwayRoster, pitcherId) ?? FindPlayer(input.HomeRoster, pitcherId);
            BaseballPlayer batter = FindPlayer(input.AwayRoster, batterId) ?? FindPlayer(input.HomeRoster, batterId);
            var throwingHand = pitcher?.ThrowingHand ?? Baseball.Core.Players.Handedness.Right;
            var battingHand = batter?.BattingHand ?? Baseball.Core.Players.Handedness.Right;
            if (battingHand == Baseball.Core.Players.Handedness.Switch)
            {
                battingHand = throwingHand == Baseball.Core.Players.Handedness.Left
                    ? Baseball.Core.Players.Handedness.Right
                    : Baseball.Core.Players.Handedness.Left;
            }
            return new OwnerMatchHandedness(throwingHand, battingHand);
        }
        public OwnerMatchOverlayState State => new OwnerMatchOverlayState(
            _visibleEventCount,
            _events.Length,
            _isPaused,
            _speed,
            SpectatorPermissionMessage,
            _viewingMode);

        /// <summary>실시간 명령 없이 감독 AI가 경기 전체를 확정하고 Full 이벤트를 관전 세션에 연결한다.</summary>
        public static OwnerMatchSpectatorSession PlayNextGame(
            OwnerModeManager manager,
            IMatchHudView hudView = null)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            int playerTeamId = manager.Runtime?.ManagerMode.LiveSeason.PlayerTeamId ?? 0;
            var eventBuffer = new MatchEventBuffer();
            ManagerModeMatchResult result = manager.PlayNextGame(
                eventBuffer,
                CreateSpectatorExecutionProfile());
            return new OwnerMatchSpectatorSession(result, eventBuffer.ToArray(), hudView, playerTeamId);
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

        public bool TrySetViewingMode(OwnerMatchViewingMode mode)
        {
            if (!State.CanChangeViewingMode || !Enum.IsDefined(typeof(OwnerMatchViewingMode), mode))
                return false;

            _viewingMode = mode;
            _isPaused = false;
            if (mode == OwnerMatchViewingMode.ResultOnly)
                return TryRevealAll();

            return true;
        }

        /// <summary>선택한 관전 밀도의 다음 경계까지 이미 확정된 이벤트만 공개한다.</summary>
        public bool TryAdvance()
        {
            if (!State.CanAdvance)
                return false;

            while (_visibleEventCount < _events.Length)
            {
                MatchEvent matchEvent = _events[_visibleEventCount++];
                if (IsAdvanceBoundary(matchEvent))
                {
                    break;
                }
            }

            PresentCurrentHud();
            return true;
        }

        private bool IsAdvanceBoundary(in MatchEvent matchEvent)
        {
            if (matchEvent.EventType is MatchEventType.HalfInningEnded or MatchEventType.MatchEnded or
                MatchEventType.MatchEndedAsDraw)
                return true;

            if (_viewingMode == OwnerMatchViewingMode.EveryMoment)
                return matchEvent.EventType is MatchEventType.Pitch or MatchEventType.PlateAppearanceEnded;

            if (_viewingMode != OwnerMatchViewingMode.KeyMoments)
                return false;

            if (matchEvent.EventType is MatchEventType.HighLeverageSituationStarted or
                MatchEventType.PlayerSubstitution or MatchEventType.PitcherEntered or
                MatchEventType.PinchHitterEntered or MatchEventType.PinchRunnerEntered or
                MatchEventType.FieldingError or MatchEventType.ThrowingError)
                return true;

            return matchEvent.EventType == MatchEventType.PlateAppearanceEnded &&
                   IsKeyPlateAppearance(matchEvent.PlateAppearanceResult);
        }

        private static bool IsKeyPlateAppearance(Baseball.Simulation.PlateAppearance.PlateAppearanceResult result)
        {
            return result is Baseball.Simulation.PlateAppearance.PlateAppearanceResult.Single or
                Baseball.Simulation.PlateAppearance.PlateAppearanceResult.Double or
                Baseball.Simulation.PlateAppearance.PlateAppearanceResult.Triple or
                Baseball.Simulation.PlateAppearance.PlateAppearanceResult.HomeRun or
                Baseball.Simulation.PlateAppearance.PlateAppearanceResult.Strikeout or
                Baseball.Simulation.PlateAppearance.PlateAppearanceResult.ReachedOnError or
                Baseball.Simulation.PlateAppearance.PlateAppearanceResult.SacrificeBunt or
                Baseball.Simulation.PlateAppearance.PlateAppearanceResult.BuntSingle or
                Baseball.Simulation.PlateAppearance.PlateAppearanceResult.BuntPopOut;
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
                MatchDecisionTraceMode.Full,
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
                new MatchHudTeamModel(
                    FormatTeamDisplayName(input.AwayRoster.TeamName, input.AwayRoster.TeamId == _playerTeamId),
                    latest.AwayScore,
                    isTop),
                new MatchHudTeamModel(
                    FormatTeamDisplayName(input.HomeRoster.TeamName, input.HomeRoster.TeamId == _playerTeamId),
                    latest.HomeScore,
                    !isTop),
                new MatchHudCountModel(latest.Balls, latest.Strikes, latest.Outs),
                new MatchHudBaseStateModel(
                    CreateParticipant(input, firstRunnerId),
                    CreateParticipant(input, secondRunnerId),
                    CreateParticipant(input, thirdRunnerId)),
                CreateParticipant(input, latest.BatterId),
                CreateParticipant(input, latest.PitcherId),
                isBetweenInnings);
        }

        /// <summary>구단명이 비어 있을 때만 역할 기반 이름으로 대체한다.</summary>
        public static string FormatTeamDisplayName(string teamName, bool isPlayerTeam)
        {
            if (!string.IsNullOrWhiteSpace(teamName))
                return teamName.Trim();

            return isPlayerTeam ? "우리 구단" : "상대 구단";
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
