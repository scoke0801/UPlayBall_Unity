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
    public static int Run(string directory)
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
        }
        return 0;
    }
}
