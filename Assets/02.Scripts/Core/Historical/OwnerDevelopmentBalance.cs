using System;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    /// <summary>오프시즌 성장 관리의 PT 비용과 성장 조건를 저작한다.</summary>
    [Serializable]
    public sealed class OwnerDevelopmentBalance
    {
        public long correctionCost;
        public long partnerCost;
        public long researchCost;
        public int[] researchWeights;
        public int skillSetBonus = 1;
        public OwnerSkillFusionBalance fusion = new OwnerSkillFusionBalance();
        public OwnerPartnerBalance partner = new OwnerPartnerBalance();
        public OwnerSloganDefinition[] slogans;
        public OwnerStudyTierDefinition[] studyTiers;
        public void Validate()
        {
            if (partner == null) throw new ArgumentException("파트너 성장 계수가 없습니다.");
            partner.Validate();
            if (fusion == null) throw new ArgumentException("합성 밸런스가 없습니다.");
            fusion.Validate();
            if (correctionCost <= 0 || partnerCost <= 0 || researchCost <= 0)
                throw new ArgumentException("성장 관리 밸런스가 올바르지 않습니다.");
            if (researchWeights == null || researchWeights.Length != 3 || researchWeights[0] < 0 || researchWeights[1] < 0
                || researchWeights[2] < 0 || researchWeights[0] + researchWeights[1] + researchWeights[2] != 100)
                throw new ArgumentException("연구 등급 확률 합계는 100이어야 합니다.");
            if (slogans == null || slogans.Length == 0) throw new ArgumentException("슬로건 목록이 없습니다.");
            foreach (var slogan in slogans) slogan.Validate();
            if (studyTiers == null || studyTiers.Length != (int)CardStudyRank.SSS) throw new ArgumentException("해외 훈련 C~SSS 여섯 등급이 필요합니다.");
            var assignedPrograms = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var tier in studyTiers)
            {
                if (tier == null || tier.weeks < 1 || tier.cost <= 0 || tier.growth < 1 || tier.greatBonus < 0
                    || double.IsNaN(tier.greatProbability) || tier.greatProbability < 0 || tier.greatProbability > 1
                    || !Enum.IsDefined(typeof(LeagueGrade), tier.requiredLeagueGrade) || tier.programIds == null)
                    throw new ArgumentException("해외 훈련 등급 값이 올바르지 않습니다.");
                foreach (string id in tier.programIds)
                    if (string.IsNullOrWhiteSpace(id) || !assignedPrograms.Add(id))
                        throw new ArgumentException("유학 등급별 과정 ID가 비었거나 중복되었습니다.");
            }
        }
        /// <summary>기존 목적지·성장 방향·해금을 유지하면서 기간과 보상을 PT 등급으로 변환한다.</summary>
        public OwnerCardGrowthBalanceTable ApplyStudyTiers(OwnerCardGrowthBalanceTable source)
        {
            Validate();
            foreach (var configuredTier in studyTiers)
                foreach (string id in configuredTier.programIds) source.GetStudyProgram(id);
            var programs = new CardStudyProgramDefinition[source.StudyPrograms.Count];
            for (int i = 0; i < programs.Length; i++)
            {
                var program = source.StudyPrograms[i];
                int tierIndex = (int)program.UnlockRequirement.Kind;
                for (int grade = 0; grade < studyTiers.Length; grade++)
                    if (Array.IndexOf(studyTiers[grade].programIds, program.ProgramId) >= 0) tierIndex = grade;
                var tier = studyTiers[tierIndex];
                var rank = (CardStudyRank)(tierIndex + 1);
                var unlock = tierIndex < 3 ? program.UnlockRequirement : CardStudyUnlockRequirement.Reach(tier.requiredLeagueGrade);
                var rewards = new Baseball.Core.Growth.AbilityChange[program.Rewards.Count];
                int total = 0; foreach (var reward in program.Rewards) total += reward.Amount;
                int assigned = 0;
                for (int j = 1; j < rewards.Length; j++)
                {
                    int amount = Math.Max(1, tier.growth * program.Rewards[j].Amount / total); assigned += amount;
                    rewards[j] = new Baseball.Core.Growth.AbilityChange(program.Rewards[j].Ability, amount);
                }
                rewards[0] = new Baseball.Core.Growth.AbilityChange(program.Rewards[0].Ability, tier.growth - assigned);
                programs[i] = new CardStudyProgramDefinition(program.ProgramId, program.DisplayName, program.PlayerType, 0, tier.weeks,
                    rewards, program.DestinationName, program.MapXPermille, program.MapYPermille, unlock,
                    tier.cost, tier.greatProbability, tier.greatBonus, rank);
            }
            return new OwnerCardGrowthBalanceTable(source.TrainingPrograms, programs);
        }
    }
    /// <summary>랜덤 재활용의 비용·확률·누적 보상을 저작한다.</summary>
    [Serializable]
    public sealed class OwnerSkillFusionBalance
    {
        public long cost = 2000;
        public int pityFailures = 10, pointsPerCraft = 20;
        public double sameCategoryChance = .70, sameShapeChance = .50;
        public double gradeBonus = .10, categoryBonus = .20, shapeBonus = .25;
        // C~S는 기존 SS 확률의 10%를 SSS로 나눠 상위 보상 빈도를 보존한다.
        // SS의 동급 확률에서 5%p를 승급으로 옮기며 SSS는 최고 등급 유지 80%를 따른다.
        public double[] gradeWeights = { 55,35,8,1.9,.09,.01, 20,50,25,4.5,.45,.05, 0,20,55,23,1.8,.2, 0,0,20,75,4.5,.5, 0,0,0,20,75,5, 0,0,0,0,20,80 };
        public void Validate()
        {
            if (cost <= 0 || pityFailures < 1 || pointsPerCraft < 1 || gradeWeights == null || gradeWeights.Length != SkillBlockGradeCatalog.Count * SkillBlockGradeCatalog.Count)
                throw new ArgumentException("합성 비용·천장·확률표가 올바르지 않습니다.");
            foreach (double value in new[] { sameCategoryChance, sameShapeChance, gradeBonus, categoryBonus, shapeBonus })
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
                    throw new ArgumentException("합성 보정은 0~1 범위여야 합니다.");
            for (int row = 0; row < SkillBlockGradeCatalog.Count; row++)
            {
                double total = 0, upgrades = 0;
                for (int column = 0; column < SkillBlockGradeCatalog.Count; column++)
                {
                    double value = gradeWeights[row * SkillBlockGradeCatalog.Count + column];
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || (column < row - 1 && value != 0))
                        throw new ArgumentException("합성 등급 확률이 올바르지 않습니다.");
                    total += value;
                    if (column > row) upgrades += value;
                }
                if (Math.Abs(total - 100) > .000001 || (row < SkillBlockGradeCatalog.Count - 1 && upgrades <= 0))
                    throw new ArgumentException("합성 확률 합계는 100이며 승급 가능성이 있어야 합니다.");
            }
        }
    }
    public enum SkillFusionFocus { Grade, Ability, Shape }
    [Serializable]
    public sealed class OwnerPartnerBalance
    {
        public double ageWeight = .20, positionWeight = .25, teamWeight = .15, complementWeight = .30, personalityWeight = .10;
        public double ageScale = 8, complementScale = 60, growthBudget = 15, buntWeight = .25, reliefStaminaWeight = .5;
        public void Validate()
        {
            double[] values = { ageWeight, positionWeight, teamWeight, complementWeight, personalityWeight,
                ageScale, complementScale, growthBudget, buntWeight, reliefStaminaWeight };
            foreach (double value in values) if (value < 0 || double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("파트너 성장 계수가 올바르지 않습니다.");
            if (ageScale <= 0 || complementScale <= 0 || Math.Abs(ageWeight + positionWeight + teamWeight + complementWeight + personalityWeight - 1) > .000001)
                throw new ArgumentException("파트너 궁합 가중치 합계는 1이어야 합니다.");
        }
    }
    [Serializable]
    public sealed class OwnerStudyTierDefinition
    {
        public string[] programIds = Array.Empty<string>();
        public LeagueGrade requiredLeagueGrade;
        public int weeks;
        public long cost;
        public int growth;
        public double greatProbability;
        public int greatBonus;
    }
}
