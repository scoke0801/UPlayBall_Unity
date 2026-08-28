using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    public sealed partial class MatchSimulator
    {
        private int ApplySingle(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            BaseState bases,
            int outs)
        {
            BaseRunner oldFirst = bases.First;
            BaseRunner oldSecond = bases.Second;
            BaseRunner oldThird = bases.Third;
            bases.Clear();
            int runs = 0;

            if (oldThird.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, oldThird, 3, outs);
                runs++;
                if (IsWalkOffComplete(state, inning, half))
                    return runs;
            }

            if (oldSecond.IsOccupied)
            {
                double scoreProbability = CalculateAdvanceProbability(
                    _balance.BaseRunning.SingleFromSecondScoreProbability,
                    oldSecond.Player.BatterAttributes.Speed,
                    defense.DefenseRating);
                if (_random.NextDouble() < scoreProbability)
                {
                    ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, oldSecond, 2, outs);
                    runs++;
                    if (IsWalkOffComplete(state, inning, half))
                        return runs;
                }
                else
                {
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, oldSecond, 2, 3, outs);
                    bases.Third = oldSecond;
                }
            }

            if (oldFirst.IsOccupied)
            {
                double thirdBaseProbability = CalculateAdvanceProbability(
                    _balance.BaseRunning.SingleFromFirstToThirdProbability,
                    oldFirst.Player.BatterAttributes.Speed,
                    defense.DefenseRating);
                if (!bases.Third.IsOccupied && _random.NextDouble() < thirdBaseProbability)
                {
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, oldFirst, 1, 3, outs);
                    bases.Third = oldFirst;
                }
                else
                {
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, oldFirst, 1, 2, outs);
                    bases.Second = oldFirst;
                }
            }

            BaseRunner batterRunner = new BaseRunner(batter.Player, batter.BattingLineIndex);
            MoveRunner(state, inning, half, defense, batter.Player.PlayerId, batterRunner, 0, 1, outs);
            bases.First = batterRunner;
            return runs;
        }

        private int ApplyDouble(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            BaseState bases,
            int outs)
        {
            BaseRunner oldFirst = bases.First;
            BaseRunner oldSecond = bases.Second;
            BaseRunner oldThird = bases.Third;
            bases.Clear();
            int runs = 0;

            if (oldThird.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, oldThird, 3, outs);
                runs++;
                if (IsWalkOffComplete(state, inning, half))
                    return runs;
            }
            if (oldSecond.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, oldSecond, 2, outs);
                runs++;
                if (IsWalkOffComplete(state, inning, half))
                    return runs;
            }
            if (oldFirst.IsOccupied)
            {
                double scoreProbability = CalculateAdvanceProbability(
                    _balance.BaseRunning.DoubleFromFirstScoreProbability,
                    oldFirst.Player.BatterAttributes.Speed,
                    defense.DefenseRating);
                if (_random.NextDouble() < scoreProbability)
                {
                    ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, oldFirst, 1, outs);
                    runs++;
                    if (IsWalkOffComplete(state, inning, half))
                        return runs;
                }
                else
                {
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, oldFirst, 1, 3, outs);
                    bases.Third = oldFirst;
                }
            }

            BaseRunner batterRunner = new BaseRunner(batter.Player, batter.BattingLineIndex);
            MoveRunner(state, inning, half, defense, batter.Player.PlayerId, batterRunner, 0, 2, outs);
            bases.Second = batterRunner;
            return runs;
        }

        private int ApplyTriple(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            BaseState bases,
            int outs)
        {
            int runs = ScoreAllExistingRunners(
                state,
                inning,
                half,
                offense,
                defense,
                batter.Player.PlayerId,
                bases,
                outs,
                stopOnWalkOff: true);
            BaseRunner batterRunner = new BaseRunner(batter.Player, batter.BattingLineIndex);
            MoveRunner(state, inning, half, defense, batter.Player.PlayerId, batterRunner, 0, 3, outs);
            bases.Third = batterRunner;
            return runs;
        }

        private int ApplyHomeRun(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            BaseState bases,
            int outs)
        {
            int runs = ScoreAllExistingRunners(
                state,
                inning,
                half,
                offense,
                defense,
                batter.Player.PlayerId,
                bases,
                outs,
                stopOnWalkOff: false);
            var batterRunner = new BaseRunner(batter.Player, batter.BattingLineIndex);
            ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, batterRunner, 0, outs);
            return runs + 1;
        }

        private int ScoreAllExistingRunners(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            int batterId,
            BaseState bases,
            int outs,
            bool stopOnWalkOff)
        {
            int runs = 0;
            if (bases.Third.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batterId, bases.Third, 3, outs);
                runs++;
                if (stopOnWalkOff && IsWalkOffComplete(state, inning, half))
                {
                    bases.Clear();
                    return runs;
                }
            }
            if (bases.Second.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batterId, bases.Second, 2, outs);
                runs++;
                if (stopOnWalkOff && IsWalkOffComplete(state, inning, half))
                {
                    bases.Clear();
                    return runs;
                }
            }
            if (bases.First.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batterId, bases.First, 1, outs);
                runs++;
            }

            bases.Clear();
            return runs;
        }

        private static bool IsWalkOffComplete(MatchSimulationState state, int inning, InningHalf half)
        {
            return inning >= Baseball.Core.Rules.BaseballRules.RegulationInnings &&
                   half == InningHalf.Bottom &&
                   state.Home.BoxScore.Runs > state.Away.BoxScore.Runs;
        }

        private int ApplyGroundOut(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            BaseState bases,
            ref int outs)
        {
            int outsBeforePlay = outs;
            if (bases.First.IsOccupied && outsBeforePlay <= 1 && IsDoublePlay(defense, bases.First))
            {
                offense.BoxScore.BattingLines[batter.BattingLineIndex].GroundedIntoDoublePlays++;
                BaseRunner forcedRunner = bases.First;
                bases.First = default;
                RecordOut(state, inning, half, defense, forcedRunner.Player.PlayerId, PlateAppearanceResult.GroundOut, ref outs);
                RecordOut(state, inning, half, defense, batter.Player.PlayerId, PlateAppearanceResult.GroundOut, ref outs);
                return 0;
            }

            RecordOut(state, inning, half, defense, batter.Player.PlayerId, PlateAppearanceResult.GroundOut, ref outs);
            int runs = 0;

            if (outsBeforePlay < 2 && bases.Third.IsOccupied)
            {
                double scoreProbability = CalculateAdvanceProbability(
                    _balance.BaseRunning.GroundOutFromThirdScoreProbability,
                    bases.Third.Player.BatterAttributes.Speed,
                    defense.DefenseRating);
                if (_random.NextDouble() < scoreProbability)
                {
                    ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, bases.Third, 3, outs);
                    bases.Third = default;
                    runs++;
                }
            }

            if (bases.Second.IsOccupied && !bases.Third.IsOccupied &&
                _random.NextDouble() < _balance.BaseRunning.GroundOutAdvanceProbability)
            {
                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.Second, 2, 3, outs);
                bases.Third = bases.Second;
                bases.Second = default;
            }
            if (bases.First.IsOccupied && !bases.Second.IsOccupied &&
                _random.NextDouble() < _balance.BaseRunning.GroundOutAdvanceProbability)
            {
                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.First, 1, 2, outs);
                bases.Second = bases.First;
                bases.First = default;
            }

            return runs;
        }

        private int ApplyFlyOut(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            BaseState bases,
            PlayerBattingLine battingLine,
            ref int outs)
        {
            int outsBeforePlay = outs;
            RecordOut(state, inning, half, defense, batter.Player.PlayerId, PlateAppearanceResult.FlyOut, ref outs);

            if (outsBeforePlay >= 2 || !bases.Third.IsOccupied)
            {
                battingLine.AtBats++;
                return 0;
            }

            double scoreProbability = CalculateAdvanceProbability(
                _balance.BaseRunning.SacrificeFlyProbability,
                bases.Third.Player.BatterAttributes.Speed,
                defense.DefenseRating);
            if (_random.NextDouble() >= scoreProbability)
            {
                battingLine.AtBats++;
                return 0;
            }

            ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, bases.Third, 3, outs);
            bases.Third = default;
            battingLine.SacrificeFlies++;
            return 1;
        }

        private bool IsDoublePlay(TeamMatchState defense, BaseRunner runner)
        {
            double probability = _balance.BaseRunning.DoublePlayProbability -
                                 (runner.Player.BatterAttributes.Speed - 50d) *
                                 _balance.BaseRunning.DoublePlayRunnerSpeedWeight +
                                 (defense.DefenseRating - 50d) *
                                 _balance.BaseRunning.DoublePlayDefenseWeight;
            return _random.NextDouble() < Clamp(probability, 0.08d, 0.65d);
        }

        private void RecordOut(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState defense,
            int playerId,
            PlateAppearanceResult result,
            ref int outs)
        {
            outs++;
            defense.ActivePitchingLine.OutsRecorded++;
            defense.RecordDefensiveOut();
            Emit(
                state,
                MatchEventType.Out,
                inning,
                half,
                playerId,
                defense.ActivePitcher.PlayerId,
                playerId,
                PitchResult.None,
                result,
                0,
                0,
                0,
                0,
                outs);
        }

        private void ScoreRunner(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            int batterId,
            BaseRunner runner,
            int fromBase,
            int outs)
        {
            offense.BoxScore.AddRun(inning);
            offense.BoxScore.BattingLines[runner.BattingLineIndex].Runs++;
            defense.ActivePitchingLine.RunsAllowed++;
            defense.ActivePitchingLine.EarnedRuns++;

            MoveRunner(state, inning, half, defense, batterId, runner, fromBase, 4, outs);
            Emit(
                state,
                MatchEventType.Score,
                inning,
                half,
                batterId,
                defense.ActivePitcher.PlayerId,
                runner.Player.PlayerId,
                PitchResult.None,
                PlateAppearanceResult.None,
                fromBase,
                4,
                0,
                0,
                outs);
        }

        private static void MoveRunner(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState defense,
            int batterId,
            BaseRunner runner,
            int fromBase,
            int toBase,
            int outs)
        {
            Emit(
                state,
                MatchEventType.RunnerAdvance,
                inning,
                half,
                batterId,
                defense.ActivePitcher.PlayerId,
                runner.Player.PlayerId,
                PitchResult.None,
                PlateAppearanceResult.None,
                fromBase,
                toBase,
                0,
                0,
                outs);
        }

        private double CalculateAdvanceProbability(double baseProbability, int speed, double defense)
        {
            return Clamp(
                baseProbability +
                (speed - 50d) * _balance.BaseRunning.RunnerSpeedWeight -
                (defense - 50d) * _balance.BaseRunning.DefenseWeight,
                0.05d,
                0.95d);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
                return minimum;
            if (value > maximum)
                return maximum;
            return value;
        }
    }
}
