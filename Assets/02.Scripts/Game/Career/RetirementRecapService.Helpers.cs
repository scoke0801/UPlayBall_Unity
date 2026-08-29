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
        private static CareerMemoryRecord CreateGameMemory(
            CareerState career,
            CareerGameAdvanceResult result,
            int year,
            int teamId,
            CareerMemoryType type,
            string titleKey,
            string narrativeKey,
            int importance,
            int impact,
            int agency,
            int rarity,
            int emotion,
            string assetKey)
        {
            return new CareerMemoryRecord(
                $"game:{year}:{result.GameId}:{(int)type}",
                career.MyPlayerId,
                year,
                result.Round,
                teamId,
                type,
                titleKey,
                narrativeKey,
                result.GameId,
                string.Empty,
                0,
                importance,
                impact,
                agency,
                rarity,
                emotion,
                BuildGameStats(result),
                agency >= 50 ? new[] { "game", "player_choice" } : new[] { "game" },
                assetKey);
        }

        private static MemoryStatValue[] BuildGameStats(CareerGameAdvanceResult result)
        {
            return new[]
            {
                new MemoryStatValue("plate_appearances", result.PlateAppearances),
                new MemoryStatValue("at_bats", result.AtBats),
                new MemoryStatValue("hits", result.Hits),
                new MemoryStatValue("home_runs", result.HomeRuns),
                new MemoryStatValue("runs_batted_in", result.RunsBattedIn),
                new MemoryStatValue("outs_recorded", result.OutsRecorded),
                new MemoryStatValue("earned_runs", result.EarnedRuns),
                new MemoryStatValue("strikeouts", result.Strikeouts)
            };
        }

        private static MemoryStatValue[] BuildSeasonStats(SeasonStatSnapshot stats)
        {
            return new[]
            {
                new MemoryStatValue("games", stats.Games),
                new MemoryStatValue("hits", stats.Hits),
                new MemoryStatValue("home_runs", stats.HomeRuns),
                new MemoryStatValue("runs_batted_in", stats.RunsBattedIn),
                new MemoryStatValue("batting_average", stats.BattingAverage, "average"),
                new MemoryStatValue("outs_recorded", stats.OutsRecorded),
                new MemoryStatValue("wins", stats.Wins),
                new MemoryStatValue("earned_run_average", stats.EarnedRunAverage, "era"),
                new MemoryStatValue("strikeouts", stats.PitchingStrikeouts)
            };
        }

        private static MemoryStatValue[] BuildGrowthStats(GrowthResultRecord result)
        {
            var stats = new MemoryStatValue[result.AbilityChanges.Length + 2];
            stats[0] = new MemoryStatValue("money_spent", result.MoneySpent, "money");
            stats[1] = new MemoryStatValue("weeks_spent", result.WeeksSpent);
            for (int index = 0; index < result.AbilityChanges.Length; index++)
            {
                AbilityChange change = result.AbilityChanges[index];
                stats[index + 2] = new MemoryStatValue($"ability.{change.Ability}", change.Amount, "signed");
            }
            return stats;
        }

        private static bool HasOfficialAppearance(CareerGameAdvanceResult result) =>
            result.PlateAppearances > 0 || result.OutsRecorded > 0;

        private static int FindFirstAppearanceSeason(CareerSeasonArchive[] seasons)
        {
            for (int index = 0; index < seasons.Length; index++)
            {
                if (HasSeasonAppearance(seasons[index]))
                    return index;
            }
            return -1;
        }

        private static int FindLastAppearanceSeason(CareerSeasonArchive[] seasons)
        {
            for (int index = seasons.Length - 1; index >= 0; index--)
            {
                if (HasSeasonAppearance(seasons[index]))
                    return index;
            }
            return -1;
        }

        private static bool HasSeasonAppearance(CareerSeasonArchive season) =>
            season.Stats.PlateAppearances > 0 || season.Stats.OutsRecorded > 0 ||
            season.PostseasonStats.PlateAppearances > 0 || season.PostseasonStats.OutsRecorded > 0;

        private static void AddDerivedFirstBattingRecord(
            CareerState career,
            CareerSeasonArchive[] seasons,
            List<CareerMemoryRecord> candidates,
            CareerMemoryType type)
        {
            if (ContainsType(candidates, type))
                return;
            for (int index = 0; index < seasons.Length; index++)
            {
                CareerSeasonArchive season = seasons[index];
                int value = type == CareerMemoryType.FirstHit ? season.Stats.Hits : season.Stats.HomeRuns;
                if (value <= 0)
                    continue;
                candidates.Add(new CareerMemoryRecord(
                    $"derived:{type}:{season.Season}", career.MyPlayerId, season.Season,
                    OffseasonDateIndex - 100, season.TeamId, type,
                    type == CareerMemoryType.FirstHit
                        ? "career.memory.first_hit.title"
                        : "career.memory.first_home_run.title",
                    type == CareerMemoryType.FirstHit
                        ? "career.memory.first_hit.narrative"
                        : "career.memory.first_home_run.narrative",
                    0, string.Empty, 0, 80, 72, 0, 70, 72,
                    new[] { new MemoryStatValue(type == CareerMemoryType.FirstHit ? "hits" : "home_runs", value) },
                    new[] { "first_record", "derived_fact" }, "career_first_record"));
                return;
            }
        }

        private static void AddDerivedFirstPitchingRecord(
            CareerState career,
            CareerSeasonArchive[] seasons,
            List<CareerMemoryRecord> candidates,
            CareerMemoryType type)
        {
            if (ContainsType(candidates, type))
                return;
            for (int index = 0; index < seasons.Length; index++)
            {
                CareerSeasonArchive season = seasons[index];
                int value = type == CareerMemoryType.FirstPitchingWin ? season.Stats.Wins : season.Stats.Saves;
                if (value <= 0)
                    continue;
                candidates.Add(new CareerMemoryRecord(
                    $"derived:{type}:{season.Season}", career.MyPlayerId, season.Season,
                    OffseasonDateIndex - 100, season.TeamId, type,
                    type == CareerMemoryType.FirstPitchingWin
                        ? "career.memory.first_win.title"
                        : "career.memory.first_save.title",
                    type == CareerMemoryType.FirstPitchingWin
                        ? "career.memory.first_win.narrative"
                        : "career.memory.first_save.narrative",
                    0, string.Empty, 0, 84, 76, 0, 76, 78,
                    new[] { new MemoryStatValue(type == CareerMemoryType.FirstPitchingWin ? "wins" : "saves", value) },
                    new[] { "first_record", "derived_fact" }, "career_first_record"));
                return;
            }
        }

        private static int GetCurrentDateIndex(SeasonState season)
        {
            int dateIndex = 0;
            IReadOnlyList<ScheduledGameState> games = season.Schedule?.Games;
            if (games == null)
                return dateIndex;
            for (int index = 0; index < games.Count; index++)
            {
                if (games[index].IsCompleted && games[index].Round > dateIndex)
                    dateIndex = games[index].Round;
            }
            return dateIndex;
        }

        private static bool IsExceptionalGame(PlayerPosition position, CareerGameAdvanceResult result)
        {
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            return isPitcher
                ? result.Strikeouts >= 10 || result.OutsRecorded >= 18 && result.EarnedRuns <= 2
                : result.Hits >= 4 || result.HomeRuns >= 2 || result.RunsBattedIn >= 5;
        }

        private static bool HasPostseasonParticipation(CareerSeasonArchive season) =>
            season.PostseasonStats.Games > 0 || season.PostseasonStats.PitchingAppearances > 0;

        private static bool HasLateBreakthrough(CareerSeasonArchive[] seasons)
        {
            for (int index = 0; index < seasons.Length; index++)
            {
                if (seasons[index].Age >= 30 && seasons[index].PrimaryRole is
                    PlayerGameRole.StartingBatter or PlayerGameRole.StartingPitcher)
                    return true;
            }
            return false;
        }

        private static bool IsLongTermReliever(PlayerPosition position, CareerSeasonArchive[] seasons)
        {
            if (position != PlayerPosition.ReliefPitcher) return false;
            int years = 0;
            for (int index = 0; index < seasons.Length; index++)
            {
                if (seasons[index].PrimaryRole == PlayerGameRole.ReliefPitcher) years++;
            }
            return years >= 5;
        }

        private static bool HasPostseasonStrength(CareerSeasonArchive[] seasons)
        {
            int appearances = 0;
            for (int index = 0; index < seasons.Length; index++)
                appearances += seasons[index].PostseasonStats.Games + seasons[index].PostseasonStats.PitchingAppearances;
            return appearances >= 20;
        }

        private static bool ContainsAward(IReadOnlyList<string> awards, string awardId)
        {
            for (int index = 0; index < awards.Count; index++)
            {
                if (string.Equals(awards[index], awardId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool ContainsMemory(CareerMemoryLog log, string memoryId)
        {
            for (int index = 0; index < log.Records.Count; index++)
            {
                if (string.Equals(log.Records[index].MemoryId, memoryId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool ContainsMemory(List<CareerMemoryRecord> memories, string memoryId)
        {
            for (int index = 0; index < memories.Count; index++)
            {
                if (string.Equals(memories[index].MemoryId, memoryId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool ContainsType(List<CareerMemoryRecord> memories, CareerMemoryType type)
        {
            for (int index = 0; index < memories.Count; index++)
            {
                if (memories[index].Type == type) return true;
            }
            return false;
        }

        private static bool ContainsSeasonType(
            List<CareerMemoryRecord> memories,
            int season,
            CareerMemoryType type)
        {
            for (int index = 0; index < memories.Count; index++)
            {
                if (memories[index].Season == season && memories[index].Type == type)
                    return true;
            }
            return false;
        }

        private static int CountType(CareerMemoryLog log, CareerMemoryType type)
        {
            int count = 0;
            for (int index = 0; index < log.Records.Count; index++)
            {
                if (log.Records[index].Type == type) count++;
            }
            return count;
        }

        private static int CountType(List<CareerMemoryRecord> memories, CareerMemoryType type)
        {
            int count = 0;
            for (int index = 0; index < memories.Count; index++)
            {
                if (memories[index].Type == type) count++;
            }
            return count;
        }

        private static void AddFirstOfType(
            List<CareerMemoryRecord> source,
            List<CareerMemoryRecord> target,
            CareerMemoryType type)
        {
            CareerMemoryRecord best = null;
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index].Type != type) continue;
                if (best == null || CompareMemoryChronology(source[index], best) < 0)
                    best = source[index];
            }
            if (best != null && !ContainsMemory(target, best.MemoryId)) target.Add(best);
        }

        private static void AddHighestAgency(
            List<CareerMemoryRecord> source,
            List<CareerMemoryRecord> target)
        {
            CareerMemoryRecord best = null;
            for (int index = 0; index < source.Count; index++)
            {
                CareerMemoryRecord candidate = source[index];
                if (candidate.PlayerAgencyScore <= 0) continue;
                if (best == null || candidate.PlayerAgencyScore > best.PlayerAgencyScore ||
                    candidate.PlayerAgencyScore == best.PlayerAgencyScore && candidate.MemoryScore > best.MemoryScore)
                    best = candidate;
            }
            if (best != null && !ContainsMemory(target, best.MemoryId)) target.Add(best);
        }

        private static void AddAdversity(
            List<CareerMemoryRecord> source,
            List<CareerMemoryRecord> target)
        {
            for (int index = 0; index < source.Count; index++)
            {
                CareerMemoryType type = source[index].Type;
                if (type is not (CareerMemoryType.Injury or CareerMemoryType.InjuryReturn)) continue;
                if (!ContainsMemory(target, source[index].MemoryId)) target.Add(source[index]);
                return;
            }
        }

        private static int CompareMemoryScore(CareerMemoryRecord left, CareerMemoryRecord right)
        {
            int score = right.MemoryScore.CompareTo(left.MemoryScore);
            if (score != 0) return score;
            return string.CompareOrdinal(left.MemoryId, right.MemoryId);
        }

        private static int CompareMemoryChronology(CareerMemoryRecord left, CareerMemoryRecord right)
        {
            int season = left.Season.CompareTo(right.Season);
            if (season != 0) return season;
            int date = left.DateIndex.CompareTo(right.DateIndex);
            return date != 0 ? date : string.CompareOrdinal(left.MemoryId, right.MemoryId);
        }

        private static string SelectBiggestChoiceMemory(CareerMemoryLog log)
        {
            CareerMemoryRecord best = null;
            for (int index = 0; index < log.Records.Count; index++)
            {
                CareerMemoryRecord candidate = log.Records[index];
                if (candidate.PlayerAgencyScore <= 0) continue;
                if (best == null || candidate.PlayerAgencyScore > best.PlayerAgencyScore ||
                    candidate.PlayerAgencyScore == best.PlayerAgencyScore && candidate.MemoryScore > best.MemoryScore)
                    best = candidate;
            }
            return best?.MemoryId ?? string.Empty;
        }

        private static double FindMemoryStat(CareerMemoryRecord memory, string statKey)
        {
            for (int index = 0; index < memory.Stats.Count; index++)
            {
                if (string.Equals(memory.Stats[index].StatKey, statKey, StringComparison.Ordinal))
                    return memory.Stats[index].Value;
            }
            return 0d;
        }

        private static void AddNamedCount(List<CareerNamedCount> counts, string key, int amount)
        {
            for (int index = 0; index < counts.Count; index++)
            {
                if (!string.Equals(counts[index].Key, key, StringComparison.Ordinal)) continue;
                counts[index] = new CareerNamedCount(key, counts[index].Count + amount);
                return;
            }
            counts.Add(new CareerNamedCount(key, amount));
        }

        private static CareerNamedCount SelectHighestCount(List<CareerNamedCount> counts)
        {
            CareerNamedCount best = default;
            for (int index = 0; index < counts.Count; index++)
            {
                if (counts[index].Count > best.Count || counts[index].Count == best.Count &&
                    string.CompareOrdinal(counts[index].Key, best.Key) < 0)
                    best = counts[index];
            }
            return best;
        }

        private static int SelectHighestIndex(int[] values)
        {
            int best = 0;
            for (int index = 1; index < values.Length; index++)
            {
                if (values[index] > values[best]) best = index;
            }
            return best;
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            for (int index = 0; index < values.Length; index++) total += values[index];
            return total;
        }

        private static int CountTraining(CareerSeasonExperienceState experience)
        {
            int total = 0;
            for (int index = 0; index < experience.TrainingCounts.Count; index++)
                total += experience.TrainingCounts[index].Count;
            return total;
        }

        private struct StatAccumulator
        {
            private int _games, _gamesStarted, _plateAppearances, _atBats, _runs, _hits, _doubles, _triples;
            private int _homeRuns, _runsBattedIn, _walks, _hitByPitches, _battingStrikeouts, _stolenBases;
            private int _caughtStealing, _pitchingAppearances, _pitchingStarts, _outsRecorded, _wins, _losses;
            private int _saves, _holds, _earnedRuns, _hitsAllowed, _walksAllowed, _pitchingStrikeouts;
            private int _qualityStarts, _fieldingErrors;

            public void Add(SeasonStatSnapshot value)
            {
                _games += value.Games; _gamesStarted += value.GamesStarted;
                _plateAppearances += value.PlateAppearances; _atBats += value.AtBats;
                _runs += value.Runs; _hits += value.Hits; _doubles += value.Doubles; _triples += value.Triples;
                _homeRuns += value.HomeRuns; _runsBattedIn += value.RunsBattedIn; _walks += value.Walks;
                _hitByPitches += value.HitByPitches; _battingStrikeouts += value.BattingStrikeouts;
                _stolenBases += value.StolenBases; _caughtStealing += value.CaughtStealing;
                _pitchingAppearances += value.PitchingAppearances; _pitchingStarts += value.PitchingStarts;
                _outsRecorded += value.OutsRecorded; _wins += value.Wins; _losses += value.Losses;
                _saves += value.Saves; _holds += value.Holds; _earnedRuns += value.EarnedRuns;
                _hitsAllowed += value.HitsAllowed; _walksAllowed += value.WalksAllowed;
                _pitchingStrikeouts += value.PitchingStrikeouts; _qualityStarts += value.QualityStarts;
                _fieldingErrors += value.FieldingErrors;
            }

            public SeasonStatSnapshot ToSnapshot() => new(
                _games, _gamesStarted, _plateAppearances, _atBats, _runs, _hits, _doubles, _triples,
                _homeRuns, _runsBattedIn, _walks, _hitByPitches, _battingStrikeouts, _stolenBases,
                _caughtStealing, _pitchingAppearances, _pitchingStarts, _outsRecorded, _wins, _losses,
                _saves, _holds, _earnedRuns, _hitsAllowed, _walksAllowed, _pitchingStrikeouts,
                _qualityStarts, _fieldingErrors);
        }
    }
}

