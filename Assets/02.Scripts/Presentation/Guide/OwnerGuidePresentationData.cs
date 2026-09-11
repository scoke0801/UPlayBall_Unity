using System;
using UnityEngine;

namespace Baseball.Presentation.Guide
{
    /// <summary>안내 문구와 조정 가능한 표시 크기를 콘텐츠에서 읽는다.</summary>
    [Serializable]
    public sealed class OwnerGuidePresentationData
    {
        public string title, open, close, next, action, keep, snooze, review, current, tips;
        public string empty, snoozedEmpty, loading, error, blockedEdit, missing, saveError, arrived, resolved;
        public string required, optional, preparation, debrief, count, confirmation, rosterCounts;
        public string rosterAction, pitchingAction, analysisAction, preparationAction, teamColorAction, tacticAction;
        public float collapsedHeight, expandedHeight, textScale;
        public float conversationWidth = 1120f;
        public int fontSize;

        public static OwnerGuidePresentationData Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("FrontManager/OwnerGuidePresentation");
            if (asset == null) throw new InvalidOperationException("구단주 안내 표시 데이터가 없습니다.");
            var data = JsonUtility.FromJson<OwnerGuidePresentationData>(asset.text);
            if (data == null || data.collapsedHeight < 44 || data.expandedHeight < 240 || data.fontSize < 16 ||
                data.textScale < 1 || string.IsNullOrWhiteSpace(data.title)) throw new InvalidOperationException("구단주 안내 표시 데이터가 잘못되었습니다.");
            return data;
        }
    }
}
