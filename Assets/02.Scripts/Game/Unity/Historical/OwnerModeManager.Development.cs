using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;
using UnityEngine;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        private OwnerDevelopmentBalance _developmentBalance;
        public OwnerDevelopmentBalance GetDevelopmentBalance()
        {
            if (_developmentBalance != null) return _developmentBalance;
            var asset = Resources.Load<TextAsset>("NewGame/OwnerDevelopment");
            if (asset == null) throw new InvalidOperationException("성장 관리 데이터를 읽을 수 없습니다.");
            var balance = JsonUtility.FromJson<OwnerDevelopmentBalance>(asset.text);
            balance.Validate(); _developmentBalance = balance; return balance;
        }
        private void CommitDevelopment(Action<ManagerHistoricalRuntimeState> action)
        {
            CommitGrowthChange(runtime => { action(runtime); return true; });
            InvalidatePregame(); NotifyRuntimeChanged();
        }
        public void CancelStudy(string cardId) => CommitDevelopment(runtime => OwnerStudyCancellationService.Cancel(runtime, cardId));
        public void CorrectCard(string cardId, PlayerAbility decrease, PlayerAbility increase, int amount, int expectedLedgerCount) =>
            CommitDevelopment(runtime => OwnerPermanentGrowthService.Correct(runtime, cardId, decrease, increase, amount,
                GetDevelopmentBalance().correctionCost, expectedLedgerCount));
        public void TrainWithPartner(string cardId, string partnerId) => CommitDevelopment(runtime =>
            OwnerPermanentGrowthService.Partner(runtime, cardId, partnerId, GetDevelopmentBalance().partnerCost, GetDevelopmentBalance().partner));
        public void ResearchSkillBlocks() => CommitDevelopment(runtime => OwnerSkillResearchService.Research(runtime, _balance.Growth.SkillBlocks,
            GetDevelopmentBalance(), new Pcg32Random(runtime.WorldHistory.WorldHistorySeed, (ulong)runtime.PlayerGrowth.Inventory.ResearchCount + 1701UL)));
        private SkillBlockDefinition GetDevelopmentBlock(string id)
        {
            foreach (var block in _balance.Growth.SkillBlocks) if (block.BlockId == id) return block;
            throw new InvalidOperationException("스킬 블록을 선택하세요.");
        }
        public void OpenSkillSelectionBox(string id) => CommitDevelopment(runtime => OwnerSkillResearchService.OpenSelectionBox(runtime, GetDevelopmentBlock(id)));
        /// <summary>저장 성공 후 실제 추첨 결과를 반환한다.</summary>
        public SkillBlockDefinition FuseSkillBlocks(int firstId, int secondId, SkillFusionFocus focus)
        {
            SkillBlockDefinition result = null;
            CommitDevelopment(runtime => result = OwnerSkillResearchService.Fuse(runtime, _balance.Growth.SkillBlocks,
                firstId, secondId, GetDevelopmentBalance().fusion, focus,
                new Pcg32Random(runtime.WorldHistory.WorldHistorySeed, (ulong)runtime.PlayerGrowth.Inventory.FusionCount + 2701UL)));
            return result;
        }
        public void CraftSkillBlock(string id) => CommitDevelopment(runtime =>
            OwnerSkillResearchService.Craft(runtime, GetDevelopmentBlock(id), GetDevelopmentBalance().fusion));
        public void SelectSlogan(string id)
        {
            OwnerSloganDefinition selected = null;
            foreach (var slogan in GetDevelopmentBalance().slogans) if (slogan.id == id) selected = slogan;
            if (selected == null) throw new InvalidOperationException("슬로건을 선택하세요.");
            CommitDevelopment(runtime => OwnerSloganService.Select(runtime, selected));
        }
    }
}
