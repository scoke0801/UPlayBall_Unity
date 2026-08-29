using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>뉴스가 재사용할 커리어 챕터 컷 이미지를 도메인 키로 지정한다.</summary>
    public enum NewsIllustrationKind
    {
        None,
        RegularSeasonFirst,
        PostseasonChampion,
        PostseasonMvp,
        GoldenGlove,
        RegularSeasonMvp,
        Training,
        OverseasTraining,
        Rest
    }

    /// <summary>고정된 사건 ID에서 이미지 키를 복원해 기사 문구 변경과 무관하게 썸네일을 유지한다.</summary>
    public static class NewsIllustrationResolver
    {
        public static NewsIllustrationKind Resolve(IReadOnlyList<string> sourceEventIds)
        {
            if (sourceEventIds == null)
                return NewsIllustrationKind.None;

            NewsIllustrationKind resolved = NewsIllustrationKind.None;
            for (int index = 0; index < sourceEventIds.Count; index++)
            {
                string eventId = sourceEventIds[index] ?? string.Empty;
                NewsIllustrationKind candidate = Resolve(eventId);
                if (GetPriority(candidate) > GetPriority(resolved))
                    resolved = candidate;
            }
            return resolved;
        }

        private static NewsIllustrationKind Resolve(string eventId)
        {
            if (eventId.IndexOf("_champion_", StringComparison.Ordinal) >= 0)
                return NewsIllustrationKind.PostseasonChampion;
            if (eventId.IndexOf("_award_postseason_mvp", StringComparison.Ordinal) >= 0)
                return NewsIllustrationKind.PostseasonMvp;
            if (eventId.IndexOf("_award_gold_glove_", StringComparison.Ordinal) >= 0)
                return NewsIllustrationKind.GoldenGlove;
            if (eventId.IndexOf("_award_regular_season_mvp", StringComparison.Ordinal) >= 0)
                return NewsIllustrationKind.RegularSeasonMvp;
            if (eventId.IndexOf("_regular_season_first", StringComparison.Ordinal) >= 0)
                return NewsIllustrationKind.RegularSeasonFirst;
            if (eventId.IndexOf("_activity_study_", StringComparison.Ordinal) >= 0)
                return NewsIllustrationKind.OverseasTraining;
            if (eventId.IndexOf("_activity_rest_", StringComparison.Ordinal) >= 0)
                return NewsIllustrationKind.Rest;
            if (eventId.IndexOf("_activity_", StringComparison.Ordinal) >= 0)
                return NewsIllustrationKind.Training;
            return NewsIllustrationKind.None;
        }

        private static int GetPriority(NewsIllustrationKind kind)
        {
            return kind switch
            {
                NewsIllustrationKind.PostseasonChampion => 5,
                NewsIllustrationKind.PostseasonMvp or NewsIllustrationKind.RegularSeasonMvp => 4,
                NewsIllustrationKind.GoldenGlove or NewsIllustrationKind.RegularSeasonFirst => 3,
                NewsIllustrationKind.OverseasTraining => 2,
                NewsIllustrationKind.Training or NewsIllustrationKind.Rest => 1,
                _ => 0
            };
        }
    }
}
