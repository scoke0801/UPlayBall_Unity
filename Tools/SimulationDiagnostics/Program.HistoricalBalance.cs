using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        private static int RunHistoricalBalance(string[] args)
        {
            if (args.Length < 3) throw new ArgumentException("historical-balance <Archive폴더> <경기수>");
            int count = int.Parse(args[2]);
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            string[] paths = Directory.GetFiles(Path.Combine(args[1], "Years"), "*.json");
            Array.Sort(paths, StringComparer.Ordinal);
            if (paths.Length == 0) throw new InvalidOperationException("Archive 연도 파일이 없습니다.");
            var years = new List<List<MatchRosterSnapshot[]>>();
            foreach (string path in paths) years.Add(ReadHistoricalTeams(path));
            var aggregate = new AggregateStatistics();
            var balance = BalanceTable.CreateDefault();
            long checksum = 0;
            for (int index = 0; index < count; index++)
            {
                List<MatchRosterSnapshot[]> teams = years[index % years.Count];
                int round = index / years.Count;
                int awayIndex = round % teams.Count;
                int opponentOffset = 1 + round / teams.Count % (teams.Count - 1);
                int homeIndex = (awayIndex + opponentOffset) % teams.Count;
                MatchRosterSnapshot away = teams[awayIndex][round % 5];
                MatchRosterSnapshot home = teams[homeIndex][(round / 5) % 5];
                ulong seed = DeterministicSeed.Derive(0xC051A817UL, (ulong)index);
                var input = new MatchInput(1, index + 1, seed, away, home, MatchRules.CreateDefault(false));
                MatchResult result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                    .Simulate(input, NullMatchEventSink.Instance);
                aggregate.Add(result);
                checksum = unchecked(checksum * 31 + result.AwayBoxScore.Runs * 101 + result.HomeBoxScore.Runs);
                if (index < years.Count) VerifyDeterminism(balance, away, home);
            }
            Console.WriteLine("DetailedMatchEngine: 전체 연도 균등 표집, 매 경기 피로 초기화, 우투우타·중립 전술·팀컬러 없음.");
            Console.WriteLine($"Archive={Path.GetFullPath(args[1])}");
            Console.WriteLine($"ManifestSHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(args[1], "manifest.json"))))}");
            Console.WriteLine($"Years={years.Count} DeterminismChecks={Math.Min(count, years.Count)} ScoreChecksum={checksum}");
            Console.WriteLine(aggregate.Format(count));
            // 실측 분포를 출력한다. 균일 50 능력치용 합격선을 역사 로스터에 재사용하지 않는다.
            return 0;
        }

        private static List<MatchRosterSnapshot[]> ReadHistoricalTeams(string path)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            var seasons = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (JsonElement season in document.RootElement.GetProperty("playerSeasons").EnumerateArray())
                seasons.Add(season.GetProperty("playerSeasonId").GetString(), season);
            var result = new List<MatchRosterSnapshot[]>();
            int teamId = 0;
            foreach (JsonElement team in document.RootElement.GetProperty("teamSeasons").EnumerateArray())
            {
                teamId++;
                var lineup = new List<LineupSlot>();
                var starters = new List<PitcherRosterEntry>();
                var bullpen = new List<PitcherRosterEntry>();
                var bench = new List<Player>();
                int playerId = teamId * 100;
                foreach (JsonElement card in team.GetProperty("core25CardIds").EnumerateArray())
                {
                    string cardId = card.GetString();
                    JsonElement season = seasons[cardId.Substring(0, cardId.LastIndexOf(':'))];
                    string role = season.GetProperty("rosterRole").GetString();
                    PlayerPosition position = HistoricalPosition(season.GetProperty("position").GetString());
                    if (role.StartsWith("StartingHitter:", StringComparison.Ordinal))
                        position = HistoricalPosition(role.Substring("StartingHitter:".Length));
                    Player player = ReadHistoricalPlayer(season, ++playerId, position);
                    if (role.StartsWith("StartingHitter:", StringComparison.Ordinal)) lineup.Add(new LineupSlot(player, position));
                    else if (season.GetProperty("playerType").GetString() == "Hitter") bench.Add(player);
                    else if (role.StartsWith("StartingPitcher:", StringComparison.Ordinal)) starters.Add(new PitcherRosterEntry(player, PitcherRole.Starter));
                    else
                    {
                        PitcherRole pitcherRole = role.StartsWith("Closer", StringComparison.Ordinal) ? PitcherRole.Closer :
                            role.StartsWith("Setup", StringComparison.Ordinal) ? PitcherRole.Setup : PitcherRole.MiddleRelief;
                        bullpen.Add(new PitcherRosterEntry(player, pitcherRole));
                    }
                }
                if (lineup.Count != 9 || starters.Count != 5 || bullpen.Count != 6 || bench.Count != 5)
                    throw new InvalidOperationException($"Core25 역할 수 불일치: {path}, {teamId}, {lineup.Count}/{starters.Count}/{bullpen.Count}/{bench.Count}");
                var rotations = new MatchRosterSnapshot[5];
                for (int index = 0; index < rotations.Length; index++)
                    rotations[index] = new MatchRosterSnapshot(teamId, "검증 구단", new Lineup(lineup.ToArray()),
                        starters[index], bullpen, bench, ManagerTacticalProfile.Balanced, RunningApproach.Balanced);
                result.Add(rotations);
            }
            if (result.Count < 2) throw new InvalidOperationException("경기에는 두 구단 이상이 필요합니다.");
            return result;
        }

        private static Player ReadHistoricalPlayer(JsonElement season, int playerId, PlayerPosition position)
        {
            JsonElement a = season.GetProperty("baseAttributes");
            var repertoire = new List<PitchRepertoireEntry>();
            foreach (JsonElement pitch in season.GetProperty("pitchRepertoire").EnumerateArray())
                repertoire.Add(new PitchRepertoireEntry(Enum.Parse<PitchType>(pitch.GetProperty("pitchType").GetString()),
                    pitch.GetProperty("baseMastery").GetInt32(), pitch.GetProperty("isPrimary").GetBoolean(),
                    pitch.GetProperty("developmentAffinity").GetDouble(), pitch.GetProperty("usagePreference").GetDouble(),
                    pitch.GetProperty("velocityOffset").GetDouble()));
            return new Player(playerId, "검증 선수", position, Handedness.Right, Handedness.Right,
                new BatterAttributes(a[0].GetInt32(), a[1].GetInt32(), a[2].GetInt32(), a[3].GetInt32(), a[4].GetInt32(), a[5].GetInt32()),
                new PitcherAttributes(a[6].GetInt32(), a[7].GetInt32(), a[8].GetInt32(), a[9].GetInt32(), a[10].GetInt32(), a[11].GetInt32()),
                pitchRepertoire: repertoire);
        }

        private static PlayerPosition HistoricalPosition(string position) => position switch
        {
            "C" => PlayerPosition.Catcher, "1B" => PlayerPosition.FirstBase, "2B" => PlayerPosition.SecondBase,
            "3B" => PlayerPosition.ThirdBase, "SS" => PlayerPosition.Shortstop, "LF" => PlayerPosition.LeftField,
            "CF" => PlayerPosition.CenterField, "RF" => PlayerPosition.RightField, "DH" => PlayerPosition.DesignatedHitter,
            "P" => PlayerPosition.StartingPitcher,
            _ => throw new ArgumentException($"알 수 없는 포지션: {position}")
        };
    }
}
