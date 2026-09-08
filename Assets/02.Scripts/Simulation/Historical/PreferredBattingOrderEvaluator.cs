using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Simulation.Historical
{
    /// <summary>원본 Core25 주전의 주력·장타 적합도 합을 최대화해 상위 2·클린업 3·하위 4명을 발급한다.</summary>
    public static class PreferredBattingOrderEvaluator
    {
        /// <summary>주전은 구단별 정원으로, 그 외 선수는 원본 공격 능력치의 상대 우세로 평가한다.</summary>
        public static Dictionary<string, PreferredBattingOrder> Evaluate(
            IReadOnlyList<PlayerSeasonDefinition> seasons, IReadOnlyList<TeamSeasonDefinition> teams)
        {
            if (seasons == null) throw new ArgumentNullException(nameof(seasons));
            var result = new Dictionary<string, PreferredBattingOrder>(StringComparer.Ordinal);
            var normalSeasons = new Dictionary<string, PlayerSeasonDefinition>(StringComparer.Ordinal);
            foreach (PlayerSeasonDefinition season in seasons)
            {
                AbilityRatings ratings = season.CreateBaseAttributes();
                int speed = ratings.Get(PlayerAbility.Speed);
                int power = ratings.Get(PlayerAbility.Power);
                int contact = ratings.Get(PlayerAbility.Contact);
                result.Add(season.PlayerSeasonId, season.PlayerType == PlayerType.Pitcher
                    ? PreferredBattingOrder.None
                    : speed > power && speed > contact ? PreferredBattingOrder.Upper
                    : power > contact ? PreferredBattingOrder.Cleanup : PreferredBattingOrder.Lower);
                normalSeasons.Add(PlayerCardDefinition.CreateStableCardId(season.PlayerSeasonId, PlayerCardEdition.Normal), season);
            }
            if (teams == null) return result;
            foreach (TeamSeasonDefinition team in teams)
            {
                var starters = new List<PlayerSeasonDefinition>(9);
                // Core25의 첫 9장은 수비 포지션별 주전이다. 현재 로스터나 카드 강화로 재평가하지 않는다.
                for (int index = 0; index < 9; index++)
                {
                    if (!normalSeasons.TryGetValue(team.Core25CardIds[index], out var season) ||
                        season.PlayerType != PlayerType.Batter || season.OriginTeamSeasonKey != team.TeamSeasonKey)
                        throw new ArgumentException("선호 타순 평가에는 같은 구단 원본 주전 타자 9명이 필요합니다.", nameof(teams));
                    starters.Add(season);
                }
                AssignStarters(starters, result);
            }
            return result;
        }

        /// <summary>현재 주전의 선호 일치 인원을 최대로 유지하고 남는 타순을 결정론적으로 채운다.</summary>
        public static string[] CreateBattingOrder(IReadOnlyList<string> cardIds, WorldCardCatalog catalog)
        {
            if (cardIds == null || cardIds.Count != 9) throw new ArgumentException("주전 9명이 필요합니다.", nameof(cardIds));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var candidates = new List<string>(cardIds);
            candidates.Sort(StringComparer.Ordinal);
            var result = new string[9];
            // 먼저 모든 구간의 일치 자리를 확보해야 넘친 상위 타자가 클린업 자리를 빼앗지 않는다.
            for (int slot = 0; slot < result.Length; slot++)
            {
                PreferredBattingOrder group = PreferredBattingOrderRule.GetGroup(slot + 1);
                int selected = -1;
                int bestRating = int.MinValue;
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (!catalog.TryGetCard(candidates[index], out var card)) throw new ArgumentException("카드 원본이 없습니다.", nameof(cardIds));
                    if (card.PreferredBattingOrder != group) continue;
                    var ability = group == PreferredBattingOrder.Upper ? PlayerAbility.Speed
                        : group == PreferredBattingOrder.Cleanup ? PlayerAbility.Power : PlayerAbility.Contact;
                    int rating = catalog.GetPlayerSeason(card).CreateBaseAttributes().Get(ability);
                    if (rating <= bestRating) continue;
                    selected = index;
                    bestRating = rating;
                }
                if (selected < 0) continue;
                result[slot] = candidates[selected];
                candidates.RemoveAt(selected);
            }
            int remaining = 0;
            for (int slot = 0; slot < result.Length; slot++)
                if (result[slot] == null) result[slot] = candidates[remaining++];
            return result;
        }

        private static void AssignStarters(List<PlayerSeasonDefinition> starters,
            Dictionary<string, PreferredBattingOrder> result)
        {
            starters.Sort((left, right) => string.CompareOrdinal(left.PlayerSeasonId, right.PlayerSeasonId));
            var speed = new int[9];
            var power = new int[9];
            for (int index = 0; index < 9; index++)
            {
                var ratings = starters[index].CreateBaseAttributes();
                speed[index] = ratings.Get(PlayerAbility.Speed);
                power[index] = ratings.Get(PlayerAbility.Power);
            }
            int bestScore = int.MinValue;
            int upperMask = 0;
            int cleanupMask = 0;
            // 양쪽에 적합한 선수를 먼저 소진하지 않도록 가능한 2명/3명 배정을 모두 비교한다.
            // 동점은 정렬된 시즌 ID 조합 중 처음 것을 택해 입력 순서와 무관하게 고정한다.
            for (int first = 0; first < 8; first++)
            for (int second = first + 1; second < 9; second++)
            {
                int upper = (1 << first) | (1 << second);
                var candidates = new List<int>(7);
                for (int index = 0; index < 9; index++)
                    if ((upper & (1 << index)) == 0) candidates.Add(index);
                candidates.Sort((left, right) => power[left] != power[right]
                    ? power[right].CompareTo(power[left]) : left.CompareTo(right));
                int score = speed[first] + speed[second] + power[candidates[0]] + power[candidates[1]] + power[candidates[2]];
                if (score <= bestScore) continue;
                bestScore = score;
                upperMask = upper;
                cleanupMask = (1 << candidates[0]) | (1 << candidates[1]) | (1 << candidates[2]);
            }
            for (int index = 0; index < 9; index++)
                result[starters[index].PlayerSeasonId] = (upperMask & (1 << index)) != 0
                    ? PreferredBattingOrder.Upper : (cleanupMask & (1 << index)) != 0
                    ? PreferredBattingOrder.Cleanup : PreferredBattingOrder.Lower;
        }
    }
}
