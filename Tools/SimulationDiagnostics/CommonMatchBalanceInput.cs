using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Historical;

namespace Baseball.Tools
{
    /// <summary>헤드리스 진단도 게임과 동일한 공통 경기 계수 자산을 소비한다.</summary>
    internal static class CommonMatchBalanceInput
    {
        public const string DefaultPath = "Assets/10.Datas/Resources/NewGame/MiniGameBalance.json";
        public const string RatingCurvePath = "Assets/10.Datas/Resources/NewGame/MatchRatingCurve.json";

        /// <summary>Unity 없는 테스트의 기본값과 게임 자산의 불일치를 검출한다.</summary>
        public static void VerifyDefaults()
        {
            BalanceTable loaded = Load();
            BalanceTable baseline = BalanceTable.CreateDefault();
            foreach (var property in typeof(MiniGameBalance).GetProperties())
                if (!Equals(property.GetValue(loaded.MiniGame), property.GetValue(baseline.MiniGame)))
                    throw new InvalidDataException("공통 경기 기본값 불일치: " + property.Name);
            if (loaded.Match.Tactical.StealAttemptUtilityScale != baseline.Match.Tactical.StealAttemptUtilityScale)
                throw new InvalidDataException("도루 기용 기본값 불일치");
            if (loaded.Match.BullpenManagement.RelieverQualityWeight != baseline.Match.BullpenManagement.RelieverQualityWeight)
                throw new InvalidDataException("불펜 기용 기본값 불일치");
            foreach (var property in typeof(MatchRatingCurveBalance).GetProperties())
                if (property.Name != nameof(MatchRatingCurveBalance.Caps) &&
                    !Equals(property.GetValue(loaded.MatchRatingCurve), property.GetValue(baseline.MatchRatingCurve)))
                    throw new InvalidDataException("경기 능력치 곡선 기본값 불일치: " + property.Name);
            foreach (var property in typeof(EffectiveRatingCapTable).GetProperties())
                if (!Equals(property.GetValue(loaded.MatchRatingCurve.Caps), property.GetValue(baseline.MatchRatingCurve.Caps)))
                    throw new InvalidDataException("경기 능력치 상한 기본값 불일치: " + property.Name);
            using JsonDocument owner = JsonDocument.Parse(File.ReadAllBytes(
                "Assets/10.Datas/Resources/NewGame/OwnerExpansionBalance.json"));
            AggregateMatchBalance aggregate = JsonSerializer.Deserialize<AggregateMatchBalance>(
                owner.RootElement.GetProperty("aggregateMatch").GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            foreach (var property in typeof(AggregateMatchBalance).GetProperties())
                if (!Equals(property.GetValue(aggregate), property.GetValue(baseline.AggregateMatch)))
                    throw new InvalidDataException("간이 경기 기본값 불일치: " + property.Name);
            Console.WriteLine("PASS 공통 경기 JSON과 Core 기본값 일치: " + loaded.ContentHash);
        }

        public static BalanceTable Load(string path = DefaultPath)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using JsonDocument document = JsonDocument.Parse(bytes);
            if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
                throw new InvalidDataException("공통 경기 밸런스 Schema가 다릅니다.");
            MiniGameBalance miniGame = JsonSerializer.Deserialize<MiniGameBalance>(bytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            BalanceTable baseline = BalanceTable.CreateDefault();
            TacticalMatchBalance tactical = baseline.Match.Tactical.WithStealAttemptUtilityScale(
                document.RootElement.GetProperty("stealAttemptUtilityScale").GetDouble());
            BullpenManagementBalance bullpen = baseline.Match.BullpenManagement.WithRelieverQualityWeight(
                document.RootElement.TryGetProperty("relieverQualityWeight", out var qualityWeight)
                    ? qualityWeight.GetDouble() : baseline.Match.BullpenManagement.RelieverQualityWeight);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            byte[] curveBytes = File.ReadAllBytes(RatingCurvePath);
            using JsonDocument curveDocument = JsonDocument.Parse(curveBytes);
            JsonElement curve = curveDocument.RootElement;
            if (curve.GetProperty("schemaVersion").GetInt32() != 1)
                throw new InvalidDataException("경기 능력치 곡선 Schema가 다릅니다.");
            bool usesPitcherCurve = curve.TryGetProperty("usePitcherCurve", out var pitcherFlag) && pitcherFlag.GetBoolean();
            bool usesUpperSpread = curve.TryGetProperty("useUpperSpreadLimit", out var upperFlag) && upperFlag.GetBoolean();
            bool usesLowerSpread = curve.TryGetProperty("useLowerSpreadLimit", out var lowerFlag) && lowerFlag.GetBoolean();
            bool usesPitcherUpperSpread = curve.TryGetProperty("usePitcherUpperSpreadLimit", out var pitcherUpperFlag) && pitcherUpperFlag.GetBoolean();
            var ratings = new MatchRatingCurveBalance(curve.GetProperty("center").GetDouble(),
                curve.GetProperty("slope").GetDouble(),
                new EffectiveRatingCapTable(curve.GetProperty("softCap").GetInt32(),
                    curve.GetProperty("hardCap").GetInt32(), curve.GetProperty("postSoftCapSlope").GetDouble()),
                curve.TryGetProperty("inputOffset", out var offset) ? offset.GetDouble() : 0d,
                usesPitcherCurve ? curve.GetProperty("pitcherSlope").GetDouble() : (double?)null,
                usesPitcherCurve ? curve.GetProperty("pitcherInputOffset").GetDouble() : (double?)null,
                usesUpperSpread ? curve.GetProperty("upperSpreadStart").GetDouble() : (double?)null,
                usesLowerSpread ? curve.GetProperty("lowerSpreadEnd").GetDouble() : (double?)null,
                usesLowerSpread ? curve.GetProperty("lowerSlope").GetDouble() : .45d,
                usesPitcherUpperSpread ? curve.GetProperty("pitcherUpperSpreadStart").GetDouble() : (double?)null);
            string curveHash = Convert.ToHexString(SHA256.HashData(curveBytes)).ToLowerInvariant();
            return new BalanceTable(baseline.Version, baseline.PlateDiscipline, baseline.BattedBall,
                baseline.BaseRunning, baseline.ContractOffer, baseline.TeamGeneration,
                baseline.PlayerEvaluation, baseline.CareerSeason, miniGame: miniGame,
                match: baseline.Match.WithTactical(tactical).WithBullpen(bullpen),
                matchRatingCurve: ratings,
                contentHash: baseline.ContentHash + ":mini-game-" + hash + ":rating-curve-" + curveHash);
        }
    }
}
