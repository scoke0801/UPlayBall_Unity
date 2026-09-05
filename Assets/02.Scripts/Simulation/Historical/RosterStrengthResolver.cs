using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Simulation.Historical
{
    /// <summary>현재 1군 선수들의 시즌 기본 능력 평균이며 가격과 경기 당일 보정은 포함하지 않는다.</summary>
    public sealed class RosterStrengthBreakdown
    {
        internal RosterStrengthBreakdown(int hitterTotal, int hitterCount, int pitcherTotal, int pitcherCount)
        {
            HitterCount = hitterCount;
            PitcherCount = pitcherCount;
            HitterStrength = Average(hitterTotal, hitterCount);
            PitcherStrength = Average(pitcherTotal, pitcherCount);
            Overall = Average(hitterTotal + pitcherTotal, hitterCount + pitcherCount);
        }

        public int HitterCount { get; }
        public int PitcherCount { get; }
        public int PlayerCount => HitterCount + PitcherCount;
        public double? HitterStrength { get; }
        public double? PitcherStrength { get; }
        public double? Overall { get; }

        private static double? Average(int total, int count) => count == 0 ? (double?)null : total / (6d * count);
    }

    /// <summary>Cost 대신 유형별 6능력 평균으로 현재 로스터의 기본 전력을 평가한다.</summary>
    public sealed class RosterStrengthResolver
    {
        /// <summary>벤치를 포함한 현재 등록 선수를 동일 가중으로 평가하고 빈 구성은 미평가로 반환한다.</summary>
        public RosterStrengthBreakdown Resolve(CurrentRosterState roster, WorldCardCatalog catalog)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            int hitterTotal = 0, pitcherTotal = 0, hitterCount = 0, pitcherCount = 0;
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (!catalog.TryGetCard(entry.CardId, out PlayerCardDefinition card))
                    throw new ArgumentException($"카탈로그에 없는 카드입니다: {entry.CardId}", nameof(roster));
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                AbilityRatings ratings = season.CreateBaseAttributes();
                bool isHitter = season.PlayerType == PlayerType.Batter;
                int total = 0;
                for (int abilityIndex = 0; abilityIndex < PlayerAbilityCatalog.AbilityCount; abilityIndex++)
                {
                    var ability = (PlayerAbility)abilityIndex;
                    if (isHitter ? PlayerAbilityCatalog.IsBatterAbility(ability) : PlayerAbilityCatalog.IsPitcherAbility(ability))
                        total += ratings.Get(ability);
                }

                // 역사 성적 대조에서 검증한 ReferenceStrength와 같은 선수별 동일 가중이다.
                // 정수 합계를 마지막에 나눠 로스터 순서와 선수별 반올림에 의한 차이를 방지한다.
                if (isHitter) { hitterTotal += total; hitterCount++; }
                else { pitcherTotal += total; pitcherCount++; }
            }
            return new RosterStrengthBreakdown(hitterTotal, hitterCount, pitcherTotal, pitcherCount);
        }
    }
}
