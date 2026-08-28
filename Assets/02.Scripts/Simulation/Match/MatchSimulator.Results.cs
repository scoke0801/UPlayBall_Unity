using System;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    public sealed partial class MatchSimulator
    {
        private void ApplyPlateAppearanceResult(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            PlateAppearanceResult result,
            BaseState bases,
            ref int outs)
        {
            PlayerBattingLine battingLine = offense.BoxScore.BattingLines[batter.BattingLineIndex];
            PlayerPitchingLine pitchingLine = defense.ActivePitchingLine;
            battingLine.PlateAppearances++;
            pitchingLine.BattersFaced++;
            int runsBattedIn = 0;

            switch (result)
            {
                case PlateAppearanceResult.Walk:
                    battingLine.Walks++;
                    pitchingLine.WalksAllowed++;
                    runsBattedIn = ApplyForcedAdvance(state, inning, half, offense, defense, batter, bases, outs);
                    break;

                case PlateAppearanceResult.HitByPitch:
                    battingLine.HitByPitches++;
                    pitchingLine.HitBatters++;
                    runsBattedIn = ApplyForcedAdvance(state, inning, half, offense, defense, batter, bases, outs);
                    break;

                case PlateAppearanceResult.Strikeout:
                    battingLine.AtBats++;
                    battingLine.Strikeouts++;
                    pitchingLine.Strikeouts++;
                    RecordOut(state, inning, half, defense, batter.Player.PlayerId, result, ref outs);
                    break;

                case PlateAppearanceResult.Single:
                case PlateAppearanceResult.Double:
                case PlateAppearanceResult.Triple:
                case PlateAppearanceResult.HomeRun:
                    battingLine.AtBats++;
                    RecordHit(state, inning, half, offense, defense, batter, result, outs);
                    runsBattedIn = ApplyHit(state, inning, half, offense, defense, batter, result, bases, outs);
                    break;

                case PlateAppearanceResult.GroundOut:
                    battingLine.AtBats++;
                    runsBattedIn = ApplyGroundOut(
                        state,
                        inning,
                        half,
                        offense,
                        defense,
                        batter,
                        bases,
                        ref outs);
                    break;

                case PlateAppearanceResult.FlyOut:
                    runsBattedIn = ApplyFlyOut(
                        state,
                        inning,
                        half,
                        offense,
                        defense,
                        batter,
                        bases,
                        battingLine,
                        ref outs);
                    break;

                default:
                    throw new InvalidOperationException("지원하지 않는 PlateAppearanceResult입니다.");
            }

            RecordFieldingOpportunity(state, inning, defense, batter, result, outs);

            battingLine.RunsBattedIn += runsBattedIn;
            Emit(
                state,
                MatchEventType.PlateAppearanceEnded,
                inning,
                half,
                batter.Player.PlayerId,
                defense.ActivePitcher.PlayerId,
                batter.Player.PlayerId,
                PitchResult.None,
                result,
                0,
                0,
                0,
                0,
                outs);
        }

        /// <summary>
        /// 타자를 1루에 세우고 채워진 앞 베이스의 주자만 밀어낸다.
        /// 볼넷과 사구는 진루 규칙이 동일하므로 같은 경로를 쓴다.
        /// </summary>
        private int ApplyForcedAdvance(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            BaseState bases,
            int outs)
        {
            int runs = 0;
            if (bases.First.IsOccupied)
            {
                if (bases.Second.IsOccupied)
                {
                    if (bases.Third.IsOccupied)
                    {
                        ScoreRunner(state, inning, half, offense, defense, batter.Player.PlayerId, bases.Third, 3, outs);
                        runs++;
                    }

                    MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.Second, 2, 3, outs);
                    bases.Third = bases.Second;
                }

                MoveRunner(state, inning, half, defense, batter.Player.PlayerId, bases.First, 1, 2, outs);
                bases.Second = bases.First;
            }

            BaseRunner batterRunner = new BaseRunner(batter.Player, batter.BattingLineIndex);
            MoveRunner(state, inning, half, defense, batter.Player.PlayerId, batterRunner, 0, 1, outs);
            bases.First = batterRunner;
            return runs;
        }

        private void RecordHit(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            PlateAppearanceResult result,
            int outs)
        {
            PlayerBattingLine battingLine = offense.BoxScore.BattingLines[batter.BattingLineIndex];
            battingLine.Hits++;
            offense.BoxScore.Hits++;
            defense.ActivePitchingLine.HitsAllowed++;

            switch (result)
            {
                case PlateAppearanceResult.Double:
                    battingLine.Doubles++;
                    break;
                case PlateAppearanceResult.Triple:
                    battingLine.Triples++;
                    break;
                case PlateAppearanceResult.HomeRun:
                    battingLine.HomeRuns++;
                    defense.ActivePitchingLine.HomeRunsAllowed++;
                    break;
            }

            Emit(
                state,
                MatchEventType.Hit,
                inning,
                half,
                batter.Player.PlayerId,
                defense.ActivePitcher.PlayerId,
                batter.Player.PlayerId,
                PitchResult.None,
                result,
                0,
                GetHitBase(result),
                0,
                0,
                outs);
        }

        private int ApplyHit(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState offense,
            TeamMatchState defense,
            LineupSlotReference batter,
            PlateAppearanceResult result,
            BaseState bases,
            int outs)
        {
            switch (result)
            {
                case PlateAppearanceResult.Single:
                    return ApplySingle(state, inning, half, offense, defense, batter, bases, outs);
                case PlateAppearanceResult.Double:
                    return ApplyDouble(state, inning, half, offense, defense, batter, bases, outs);
                case PlateAppearanceResult.Triple:
                    return ApplyTriple(state, inning, half, offense, defense, batter, bases, outs);
                case PlateAppearanceResult.HomeRun:
                    return ApplyHomeRun(state, inning, half, offense, defense, batter, bases, outs);
                default:
                    throw new InvalidOperationException("안타가 아닌 결과입니다.");
            }
        }

        private static int GetHitBase(PlateAppearanceResult result)
        {
            switch (result)
            {
                case PlateAppearanceResult.Single:
                    return 1;
                case PlateAppearanceResult.Double:
                    return 2;
                case PlateAppearanceResult.Triple:
                    return 3;
                case PlateAppearanceResult.HomeRun:
                    return 4;
                default:
                    return 0;
            }
        }
    }
}
