using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>연습경기 순위가 어떤 전력에서 왔는지 확인하려고 확정 편성을 그대로 내보낸다.</summary>
internal static class LegendaryPracticeRosterDump
{
    public static int Run(string[] args)
    {
        string root = Path.GetFullPath(args[1]);
        string catalogPath = Path.GetFullPath(args[2]);
        string output = Path.GetFullPath(args[3]);
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
        var balance = CommonMatchBalanceInput.Load(CommonMatchBalanceInput.DefaultPath, CommonMatchBalanceInput.RatingCurvePath);
        var catalog = JsonSerializer.Deserialize<LegendaryPracticeCatalog>(File.ReadAllText(catalogPath), json);
        var identities = new WorldIdentityGenerator().Generate(content.PlayerPersons, content.TeamSeasons, content.IdentityNameCatalog, catalog.seed);
        var builder = new LegendaryPracticeRosterBuilder(content, balance);

        var teams = new List<object>();
        foreach (var entry in catalog.teams)
        {
            content.TryGetTeamSeason(entry.teamSeasonKey, out var definition);
            var cards = builder.SelectCards(definition);
            var snapshot = builder.Build(definition, identities, entry.rank, entry.rank * 1000, out var colors)[0];
            var slots = new List<object>();
            for (int i = 0; i < 25; i++)
            {
                content.TryGetPlayerSeason(cards[i].PlayerSeasonId, out var season);
                slots.Add(new
                {
                    slot = i,
                    kind = i < 9 ? "Lineup" : i < 14 ? "Bench" : i < 19 ? "Rotation" : "Bullpen",
                    edition = cards[i].Edition.ToString(),
                    originYear = season.OriginYear,
                    originFranchise = season.OriginFranchiseId,
                    isImported = season.OriginTeamSeasonKey != definition.TeamSeasonKey,
                    position = season.Position.ToString(),
                    pitcherRole = season.PitcherRole.ToString(),
                    cost = season.Cost,
                    raw = RawAbilities(season, cards[i])
                });
            }
            var lineup = snapshot.StartingLineup;
            var batters = new List<Player>();
            for (int i = 0; i < lineup.Count; i++) batters.Add(lineup[i].Player);
            var rotation = new List<Player> { snapshot.StartingPitcher.Player };
            var relief = snapshot.Bullpen.Select(p => p.Player).ToList();
            teams.Add(new
            {
                entry.rank, entry.year, entry.wins, entry.losses, entry.draws, entry.runsScored, entry.runsAllowed,
                winRate = entry.WinRate,
                franchise = identities.GetFranchiseDisplayName(definition.FranchiseId),
                teamColors = colors.Select(c => c.TeamColorId).ToArray(),
                lineupCurved = new
                {
                    contact = batters.Average(p => p.BatterAttributes.Contact),
                    power = batters.Average(p => p.BatterAttributes.Power),
                    speed = batters.Average(p => p.BatterAttributes.Speed),
                    defense = batters.Average(p => p.BatterAttributes.Defense),
                    mental = batters.Average(p => p.BatterAttributes.Mental)
                },
                starterCurved = Pitching(rotation),
                bullpenCurved = Pitching(relief),
                slots
            });
        }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(teams, json));
        Console.WriteLine($"연습경기 편성 {teams.Count}팀을 내보냈습니다: {output}");
        return 0;
    }

    private static object Pitching(List<Player> pitchers) => new
    {
        stamina = pitchers.Average(p => p.PitcherAttributes.Stamina),
        velocity = pitchers.Average(p => p.PitcherAttributes.Velocity),
        stuff = pitchers.Average(p => p.PitcherAttributes.Stuff),
        breaking = pitchers.Average(p => p.PitcherAttributes.Breaking),
        control = pitchers.Average(p => p.PitcherAttributes.Control),
        mental = pitchers.Average(p => p.PitcherAttributes.Mental)
    };

    private static Dictionary<string, int> RawAbilities(PlayerSeasonDefinition season, PlayerCardDefinition card)
    {
        var source = season.CreateBaseAttributes();
        var result = new Dictionary<string, int>();
        for (int i = 0; i < PlayerAbilityCatalog.AbilityCount; i++)
        {
            var ability = (PlayerAbility)i;
            result[ability.ToString()] = source.Get(ability) + card.GetModifier(ability);
        }
        return result;
    }
}
