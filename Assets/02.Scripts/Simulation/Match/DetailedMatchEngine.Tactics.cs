using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    internal sealed partial class DetailedMatchEngine
    {
        private void TryChangePitcher(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState defense,
            DecisionContext context,
            int inheritedRunners)
        {
            if (defense.ActivePitcherState.BattersFaced == 0)
                return;
            BullpenManagementBalance balance = _balance.Match.BullpenManagement;
            int available = defense.CountAvailableRelievers(balance, allowEmergency: true);
            PitcherChangeDecision decision = _pitcherManagementAi.Evaluate(
                context,
                available,
                defense.CalculateBullpenFreshness(balance));
            if (!decision.ShouldChange)
                return;

            int remainingInnings = Math.Max(1, state.Input.Rules.RegulationInnings - inning + 1);
            int candidateIndex = defense.SelectReliever(
                _pitcherManagementAi,
                balance,
                context.Leverage,
                remainingInnings,
                inning,
                -context.ScoreDifference);
            if (candidateIndex < 0)
                return;

            PitcherGameState removed = defense.ChangePitcher(
                candidateIndex,
                inning,
                half,
                decision.Reason,
                inheritedRunners,
                -context.ScoreDifference);
            defense.ActivePitcherState.StartInning();
            DecisionReasonCode reasonCode = MapPitcherReason(decision.Reason);
            state.Trace?.Add(new DecisionTraceEntry(
                inning,
                half,
                removed.Player.PlayerId,
                "PitchingChange",
                reasonCode,
                decision.PullScore,
                decision.Threshold));
            Emit(
                state,
                MatchEventType.PitcherRemoved,
                inning,
                half,
                pitcherId: removed.Player.PlayerId,
                playerId: removed.Player.PlayerId,
                reasonCode: reasonCode);
            Emit(
                state,
                MatchEventType.PitcherEntered,
                inning,
                half,
                pitcherId: defense.ActivePitcher.PlayerId,
                playerId: defense.ActivePitcher.PlayerId,
                reasonCode: reasonCode);
        }

        private void TryApplyDefensiveReplacements(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense)
        {
            if (inning < 7 || defense.BoxScore.Runs <= offense.BoxScore.Runs)
                return;
            for (int orderIndex = 0; orderIndex < BaseballRules.BattingOrderSize; orderIndex++)
            {
                if (!defense.TryFindDefensiveReplacement(orderIndex, out int benchIndex))
                    continue;
                Player leaving = defense.SubstitutePositionPlayer(
                    orderIndex,
                    benchIndex,
                    inning,
                    half,
                    SubstitutionType.DefensiveReplacement,
                    DecisionReasonCode.DefensiveStrategy);
                Player entering = defense.GetBatter(orderIndex).Player;
                Emit(
                    state,
                    MatchEventType.DefensiveReplacement,
                    inning,
                    half,
                    playerId: entering.PlayerId,
                    fromBase: leaving.PlayerId,
                    reasonCode: DecisionReasonCode.DefensiveStrategy);
            }
        }

        private void PlaceAutomaticRunnerIfNeeded(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedBaseState bases)
        {
            if (state.Input.Rules.ExtraInningPolicy != ExtraInningPolicy.AutomaticRunnerUntilWinner ||
                inning < state.Input.Rules.AutomaticRunnerStartInning)
            {
                return;
            }
            int previousIndex = (offense.NextBattingOrderIndex + BaseballRules.BattingOrderSize - 1) %
                                BaseballRules.BattingOrderSize;
            DetailedLineupReference runner = offense.GetBatter(previousIndex);
            bases.Second = new DetailedBaseRunner(
                runner.Player,
                runner.BattingLineIndex,
                defense.ActivePitcher.PlayerId,
                isUnearned: true);
            Emit(
                state,
                MatchEventType.RunnerAdvance,
                inning,
                half,
                pitcherId: defense.ActivePitcher.PlayerId,
                playerId: runner.Player.PlayerId,
                fromBase: 0,
                toBase: 2);
        }

        private bool TryStealBeforePlateAppearance(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DecisionContext context,
            DetailedBaseState bases,
            EarnedRunTracker earnedRunTracker,
            ref int outs)
        {
            if (!bases.First.IsOccupied || bases.Second.IsOccupied)
                return false;
            Player catcher = defense.GetCatcher();
            if (!_tacticalAi.ShouldAttemptSteal(context, bases.First.Player, catcher))
                return false;

            DetailedBaseRunner runner = bases.First;
            double successChance = _tacticalAi.CalculateStealSuccess(
                runner.Player,
                catcher,
                defense.ActivePitcher);
            Emit(
                state,
                MatchEventType.StealAttempted,
                inning,
                half,
                pitcherId: defense.ActivePitcher.PlayerId,
                playerId: runner.Player.PlayerId,
                fromBase: 1,
                toBase: 2,
                outs: outs,
                reasonCode: DecisionReasonCode.ExpectedValue);
            if (_random.Baserunning.NextDouble() < successChance)
            {
                bases.First = default;
                bases.Second = runner;
                offense.BoxScore.GetBattingLine(runner.Player.PlayerId).StolenBases++;
                Emit(
                    state,
                    MatchEventType.StealSucceeded,
                    inning,
                    half,
                    pitcherId: defense.ActivePitcher.PlayerId,
                    playerId: runner.Player.PlayerId,
                    fromBase: 1,
                    toBase: 2,
                    outs: outs,
                    reasonCode: DecisionReasonCode.ExpectedValue);
            }
            else
            {
                bases.First = default;
                offense.BoxScore.GetBattingLine(runner.Player.PlayerId).CaughtStealing++;
                RecordOut(
                    state,
                    inning,
                    half,
                    defense,
                    runner.Player.PlayerId,
                    PlateAppearanceResult.None,
                    earnedRunTracker,
                    ref outs);
                Emit(
                    state,
                    MatchEventType.CaughtStealing,
                    inning,
                    half,
                    pitcherId: defense.ActivePitcher.PlayerId,
                    playerId: runner.Player.PlayerId,
                    fromBase: 1,
                    outs: outs,
                    reasonCode: DecisionReasonCode.ExpectedValue);
            }
            return true;
        }

        private void UpdatePitcherAfterPlateAppearance(
            DetailedTeamGameState defense,
            PlateAppearanceResult result,
            int runsCharged,
            DetailedBaseState bases,
            LeverageTier leverage)
        {
            bool wasHit = result is PlateAppearanceResult.Single or PlateAppearanceResult.Double or
                PlateAppearanceResult.Triple or PlateAppearanceResult.HomeRun or PlateAppearanceResult.BuntSingle;
            bool wasWalk = result is PlateAppearanceResult.Walk or PlateAppearanceResult.IntentionalWalk or
                PlateAppearanceResult.HitByPitch;
            bool reached = wasHit || wasWalk || result == PlateAppearanceResult.ReachedOnError;
            defense.ActivePitcherState.RecordPlateAppearance(reached, wasHit, wasWalk, runsCharged);
            PitcherStressBalance stress = _balance.Match.PitcherStress;
            if (wasWalk) defense.ActivePitcherState.AddStress(stress.WalkStress);
            if (wasHit) defense.ActivePitcherState.AddStress(
                result is PlateAppearanceResult.Double or PlateAppearanceResult.Triple or PlateAppearanceResult.HomeRun
                    ? stress.ExtraBaseHitStress
                    : stress.HitStress);
            if (runsCharged > 0) defense.ActivePitcherState.AddStress(stress.RunStress * runsCharged);
            if (bases.Second.IsOccupied || bases.Third.IsOccupied)
                defense.ActivePitcherState.AddStress(stress.ScoringPositionStress);
            if (!reached)
                defense.ActivePitcherState.RecoverStress(stress.OutRecovery);
            if (leverage >= LeverageTier.High)
                defense.RecordHighLeverageBatter();
        }

        private static DecisionReasonCode MapPitcherReason(PitcherChangeReason reason)
        {
            return reason switch
            {
                PitcherChangeReason.Fatigue => DecisionReasonCode.Fatigue,
                PitcherChangeReason.PitchLimit => DecisionReasonCode.PitchLimit,
                PitcherChangeReason.TimesThroughOrder => DecisionReasonCode.TimesThroughOrder,
                PitcherChangeReason.Performance => DecisionReasonCode.Performance,
                PitcherChangeReason.HighLeverage => DecisionReasonCode.HighLeverage,
                PitcherChangeReason.Matchup => DecisionReasonCode.Matchup,
                PitcherChangeReason.Injury => DecisionReasonCode.Injury,
                PitcherChangeReason.ScheduledUsage => DecisionReasonCode.ScheduledUsage,
                PitcherChangeReason.DefensiveStrategy => DecisionReasonCode.DefensiveStrategy,
                _ => DecisionReasonCode.Emergency
            };
        }
    }
}
