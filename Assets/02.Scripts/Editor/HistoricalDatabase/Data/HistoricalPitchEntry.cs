using System;
using Baseball.Core.Players;
using UnityEngine;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>희소성 선택과 고품질 추첨을 구분하는 Editor 전용 Bake 추적 정보다.</summary>
    [Serializable]
    public sealed class HistoricalPitchGenerationTrace
    {
#pragma warning disable 0649
        [SerializeField] private string sourceKind;
        [SerializeField] private long generationSeed;
        [SerializeField] private string balanceVersion;
        [SerializeField] private string seedDigest;
        [SerializeField] private HistoricalPitchGenerationRoll[] pitches;
#pragma warning restore 0649
        public string SourceKind => sourceKind ?? string.Empty;
        public long GenerationSeed => generationSeed;
        public string BalanceVersion => balanceVersion ?? string.Empty;
        public string SeedDigest => seedDigest ?? string.Empty;
        public HistoricalPitchGenerationRoll[] Pitches => pitches ?? Array.Empty<HistoricalPitchGenerationRoll>();
    }

    /// <summary>구종 하나의 희소성 여부와 별도의 고품질 추첨 결과를 보관한다.</summary>
    [Serializable]
    public sealed class HistoricalPitchGenerationRoll
    {
#pragma warning disable 0649
        [SerializeField] private string pitchType;
        [SerializeField] private bool rare;
        [SerializeField] private bool premiumRoll;
        [SerializeField] private double sampledMastery;
#pragma warning restore 0649
        public string PitchType => pitchType ?? string.Empty;
        public bool Rare => rare;
        public bool PremiumRoll => premiumRoll;
        public double SampledMastery => sampledMastery;
    }

    /// <summary>합성 구종의 원시 성장 적성과 사용 선호를 Editor 검수에 제공한다.</summary>
    [Serializable]
    public sealed class HistoricalPitchEntry
    {
#pragma warning disable 0649
        [SerializeField] private string pitchType;
        [SerializeField] private int baseMastery;
        [SerializeField] private bool isPrimary;
        [SerializeField] private double developmentAffinity;
        [SerializeField] private double usagePreference;
        [SerializeField] private double velocityOffset;
#pragma warning restore 0649
        public string PitchType => pitchType ?? string.Empty;
        public int BaseMastery => baseMastery;
        public bool IsPrimary => isPrimary;
        public double DevelopmentAffinity => developmentAffinity;
        public double UsagePreference => usagePreference;
        public double VelocityOffset => velocityOffset;
        /// <summary>Runtime과 같은 구종 값 객체로 변환한다.</summary>
        public PitchRepertoireEntry ToEntry() => new PitchRepertoireEntry(
            (Baseball.Core.Players.PitchType)Enum.Parse(typeof(Baseball.Core.Players.PitchType), PitchType),
            BaseMastery, IsPrimary, DevelopmentAffinity, UsagePreference, VelocityOffset);
    }
}
