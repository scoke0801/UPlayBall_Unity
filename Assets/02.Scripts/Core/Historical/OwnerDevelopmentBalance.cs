using System;

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
        public OwnerPartnerBalance partner = new OwnerPartnerBalance();
        public OwnerSloganDefinition[] slogans;
        public OwnerStudyTierDefinition[] studyTiers;
        public void Validate()
        {
            if (partner == null) throw new ArgumentException("파트너 성장 계수가 없습니다.");
            partner.Validate();
            if (correctionCost <= 0 || partnerCost <= 0 || researchCost <= 0)
                throw new ArgumentException("성장 관리 밸런스가 올바르지 않습니다.");
            if (researchWeights == null || researchWeights.Length != 3 || researchWeights[0] < 0 || researchWeights[1] < 0
                || researchWeights[2] < 0 || researchWeights[0] + researchWeights[1] + researchWeights[2] != 100)
                throw new ArgumentException("연구 등급 확률 합계는 100이어야 합니다.");
            if (slogans == null || slogans.Length == 0) throw new ArgumentException("슬로건 목록이 없습니다.");
            foreach (var slogan in slogans) slogan.Validate();
            if (studyTiers == null || studyTiers.Length != 3) throw new ArgumentException("해외 훈련 세 등급이 필요합니다.");
            foreach (var tier in studyTiers)
                if (tier.weeks < 1 || tier.weeks > 3 || tier.cost <= 0 || tier.growth < 1 || tier.greatBonus < 0
                    || tier.greatProbability < 0 || tier.greatProbability > 1) throw new ArgumentException("해외 훈련 등급 값이 올바르지 않습니다.");
        }
        /// <summary>기존 목적지·성장 방향·해금을 유지하면서 기간과 보상을 PT 등급으로 변환한다.</summary>
        public OwnerCardGrowthBalanceTable ApplyStudyTiers(OwnerCardGrowthBalanceTable source)
        {
            Validate();
            var programs = new CardStudyProgramDefinition[source.StudyPrograms.Count];
            for (int i = 0; i < programs.Length; i++)
            {
                var program = source.StudyPrograms[i];
                var tier = studyTiers[(int)program.UnlockRequirement.Kind];
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
                    rewards, program.DestinationName, program.MapXPermille, program.MapYPermille, program.UnlockRequirement,
                    tier.cost, tier.greatProbability, tier.greatBonus);
            }
            return new OwnerCardGrowthBalanceTable(source.TrainingPrograms, programs);
        }
    }
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
        public int weeks;
        public long cost;
        public int growth;
        public double greatProbability;
        public int greatBonus;
    }
}
