using System.Text.Json;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>같은 편성끼리의 자체 대결에서 능력치 하나만 올려 경기 결과의 한계 민감도를 측정한다.</summary>
internal static class AbilitySensitivity
{
    private const int Delta = 8;

    public static int Run(string[] args)
    {
        string root = Path.GetFullPath(args[1]);
        string catalogPath = Path.GetFullPath(args[2]);
        string output = Path.GetFullPath(args[3]);
        int games = int.Parse(args[4]);
        if (games < 2) throw new ArgumentOutOfRangeException(nameof(games));
        bool measureStarterOnly = args.Length == 6 && args[5] == "StarterStamina";
        var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        HistoricalRuntimeContentFile Read(string path) => new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        var source = new HistoricalRuntimeContentCatalog();
        source.Configure(new TextAsset(manifest.RootElement.GetRawText()), Read("player_persons.json"),
            manifest.RootElement.GetProperty("years").EnumerateArray().Select(y => new HistoricalRuntimeYearContentFile(
                y.GetProperty("year").GetInt32(), Read(y.GetProperty("path").GetString()))).ToArray());
        string specialPath = Path.Combine(root, "BakedSpecialCards.json");
        if (File.Exists(specialPath)) source.ConfigureSpecialCards(new TextAsset(File.ReadAllText(specialPath)),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(specialPath))));
        var content = new UnityHistoricalContentProvider(source, HistoricalContentVerificationMode.Full).Load();
        var balance = CommonMatchBalanceInput.Load(args.Length == 6 && !measureStarterOnly ? args[5] :
            CommonMatchBalanceInput.DefaultPath, CommonMatchBalanceInput.RatingCurvePath);
        var catalog = JsonSerializer.Deserialize<LegendaryPracticeCatalog>(File.ReadAllText(catalogPath), json);
        var identities = new WorldIdentityGenerator().Generate(content.PlayerPersons, content.TeamSeasons, content.IdentityNameCatalog, catalog.seed);
        var builder = new LegendaryPracticeRosterBuilder(content, balance);

        // 같은 팀의 홈·원정 교대 대결로 기준선의 기대 승률을 5할로 두되 유한 표본 오차는 남는다.
        content.TryGetTeamSeason(catalog.teams[49].teamSeasonKey, out var definition);
        var baseline = builder.Build(definition, identities, 1, 100000, out _);
        var opponent = builder.Build(definition, identities, 2, 200000, out _);

        var baselineScores = new (int Scored, int Allowed)[games];
        var results = new List<object> { Measure(balance, "Baseline", baseline, opponent, games, baselineScores, true) };
        if (!measureStarterOnly)
        {
            foreach (var ability in new[] { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed,
                PlayerAbility.Bunt, PlayerAbility.Defense, PlayerAbility.BatterMental })
                results.Add(Measure(balance, ability.ToString(), Adjust(baseline, ability), opponent, games, baselineScores));
            foreach (var ability in new[] { PlayerAbility.Stamina, PlayerAbility.Velocity, PlayerAbility.Stuff,
                PlayerAbility.Breaking, PlayerAbility.Control, PlayerAbility.PitcherMental })
                results.Add(Measure(balance, ability.ToString(), Adjust(baseline, ability), opponent, games, baselineScores));
        }
        results.Add(Measure(balance, "StarterStamina", Adjust(baseline, PlayerAbility.Stamina, true), opponent, games, baselineScores));

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(results, json));
        Console.WriteLine("능력치 +" + Delta + " 민감도 " + results.Count + "종 x " + games + "경기를 기록했습니다: " + output);
        return 0;
    }

    private static object Measure(Baseball.Core.Balance.BalanceTable balance, string label,
        MatchRosterSnapshot[] test, MatchRosterSnapshot[] opponent, int games,
        (int Scored, int Allowed)[] baselineScores, bool isBaseline = false)
    {
        int wins = 0, losses = 0, draws = 0, scored = 0, allowed = 0;
        long walksAllowed = 0, strikeouts = 0, starterPitches = 0, starterOuts = 0, sacrificeBunts = 0;
        int changedScoreGames = 0;
        double pairedSum = 0d, pairedSquares = 0d;
        for (int game = 1; game <= games; game++)
        {
            bool testIsHome = game % 2 == 0;
            int rotation = game % 5;
            ulong seed = DeterministicSeed.Derive(77770000, (ulong)game);
            var away = testIsHome ? opponent[rotation] : test[rotation];
            var home = testIsHome ? test[rotation] : opponent[rotation];
            var input = new MatchInput(1, game, seed, away, home,
                new MatchRules(9, 0, ExtraInningPolicy.DrawAtLimit, 10, true, 0),
                historicalConfiguration: new HistoricalMatchConfiguration(balance.HistoricalAssignment.CreateRule()));
            var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input,
                NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
            int testRuns = testIsHome ? result.HomeBoxScore.Runs : result.AwayBoxScore.Runs;
            int otherRuns = testIsHome ? result.AwayBoxScore.Runs : result.HomeBoxScore.Runs;
            scored += testRuns; allowed += otherRuns;
            var box = testIsHome ? result.HomeBoxScore : result.AwayBoxScore;
            foreach (var line in box.PitchingLines) { walksAllowed += line.WalksAllowed; strikeouts += line.Strikeouts; }
            starterPitches += box.PitchingLine.PitchesThrown;
            starterOuts += box.PitchingLine.OutsRecorded;
            foreach (var line in box.BattingLines) sacrificeBunts += line.SacrificeBunts;
            if (isBaseline) baselineScores[game - 1] = (testRuns, otherRuns);
            var original = baselineScores[game - 1];
            if (original.Scored != testRuns || original.Allowed != otherRuns) changedScoreGames++;
            double pairedDifference = testRuns - otherRuns - (original.Scored - original.Allowed);
            pairedSum += pairedDifference;
            pairedSquares += pairedDifference * pairedDifference;
            if (testRuns > otherRuns) wins++; else if (otherRuns > testRuns) losses++; else draws++;
        }
        var row = new { ability = label, wins, losses, draws,
            winRate = wins + losses == 0 ? 0d : (double)wins / (wins + losses),
            runsScored = (double)scored / games, runsAllowed = (double)allowed / games,
            walksAllowedPerGame = (double)walksAllowed / games, strikeoutsPerGame = (double)strikeouts / games,
            starterPitchesPerGame = (double)starterPitches / games, starterInningsPerGame = starterOuts / (3d * games),
            sacrificeBunts, changedScoreGames, pairedRunDifference = pairedSum / games,
            pairedRunDifference95HalfWidth = 1.96d * Math.Sqrt(Math.Max(0d,
                (pairedSquares - pairedSum * pairedSum / games) / (games - 1) / games)) };
        Console.WriteLine(label.PadRight(16) + " 승률 " + row.winRate.ToString("F3") +
            "  RS " + row.runsScored.ToString("F3") + "  RA " + row.runsAllowed.ToString("F3"));
        return row;
    }

    private static MatchRosterSnapshot[] Adjust(MatchRosterSnapshot[] snapshots, PlayerAbility ability, bool startersOnly = false)
    {
        var result = new MatchRosterSnapshot[snapshots.Length];
        for (int index = 0; index < snapshots.Length; index++)
        {
            var s = snapshots[index];
            var slots = new LineupSlot[s.StartingLineup.Count];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LineupSlot(startersOnly ? s.StartingLineup[i].Player : Adjust(s.StartingLineup[i].Player, ability),
                    s.StartingLineup[i].FieldingPosition);
            var bullpen = new PitcherRosterEntry[s.Bullpen.Count];
            for (int i = 0; i < bullpen.Length; i++)
                bullpen[i] = CopyPitcher(s.Bullpen[i], startersOnly ? s.Bullpen[i].Player : Adjust(s.Bullpen[i].Player, ability));
            var bench = new Player[s.Bench.Count];
            for (int i = 0; i < bench.Length; i++) bench[i] = startersOnly ? s.Bench[i] : Adjust(s.Bench[i], ability);
            result[index] = new MatchRosterSnapshot(s.TeamId, s.TeamName, new Lineup(slots),
                CopyPitcher(s.StartingPitcher, Adjust(s.StartingPitcher.Player, ability)),
                bullpen, bench, s.ManagerProfile, s.RunningApproach, s.PlayerCharacterId, s.PlayerConditions, s.BatteryConditions);
        }
        return result;
    }

    private static PitcherRosterEntry CopyPitcher(PitcherRosterEntry entry, Player player) =>
        new(player, entry.Role, entry.Condition, entry.RecentWorkload, entry.PitchLimit, entry.NaturalRole,
            entry.ActiveRosterRole, entry.PlayerSeasonId, entry.NaturalRoleConfidence,
            entry.CapacityMultiplier, entry.RecoveryMultiplier);

    private static Player Adjust(Player player, PlayerAbility ability)
    {
        var b = player.BatterAttributes;
        int Bump(int value, PlayerAbility target) => target == ability ? value + Delta : value;
        // 경기 입력 축의 국소 실험이다. 원시 카드 +8의 성장 곡선 실험과 구별한다.
        // Baked는 성장 차이의 고정 기준이므로 함께 올리면 구종 성장분을 상쇄한다.
        PitcherAttributes BumpPitcher(PitcherAttributes p) => new PitcherAttributes(
            Bump(p.Stamina, PlayerAbility.Stamina), Bump(p.Velocity, PlayerAbility.Velocity),
            Bump(p.Stuff, PlayerAbility.Stuff), Bump(p.Breaking, PlayerAbility.Breaking),
            Bump(p.Control, PlayerAbility.Control), Bump(p.Mental, PlayerAbility.PitcherMental));
        var u = player.UncurvedPitcherAttributes;
        double BumpRaw(double value, PlayerAbility target) => target == ability ? value + Delta : value;
        return new Player(player.PlayerId, player.Name, player.PrimaryPosition, player.BattingHand, player.ThrowingHand,
            new BatterAttributes(Bump(b.Contact, PlayerAbility.Contact), Bump(b.Power, PlayerAbility.Power),
                Bump(b.Speed, PlayerAbility.Speed), Bump(b.Bunt, PlayerAbility.Bunt),
                Bump(b.Defense, PlayerAbility.Defense), Bump(b.Mental, PlayerAbility.BatterMental)),
            BumpPitcher(player.PitcherAttributes),
            secondaryPositions: player.SecondaryPositions, nationality: player.Nationality,
            pitchRepertoire: player.PitchRepertoire, traitIds: player.TraitIds,
            bakedPitcherAttributes: player.BakedPitcherAttributes,
            permanentPitcherAttributes: BumpPitcher(player.PermanentPitcherAttributes),
            hasResolvedMatchRatings: player.HasResolvedMatchRatings,
            uncurvedPitcherAttributes: new PitcherRatingValues(
                BumpRaw(u.Stamina, PlayerAbility.Stamina), BumpRaw(u.Velocity, PlayerAbility.Velocity),
                BumpRaw(u.Stuff, PlayerAbility.Stuff), BumpRaw(u.Breaking, PlayerAbility.Breaking),
                BumpRaw(u.Control, PlayerAbility.Control), BumpRaw(u.Mental, PlayerAbility.PitcherMental)),
            isPositionEvidenceMissing: player.IsPositionEvidenceMissing);
    }
}
