using System;
using System.Linq;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    /// <summary>Baked 공통 Definition과 World Schema의 변경 불가 계약을 검증한다.</summary>
    public sealed class HistoricalContentDefinitionTests
    {
        [Test]
        public void PlayerCardEdition_정확히네종만존재한다()
        {
            Assert.That(Enum.GetValues(typeof(PlayerCardEdition)).Length, Is.EqualTo(4));
            Assert.That(Enum.IsDefined(typeof(PlayerCardEdition), PlayerCardEdition.Normal), Is.True);
            Assert.That(Enum.IsDefined(typeof(PlayerCardEdition), PlayerCardEdition.AllStar), Is.True);
            Assert.That(Enum.IsDefined(typeof(PlayerCardEdition), PlayerCardEdition.GoldenGlove), Is.True);
            Assert.That(Enum.IsDefined(typeof(PlayerCardEdition), PlayerCardEdition.Mvp), Is.True);
        }

        [Test]
        public void PlayerSeason_훈련상한이Base보다낮으면거부한다()
        {
            var baseRatings = new AbilityRatings(60);
            var lowCeiling = new AbilityRatings(59);

            Assert.Throws<ArgumentException>(() => CreateSeason(baseRatings, lowCeiling));
        }

        [Test]
        public void PlayerSeason_반환한능력치변경이Definition에유출되지않는다()
        {
            PlayerSeasonDefinition season = CreateSeason(new AbilityRatings(60), new AbilityRatings(70));
            AbilityRatings copy = season.CreateBaseAttributes();

            copy.AddClamped(PlayerAbility.Contact, 10);

            Assert.That(season.CreateBaseAttributes().Get(PlayerAbility.Contact), Is.EqualTo(60));
        }

        [Test]
        public void CardId_같은Season과Edition은항상같다()
        {
            string first = PlayerCardDefinition.CreateStableCardId("SEASON_2011_001", PlayerCardEdition.Mvp);
            string second = PlayerCardDefinition.CreateStableCardId("SEASON_2011_001", PlayerCardEdition.Mvp);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.EqualTo("SEASON_2011_001:Mvp"));
        }

        [Test]
        public void TeamSeason_Core25가정확히25가아니면거부한다()
        {
            string[] twentyFour = Enumerable.Range(1, 24).Select(index => "CARD_" + index).ToArray();

            Assert.Throws<ArgumentException>(() => new TeamSeasonDefinition(
                "COMETS_2011",
                "COMETS",
                2011,
                twentyFour,
                twentyFour,
                50d));
        }

        [Test]
        public void WorldHistorySnapshot_두Mode가같은AwardSchema를보유한다()
        {
            var award = new WorldAwardEntry(
                2011,
                WorldAwardType.RegularSeasonMvp,
                "SEASON_2011_001",
                PlayerPosition.CenterField);
            var record = new WorldAwardRecord(new[] { award });

            var original = new WorldHistorySnapshot(WorldRecordMode.OriginalHistory, 7UL, Array.Empty<SeasonStatistics>(), record);
            var simulated = new WorldHistorySnapshot(WorldRecordMode.SimulatedHistory, 7UL, Array.Empty<SeasonStatistics>(), record);

            Assert.That(original.Awards.HasAward("SEASON_2011_001", WorldAwardType.RegularSeasonMvp), Is.True);
            Assert.That(simulated.Awards.HasAward("SEASON_2011_001", WorldAwardType.RegularSeasonMvp), Is.True);
        }

        private static PlayerSeasonDefinition CreateSeason(AbilityRatings baseRatings, AbilityRatings ceiling)
        {
            return new PlayerSeasonDefinition(
                "SEASON_2011_001",
                "PERSON_001",
                2011,
                "COMETS",
                "COMETS_2011",
                PlayerPosition.CenterField,
                PitcherRole.MiddleRelief,
                PlayerType.Batter,
                RegistrationType.Domestic,
                baseRatings,
                5,
                ceiling);
        }
    }
}
