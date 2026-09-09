using UnityEngine;

namespace Baseball.Editor.SpriteSheets
{
    /// <summary>원본 해시·메타데이터·결과 이미지가 같으면 검수 편집을 보존한다.</summary>
    public sealed class SpriteSheetImportReceipt : ScriptableObject
    {
        public string fingerprint;
    }
}
