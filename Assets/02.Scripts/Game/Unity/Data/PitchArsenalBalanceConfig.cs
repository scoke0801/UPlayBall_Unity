using System;
using System.Security.Cryptography;
using System.Text;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>Offline Bake와 같은 JSON 정본을 순수 C# 구종 밸런스로 변환한다.</summary>
    public static class PitchArsenalBalanceConfig
    {
        public const string ResourcePath = "NewGame/PitchArsenalBalance";
        /// <summary>구종 계수 JSON을 검증하고 실행 계약을 만든다.</summary>
        public static PitchArsenalBalance Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("구종 밸런스 JSON이 필요합니다.");
            var data = JsonUtility.FromJson<PitchArsenalBalanceData>(json);
            if (data == null || data.schemaVersion != 1 || data.types == null ||
                data.grade == null || data.growth == null)
                throw new InvalidOperationException("구종 밸런스 Schema 또는 필수 항목이 없습니다.");
            var definitions = new PitchTypeDefinition[data.types.Length];
            for (int i = 0; i < definitions.Length; i++)
                definitions[i] = data.types[i]?.Build() ?? throw new InvalidOperationException("구종 정의가 없습니다.");
            return new PitchArsenalBalance(data.version, definitions,
                new PitchGradeBalance(data.grade.labels, data.grade.thresholds),
                new PitchGrowthBalance(data.growth.primaryFocus, data.growth.secondaryFocus,
                    data.growth.auxiliaryFocus, data.growth.fivePitchAuxiliaryModifier,
                    data.growth.sixPitchAuxiliaryModifier), data.velocityReferenceKph,
                data.velocityKphPerRating, data.ratingCenter, data.qualityMasteryWeight);
        }
        /// <summary>Production Resources에서 명시적인 구종 밸런스를 읽는다.</summary>
        public static PitchArsenalBalance Load()
        {
            return Load(out _);
        }
        /// <summary>실제 로드한 JSON 해시를 경기/과거 기록 캐시 키에 함께 제공한다.</summary>
        public static PitchArsenalBalance Load(out string contentHash)
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("구종 밸런스 리소스가 없습니다: " + ResourcePath);
            using SHA256 hash = SHA256.Create();
            byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(asset.text));
            var text = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest) text.Append(value.ToString("x2"));
            contentHash = text.ToString();
            return Parse(asset.text);
        }
    }
#pragma warning disable 0649
    [Serializable] internal sealed class PitchArsenalBalanceData
    {
        public int schemaVersion;
        public string version;
        public double velocityReferenceKph, velocityKphPerRating, ratingCenter, qualityMasteryWeight;
        public PitchGradeData grade;
        public PitchGrowthData growth;
        public PitchDefinitionData[] types;
    }
    [Serializable] internal sealed class PitchGradeData
    {
        public string[] labels;
        public double[] thresholds;
    }
    [Serializable] internal sealed class PitchGrowthData
    {
        public double primaryFocus, secondaryFocus, auxiliaryFocus;
        public double fivePitchAuxiliaryModifier, sixPitchAuxiliaryModifier;
    }
    [Serializable] internal sealed class PitchDefinitionData
    {
        public string pitchType;
        public string displayName;
        public double generationWeight;
        public double intrinsicValue;
        public double potentialCeilingBias;
        public double baseGrowthEfficiency;
        public double masteryDifficulty;
        public double velocityInfluence;
        public double stuffInfluence;
        public double breakingInfluence;
        public double controlInfluence;
        public double controlDifficulty;
        public double velocityGapKph;
        public double baseVelocityOffsetKph;
        public bool isRare;
        public double earlyEraWeight;
        public double modernEraWeight;
        public double starterWeight;
        public double relieverWeight;
        public PitchTypeDefinition Build()
        {
            if (!Enum.TryParse(pitchType, out PitchType type) || !Enum.IsDefined(typeof(PitchType), type))
                throw new InvalidOperationException("지원하지 않는 구종: " + pitchType);
            return new PitchTypeDefinition(type, displayName, generationWeight, intrinsicValue, potentialCeilingBias, baseGrowthEfficiency, masteryDifficulty, velocityInfluence, stuffInfluence, breakingInfluence, controlInfluence, controlDifficulty, velocityGapKph, baseVelocityOffsetKph, isRare, earlyEraWeight, modernEraWeight, starterWeight, relieverWeight);
        }
    }
#pragma warning restore 0649
}
