using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>외부 구단 집합을 기존 중립 진단 Reader로 읽고 홈·원정 및 25개 선발 조합을 균등 대결한다.</summary>
        private static int RunHistoricalStrength(string[] args)
        {
            if (args.Length != 5)
                throw new ArgumentException("historical-strength <Runtime Archive> <집합 JSON> <대진당 경기수: 50의 배수> <결과 JSON>");
            int gamesPerPair = int.Parse(args[3]);
            if (gamesPerPair <= 0 || gamesPerPair % 50 != 0)
                throw new ArgumentException("홈·원정과 5×5 선발 조합을 맞추려면 대진당 경기수가 50의 배수여야 합니다.");
            var balance = BalanceTable.CreateDefault();
            using JsonDocument config = JsonDocument.Parse(File.ReadAllText(args[2]));
            var teamCache = new Dictionary<int, Dictionary<string, StrengthTeam>>();
            var reports = new List<object>();
            int gameId = 0;
            int determinismChecks = 0;
            long checksum = 0;
            foreach (JsonElement cohort in config.RootElement.GetProperty("cohorts").EnumerateArray())
            {
                string label = cohort.GetProperty("label").GetString();
                var teams = new List<StrengthTeam>();
                foreach (JsonElement member in cohort.GetProperty("teams").EnumerateArray())
                {
                    int year = member.GetProperty("year").GetInt32();
                    if (!teamCache.TryGetValue(year, out Dictionary<string, StrengthTeam> yearTeams))
                    {
                        yearTeams = ReadStrengthTeams(args[1], year);
                        teamCache.Add(year, yearTeams);
                    }
                    string key = member.GetProperty("teamSeasonKey").GetString();
                    if (teams.Any(t => t.Key == key)) throw new ArgumentException("같은 집합에 중복된 구단 시즌이 있습니다.");
                    StrengthTeam source = yearTeams[key];
                    teams.Add(new StrengthTeam { Key = key, Name = member.GetProperty("name").GetString(), Rotations = source.Rotations });
                }
                if (teams.Count < 2) throw new ArgumentException("비교 집합에는 두 구단 이상이 필요합니다.");
                var aggregate = new AggregateStatistics();
                int cohortGameCount = 0;
                for (int left = 0; left < teams.Count; left++)
                for (int right = left + 1; right < teams.Count; right++)
                {
                    VerifyDeterminism(balance, teams[left].Rotations[0], teams[right].Rotations[0]);
                    determinismChecks++;
                    for (int sample = 0; sample < gamesPerPair; sample++)
                    {
                        int combination = sample / 2 % 25;
                        MatchRosterSnapshot leftRoster = teams[left].Rotations[combination % 5];
                        MatchRosterSnapshot rightRoster = teams[right].Rotations[combination / 5];
                        bool leftIsHome = sample % 2 == 0;
                        ulong seed = DeterministicSeed.Derive(0x5712E697UL, (ulong)++gameId);
                        var input = new MatchInput(1, gameId, seed,
                            leftIsHome ? rightRoster : leftRoster, leftIsHome ? leftRoster : rightRoster,
                            MatchRules.CreateDefault(false));
                        MatchResult result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                            .Simulate(input, NullMatchEventSink.Instance);
                        int leftRuns = leftIsHome ? result.HomeBoxScore.Runs : result.AwayBoxScore.Runs;
                        int rightRuns = leftIsHome ? result.AwayBoxScore.Runs : result.HomeBoxScore.Runs;
                        teams[left].Add(leftRuns, rightRuns);
                        teams[right].Add(rightRuns, leftRuns);
                        aggregate.Add(result);
                        checksum = unchecked(checksum * 31 + leftRuns * 101 + rightRuns);
                        cohortGameCount++;
                    }
                }
                if (teams.Sum(t => t.Wins) != teams.Sum(t => t.Losses)
                    || teams.Sum(t => t.RunsFor) != teams.Sum(t => t.RunsAgainst)
                    || teams.Any(t => t.Games != gamesPerPair * (teams.Count - 1)))
                    throw new InvalidOperationException("대진 수 또는 승패·득실점 합계가 일치하지 않습니다.");
                reports.Add(new { label, games = cohortGameCount, teams = teams.Select(t => t.Report()).ToArray() });
                Console.WriteLine($"Cohort={label} Games={cohortGameCount}");
                Console.WriteLine(aggregate.Format(cohortGameCount));
            }
            var report = new
            {
                archive = Path.GetFullPath(args[1]),
                manifestSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(args[1], "manifest.json")))),
                configSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))),
                seedBase = "0x5712E697", gamesPerPair, totalGames = gameId, determinismChecks, checksum,
                conditions = "DetailedMatchEngine; 매 경기 피로 초기화; 우투우타; 중립 전술; 팀컬러 없음; 선발 조합 25개와 홈/원정 균등; 배치 위치를 원포지션으로 간주; 역사 포지션 보정 없음",
                cohorts = reports
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[4])));
            File.WriteAllText(args[4], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"TotalGames={gameId} DeterminismChecks={determinismChecks} Checksum={checksum}");
            return 0;
        }

        private static Dictionary<string, StrengthTeam> ReadStrengthTeams(string archive, int year)
        {
            string path = Path.Combine(archive, "Years", year + ".json");
            // 연도를 넘나드는 대결에서도 선수·구단 ID가 겹치지 않아야 한다.
            List<MatchRosterSnapshot[]> rotations = ReadHistoricalTeams(path, year * 100);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            var result = new Dictionary<string, StrengthTeam>(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement team in document.RootElement.GetProperty("teamSeasons").EnumerateArray())
            {
                string key = team.GetProperty("teamSeasonKey").GetString();
                result.Add(key, new StrengthTeam { Key = key, Rotations = rotations[index++] });
            }
            return result;
        }

        private sealed class StrengthTeam
        {
            public string Key;
            public string Name;
            public MatchRosterSnapshot[] Rotations;
            public int Wins;
            public int Losses;
            public int Draws;
            public long RunsFor;
            public long RunsAgainst;
            public int Games => Wins + Losses + Draws;

            public void Add(int scored, int allowed)
            {
                if (scored > allowed) Wins++;
                else if (scored < allowed) Losses++;
                else Draws++;
                RunsFor += scored;
                RunsAgainst += allowed;
            }

            public object Report()
            {
                double rate = Wins + Losses > 0 ? Wins / (double)(Wins + Losses) : 0;
                double standardError = Wins + Losses > 0 ? Math.Sqrt(rate * (1 - rate) / (Wins + Losses)) : 0;
                return new { key = Key, name = Name, games = Games, wins = Wins, losses = Losses, draws = Draws,
                    winRate = rate, approximate95HalfWidth = 1.96 * standardError,
                    runsFor = RunsFor / (double)Games, runsAgainst = RunsAgainst / (double)Games };
            }
        }
    }
}
