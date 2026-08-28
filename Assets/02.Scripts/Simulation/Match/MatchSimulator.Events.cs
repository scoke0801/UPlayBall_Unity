using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    public sealed partial class MatchSimulator
    {
        private static void Emit(
            MatchSimulationState state,
            MatchEventType eventType,
            int inning,
            InningHalf half,
            int batterId,
            int pitcherId,
            int playerId,
            PitchResult pitchResult,
            PlateAppearanceResult plateAppearanceResult,
            int fromBase,
            int toBase,
            int balls,
            int strikes,
            int outs)
        {
            var matchEvent = new MatchEvent(
                state.NextEventSequence++,
                eventType,
                inning,
                half,
                batterId,
                pitcherId,
                playerId,
                pitchResult,
                plateAppearanceResult,
                fromBase,
                toBase,
                balls,
                strikes,
                outs,
                state.Away.BoxScore.Runs,
                state.Home.BoxScore.Runs);
            state.EventSink.Record(matchEvent);
        }

        private sealed class MatchSimulationState
        {
            public MatchSimulationState(MatchInput input, IMatchEventSink eventSink)
            {
                Away = new TeamMatchState(input.AwayTeam);
                Home = new TeamMatchState(input.HomeTeam);
                EventSink = eventSink;
            }

            public TeamMatchState Away { get; }
            public TeamMatchState Home { get; }
            public IMatchEventSink EventSink { get; }
            public int NextEventSequence { get; set; }
            public int NextDecisionIndex { get; set; }
        }

        private sealed class TeamMatchState
        {
            public TeamMatchState(Team team)
            {
                Team = team;
                BoxScore = new TeamBoxScoreBuilder(team, BaseballRules.MaximumInnings);
                DefenseRating = team.Lineup.CalculateDefenseRating();
                ActivePitcher = team.StartingPitcher;
                ActivePitchingLine = BoxScore.PitchingLine;
            }

            public Team Team { get; }
            public TeamBoxScoreBuilder BoxScore { get; }
            public double DefenseRating { get; }
            public int NextBattingOrderIndex { get; set; }
            public Player ActivePitcher { get; private set; }
            public PlayerPitchingLine ActivePitchingLine { get; private set; }

            /// <summary>
            /// 이닝 시작 시 팀의 투수 운용 계약에 맞춰 현재 투수를 고정한다.
            /// </summary>
            public void SelectPitcher(int inning)
            {
                ActivePitcher = Team.GetPitcherForInning(inning);
                ActivePitchingLine = BoxScore.GetPitchingLine(ActivePitcher.PlayerId);
            }
        }

        private readonly struct LineupSlotReference
        {
            public LineupSlotReference(Player player, int battingOrderIndex)
            {
                Player = player;
                BattingOrderIndex = battingOrderIndex;
            }

            public Player Player { get; }
            public int BattingOrderIndex { get; }
        }

        private readonly struct BaseRunner
        {
            public BaseRunner(Player player, int battingOrderIndex)
            {
                Player = player;
                BattingOrderIndex = battingOrderIndex;
            }

            public Player Player { get; }
            public int BattingOrderIndex { get; }
            public bool IsOccupied => Player != null;
        }

        private sealed class BaseState
        {
            public BaseRunner First { get; set; }
            public BaseRunner Second { get; set; }
            public BaseRunner Third { get; set; }

            public void Clear()
            {
                First = default;
                Second = default;
                Third = default;
            }
        }
    }
}
