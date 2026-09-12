using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>저장된 카드별 성장 상태를 표시하며 다른 연도·종류의 카드에 이력을 전용하지 않는다.</summary>
    public static class OwnerCardGrowthBadgeBuilder
    {
        /// <summary>실제 보유 카드의 유학 시작 이력과 진행 프로젝트를 읽는다.</summary>
        public static PlayerCardGrowthBadgeModel Build(ManagerHistoricalRuntimeState runtime, string cardId, Baseball.Core.Balance.GrowthBalanceTable balance = null)
        {
            if (runtime == null || string.IsNullOrEmpty(cardId) ||
                !runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState owned))
                return PlayerCardGrowthBadgeModel.Empty;
            return Build(owned, runtime.PlayerGrowth, balance);
        }

        /// <summary>완료 후 프로젝트가 제거되어도 저장된 유학 이력으로 배지를 유지한다.</summary>
        public static PlayerCardGrowthBadgeModel Build(OwnedPlayerCardState owned, OwnerPlayerGrowthState growth, Baseball.Core.Balance.GrowthBalanceTable balance = null)
        {
            if (owned == null) return PlayerCardGrowthBadgeModel.Empty;
            int supportGames = 0;
            var boardRank = PlayerBoardBadgeRank.None;
            if (balance != null)
                foreach (var placement in owned.SkillBoard.Placements)
                    foreach (var definition in balance.SkillBlocks)
                        if (definition.BlockId == placement.Instance.DefinitionId)
                            boardRank = (PlayerBoardBadgeRank)System.Math.Max((int)boardRank, System.Math.Min(4, (int)definition.Rarity + 1));
            foreach (var modifier in owned.Training.Ledger.Entries)
                if (modifier.Source == OwnerGrowthSource.Support && modifier.IsActive)
                    supportGames = System.Math.Max(supportGames, modifier.RemainingGames);
            if (growth != null)
                foreach (CardStudyProjectState project in growth.StudyProjects)
                    if (string.Equals(project.CardId, owned.CardId, System.StringComparison.Ordinal))
                        return new PlayerCardGrowthBadgeModel(PlayerStudyBadgeState.InProgress,
                            $"유학 중 · {project.RemainingWeeks}주 남음", supportGames: supportGames, boardRank: boardRank);
            // LastStudySeason은 시작 시 저장된다. 진행 프로젝트가 없을 때만 완료 이력으로 표시한다.
            bool hasCompletedStudy = owned.LastStudySeason >= 0;
            for (int index = 0; !hasCompletedStudy && index < PlayerAbilityCatalog.AbilityCount; index++)
                hasCompletedStudy = owned.Training.GetStudyBonus((PlayerAbility)index) > 0;
            return hasCompletedStudy
                ? new PlayerCardGrowthBadgeModel(PlayerStudyBadgeState.Completed, supportGames: supportGames, boardRank: boardRank)
                : new PlayerCardGrowthBadgeModel(supportGames: supportGames, boardRank: boardRank);
        }
    }
}
