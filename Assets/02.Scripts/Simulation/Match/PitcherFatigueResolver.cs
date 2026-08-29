using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// Stamina·역할·최근 부하로 투구 용량과 현재 실효 능력치를 계산한다.
    /// </summary>
    public sealed class PitcherFatigueResolver
    {
        private readonly PitcherFatigueBalance _fatigue;
        private readonly PitcherStressBalance _stress;
        private readonly BullpenManagementBalance _bullpen;

        public PitcherFatigueResolver(MatchBalanceTable balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            _fatigue = balance.PitcherFatigue;
            _stress = balance.PitcherStress;
            _bullpen = balance.BullpenManagement;
        }

        public PitcherGameState CreateState(PitcherRosterEntry entry)
        {
            return new PitcherGameState(entry, CalculateEffectiveCapacity(entry));
        }

        public double CalculateEffectiveCapacity(PitcherRosterEntry entry)
        {
            PitcherAttributes ratings = entry.Player.PitcherAttributes;
            double capacity = entry.Role == PitcherRole.Starter
                ? _fatigue.StarterBaseCapacity + ratings.Stamina * _fatigue.StarterStaminaWeight
                : _fatigue.RelieverBaseCapacity + ratings.Stamina * _fatigue.RelieverStaminaWeight;

            if (entry.Role == PitcherRole.LongRelief || entry.Role == PitcherRole.Swingman)
                capacity *= _fatigue.LongReliefMultiplier;
            else if (entry.Role == PitcherRole.Closer)
                capacity *= _fatigue.CloserMultiplier;

            double recentLoad = CalculateRecentLoad(entry.RecentWorkload);
            double staminaMitigation = 1d - ratings.Stamina * 0.003d;
            double workloadPenalty = recentLoad * staminaMitigation * 0.28d;
            double conditionMultiplier = 0.85d + entry.Condition * 0.0015d;
            return Math.Max(8d, capacity * conditionMultiplier - workloadPenalty);
        }

        public double CalculateRecentLoad(in RecentPitchingWorkload workload)
        {
            return workload.PreviousDayPitches +
                   workload.TwoDaysAgoPitches * _bullpen.RecentLoadDayTwoWeight +
                   workload.ThreeDaysAgoPitches * _bullpen.RecentLoadDayThreeWeight;
        }

        public EffectivePitcherRatings Resolve(PitcherGameState state, PitchingApproach approach)
        {
            PitcherAttributes source = state.Player.PitcherAttributes;
            double overload = CalculateOverload(state.FatigueRatio);
            double stressPenalty = CalculateStressPenalty(state);
            double velocity = source.Velocity - _fatigue.MaximumVelocityPenalty * Math.Pow(overload, 1.40d);
            double stuff = source.Stuff - _fatigue.MaximumStuffPenalty * Math.Pow(overload, 1.25d);
            double breaking = source.Breaking - _fatigue.MaximumBreakingPenalty * Math.Pow(overload, 1.25d);
            double control = source.Control -
                             _fatigue.MaximumControlPenalty * Math.Pow(overload, 1.35d) -
                             stressPenalty;
            ApplyApproach(approach, source, ref velocity, ref stuff, ref breaking, ref control);
            return new EffectivePitcherRatings(
                ClampRating(velocity),
                ClampRating(stuff),
                ClampRating(breaking),
                ClampRating(control),
                source.Mental);
        }

        public PitcherFatigueBand GetBand(double fatigueRatio)
        {
            if (fatigueRatio < 0.55d) return PitcherFatigueBand.Normal;
            if (fatigueRatio < 0.75d) return PitcherFatigueBand.Tiring;
            if (fatigueRatio < 0.90d) return PitcherFatigueBand.Fatigued;
            if (fatigueRatio < 1.05d) return PitcherFatigueBand.Limit;
            return PitcherFatigueBand.Overloaded;
        }

        private double CalculateOverload(double fatigueRatio)
        {
            double range = _fatigue.OverloadRatio - _fatigue.PenaltyStartRatio;
            if (fatigueRatio <= _fatigue.PenaltyStartRatio) return 0d;
            if (fatigueRatio >= _fatigue.OverloadRatio) return 1d;
            return (fatigueRatio - _fatigue.PenaltyStartRatio) / range;
        }

        private double CalculateStressPenalty(PitcherGameState state)
        {
            double mitigation = state.Player.PitcherAttributes.Mental * _stress.MentalMitigationWeight;
            return _stress.MaximumControlPenalty * state.CurrentInningStress * (1d - mitigation);
        }

        private static void ApplyApproach(
            PitchingApproach approach,
            PitcherAttributes source,
            ref double velocity,
            ref double stuff,
            ref double breaking,
            ref double control)
        {
            switch (approach)
            {
                case PitchingApproach.AttackZone:
                    control += 4d;
                    breaking -= 1d;
                    break;
                case PitchingApproach.Nibble:
                    control -= source.Control < 50 ? 5d : 2d;
                    breaking += 2d;
                    break;
                case PitchingApproach.Strikeout:
                    stuff += 3d + Math.Max(0d, source.Stuff - 50d) * 0.03d;
                    control -= 4d;
                    break;
                case PitchingApproach.PitchAround:
                    control -= 7d;
                    stuff += 1d;
                    break;
                case PitchingApproach.GroundBall:
                    breaking += 4d + Math.Max(0d, source.Breaking - 50d) * 0.03d;
                    stuff -= 1d;
                    break;
                case PitchingApproach.Balanced:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(approach));
            }
        }

        private static double ClampRating(double value)
        {
            if (value < 0d) return 0d;
            if (value > 100d) return 100d;
            return value;
        }
    }
}
