using System;
using Baseball.Core.Historical;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>구단주 전용 성장 저작 데이터를 순수 밸런스에 주입한다.</summary>
    internal static class OwnerDevelopmentConfig
    {
        public static OwnerDevelopmentBalance Load()
        {
            var asset = Resources.Load<TextAsset>("NewGame/OwnerDevelopment");
            if (asset == null) throw new InvalidOperationException("구단주 성장 설정이 없습니다.");
            var data = JsonUtility.FromJson<OwnerDevelopmentBalance>(asset.text); data.Validate(); return data;
        }
    }
}
