using System;
using System.Security.Cryptography;
using System.Text;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>능력 표시와 경기 효과 분산의 변환 계수를 Resources에서 주입한다.</summary>
    public static class MatchRatingCurveConfig
    {
        /// <summary>실제 JSON 해시를 과거 시뮬레이션 캐시 키에 포함한다.</summary>
        public static MatchRatingCurveBalance Load(out string contentHash)
        {
            TextAsset asset = Resources.Load<TextAsset>("NewGame/MatchRatingCurve");
            if (asset == null) throw new InvalidOperationException("경기 Rating Curve 리소스가 없습니다.");
            using SHA256 hash = SHA256.Create();
            byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(asset.text));
            var result = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest) result.Append(value.ToString("x2"));
            contentHash = result.ToString();
            return Parse(asset.text);
        }

        /// <summary>상한과 압축 기울기를 검증한 순수 C# 정의를 만든다.</summary>
        public static MatchRatingCurveBalance Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("경기 곡선 JSON이 필요합니다.");
            MatchRatingCurveData data = JsonUtility.FromJson<MatchRatingCurveData>(json);
            if (data == null || data.schemaVersion != 1) throw new ArgumentException("경기 곡선 Schema가 다릅니다.");
            return new MatchRatingCurveBalance(data.center, data.slope,
                new EffectiveRatingCapTable(data.softCap, data.hardCap, data.postSoftCapSlope));
        }
    }
#pragma warning disable 0649
    [Serializable] internal sealed class MatchRatingCurveData
    {
        public int schemaVersion, softCap, hardCap;
        public double center, slope, postSoftCapSlope;
    }
#pragma warning restore 0649
}
