using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    public sealed partial class RetirementRecapService
    {
        public RetirementRecapSnapshot CreateSnapshot(CareerState career, RetirementReason reason)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (career.Retirement.Snapshot != null)
                return career.Retirement.Snapshot;

            ImportLegacySeasonHistory(career);
            TeamState team = career.World.GetTeam(career.MyPlayer.CurrentTeamId);
            ArchiveCompletedSeason(career, team);
            CareerSeasonArchive[] seasons = CopySeasons(career.Retirement.Seasons);
            CareerMemoryRecord[] memories = BuildFeaturedMemories(career, seasons);
            SeasonStatSnapshot totals = SumRegularSeasonStats(seasons);
            int careerBestSeason = SelectCareerBestSeason(career.MyPlayer.PrimaryPosition, seasons);
            CareerChoiceSnapshot choices = BuildChoices(career, seasons);
            LeagueLegacySnapshot leagueLegacy = BuildLeagueLegacy(seasons);
            FranchiseLegacySnapshot franchiseLegacy = BuildFranchiseLegacy(seasons);
            SelectTitles(career, seasons, choices, leagueLegacy, franchiseLegacy,
                out string primaryTitle, out string secondaryTitle);
            int debutSeason = seasons.Length == 0 ? career.CurrentLeague.LeagueYear : seasons[0].Season;
            int retirementSeason = career.CurrentLeague.CurrentSeason.Year;
            return new RetirementRecapSnapshot(
                CurrentSnapshotVersion,
                career.MyPlayerId,
                career.MyPlayer.Name,
                career.MyPlayer.PrimaryPosition,
                career.MyPlayer.BattingHand,
                career.MyPlayer.ThrowingHand,
                debutSeason,
                retirementSeason,
                reason,
                primaryTitle,
                secondaryTitle,
                seasons,
                memories,
                new CareerTotalStatSnapshot(totals, seasons.Length),
                choices,
                leagueLegacy,
                franchiseLegacy,
                careerBestSeason,
                SelectSignatureRecord(career.MyPlayer.PrimaryPosition, totals),
                SelectFinalNarrativeKey(reason, seasons, choices),
                reason == RetirementReason.Unsigned ? "retirement_unassigned" : "retirement_locker_room");
        }

        private static void ImportLegacySeasonHistory(CareerState career)
        {
            if (career.Retirement.Seasons.Count > 0 || career.SeasonHistory.Count == 0)
                return;

            for (int index = 0; index < career.SeasonHistory.Count; index++)
            {
                CareerSeasonHistoryRecord history = career.SeasonHistory[index];
                int estimatedAge = Math.Max(16, career.MyPlayer.Age - career.SeasonHistory.Count + index);
                career.Retirement.AddSeason(new CareerSeasonArchive(
                    history.Year,
                    history.Year,
                    estimatedAge,
                    history.LeagueLevel,
                    history.TeamId,
                    history.TeamName,
                    SelectPrimaryRole(career.MyPlayer.PrimaryPosition, null, history.Statistics),
                    0,
                    0,
                    new SeasonStatSnapshot(history.Statistics),
                    new SeasonStatSnapshot(history.PostseasonStatistics),
                    BuildLegacyAwardKeys(career.MyPlayerId, history),
                    new ContractSeasonSnapshot(FindSeasonContract(career, history.Year)),
                    new GrowthSeasonSnapshot(
                        Array.Empty<SeasonAbilitySnapshot>(),
                        Array.Empty<CareerNamedCount>(),
                        0L,
                        0),
                    new InjurySeasonSnapshot(Array.Empty<InjuryRecordSnapshot>()),
                    new PlayStyleSeasonSnapshot(null),
                    new SkillBoardSeasonSnapshot(string.Empty, Array.Empty<SkillBlockArchiveSnapshot>()),
                    Array.Empty<string>()));
            }
        }

        private static string[] BuildLegacyAwardKeys(
            int playerId,
            CareerSeasonHistoryRecord history)
        {
            var result = new List<string>();
            if (history.Awards != null)
            {
                for (int index = 0; index < history.Awards.Results.Count; index++)
                {
                    SeasonAwardResultState award = history.Awards.Results[index];
                    if (award.IncludesWinner(playerId))
                        result.Add(award.AwardId);
                }
            }
            result.Sort(StringComparer.Ordinal);
            if (history.Postseason?.PlayerTeamResult == PlayerTeamPostseasonResult.Champion)
                result.Add("champion");
            return result.ToArray();
        }

        private void RecordSeasonOutcomeMemories(CareerState career, SeasonState season, int teamId)
        {
            if (season.Awards != null)
            {
                for (int index = 0; index < season.Awards.Results.Count; index++)
                {
                    SeasonAwardResultState award = season.Awards.Results[index];
                    if (!award.IncludesWinner(career.MyPlayerId))
                        continue;
                    string id = $"award:{season.Year}:{award.AwardId}";
                    if (ContainsMemory(career.Retirement.MemoryLog, id))
                        continue;
                    career.Retirement.MemoryLog.Append(new CareerMemoryRecord(
                        id, career.MyPlayerId, season.Year, OffseasonDateIndex + 1_000 + index, teamId,
                        CareerMemoryType.Award, "career.memory.award.title", "career.memory.award.narrative",
                        0, string.Empty, 0, 92, 82, 15, 85, 88,
                        Array.Empty<MemoryStatValue>(), new[] { "award" }, "career_award"));
                }
            }

            if (season.Postseason?.PlayerTeamResult == PlayerTeamPostseasonResult.Champion)
            {
                string id = $"championship:{season.Year}:{teamId}";
                if (!ContainsMemory(career.Retirement.MemoryLog, id))
                {
                    career.Retirement.MemoryLog.Append(new CareerMemoryRecord(
                        id, career.MyPlayerId, season.Year, OffseasonDateIndex + 2_000, teamId,
                        CareerMemoryType.Championship,
                        "career.memory.championship.title", "career.memory.championship.narrative",
                        0, string.Empty, 0, 100, 92, 20, 92, 100,
                        Array.Empty<MemoryStatValue>(), new[] { "postseason", "championship" },
                        "career_championship"));
                }
            }
        }
    }
}

