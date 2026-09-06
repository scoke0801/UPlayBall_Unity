using Baseball.Core.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>작전 카드 3계열 공용 아트를 Resources에서 한 번만 읽어 UI에 제공한다.</summary>
    public static class TacticCardArtwork
    {
        public const string BattingKey = "tactic-batting";
        public const string PitchingKey = "tactic-pitching";
        public const string CommonKey = "tactic-common";
        public const string ScheduleTokenKey = "tactic-schedule-token";

        private static Texture2D _batting;
        private static Texture2D _pitching;
        private static Texture2D _common;
        private static Texture2D _scheduleToken;

        public static string GetKey(TacticCardCategory category)
        {
            return category switch
            {
                TacticCardCategory.Batting => BattingKey,
                TacticCardCategory.Pitching => PitchingKey,
                // 분석 카드는 별도 시각 계열을 늘리지 않고 공용 작전 아트를 사용한다.
                TacticCardCategory.Analysis => CommonKey,
                TacticCardCategory.Common => CommonKey,
                _ => CommonKey
            };
        }

        public static Texture2D Load(string artworkKey)
        {
            switch (artworkKey)
            {
                case BattingKey:
                    return _batting != null
                        ? _batting
                        : _batting = Resources.Load<Texture2D>("UI/TacticCards/tactic_batting");
                case PitchingKey:
                    return _pitching != null
                        ? _pitching
                        : _pitching = Resources.Load<Texture2D>("UI/TacticCards/tactic_pitching");
                case CommonKey:
                    return _common != null
                        ? _common
                        : _common = Resources.Load<Texture2D>("UI/TacticCards/tactic_common");
                case ScheduleTokenKey:
                    return _scheduleToken != null
                        ? _scheduleToken
                        : _scheduleToken = Resources.Load<Texture2D>("UI/TacticCards/tactic_schedule_token");
                default:
                    return null;
            }
        }

        public static RawImage Create(Transform parent, string name, string artworkKey, Color tint)
        {
            var artworkObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            artworkObject.transform.SetParent(parent, false);
            RawImage artwork = artworkObject.GetComponent<RawImage>();
            artwork.texture = Load(artworkKey);
            artwork.color = tint;
            artwork.raycastTarget = false;
            return artwork;
        }
    }
}
