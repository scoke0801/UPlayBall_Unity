using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;

namespace Baseball.Simulation.Match
{
    /// <summary>저장 숙련도를 바꾸지 않고 구종의 안정 등급·실전 품질·구속을 계산한다.</summary>
    public static class PitchEffectivenessResolver
    {
        /// <summary>컨디션과 전술을 제외한 영구 능력치로 카드 표시 품질을 계산한다.</summary>
        public static double ResolveStableQuality(in PitchRepertoireEntry entry,
            PitcherAttributes permanentAttributes, PitchArsenalBalance balance,
            PitcherAttributes? bakedAttributes = null, int pitchCount = 1, int priorityIndex = 0)
        {
            PitcherAttributes baseline = bakedAttributes ?? permanentAttributes;
            PitchTypeDefinition type = balance.Get(entry.PitchType);
            double growth = PitchGrowthResolver.ResolveEfficiency(entry, pitchCount, priorityIndex, balance);
            return Clamp(entry.BaseMastery + growth * (
                (permanentAttributes.Stuff - baseline.Stuff) * type.StuffInfluence +
                (permanentAttributes.Breaking - baseline.Breaking) * type.BreakingInfluence +
                (permanentAttributes.Control - baseline.Control) * type.ControlInfluence));
        }

        /// <summary>영구 성장분만 성장 적성으로 조정하고 피로·전술은 현재 입력 그대로 소비한다.</summary>
        public static double ResolvePlayerQuality(in PitchRepertoireEntry entry, Player pitcher,
            double stuff, double breaking, double control, PitchArsenalBalance balance)
        {
            int priority = GetPriority(entry, pitcher);
            double growth = PitchGrowthResolver.ResolveEfficiency(entry,
                Math.Max(1, pitcher.PitchRepertoire.Count), priority, balance);
            PitchTypeDefinition type = balance.Get(entry.PitchType);
            PitcherAttributes permanent = pitcher.PermanentPitcherAttributes;
            PitcherAttributes baked = pitcher.BakedPitcherAttributes;
            double permanentContribution = (permanent.Stuff - baked.Stuff) * type.StuffInfluence +
                (permanent.Breaking - baked.Breaking) * type.BreakingInfluence +
                (permanent.Control - baked.Control) * type.ControlInfluence;
            return Clamp(balance.RatingCenter + (entry.BaseMastery - balance.RatingCenter) *
                balance.QualityMasteryWeight + ResolveAbilityContribution(entry.PitchType, stuff, breaking, control, balance) +
                permanentContribution * (growth - 1d));
        }

        /// <summary>사용 우선순위로 주무기 두 개를 안정적으로 보호한다.</summary>
        public static int GetPriority(in PitchRepertoireEntry entry, Player pitcher)
        {
            int priority = 0;
            for (int index = 0; index < pitcher.PitchRepertoire.Count; index++)
            {
                PitchRepertoireEntry other = pitcher.PitchRepertoire[index];
                if (other.PitchType == entry.PitchType) continue;
                if ((other.IsPrimary && !entry.IsPrimary) ||
                    (other.IsPrimary == entry.IsPrimary &&
                    (other.UsagePreference > entry.UsagePreference ||
                    (other.UsagePreference == entry.UsagePreference && (int)other.PitchType < (int)entry.PitchType))))
                    priority++;
            }
            return priority;
        }

        /// <summary>현재 능력을 투구 판정 품질로 변환한다. 희소도와 구종 수는 성능을 깎지 않는다.</summary>
        public static double ResolveQuality(in PitchRepertoireEntry entry, double stuff,
            double breaking, double control, PitchArsenalBalance balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            return Clamp(balance.RatingCenter + (entry.BaseMastery - balance.RatingCenter) *
                balance.QualityMasteryWeight + ResolveAbilityContribution(entry.PitchType, stuff, breaking, control, balance));
        }

        /// <summary>직구보다 변화구의 구속 성장 기울기를 작게 유지하는 실제 km/h를 반환한다.</summary>
        public static double ResolveVelocityKph(in PitchRepertoireEntry entry,
            double velocityRating, PitchArsenalBalance balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            PitchTypeDefinition type = balance.Get(entry.PitchType);
            return balance.VelocityReferenceKph - type.VelocityGapKph + type.BaseVelocityOffsetKph +
                entry.VelocityOffset + (velocityRating - balance.RatingCenter) *
                balance.VelocityKphPerRating * type.VelocityInfluence;
        }

        /// <summary>구종에 필요한 변화 기술의 비중만 움직임에 적용한다.</summary>
        public static double ResolveMovementScale(in PitchRepertoireEntry entry,
            double breakingRating, PitchArsenalBalance balance)
        {
            PitchTypeDefinition type = balance.Get(entry.PitchType);
            return Math.Max(0.55d, Math.Min(1.65d, 1d +
                (breakingRating - balance.RatingCenter) * type.BreakingInfluence / balance.RatingCenter));
        }

        private static double ResolveAbilityContribution(PitchType pitchType, double stuff,
            double breaking, double control, PitchArsenalBalance balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            PitchTypeDefinition type = balance.Get(pitchType);
            return (stuff - balance.RatingCenter) * type.StuffInfluence +
                (breaking - balance.RatingCenter) * type.BreakingInfluence +
                (control - balance.RatingCenter) * type.ControlInfluence + type.IntrinsicValue;
        }

        private static double Clamp(double value) => Math.Max(0d, Math.Min(100d, value));
    }

    /// <summary>구종의 현재 성능은 유지하고 보조 구종의 영구 능력 성장 효율만 분산한다.</summary>
    public static class PitchGrowthResolver
    {
        /// <summary>주무기와 준주무기를 보호하는 구종별 성장 배율을 반환한다.</summary>
        public static double ResolveEfficiency(in PitchRepertoireEntry entry,
            int pitchCount, int priorityIndex, PitchArsenalBalance balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (pitchCount < 1 || pitchCount > 6 || priorityIndex < 0 || priorityIndex >= pitchCount)
                throw new ArgumentOutOfRangeException(nameof(pitchCount));
            PitchGrowthBalance growth = balance.Growth;
            double focus = entry.IsPrimary || priorityIndex == 0 ? growth.PrimaryFocus :
                priorityIndex == 1 ? growth.SecondaryFocus : growth.AuxiliaryFocus;
            double breadth = entry.IsPrimary || priorityIndex <= 1 || pitchCount <= 4 ? 1d :
                pitchCount == 5 ? growth.FivePitchAuxiliaryModifier : growth.SixPitchAuxiliaryModifier;
            PitchTypeDefinition type = balance.Get(entry.PitchType);
            return type.BaseGrowthEfficiency / type.MasteryDifficulty *
                entry.DevelopmentAffinity * focus * breadth;
        }
    }
}
