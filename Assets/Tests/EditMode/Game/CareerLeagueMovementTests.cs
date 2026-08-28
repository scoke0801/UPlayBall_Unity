using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.News;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>플레이어의 실제 인접 리그 오퍼와 계약 체결 후 월드 소유권 이전을 검증한다.</summary>
    public sealed class CareerLeagueMovementTests
    {
        [Test]
        public void SignSelectedOffer_적격Minor오퍼를체결하면리그와로스터를원자적으로이동한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(seed: 927006UL, strongPlayer: true);
            CareerSeasonTransitionService transition = OpenThirdOffseasonMarket(career, configuration);
            ContractOffer promotion = FindOffer(transition, ContractOfferChannel.Promotion);
            int previousTeamId = career.MyPlayer.CurrentTeamId;
            LeagueId previousLeagueId = career.MyPlayer.CurrentLeagueId;

            transition.SelectRenewalOffer(promotion.Team.TeamId);
            transition.SignSelectedOffer();

            Assert.That(career.CurrentLeague.LeagueLevel, Is.EqualTo(LeagueLevel.Minor));
            Assert.That(career.MyPlayer.CurrentLeagueId, Is.EqualTo(LeagueId.MinorMain));
            Assert.That(career.CurrentContract.CurrentLeagueId, Is.EqualTo(LeagueId.MinorMain));
            Assert.That(Contains(career.World.GetTeam(previousTeamId), career.MyPlayerId), Is.False);
            Assert.That(Contains(career.World.GetTeam(career.MyPlayer.CurrentTeamId), career.MyPlayerId), Is.True);
            Assert.That(career.World.GetLeague(previousLeagueId).CompletedSeasonSummaries.Count, Is.EqualTo(3));

            PlayerMovementRecord movement = FindLastMyMovement(career);
            Assert.That(movement.MovementType, Is.EqualTo(PlayerMovementType.Promotion));
            Assert.That(movement.PreviousLeagueId, Is.EqualTo(LeagueId.RookieMain));
            Assert.That(movement.TargetLeagueId, Is.EqualTo(LeagueId.MinorMain));
            Assert.That(HasDomainEvent(career, "PlayerPromoted"), Is.True);
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        [Test]
        public void OpenMarket_최소전력미달선수에게가짜승격오퍼를만들지않는다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(seed: 928001UL, strongPlayer: false);

            CareerSeasonTransitionService transition = OpenThirdOffseasonMarket(career, configuration);

            Assert.That(HasOffer(transition, ContractOfferChannel.Promotion), Is.False);
        }

        [Test]
        public void OpenMarket_같은Seed와입력은같은승격오퍼를만든다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState first = CreateCareer(seed: 927006UL, strongPlayer: true);
            CareerState second = CreateCareer(seed: 927006UL, strongPlayer: true);

            CareerSeasonTransitionService firstTransition = OpenThirdOffseasonMarket(first, configuration);
            CareerSeasonTransitionService secondTransition = OpenThirdOffseasonMarket(second, configuration);

            Assert.That(BuildOfferFingerprint(secondTransition), Is.EqualTo(BuildOfferFingerprint(firstTransition)));
        }

        private static CareerState CreateCareer(ulong seed, bool strongPlayer)
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), seed);
            flow.SubmitIdentity("승격 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(strongPlayer
                ? new BatterAttributes(65, 40, 40, 65, 62, 40)
                : new BatterAttributes(40, 40, 40, 40, 40, 40));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static CareerSeasonTransitionService OpenThirdOffseasonMarket(
            CareerState career,
            NewGameConfiguration configuration)
        {
            for (int season = 0; season < 2; season++)
            {
                CompleteToOffseason(career, configuration);
                new CareerSeasonTransitionService(career, configuration.Balance).AdvanceToNextSeason();
            }
            CompleteToOffseason(career, configuration);
            var transition = new CareerSeasonTransitionService(career, configuration.Balance);
            SeasonTransitionStep step = transition.BeginTransition();
            if (step == SeasonTransitionStep.CurrentTeamNegotiation)
                transition.OpenMarket(holdCurrentTeamOffer: true);
            return transition;
        }

        private static void CompleteToOffseason(
            CareerState career,
            NewGameConfiguration configuration)
        {
            var completion = new CareerSeasonAutoCompletionService(
                career,
                configuration.Balance,
                CareerNewsConfiguration.CreateDefault());
            completion.CompleteCurrentPhase();
            completion.CompleteCurrentPhase();
            new CareerGrowthService(career, configuration.Balance).SettleSeasonAndBeginOffseason(
                new SeasonUsageSummary(
                    1d,
                    new[] { new AbilityWeight(PlayerAbility.Contact, 1d) }));
        }

        private static ContractOffer FindOffer(
            CareerSeasonTransitionService transition,
            ContractOfferChannel channel)
        {
            for (int index = 0; index < transition.RenewalOffers.Count; index++)
            {
                ContractOffer offer = transition.RenewalOffers[index];
                if (offer.Channel == channel)
                    return offer;
            }
            throw new AssertionException($"{channel} 오퍼를 찾지 못했습니다.");
        }

        private static bool HasOffer(
            CareerSeasonTransitionService transition,
            ContractOfferChannel channel)
        {
            for (int index = 0; index < transition.RenewalOffers.Count; index++)
            {
                if (transition.RenewalOffers[index].Channel == channel)
                    return true;
            }
            return false;
        }

        private static bool Contains(TeamState team, int playerId)
        {
            for (int index = 0; index < team.RosterPlayerIds.Count; index++)
            {
                if (team.RosterPlayerIds[index] == playerId)
                    return true;
            }
            return false;
        }

        private static bool HasDomainEvent(CareerState career, string eventType)
        {
            for (int index = 0; index < career.World.DomainEvents.Events.Count; index++)
            {
                if (career.World.DomainEvents.Events[index].EventType == eventType)
                    return true;
            }
            return false;
        }

        private static PlayerMovementRecord FindLastMyMovement(CareerState career)
        {
            for (int index = career.World.MovementLedger.Records.Count - 1; index >= 0; index--)
            {
                PlayerMovementRecord movement = career.World.MovementLedger.Records[index];
                if (movement.PlayerId == career.MyPlayerId)
                    return movement;
            }
            throw new AssertionException("내 선수의 이동 기록을 찾지 못했습니다.");
        }

        private static string BuildOfferFingerprint(CareerSeasonTransitionService transition)
        {
            var result = new System.Text.StringBuilder();
            for (int index = 0; index < transition.RenewalOffers.Count; index++)
            {
                ContractOffer offer = transition.RenewalOffers[index];
                result.Append(offer.Team.TeamId).Append(',')
                    .Append((int)offer.Channel).Append(',')
                    .Append(offer.AnnualSalary).Append(',')
                    .Append(offer.OfferScore).Append(';');
            }
            return result.ToString();
        }
    }
}
