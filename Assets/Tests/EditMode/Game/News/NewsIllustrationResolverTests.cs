using Baseball.Game.Career.News;
using NUnit.Framework;

namespace Baseball.Game.Tests.News
{
    public sealed class NewsIllustrationResolverTests
    {
        [TestCase("season_1_champion_4", NewsIllustrationKind.PostseasonChampion)]
        [TestCase("season_1_award_postseason_mvp", NewsIllustrationKind.PostseasonMvp)]
        [TestCase("season_1_award_gold_glove_shortstop", NewsIllustrationKind.GoldenGlove)]
        [TestCase("season_1_award_regular_season_mvp", NewsIllustrationKind.RegularSeasonMvp)]
        [TestCase("season_1_activity_study_japan", NewsIllustrationKind.OverseasTraining)]
        [TestCase("season_1_activity_rest_2", NewsIllustrationKind.Rest)]
        public void Resolve_구조화된사건Id를이미지키로변환한다(
            string eventId,
            NewsIllustrationKind expected)
        {
            NewsIllustrationKind actual = NewsIllustrationResolver.Resolve(new[] { eventId });

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_병합기사에서는우승이미지가일반활동보다우선한다()
        {
            NewsIllustrationKind actual = NewsIllustrationResolver.Resolve(new[]
            {
                "season_1_activity_personal_training_1",
                "season_1_champion_4"
            });

            Assert.That(actual, Is.EqualTo(NewsIllustrationKind.PostseasonChampion));
        }
    }
}
