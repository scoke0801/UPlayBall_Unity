using System;
using System.Security.Cryptography;
using System.Text;
using Baseball.Core.Balance;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>공통 상세 투구·타격 계수를 JSON에서 읽고 캐시 해시에 포함한다.</summary>
    public static class MiniGameBalanceConfig
    {
        /// <summary>선수·구단주 모드가 공유할 투구·타격 및 도루 기용 설정을 읽는다.</summary>
        public static MiniGameBalance Load(out string contentHash, out TacticalMatchBalance tactical)
            => Load(out contentHash, out tactical, out _);

        /// <summary>불펜 기용 평가까지 공통 경기 자산에서 함께 주입한다.</summary>
        public static MiniGameBalance Load(out string contentHash, out TacticalMatchBalance tactical,
            out BullpenManagementBalance bullpen)
        {
            TextAsset asset = Resources.Load<TextAsset>("NewGame/MiniGameBalance");
            if (asset == null) throw new InvalidOperationException("공통 경기 밸런스 리소스가 없습니다.");
            using SHA256 hash = SHA256.Create();
            byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(asset.text));
            var result = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest) result.Append(value.ToString("x2"));
            contentHash = result.ToString();
            MiniGameBalanceData data = ReadData(asset.text);
            tactical = MatchBalanceTable.CreateDefault().Tactical.WithStealAttemptUtilityScale(data.stealAttemptUtilityScale);
            bullpen = MatchBalanceTable.CreateDefault().BullpenManagement.WithRelieverQualityWeight(data.relieverQualityWeight);
            return Build(data);
        }

        /// <summary>실제 팀 기록 없이 순수 C# 경기 계수만 생성한다.</summary>
        public static MiniGameBalance Parse(string json)
        {
            return Build(ReadData(json));
        }

        private static MiniGameBalanceData ReadData(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("공통 경기 밸런스 JSON이 필요합니다.");
            MiniGameBalanceData data = JsonUtility.FromJson<MiniGameBalanceData>(json);
            if (data == null || data.schemaVersion != 1) throw new ArgumentException("공통 경기 밸런스 Schema가 다릅니다.");
            return data;
        }

        private static MiniGameBalance Build(MiniGameBalanceData data)
        {
            return new MiniGameBalance(
                targetHorizontalLimit: data.targetHorizontalLimit,
                targetVerticalLimit: data.targetVerticalLimit,
                baseCommandDeviation: data.baseCommandDeviation,
                controlDeviationWeight: data.controlDeviationWeight,
                minimumCommandDeviation: data.minimumCommandDeviation,
                maximumCommandDeviation: data.maximumCommandDeviation,
                baseBatRadiusX: data.baseBatRadiusX,
                baseBatRadiusY: data.baseBatRadiusY,
                contactRadiusWeight: data.contactRadiusWeight,
                perfectTimingMilliseconds: data.perfectTimingMilliseconds,
                validTimingMilliseconds: data.validTimingMilliseconds,
                foulTimingMilliseconds: data.foulTimingMilliseconds,
                contactTimingWeight: data.contactTimingWeight,
                swingImpactLeadMilliseconds: data.swingImpactLeadMilliseconds,
                contactIntentRadiusMultiplier: data.contactIntentRadiusMultiplier,
                powerIntentRadiusMultiplier: data.powerIntentRadiusMultiplier,
                contactIntentExitVelocityPenalty: data.contactIntentExitVelocityPenalty,
                powerIntentExitVelocityBonus: data.powerIntentExitVelocityBonus,
                baseExitVelocity: data.baseExitVelocity,
                powerExitVelocityWeight: data.powerExitVelocityWeight,
                outOfZoneQualityPenalty: data.outOfZoneQualityPenalty,
                aiWastePitchProbability: data.aiWastePitchProbability,
                aiTwoStrikeWasteProbability: data.aiTwoStrikeWasteProbability,
                aiThreeBallChallengeProbability: data.aiThreeBallChallengeProbability,
                aiWastePitchDistance: data.aiWastePitchDistance,
                aiInsideWasteProbability: data.aiInsideWasteProbability,
                aiLocationErrorScale: data.aiLocationErrorScale,
                aiTimingErrorMilliseconds: data.aiTimingErrorMilliseconds,
                contactQualityBase: data.contactQualityBase,
                launchAngleBaseDegrees: data.launchAngleBaseDegrees,
                launchAngleLocationScale: data.launchAngleLocationScale,
                homeRunMinimumExitVelocity: data.homeRunMinimumExitVelocity,
                homeRunMinimumLaunchAngle: data.homeRunMinimumLaunchAngle,
                homeRunMaximumLaunchAngle: data.homeRunMaximumLaunchAngle,
                homeRunProbabilityMultiplier: data.homeRunProbabilityMultiplier,
                repeatRecognitionBase: data.repeatRecognitionBase,
                repeatRecognitionMentalWeight: data.repeatRecognitionMentalWeight,
                repeatChaseReduction: data.repeatChaseReduction,
                repeatExecutionErrorReduction: data.repeatExecutionErrorReduction,
                aiThreeBallTargetHorizontal: data.aiThreeBallTargetHorizontal,
                aiPitchQualityDifficultyWeight: data.aiPitchQualityDifficultyWeight,
                contactPitchQualityWeight: data.contactPitchQualityWeight,
                contactBatterQualityWeight: data.contactBatterQualityWeight,
                hitByPitchMinimumInsideLocation: data.hitByPitchMinimumInsideLocation,
                hitByPitchMaximumHeight: data.hitByPitchMaximumHeight,
                hitByPitchContactProbability: data.hitByPitchContactProbability,
                aiMentalChaseWeight: data.aiMentalChaseWeight,
                aiStuffLocationWeight: data.aiStuffLocationWeight);
        }
    }

