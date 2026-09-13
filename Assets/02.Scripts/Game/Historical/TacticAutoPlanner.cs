using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Career;

namespace Baseball.Game.Historical
{
    public enum TacticAutoVenue { All, Home, Away }
    public enum TacticAutoPolicy { FillEmptySlots, Replace }
    public enum TacticAutoPriority { LowerTier, HigherTier, LargerStock }

    /// <summary>이번 자동 배치에만 사용하는 경기·카드 필터와 소비 방침이다.</summary>
    public sealed class TacticAutoOptions
    {
        public int GameCount { get; set; } = TacticAutoPlanner.MaximumPlanningGames;
        public TacticAutoVenue Venue { get; set; }
        public TacticCardCategory? Category { get; set; }
        public TacticTier MaximumTier { get; set; } = TacticTier.Signature;
        public bool CanUseDisruption { get; set; }
        public TacticAutoPolicy Policy { get; set; }
        public TacticAutoPriority Priority { get; set; }
        public int CardsPerGame { get; set; } = 2;
    }

    /// <summary>미리보기의 원래 구성과 적용할 구성을 함께 보관한다.</summary>
    public sealed class TacticAutoGamePlan
    {
        public int GameId { get; }
        public IReadOnlyList<string> OriginalIds { get; }
        public IReadOnlyList<string> CardIds { get; }
        public bool HasChanges { get; }
        public TacticAutoGamePlan(int gameId, IReadOnlyList<string> original, IReadOnlyList<string> cards)
        {
            GameId = gameId;
            OriginalIds = new List<string>(original).AsReadOnly();
            CardIds = new List<string>(cards).AsReadOnly();
            HasChanges = original.Count != cards.Count;
            for (int i = 0; i < Math.Min(original.Count, cards.Count); i++)
                if (original[i] != cards[i]) HasChanges = true;
        }
    }

    /// <summary>예정 경기 예약을 먼저 확보한 뒤 일정 순으로 결정론적 자동 배치안을 만든다.</summary>
    public static class TacticAutoPlanner
    {
        // 구단주 운영의 1주 경기 수를 공유하여 유학·주간 결산과 같은 기간을 예약한다.
        public const int MaximumPlanningWeeks = 3;
        public const int MaximumPlanningGames = MaximumPlanningWeeks * ManagerLiveSeasonState.GamesPerOperationWeek;

        public static IReadOnlyList<TacticAutoGamePlan> Build(
            IReadOnlyList<ScheduledGameState> games, int playerTeamId, ScheduledGameState nextGame,
            IReadOnlyList<string> defaultIds, IReadOnlyList<TacticCardDefinition> definitions,
            Func<string, int> getCount, TacticAutoOptions options, int planningLimit)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.GameCount < 1 || options.GameCount > planningLimit || options.CardsPerGame < 1 || options.CardsPerGame > 2)
                throw new ArgumentException("경기 범위와 장착 수를 다시 선택해 주세요.");
            var targets = new List<ScheduledGameState>();
            var available = new Dictionary<string, int>(StringComparer.Ordinal);
            var catalog = new Dictionary<string, TacticCardDefinition>(StringComparer.Ordinal);
            var candidates = new List<TacticCardDefinition>();
            foreach (var card in definitions)
            {
                available[card.CardId] = getCount(card.CardId);
                catalog[card.CardId] = card;
                if ((!options.Category.HasValue || options.Category == card.Category) &&
                    card.TacticTier <= options.MaximumTier && (options.CanUseDisruption || !card.IsDisruption))
                    candidates.Add(card);
            }
            int future = 0;
            foreach (var game in games)
            {
                if (game.IsCompleted || !game.IncludesTeam(playerTeamId)) continue;
                bool isTarget = future++ < options.GameCount &&
                    (options.Venue == TacticAutoVenue.All || (game.HomeTeamId == playerTeamId) == (options.Venue == TacticAutoVenue.Home));
                if (isTarget) targets.Add(game);
                if (isTarget && options.Policy == TacticAutoPolicy.Replace) continue;
                foreach (string id in GetEffectiveIds(game, nextGame, defaultIds))
                    if (available.ContainsKey(id)) available[id]--;
            }
            var result = new List<TacticAutoGamePlan>();
            foreach (var game in targets)
            {
                var original = GetEffectiveIds(game, nextGame, defaultIds);
                var selected = options.Policy == TacticAutoPolicy.FillEmptySlots ? new List<string>(original) : new List<string>();
                while (selected.Count < options.CardsPerGame)
                {
                    candidates.Sort((a, b) => Compare(a, b, available, options.Priority));
                    TacticCardDefinition chosen = null;
                    foreach (var card in candidates)
                    {
                        if (available[card.CardId] <= 0 || selected.Contains(card.CardId)) continue;
                        bool hasDisruption = false;
                        foreach (string id in selected)
                            if (catalog.TryGetValue(id, out var equipped) && equipped.IsDisruption) hasDisruption = true;
                        if (card.IsDisruption && hasDisruption) continue;
                        chosen = card;
                        break;
                    }
                    if (chosen == null) break;
                    selected.Add(chosen.CardId);
                    available[chosen.CardId]--;
                }
                result.Add(new TacticAutoGamePlan(game.GameId, original, selected));
            }
            return result.AsReadOnly();
        }

        /// <summary>미설정 다음 경기의 기본 프리셋도 예약 수량에 포함한다.</summary>
        public static IReadOnlyList<string> GetEffectiveIds(ScheduledGameState game, ScheduledGameState nextGame, IReadOnlyList<string> defaultIds) =>
            game.HasTacticPlan ? game.PlannedTacticCardIds : ReferenceEquals(game, nextGame) ? defaultIds : Array.Empty<string>();

        private static int Compare(TacticCardDefinition a, TacticCardDefinition b, Dictionary<string, int> stock, TacticAutoPriority priority)
        {
            int order = priority == TacticAutoPriority.LargerStock ? stock[b.CardId].CompareTo(stock[a.CardId]) :
                priority == TacticAutoPriority.HigherTier ? b.TacticTier.CompareTo(a.TacticTier) : a.TacticTier.CompareTo(b.TacticTier);
            return order != 0 ? order : string.CompareOrdinal(a.CardId, b.CardId);
        }
    }
}
