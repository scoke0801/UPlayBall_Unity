using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Core.Balance
{
    /// <summary>구종별 희소성·성장·실전 기여를 독립적으로 정의한다.</summary>
    public sealed class PitchTypeDefinition
    {
        public PitchTypeDefinition(PitchType pitchType, string displayName,
            double generationWeight, double intrinsicValue, double potentialCeilingBias,
            double baseGrowthEfficiency, double masteryDifficulty, double velocityInfluence,
            double stuffInfluence, double breakingInfluence, double controlInfluence,
            double controlDifficulty, double velocityGapKph, double baseVelocityOffsetKph,
            bool isRare, double earlyEraWeight, double modernEraWeight,
            double starterWeight, double relieverWeight)
        {
            if (!Enum.IsDefined(typeof(PitchType), pitchType) || string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("구종과 표시 이름이 필요합니다.");
            double[] values = { generationWeight, intrinsicValue, potentialCeilingBias,
                baseGrowthEfficiency, masteryDifficulty, velocityInfluence, stuffInfluence,
                breakingInfluence, controlInfluence, controlDifficulty, velocityGapKph,
                baseVelocityOffsetKph, earlyEraWeight, modernEraWeight, starterWeight, relieverWeight };
            foreach (double value in values)
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentException("구종 계수는 유한해야 합니다.");
            if (generationWeight <= 0 || baseGrowthEfficiency <= 0 || masteryDifficulty <= 0 ||
                velocityInfluence < 0 || stuffInfluence < 0 || breakingInfluence < 0 || controlInfluence < 0 ||
                earlyEraWeight <= 0 || modernEraWeight <= 0 || starterWeight <= 0 || relieverWeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(generationWeight));
            PitchType = pitchType; DisplayName = displayName;
            GenerationWeight = generationWeight; IntrinsicValue = intrinsicValue;
            PotentialCeilingBias = potentialCeilingBias; BaseGrowthEfficiency = baseGrowthEfficiency;
            MasteryDifficulty = masteryDifficulty; VelocityInfluence = velocityInfluence;
            StuffInfluence = stuffInfluence; BreakingInfluence = breakingInfluence;
            ControlInfluence = controlInfluence; ControlDifficulty = controlDifficulty;
            VelocityGapKph = velocityGapKph; BaseVelocityOffsetKph = baseVelocityOffsetKph;
            IsRare = isRare; EarlyEraWeight = earlyEraWeight; ModernEraWeight = modernEraWeight;
            StarterWeight = starterWeight; RelieverWeight = relieverWeight;
        }
        public PitchType PitchType { get; }
        public string DisplayName { get; }
        public double GenerationWeight { get; }
        public double IntrinsicValue { get; }
        public double PotentialCeilingBias { get; }
        public double BaseGrowthEfficiency { get; }
        public double MasteryDifficulty { get; }
        public double VelocityInfluence { get; }
        public double StuffInfluence { get; }
        public double BreakingInfluence { get; }
        public double ControlInfluence { get; }
        public double ControlDifficulty { get; }
        public double VelocityGapKph { get; }
        public double BaseVelocityOffsetKph { get; }
        public bool IsRare { get; }
        public double EarlyEraWeight { get; }
        public double ModernEraWeight { get; }
        public double StarterWeight { get; }
        public double RelieverWeight { get; }
    }

    /// <summary>저장 숙련도와 안정적인 카드 등급 사이의 경계를 보관한다.</summary>
    public sealed class PitchGradeBalance
    {
        private readonly string[] _labels;
        private readonly double[] _thresholds;
        public PitchGradeBalance(IReadOnlyList<string> labels, IReadOnlyList<double> thresholds)
        {
            if (labels == null || thresholds == null || labels.Count == 0 || labels.Count != thresholds.Count)
                throw new ArgumentException("등급 이름과 경계가 필요합니다.");
            _labels = new string[labels.Count]; _thresholds = new double[labels.Count];
            for (int i = 0; i < labels.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(labels[i]) || double.IsNaN(thresholds[i]) ||
                    thresholds[i] < 0 || thresholds[i] > 100 || (i > 0 && thresholds[i] <= thresholds[i - 1]))
                    throw new ArgumentException("등급 경계는 0~100의 오름차순이어야 합니다.");
                _labels[i] = labels[i]; _thresholds[i] = thresholds[i];
            }
            if (_thresholds[0] != 0d) throw new ArgumentException("최저 등급은 0부터 시작해야 합니다.");
        }
        /// <summary>일시적 컨디션을 제외한 숙련도/안정 품질의 등급을 반환한다.</summary>
        public string GetGrade(double mastery)
        {
            if (double.IsNaN(mastery)) throw new ArgumentOutOfRangeException(nameof(mastery));
            for (int i = _thresholds.Length - 1; i >= 0; i--)
                if (mastery >= _thresholds[i]) return _labels[i];
            return _labels[0];
        }
    }

    /// <summary>주력 구종을 보호하면서 넓은 구종 목록의 보조 성장만 분산한다.</summary>
    public sealed class PitchGrowthBalance
    {
        public PitchGrowthBalance(double primaryFocus, double secondaryFocus, double auxiliaryFocus,
            double fivePitchAuxiliaryModifier, double sixPitchAuxiliaryModifier)
        {
            double[] values = { primaryFocus, secondaryFocus, auxiliaryFocus, fivePitchAuxiliaryModifier, sixPitchAuxiliaryModifier };
            foreach (double value in values)
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                    throw new ArgumentOutOfRangeException(nameof(primaryFocus));
            if (fivePitchAuxiliaryModifier > 1 || sixPitchAuxiliaryModifier > fivePitchAuxiliaryModifier)
                throw new ArgumentException("보조 성장 분산은 구종이 많을수록 커져야 합니다.");
            PrimaryFocus = primaryFocus; SecondaryFocus = secondaryFocus; AuxiliaryFocus = auxiliaryFocus;
            FivePitchAuxiliaryModifier = fivePitchAuxiliaryModifier; SixPitchAuxiliaryModifier = sixPitchAuxiliaryModifier;
        }
        public double PrimaryFocus { get; }
        public double SecondaryFocus { get; }
        public double AuxiliaryFocus { get; }
        public double FivePitchAuxiliaryModifier { get; }
        public double SixPitchAuxiliaryModifier { get; }
    }

    /// <summary>JSON에서 로드하는 구종 밸런스의 순수 C# 실행 계약이다.</summary>
    public sealed partial class PitchArsenalBalance
    {
        private readonly PitchTypeDefinition[] _types;
        public PitchArsenalBalance(string version, IReadOnlyList<PitchTypeDefinition> types,
            PitchGradeBalance grade, PitchGrowthBalance growth, double velocityReferenceKph,
            double velocityKphPerRating, double ratingCenter, double qualityMasteryWeight)
        {
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("구종 버전이 필요합니다.");
            int count = Enum.GetValues(typeof(PitchType)).Length;
            if (types == null || types.Count != count) throw new ArgumentException("모든 구종 정의가 필요합니다.");
            _types = new PitchTypeDefinition[count];
            foreach (PitchTypeDefinition type in types)
            {
                if (type == null || _types[(int)type.PitchType] != null) throw new ArgumentException("중복/누락 구종입니다.");
                _types[(int)type.PitchType] = type;
            }
            if (double.IsNaN(velocityReferenceKph) || velocityReferenceKph <= 0 || double.IsInfinity(velocityReferenceKph) ||
                double.IsNaN(velocityKphPerRating) || velocityKphPerRating <= 0 || double.IsInfinity(velocityKphPerRating) ||
                double.IsNaN(ratingCenter) || ratingCenter < 0 || ratingCenter > 100 ||
                double.IsNaN(qualityMasteryWeight) || qualityMasteryWeight < 0 || qualityMasteryWeight > 1)
                throw new ArgumentException("구속/품질 계수가 유효하지 않습니다.");
            Version = version; Grade = grade ?? throw new ArgumentNullException(nameof(grade));
            Growth = growth ?? throw new ArgumentNullException(nameof(growth));
            VelocityReferenceKph = velocityReferenceKph; VelocityKphPerRating = velocityKphPerRating;
            RatingCenter = ratingCenter; QualityMasteryWeight = qualityMasteryWeight;
        }
        public string Version { get; }
        public PitchGradeBalance Grade { get; }
        public PitchGrowthBalance Growth { get; }
        public double VelocityReferenceKph { get; }
        public double VelocityKphPerRating { get; }
        public double RatingCenter { get; }
        public double QualityMasteryWeight { get; }
        /// <summary>구종별 정의를 할당 없이 반환한다.</summary>
        public PitchTypeDefinition Get(PitchType type)
        {
            int index = (int)type;
            if (index < 0 || index >= _types.Length) throw new ArgumentOutOfRangeException(nameof(type));
            return _types[index];
        }
    }
}
