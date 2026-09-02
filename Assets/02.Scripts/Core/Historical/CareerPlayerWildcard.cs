using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>Career Player 명예 Wildcard의 시즌 내 발표 시점을 구분한다.</summary>
    public enum CareerSeasonPhase
    {
        BeforeAllStarSelection,
        AfterAllStarSelection,
        AfterSeasonAwards
    }

    /// <summary>고정 시즌 카드가 없는 Career Player의 현재 구단 정체성과 실제 수상 명예를 계산한다.</summary>
    public sealed class CareerPlayerWildcard
    {
        private readonly string _careerPlayerAwardId;

        public CareerPlayerWildcard(string careerPlayerAwardId)
        {
            if (string.IsNullOrWhiteSpace(careerPlayerAwardId))
                throw new ArgumentException("Career Player Award 식별자는 비어 있을 수 없습니다.", nameof(careerPlayerAwardId));
            _careerPlayerAwardId = careerPlayerAwardId.Trim();
        }

        /// <summary>이적을 즉시 반영하도록 저장된 Origin 대신 현재 TeamSeason에서 정체성 Key를 만든다.</summary>
        public TeamColorEligibilityKey ResolveIdentity(TeamSeasonDefinition currentTeam)
        {
            if (currentTeam == null)
                throw new ArgumentNullException(nameof(currentTeam));

            return new TeamColorEligibilityKey(
                currentTeam.OriginYear,
                currentTeam.FranchiseId,
                currentTeam.TeamSeasonKey,
                PlayerCardEdition.Normal);
        }

        /// <summary>실제 World Award 발표 시점과 유효 기간을 만족하는 명예 Edition만 반환한다.</summary>
        public IReadOnlyList<PlayerCardEdition> ResolveHonorEditions(
            WorldAwardRecord awards,
            int currentSeasonYear,
            CareerSeasonPhase phase)
        {
            if (awards == null)
                throw new ArgumentNullException(nameof(awards));
            if (currentSeasonYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentSeasonYear));

            bool hasAllStar = false;
            bool hasGoldenGlove = false;
            bool hasMvp = false;
            IReadOnlyList<WorldAwardEntry> entries = awards.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                WorldAwardEntry entry = entries[index];
                if (!string.Equals(entry.PlayerSeasonId, _careerPlayerAwardId, StringComparison.Ordinal))
                    continue;

                switch (entry.AwardType)
                {
                    case WorldAwardType.AllStar:
                        if (entry.SeasonYear == currentSeasonYear &&
                            phase >= CareerSeasonPhase.AfterAllStarSelection)
                            hasAllStar = true;
                        break;
                    case WorldAwardType.GoldenGlove:
                        if (IsPostseasonHonorActive(entry.SeasonYear, currentSeasonYear, phase))
                            hasGoldenGlove = true;
                        break;
                    case WorldAwardType.RegularSeasonMvp:
                    case WorldAwardType.AllStarGameMvp:
                    case WorldAwardType.PostseasonMvp:
                        if (IsPostseasonHonorActive(entry.SeasonYear, currentSeasonYear, phase))
                            hasMvp = true;
                        break;
                }
            }

            var result = new List<PlayerCardEdition>(3);
            if (hasAllStar)
                result.Add(PlayerCardEdition.AllStar);
            if (hasGoldenGlove)
                result.Add(PlayerCardEdition.GoldenGlove);
            if (hasMvp)
                result.Add(PlayerCardEdition.Mvp);
            return result;
        }

        private static bool IsPostseasonHonorActive(
            int awardSeasonYear,
            int currentSeasonYear,
            CareerSeasonPhase phase)
        {
            if (currentSeasonYear == awardSeasonYear)
                return phase >= CareerSeasonPhase.AfterSeasonAwards;
            return currentSeasonYear == awardSeasonYear + 1;
        }
    }
}
