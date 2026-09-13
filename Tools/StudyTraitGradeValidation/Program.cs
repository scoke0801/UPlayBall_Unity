using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Simulation.Random;
using Baseball.Tools.ManagerReportValidation;

// NUnit·Unity를 실행하지 않는 실제 콘텐츠·명령·저장 경로 검증이다.
int checks = 0;
var options = new JsonSerializerOptions { IncludeFields = true };
var traits = JsonSerializer.Deserialize<OwnerTraitTrainingBalance>(File.ReadAllText("Assets/10.Datas/Resources/NewGame/OwnerTraitTraining.json"), options);
traits.Validate();
var development = JsonSerializer.Deserialize<OwnerDevelopmentBalance>(File.ReadAllText("Assets/10.Datas/Resources/NewGame/OwnerDevelopment.json"), options);
var studies = development.ApplyStudyTiers(OwnerCardGrowthBalanceTable.CreateDefault());
Check(studies.StudyPrograms.Count == 24, "기존 지도 과정 수 보존");
foreach (CardStudyRank rank in Enum.GetValues<CardStudyRank>().Where(value => value != CardStudyRank.None))
{
    var programs = studies.StudyPrograms.Where(program => program.Rank == rank).ToArray();
    Check(programs.Any(program => program.PlayerType == PlayerType.Batter), rank + " 야수 과정");
    Check(programs.Any(program => program.PlayerType == PlayerType.Pitcher), rank + " 투수 과정");
    foreach (var program in programs)
    {
        var tier = development.studyTiers[(int)rank - 1];
        Check(program.Rewards.Sum(reward => reward.Amount) == tier.growth, "보상 총합");
        Check(program.DurationWeeks == tier.weeks && program.MoneyCost == tier.cost, "기간·비용");
        if (rank >= CardStudyRank.S)
        {
            Check(!program.UnlockRequirement.IsSatisfied((LeagueGrade)((int)tier.requiredLeagueGrade - 1), 99), "조기 해금 금지");
            Check(program.UnlockRequirement.IsSatisfied(tier.requiredLeagueGrade, 0), "도달 리그 해금");
        }
    }
    CheckSprite("StudyRank_" + rank);
}
for (int i = 0; i < traits.experience.Length; i++)
{
    Check((int)traits.GetRank(traits.experience[i] - 1) == i, "승급 직전 경계");
    Check((int)traits.GetRank(traits.experience[i]) == i + 1, "승급 경계");
}
CheckSprite("TraitRank_SS"); CheckSprite("TraitRank_SSS");
foreach (CardTraitRank rank in new[] { CardTraitRank.SS, CardTraitRank.SSS })
{
    var fixture = RuntimeFixture.Fixture.Create(WorldRecordMode.SimulatedHistory);
    var adapter = fixture.CreateAdapter();
    var runtime = adapter.CreateSimulationCopy(fixture.State);
    foreach (var group in runtime.LeagueWorld.Groups)
        foreach (var game in group.Season.Schedule.Games) if (!game.IsCompleted) game.Complete(1, 0);
    typeof(ManagerHistoricalRuntimeState).GetProperty("LeagueWorld").SetValue(runtime, null);
    var card = runtime.OwnedCards.First(item => OwnerTraitTrainingService.Season(runtime, item.CardId).PlayerType == PlayerType.Batter);
    card.Trait.trait = CardTraitKind.Contact;
    card.Trait.rank = (CardTraitRank)((int)rank - 1);
    card.Trait.experience = traits.experience[(int)rank - 1] - 1;
    runtime.PlayerGrowth.Traits.points = 10000;
    string partner = runtime.OwnedCards.First(item => {
        try { OwnerTraitTrainingService.PartnerExperience(runtime, card.CardId, item.CardId, traits); return true; }
        catch (InvalidOperationException) { return false; }
    }).CardId;
    var preview = OwnerTraitTrainingService.Preview(runtime, card.CardId, new[] { partner }, traits);
    long beforeMoney = runtime.Economy.Money;
    OwnerTraitTrainingService.Train(runtime, card.CardId, new[] { partner }, traits,
        runtime.ManagerMode.LiveSeason.SeasonNumber, card.Trait.revision, new Pcg32Random(7));
    Check(card.Trait.rank == rank && card.Trait.experience == preview.TotalExperience, "실제 " + rank + " 승급");
    Check(runtime.Economy.Money == beforeMoney - preview.Money, "미리보기 비용 차감");
    var restored = adapter.CreateSimulationCopy(runtime);
    restored.TryGetOwnedCard(card.CardId, out var copy);
    Check(copy.Trait.rank == rank && copy.Trait.experience == card.Trait.experience, "저장·복원 " + rank);
    if (rank == CardTraitRank.SSS)
    {
        bool blocked = false;
        try { OwnerTraitTrainingService.Preview(runtime, card.CardId, new[] { partner }, traits); }
        catch (InvalidOperationException exception) { blocked = exception.Message.Contains("최고 등급"); }
        Check(blocked, "SSS 재훈련 차단");
    }
}
Console.WriteLine($"Checks={checks}; EditorTests=NotRun");
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
void CheckSprite(string name)
{
    string path = "Assets/10.Datas/Resources/UI/PlayerGrowthBadges/" + name + ".png";
    Check(File.Exists(path) && File.Exists(path + ".meta"), "배지 자산 " + name);
}
