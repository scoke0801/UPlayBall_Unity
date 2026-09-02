using Baseball.Core.Historical;
using Baseball.Core.Players;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    public sealed class CareerPlayerWildcardTests
    {
        [Test]
        public void ResolveIdentity_이적한현재구단의정체성을사용한다()
        {
            var wildcard = new CareerPlayerWildcard("career-player");
            string[] cardIds = CreateCardIds();
            var team = new TeamSeasonDefinition("team:2031:B", "B", 2031, cardIds, cardIds, 50d);

            TeamColorEligibilityKey key = wildcard.ResolveIdentity(team);

            Assert.That(key.OriginYear, Is.EqualTo(2031));
            Assert.That(key.OriginFranchiseId, Is.EqualTo("B"));
            Assert.That(key.OriginTeamSeasonKey, Is.EqualTo("team:2031:B"));
            Assert.That(key.Edition, Is.EqualTo(PlayerCardEdition.Normal));
        }

        [Test]
        public void ResolveHonorEditions_실제수상과발표시점및유효기간을지킨다()
        {
            var wildcard = new CareerPlayerWildcard("career-player");
            var awards = new WorldAwardRecord(new[]
            {
                new WorldAwardEntry(2030, WorldAwardType.AllStar, "career-player", PlayerPosition.Shortstop),
                new WorldAwardEntry(2030, WorldAwardType.GoldenGlove, "career-player", PlayerPosition.Shortstop),
                new WorldAwardEntry(2030, WorldAwardType.RegularSeasonMvp, "career-player", PlayerPosition.Shortstop)
            });

            Assert.That(wildcard.ResolveHonorEditions(
                awards, 2030, CareerSeasonPhase.BeforeAllStarSelection), Is.Empty);
            Assert.That(wildcard.ResolveHonorEditions(
                awards, 2030, CareerSeasonPhase.AfterAllStarSelection), Is.EqualTo(new[] { PlayerCardEdition.AllStar }));
            Assert.That(wildcard.ResolveHonorEditions(
                awards, 2030, CareerSeasonPhase.AfterSeasonAwards),
                Is.EqualTo(new[] { PlayerCardEdition.AllStar, PlayerCardEdition.GoldenGlove, PlayerCardEdition.Mvp }));
            Assert.That(wildcard.ResolveHonorEditions(
                awards, 2031, CareerSeasonPhase.BeforeAllStarSelection),
                Is.EqualTo(new[] { PlayerCardEdition.GoldenGlove, PlayerCardEdition.Mvp }));
            Assert.That(wildcard.ResolveHonorEditions(
                awards, 2032, CareerSeasonPhase.AfterSeasonAwards), Is.Empty);
        }

        [Test]
        public void ResolveHonorEditions_다른선수수상은활성화하지않는다()
        {
            var wildcard = new CareerPlayerWildcard("career-player");
            var awards = new WorldAwardRecord(new[]
            {
                new WorldAwardEntry(2030, WorldAwardType.AllStar, "other-player", PlayerPosition.Catcher)
            });

            Assert.That(wildcard.ResolveHonorEditions(
                awards, 2030, CareerSeasonPhase.AfterSeasonAwards), Is.Empty);
        }

        private static string[] CreateCardIds()
        {
            var ids = new string[25];
            for (int index = 0; index < ids.Length; index++)
                ids[index] = "card-" + index;
            return ids;
        }
    }
}
