using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Game.Guide;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;

namespace Baseball.Tools.ManagerReportValidation;

internal static partial class Program
{
    private static void RunIntegrationChecks()
    {
        var fixture = RuntimeFixture.Fixture.Create(WorldRecordMode.SimulatedHistory);
        var adapter = fixture.CreateAdapter();
        var initial = adapter.CreateSaveData(fixture.State);
        var balance = BalanceTable.CreateDefault();
        Run("실제 훈련 명령의 성장·비용·보고가 같은 저장 후보에 반영", () =>
        {
            var source = adapter.Restore(initial);
            var candidate = adapter.CreateSimulationCopy(source);
            var coordinator = new ManagerModeCoordinator(balance);
            var card = candidate.OwnedCards[1];
            var result = coordinator.TrainOwnedCard(candidate, card.CardId,
                new CardTrainingProgramDefinition("validation", PlayerAbility.Contact, 1, 2));
            Require(result.GainedPoints > 0);
            Equal(result.GainedPoints, News(candidate.GuideProgress, ManagerNewsKind.Training).Single().news.growth[(int)result.Ability]);
            Equal(0, News(source.GuideProgress, ManagerNewsKind.Training).Count());
            Require(source.Economy.DevelopmentPoints > candidate.Economy.DevelopmentPoints);
            var restored = adapter.Restore(adapter.CreateSaveData(candidate));
            Equal(result.GainedPoints, News(restored.GuideProgress, ManagerNewsKind.Training).Single().news.growth[(int)result.Ability]);
        });
        Run("실제 경기 12회 쌍대 실행에서 소식 정책·저장 복원이 이벤트·점수에 영향 없음", () =>
        {
            var left = adapter.Restore(initial);
            var right = adapter.Restore(initial);
            string studyCard = left.Rosters[1].Entries[0].CardId;
            var studyProgram = balance.OwnerCardGrowth.StudyPrograms.First(x => x.PlayerType == PlayerType.Batter);
            foreach (var runtime in new[] { left, right })
            {
                runtime.AcquireCard(studyCard);
                runtime.PlayerGrowth.AddStudy(new CardStudyProjectState(studyCard, studyProgram.ProgramId,
                    runtime.ManagerMode.LiveSeason.SeasonNumber, 1));
            }
            left.GuideProgress.ConfigureReportPolicy(new ManagerReportPolicy {
                homeRunMilestones = new[] { 1 }, strikeoutMilestones = new[] { 1 } });
            right.GuideProgress.ConfigureReportPolicy(new ManagerReportPolicy {
                homeRunMilestones = Array.Empty<int>(), strikeoutMilestones = Array.Empty<int>(), maximumHomeNewsPerWeek = 1 });
            var leftService = new ManagerModeMatchService(fixture.Provider, balance);
            var rightService = new ManagerModeMatchService(fixture.Provider, balance);
            int ownRuns = 0, againstRuns = 0;
            for (int i = 1; i <= 12; i++)
            {
                var leftEvents = new MatchEventBuffer(); var rightEvents = new MatchEventBuffer();
                var a = leftService.PlayNextGame(left, leftEvents, MatchExecutionProfile.DetailedInteractive);
                var b = rightService.PlayNextGame(right, rightEvents, MatchExecutionProfile.DetailedInteractive);
                Equal(a.Match.HomeBoxScore.Runs, b.Match.HomeBoxScore.Runs); Equal(a.Match.AwayBoxScore.Runs, b.Match.AwayBoxScore.Runs);
                Equal(leftEvents.Count, rightEvents.Count); Require(leftEvents.Count > 0);
                for (int e = 0; e < leftEvents.Count; e++) Equal(leftEvents[e], rightEvents[e]);
                if (i > 6)
                {
                    bool home = a.Match.HomeBoxScore.TeamId == left.ManagerMode.LiveSeason.PlayerTeamId;
                    ownRuns += home ? a.Match.HomeBoxScore.Runs : a.Match.AwayBoxScore.Runs;
                    againstRuns += home ? a.Match.AwayBoxScore.Runs : a.Match.HomeBoxScore.Runs;
                }
                if (i == 6)
                {
                    new ManagerModeCoordinator(balance).AdvanceWeek(left);
                    new ManagerModeCoordinator(balance).AdvanceWeek(right);
                    right = adapter.Restore(adapter.CreateSaveData(right));
                    right.GuideProgress.ConfigureReportPolicy(new ManagerReportPolicy {
                        homeRunMilestones = Array.Empty<int>(), strikeoutMilestones = Array.Empty<int>(), maximumHomeNewsPerWeek = 1 });
                }
            }
            var review = News(left.GuideProgress, ManagerNewsKind.WeeklyReview).Single(x => x.createdWeek == 1).news;
            Equal(6, review.games); Equal(ownRuns, review.runsFor); Equal(againstRuns, review.runsAgainst);
            Require(News(left.GuideProgress, ManagerNewsKind.StrikeoutMilestone).Any());
            Require(!News(right.GuideProgress, ManagerNewsKind.StrikeoutMilestone).Any());
            var study = News(right.GuideProgress, ManagerNewsKind.Study).Single().news;
            Require(study.growth.Sum() > 0); Equal(studyProgram.DisplayName, study.label);
            Equal(0, right.PlayerGrowth.StudyProjects.Count);
            Equal(JsonSerializer.Serialize(review, JsonOptions), JsonSerializer.Serialize(
                News(right.GuideProgress, ManagerNewsKind.WeeklyReview).Single(x => x.createdWeek == 1).news, JsonOptions));
        });
    }
}
