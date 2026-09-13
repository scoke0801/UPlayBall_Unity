using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>저장된 카드별 성장 상태를 표시하며 다른 연도·종류의 카드에 이력을 전용하지 않는다.</summary>
    public static class OwnerCardGrowthBadgeBuilder
    {
        private static readonly OwnerTraitTrainingBalance DefaultTraits = new OwnerTraitTrainingBalance();
        public static string TraitName(CardTraitKind kind) => kind == CardTraitKind.None ? "" : DefaultTraits.Get(kind).name;
        /// <summary>실제 보유 카드의 유학 시작 이력과 진행 프로젝트를 읽는다.</summary>
        public static PlayerCardGrowthBadgeModel Build(ManagerHistoricalRuntimeState runtime, string cardId, Baseball.Core.Balance.GrowthBalanceTable balance = null, OwnerTraitTrainingBalance traits = null, OwnerCardGrowthBalanceTable studies = null)
        {
            if (runtime == null || string.IsNullOrEmpty(cardId) ||
                !runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState owned))
                return PlayerCardGrowthBadgeModel.Empty;
            return Build(owned, runtime.PlayerGrowth, balance, traits, studies);
        }

        /// <summary>완료 후 프로젝트가 제거되어도 저장된 유학 이력으로 배지를 유지한다.</summary>
        public static PlayerCardGrowthBadgeModel Build(OwnedPlayerCardState owned, OwnerPlayerGrowthState growth, Baseball.Core.Balance.GrowthBalanceTable balance = null, OwnerTraitTrainingBalance traits = null, OwnerCardGrowthBalanceTable studies = null)
        {
            if (owned == null) return PlayerCardGrowthBadgeModel.Empty;
            int supportGames = 0;
            var traitRank = (PlayerTraitBadgeRank)owned.Trait.rank;
            string traitDescription = DescribeTrait(owned.Trait, traits ?? DefaultTraits);
            var boardRank = PlayerBoardBadgeRank.None;
            if (balance != null)
                foreach (var placement in owned.SkillBoard.Placements)
                    foreach (var definition in balance.SkillBlocks)
                        if (definition.BlockId == placement.Instance.DefinitionId)
                            boardRank = (PlayerBoardBadgeRank)System.Math.Max((int)boardRank, (int)ToBoardRank(definition.Rarity));
            string boardDescription = DescribeBoard(owned, balance);
            foreach (var modifier in owned.Training.Ledger.Entries)
                if (modifier.Source == OwnerGrowthSource.Support && modifier.IsActive)
                    supportGames = System.Math.Max(supportGames, modifier.RemainingGames);
            if (growth != null)
                foreach (CardStudyProjectState project in growth.StudyProjects)
                    if (string.Equals(project.CardId, owned.CardId, System.StringComparison.Ordinal))
                        return new PlayerCardGrowthBadgeModel(PlayerStudyBadgeState.InProgress,
                            DescribeStudyProject(project, studies) + DescribeStudyBonuses(owned), traitRank, traitDescription, supportGames: supportGames, boardRank: boardRank, boardDescription: boardDescription,
                            studyRank: FindStudyRank(owned, studies, project.ProgramId));
            // LastStudySeason은 시작 시 저장된다. 진행 프로젝트가 없을 때만 완료 이력으로 표시한다.
            bool hasCompletedStudy = owned.LastStudySeason >= 0;
            for (int index = 0; !hasCompletedStudy && index < PlayerAbilityCatalog.AbilityCount; index++)
                hasCompletedStudy = owned.Training.GetStudyBonus((PlayerAbility)index) > 0;
            return hasCompletedStudy
                ? new PlayerCardGrowthBadgeModel(PlayerStudyBadgeState.Completed,
                    DescribeCompletedStudies(owned, studies), traitRank: traitRank, traitDescription: traitDescription, supportGames: supportGames, boardRank: boardRank, boardDescription: boardDescription,
                    studyRank: FindStudyRank(owned, studies))
                : new PlayerCardGrowthBadgeModel(traitRank: traitRank, traitDescription: traitDescription, supportGames: supportGames, boardRank: boardRank, boardDescription: boardDescription);
        }

        private static PlayerBoardBadgeRank ToBoardRank(SkillBlockRarity rarity) => rarity switch
        {
            SkillBlockRarity.Normal => PlayerBoardBadgeRank.C,
            SkillBlockRarity.Rare => PlayerBoardBadgeRank.B,
            SkillBlockRarity.Elite => PlayerBoardBadgeRank.A,
            SkillBlockRarity.Unique => PlayerBoardBadgeRank.S,
            SkillBlockRarity.Legendary => PlayerBoardBadgeRank.SS,
            SkillBlockRarity.Mythic => PlayerBoardBadgeRank.SSS,
            _ => PlayerBoardBadgeRank.None
        };

        private static string DescribeBoard(OwnedPlayerCardState owned, Baseball.Core.Balance.GrowthBalanceTable balance)
        {
            if (balance == null || owned.SkillBoard.Placements.Count == 0) return "";
            var text = new System.Text.StringBuilder();
            var displayed = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var placement in owned.SkillBoard.Placements)
                foreach (var definition in balance.SkillBlocks)
                    if (definition.BlockId == placement.Instance.DefinitionId)
                    {
                        foreach (var bonus in definition.AbilityBonuses)
                        {
                            string effect = ToBoardRank(definition.Rarity) + " 등급 · " + PlayerAbilityCatalog.GetDisplayName(bonus.Ability);
                            if (!displayed.Add(effect)) continue;
                            if (text.Length > 0) text.Append('\n');
                            text.Append(effect);
                        }
                        break;
                    }
            return text.ToString();
        }

        private static string DescribeStudyProject(CardStudyProjectState project, OwnerCardGrowthBalanceTable studies)
        {
            CardStudyProgramDefinition program = null;
            if (studies != null)
                foreach (var candidate in studies.StudyPrograms)
                    if (candidate.ProgramId == project.ProgramId) { program = candidate; break; }
            var text = new System.Text.StringBuilder();
            text.Append("유학 중 · ").Append(project.RemainingWeeks).Append("주 남음\n");
            AppendStudyProgram(text, program);
            if (program == null) return text.ToString();
            text.Append("\n\n기본 수료 효과");
            foreach (var reward in program.Rewards)
                text.Append('\n').Append(PlayerAbilityCatalog.GetDisplayName(reward.Ability)).Append(" +").Append(reward.Amount);
            text.Append("\n성장 상한에 따라 실제 적용량이 달라질 수 있습니다.");
            return text.ToString();
        }

        private static string DescribeCompletedStudies(OwnedPlayerCardState owned, OwnerCardGrowthBalanceTable studies)
        {
            var text = new System.Text.StringBuilder();
            var entries = owned.Training.Ledger.Entries;
            var displayed = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry.Source != OwnerGrowthSource.OverseasTraining || !entry.IsActive || !displayed.Add(entry.DisplayName)) continue;
                CardStudyProgramDefinition program = null;
                // 완료 원장은 과정 표시명만 보존한다. 정확히 일치하는 정의가 없으면 목적지를 추정하지 않는다.
                if (studies != null)
                    foreach (var candidate in studies.StudyPrograms)
                        if (entry.DisplayName == candidate.DisplayName + " · 완료" ||
                            entry.DisplayName == candidate.DisplayName + " · 대성공")
                        { program = candidate; break; }
                if (text.Length > 0) text.Append("\n\n");
                AppendStudyProgram(text, program);
                if (entry.DisplayName.EndsWith(" · 대성공", System.StringComparison.Ordinal)) text.Append(" · 대성공");
                text.Append("\n적용 효과");
                for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
                {
                    var ability = (PlayerAbility)index;
                    int amount = 0;
                    foreach (var effect in entries)
                        if (effect.Source == OwnerGrowthSource.OverseasTraining && effect.IsActive && effect.DisplayName == entry.DisplayName)
                            amount += effect.Get(ability);
                    if (amount > 0) text.Append('\n').Append(PlayerAbilityCatalog.GetDisplayName(ability)).Append(" +").Append(amount);
                }
            }
            if (displayed.Count == 0) text.Append("\n유학지 · 등급 기록 없음\n현재 적용 중인 유학 능력치 보너스 없음");
            return text.ToString();
        }

        private static void AppendStudyProgram(System.Text.StringBuilder text, CardStudyProgramDefinition program)
        {
            if (program == null) { text.Append("유학지 · 등급 기록 없음"); return; }
            string rank = program.Rank.ToString();
            text.Append(program.DestinationName).Append("(").Append(rank).Append("등급)\n").Append(program.DisplayName);
        }

        /// <summary>진행 과정 또는 현재 활성 완료 효과에서 등급을 읽고, 기록이 없으면 추정하지 않는다.</summary>
        private static PlayerStudyBadgeRank FindStudyRank(OwnedPlayerCardState owned, OwnerCardGrowthBalanceTable studies, string programId = null)
        {
            var rank = PlayerStudyBadgeRank.None;
            if (studies == null) return rank;
            foreach (var program in studies.StudyPrograms)
            {
                if (programId != null)
                {
                    if (program.ProgramId == programId) return (PlayerStudyBadgeRank)program.Rank;
                    continue;
                }
                foreach (var entry in owned.Training.Ledger.Entries)
                    if (entry.Source == OwnerGrowthSource.OverseasTraining && entry.IsActive &&
                        (entry.DisplayName == program.DisplayName + " · 완료" || entry.DisplayName == program.DisplayName + " · 대성공"))
                        rank = (PlayerStudyBadgeRank)System.Math.Max((int)rank, (int)program.Rank);
            }
            return rank;
        }

        private static string DescribeStudyBonuses(OwnedPlayerCardState owned, bool showEmpty = false)
        {
            var text = new System.Text.StringBuilder();
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                int bonus = owned.Training.GetStudyBonus(ability);
                if (bonus <= 0) continue;
                if (text.Length == 0) text.Append("\n현재 유학 효과");
                text.Append('\n').Append(PlayerAbilityCatalog.GetDisplayName(ability)).Append(" +").Append(bonus);
            }
            return text.Length > 0 ? text.ToString() : showEmpty ? "\n현재 적용 중인 유학 능력치 보너스 없음" : "";
        }

        public static string DescribeTrait(PlayerTraitProgress progress, OwnerTraitTrainingBalance balance)
        {
            if (progress.trait == CardTraitKind.None) return "특성 미보유";
            return DescribeEffect(balance.Get(progress.trait), progress.rank, balance);
        }

        public static string DescribeEffect(CardTraitDefinition definition, CardTraitRank rank, OwnerTraitTrainingBalance balance)
        {
            double value = definition.effect * balance.multipliers[System.Math.Max(0, (int)rank - 1)];
            bool probability = definition.kind == CardTraitKind.Power || definition.kind == CardTraitKind.Groundball
                || definition.kind == CardTraitKind.Running || definition.kind == CardTraitKind.Endurance;
            string amount = probability ? (value * 100).ToString("0.#") + (definition.kind == CardTraitKind.Endurance ? "%" : "%p") : value.ToString("0.#");
            string sign = !probability && value > 0 ? "+" : "";
            return definition.name + " · " + definition.description + " " + sign + amount;
        }
    }
}
