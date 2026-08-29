using System.Collections.Generic;
using Baseball.Game.Career.News;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>Resources의 8개 정의를 한 번만 읽어 연출과 뉴스 썸네일이 같은 Sprite를 공유하게 한다.</summary>
    public static class CareerPresentationAssetLibrary
    {
        private const string ResourcePath = "UI";
        private static Dictionary<CareerPresentationType, CareerPresentationData> _dataByType;

        public static CareerPresentationData Get(CareerPresentationType type)
        {
            EnsureLoaded();
            return _dataByType.TryGetValue(type, out CareerPresentationData data) ? data : null;
        }

        public static Sprite GetIllustration(NewsIllustrationKind kind)
        {
            CareerPresentationType? type = kind switch
            {
                NewsIllustrationKind.RegularSeasonFirst => CareerPresentationType.RegularSeasonFirst,
                NewsIllustrationKind.PostseasonChampion => CareerPresentationType.PostseasonChampion,
                NewsIllustrationKind.PostseasonMvp => CareerPresentationType.PostseasonMvp,
                NewsIllustrationKind.GoldenGlove => CareerPresentationType.GoldenGlove,
                NewsIllustrationKind.RegularSeasonMvp => CareerPresentationType.RegularSeasonMvp,
                NewsIllustrationKind.Training => CareerPresentationType.Training,
                NewsIllustrationKind.OverseasTraining => CareerPresentationType.OverseasTraining,
                NewsIllustrationKind.Rest => CareerPresentationType.Rest,
                _ => null
            };
            return type.HasValue ? Get(type.Value)?.Illustration : null;
        }

        private static void EnsureLoaded()
        {
            if (_dataByType != null)
                return;
            _dataByType = new Dictionary<CareerPresentationType, CareerPresentationData>();
            CareerPresentationData[] data = Resources.LoadAll<CareerPresentationData>(ResourcePath);
            for (int index = 0; index < data.Length; index++)
            {
                CareerPresentationData item = data[index];
                if (item != null && !_dataByType.ContainsKey(item.Type))
                    _dataByType.Add(item.Type, item);
            }
        }
    }
}
