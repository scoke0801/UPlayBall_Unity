using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>같은 경기·계약·부상 흐름의 사건을 한 기사 후보로 병합한다.</summary>
    internal sealed class NewsMergeService
    {
        public List<NewsCandidate> Merge(IReadOnlyList<NewsEvent> events)
        {
            var result = new List<NewsCandidate>();
            var byMergeKey = new Dictionary<string, NewsCandidate>(System.StringComparer.Ordinal);
            for (int index = 0; index < events.Count; index++)
            {
                NewsEvent source = events[index];
                string mergeKey = string.IsNullOrEmpty(source.MergeKey) ? source.EventId : source.MergeKey;
                if (byMergeKey.TryGetValue(mergeKey, out NewsCandidate existing))
                {
                    existing.Merge(source);
                    continue;
                }

                var candidate = new NewsCandidate(source);
                byMergeKey.Add(mergeKey, candidate);
                result.Add(candidate);
            }
            return result;
        }
    }
}
