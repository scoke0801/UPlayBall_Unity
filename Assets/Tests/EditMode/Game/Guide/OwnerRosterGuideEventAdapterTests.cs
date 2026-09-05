using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Guide;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Guide
{
    /// <summary>구단주 로스터 Fact 변환과 dedupe Revision의 결정론을 검증한다.</summary>
    public sealed class OwnerRosterGuideEventAdapterTests
    {
        private const string TeamSeasonKey = "1982:TEAM_A";

        [Test]
        public void Revision_같은구성이면항상같은값이다()
        {
            int first = OwnerRosterRevision.Compute(CreateRoster());
            int second = OwnerRosterRevision.Compute(CreateRoster());

            Assert.AreEqual(first, second);
            Assert.GreaterOrEqual(first, 0);
        }

        [Test]
        public void Revision_역할이바뀌면값이달라진다()
        {
            CurrentRosterState changed = CreateRoster(
                (index, entry) => index == 0
                    ? Entry(entry.CardId, entry.PlayerPersonId, ActiveRosterRole.BenchHitter)
                    : entry);

            Assert.AreNotEqual(
                OwnerRosterRevision.Compute(CreateRoster()),
                OwnerRosterRevision.Compute(changed));
        }

        [Test]
        public void Revision_선수가바뀌면값이달라진다()
        {
            CurrentRosterState changed = CreateRoster(
                (index, entry) => index == 0
                    ? Entry("CARD_X", "PERSON_X", entry.Role)
                    : entry);

            Assert.AreNotEqual(
                OwnerRosterRevision.Compute(CreateRoster()),
                OwnerRosterRevision.Compute(changed));
        }

        [Test]
        public void CreateFacts_유효한로스터는RosterValidated하나만만든다()
        {
            GuideFact[] facts = Create(new RosterValidationResult(new List<RosterValidationIssue>()));

            Assert.AreEqual(1, facts.Length);
            Assert.AreEqual("RosterValidated", facts[0].FactType);
            Assert.AreEqual(GuideModeScope.Owner, facts[0].Mode);
            Assert.AreEqual("7", facts[0].RuntimeContext["rosterRevision"]);
        }

        [Test]
        public void CreateFacts_인원부족은검증결과값을그대로payload로옮긴다()
        {
            GuideFact[] facts = Create(Invalid(
                new RosterValidationIssue(RosterValidationIssueCode.TotalCount, 25, 24)));

            Assert.AreEqual(1, facts.Length);
            Assert.AreEqual("RosterInvalidTotal", facts[0].FactType);
            Assert.AreEqual("24", facts[0].Payload["current"]);
            Assert.AreEqual("25", facts[0].Payload["required"]);
        }

        [Test]
        public void CreateFacts_야수와투수수가모두어긋나도역할Fact는한번만만든다()
        {
            GuideFact[] facts = Create(Invalid(
                new RosterValidationIssue(RosterValidationIssueCode.HitterCount, 14, 13),
                new RosterValidationIssue(RosterValidationIssueCode.PitcherCount, 11, 12)));

            Assert.AreEqual(1, facts.Length);
            Assert.AreEqual("RosterRoleCountInvalid", facts[0].FactType);
            // payload는 issue 값이 아니라 실제 로스터 집계다.
            Assert.AreEqual("2", facts[0].Payload["hitters"]);
            Assert.AreEqual("1", facts[0].Payload["pitchers"]);
        }

        [Test]
        public void CreateFacts_중복선수는이름을payload로해석해dedupe에personId를남긴다()
        {
            GuideFact[] facts = Create(Invalid(
                new RosterValidationIssue(
                    RosterValidationIssueCode.DuplicatePlayerPersonId,
                    1,
                    2,
                    "PERSON_1")));

            Assert.AreEqual(1, facts.Length);
            Assert.AreEqual("DuplicatePlayerPerson", facts[0].FactType);
            Assert.AreEqual("이름:PERSON_1", facts[0].Payload["playerName"]);
            Assert.AreEqual("PERSON_1", facts[0].RuntimeContext["playerPersonId"]);
        }

        private static GuideFact[] Create(RosterValidationResult validation)
        {
            return new OwnerRosterGuideEventAdapter().CreateFacts(
                CreateRoster(),
                validation,
                new GuideFactIdentity(1234UL, "owner-roster-test", "owner-save", 0),
                rosterRevision: 7,
                personId => "이름:" + personId);
        }

        private static RosterValidationResult Invalid(params RosterValidationIssue[] issues) =>
            new(issues);

        /// <summary>야수 2명·투수 1명만 둔 최소 구성으로 집계 payload를 확인 가능하게 만든다.</summary>
        private static CurrentRosterState CreateRoster(
            System.Func<int, ActiveRosterEntry, ActiveRosterEntry> replace = null)
        {
            var entries = new List<ActiveRosterEntry>
            {
                Entry("CARD_1", "PERSON_1", ActiveRosterRole.StartingCatcher),
                Entry("CARD_2", "PERSON_2", ActiveRosterRole.StartingFirstBase),
                Entry("CARD_3", "PERSON_3", ActiveRosterRole.Closer)
            };
            if (replace != null)
                for (int index = 0; index < entries.Count; index++)
                    entries[index] = replace(index, entries[index]);
            return new CurrentRosterState(TeamSeasonKey, entries);
        }

        private static ActiveRosterEntry Entry(string cardId, string personId, ActiveRosterRole role) =>
            new(cardId, personId + ":S", personId, RegistrationType.Domestic, role);
    }
}
