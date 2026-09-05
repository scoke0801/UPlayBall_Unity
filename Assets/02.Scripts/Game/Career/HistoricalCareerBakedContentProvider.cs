using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Teams;
using Baseball.Game.Historical;

namespace Baseball.Game.Career
{
    /// <summary>Canonical Historical Content와 Simulated World를 선수 커리어의 일반 선수 입력으로 변환한다.</summary>
    public sealed class HistoricalCareerBakedContentProvider : ICareerBakedContentProvider
    {
        private readonly IHistoricalContentProvider _contentProvider;
        private readonly HistoricalWorldRuntimeBuilder _worldBuilder;
        private readonly int[] _leagueSeasonYears;
        private CareerBakedContent _cachedContent;
        private ulong _cachedWorldSeed;

        public HistoricalCareerBakedContentProvider(
            IHistoricalContentProvider contentProvider,
            BalanceTable balance,
            IReadOnlyList<int> leagueSeasonYears,
            IBakedWorldHistorySource bakedWorldHistorySource = null)
        {
            _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
            _worldBuilder = new HistoricalWorldRuntimeBuilder(
                balance ?? throw new ArgumentNullException(nameof(balance)),
                bakedHistorySource: bakedWorldHistorySource);
            int gradeCount = Enum.GetValues(typeof(LeagueGrade)).Length;
            if (leagueSeasonYears == null || leagueSeasonYears.Count != gradeCount)
            {
                throw new ArgumentException(
                    $"Career LeagueGrade {gradeCount}개에 대응하는 Historical 연도 정책이 필요합니다.",
                    nameof(leagueSeasonYears));
            }
            _leagueSeasonYears = new int[gradeCount];
            var uniqueYears = new HashSet<int>();
            for (int index = 0; index < gradeCount; index++)
            {
                int year = leagueSeasonYears[index];
                if (year <= 0 || !uniqueYears.Add(year))
                    throw new ArgumentException("Historical 리그 배치 연도는 양수이며 서로 달라야 합니다.", nameof(leagueSeasonYears));
                _leagueSeasonYears[index] = year;
            }
        }

        public CareerBakedContent Load(CareerBakedContentRequest request)
        {
            if (request.RecordMode != WorldRecordMode.SimulatedHistory)
                throw new InvalidOperationException("Production 커리어 Content는 SimulatedHistory만 지원합니다.");
            if (_cachedContent != null && _cachedWorldSeed == request.WorldHistorySeed)
                return _cachedContent;

            HistoricalBakedContent baked = _contentProvider.Load()
                ?? throw new InvalidOperationException("Historical Content Provider가 null을 반환했습니다.");
            HistoricalWorldRuntimeContent world = _worldBuilder.GetOrBuild(
                baked,
                WorldRecordMode.SimulatedHistory,
                request.WorldHistorySeed);
            CareerBakedTeamRuntimeDefinition[] teams = CreateCareerTeams(baked, world, _leagueSeasonYears);
            _cachedWorldSeed = request.WorldHistorySeed;
            _cachedContent = new CareerBakedContent(
                baked.Manifest.SourceManifest,
                baked.PlayerPersons,
                world.WorldCardCatalog,
                teams,
                world.WorldHistory,
                world.IdentityRegistry);
            return _cachedContent;
        }

        private static CareerBakedTeamRuntimeDefinition[] CreateCareerTeams(
            HistoricalBakedContent baked,
            HistoricalWorldRuntimeContent world,
            IReadOnlyList<int> leagueSeasonYears)
        {
            int gradeCount = Enum.GetValues(typeof(LeagueGrade)).Length;
            int teamId = 1;
            var result = new CareerBakedTeamRuntimeDefinition[
                gradeCount * LeagueInstance.MaximumRegularFranchiseTeamCount];
            TeamArchetypeProfile[] archetypes = TeamArchetypeLibrary.CreateDefaultPool();
            int outputIndex = 0;
            for (int gradeIndex = 0; gradeIndex < gradeCount; gradeIndex++)
            {
                HistoricalYearContentDefinition year = FindConfiguredYear(
                    baked.Years,
                    leagueSeasonYears[gradeIndex]);
                if (year.TeamSeasons.Count != LeagueInstance.MaximumRegularFranchiseTeamCount)
                {
                    throw new InvalidOperationException(
                        $"Career 리그 배치 연도 {year.Year}는 정본 10구단 시즌이어야 합니다.");
                }
                TeamSeasonDefinition[] orderedTeams = CopyAndSort(year.TeamSeasons);
                for (int index = 0; index < orderedTeams.Length; index++)
                {
                    TeamSeasonDefinition team = orderedTeams[index];
                    string displayName = world.IdentityRegistry.GetFranchiseDisplayName(team.FranchiseId);
                    int stableHash = CreateStablePositiveHash(team.FranchiseId);
                    result[outputIndex++] = new CareerBakedTeamRuntimeDefinition(
                        teamId,
                        (LeagueGrade)gradeIndex,
                        team,
                        CreateRoster(team, world.WorldCardCatalog),
                        new TeamIdentityDefinition(displayName, CreateColor(stableHash)),
                        archetypes[stableHash % archetypes.Length],
                        emblemId: teamId);
                    teamId++;
                }
            }
            return result;
        }

        private static HistoricalYearContentDefinition FindConfiguredYear(
            IReadOnlyList<HistoricalYearContentDefinition> years,
            int configuredYear)
        {
            for (int index = 0; index < years.Count; index++)
            {
                if (years[index].Year == configuredYear)
                    return years[index];
            }
            throw new InvalidOperationException(
                $"Career Historical 리그 배치 연도를 찾을 수 없습니다: {configuredYear}");
        }

        private static CurrentRosterState CreateRoster(TeamSeasonDefinition team, WorldCardCatalog catalog)
        {
            var entries = new ActiveRosterEntry[team.Core25CardIds.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                string cardId = team.Core25CardIds[index];
                if (!catalog.TryGetCard(cardId, out PlayerCardDefinition card) ||
                    card.Edition != PlayerCardEdition.Normal)
                    throw new InvalidOperationException($"{team.TeamSeasonKey} Core25 Normal Card가 없습니다.");
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                entries[index] = new ActiveRosterEntry(
                    card.CardId,
                    season.PlayerSeasonId,
                    season.PlayerPersonId,
                    season.RegistrationType,
                    GetCore25Role(index));
            }
            return new CurrentRosterState(team.TeamSeasonKey, entries);
        }

        private static ActiveRosterRole GetCore25Role(int index)
        {
            if (index < ActiveRosterCompositionRule.StartingHitterCount)
                return (ActiveRosterRole)index;
            if (index < ActiveRosterCompositionRule.HitterCount)
                return ActiveRosterRole.BenchHitter;
            return (ActiveRosterRole)(
                (int)ActiveRosterRole.StartingPitcher1 + index - ActiveRosterCompositionRule.HitterCount);
        }

        private static TeamSeasonDefinition[] CopyAndSort(IReadOnlyList<TeamSeasonDefinition> source)
        {
            var result = new TeamSeasonDefinition[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index];
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.TeamSeasonKey, right.TeamSeasonKey));
            return result;
        }

        private static TeamColor CreateColor(int hash)
        {
            return new TeamColor(
                (byte)(48 + hash % 160),
                (byte)(48 + hash / 7 % 160),
                (byte)(48 + hash / 49 % 160));
        }

        private static int CreateStablePositiveHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
