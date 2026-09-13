using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>저장된 성장 출처를 내부 ID 없이 선수에게 설명한다.</summary>
    public static class OwnerGrowthHistoryFormatter
    {
        public static string Format(OwnerGrowthLedger ledger)
        {
            var text = new StringBuilder();
            var entries = ledger.Entries;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var changes = new StringBuilder();
                for (int stat = 0; stat < PlayerAbilityCatalog.AbilityCount; stat++)
                {
                    int amount = entry.Get((PlayerAbility)stat);
                    if (amount == 0) continue;
                    if (changes.Length > 0) changes.Append(" · ");
                    changes.Append(GetAbilityName((PlayerAbility)stat)).Append(' ').Append(amount > 0 ? "+" : "").Append(amount);
                }
                if (changes.Length == 0 && entry.Source != OwnerGrowthSource.Mentoring && entry.Source != OwnerGrowthSource.Support) continue;
                if (changes.Length == 0) changes.Append(entry.Source == OwnerGrowthSource.Mentoring ? "지도 참여 완료" : "컨디션 지원");
                text.Append(entry.DisplayName).Append(entry.IsActive ? "" : " [효과 종료]");
                if (entry.RemainingGames > 0 && entry.IsActive) text.Append(" · ").Append(entry.RemainingGames).Append("경기 남음");
                text.Append('\n').Append(changes).Append("\n\n");
            }
            return text.Length == 0 ? "아직 완료한 성장 과정이 없습니다." : text.ToString();
        }
        public static string GetAbilityName(PlayerAbility ability) => PlayerAbilityCatalog.GetDisplayName(ability);
        /// <summary>장착 블록과 현재 인접 세트도 영구 성장 기록과 함께 조회한다.</summary>
        public static string Format(OwnedPlayerCardState owned, Baseball.Core.Balance.GrowthBalanceTable growth)
        {
            var result = new StringBuilder();
            foreach (var placement in owned.SkillBoard.Placements)
                foreach (var block in growth.SkillBlocks)
                    if (block.BlockId == placement.Instance.DefinitionId)
                    {
                        result.Append("장착 블록 · ").Append(block.ShapeCells.Length).Append("칸\n");
                        foreach (var bonus in block.AbilityBonuses) result.Append(GetAbilityName(bonus.Ability)).Append(" +").Append(bonus.Amount).Append(" · ");
                        result.Append("원본 효과\n\n");
                    }
            var board = new Baseball.Simulation.Growth.SkillBoardService(growth.SkillBoard,growth.SkillBlocks);
            for (int i = 0; i < PlayerAbilityCatalog.AbilityCount; i++)
            {
                int bonus = Baseball.Simulation.Historical.OwnerSkillSetResolver.GetBonus(owned.SkillBoard.Placements,board,growth.SkillBlocks,(PlayerAbility)i);
                if (bonus > 0) result.Append("인접 세트 · 같은 계열 3블록\n").Append(GetAbilityName((PlayerAbility)i)).Append(" +").Append(bonus).Append("\n\n");
            }
            result.Append(Format(owned.Training.Ledger)); return result.ToString();
        }
    }
}
