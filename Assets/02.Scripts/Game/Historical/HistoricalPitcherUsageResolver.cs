using System;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Teams;

namespace Baseball.Game.Historical
{
    /// <summary>역사 베이크와 구단주 경기가 동일한 원기록 기반 투구 용량·회복력을 사용한다.</summary>
    internal static class HistoricalPitcherUsageResolver
    {
        public static (double Capacity, double Recovery) Resolve(PlayerSeasonDefinition season,
            PitcherRole assignedRole, HistoricalPitcherUsageBalance balance)
        {
            if (season.HistoricalPitchingAppearances <= 0 || season.HistoricalPitchingOuts <= 0 ||
                season.HistoricalTeamGames <= 0) return (1d, 1d);
            bool starter = assignedRole == PitcherRole.Starter;
            double innings = season.HistoricalPitchingOuts / 3d;
            double capacityBaseline = starter ? balance.StarterInningsPerAppearance : balance.RelieverInningsPerAppearance;
            double recoveryBaseline = starter ? balance.StarterInningsPerTeamGame : balance.RelieverInningsPerTeamGame;
            double Clamp(double value) => Math.Max(balance.MinimumMultiplier, Math.Min(balance.MaximumMultiplier, value));
            return (Clamp(innings / season.HistoricalPitchingAppearances / capacityBaseline),
                Clamp(innings / season.HistoricalTeamGames / recoveryBaseline));
        }
    }
}
