#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Baseball.Core.Growth;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        private readonly OwnerCheatService _cheatService = new OwnerCheatService();

        /// <summary>개발 치트로 구단 자원을 증가시키고 열린 화면을 즉시 갱신한다.</summary>
        public void CheatIncreaseResources(long money, int scoutingPoints, int developmentPoints)
        {
            _cheatService.IncreaseResources(RequireRuntime(), money, scoutingPoints, developmentPoints);
            NotifyRuntimeChanged();
        }

        /// <summary>개발 치트로 특정 선수 카드 한 장을 지급한다.</summary>
        public OwnerCheatGrantResult CheatAcquireCard(string cardId)
        {
            OwnerCheatGrantResult result = _cheatService.AcquireCard(RequireRuntime(), cardId);
            NotifyRuntimeChanged();
            return result;
        }

        /// <summary>개발 치트로 특정 연도·구단의 모든 활성 카드를 N장씩 지급한다.</summary>
        public OwnerCheatGrantResult CheatAcquireCards(int originYear, string franchiseId, int countPerCard)
        {
            OwnerCheatGrantResult result = _cheatService.AcquireCards(
                RequireRuntime(), originYear, franchiseId, countPerCard);
            NotifyRuntimeChanged();
            return result;
        }

        /// <summary>개발 치트로 현재 월드의 모든 활성 카드를 한 장씩 지급한다.</summary>
        public OwnerCheatGrantResult CheatAcquireAllCards()
        {
            OwnerCheatGrantResult result = _cheatService.AcquireAllCards(RequireRuntime());
            NotifyRuntimeChanged();
            return result;
        }

        /// <summary>개발 치트로 특정 스킬 블록 인스턴스 하나를 지급한다.</summary>
        public OwnerCheatGrantResult CheatAcquireSkillBlock(string definitionId)
        {
            OwnerCheatGrantResult result = _cheatService.AcquireSkillBlock(
                RequireRuntime(), _balance.Growth.SkillBlocks, definitionId);
            NotifyRuntimeChanged();
            return result;
        }

        /// <summary>개발 치트로 특정 등급의 모든 스킬 블록을 N개씩 지급한다.</summary>
        public OwnerCheatGrantResult CheatAcquireSkillBlocks(SkillBlockRarity rarity, int countPerDefinition)
        {
            OwnerCheatGrantResult result = _cheatService.AcquireSkillBlocks(
                RequireRuntime(), _balance.Growth.SkillBlocks, rarity, countPerDefinition);
            NotifyRuntimeChanged();
            return result;
        }

        /// <summary>개발 치트로 모든 스킬 블록을 N개씩 지급한다.</summary>
        public OwnerCheatGrantResult CheatAcquireAllSkillBlocks(int countPerDefinition)
        {
            OwnerCheatGrantResult result = _cheatService.AcquireAllSkillBlocks(
                RequireRuntime(), _balance.Growth.SkillBlocks, countPerDefinition);
            NotifyRuntimeChanged();
            return result;
        }
    }
}
#endif
