using System;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    internal sealed partial class DetailedMatchEngine
    {
        private void ApplyPlateAppearanceResult(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedPlateAppearanceOutcome outcome,
            DetailedBaseState bases,
            EarnedRunTracker earnedRunTracker,
            ref int outs)
        {
            PlayerBattingLine battingLine = offense.BoxScore.GetBattingLine(batter.Player.PlayerId);
            PlayerPitchingLine pitchingLine = defense.ActivePitchingLine;
            battingLine.PlateAppearances++;
            pitchingLine.BattersFaced++;
            int runsBattedIn = 0;

            switch (outcome.Result)
            {
                case PlateAppearanceResult.Walk:
                    battingLine.Walks++;
                    pitchingLine.WalksAllowed++;
                    runsBattedIn = ApplyForcedAdvance(
                        state, inning, half, offense, defense, batter, bases, earnedRunTracker, outs, false);
                    break;
                case PlateAppearanceResult.IntentionalWalk:
                    battingLine.Walks++;
                    battingLine.IntentionalWalks++;
                    pitchingLine.WalksAllowed++;
                    runsBattedIn = ApplyForcedAdvance(
                        state, inning, half, offense, defense, batter, bases, earnedRunTracker, outs, false);
                    break;
                case PlateAppearanceResult.HitByPitch:
                    battingLine.HitByPitches++;
                    pitchingLine.HitBatters++;
                    runsBattedIn = ApplyForcedAdvance(
                        state, inning, half, offense, defense, batter, bases, earnedRunTracker, outs, false);
                    break;
                case PlateAppearanceResult.Strikeout:
                    battingLine.AtBats++;
                    battingLine.Strikeouts++;
                    pitchingLine.Strikeouts++;
                    RecordOut(state, inning, half, defense, batter.Player.PlayerId,
                        outcome.Result, earnedRunTracker, ref outs);
                    break;
                case PlateAppearanceResult.Single:
                case PlateAppearanceResult.Double:
                case PlateAppearanceResult.Triple:
                case PlateAppearanceResult.HomeRun:
                case PlateAppearanceResult.BuntSingle:
                    battingLine.AtBats++;
                    RecordHit(state, inning, half, offense, defense, batter, outcome.Result, outs);
                    runsBattedIn = ApplyHit(
                        state, inning, half, offense, defense, batter, outcome.Result,
                        outcome.Fielding, bases, earnedRunTracker, ref outs);
                    break;
                case PlateAppearanceResult.ReachedOnError:
                    battingLine.AtBats++;
                    battingLine.ReachedOnErrors++;
                    runsBattedIn = ApplyReachedOnError(
                        state, inning, half, offense, defense, batter, outcome,
                        bases, earnedRunTracker, outs);
                    break;
                case PlateAppearanceResult.FieldersChoice:
                    battingLine.AtBats++;
                    runsBattedIn = ApplyFieldersChoice(
                        state, inning, half, offense, defense, batter, outcome,
                        bases, earnedRunTracker, ref outs);
                    break;
                case PlateAppearanceResult.SacrificeBunt:
                    battingLine.SacrificeBunts++;
                    runsBattedIn = ApplySacrificeBunt(
                        state, inning, half, offense, defense, batter, outcome,
                        bases, earnedRunTracker, ref outs);
                    break;
                case PlateAppearanceResult.GroundOut:
                    battingLine.AtBats++;
                    runsBattedIn = ApplyGroundOut(
                        state, inning, half, offense, defense, batter, outcome,
                        bases, earnedRunTracker, ref outs);
                    break;
                case PlateAppearanceResult.FlyOut:
                case PlateAppearanceResult.BuntPopOut:
                    runsBattedIn = ApplyFlyOut(
                        state, inning, half, offense, defense, batter, outcome,
                        bases, battingLine, earnedRunTracker, ref outs);
                    break;
                default:
                    throw new InvalidOperationException($"지원하지 않는 V2 타석 결과입니다: {outcome.Result}");
            }

            RecordFieldingOutcome(state, inning, half, defense, batter, outcome, outs);
            battingLine.RunsBattedIn += runsBattedIn;
            Emit(
                state,
                MatchEventType.PlateAppearanceEnded,
                inning,
                half,
                batter.Player.PlayerId,
                defense.ActivePitcher.PlayerId,
                batter.Player.PlayerId,
                plateAppearanceResult: outcome.Result,
                outs: outs,
                ballInPlayData: outcome.BattedBall.HasValue
                    ? new BallInPlayEventData(outcome.BattedBall, outcome.Fielding)
                    : default);
        }

        private int ApplyForcedAdvance(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            int outs,
            bool batterUnearned)
        {
            int runs = 0;
            if (bases.First.IsOccupied)
            {
                if (bases.Second.IsOccupied)
                {
                    if (bases.Third.IsOccupied)
                    {
                        ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId,
                            bases.Third, 3, tracker, outs);
                        runs++;
                    }
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.Second, 2, 3, outs);
                    bases.Third = bases.Second;
                }
                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.First, 1, 2, outs);
                bases.Second = bases.First;
            }
            var batterRunner = new DetailedBaseRunner(
                batter.Player,
                batter.BattingLineIndex,
                defense.ActivePitcher.PlayerId,
                batterUnearned);
            MoveRunner(state, inning, half, defense, batter.Player.PlayerId, batterRunner, 0, 1, outs);
            bases.First = batterRunner;
            return runs;
        }

        private void RecordHit(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            PlateAppearanceResult result,
            int outs)
        {
            PlayerBattingLine line = offense.BoxScore.GetBattingLine(batter.Player.PlayerId);
            line.Hits++;
            offense.BoxScore.Hits++;
            defense.ActivePitchingLine.HitsAllowed++;
            if (result == PlateAppearanceResult.Double) line.Doubles++;
            if (result == PlateAppearanceResult.Triple) line.Triples++;
            if (result == PlateAppearanceResult.HomeRun)
            {
                line.HomeRuns++;
                defense.ActivePitchingLine.HomeRunsAllowed++;
            }
            Emit(
                state,
                MatchEventType.Hit,
                inning,
                half,
                batter.Player.PlayerId,
                defense.ActivePitcher.PlayerId,
                batter.Player.PlayerId,
                plateAppearanceResult: result,
                toBase: GetHitBase(result),
                outs: outs);
        }

        private int ApplyHit(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            PlateAppearanceResult result,
            FieldingPlayOutcome fielding,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            ref int outs)
        {
            if (result is PlateAppearanceResult.Single or PlateAppearanceResult.BuntSingle)
                return ApplySingle(state, inning, half, offense, defense, batter, fielding, bases, tracker, ref outs);
            if (result == PlateAppearanceResult.Double)
                return ApplyDouble(state, inning, half, offense, defense, batter, fielding, bases, tracker, ref outs);
            if (result == PlateAppearanceResult.Triple)
                return ApplyTriple(state, inning, half, offense, defense, batter, bases, tracker, outs);
            return ApplyHomeRun(state, inning, half, offense, defense, batter, bases, tracker, outs);
        }

        private int ApplySingle(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            FieldingPlayOutcome fielding,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            ref int outs)
        {
            DetailedBaseRunner first = bases.First;
            DetailedBaseRunner second = bases.Second;
            DetailedBaseRunner third = bases.Third;
            bases.Clear();
            int runs = 0;
            int arm = GetFielderArm(defense, fielding);
            if (third.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, third, 3, tracker, outs);
                runs++;
            }
            if (second.IsOccupied && !IsWalkOffComplete(state, inning, half))
            {
                BaserunningDecision decision = _baserunningResolver.DecideExtraBase(
                    _balance.BaseRunning.SingleFromSecondScoreProbability,
                    second.Player,
                    arm,
                    outs,
                    inning,
                    offense.BoxScore.Runs - defense.BoxScore.Runs,
                    offense.Roster.RunningApproach);
                if (decision.ShouldAttempt && _baserunningResolver.Resolve(decision))
                {
                    ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, second, 2, tracker, outs);
                    runs++;
                }
                else if (decision.ShouldAttempt)
                {
                    RecordRunnerThrownOut(state, inning, half, defense, second, 2, 4, tracker, ref outs);
                }
                else
                {
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, second, 2, 3, outs);
                    bases.Third = second;
                }
            }
            if (first.IsOccupied && outs < 3)
            {
                BaserunningDecision decision = _baserunningResolver.DecideExtraBase(
                    _balance.BaseRunning.SingleFromFirstToThirdProbability,
                    first.Player,
                    arm,
                    outs,
                    inning,
                    offense.BoxScore.Runs - defense.BoxScore.Runs,
                    offense.Roster.RunningApproach);
                if (!bases.Third.IsOccupied && decision.ShouldAttempt && _baserunningResolver.Resolve(decision))
                {
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, first, 1, 3, outs);
                    bases.Third = first;
                }
                else if (!bases.Third.IsOccupied && decision.ShouldAttempt)
                {
                    RecordRunnerThrownOut(state, inning, half, defense, first, 1, 3, tracker, ref outs);
                }
                else
                {
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, first, 1, 2, outs);
                    bases.Second = first;
                }
            }
            if (outs < 3)
            {
                var runner = new DetailedBaseRunner(
                    batter.Player, batter.BattingLineIndex, defense.ActivePitcher.PlayerId, false);
                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, runner, 0, 1, outs);
                bases.First = runner;
            }
            return runs;
        }

        private int ApplyDouble(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            FieldingPlayOutcome fielding,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            ref int outs)
        {
            DetailedBaseRunner first = bases.First;
            DetailedBaseRunner second = bases.Second;
            DetailedBaseRunner third = bases.Third;
            bases.Clear();
            int runs = 0;
            if (third.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, third, 3, tracker, outs);
                runs++;
            }
            if (second.IsOccupied && !IsWalkOffComplete(state, inning, half))
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, second, 2, tracker, outs);
                runs++;
            }
            if (first.IsOccupied && !IsWalkOffComplete(state, inning, half))
            {
                BaserunningDecision decision = _baserunningResolver.DecideExtraBase(
                    _balance.BaseRunning.DoubleFromFirstScoreProbability,
                    first.Player,
                    GetFielderArm(defense, fielding),
                    outs,
                    inning,
                    offense.BoxScore.Runs - defense.BoxScore.Runs,
                    offense.Roster.RunningApproach);
                if (decision.ShouldAttempt && _baserunningResolver.Resolve(decision))
                {
                    ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, first, 1, tracker, outs);
                    runs++;
                }
                else if (decision.ShouldAttempt)
                {
                    RecordRunnerThrownOut(state, inning, half, defense, first, 1, 4, tracker, ref outs);
                }
                else
                {
                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, first, 1, 3, outs);
                    bases.Third = first;
                }
            }
            if (outs < 3)
            {
                var runner = new DetailedBaseRunner(
                    batter.Player, batter.BattingLineIndex, defense.ActivePitcher.PlayerId, false);
                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, runner, 0, 2, outs);
                bases.Second = runner;
            }
            return runs;
        }

        private int ApplyTriple(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            int outs)
        {
            int runs = ScoreAllRunners(state, inning, half, offense, defense, batter.Player.PlayerId, bases, tracker, outs);
            var runner = new DetailedBaseRunner(
                batter.Player, batter.BattingLineIndex, defense.ActivePitcher.PlayerId, false);
            MoveRunner(state, inning, half, defense, batter.Player.PlayerId, runner, 0, 3, outs);
            bases.Third = runner;
            return runs;
        }

        private int ApplyHomeRun(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            int outs)
        {
            int runs = ScoreAllRunners(state, inning, half, offense, defense, batter.Player.PlayerId, bases, tracker, outs);
            var batterRunner = new DetailedBaseRunner(
                batter.Player, batter.BattingLineIndex, defense.ActivePitcher.PlayerId, false);
            ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, batterRunner, 0, tracker, outs);
            return runs + 1;
        }

        private int ApplyReachedOnError(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedPlateAppearanceOutcome outcome,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            int outs)
        {
            defense.BoxScore.Errors++;
            if (outcome.Fielding.WasRoutine)
                tracker.RecordRoutineError();
            return ApplyForcedAdvance(
                state, inning, half, offense, defense, batter, bases, tracker, outs, batterUnearned: true);
        }

        private int ApplyFieldersChoice(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedPlateAppearanceOutcome outcome,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            ref int outs)
        {
            if (!bases.First.IsOccupied)
            {
                RecordOut(state, inning, half, defense, batter.Player.PlayerId,
                    PlateAppearanceResult.GroundOut, tracker, ref outs);
                return 0;
            }
            DetailedBaseRunner forced = bases.First;
            bases.First = default;
            RecordOut(state, inning, half, defense, forced.Player.PlayerId,
                PlateAppearanceResult.FieldersChoice, tracker, ref outs);
            if (bases.Second.IsOccupied && !bases.Third.IsOccupied)
            {
                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.Second, 2, 3, outs);
                bases.Third = bases.Second;
                bases.Second = default;
            }
            var batterRunner = new DetailedBaseRunner(
                batter.Player, batter.BattingLineIndex, defense.ActivePitcher.PlayerId, false);
            MoveRunner(state, inning, half, defense, batter.Player.PlayerId, batterRunner, 0, 1, outs);
            bases.First = batterRunner;
            Emit(state, MatchEventType.FieldersChoice, inning, half,
                batter.Player.PlayerId, defense.ActivePitcher.PlayerId, outcome.Fielding.FielderId,
                plateAppearanceResult: PlateAppearanceResult.FieldersChoice, outs: outs);
            return 0;
        }

        private int ApplySacrificeBunt(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedPlateAppearanceOutcome outcome,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            ref int outs)
        {
            RecordOut(state, inning, half, defense, batter.Player.PlayerId,
                PlateAppearanceResult.SacrificeBunt, tracker, ref outs);
            if (outs >= 3) return 0;
            if (bases.Third.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId,
                    bases.Third, 3, tracker, outs);
                bases.Third = default;
            }
            if (bases.Second.IsOccupied)
            {
                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.Second, 2, 3, outs);
                bases.Third = bases.Second;
                bases.Second = default;
            }
            if (bases.First.IsOccupied)
            {
                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.First, 1, 2, outs);
                bases.Second = bases.First;
                bases.First = default;
            }
            Emit(state, MatchEventType.BuntResolved, inning, half,
                batter.Player.PlayerId, defense.ActivePitcher.PlayerId, outcome.Fielding.FielderId,
                plateAppearanceResult: PlateAppearanceResult.SacrificeBunt, outs: outs);
            return 0;
        }

        private int ApplyGroundOut(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedPlateAppearanceOutcome outcome,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            ref int outs)
        {
            if (outcome.Fielding.IsDoublePlay && bases.First.IsOccupied && outs < 2)
            {
                offense.BoxScore.GetBattingLine(batter.Player.PlayerId).GroundedIntoDoublePlays++;
                DetailedBaseRunner forced = bases.First;
                bases.First = default;
                RecordOut(state, inning, half, defense, forced.Player.PlayerId,
                    PlateAppearanceResult.GroundOut, tracker, ref outs);
                RecordOut(state, inning, half, defense, batter.Player.PlayerId,
                    PlateAppearanceResult.GroundOut, tracker, ref outs);
                Emit(state, MatchEventType.DoublePlay, inning, half,
                    batter.Player.PlayerId, defense.ActivePitcher.PlayerId, outcome.Fielding.FielderId,
                    plateAppearanceResult: PlateAppearanceResult.GroundOut, outs: outs);
                return 0;
            }

            int outsBefore = outs;
            RecordOut(state, inning, half, defense, batter.Player.PlayerId,
                PlateAppearanceResult.GroundOut, tracker, ref outs);
            if (outsBefore >= 2 || !bases.Third.IsOccupied)
                return 0;
            BaserunningDecision decision = _baserunningResolver.DecideExtraBase(
                _balance.BaseRunning.GroundOutFromThirdScoreProbability,
                bases.Third.Player,
                GetFielderArm(defense, outcome.Fielding),
                outsBefore,
                inning,
                offense.BoxScore.Runs - defense.BoxScore.Runs,
                offense.Roster.RunningApproach);
            if (decision.ShouldAttempt && _baserunningResolver.Resolve(decision))
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId,
                    bases.Third, 3, tracker, outs);
                bases.Third = default;
                return 1;
            }
            if (decision.ShouldAttempt)
            {
                DetailedBaseRunner runner = bases.Third;
                bases.Third = default;
                RecordRunnerThrownOut(state, inning, half, defense, runner, 3, 4, tracker, ref outs);
            }
            return 0;
        }

        private int ApplyFlyOut(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedPlateAppearanceOutcome outcome,
            DetailedBaseState bases,
            PlayerBattingLine battingLine,
            EarnedRunTracker tracker,
            ref int outs)
        {
            int outsBefore = outs;
            RecordOut(state, inning, half, defense, batter.Player.PlayerId,
                outcome.Result, tracker, ref outs);
            if (outsBefore >= 2 || !bases.Third.IsOccupied)
            {
                battingLine.AtBats++;
                return 0;
            }
            BaserunningDecision decision = _baserunningResolver.DecideExtraBase(
                _balance.BaseRunning.SacrificeFlyProbability,
                bases.Third.Player,
                GetFielderArm(defense, outcome.Fielding),
                outsBefore,
                inning,
                offense.BoxScore.Runs - defense.BoxScore.Runs,
                offense.Roster.RunningApproach);
            if (!decision.ShouldAttempt)
            {
                battingLine.AtBats++;
                return 0;
            }
            DetailedBaseRunner runner = bases.Third;
            bases.Third = default;
            if (_baserunningResolver.Resolve(decision))
            {
                ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId,
                    runner, 3, tracker, outs);
                battingLine.SacrificeFlies++;
                return 1;
            }
            RecordRunnerThrownOut(state, inning, half, defense, runner, 3, 4, tracker, ref outs);
            battingLine.AtBats++;
            return 0;
        }

        private int ScoreAllRunners(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            int batterId,
            DetailedBaseState bases,
            EarnedRunTracker tracker,
            int outs)
        {
            int runs = 0;
            if (bases.Third.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batterId, bases.Third, 3, tracker, outs);
                runs++;
            }
            if (bases.Second.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batterId, bases.Second, 2, tracker, outs);
                runs++;
            }
            if (bases.First.IsOccupied)
            {
                ScoreRunner(state, inning, half, offense, defense, batterId, bases.First, 1, tracker, outs);
                runs++;
            }
            bases.Clear();
            return runs;
        }

        private void ScoreRunner(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            int batterId,
            DetailedBaseRunner runner,
            int fromBase,
            EarnedRunTracker tracker,
            int outs)
        {
            offense.BoxScore.AddRun(inning);
            offense.BoxScore.GetBattingLine(runner.Player.PlayerId).Runs++;
            PlayerPitchingLine responsible = defense.BoxScore.GetPitchingLine(runner.ResponsiblePitcherId);
            responsible.RunsAllowed++;
            if (tracker.IsEarned(runner))
                responsible.EarnedRuns++;
            defense.RecordInheritedRunnerScored(runner.ResponsiblePitcherId);
            MoveRunner(state, inning, half, defense, batterId, runner, fromBase, 4, outs);
            Emit(state, MatchEventType.Score, inning, half,
                batterId, defense.ActivePitcher.PlayerId, runner.Player.PlayerId,
                fromBase: fromBase, toBase: 4, outs: outs);
        }

        private static void MoveRunner(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState defense,
            int batterId,
            DetailedBaseRunner runner,
            int fromBase,
            int toBase,
            int outs)
        {
            Emit(state, MatchEventType.RunnerAdvance, inning, half,
                batterId, defense.ActivePitcher.PlayerId, runner.Player.PlayerId,
                fromBase: fromBase, toBase: toBase, outs: outs);
        }

        private void RecordRunnerThrownOut(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState defense,
            DetailedBaseRunner runner,
            int fromBase,
            int toBase,
            EarnedRunTracker tracker,
            ref int outs)
        {
            RecordOut(state, inning, half, defense, runner.Player.PlayerId,
                PlateAppearanceResult.None, tracker, ref outs);
            Emit(state, MatchEventType.RunnerThrownOut, inning, half,
                pitcherId: defense.ActivePitcher.PlayerId,
                playerId: runner.Player.PlayerId,
                fromBase: fromBase,
                toBase: toBase,
                outs: outs);
        }

        private void RecordOut(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState defense,
            int playerId,
            PlateAppearanceResult result,
            EarnedRunTracker tracker,
            ref int outs)
        {
            outs++;
            defense.ActivePitchingLine.OutsRecorded++;
            defense.RecordDefensiveOut();
            tracker.RecordActualOut();
            Emit(state, MatchEventType.Out, inning, half,
                pitcherId: defense.ActivePitcher.PlayerId,
                playerId: playerId,
                plateAppearanceResult: result,
                outs: outs);
        }

        private void RecordFieldingOutcome(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DetailedPlateAppearanceOutcome outcome,
            int outs)
        {
            if (outcome.Fielding.FielderId <= 0)
                return;
            PlayerFieldingLine line = defense.BoxScore.GetFieldingLineByPlayer(outcome.Fielding.FielderId);
            line.Opportunities++;
            line.ExpectedOuts += outcome.Fielding.ReachChance;
            if (!outcome.Fielding.WasRoutine) line.DifficultPlayAttempts++;
            bool success = outcome.Fielding.FailureType == FieldingFailureType.None &&
                           outcome.Result is PlateAppearanceResult.GroundOut or PlateAppearanceResult.FlyOut or
                               PlateAppearanceResult.FieldersChoice or PlateAppearanceResult.SacrificeBunt;
            if (success)
            {
                line.SuccessfulPlays++;
                line.Putouts++;
                if (!outcome.Fielding.WasRoutine) line.DifficultPlaysMade++;
                if (outcome.Fielding.IsDoublePlay)
                {
                    line.DoublePlays++;
                    line.Assists++;
                }
                line.EstimatedRunsSaved += (1d - outcome.Fielding.ReachChance) * 0.55d;
            }
            else
            {
                line.EstimatedRunsSaved -= outcome.Fielding.ReachChance * 0.55d;
            }

            if (outcome.Fielding.FailureType is FieldingFailureType.FieldingError or FieldingFailureType.ThrowingError)
            {
                line.Errors++;
                MatchEventType type = outcome.Fielding.FailureType == FieldingFailureType.ThrowingError
                    ? MatchEventType.ThrowingError
                    : MatchEventType.FieldingError;
                Emit(state, type, inning, half,
                    batter.Player.PlayerId, defense.ActivePitcher.PlayerId, outcome.Fielding.FielderId,
                    plateAppearanceResult: PlateAppearanceResult.ReachedOnError,
                    outs: outs);
            }
        }

        private static int GetFielderArm(DetailedTeamGameState defense, FieldingPlayOutcome fielding)
        {
            if (fielding.FielderId <= 0)
                return 50;
            Player fielder = defense.GetActiveFielder(fielding.FielderPosition);
            if (fielder == null && defense.ActivePitcher.PlayerId == fielding.FielderId)
                fielder = defense.ActivePitcher;
            return fielder == null
                ? 50
                : FieldingProfile.Derive(fielder, fielding.FielderPosition).Arm;
        }

        private static int GetHitBase(PlateAppearanceResult result)
        {
            return result switch
            {
                PlateAppearanceResult.Single or PlateAppearanceResult.BuntSingle => 1,
                PlateAppearanceResult.Double => 2,
                PlateAppearanceResult.Triple => 3,
                PlateAppearanceResult.HomeRun => 4,
                _ => 0
            };
        }

        private static bool IsWalkOffComplete(DetailedMatchState state, int inning, InningHalf half)
        {
            return inning >= state.Input.Rules.RegulationInnings &&
                   half == InningHalf.Bottom &&
                   state.Home.BoxScore.Runs > state.Away.BoxScore.Runs;
        }
    }
}
