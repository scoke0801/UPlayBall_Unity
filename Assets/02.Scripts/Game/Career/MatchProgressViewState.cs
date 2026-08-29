using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 경기 진행 화면 전체가 공유하는 흐름 단계다. 각 패널이 이닝·아웃·현재 타자를 따로 판단하면
    /// 서로 다른 상태를 표시하므로, 화면 전환의 기준은 이 값 하나로 통일한다.
    /// </summary>
    public enum MatchFlowState
    {
        AutoRunning = 0,
        Paused = 1,
        SideChange = 2,
        PlayerCallUp = 3,
        PlayerAtBat = 4,
        PlayerAtBatResult = 5,
        PlayerSubstitutedOut = 6,
        GameEnded = 7
    }

    /// <summary>
    /// 내 선수가 지금 경기에서 어떤 위치에 있는지를 나타낸다.
    /// </summary>
    public enum PlayerMatchState
    {
        NotPlaying = 0,
        Bench = 1,
        StarterWaiting = 2,
        OnDeck = 3,
        AtBat = 4,
        OnBase = 5,
        Fielding = 6,
        SubstitutedOut = 7
    }

    /// <summary>
    /// 화면에서 항상 하나만 강조되는 주 행동이다.
    /// </summary>
    public enum MatchPrimaryAction
    {
        Pause = 0,
        AdvanceToPlayerEntry = 1,
        AdvanceToPlayerAtBat = 2,
        EnterPlateAppearance = 3,
        NextPitch = 4,
        ContinueMatch = 5,
        FinishMatch = 6,
        ViewResult = 7
    }

    /// <summary>
    /// 흐름 판정에 필요한 진행 제어 값을 모은다.
    /// </summary>
    public struct MatchProgressFlowContext
    {
        public CareerMatchPhase Phase;
        public bool IsDecisionInputReady;
        public bool HasControlledResult;
        public bool IsAutomaticPlaybackActive;
        public bool IsPaused;
        public bool IsCallUpAcknowledged;
    }

    /// <summary>
    /// 흐름 판정에 필요한 내 선수 정보를 모은다.
    /// </summary>
    public struct MatchProgressPlayerContext
    {
        public int ControlledPlayerId;
        public PlayerGameRole Role;
        public PlayerPosition Position;
        public bool IsPlayerTeamHome;
        public bool CanReceiveBattingDecisions;
    }

    /// <summary>
    /// 공개된 이벤트와 진행 제어 상태를 합쳐 화면 전체가 함께 쓰는 표시 상태를 만든다.
    /// </summary>
    public readonly struct MatchProgressViewState
    {
        private MatchProgressViewState(
            MatchFlowState flow,
            PlayerMatchState playerState,
            MatchPrimaryAction primaryAction,
            bool isAwayTeamBatting,
            bool isPlayerTeamBatting,
            bool hasPlayerEnteredGame,
            int plateAppearancesUntilPlayerAtBat)
        {
            Flow = flow;
            PlayerState = playerState;
            PrimaryAction = primaryAction;
            IsAwayTeamBatting = isAwayTeamBatting;
            IsPlayerTeamBatting = isPlayerTeamBatting;
            HasPlayerEnteredGame = hasPlayerEnteredGame;
            PlateAppearancesUntilPlayerAtBat = plateAppearancesUntilPlayerAtBat;
        }

        public MatchFlowState Flow { get; }
        public PlayerMatchState PlayerState { get; }
        public MatchPrimaryAction PrimaryAction { get; }
        public bool IsAwayTeamBatting { get; }
        public bool IsPlayerTeamBatting { get; }
        public bool HasPlayerEnteredGame { get; }

        /// <summary>
        /// 내 선수의 다음 타석까지 남은 타석 수다. 아직 알 수 없으면 -1이다.
        /// </summary>
        public int PlateAppearancesUntilPlayerAtBat { get; }

        /// <summary>
        /// 중앙 무대가 자동 중계 화면 대신 전용 연출로 전환되어야 하는 흐름인지 반환한다.
        /// </summary>
        public bool IsStageTakeover =>
            Flow is MatchFlowState.SideChange or MatchFlowState.PlayerCallUp or
                MatchFlowState.PlayerAtBat or MatchFlowState.PlayerAtBatResult;

        /// <summary>
        /// 자동 진행 제어와 배속 조작을 감춰야 하는 흐름인지 반환한다.
        /// 내 선수 타석에서는 주 버튼이 다음 투구 하나만 남아야 한다.
        /// </summary>
        public bool IsPlaybackControlHidden =>
            Flow is MatchFlowState.PlayerAtBat or MatchFlowState.PlayerCallUp or MatchFlowState.GameEnded;

        /// <summary>
        /// 공개된 이벤트와 진행 제어 상태로 화면 표시 상태를 만든다.
        /// </summary>
        public static MatchProgressViewState Create(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            CareerMatchPlaybackSnapshot snapshot,
            MatchProgressPlayerContext player,
            MatchProgressFlowContext flow)
        {
            bool isAwayBatting = snapshot.Half == InningHalf.Top;
            bool isPlayerTeamBatting = player.IsPlayerTeamHome != isAwayBatting;
            bool hasEntered = player.Role == PlayerGameRole.StartingBatter ||
                              HasSubstitutionIn(events, visibleEventCount, player.ControlledPlayerId);
            bool isSubstitutedOut =
                HasSubstitutionOut(events, visibleEventCount, player.ControlledPlayerId);
            bool isOnBase = player.ControlledPlayerId != 0 &&
                            (snapshot.FirstRunnerId == player.ControlledPlayerId ||
                             snapshot.SecondRunnerId == player.ControlledPlayerId ||
                             snapshot.ThirdRunnerId == player.ControlledPlayerId);
            int remainingPlateAppearances = hasEntered && !isSubstitutedOut
                ? CountPlateAppearancesUntilPlayer(events, visibleEventCount, player.ControlledPlayerId)
                : -1;

            MatchFlowState flowState = ResolveFlow(
                events,
                visibleEventCount,
                player.ControlledPlayerId,
                isSubstitutedOut,
                flow);
            PlayerMatchState playerState = ResolvePlayerState(
                flowState,
                player,
                isPlayerTeamBatting,
                hasEntered,
                isSubstitutedOut,
                isOnBase,
                remainingPlateAppearances);

            return new MatchProgressViewState(
                flowState,
                playerState,
                ResolvePrimaryAction(flowState, playerState, player),
                isAwayBatting,
                isPlayerTeamBatting,
                hasEntered,
                remainingPlateAppearances);
        }

        private static MatchFlowState ResolveFlow(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            int controlledPlayerId,
            bool isSubstitutedOut,
            MatchProgressFlowContext flow)
        {
            if (flow.Phase == CareerMatchPhase.Completed && !flow.IsAutomaticPlaybackActive)
                return MatchFlowState.GameEnded;
            if (flow.HasControlledResult)
                return MatchFlowState.PlayerAtBatResult;

            bool isJustCalledUp = IsLatestEventCallUp(events, visibleEventCount, controlledPlayerId);
            if (isJustCalledUp && !flow.IsCallUpAcknowledged)
                return MatchFlowState.PlayerCallUp;
            if (flow.IsDecisionInputReady)
                return MatchFlowState.PlayerAtBat;
            if (IsLatestEventType(events, visibleEventCount, MatchEventType.HalfInningEnded))
                return MatchFlowState.SideChange;
            if (isSubstitutedOut && flow.IsPaused)
                return MatchFlowState.PlayerSubstitutedOut;
            if (flow.IsPaused || !flow.IsAutomaticPlaybackActive)
                return MatchFlowState.Paused;
            return MatchFlowState.AutoRunning;
        }

        private static PlayerMatchState ResolvePlayerState(
            MatchFlowState flow,
            MatchProgressPlayerContext player,
            bool isPlayerTeamBatting,
            bool hasEntered,
            bool isSubstitutedOut,
            bool isOnBase,
            int remainingPlateAppearances)
        {
            if (flow is MatchFlowState.PlayerAtBat or MatchFlowState.PlayerAtBatResult or
                MatchFlowState.PlayerCallUp)
            {
                return PlayerMatchState.AtBat;
            }
            if (isSubstitutedOut)
                return PlayerMatchState.SubstitutedOut;
            if (isOnBase)
                return PlayerMatchState.OnBase;
            if (!hasEntered)
                return player.Role == PlayerGameRole.Bench
                    ? PlayerMatchState.Bench
                    : PlayerMatchState.NotPlaying;
            if (!isPlayerTeamBatting)
                return PlayerMatchState.Fielding;
            return remainingPlateAppearances == 0
                ? PlayerMatchState.OnDeck
                : PlayerMatchState.StarterWaiting;
        }

        private static MatchPrimaryAction ResolvePrimaryAction(
            MatchFlowState flow,
            PlayerMatchState playerState,
            MatchProgressPlayerContext player)
        {
            switch (flow)
            {
                case MatchFlowState.GameEnded:
                    return MatchPrimaryAction.ViewResult;
                case MatchFlowState.PlayerAtBat:
                    return MatchPrimaryAction.NextPitch;
                case MatchFlowState.PlayerCallUp:
                    return MatchPrimaryAction.EnterPlateAppearance;
                case MatchFlowState.PlayerAtBatResult:
                    return MatchPrimaryAction.ContinueMatch;
                case MatchFlowState.PlayerSubstitutedOut:
                    return MatchPrimaryAction.FinishMatch;
                case MatchFlowState.AutoRunning:
                case MatchFlowState.SideChange:
                    return MatchPrimaryAction.Pause;
            }

            if (!player.CanReceiveBattingDecisions)
                return MatchPrimaryAction.FinishMatch;
            return playerState switch
            {
                PlayerMatchState.Bench => MatchPrimaryAction.AdvanceToPlayerEntry,
                PlayerMatchState.SubstitutedOut or PlayerMatchState.NotPlaying =>
                    MatchPrimaryAction.FinishMatch,
                _ => MatchPrimaryAction.AdvanceToPlayerAtBat
            };
        }

        private static bool IsLatestEventType(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            MatchEventType eventType)
        {
            return visibleEventCount > 0 && events[visibleEventCount - 1].EventType == eventType;
        }

        private static bool IsLatestEventCallUp(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            int controlledPlayerId)
        {
            if (visibleEventCount == 0 || controlledPlayerId == 0)
                return false;

            MatchEvent latest = events[visibleEventCount - 1];
            return latest.EventType == MatchEventType.PlayerSubstitution &&
                   latest.BatterId == controlledPlayerId;
        }

        private static bool HasSubstitutionIn(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            int controlledPlayerId)
        {
            for (int index = 0; index < visibleEventCount; index++)
            {
                if (events[index].EventType == MatchEventType.PlayerSubstitution &&
                    events[index].BatterId == controlledPlayerId)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasSubstitutionOut(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            int controlledPlayerId)
        {
            for (int index = 0; index < visibleEventCount; index++)
            {
                if (events[index].EventType == MatchEventType.PlayerSubstitution &&
                    events[index].PlayerId == controlledPlayerId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 아직 공개하지 않은 이벤트를 훑어 내 선수의 다음 타석까지 남은 타석 수를 센다.
        /// 타순은 이미 확정된 정보이므로 결과를 미리 보여 주지 않으면서 대기 순번만 알 수 있다.
        /// </summary>
        private static int CountPlateAppearancesUntilPlayer(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            int controlledPlayerId)
        {
            int count = 0;
            for (int index = visibleEventCount; index < events.Count; index++)
            {
                MatchEvent matchEvent = events[index];
                if (matchEvent.EventType != MatchEventType.PlateAppearanceEnded)
                {
                    if (matchEvent.EventType == MatchEventType.Pitch &&
                        matchEvent.BatterId == controlledPlayerId)
                    {
                        return count;
                    }
                    continue;
                }

                if (matchEvent.BatterId == controlledPlayerId)
                    return count;
                count++;
            }
            return -1;
        }
    }
}
