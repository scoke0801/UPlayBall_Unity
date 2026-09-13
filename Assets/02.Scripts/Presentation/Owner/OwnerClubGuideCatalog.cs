using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 정보 진입 안내를 실제 성적 조건과 표정에 맞춰 순환한다.</summary>
    [Serializable]
    public sealed class OwnerClubGuideCatalog
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string condition;
            public string expression;
            public string text;

            /// <summary>화면에서 확정된 기록만 사용해 대사의 노출 조건을 판단한다.</summary>
            public bool IsEligible(OwnerClubInformationPresentationModel model)
            {
                switch (condition)
                {
                    case "Any": return true;
                    case "BeforeFirstGame": return model.Games == 0;
                    case "Played": return model.Games > 0;
                    case "WinningRecord": return model.Wins > model.Losses;
                    case "LosingRecord": return model.Losses > model.Wins;
                    default: return false;
                }
            }
        }

        public Entry[] entries;
        private int _cursor;
        private readonly Queue<string> _recent = new Queue<string>();

        /// <summary>시뮬레이션 난수를 소비하지 않고 최근 여덟 대사를 피해서 선택한다.</summary>
        public Entry Select(OwnerClubInformationPresentationModel model)
        {
            for (int offset = 0; offset < entries.Length; offset++)
            {
                // 80개와 서로소인 간격으로 표정별 저작 순서를 섞는다.
                int index = (_cursor * 31) % entries.Length;
                _cursor = (_cursor + 1) % entries.Length;
                Entry entry = entries[index];
                if (!entry.IsEligible(model) || _recent.Contains(entry.id)) continue;
                _recent.Enqueue(entry.id);
                if (_recent.Count > 8) _recent.Dequeue();
                return entry;
            }
            return entries[0];
        }

        /// <summary>기존 FrontManager 리소스 경로에서 안내 저작 데이터를 읽는다.</summary>
        public static OwnerClubGuideCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("FrontManager/OwnerClubGuides");
            if (asset == null) throw new InvalidOperationException("구단주 안내 데이터가 없습니다.");
            return JsonUtility.FromJson<OwnerClubGuideCatalog>(asset.text);
        }
    }
}
