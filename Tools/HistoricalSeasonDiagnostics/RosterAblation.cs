using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Simulation.Career;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>실제 시즌의 경기 전 상태를 고정하고 타순·수비 적응도·보직 적응도를 따로 제거하는 진단이다.</summary>
internal static class RosterAblation
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };
    private enum Variant { Baseline, TargetOrder, BothOrder, TargetPositionNeutral, TargetRoleNeutral }

    public static int Run(string[] args)
    {
        if (args.Length != 5) throw new ArgumentException("--roster-ablation <Runtime> <출력> <반복> <TeamSeasonKey,Key>");
        string root = Path.GetFullPath(args[1]);
        int repeats = int.Parse(args[3]);
        if (repeats < 1) throw new ArgumentOutOfRangeException(nameof(repeats));
        string[] targets = args[4].Split(',');
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        var catalog = new HistoricalRuntimeContentCatalog();
        HistoricalRuntimeContentFile Entry(string path) => new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        catalog.Configure(new TextAsset(manifest.RootElement.GetRawText()), Entry("player_persons.json"),
            manifest.RootElement.GetProperty("years").EnumerateArray().Select(y =>
                new HistoricalRuntimeYearContentFile(y.GetProperty("year").GetInt32(), Entry(y.GetProperty("path").GetString()))).ToArray());
        var content = new UnityHistoricalContentProvider(catalog, HistoricalContentVerificationMode.Full).Load();
        var balance = BalanceTable.CreateDefault();
        var identities = new WorldIdentityGenerator().Generate(content.PlayerPersons, content.TeamSeasons, content.IdentityNameCatalog, 20260905);
        var source = new BakedHistoricalDetailedSeasonSource(content, balance, identities);
        var rows = new List<object>();
        var snapshots = new List<object>();
        long seasonGames = 0, replayGames = 0, equalityChecks = 0;
        foreach (string target in targets)
        {
            int year = int.Parse(target.Split('_').Last());
            var teams = content.GetYear(year).TeamSeasons;
            if (!teams.Any(t => t.TeamSeasonKey == target)) throw new ArgumentException("대상 구단이 없습니다.");
            for (int run = 0; run < repeats; run++)
            {
                ulong seed = 20260905UL + (ulong)run * 104729UL;
                var season = source.RunSeason(seed, teams);
                seasonGames += source.LastRunMetrics.TotalGameCount;
                var ids = season.Players.ToDictionary(p => p.PlayerId);
                var totals = Enum.GetValues<Variant>().ToDictionary(v => v, _ => new Totals());
                bool snapshotSaved = false;
                foreach (var match in season.Matches)
                {
                    if (match.Stage is HistoricalMatchStage.AllStarGame or HistoricalMatchStage.Postseason) continue;
                    var original = match.Result;
                    MatchInput input = original.Input;
                    bool isAway = ids[input.AwayRoster.StartingPitcher.Player.PlayerId].TeamSeasonKey == target;
                    bool isHome = ids[input.HomeRoster.StartingPitcher.Player.PlayerId].TeamSeasonKey == target;
                    if (!isAway && !isHome) continue;
                    int targetId = isAway ? input.AwayRoster.TeamId : input.HomeRoster.TeamId;
                    if (run == 0 && !snapshotSaved)
                    {
                        var roster = isAway ? input.AwayRoster : input.HomeRoster;
                        snapshots.Add(new { target, lineup = Describe(roster, ids, balance),
                            aiOrder = Describe(Transform(roster, Variant.TargetOrder, balance), ids, balance) });
                        snapshotSaved = true;
                    }
                    foreach (Variant variant in Enum.GetValues<Variant>())
                    {
                        MatchRosterSnapshot away = input.AwayRoster, home = input.HomeRoster;
                        if (isAway || variant == Variant.BothOrder) away = Transform(away, variant, balance);
                        if (isHome || variant == Variant.BothOrder) home = Transform(home, variant, balance);
                        var altered = new MatchInput(input.SeasonId, input.GameId, input.RandomSeed, away, home,
                            input.Rules, input.RulesVersion, input.VersionStamp, input.HistoricalConfiguration);
                        var result = new MatchSimulator(balance, MatchRandomStreams.Create(input.RandomSeed))
                            .Simulate(altered, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
                        if (variant == Variant.Baseline)
                        {
                            if (JsonSerializer.Serialize(original, JsonOptions) != JsonSerializer.Serialize(result, JsonOptions))
                                throw new InvalidOperationException("기준 경기 재생 불일치: 경기 입력을 독립적으로 재생할 수 없습니다.");
                            equalityChecks++;
                        }
                        totals[variant].Add(result, targetId, ids);
                        replayGames++;
                    }
                }
                foreach (var pair in totals) rows.Add(new { year, target, seed, variant = pair.Key.ToString(), totals = pair.Value });
                if ((run + 1) % 8 == 0) Console.WriteLine($"{target}: {run + 1}/{repeats}시즌, 재생 {replayGames:N0}경기");
            }
        }
        string output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { contentHash = content.Manifest.ContentHash,
            balanceHash = balance.ContentHash, repeats, seasonGames, replayGames, equalityChecks,
            frozenPreviousDayWorkload = true, snapshots, rows }, JsonOptions));
        return 0;
    }

    private static object[] Describe(MatchRosterSnapshot roster, Dictionary<int, HistoricalPlayerSeasonIdentity> ids, BalanceTable balance)
        => Enumerable.Range(0, 9).Select(i =>
        {
            var slot = roster.StartingLineup[i];
            var player = slot.Player;
            return (object)new { order = i + 1, playerSeasonId = ids[player.PlayerId].PlayerSeasonId,
                fielding = slot.FieldingPosition.ToString(), natural = player.PrimaryPosition.ToString(),
                proficiency = player.GetPositionProficiency(slot.FieldingPosition), player.IsPositionEvidenceMissing,
                raw = player.BatterAttributes, effective = MatchRatingCurve.ProjectPlayer(player, balance.MatchRatingCurve).BatterAttributes };
        }).ToArray();

    private static MatchRosterSnapshot Transform(MatchRosterSnapshot source, Variant variant, BalanceTable balance)
    {
        if (variant == Variant.Baseline) return source;
        var slots = Enumerable.Range(0, 9).Select(i => source.StartingLineup[i]).ToArray();
        if (variant == Variant.TargetPositionNeutral)
            slots = slots.Select(s => new LineupSlot(WithPosition(s.Player, s.FieldingPosition), s.FieldingPosition)).ToArray();
        Lineup lineup = variant is Variant.TargetOrder or Variant.BothOrder
            ? new ManagerLineupAi(balance.ManagerLineup).BuildLineup(slots) : new Lineup(slots);
        PitcherRosterEntry Neutral(PitcherRosterEntry p) => variant != Variant.TargetRoleNeutral ? p :
            new(p.Player, p.Role, p.Condition, p.RecentWorkload, p.PitchLimit, p.Role,
                p.ActiveRosterRole, p.PlayerSeasonId, p.NaturalRoleConfidence);
        return new MatchRosterSnapshot(source.TeamId, source.TeamName, lineup, Neutral(source.StartingPitcher),
            source.Bullpen.Select(Neutral).ToArray(), source.Bench, source.ManagerProfile, source.RunningApproach,
            source.PlayerCharacterId, source.PlayerConditions, source.BatteryConditions);
    }

    private static Player WithPosition(Player p, PlayerPosition position)
    {
        if (p.PrimaryPosition == position) return p;
        // 주 포지션 자체를 바꾸면 교체 AI의 후보 분류까지 달라지므로 적응도만 추가한다.
        var secondary = p.SecondaryPositions.Where(s => s.Position != position)
            .Append(new PositionProficiency(position, 100)).ToArray();
        return new Player(p.PlayerId, p.Name, p.PrimaryPosition,
            p.BattingHand, p.ThrowingHand, p.BatterAttributes, p.PitcherAttributes,
            secondary, p.Nationality, p.PitchRepertoire, p.TraitIds,
            p.BakedPitcherAttributes, p.PermanentPitcherAttributes, p.HasResolvedMatchRatings,
            p.UncurvedPitcherAttributes, p.IsPositionEvidenceMissing);
    }

    private sealed class Totals
    {
        public int Games, Wins, Losses, Ties, Runs, RunsAllowed, AtBats, Hits, HomeRuns, Walks, Strikeouts, Errors;
        public int PitchingOuts, EarnedRuns, StarterOuts, StarterEarnedRuns, ReliefOuts, ReliefEarnedRuns;
        public Dictionary<string, int[]> Pitchers = new();
        public Dictionary<string, int[]> Hitters = new();

        public void Add(MatchResult result, int teamId, Dictionary<int, HistoricalPlayerSeasonIdentity> ids)
        {
            var own = result.AwayBoxScore.TeamId == teamId ? result.AwayBoxScore : result.HomeBoxScore;
            var other = result.AwayBoxScore.TeamId == teamId ? result.HomeBoxScore : result.AwayBoxScore;
            Games++; if (result.IsTie) Ties++; else if (result.WinnerTeamId == teamId) Wins++; else Losses++;
            Runs += own.Runs; RunsAllowed += other.Runs;
            foreach (var hitter in own.BattingLines)
            {
                AtBats += hitter.AtBats; Hits += hitter.Hits; HomeRuns += hitter.HomeRuns; Walks += hitter.Walks; Strikeouts += hitter.Strikeouts;
                string id = ids[hitter.PlayerId].PlayerSeasonId;
                if (!Hitters.TryGetValue(id, out int[] row)) Hitters[id] = row = new int[4];
                row[0] += hitter.PlateAppearances; row[1] += hitter.AtBats; row[2] += hitter.Hits; row[3] += hitter.HomeRuns;
            }
            foreach (var pitcher in own.PitchingLines)
            {
                PitchingOuts += pitcher.OutsRecorded; EarnedRuns += pitcher.EarnedRuns;
                if (pitcher.IsReliefAppearance) { ReliefOuts += pitcher.OutsRecorded; ReliefEarnedRuns += pitcher.EarnedRuns; }
                else { StarterOuts += pitcher.OutsRecorded; StarterEarnedRuns += pitcher.EarnedRuns; }
                string id = ids[pitcher.PlayerId].PlayerSeasonId;
                if (!Pitchers.TryGetValue(id, out int[] row)) Pitchers[id] = row = new int[5];
                row[0] += pitcher.OutsRecorded; row[1] += pitcher.EarnedRuns; row[2] += pitcher.PitchesThrown;
                row[3]++; if (!pitcher.IsReliefAppearance) row[4]++;
            }
            foreach (var fielding in own.FieldingLines) Errors += fielding.Errors;
        }
    }
}
