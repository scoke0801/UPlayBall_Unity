using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    public enum PitcherFatigueBand
    {
        Normal = 0,
        Tiring = 1,
        Fatigued = 2,
        Limit = 3,
        Overloaded = 4
    }

    /// <summary>
    /// 원본 능력치를 바꾸지 않고 현재 투구에 적용할 실효 투수 능력치를 보관한다.
    /// </summary>
    public readonly struct EffectivePitcherRatings
    {
        public EffectivePitcherRatings(
            double velocity,
            double stuff,
            double breaking,
            double control,
            double mental)
        {
            Velocity = velocity;
            Stuff = stuff;
            Breaking = breaking;
            Control = control;
            Mental = mental;
        }

        public double Velocity { get; }
        public double Stuff { get; }
        public double Breaking { get; }
        public double Control { get; }
        public double Mental { get; }
    }

    /// <summary>
    /// 한 투수의 경기 내 물리 피로·단기 압박·실적·타자별 대면 횟수를 누적한다.
    /// </summary>
    public sealed class PitcherGameState
    {
        private readonly Dictionary<int, int> _batterMatchups = new Dictionary<int, int>(12);

        public PitcherGameState(PitcherRosterEntry rosterEntry, double effectiveCapacity)
        {
            RosterEntry = rosterEntry ?? throw new ArgumentNullException(nameof(rosterEntry));
            if (effectiveCapacity <= 0d)
                throw new ArgumentOutOfRangeException(nameof(effectiveCapacity));
            EffectiveCapacity = effectiveCapacity;
        }

        public PitcherRosterEntry RosterEntry { get; }
        public Player Player => RosterEntry.Player;
        public PitcherRole Role => RosterEntry.Role;
        public double EffectiveCapacity { get; }
        public int PitchCount { get; private set; }
        public int BattersFaced { get; private set; }
        public int InningsStarted { get; private set; }
        public int RunsAllowed { get; private set; }
        public int HitsAllowed { get; private set; }
        public int WalksAllowed { get; private set; }
        public double CurrentInningStress { get; private set; }
        public int ConsecutiveBattersReached { get; private set; }
        public int InheritedRunners { get; internal set; }
        public int InheritedRunnersScored { get; internal set; }
        public bool HasEntered { get; internal set; }
        public bool HasBeenRemoved { get; internal set; }
        public double FatigueRatio => PitchCount / EffectiveCapacity;
        public int TimesThroughOrder => BattersFaced / 9 + 1;

        public void StartInning()
        {
            InningsStarted++;
        }

        public int BeginPlateAppearance(int batterId)
        {
            if (!_batterMatchups.TryGetValue(batterId, out int previousCount))
                previousCount = 0;
            int currentCount = previousCount + 1;
            _batterMatchups[batterId] = currentCount;
            return currentCount;
        }

        public void RecordPitch()
        {
            PitchCount++;
        }

        public void RecordPlateAppearance(bool reachedBase, bool wasHit, bool wasWalk, int runsAllowed)
        {
            BattersFaced++;
            if (wasHit) HitsAllowed++;
            if (wasWalk) WalksAllowed++;
            RunsAllowed += runsAllowed;
            ConsecutiveBattersReached = reachedBase ? ConsecutiveBattersReached + 1 : 0;
        }

        public void AddStress(double amount)
        {
            CurrentInningStress = Clamp01(CurrentInningStress + amount);
        }

        public void RecoverStress(double amount)
        {
            CurrentInningStress = Clamp01(CurrentInningStress - amount);
        }

        public void EndInning(PitcherStressBalance balance)
        {
            double mentalRecovery = Player.PitcherAttributes.Mental * 0.002d;
            CurrentInningStress *= Math.Max(0d, 1d - balance.InningRecovery - mentalRecovery);
            ConsecutiveBattersReached = 0;
        }

        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            if (value > 1d) return 1d;
            return value;
        }
    }

    /// <summary>
    /// 경기 종료 뒤 시즌 레이어가 최근 부하와 회복을 갱신할 투수 사용량이다.
    /// </summary>
    public readonly struct PitcherUsageReport
    {
        public PitcherUsageReport(
            int playerId,
            int pitchCount,
            int highLeverageBattersFaced,
            int overloadPitches,
            int inningsStarted,
            PitcherRole role,
            int inheritedRunners,
            int inheritedRunnersScored)
        {
            PlayerId = playerId;
            PitchCount = pitchCount;
            HighLeverageBattersFaced = highLeverageBattersFaced;
            OverloadPitches = overloadPitches;
            InningsStarted = inningsStarted;
            Role = role;
            InheritedRunners = inheritedRunners;
            InheritedRunnersScored = inheritedRunnersScored;
        }

        public int PlayerId { get; }
        public int PitchCount { get; }
        public int HighLeverageBattersFaced { get; }
        public int OverloadPitches { get; }
        public int InningsStarted { get; }
        public PitcherRole Role { get; }
        public int InheritedRunners { get; }
        public int InheritedRunnersScored { get; }
    }
}
