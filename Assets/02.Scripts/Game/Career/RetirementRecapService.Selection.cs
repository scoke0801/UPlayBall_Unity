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
        private CareerMemoryRecord[] BuildFeaturedMemories(
            CareerState career,
            CareerSeasonArchive[] seasons)
        {
            var candidates = new List<CareerMemoryRecord>();
            for (int index = 0; index < career.Retirement.MemoryLog.Records.Count; index++)
                candidates.Add(career.Retirement.MemoryLog.Records[index]);
            AddDerivedMemories(career, seasons, candidates);
            candidates.Sort(CompareMemoryScore);

            var selected = new List<CareerMemoryRecord>(7);
            AddFirstOfType(candidates, selected, CareerMemoryType.CareerDebut);
            AddFirstOfType(candidates, selected, CareerMemoryType.FinalAppearance);
            AddHighestAgency(candidates, selected);
            AddAdversity(candidates, selected);
            for (int index = 0; index < candidates.Count && selected.Count < 7; index++)
            {
                CareerMemoryRecord candidate = candidates[index];
                if (ContainsMemory(selected, candidate.MemoryId) || CountType(selected, candidate.Type) >= 2)
                    continue;
                selected.Add(candidate);
            }
            selected.Sort(CompareMemoryChronology);
            return selected.ToArray();
        }

        private static void AddDerivedMemories(
            CareerState career,
            CareerSeasonArchive[] seasons,
            List<CareerMemoryRecord> candidates)
        {
            if (seasons.Length == 0)
                return;
            int firstAppearanceIndex = FindFirstAppearanceSeason(seasons);
            if (!ContainsType(candidates, CareerMemoryType.CareerDebut) && firstAppearanceIndex >= 0)
            {
                CareerSeasonArchive first = seasons[firstAppearanceIndex];
                candidates.Add(new CareerMemoryRecord(
                    $"derived:debut:{first.Season}", career.MyPlayerId, first.Season, 0, first.TeamId,
                    CareerMemoryType.CareerDebut,
                    "career.memory.debut.title", "career.memory.debut.narrative",
                    0, string.Empty, 0, 90, 90, 0, 95, 85,
                    Array.Empty<MemoryStatValue>(), new[] { "debut", "derived_fact" }, "career_debut"));
            }
            int finalAppearanceIndex = FindLastAppearanceSeason(seasons);
            if (!ContainsType(candidates, CareerMemoryType.FinalAppearance) && finalAppearanceIndex >= 0)
            {
                CareerSeasonArchive last = seasons[finalAppearanceIndex];
                int finalGameId = career.Retirement.LastOfficialGameId;
                int finalRound = career.Retirement.LastOfficialGameRound;
                int finalTeamId = career.Retirement.LastOfficialGameTeamId;
                if (career.Retirement.LastOfficialGameYear > 0)
                {
                    for (int index = seasons.Length - 1; index >= 0; index--)
                    {
                        if (seasons[index].Season != career.Retirement.LastOfficialGameYear)
                            continue;
                        last = seasons[index];
                        break;
                    }
                }
                candidates.Add(new CareerMemoryRecord(
                    $"derived:final:{last.Season}:{finalGameId}", career.MyPlayerId, last.Season,
                    int.MaxValue,
                    finalTeamId > 0 ? finalTeamId : last.TeamId,
                    CareerMemoryType.FinalAppearance,
                    "career.memory.final_appearance.title", "career.memory.final_appearance.narrative",
                    finalGameId, string.Empty, 0, 100, 100, 80, 100, 100,
                    finalRound > 0
                        ? new[]
                        {
                            new MemoryStatValue("games", last.Stats.Games),
                            new MemoryStatValue("round", finalRound)
                        }
                        : new[] { new MemoryStatValue("games", last.Stats.Games) },
                    new[] { "final", "derived_fact" }, "career_retirement"));
            }

            AddDerivedFirstBattingRecord(career, seasons, candidates, CareerMemoryType.FirstHit);
            AddDerivedFirstBattingRecord(career, seasons, candidates, CareerMemoryType.FirstHomeRun);
            AddDerivedFirstPitchingRecord(career, seasons, candidates, CareerMemoryType.FirstPitchingWin);
            AddDerivedFirstPitchingRecord(career, seasons, candidates, CareerMemoryType.FirstSave);
            for (int seasonIndex = 0; seasonIndex < seasons.Length; seasonIndex++)
            {
                CareerSeasonArchive season = seasons[seasonIndex];
                if (season.Injuries.Injuries.Count == 0 || ContainsSeasonType(candidates, season.Season, CareerMemoryType.Injury))
                    continue;
                candidates.Add(new CareerMemoryRecord(
                    $"derived:injury:{season.Season}", career.MyPlayerId, season.Season,
                    OffseasonDateIndex - 60, season.TeamId, CareerMemoryType.Injury,
                    "career.memory.injury.title", "career.memory.injury.narrative",
                    0, string.Empty, 0, 72, 68, 20, 45, 82,
                    new[] { new MemoryStatValue("injury_count", season.Injuries.Injuries.Count) },
                    new[] { "adversity", "derived_fact" }, "career_injury"));

                if (seasonIndex + 1 >= seasons.Length || seasons[seasonIndex + 1].Stats.Games == 0)
                    continue;
                CareerSeasonArchive returned = seasons[seasonIndex + 1];
                candidates.Add(new CareerMemoryRecord(
                    $"derived:return:{returned.Season}", career.MyPlayerId, returned.Season,
                    1, returned.TeamId, CareerMemoryType.InjuryReturn,
                    "career.memory.injury_return.title", "career.memory.injury_return.narrative",
                    0, string.Empty, 0, 78, 80, 30, 55, 88,
                    new[] { new MemoryStatValue("games", returned.Stats.Games) },
                    new[] { "adversity", "recovery", "derived_fact" }, "career_return"));
            }
            int bestIndex = SelectCareerBestSeason(career.MyPlayer.PrimaryPosition, seasons, returnIndex: true);
            if (bestIndex >= 0 && HasSeasonAppearance(seasons[bestIndex]))
            {
                CareerSeasonArchive best = seasons[bestIndex];
                candidates.Add(new CareerMemoryRecord(
                    $"derived:career_high:{best.Season}", career.MyPlayerId, best.Season, OffseasonDateIndex - 30,
                    best.TeamId, CareerMemoryType.ExceptionalGame,
                    "career.memory.career_high_season.title", "career.memory.career_high_season.narrative",
                    0, string.Empty, 0, 95, 88, 10, 82, 90,
                    BuildSeasonStats(best.Stats), new[] { "career_high", "derived_fact" }, "career_high"));
            }
        }

        private CareerChoiceSnapshot BuildChoices(CareerState career, CareerSeasonArchive[] seasons)
        {
            var teams = new HashSet<int>();
            var training = new List<CareerNamedCount>();
            var skillDurations = new List<CareerNamedCount>();
            int[] roleSeasons = new int[6];
            int[] battingApproaches = new int[6];
            int[] pitchingApproaches = new int[6];
            int trainingCount = 0;
            int studyCount = 0;
            int postseasonCount = 0;
            int championshipCount = 0;
            long highestDeclinedAnnualSalary = 0L;
            string highestDeclinedMemoryId = string.Empty;
            int longestAcceptedContractYears = 0;
            for (int seasonIndex = 0; seasonIndex < seasons.Length; seasonIndex++)
            {
                CareerSeasonArchive season = seasons[seasonIndex];
                teams.Add(season.TeamId);
                roleSeasons[(int)season.PrimaryRole]++;
                studyCount += season.Growth.StudyCount;
                for (int index = 0; index < season.Growth.TrainingCounts.Count; index++)
                {
                    CareerNamedCount count = season.Growth.TrainingCounts[index];
                    trainingCount += count.Count;
                    AddNamedCount(training, count.Key, count.Count);
                }
                for (int index = 0; index < season.SkillBoard.Blocks.Count; index++)
                    AddNamedCount(skillDurations, season.SkillBoard.Blocks[index].DefinitionId, 1);
                for (int index = 0; index < 6; index++)
                {
                    battingApproaches[index] += season.PlayStyle.GetBattingApproachCount((BattingApproach)index);
                    pitchingApproaches[index] += season.PlayStyle.GetPitchingApproachCount((PitchingApproach)index);
                }
                if (HasPostseasonParticipation(season)) postseasonCount++;
                if (ContainsAward(season.Awards, "champion")) championshipCount++;
            }

            int renewals = 0;
            int transfers = 0;
            for (int index = 0; index < career.World.MovementLedger.Records.Count; index++)
            {
                PlayerMovementRecord movement = career.World.MovementLedger.Records[index];
                if (movement.PlayerId != career.MyPlayerId)
                    continue;
                if (movement.MovementType is PlayerMovementType.CurrentTeamExtension or
                    PlayerMovementType.CurrentTeamRenewal)
                    renewals++;
                else if (movement.MovementType is PlayerMovementType.SameLeagueTransfer or
                    PlayerMovementType.Trade or PlayerMovementType.Promotion or PlayerMovementType.Rehabilitation)
                    transfers++;
            }
            for (int index = 0; index < career.Retirement.MemoryLog.Records.Count; index++)
            {
                CareerMemoryRecord memory = career.Retirement.MemoryLog.Records[index];
                if (memory.Type == CareerMemoryType.ContractDeclined)
                {
                    long salary = (long)FindMemoryStat(memory, "annual_salary");
                    if (salary > highestDeclinedAnnualSalary)
                    {
                        highestDeclinedAnnualSalary = salary;
                        highestDeclinedMemoryId = memory.MemoryId;
                    }
                }
                else if (memory.Type == CareerMemoryType.ContractAccepted)
                {
                    int years = (int)FindMemoryStat(memory, "contract_years");
                    if (years > longestAcceptedContractYears)
                        longestAcceptedContractYears = years;
                }
            }
            int injuryReturns = CountType(career.Retirement.MemoryLog, CareerMemoryType.InjuryReturn);
            CareerNamedCount mostTraining = SelectHighestCount(training);
            CareerNamedCount longestSkill = SelectHighestCount(skillDurations);
            int roleIndex = SelectHighestIndex(roleSeasons);
            bool isPitcher = career.MyPlayer.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            int[] approaches = isPitcher ? pitchingApproaches : battingApproaches;
            int approachIndex = SelectHighestIndex(approaches);
            int approachTotal = Sum(approaches);
            string approachKey = isPitcher
                ? $"pitching.{(PitchingApproach)approachIndex}"
                : $"batting.{(BattingApproach)approachIndex}";
            return new CareerChoiceSnapshot(
                teams.Count,
                career.ContractHistory.Count,
                renewals,
                transfers,
                trainingCount,
                studyCount,
                injuryReturns,
                postseasonCount,
                championshipCount,
                mostTraining,
                ((PlayerGameRole)roleIndex).ToString(),
                roleSeasons[roleIndex],
                longestSkill.Key,
                longestSkill.Count,
                approachTotal == 0 ? string.Empty : approachKey,
                approachTotal == 0 ? 0 : approaches[approachIndex],
                approachTotal,
                SelectBiggestChoiceMemory(career.Retirement.MemoryLog),
                highestDeclinedAnnualSalary,
                highestDeclinedMemoryId,
                longestAcceptedContractYears);
        }

        private static LeagueLegacySnapshot BuildLeagueLegacy(CareerSeasonArchive[] seasons)
        {
            int awards = 0;
            int championships = 0;
            for (int index = 0; index < seasons.Length; index++)
            {
                awards += seasons[index].Awards.Count;
                if (ContainsAward(seasons[index].Awards, "champion")) championships++;
            }
            var records = new List<string>();
            if (seasons.Length >= 10) records.Add("legacy.ten_seasons");
            if (championships > 0) records.Add("legacy.championship");
            if (awards == 0 && seasons.Length >= 7) records.Add("legacy.long_career_without_award");
            return new LeagueLegacySnapshot(awards, championships, records.ToArray());
        }

        private static FranchiseLegacySnapshot BuildFranchiseLegacy(CareerSeasonArchive[] seasons)
        {
            int teamId = 0;
            string teamName = string.Empty;
            int bestSeasons = 0;
            int bestGames = 0;
            for (int index = 0; index < seasons.Length; index++)
            {
                int count = 0;
                int games = 0;
                for (int other = 0; other < seasons.Length; other++)
                {
                    if (seasons[other].TeamId != seasons[index].TeamId) continue;
                    count++;
                    games += seasons[other].Stats.Games;
                }
                if (count > bestSeasons || count == bestSeasons && seasons[index].TeamId < teamId)
                {
                    teamId = seasons[index].TeamId;
                    teamName = seasons[index].TeamName;
                    bestSeasons = count;
                    bestGames = games;
                }
            }
            var records = new List<string>();
            if (bestSeasons >= 10) records.Add("franchise.ten_seasons");
            if (seasons.Length > 0 && bestSeasons == seasons.Length) records.Add("franchise.one_club");
            return new FranchiseLegacySnapshot(teamId, teamName, bestSeasons, bestGames, records.ToArray());
        }

        private static void SelectTitles(
            CareerState career,
            CareerSeasonArchive[] seasons,
            CareerChoiceSnapshot choices,
            LeagueLegacySnapshot leagueLegacy,
            FranchiseLegacySnapshot franchiseLegacy,
            out string primary,
            out string secondary)
        {
            if (seasons.Length <= 3)
                primary = "career.title.short_but_clear";
            else if (leagueLegacy.AwardCount >= 5)
                primary = "career.title.era_defining";
            else if (franchiseLegacy.Seasons == seasons.Length && seasons.Length >= 10)
                primary = "career.title.franchise_face";
            else if (HasLateBreakthrough(seasons))
                primary = "career.title.late_bloomer";
            else if (choices.InjuryReturnCount > 0)
                primary = "career.title.rose_again";
            else if (IsLongTermReliever(career.MyPlayer.PrimaryPosition, seasons))
                primary = "career.title.always_ready";
            else if (choices.TeamCount >= 3)
                primary = "career.title.many_cities";
            else
                primary = "career.title.built_with_consistency";

            if (choices.ChampionshipCount > 0 || HasPostseasonStrength(seasons))
                secondary = "career.title.strong_in_autumn";
            else if (franchiseLegacy.Seasons >= Math.Max(5, seasons.Length * 3 / 4))
                secondary = "career.title.became_team_name";
            else if (choices.TeamCount >= 3)
                secondary = "career.title.found_a_place_everywhere";
            else
                secondary = string.Empty;
        }

        private static string SelectFinalNarrativeKey(
            RetirementReason reason,
            CareerSeasonArchive[] seasons,
            CareerChoiceSnapshot choices)
        {
            if (reason == RetirementReason.Medical) return "career.retirement.final.medical";
            if (reason == RetirementReason.Unsigned) return "career.retirement.final.unsigned";
            if (seasons.Length <= 3) return "career.retirement.final.short";
            if (choices.TeamCount == 1 && seasons.Length >= 7) return "career.retirement.final.franchise";
            if (choices.TeamCount >= 3) return "career.retirement.final.journeyman";
            if (reason == RetirementReason.DeclaredFinalSeason) return "career.retirement.final.declared";
            return "career.retirement.final.voluntary";
        }

        private static CareerSignatureRecordSnapshot SelectSignatureRecord(
            PlayerPosition position,
            SeasonStatSnapshot totals)
        {
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            if (isPitcher)
            {
                if (totals.PitchingStrikeouts >= 1_000)
                    return new CareerSignatureRecordSnapshot("strikeouts", totals.PitchingStrikeouts, "number");
                if (totals.Wins >= 100)
                    return new CareerSignatureRecordSnapshot("wins", totals.Wins, "number");
                return new CareerSignatureRecordSnapshot("pitching_appearances", totals.PitchingAppearances, "number");
            }
            if (totals.Hits >= 1_000)
                return new CareerSignatureRecordSnapshot("hits", totals.Hits, "number");
            if (totals.HomeRuns >= 200)
                return new CareerSignatureRecordSnapshot("home_runs", totals.HomeRuns, "number");
            return new CareerSignatureRecordSnapshot("games", totals.Games, "number");
        }

        private static SeasonStatSnapshot SumRegularSeasonStats(CareerSeasonArchive[] seasons)
        {
            var accumulator = new StatAccumulator();
            for (int index = 0; index < seasons.Length; index++)
                accumulator.Add(seasons[index].Stats);
            return accumulator.ToSnapshot();
        }

        private static int SelectCareerBestSeason(
            PlayerPosition position,
            CareerSeasonArchive[] seasons,
            bool returnIndex = false)
        {
            int bestIndex = -1;
            double bestScore = double.MinValue;
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            for (int index = 0; index < seasons.Length; index++)
            {
                if (!HasSeasonAppearance(seasons[index]))
                    continue;
                SeasonStatSnapshot stats = seasons[index].Stats;
                double score = isPitcher
                    ? stats.OutsRecorded * 0.08d + stats.PitchingStrikeouts * 0.18d +
                      stats.Wins * 2d + stats.Saves * 1.5d - stats.EarnedRunAverage * 6d
                    : stats.Games * 0.08d + stats.OnBasePercentage * 80d +
                      stats.SluggingPercentage * 90d + stats.HomeRuns * 0.8d + stats.RunsBattedIn * 0.15d;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = index;
                }
            }
            if (returnIndex) return bestIndex;
            return bestIndex < 0 ? 0 : seasons[bestIndex].Season;
        }
    }
}