#pragma warning disable 0649
    [Serializable] internal sealed class MiniGameBalanceData
    {
        public int schemaVersion;
        public double hitByPitchMinimumInsideLocation = 1.18d;
        public double hitByPitchMaximumHeight = 1.05d;
        public double hitByPitchContactProbability = .18d;
        public double aiMentalChaseWeight = .0045d;
        public double stealAttemptUtilityScale;
        public double relieverQualityWeight = 2d;
        public double aiPitchQualityDifficultyWeight = .005d;
        public double aiStuffLocationWeight = .008d;
        public double contactPitchQualityWeight = .25d;
        public double contactBatterQualityWeight = .25d;
        public double targetHorizontalLimit;
        public double targetVerticalLimit;
        public double baseCommandDeviation;
        public double controlDeviationWeight;
        public double minimumCommandDeviation;
        public double maximumCommandDeviation;
        public double baseBatRadiusX;
        public double baseBatRadiusY;
        public double contactRadiusWeight;
        public double perfectTimingMilliseconds;
        public double validTimingMilliseconds;
        public double foulTimingMilliseconds;
        public double contactTimingWeight;
        public double swingImpactLeadMilliseconds;
        public double contactIntentRadiusMultiplier;
        public double powerIntentRadiusMultiplier;
        public double contactIntentExitVelocityPenalty;
        public double powerIntentExitVelocityBonus;
        public double baseExitVelocity;
        public double powerExitVelocityWeight;
        public double outOfZoneQualityPenalty;
        public double aiWastePitchProbability;
        public double aiTwoStrikeWasteProbability;
        public double aiThreeBallChallengeProbability;
        public double aiWastePitchDistance;
        public double aiInsideWasteProbability;
        public double aiLocationErrorScale;
        public double aiTimingErrorMilliseconds;
        public double contactQualityBase;
        public double launchAngleBaseDegrees;
        public double launchAngleLocationScale;
        public double homeRunMinimumExitVelocity;
        public double homeRunMinimumLaunchAngle;
        public double homeRunMaximumLaunchAngle;
        public double homeRunProbabilityMultiplier;
        public double repeatRecognitionBase;
        public double repeatRecognitionMentalWeight;
        public double repeatChaseReduction;
        public double repeatExecutionErrorReduction;
        public double aiThreeBallTargetHorizontal;
    }
#pragma warning restore 0649
}
