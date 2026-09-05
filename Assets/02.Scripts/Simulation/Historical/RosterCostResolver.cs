using System;
using Baseball.Core.Historical;

namespace Baseball.Simulation.Historical
{
    /// <summary>벤치 야수 5명을 제외한 주전 야수 9명과 투수 11명의 Canonical Cost 합계다.</summary>
    public readonly struct RosterCostBreakdown
    {
        public RosterCostBreakdown(int startingHitterCost, int pitcherCost)
        {
            if (startingHitterCost < 0)
                throw new ArgumentOutOfRangeException(nameof(startingHitterCost));
            if (pitcherCost < 0)
                throw new ArgumentOutOfRangeException(nameof(pitcherCost));
            StartingHitterCost = startingHitterCost;
            PitcherCost = pitcherCost;
        }

        public int StartingHitterCost { get; }
        public int PitcherCost { get; }
        public int TotalCost => StartingHitterCost + PitcherCost;
    }

    /// <summary>카드 Edition과 무관한 PlayerSeason Cost로 프야매식 20인 로스터 비용을 계산한다.</summary>
    public sealed class RosterCostResolver
    {
        private readonly ActiveRosterCompositionRule _rule;

        public RosterCostResolver(ActiveRosterCompositionRule rule = null)
        {
            _rule = rule ?? ActiveRosterCompositionRule.Standard;
        }

        /// <summary>주전 야수 슬롯과 모든 투수 슬롯만 합산하며 벤치 야수는 제외한다.</summary>
        public RosterCostBreakdown Resolve(CurrentRosterState roster, WorldCardCatalog catalog)
        {
            if (roster == null)
                throw new ArgumentNullException(nameof(roster));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            int startingHitterCost = 0;
            int pitcherCost = 0;
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (!_rule.IsStartingHitterRole(entry.Role) && !_rule.IsPitcherRole(entry.Role))
                    continue;
                if (!catalog.TryGetCard(entry.CardId, out PlayerCardDefinition card))
                    throw new ArgumentException($"카탈로그에 없는 카드입니다: {entry.CardId}", nameof(roster));

                int cost = catalog.GetPlayerSeason(card).Cost;
                if (_rule.IsStartingHitterRole(entry.Role))
                    startingHitterCost += cost;
                else
                    pitcherCost += cost;
            }
            return new RosterCostBreakdown(startingHitterCost, pitcherCost);
        }
    }
}
