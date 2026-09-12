using System;
using System.Linq;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>실제 구단주 저장·일정·성장 서비스를 연결해 오프시즌 경계를 검증한다.</summary>
    public sealed class OwnerScheduleGateTests
    {
        [Test]
        public void 정규시즌에는운영만허용하고성장변경은자원을소비하지않는다()
        {
            var runtime = CreateRuntime(out _);
            var coordinator = new ManagerModeCoordinator(BalanceTable.CreateDefault());
            long money = runtime.Economy.Money;
            int points = runtime.Economy.DevelopmentPoints;
            Assert.That(OwnerScheduleGateService.GetPhase(runtime), Is.EqualTo(OwnerSeasonPhase.RegularSeason));
            foreach (OwnerGrowthAction action in Enum.GetValues(typeof(OwnerGrowthAction)))
                Assert.That(OwnerScheduleGateService.Evaluate(runtime, action).IsAllowed,
                    Is.EqualTo(action == OwnerGrowthAction.Support || action == OwnerGrowthAction.Staff), action.ToString());
            string cardId = runtime.OwnedCards[0].CardId;
            Assert.Throws<InvalidOperationException>(() => coordinator.StartOwnedCardStudy(runtime, cardId,
                BalanceTable.CreateDefault().OwnerCardGrowth.StudyPrograms[0]));
            Assert.Throws<InvalidOperationException>(() => coordinator.PlaceOwnedCardSkillBlock(runtime, cardId, 1, 0, 0, 0));
            Assert.Throws<InvalidOperationException>(() => coordinator.AutoPlaceOwnedCardSkillBlock(runtime, cardId, 1));
            Assert.Throws<InvalidOperationException>(() => coordinator.RemoveOwnedCardSkillBlock(runtime, cardId, 1));
            Assert.That(runtime.Economy.Money, Is.EqualTo(money));
            Assert.That(runtime.Economy.DevelopmentPoints, Is.EqualTo(points));
            Assert.That(runtime.PlayerGrowth.StudyProjects, Is.Empty);
        }

        [Test]
        public void 포스트시즌미확정과다른조진행중에는오프시즌을열지않는다()
        {
            var runtime = CreateRuntime(out _);
            foreach (var game in runtime.ManagerMode.LiveSeason.Schedule.Games) game.Complete(1, 0);
            if (!runtime.LeagueWorld.IsRegularSeasonCompleted)
                Assert.That(OwnerScheduleGateService.GetPhase(runtime), Is.EqualTo(OwnerSeasonPhase.RegularSeason));
            CompleteRegularSeason(runtime);
            Assert.That(OwnerScheduleGateService.GetPhase(runtime), Is.EqualTo(OwnerSeasonPhase.Postseason));
            Assert.That(OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).IsAllowed, Is.False);
            CompletePostseason(runtime);
            Assert.That(OwnerScheduleGateService.GetPhase(runtime), Is.EqualTo(OwnerSeasonPhase.Offseason));
        }

        [Test]
        public void 오프시즌주차는중복정산과시설보상반복지급을막고저장복원한다()
        {
            var runtime = CreateRuntime(out var adapter);
            CompleteRegularSeason(runtime); CompletePostseason(runtime);
            var coordinator = new ManagerModeCoordinator(BalanceTable.CreateDefault());
            long money = runtime.Economy.Money;
            int points = runtime.Economy.DevelopmentPoints;
            int scouting = runtime.Economy.ScoutingPoints;
            int operationWeek = runtime.ManagerMode.LiveSeason.CurrentWeekIndex;
            for (int week = 0; week < 4; week++)
            {
                Assert.That(coordinator.AdvanceOffseasonWeek(runtime, week), Is.True);
                Assert.That(coordinator.AdvanceOffseasonWeek(runtime, week), Is.False);
                runtime = adapter.Restore(adapter.CreateSaveData(runtime));
                Assert.That(runtime.PlayerGrowth.Offseason.CompletedWeeks, Is.EqualTo(week + 1));
            }
            Assert.That(coordinator.AdvanceOffseasonWeek(runtime, 4), Is.False);
            Assert.That(runtime.Economy.Money, Is.EqualTo(money));
            Assert.That(runtime.Economy.DevelopmentPoints, Is.EqualTo(points));
            Assert.That(runtime.Economy.ScoutingPoints, Is.EqualTo(scouting));
            Assert.That(runtime.ManagerMode.LiveSeason.CurrentWeekIndex, Is.EqualTo(operationWeek));
            Assert.That(OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.OverseasTraining, 1).IsAllowed, Is.False);
            Assert.That(OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).IsAllowed, Is.True,
                "주차를 소모하지 않는 편성은 다음 시즌을 시작하기 전까지 허용한다.");
        }

        [Test]
        public void 남은주차보다긴과정은차감전에거절하고마지막주까지훈련을정산한다()
        {
            var runtime = CreateRuntime(out var adapter);
            CompleteRegularSeason(runtime); CompletePostseason(runtime);
            var coordinator = new ManagerModeCoordinator(BalanceTable.CreateDefault());
            Assert.That(OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.OverseasTraining, 4).IsAllowed, Is.True);
            coordinator.AdvanceOffseasonWeek(runtime, 0);
            int points = runtime.Economy.DevelopmentPoints;
            Assert.That(OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.OverseasTraining, 4).IsAllowed, Is.False);
            Assert.That(OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.OverseasTraining, 3).IsAllowed, Is.True);
            Assert.Throws<InvalidOperationException>(() => coordinator.StartOwnedCardStudy(runtime, runtime.OwnedCards[0].CardId,
                BalanceTable.CreateDefault().OwnerCardGrowth.StudyPrograms[0]));
            Assert.That(runtime.Economy.DevelopmentPoints, Is.EqualTo(points));
            var save = adapter.CreateSaveData(runtime);
            save.playerGrowth.offseasonCompletedWeeks = 5;
            Assert.Throws<ArgumentOutOfRangeException>(() => adapter.Restore(save));
        }

        [Test]
        public void 유학은오프시즌에서만진행하며저장후두경로의영구성장이같다()
        {
            var runtime = CreateRuntime(out var adapter);
            var coordinator = new ManagerModeCoordinator(BalanceTable.CreateDefault());
            string cardId = runtime.OwnedCards.First(card =>
                runtime.WorldCardCatalog.GetPlayerSeason(runtime.WorldCardCatalog.Cards.First(definition => definition.CardId == card.CardId)).PlayerType
                    == Baseball.Core.Players.PlayerType.Batter).CardId;
            runtime.PlayerGrowth.AddStudy(new CardStudyProjectState(cardId, "study_contact", 1, 4));
            CompleteRegularSeason(runtime);
            coordinator.AdvanceWeek(runtime);
            Assert.That(runtime.PlayerGrowth.StudyProjects[0].RemainingWeeks, Is.EqualTo(4));
            CompletePostseason(runtime);
            Assert.Throws<InvalidOperationException>(() => coordinator.AdvanceSeason(runtime));
            coordinator.AdvanceOffseasonWeek(runtime, 0);
            var restored = adapter.Restore(adapter.CreateSaveData(runtime));
            for (int week = 1; week < 4; week++)
            {
                coordinator.AdvanceOffseasonWeek(runtime, week);
                coordinator.AdvanceOffseasonWeek(restored, week);
            }
            Assert.That(runtime.PlayerGrowth.StudyProjects, Is.Empty);
            Assert.That(restored.PlayerGrowth.StudyProjects, Is.Empty);
            runtime.TryGetOwnedCard(cardId, out var originalCard);
            restored.TryGetOwnedCard(cardId, out var restoredCard);
            for (int ability = 0; ability < PlayerAbilityCatalog.AbilityCount; ability++)
                Assert.That(restoredCard.Training.GetStudyBonus((PlayerAbility)ability),
                    Is.EqualTo(originalCard.Training.GetStudyBonus((PlayerAbility)ability)));
            int previous = originalCard.Training.GetStudyBonus(PlayerAbility.Contact);
            Assert.That(coordinator.AdvanceOffseasonWeek(runtime, 3), Is.False);
            Assert.That(originalCard.Training.GetStudyBonus(PlayerAbility.Contact), Is.EqualTo(previous));
        }

        [Test]
        public void 유학신청은정확한비용을한번소비하고부족할때는시작이력을남기지않는다()
        {
            var runtime = CreateRuntime(out var adapter);
            CompleteRegularSeason(runtime); CompletePostseason(runtime);
            var balance = BalanceTable.CreateDefault();
            var coordinator = new ManagerModeCoordinator(balance);
            var definition = runtime.WorldCardCatalog.Cards.First(card => card.CanAcquireFromScout &&
                !runtime.TryGetOwnedCard(card.CardId, out _) &&
                runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerType == Baseball.Core.Players.PlayerType.Batter);
            runtime.AcquireCard(definition.CardId);
            var save = adapter.CreateSaveData(runtime);
            save.managerMode.clubOperation.facilities.Single(facility => facility.type == (int)FacilityType.TrainingCenter).level = 1;
            var program = balance.OwnerCardGrowth.GetStudyProgram("study_contact");
            save.economy.developmentPoints = program.DevelopmentPointCost - 1;
            runtime = adapter.Restore(save);
            Assert.Throws<InvalidOperationException>(() => coordinator.StartOwnedCardStudy(runtime, definition.CardId, program));
            Assert.That(runtime.PlayerGrowth.StudyProjects, Is.Empty);
            runtime.TryGetOwnedCard(definition.CardId, out var owned);
            Assert.That(owned.LastStudySeason, Is.EqualTo(-1));
            save.economy.developmentPoints = program.DevelopmentPointCost;
            runtime = adapter.Restore(save);
            coordinator.StartOwnedCardStudy(runtime, definition.CardId, program);
            Assert.That(runtime.Economy.DevelopmentPoints, Is.Zero);
            Assert.That(runtime.PlayerGrowth.StudyProjects.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => coordinator.StartOwnedCardStudy(runtime, definition.CardId, program));
            Assert.That(runtime.PlayerGrowth.StudyProjects.Count, Is.EqualTo(1));
            var restored = adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(restored.PlayerGrowth.StudyProjects[0].RemainingWeeks, Is.EqualTo(program.DurationWeeks));
            Assert.That(restored.Economy.DevelopmentPoints, Is.Zero);
        }

        [Test]
        public void 다음시즌전환은오프시즌진행을초기화한다()
        {
            var runtime = CreateRuntime(out _);
            CompleteRegularSeason(runtime); CompletePostseason(runtime);
            var coordinator = new ManagerModeCoordinator(BalanceTable.CreateDefault());
            coordinator.AdvanceOffseasonWeek(runtime, 0);
            Assert.That(coordinator.AdvanceSeason(runtime).IsApplied, Is.True);
            Assert.That(runtime.PlayerGrowth.Offseason.CompletedWeeks, Is.Zero);
            Assert.That(OwnerScheduleGateService.GetPhase(runtime), Is.EqualTo(OwnerSeasonPhase.RegularSeason));
        }

        private static ManagerHistoricalRuntimeState CreateRuntime(out ManagerHistoricalSaveAdapter adapter)
        {
            var fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType("Fixture", BindingFlags.NonPublic);
            object fixture = fixtureType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            adapter = (ManagerHistoricalSaveAdapter)fixture.GetType().GetMethod("CreateAdapter").Invoke(fixture, null);
            var state = (ManagerHistoricalRuntimeState)fixture.GetType().GetProperty("State").GetValue(fixture);
            return adapter.Restore(adapter.CreateSaveData(state));
        }

        private static void CompleteRegularSeason(ManagerHistoricalRuntimeState runtime)
        {
            foreach (var group in runtime.LeagueWorld.Groups)
                foreach (var game in group.Season.Schedule.Games)
                    if (!game.IsCompleted) game.Complete(1, 0);
        }

        private static void CompletePostseason(ManagerHistoricalRuntimeState runtime)
        {
            new OwnerPostseasonService(BalanceTable.CreateDefault()).EnsureInitialized(runtime);
            int gameId = 1_500_000;
            foreach (var group in runtime.LeagueWorld.Groups)
                while (!group.Postseason.IsCompleted)
                {
                    var series = group.Postseason.EnsureCurrentSeries();
                    while (!series.IsCompleted)
                    {
                        ScheduledGameState game = series.AppendNextGame(gameId, (ulong)gameId++);
                        bool home = game.HomeTeamId == series.HigherSeedTeamId;
                        game.Complete(home ? 0 : 1, home ? 1 : 0);
                        series.RecordCompletedGame(game);
                    }
                }
        }
    }
}
