using System;
using System.IO;

namespace Baseball.Game.Unity.Persistence
{
    /// <summary>모드별 기존 파일을 슬롯 1로 유지하고 나머지 슬롯을 독립 경로로 분리한다.</summary>
    public static class SaveSlotPaths
    {
        public const int SlotCount = 5;

        /// <summary>주입된 슬롯 1 경로를 기준으로 검증된 슬롯 경로를 반환한다.</summary>
        public static string GetFilePath(string slotOnePath, int slot)
        {
            if (slot < 1 || slot > SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slot), "저장 슬롯은 1~5 사이여야 합니다.");
            if (slot == 1) return slotOnePath;
            return Path.Combine(Path.GetDirectoryName(slotOnePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(slotOnePath) + ".slot" + slot + Path.GetExtension(slotOnePath));
        }
    }
}
