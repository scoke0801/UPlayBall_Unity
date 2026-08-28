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
                _activeBatters = new Player[team.Lineup.Count];
                _activeBattingLineIndices = new int[team.Lineup.Count];
                for (int index = 0; index < team.Lineup.Count; index++)
                {
                    _activeBatters[index] = team.Lineup[index].Player;
                    _activeBattingLineIndices[index] = index;
                }
                DefenseRating = CalculateDefenseRating();
                ActivePitcher = team.StartingPitcher;
                ActivePitchingLine = BoxScore.PitchingLine;
            }

            private readonly Player[] _activeBatters;
            private readonly int[] _activeBattingLineIndices;
            public Team Team { get; }
            public TeamBoxScoreBuilder BoxScore { get; }
            public double DefenseRating { get; private set; }
            public int NextBattingOrderIndex { get; set; }
            public Player ActivePitcher { get; private set; }
            public PlayerPitchingLine ActivePitchingLine { get; private set; }
            public bool HasUsedPositionPlayerSubstitution { get; private set; }

            /// <summary>
            /// 현재 타순을 맡은 선수와 그 선수 전용 기록 인덱스를 반환한다.
            /// </summary>
            public LineupSlotReference GetBatter(int battingOrderIndex)
            {
                return new LineupSlotReference(
                    _activeBatters[battingOrderIndex],
                    battingOrderIndex,
                    _activeBattingLineIndices[battingOrderIndex]);
            }

            /// <summary>
            /// 감독의 교체 조건을 만족하면 후보 선수가 기존 타순과 수비 위치를 승계한다.
            /// </summary>
            public bool TryApplyPositionPlayerSubstitution(
                int battingOrderIndex,
                int inning,
                int opponentRuns,
                out Player replacedPlayer)
            {
                replacedPlayer = null;
                PositionPlayerSubstitutionPlan plan = Team.PositionPlayerSubstitution;
                if (plan == null || HasUsedPositionPlayerSubstitution ||
                    plan.BattingOrderIndex != battingOrderIndex ||
                    !plan.CanEnter(inning, BoxScore.Runs, opponentRuns))
                {
                    return false;
                }

                replacedPlayer = _activeBatters[battingOrderIndex];
                _activeBatters[battingOrderIndex] = plan.Player;
                _activeBattingLineIndices[battingOrderIndex] = Team.Lineup.Count;
                HasUsedPositionPlayerSubstitution = true;
                DefenseRating = CalculateDefenseRating();
                return true;
            }

            /// <summary>
            /// 현재 수비 배치에서 지정 포지션을 맡은 선수를 반환한다.
            /// </summary>
            public Player GetActiveFielder(PlayerPosition position)
            {
                for (int index = 0; index < Team.Lineup.Count; index++)
                {
                    if (Team.Lineup[index].FieldingPosition == position)
                        return _activeBatters[index];
                }
                return null;
            }

            /// <summary>
            /// 현재 수비 중인 선수에게만 수비 이닝 아웃을 누적한다.
            /// </summary>
            public void RecordDefensiveOut()
            {
                for (int index = 0; index < Team.Lineup.Count; index++)
                {
                    if (Team.Lineup[index].FieldingPosition == PlayerPosition.DesignatedHitter)
                        continue;
                    BoxScore.GetFieldingLineByPlayer(_activeBatters[index].PlayerId).DefensiveOuts++;
                }
                BoxScore.GetFieldingLineByPlayer(ActivePitcher.PlayerId).DefensiveOuts++;
            }

            /// <summary>
            /// 이닝 시작 시 팀의 투수 운용 계약에 맞춰 현재 투수를 고정한다.
            /// </summary>
            public void SelectPitcher(int inning)
            {
                ActivePitcher = Team.GetPitcherForInning(inning);
                ActivePitchingLine = BoxScore.GetPitchingLine(ActivePitcher.PlayerId);
            }

            private double CalculateDefenseRating()
            {
                double total = 0d;
                int fielderCount = 0;
                for (int index = 0; index < Team.Lineup.Count; index++)
                {
                    PlayerPosition position = Team.Lineup[index].FieldingPosition;
                    if (position == PlayerPosition.DesignatedHitter)
                        continue;

                    Player player = _activeBatters[index];
                    int proficiency = player.GetPositionProficiency(position);
                    total += player.BatterAttributes.Defense * proficiency / 100d;
                    fielderCount++;
                }
                return fielderCount == 0 ? 0d : total / fielderCount;
            }
        }

        private readonly struct LineupSlotReference
        {
            public LineupSlotReference(Player player, int battingOrderIndex, int battingLineIndex)
            {
                Player = player;
                BattingOrderIndex = battingOrderIndex;
                BattingLineIndex = battingLineIndex;
            }

            public Player Player { get; }
            public int BattingOrderIndex { get; }
            public int BattingLineIndex { get; }
        }

        private readonly struct BaseRunner
        {
            public BaseRunner(Player player, int battingLineIndex)
            {
                Player = player;
                BattingLineIndex = battingLineIndex;
            }

            public Player Player { get; }
            public int BattingLineIndex { get; }
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
