using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Rules;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>실제 역사 100팀의 성장 상태와 상세 엔진 전력 상승을 대조한다.</summary>
internal static class PracticeDevelopmentValidation
{
    public static int Run(string[] args)
    {
        string root = Path.GetFullPath(args[1]);
        var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        HistoricalRuntimeContentFile Read(string path) => new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        var source = new HistoricalRuntimeContentCatalog();
        source.Configure(new TextAsset(manifest.RootElement.GetRawText()), Read("player_persons.json"),
            manifest.RootElement.GetProperty("years").EnumerateArray().Select(y => new HistoricalRuntimeYearContentFile(
                y.GetProperty("year").GetInt32(), Read(y.GetProperty("path").GetString()))).ToArray());
        string specialPath = Path.Combine(root, "BakedSpecialCards.json");
        source.ConfigureSpecialCards(new TextAsset(File.ReadAllText(specialPath)),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(specialPath))));
        var content = new UnityHistoricalContentProvider(source, HistoricalContentVerificationMode.Full).Load();
        T Load<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText("Assets/10.Datas/Resources/NewGame/" + name + ".json"), json)!;
        var common = CommonMatchBalanceInput.Load();
        var owner = Load<OwnerDevelopmentBalance>("OwnerDevelopment");
        var balance = new BalanceTable(common.Version, common.PlateDiscipline, common.BattedBall,
            common.BaseRunning, common.ContractOffer, common.TeamGeneration, common.PlayerEvaluation, common.CareerSeason,
            growth: OwnerSkillContent.Compose(common.Growth, owner.skillSetBonus),
            miniGame: common.MiniGame, match: common.Match, matchRatingCurve: common.MatchRatingCurve,
            ownerCardGrowth: owner.ApplyStudyTiers(common.OwnerCardGrowth)) { TraitTraining = Load<OwnerTraitTrainingBalance>("OwnerTraitTraining") };
        var development = Load<LegendaryPracticeDevelopmentBalance>("LegendaryPracticeDevelopment");
        var catalog = Load<LegendaryPracticeCatalog>("LegendaryPracticeCatalog");
        catalog.ValidateCoverage(content.TeamSeasons);
        var identities = new WorldIdentityGenerator().Generate(content.PlayerPersons, content.TeamSeasons, content.IdentityNameCatalog, catalog.seed);
        var builder = new LegendaryPracticeRosterBuilder(content, balance, development);
        var resolver = new OwnerCardAbilityResolver(balance.Growth);
        var groups = new Dictionary<int, Summary>();
        int cardsChecked = 0, replayChecks = 0;
        foreach (var entry in catalog.teams)
        {
            content.TryGetTeamSeason(entry.teamSeasonKey, out var team);
            if (builder.GetRosterHash(team) != entry.rosterHash) throw new Exception("기본 역사 편성 해시가 바뀌었습니다.");
            var tier = development.GetTier(entry.rank);
            foreach (var card in builder.SelectCards(team))
            {
                content.TryGetPlayerSeason(card.PlayerSeasonId, out var season);
                var owned = builder.CreateDevelopment(card, entry.rank);
                var repeated = builder.CreateDevelopment(card, entry.rank);
                if (owned.EnhancementLevel != tier.enhancement || owned.SkillBoard.Placements.Count != tier.blockCount ||
                    owned.LastStudySeason != tier.studyCompletions || owned.Trait.rank != tier.traitRank ||
                    owned.Trait.trainingSeason <= owned.LastStudySeason) throw new Exception("성장 단계가 다릅니다.");
                if (!owned.SkillBoard.Placements.SequenceEqual(repeated.SkillBoard.Placements)) throw new Exception("장착 결정론 실패");
                foreach (var placement in owned.SkillBoard.Placements)
                {
                    var block = balance.Growth.SkillBlocks.Single(b => b.BlockId == placement.Instance.DefinitionId);
                    if (block.Rarity != tier.blockRarity || !SkillBlockCategoryCatalog.IsAvailableTo(block.Category, season.PlayerType))
                        throw new Exception("블록 등급 또는 선수 유형이 다릅니다.");
                }
                for (int a = 0; a < PlayerAbilityCatalog.AbilityCount; a++)
                {
                    var ability = (PlayerAbility)a;
                    var value = resolver.ResolveContribution(season, card, owned, ability);
                    if (value.PermanentTotal != resolver.ResolveRawPermanent(season, card, repeated, ability) ||
                        value.PermanentTotal < resolver.ResolveRawPermanent(season, card, null, ability) ||
                        owned.Training.GetStudyBonus(ability) > Math.Max(0, season.CreateTrainingCeiling().Get(ability) - season.CreateBaseAttributes().Get(ability)))
                        throw new Exception("성장 합산·결정론·유학 상한 실패");
                }
                cardsChecked++;
            }
            var baseline = builder.Build(team, identities, 1, 1000, out _);
            var grown = builder.Build(team, identities, 2, 2000, out _, entry.rank);
            if (!groups.TryGetValue(tier.maximumRank, out var summary)) groups.Add(tier.maximumRank, summary = new Summary());
            for (int game = 0; game < 100; game++)
            {
                bool grownHome = game % 2 == 0;
                ulong seed = DeterministicSeed.Derive(20260913, (ulong)(entry.rank * 1000 + game));
                var input = new MatchInput(1, game + 1, seed, grownHome ? baseline[game % 5] : grown[game % 5],
                    grownHome ? grown[game % 5] : baseline[game % 5],
                    new MatchRules(9, 0, ExtraInningPolicy.DrawAtLimit, 10, true, 0),
                    historicalConfiguration: new HistoricalMatchConfiguration(balance.HistoricalAssignment.CreateRule()));
                var events = new MatchEventBuffer();
                var profile = game == 0 ? new MatchExecutionProfile(SimulationEngineKind.Detailed,
                    MatchDecisionMode.InternalAiOnly, MatchEventMode.Full, MatchDecisionTraceMode.Full,
                    MatchStatisticsMode.FullBoxScore) : MatchExecutionProfile.DetailedBackground;
                var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input, events, profile);
                if (game == 0)
                {
                    var repeatedEvents = new MatchEventBuffer();
                    new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input, repeatedEvents, profile);
                    if (!events.ToArray().SequenceEqual(repeatedEvents.ToArray())) throw new Exception("이벤트 재현 실패");
                    replayChecks++;
                }
                summary.Add(result, grownHome);
            }
            if (entry.rank % 20 == 0) Console.WriteLine($"{entry.rank}/100팀 검증 완료");
        }
        var report = new { cardsChecked, replayChecks, games = groups.Values.Sum(s => s.Games),
            tiers = groups.OrderBy(g => g.Key).Select(g => new { maximumRank = g.Key, results = g.Value.Report() }) };
        File.WriteAllText(args[2], JsonSerializer.Serialize(report, json));
        Console.WriteLine(JsonSerializer.Serialize(report, json));
        return 0;
    }

    private sealed class Summary
    {
        public int Games, Wins, Losses, Draws;
        private long _atBats, _hits, _pa, _walks, _strikeouts, _homeRuns, _runs, _earned, _outs;
        public void Add(MatchResult result, bool grownHome)
        {
            Games++;
            int margin = (result.HomeBoxScore.Runs - result.AwayBoxScore.Runs) * (grownHome ? 1 : -1);
            if (margin > 0) Wins++; else if (margin < 0) Losses++; else Draws++;
            foreach (var box in new[] { result.HomeBoxScore, result.AwayBoxScore })
            {
                _runs += box.Runs;
                foreach (var b in box.BattingLines)
                { _atBats += b.AtBats; _hits += b.Hits; _pa += b.PlateAppearances; _walks += b.Walks; _strikeouts += b.Strikeouts; _homeRuns += b.HomeRuns; }
                foreach (var p in box.PitchingLines) { _earned += p.EarnedRuns; _outs += p.OutsRecorded; }
            }
        }
        public object Report() => new { Games, Wins, Losses, Draws, winRate = (double)Wins / (Wins + Losses),
            avg = (double)_hits / _atBats, era = 27d * _earned / _outs, homeRunsPerGame = (double)_homeRuns / Games,
            runsPerGame = (double)_runs / Games, walkRate = (double)_walks / _pa, strikeoutRate = (double)_strikeouts / _pa };
    }
}
