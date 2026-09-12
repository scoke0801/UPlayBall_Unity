using System.Security.Cryptography;
using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>게시 전 후보의 실제 Runtime 로더와 특수 카드 전체 참조를 검사한다.</summary>
internal static class ContentValidation
{
    /// <summary>후보 Archive를 두 검증 모드로 읽고 발급 카드와 레시피를 실제로 조회한다.</summary>
    public static int Run(string directory, string practicePath = null)
    {
        string root = Path.GetFullPath(directory);
        string specialPath = Path.Combine(root, "BakedSpecialCards.json");
        byte[] specialBytes = File.ReadAllBytes(specialPath);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        HistoricalRuntimeContentFile ReadEntry(string path) =>
            new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        foreach (var mode in new[] { HistoricalContentVerificationMode.Fast, HistoricalContentVerificationMode.Full })
        {
            var asset = new HistoricalRuntimeContentCatalog();
            asset.Configure(new TextAsset(manifest.RootElement.GetRawText()), ReadEntry("player_persons.json"),
                manifest.RootElement.GetProperty("years").EnumerateArray().Select(year =>
                    new HistoricalRuntimeYearContentFile(year.GetProperty("year").GetInt32(),
                        ReadEntry(year.GetProperty("path").GetString()))).ToArray());
            asset.ConfigureSpecialCards(new TextAsset(System.Text.Encoding.UTF8.GetString(specialBytes)),
                Convert.ToHexString(SHA256.HashData(specialBytes)));
            var content = new UnityHistoricalContentProvider(asset, mode).Load();
            var special = content.SpecialCards;
            if (special == null || special.Cards.Count == 0)
                throw new InvalidOperationException("검증할 특수 카드가 없습니다.");
            var catalog = WorldCardCatalogBuilder.Build(content.PlayerSeasons, null, CardEditionBalanceTable.CreateInitial(),
                content.PlayerPersons, content.TeamSeasons, special);
            var personsBySeason = content.PlayerSeasons.ToDictionary(season => season.PlayerSeasonId, season => season.PlayerPersonId);
            var careerHighPersons = special.Cards.Where(card => card.Edition == PlayerCardEdition.CareerHigh)
                .Select(card => personsBySeason[card.PlayerSeasonId]).ToHashSet();
            if (special.Cards.Where(card => card.Edition == PlayerCardEdition.Legend)
                .Any(card => careerHighPersons.Contains(personsBySeason[card.PlayerSeasonId])))
                throw new InvalidOperationException("동일 선수의 커리어하이와 레전드가 중복 발급되었습니다.");
            foreach (var card in special.Cards)
            {
                var issued = catalog.GetRequiredCard(card.CardId);
                var normal = catalog.GetRequiredCard(card.PlayerSeasonId + ":Normal");
                if (issued.PreferredBattingOrder != normal.PreferredBattingOrder)
                    throw new InvalidOperationException($"일반·특수 카드 타순 불일치: {card.CardId}");
                if (issued.IsUniqueOwnedCard)
                    _ = catalog.SpecialCards.GetRequiredRecipe(card.CardId);
            }
            Console.WriteLine($"{mode}: 특수 카드 {special.Cards.Count}장, 레시피 {special.Recipes.Count}개 참조 검증 통과");
            if (practicePath != null) ValidatePractice(content, practicePath);
        }
        return 0;
    }

    /// <summary>기존 순위 Bake의 모든 상대가 현재 카드로 같은 편성을 재구성하는지 검증한다.</summary>
    private static void ValidatePractice(HistoricalBakedContent content, string path)
    {
        var practice = JsonSerializer.Deserialize<LegendaryPracticeCatalog>(File.ReadAllText(path),
            new JsonSerializerOptions { IncludeFields = true });
        practice.ValidateCoverage(content.TeamSeasons);
        if (practice.contentHash != content.Manifest.ContentHash || practice.simulationVersion !=
            LegendaryPracticeCatalog.CreateSimulationVersion(
                File.ReadAllText(Baseball.Tools.CommonMatchBalanceInput.DefaultPath),
                File.ReadAllText(Baseball.Tools.CommonMatchBalanceInput.RatingCurvePath)))
            throw new InvalidOperationException("연습경기 Bake의 정본 또는 경기 규칙이 변경되었습니다.");
        var balance = Baseball.Tools.CommonMatchBalanceInput.Load(Baseball.Tools.CommonMatchBalanceInput.DefaultPath,
            Baseball.Tools.CommonMatchBalanceInput.RatingCurvePath);
        var builder = new LegendaryPracticeRosterBuilder(content, balance);
        var identities = new WorldIdentityGenerator().Generate(content.PlayerPersons, content.TeamSeasons,
            content.IdentityNameCatalog, practice.seed);
        foreach (var team in content.TeamSeasons)
        {
            var cards = builder.SelectCards(team);
            var repeatedCards = builder.SelectCards(team);
            var snapshots = builder.Build(team, identities, 1, 100, out _);
            for (int slot = 0; slot < cards.Length; slot++)
            {
                if (cards[slot].CardId != repeatedCards[slot].CardId)
                    throw new InvalidOperationException("연습경기 카드 편성 재현 실패: " + team.TeamSeasonKey);
                if (cards[slot].Edition != PlayerCardEdition.CareerHigh &&
                    cards[slot].Edition != PlayerCardEdition.Legend) continue;
                if (slot >= 9 && slot < 14)
                    throw new InvalidOperationException("연습경기 특수 타자가 벤치에 배치되었습니다: " + team.TeamSeasonKey);
                if (slot >= 9) continue;
                foreach (var snapshot in snapshots)
                {
                    bool isStarting = false;
                    for (int order = 0; order < snapshot.StartingLineup.Count; order++)
                        if (snapshot.StartingLineup[order].Player.PlayerId == 100 + slot + 1) isStarting = true;
                    if (!isStarting)
                        throw new InvalidOperationException("연습경기 특수 타자가 실제 선발에서 누락되었습니다: " + team.TeamSeasonKey);
                }
            }
            if (snapshots.Length != 5)
                throw new InvalidOperationException("전체 역사 팀의 선발 로테이션을 구성할 수 없습니다.");
        }
        foreach (var entry in practice.teams)
        {
            if (!content.TryGetTeamSeason(entry.teamSeasonKey, out var team) ||
                builder.GetRosterHash(team) != entry.rosterHash)
                throw new InvalidOperationException("연습경기 Bake 편성이 변경되었습니다: " + entry.challengeTeamId);
            if (builder.Build(team, identities, entry.rank, entry.rank * 100, out _).Length != 5)
                throw new InvalidOperationException("연습경기 선발 로테이션을 구성할 수 없습니다.");
        }
        Console.WriteLine($"연습경기 전체 {content.TeamSeasons.Count}팀 참가·5선발 편성, 상위 {practice.teams.Length}팀 Bake 해시·카드 검증 통과");
    }
}
