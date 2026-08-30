using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
using Baseball.Simulation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 캐릭터 생성부터 계약과 Rookie 정규 시즌 시작까지의 Game 상태 전이를 검증한다.
    /// </summary>
    public sealed class NewGameFlowTests
    {
        [Test]
        public void CompleteFlow_계약과세이브상태를만들고Rookie정규시즌을시작한다()
        {
            NewGameFlow flow = CreatePlayerCard(seed: 424242UL);

            flow.GenerateOffers();
            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.ContractOffers));
            Assert.That(flow.State.SetupResult.Offers.Length, Is.InRange(3, 5));

            int selectedTeamId = flow.State.SetupResult.Offers[0].Team.TeamId;
            flow.SelectOffer(selectedTeamId);
            flow.SignSelectedOffer();

            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.ContractComplete));
            Assert.That(flow.Career.SaveVersion, Is.EqualTo(NewGameFlow.CurrentSaveVersion));
            Assert.That(flow.Career.MyPlayer.Name, Is.EqualTo("최민석"));
            Assert.That(flow.Career.MyPlayer.Nationality, Is.EqualTo("대한민국"));
            Assert.That(flow.Career.MyPlayer.CurrentTeamId, Is.EqualTo(selectedTeamId));
            Assert.That(flow.Career.World.WorldSeed, Is.EqualTo(424242UL));
            Assert.That(
                flow.Career.CurrentLeague.RandomSeed,
                Is.Not.EqualTo(flow.Career.World.GetLeague(LeagueId.MinorMain).RandomSeed));
            Assert.That(flow.Career.CurrentLeague.Teams.Count, Is.EqualTo(8));
            Assert.That(flow.Career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Preseason));
            Assert.That(flow.Career.AvailableMoney, Is.EqualTo(flow.Career.CurrentContract.SigningBonus));

            flow.StartRookieSeason();

            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.Completed));
            Assert.That(flow.Career.CurrentLeague.CurrentSeason.LeagueLevel, Is.EqualTo(LeagueLevel.Rookie));
            Assert.That(flow.Career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.RegularSeason));
        }

        [Test]
        public void SelectPosition_타자유형에서투수포지션을선택하면거부한다()
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), 1UL);
            flow.SubmitIdentity("테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);

            Assert.Throws<System.ArgumentException>(() =>
                flow.SelectPosition(PlayerPosition.StartingPitcher));
            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.Position));
        }

        [Test]
        public void SubmitAttributes_포지션과어긋난빌드도경고만하고진행한다()
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), 9UL);
            flow.SubmitIdentity("장타 유격수", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);

            flow.SubmitBatterAttributes(new BatterAttributes(70, 75, 50, 50, 50, 65));

            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.PlayerCard));
            Assert.That(flow.BuildWarning, Is.Not.Empty);
            Assert.DoesNotThrow(flow.GenerateOffers);
        }

        [Test]
        public void GenerateOffers_같은Seed와선택이면계약목록이같다()
        {
            NewGameFlow first = CreatePlayerCard(777UL);
            NewGameFlow second = CreatePlayerCard(777UL);

            first.GenerateOffers();
            second.GenerateOffers();

            Assert.That(second.State.SetupResult.Offers.Length, Is.EqualTo(first.State.SetupResult.Offers.Length));
            for (int index = 0; index < first.State.SetupResult.Offers.Length; index++)
            {
                Assert.That(
                    second.State.SetupResult.Offers[index].Team.TeamId,
                    Is.EqualTo(first.State.SetupResult.Offers[index].Team.TeamId));
                Assert.That(
                    second.State.SetupResult.Offers[index].AnnualSalary,
                    Is.EqualTo(first.State.SetupResult.Offers[index].AnnualSalary));
            }
        }

        [Test]
        public void GuidedPitcherFlow_5단계선택을계약후커리어프로필에보존한다()
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), 20260829UL);

            flow.SubmitBasicInformation("  김민우  ", PlayerType.Pitcher, Handedness.Left, Handedness.Left);
            flow.SubmitCreationPosition(PlayerPosition.Unknown, PitcherRole.Starter);
            flow.SubmitCreationAttributes(new[] { 64, 61, 60, 55 });
            flow.SubmitPitcherDetails(
                new[] { PitchType.FourSeamFastball, PitchType.Slider, PitchType.Changeup },
                PitchType.Slider);
            flow.SubmitMatchSettings(
                BattingApproach.Balanced,
                PitchingApproach.ControlFirst,
                MatchProgressMode.InterveneOnPlayer,
                gameSpeed: 2,
                autoSlowOnPlayerEvent: true);

            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.FinalConfirmation));
            Assert.That(flow.State.Draft.PlayerName, Is.EqualTo("김민우"));
            Assert.That(flow.State.Draft.PitchRepertoire.Length, Is.EqualTo(3));
            Assert.That(flow.State.Draft.PitchRepertoire[1].IsPrimary, Is.True);
            Assert.That(flow.State.Draft.PitchRepertoire[1].Proficiency, Is.EqualTo(55));

            flow.ConfirmCreation();
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();

            Assert.That(flow.Career.CreationProfile.PlayerType, Is.EqualTo(PlayerType.Pitcher));
            Assert.That(flow.Career.CreationProfile.PreferredPitcherRole, Is.EqualTo(PitcherRole.Starter));
            Assert.That(flow.Career.CreationProfile.InitialAttributes, Is.EqualTo(new[] { 64, 61, 60, 55 }));
            Assert.That(flow.Career.GameSettings.PitchingApproach, Is.EqualTo(PitchingApproach.ControlFirst));
            Assert.That(flow.Career.GameSettings.MatchProgressMode, Is.EqualTo(MatchProgressMode.InterveneOnPlayer));
            Assert.That(flow.Career.GameSettings.GameSpeed, Is.EqualTo(2));
            Assert.That(flow.Career.GameSettings.AutoSlowOnPlayerEvent, Is.True);
        }

        [Test]
        public void GuidedAttributeAllocation_포인트를덜쓰거나상한을넘으면거부한다()
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), 33UL);
            flow.SubmitBasicInformation("테스트 타자", PlayerType.Batter, Handedness.Right, Handedness.Right);
            flow.SubmitCreationPosition(PlayerPosition.Shortstop, PitcherRole.Starter);

            Assert.Throws<System.ArgumentException>(() =>
                flow.SubmitCreationAttributes(new[] { 59, 59, 59, 59, 59, 59 }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                flow.SubmitCreationAttributes(new[] { 76, 64, 60, 60, 50, 50 }));
            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.AttributeAllocation));

            Assert.DoesNotThrow(() =>
                flow.SubmitCreationAttributes(new[] { 60, 60, 60, 60, 60, 60 }));
            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.PlayerDetails));
        }

        [Test]
        public void GuidedBatterAttributes_송구와선구안을각각Arm과Mental에연결한다()
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), 441UL);
            flow.SubmitBasicInformation("송구 테스트", PlayerType.Batter, Handedness.Right, Handedness.Right);
            flow.SubmitCreationPosition(PlayerPosition.ThirdBase, PitcherRole.Starter);

            flow.SubmitCreationAttributes(new[] { 55, 55, 55, 55, 65, 75 });

            BatterAttributes attributes = flow.State.BatterAttributes.Value;
            Assert.That(attributes.Mental, Is.EqualTo(55));
            Assert.That(attributes.Arm, Is.EqualTo(75));
            Assert.That(attributes.Bunt, Is.EqualTo(55), "송구가 번트 능력으로 재사용되면 안 됩니다.");
        }

        [Test]
        public void WorldRoster_포지션별능력치를보존한선수원본을실제경기입력에사용한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            NewGameFlow flow = CreatePlayerCard(8877UL);
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();

            CareerState career = flow.Career;
            ScheduledGameState game = career.CurrentLeague.CurrentSeason.Schedule
                .GetNextGameForTeam(career.MyPlayer.CurrentTeamId);
            MatchInput input = new CareerGameRunner(career, configuration.Balance).CreateMatchInput(
                game,
                PlayerGameRole.Bench,
                career.CurrentLeague.CurrentSeason.SeasonId);
            Player matchPlayer = input.AwayRoster.StartingLineup[0].Player.PlayerId == career.MyPlayer.PlayerId
                ? input.HomeRoster.StartingLineup[0].Player
                : input.AwayRoster.StartingLineup[0].Player;
            Player worldPlayer = career.World.GetPlayer(matchPlayer.PlayerId).ToPlayer();
            BatterAttributes value = matchPlayer.BatterAttributes;

            Assert.That(value.Contact, Is.EqualTo(worldPlayer.BatterAttributes.Contact));
            Assert.That(value.Power, Is.EqualTo(worldPlayer.BatterAttributes.Power));
            Assert.That(value.Arm, Is.EqualTo(worldPlayer.BatterAttributes.Arm));
            Assert.That(value.Defense, Is.EqualTo(worldPlayer.BatterAttributes.Defense));
            Assert.That(
                value.Contact == value.Power &&
                value.Power == value.Speed &&
                value.Speed == value.Arm &&
                value.Arm == value.Defense &&
                value.Defense == value.Mental,
                Is.False,
                "NPC 능력치가 단일 OVR로 평탄화되면 안 됩니다.");
            int expectedOverall = FindCompetitorOverall(career, matchPlayer.PlayerId);
            Assert.That(
                new PlayerValueEvaluator(configuration.Balance.PlayerEvaluation)
                    .CalculatePositionValue(matchPlayer),
                Is.EqualTo(expectedOverall));
        }

        [Test]
        public void PreferredPitcherRole_Closer희망을경기불펜역할에전달한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var flow = new NewGameFlow(configuration, 9901UL);
            flow.SubmitBasicInformation("마무리 테스트", PlayerType.Pitcher, Handedness.Right, Handedness.Right);
            flow.SubmitCreationPosition(PlayerPosition.Unknown, PitcherRole.Closer);
            flow.SubmitCreationAttributes(new[] { 60, 60, 60, 60 });
            flow.SubmitPitcherDetails(
                new[] { PitchType.FourSeamFastball, PitchType.Slider, PitchType.Changeup },
                PitchType.FourSeamFastball);
            flow.SubmitMatchSettings(
                BattingApproach.Balanced,
                PitchingApproach.Balanced,
                MatchProgressMode.InstantResult,
                gameSpeed: 2,
                autoSlowOnPlayerEvent: true);
            flow.ConfirmCreation();
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();

            CareerState career = flow.Career;
            ScheduledGameState game = career.CurrentLeague.CurrentSeason.Schedule
                .GetNextGameForTeam(career.MyPlayer.CurrentTeamId);
            MatchInput input = new CareerGameRunner(career, configuration.Balance).CreateMatchInput(
                game,
                PlayerGameRole.ReliefPitcher,
                career.CurrentLeague.CurrentSeason.SeasonId);
            MatchRosterSnapshot roster = input.AwayRoster.TeamId == career.MyPlayer.CurrentTeamId
                ? input.AwayRoster
                : input.HomeRoster;
            PitcherRosterEntry playerEntry = null;
            for (int index = 0; index < roster.Bullpen.Count; index++)
            {
                if (roster.Bullpen[index].Player.PlayerId == career.MyPlayer.PlayerId)
                    playerEntry = roster.Bullpen[index];
            }

            Assert.That(playerEntry, Is.Not.Null);
            Assert.That(playerEntry.Role, Is.EqualTo(PitcherRole.Closer));
        }

        private static int FindCompetitorOverall(CareerState career, int playerId)
        {
            for (int teamIndex = 0; teamIndex < career.CurrentLeague.Teams.Count; teamIndex++)
            {
                TeamState team = career.CurrentLeague.Teams[teamIndex];
                for (int playerIndex = 0; playerIndex < team.RosterCompetitors.Count; playerIndex++)
                {
                    RosterCompetitorState competitor = team.RosterCompetitors[playerIndex];
                    if (competitor.PlayerId == playerId)
                        return competitor.Overall;
                }
            }
            throw new System.InvalidOperationException($"PlayerId {playerId}의 경쟁자 요약을 찾을 수 없습니다.");
        }

        private static NewGameFlow CreatePlayerCard(ulong seed)
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), seed);
            flow.SubmitIdentity("최민석", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(65, 60, 62, 53, 65, 55));
            return flow;
        }
    }
}
