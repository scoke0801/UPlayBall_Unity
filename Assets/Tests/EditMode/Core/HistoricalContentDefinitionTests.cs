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
        public void PlayerSeason_부포지션을복사하고경기선수에전달한다()
        {
            var positions = new[] { new PositionProficiency(PlayerPosition.RightField, 100) };
            var season = new PlayerSeasonDefinition("s", "p", 2010, "f", "f_2010",
                PlayerPosition.LeftField, PitcherRole.MiddleRelief, PlayerType.Batter,
                RegistrationType.Domestic, new AbilityRatings(60), 5, new AbilityRatings(70),
                secondaryPositions: positions);
            positions[0] = new PositionProficiency(PlayerPosition.Catcher, 35);
            var player = new Player(1, "검증 선수", season.Position, Handedness.Left, Handedness.Right,
                season.CreateBaseAttributes().ToBatterAttributes(), season.CreateBaseAttributes().ToPitcherAttributes(),
                secondaryPositions: season.SecondaryPositions);
            Assert.That(player.GetPositionProficiency(PlayerPosition.RightField), Is.EqualTo(100));
            Assert.That(player.GetPositionProficiency(PlayerPosition.Catcher), Is.EqualTo(35));
        }

        [TestCase(PlayerPosition.LeftField)]
        [TestCase(PlayerPosition.StartingPitcher)]
        [TestCase(PlayerPosition.Unknown)]
        public void PlayerSeason_부적격부포지션을거부한다(PlayerPosition position)
        {
            Assert.Throws<ArgumentException>(() => new PlayerSeasonDefinition("s", "p", 2010, "f", "f_2010",
                PlayerPosition.LeftField, PitcherRole.MiddleRelief, PlayerType.Batter,
                RegistrationType.Domestic, new AbilityRatings(60), 5, new AbilityRatings(70),
                secondaryPositions: new[] { new PositionProficiency(position, 100) }));
        }

        [Test]
        public void PlayerCardEdition_기본네종의저장값뒤에특수카드를추가한다()
        {
            Assert.That((int)PlayerCardEdition.Normal, Is.EqualTo(0));
            Assert.That((int)PlayerCardEdition.AllStar, Is.EqualTo(1));
            Assert.That((int)PlayerCardEdition.GoldenGlove, Is.EqualTo(2));
            Assert.That((int)PlayerCardEdition.Mvp, Is.EqualTo(3));
            Assert.That((int)PlayerCardEdition.Rare, Is.EqualTo(4));
            Assert.That((int)PlayerCardEdition.Ex, Is.EqualTo(5));
            Assert.That((int)PlayerCardEdition.CareerHigh, Is.EqualTo(6));
            Assert.That((int)PlayerCardEdition.Legend, Is.EqualTo(7));
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
        public void PlayerSeason_Provenance와NaturalRoleConfidence를Bake값으로보존한다()
        {
            var season = new PlayerSeasonDefinition(
                "SEASON_REPLACEMENT_1982_001",
                "PERSON_REPLACEMENT_1982_001",
                1982,
                "COMETS",
                "COMETS_1982",
                PlayerPosition.ReliefPitcher,
                PitcherRole.MiddleRelief,
                PlayerType.Pitcher,
                RegistrationType.Domestic,
                new AbilityRatings(45),
                2,
                new AbilityRatings(55),
                PlayerDataProvenance.ReplacementGenerated,
                PitcherRoleConfidence.Low);

            Assert.That(season.DataProvenance, Is.EqualTo(PlayerDataProvenance.ReplacementGenerated));
            Assert.That(season.PitcherRoleConfidence, Is.EqualTo(PitcherRoleConfidence.Low));
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
