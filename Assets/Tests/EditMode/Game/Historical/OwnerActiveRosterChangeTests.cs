using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>1군 카드 교체 후보가 역할과 저장 상태를 일관되게 바꾸는지 검증한다.</summary>
    public sealed class OwnerActiveRosterChangeTests
    {
        [Test]
        public void ReplaceCard_기존1군역할을보존하고새카드원본을사용한다()
        {
            var roster = new CurrentRosterState("team", new[]
            {
                new ActiveRosterEntry("OLD", "season-old", "person-old",
                    RegistrationType.Domestic, ActiveRosterRole.StartingCatcher)
            });
            var card = new PlayerCardDefinition(
                "NEW",
                "season-new",
                PlayerCardEdition.Normal,
                new int[PlayerAbilityCatalog.AbilityCount]);
            var season = new PlayerSeasonDefinition(
                "season-new",
                "person-new",
                2025,
                "franchise",
                "team-2025",
                PlayerPosition.Catcher,
                PitcherRole.Starter,
                PlayerType.Batter,
                RegistrationType.Domestic,
                new AbilityRatings(50),
                5,
                new AbilityRatings(70));

            CurrentRosterState result = OwnerActiveRosterChangeBuilder.ReplaceCard(
                roster,
                "OLD",
                card,
                season);

            Assert.That(result.Entries[0].CardId, Is.EqualTo("NEW"));
            Assert.That(result.Entries[0].PlayerPersonId, Is.EqualTo("person-new"));
            Assert.That(result.Entries[0].Role, Is.EqualTo(ActiveRosterRole.StartingCatcher));
        }

        [Test]
        public void ReplacePlayerStatus_빠진선수상태를중립컨디션의새선수로교체한다()
        {
            var status = new TeamSeasonPlayerStatusState("team", new[]
            {
                new TeamSeasonPlayerStatus("person-old", 62)
            });

            TeamSeasonPlayerStatusState result = OwnerActiveRosterChangeBuilder.ReplacePlayerStatus(
                status,
                "person-old",
                "person-new",
                80);

            Assert.That(result.TryGetPlayer("person-old", out _), Is.False);
            Assert.That(result.GetRequiredPlayer("person-new").StoredBaseCondition, Is.EqualTo(80));
        }

        [Test]
        public void ClearUnavailableTeamColors_발동조건을잃은슬롯만해제하고배치를보존한다()
        {
            LineupPresetState source = CreatePreset(new[] { "TC_KEEP", "TC_DROP" });

            LineupPresetState result = OwnerActiveRosterChangeBuilder.ClearUnavailableTeamColors(
                source,
                new[] { "TC_KEEP" },
                out int clearedCount);

            Assert.That(clearedCount, Is.EqualTo(1));
            Assert.That(result.TeamColorIds, Is.EqualTo(new[] { "TC_KEEP", null }));
            Assert.That(result.StarterRotationCardIds, Is.EqualTo(source.StarterRotationCardIds));
            Assert.That(result.DefaultTacticCardIds, Is.EqualTo(source.DefaultTacticCardIds));
        }

        [Test]
        public void ClearUnavailableTeamColors_모두사용가능하면원본프리셋을재사용한다()
        {
            LineupPresetState source = CreatePreset(new[] { "TC_A", "TC_B" });

            LineupPresetState result = OwnerActiveRosterChangeBuilder.ClearUnavailableTeamColors(
                source,
                new[] { "TC_A", "TC_B" },
                out int clearedCount);

            Assert.That(clearedCount, Is.Zero);
            Assert.That(result, Is.SameAs(source));
        }

        private static LineupPresetState CreatePreset(string[] teamColorIds)
        {
            return new LineupPresetState(
                "default",
                "기본 라인업",
                new[]
                {
                    new LineupPresetSlot("H0", PlayerPosition.Catcher),
                    new LineupPresetSlot("H1", PlayerPosition.FirstBase),
                    new LineupPresetSlot("H2", PlayerPosition.SecondBase),
                    new LineupPresetSlot("H3", PlayerPosition.ThirdBase),
                    new LineupPresetSlot("H4", PlayerPosition.Shortstop),
                    new LineupPresetSlot("H5", PlayerPosition.LeftField),
                    new LineupPresetSlot("H6", PlayerPosition.CenterField),
                    new LineupPresetSlot("H7", PlayerPosition.RightField),
                    new LineupPresetSlot("H8", PlayerPosition.DesignatedHitter)
                },
                new[] { "H0", "H1", "H2", "H3", "H4", "H5", "H6", "H7", "H8" },
                new[] { "B0", "B1", "B2", "B3", "B4" },
                new[] { "S0", "S1", "S2", "S3", "S4" },
                new[] { "R0", "R1", "R2", "R3" },
                "SETUP",
                "CLOSER",
                teamColorIds,
                new[] { "TACTIC" });
        }
    }
}
