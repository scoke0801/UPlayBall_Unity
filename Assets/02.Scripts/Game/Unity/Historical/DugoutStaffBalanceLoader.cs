using System;
using Baseball.Core.Historical;
using UnityEngine;

namespace Baseball.Game.Historical
{
    /// <summary>메인 스레드에서 인선 저작 JSON을 읽고 순수 경기 데이터로 전달한다.</summary>
    public static class DugoutStaffBalanceLoader
    {
        public static DugoutStaffCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("NewGame/DugoutStaffBalance");
            if (asset == null) throw new InvalidOperationException("덕아웃 효과 데이터를 불러올 수 없습니다.");
            return JsonUtility.FromJson<DugoutStaffBalanceData>(asset.text).BuildCatalog();
        }
    }
}
