using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>은퇴 기억 점수, 선정 제약, 불변 스냅샷과 월드 은퇴 커밋을 검증한다.</summary>
    public sealed class RetirementRecapTests
    {
        [Test]
        public void MemoryScore_기획가중치3025201510을적용한다()
        {
            CareerMemoryRecord memory = CreateMemory(
                "score", 2028, 1, CareerMemoryType.ExceptionalGame,
                importance: 100, impact: 80, agency: 60, rarity: 40, emotion: 20);

            Assert.That(memory.MemoryScore, Is.EqualTo(70d).Within(0.000001d));
        }

        [Test]
        public void MemoryLog_중복ID와시간역행을거부한다()
        {
            var log = new CareerMemoryLog();
            log.Append(CreateMemory("first", 2028, 5, CareerMemoryType.CareerDebut));

            Assert.Throws<InvalidOperationException>(() =>
                log.Append(CreateMemory("first", 2028, 6, CareerMemoryType.FirstHit)));
            Assert.Throws<InvalidOperationException>(() =>
                log.Append(CreateMemory("past", 2028, 4, CareerMemoryType.FirstHit)));
        }

        [Test]
        public void CreateSnapshot_필수순간과선택역경을포함하고동일유형은두개까지만선정한다()
        {
            CreateCareer(out CareerState career, out BalanceTable balance);
            int year = career.CurrentLeague.CurrentSeason.Year;
            CareerMemoryLog log = career.Retirement.MemoryLog;
            log.Append(CreateMemory("debut", year, 1, CareerMemoryType.CareerDebut));
            for (int index = 0; index < 6; index++)
            {
                log.Append(CreateMemory(
                    "game_" + index,
                    year,
                    2 + index,
                    CareerMemoryType.ExceptionalGame,
                    importance: 100 - index,
                    impact: 85,
                    rarity: 85,
                    emotion: 80));
            }
            log.Append(CreateMemory(
                "injury", year, 10, CareerMemoryType.Injury,
                importance: 40, impact: 45, emotion: 90,
                tags: new[] { "adversity" }));
            log.Append(CreateMemory(
                "choice", year, 11, CareerMemoryType.ContractDeclined,
                importance: 50, impact: 65, agency: 100,
                tags: new[] { "player_choice" }));
            log.Append(CreateMemory(
                "final", year, 12, CareerMemoryType.FinalAppearance,
                importance: 100, impact: 100, agency: 80, rarity: 100, emotion: 100,
                tags: new[] { "final" }));

            RetirementRecapSnapshot snapshot = new RetirementRecapService(balance)
                .CreateSnapshot(career, RetirementReason.Voluntary);

            Assert.That(snapshot.FeaturedMemories.Count, Is.LessThanOrEqualTo(7));
            Assert.That(ContainsType(snapshot, CareerMemoryType.CareerDebut), Is.True);
            Assert.That(ContainsType(snapshot, CareerMemoryType.FinalAppearance), Is.True);
            Assert.That(ContainsId(snapshot, "choice"), Is.True);
            Assert.That(ContainsId(snapshot, "injury"), Is.True);
            Assert.That(CountType(snapshot, CareerMemoryType.ExceptionalGame), Is.LessThanOrEqualTo(2));
            for (int index = 1; index < snapshot.FeaturedMemories.Count; index++)
            {
                CareerMemoryRecord previous = snapshot.FeaturedMemories[index - 1];
                CareerMemoryRecord current = snapshot.FeaturedMemories[index];
                Assert.That(
                    current.Season > previous.Season ||
                    current.Season == previous.Season && current.DateIndex >= previous.DateIndex,
                    Is.True);
            }
        }

        [Test]
        public void WorldRetirePlayer_로스터계약과소속을정리하고역사에남긴다()
        {
            CreateCareer(out CareerState career, out _);
            int playerId = career.MyPlayerId;
            int teamId = career.MyPlayer.CurrentTeamId;
            int seasonId = career.CurrentLeague.CurrentSeason.SeasonId;

            career.World.RetirePlayer(
                playerId,
                seasonId,
                career.CurrentExpectedRole,
                "test_retirement");

            Assert.That(career.MyPlayer.CareerStatus, Is.EqualTo(PlayerCareerStatus.Retired));
            Assert.That(career.MyPlayer.CurrentTeamId, Is.Zero);
            Assert.That(career.MyPlayer.CurrentLeagueId.IsAssigned, Is.False);
            Assert.That(career.CurrentContract.IsActive, Is.False);
            Assert.That(ContainsRosterPlayer(career.World.GetTeam(teamId), playerId), Is.False);
            Assert.That(career.World.MovementLedger.Records[^1].MovementType,
                Is.EqualTo(PlayerMovementType.Retirement));
            Assert.That(career.World.DomainEvents.Contains($"retirement:{seasonId}:{playerId}"), Is.True);
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        [Test]
        public void ViewBuilder_스냅샷만으로5막과8개기록관탭을완성한다()
        {
            CreateCareer(out CareerState career, out BalanceTable balance);
            RetirementRecapSnapshot snapshot = new RetirementRecapService(balance)
                .CreateSnapshot(career, RetirementReason.DeclaredFinalSeason);
            var builder = new RetirementRecapViewBuilder();

            RetirementRecapBeat[] beats = builder.BuildRecap(snapshot);

            Assert.That(beats, Is.Not.Empty);
            Assert.That(beats[0].Act, Is.EqualTo(RetirementRecapAct.Prologue));
            Assert.That(beats[^1].Act, Is.EqualTo(RetirementRecapAct.CareerCard));
            foreach (RetirementArchiveTab tab in Enum.GetValues(typeof(RetirementArchiveTab)))
            {
                RetirementArchivePage page = builder.BuildArchivePage(snapshot, tab);
                Assert.That(page.Title, Is.Not.Empty);
                Assert.That(page.Body, Is.Not.Null);
            }
        }

        private static void CreateCareer(out CareerState career, out BalanceTable balance)
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var flow = new NewGameFlow(configuration, 20260829UL);
            flow.SubmitIdentity("회고 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(65, 60, 62, 53, 65, 55));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            career = flow.Career;
            balance = configuration.Balance;
        }

        private static CareerMemoryRecord CreateMemory(
            string id,
            int season,
            int dateIndex,
            CareerMemoryType type,
            int importance = 50,
            int impact = 50,
            int agency = 0,
            int rarity = 50,
            int emotion = 50,
            string[] tags = null)
        {
            return new CareerMemoryRecord(
                id,
                NewGameFlow.MyPlayerId,
                season,
                dateIndex,
                1,
                type,
                "title",
                "narrative",
                0,
                string.Empty,
                0,
                importance,
                impact,
                agency,
                rarity,
                emotion,
                Array.Empty<MemoryStatValue>(),
                tags ?? Array.Empty<string>(),
                "asset");
        }

        private static bool ContainsType(RetirementRecapSnapshot snapshot, CareerMemoryType type)
        {
            for (int index = 0; index < snapshot.FeaturedMemories.Count; index++)
            {
                if (snapshot.FeaturedMemories[index].Type == type)
                    return true;
            }
            return false;
        }

        private static bool ContainsId(RetirementRecapSnapshot snapshot, string memoryId)
        {
            for (int index = 0; index < snapshot.FeaturedMemories.Count; index++)
            {
                if (snapshot.FeaturedMemories[index].MemoryId == memoryId)
                    return true;
            }
            return false;
        }

        private static int CountType(RetirementRecapSnapshot snapshot, CareerMemoryType type)
        {
            int result = 0;
            for (int index = 0; index < snapshot.FeaturedMemories.Count; index++)
            {
                if (snapshot.FeaturedMemories[index].Type == type)
                    result++;
            }
            return result;
        }

        private static bool ContainsRosterPlayer(TeamState team, int playerId)
        {
            for (int index = 0; index < team.RosterPlayerIds.Count; index++)
            {
                if (team.RosterPlayerIds[index] == playerId)
                    return true;
            }
            return false;
        }
    }
}
