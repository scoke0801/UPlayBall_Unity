using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>Offline 발급 파일에서 읽은 카드·계보·레시피 묶음이다. Runtime은 선수를 다시 선정하지 않는다.</summary>
    public sealed class BakedSpecialCardContent
    {
        public BakedSpecialCardContent(IReadOnlyList<PlayerCardDefinition> cards,
            TeamColorLineageMap lineages, IReadOnlyList<SpecialRecruitRecipe> recipes)
        {
            if (cards == null || recipes == null) throw new ArgumentNullException();
            Cards = new List<PlayerCardDefinition>(cards).AsReadOnly();
            Lineages = lineages ?? throw new ArgumentNullException(nameof(lineages));
            Recipes = new List<SpecialRecruitRecipe>(recipes).AsReadOnly();
        }
        public IReadOnlyList<PlayerCardDefinition> Cards { get; }
        public TeamColorLineageMap Lineages { get; }
        public IReadOnlyList<SpecialRecruitRecipe> Recipes { get; }
    }

    /// <summary>Offline에서 확정한 특수 영입 재료 슬롯의 Normal 카드 후보를 보관한다.</summary>
    public sealed class SpecialRecruitMaterialGroup
    {
        public SpecialRecruitMaterialGroup(string groupId, IReadOnlyList<string> candidateCardIds)
        {
            if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentException("재료 그룹 ID가 필요합니다.");
            GroupId = groupId;
            if (candidateCardIds == null || candidateCardIds.Count == 0)
                throw new ArgumentException("재료 후보가 필요합니다.");
            var ids = new List<string>(candidateCardIds);
            ids.Sort(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
                if (string.IsNullOrWhiteSpace(ids[index]) || (index > 0 && ids[index] == ids[index - 1]))
                    throw new ArgumentException("빈 후보나 중복 후보는 허용하지 않습니다.");
            CandidateCardIds = ids.AsReadOnly();
        }

        public string GroupId { get; }
        public IReadOnlyList<string> CandidateCardIds { get; }
        public bool Contains(string cardId)
        {
            for (int index = 0; index < CandidateCardIds.Count; index++)
                if (string.Equals(CandidateCardIds[index], cardId, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    /// <summary>커리어 하이의 연도 후보 또는 레전드의 8개 재료 그룹을 사전 확정한다.</summary>
    public sealed class SpecialRecruitRecipe
    {
        public const int RequiredMaterialCount = 8;

        public SpecialRecruitRecipe(string targetCardId, IReadOnlyList<SpecialRecruitMaterialGroup> materialGroups)
        {
            if (string.IsNullOrWhiteSpace(targetCardId)) throw new ArgumentException("목표 카드 ID가 필요합니다.");
            if (materialGroups == null || materialGroups.Count != RequiredMaterialCount)
                throw new ArgumentException("특수 영입에는 정확히 8개 재료 슬롯이 필요합니다.");
            TargetCardId = targetCardId;
            var groups = new SpecialRecruitMaterialGroup[materialGroups.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < groups.Length; index++)
            {
                groups[index] = materialGroups[index] ?? throw new ArgumentException("빈 재료 그룹입니다.");
                if (!ids.Add(groups[index].GroupId)) throw new ArgumentException("재료 그룹 ID가 중복됩니다.");
            }
            MaterialGroups = Array.AsReadOnly(groups);
        }

        public string TargetCardId { get; }
        public IReadOnlyList<SpecialRecruitMaterialGroup> MaterialGroups { get; }
    }

    /// <summary>원본 Franchise ID를 바꾸지 않고 사전 저작된 게임용 계보만 조회한다.</summary>
    public sealed class TeamColorLineageMap
    {
        private readonly Dictionary<string, string> _lineages;

        public TeamColorLineageMap(IReadOnlyDictionary<string, string> lineages)
        {
            if (lineages == null) throw new ArgumentNullException(nameof(lineages));
            _lineages = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in lineages)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
                    throw new ArgumentException("구단과 계보 ID가 필요합니다.");
                _lineages.Add(entry.Key, entry.Value);
            }
        }

        public string GetRequired(string franchiseId)
        {
            if (!_lineages.TryGetValue(franchiseId, out string lineage))
                throw new ArgumentException("사전 정의되지 않은 구단 계보입니다.", nameof(franchiseId));
            return lineage;
        }

        public IReadOnlyList<string> GetFranchises(string lineageId)
        {
            var result = new List<string>();
            foreach (var pair in _lineages)
                if (pair.Value == lineageId) result.Add(pair.Key);
            result.Sort(StringComparer.Ordinal);
            return result.AsReadOnly();
        }
    }

    /// <summary>Runtime은 검증된 Bake 결과의 레시피와 계보만 소비한다.</summary>
    public sealed class SpecialCardCatalog
    {
        private readonly Dictionary<string, SpecialRecruitRecipe> _recipes;

        public SpecialCardCatalog(WorldCardCatalog cards, TeamColorLineageMap lineages,
            IReadOnlyList<SpecialRecruitRecipe> recipes)
        {
            Cards = cards ?? throw new ArgumentNullException(nameof(cards));
            Lineages = lineages ?? throw new ArgumentNullException(nameof(lineages));
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            _recipes = new Dictionary<string, SpecialRecruitRecipe>(StringComparer.Ordinal);
            foreach (SpecialRecruitRecipe recipe in recipes)
            {
                if (recipe == null) throw new ArgumentException("빈 레시피입니다.");
                ValidateRecipe(recipe);
                _recipes.Add(recipe.TargetCardId, recipe);
            }
            foreach (PlayerCardDefinition card in cards.Cards)
                if (card.IsUniqueOwnedCard && !_recipes.ContainsKey(card.CardId))
                    throw new ArgumentException("특수 영입 카드의 레시피가 누락되었습니다.");
        }

        public WorldCardCatalog Cards { get; }
        public TeamColorLineageMap Lineages { get; }
        public SpecialRecruitRecipe GetRequiredRecipe(string cardId) => _recipes[cardId];

        private void ValidateRecipe(SpecialRecruitRecipe recipe)
        {
            if (!Cards.TryGetCard(recipe.TargetCardId, out PlayerCardDefinition target) || !target.IsUniqueOwnedCard)
                throw new ArgumentException("특수 영입 대상이 아닌 카드입니다.");
            PlayerSeasonDefinition peak = Cards.GetPlayerSeason(target);
            if (Lineages.GetRequired(peak.OriginFranchiseId) != target.TeamColorLineageId)
                throw new ArgumentException("베이스 시즌의 계보가 일치하지 않습니다.");
            var years = new HashSet<int>();
            foreach (SpecialRecruitMaterialGroup group in recipe.MaterialGroups)
            {
                if (target.Edition == PlayerCardEdition.CareerHigh)
                {
                    var pool = recipe.MaterialGroups[0].CandidateCardIds;
                    if (pool.Count != group.CandidateCardIds.Count)
                        throw new ArgumentException("커리어 하이 슬롯은 같은 유효 시즌 후보 풀을 사용해야 합니다.");
                    for (int index = 0; index < pool.Count; index++)
                        if (pool[index] != group.CandidateCardIds[index])
                            throw new ArgumentException("커리어 하이 슬롯별 후보가 다릅니다.");
                }
                foreach (string id in group.CandidateCardIds)
                {
                    if (!Cards.TryGetCard(id, out PlayerCardDefinition card) || card.Edition != PlayerCardEdition.Normal)
                        throw new ArgumentException("재료는 존재하는 Normal 카드여야 합니다.");
                    PlayerSeasonDefinition season = Cards.GetPlayerSeason(card);
                    if (Lineages.GetRequired(season.OriginFranchiseId) != target.TeamColorLineageId)
                        throw new ArgumentException("다른 계보의 재료는 허용하지 않습니다.");
                    if (target.Edition == PlayerCardEdition.CareerHigh && season.PlayerPersonId != peak.PlayerPersonId)
                        throw new ArgumentException("커리어 하이 재료는 동일 선수여야 합니다.");
                    years.Add(season.OriginYear);
                }
            }
            if (target.Edition == PlayerCardEdition.CareerHigh && years.Count < SpecialRecruitRecipe.RequiredMaterialCount)
                throw new ArgumentException("커리어 하이에는 서로 다른 유효 연도 8개가 필요합니다.");
        }
    }
}
