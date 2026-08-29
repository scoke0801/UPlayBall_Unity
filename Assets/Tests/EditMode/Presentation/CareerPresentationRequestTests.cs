using Baseball.Core.Growth;
using Baseball.Presentation.Career;
using NUnit.Framework;

namespace Baseball.Presentation.Tests
{
    public sealed class CareerPresentationRequestTests
    {
        [Test]
        public void Queue_같은RequestId는한번만대기시킨다()
        {
            var queue = new CareerPresentationQueue();
            CareerPresentationRequest request = CreateRequest("same");

            Assert.That(queue.Enqueue(request), Is.True);
            Assert.That(queue.Enqueue(request), Is.False);
            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue.TryDequeue(out CareerPresentationRequest dequeued), Is.True);
            Assert.That(dequeued, Is.SameAs(request));
        }

        [Test]
        public void GrowthFactory_유학은Travel연출과주차를보존한다()
        {
            GrowthResultRecord result = CreateGrowthResult(
                GrowthSourceType.Study,
                "japan_batting_camp",
                weeksSpent: 6);

            bool created = CareerPresentationRequestFactory.TryCreateGrowthActivity(
                result,
                "김하준",
                3,
                isRepeat: false,
                out CareerPresentationRequest request);

            Assert.That(created, Is.True);
            Assert.That(request.Type, Is.EqualTo(CareerPresentationType.OverseasTraining));
            Assert.That(request.Grade, Is.EqualTo(CareerPresentationGrade.Activity));
            Assert.That(request.StartWeek, Is.EqualTo(3));
            Assert.That(request.EndWeek, Is.EqualTo(8));
            Assert.That(request.Title, Is.EqualTo("동아시아 컨택 캠프"));
        }

        [Test]
        public void GrowthFactory_반복훈련은CompactCut을사용한다()
        {
            GrowthResultRecord result = CreateGrowthResult(
                GrowthSourceType.PersonalTraining,
                "personal_batting",
                weeksSpent: 2);

            bool created = CareerPresentationRequestFactory.TryCreateGrowthActivity(
                result,
                "김하준",
                5,
                isRepeat: true,
                out CareerPresentationRequest request);

            Assert.That(created, Is.True);
            Assert.That(request.Type, Is.EqualTo(CareerPresentationType.Training));
            Assert.That(request.Grade, Is.EqualTo(CareerPresentationGrade.Compact));
        }

        [Test]
        public void GrowthFactory_RestSourceId는회복연출로구분한다()
        {
            GrowthResultRecord result = CreateGrowthResult(
                GrowthSourceType.PersonalTraining,
                "rest",
                weeksSpent: 1,
                conditionChange: 35);

            bool created = CareerPresentationRequestFactory.TryCreateGrowthActivity(
                result,
                "김하준",
                4,
                isRepeat: false,
                out CareerPresentationRequest request);

            Assert.That(created, Is.True);
            Assert.That(request.Type, Is.EqualTo(CareerPresentationType.Rest));
            Assert.That(request.Stats[0].Label, Is.EqualTo("컨디션"));
            Assert.That(request.Stats[0].Value, Is.EqualTo("+35"));
        }

        [Test]
        public void AwardReplay_정규시즌Mvp는보상콜백없는MajorCut으로복원한다()
        {
            var award = new Baseball.Game.Career.CareerAwardRecordView(
                2028,
                Baseball.Game.Career.LeagueLevel.Major,
                Baseball.Game.Career.AwardCategory.RegularSeasonMvp,
                Baseball.Core.Players.PlayerPosition.Shortstop,
                isCurrent: false);

            bool created = CareerPresentationRequestFactory.TryCreateAwardReplay(
                award,
                "김하준",
                out CareerPresentationRequest request);

            Assert.That(created, Is.True);
            Assert.That(request.Type, Is.EqualTo(CareerPresentationType.RegularSeasonMvp));
            Assert.That(request.Grade, Is.EqualTo(CareerPresentationGrade.Major));
            Assert.That(request.Completed, Is.Null);
        }

        private static CareerPresentationRequest CreateRequest(string id)
        {
            return new CareerPresentationRequest(
                id,
                CareerPresentationType.Training,
                CareerPresentationGrade.Activity,
                2028,
                "OFFSEASON",
                "훈련",
                "김하준",
                "결과",
                System.Array.Empty<PresentationStat>());
        }

        private static GrowthResultRecord CreateGrowthResult(
            GrowthSourceType sourceType,
            string sourceId,
            int weeksSpent,
            int conditionChange = -10)
        {
            return new GrowthResultRecord(
                1,
                2028,
                sourceType,
                sourceId,
                new GrowthInputSnapshot(20, 80, WorkEthicGrade.Diligent, TrainingFitGrade.High, 0),
                1234UL,
                System.Array.Empty<AbilityChange>(),
                System.Array.Empty<AbilityChange>(),
                conditionChange,
                15_000_000L,
                weeksSpent);
        }
    }
}
