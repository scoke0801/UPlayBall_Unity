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
        public void BeginTransition_잔여계약의상위리그조항이발동하면유지와승격오퍼를함께제시한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(seed: 927006UL, strongPlayer: true, initialOfferIndex: 1);
            ReplaceWithLongTermContract(career, configuration.FirstSeasonYear);
            int contractId = career.CurrentContract.ContractId;
            CareerSeasonTransitionService transition = OpenThirdOffseasonMarket(career, configuration);

            Assert.That(transition.Step, Is.EqualTo(SeasonTransitionStep.ContractOffers));
            ContractOffer continuation = FindOffer(transition, ContractOfferChannel.ContractContinuation);
            Assert.That(HasOffer(transition, ContractOfferChannel.Promotion), Is.True);
            transition.SelectRenewalOffer(continuation.Team.TeamId);
            transition.SignSelectedOffer();
            Assert.That(career.CurrentContract.ContractId, Is.EqualTo(contractId));
            Assert.That(career.CurrentContract.SignedYear, Is.EqualTo(configuration.FirstSeasonYear));
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        [Test]
        public void SignSelectedOffer_잔여계약상위리그이적은보상금과조항발동이력을남긴다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(seed: 927006UL, strongPlayer: true, initialOfferIndex: 1);
            ReplaceWithLongTermContract(career, configuration.FirstSeasonYear);
            long expectedCompensation = career.CurrentContract.UpperLeagueReleaseCompensation;
            int previousContractId = career.CurrentContract.ContractId;
            CareerSeasonTransitionService transition = OpenThirdOffseasonMarket(career, configuration);
            ContractOffer promotion = FindOffer(transition, ContractOfferChannel.Promotion);

            transition.SelectRenewalOffer(promotion.Team.TeamId);
            transition.SignSelectedOffer();

            PlayerMovementRecord movement = FindLastMyMovement(career);
            Assert.That(career.CurrentContract.ContractId, Is.Not.EqualTo(previousContractId));
            Assert.That(movement.MovementType, Is.EqualTo(PlayerMovementType.Promotion));
            Assert.That(movement.TransferCompensation, Is.EqualTo(expectedCompensation));
            Assert.That(HasDomainEvent(career, "UpperLeagueReleaseClauseActivated"), Is.True);
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        [Test]
        public void SignSelectedOffer_적격상위리그오퍼를체결하면리그와로스터를원자적으로이동한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(seed: 927006UL, strongPlayer: true, initialOfferIndex: 1);
            CareerSeasonTransitionService transition = OpenThirdOffseasonMarket(career, configuration);
            ContractOffer promotion = FindOffer(transition, ContractOfferChannel.Promotion);
            LeagueLevel targetLeagueLevel = transition.GetPlannedLeagueLevel(promotion.Team.TeamId);
            int previousTeamId = career.MyPlayer.CurrentTeamId;
            LeagueId previousLeagueId = career.MyPlayer.CurrentLeagueId;
            Assert.That(career.CurrentContract.CurrentLeagueId, Is.EqualTo(previousLeagueId));

            transition.SelectRenewalOffer(promotion.Team.TeamId);
            transition.SignSelectedOffer();

            Assert.That(career.CurrentLeague.LeagueLevel, Is.EqualTo(targetLeagueLevel));
            Assert.That(career.MyPlayer.CurrentLeagueId, Is.EqualTo(career.CurrentLeague.LeagueId));
            Assert.That(career.CurrentContract.CurrentLeagueId, Is.EqualTo(career.CurrentLeague.LeagueId));
            Assert.That(
                career.CurrentContract.HasUpperLeagueReleaseClause,
                Is.EqualTo(targetLeagueLevel <= LeagueLevel.Minor));
            Assert.That(career.CurrentContract.HasRelegationTransferRequestClause, Is.True);
            Assert.That(Contains(career.World.GetTeam(previousTeamId), career.MyPlayerId), Is.False);
            Assert.That(Contains(career.World.GetTeam(career.MyPlayer.CurrentTeamId), career.MyPlayerId), Is.True);
            Assert.That(career.World.GetLeague(previousLeagueId).CompletedSeasonSummaries.Count, Is.EqualTo(3));

            PlayerMovementRecord movement = FindLastMyMovement(career);
            Assert.That(
                movement.MovementType,
                Is.EqualTo(PlayerMovementType.Promotion),
                BuildMovementFingerprint(career));
            Assert.That(movement.PreviousLeagueId, Is.EqualTo(previousLeagueId));
            Assert.That(movement.TargetLeagueId, Is.EqualTo(career.CurrentLeague.LeagueId));
            Assert.That(HasDomainEvent(career, "PlayerPromoted"), Is.True);
            Assert.That(HasDomainEvent(career, "UpperLeagueInterestConfirmed"), Is.True);
            Assert.That(HasDomainEvent(career, "CrossLeagueContractSigned"), Is.True);
            Assert.That(HasTransaction(career, $"first_league_reach_{(int)targetLeagueLevel}"), Is.True);
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        [Test]
        public void OpenMarket_최소전력미달선수에게가짜승격오퍼를만들지않는다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(seed: 928001UL, strongPlayer: false, initialOfferIndex: 1);

            CareerSeasonTransitionService transition = OpenThirdOffseasonMarket(career, configuration);

            Assert.That(HasOffer(transition, ContractOfferChannel.Promotion), Is.False);
        }

        [Test]
        public void OpenMarket_정식오퍼와Rookie테스트두곳을모두통과하지못하면미계약종료를요구한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(seed: 928001UL, strongPlayer: false);
            for (int ability = 0; ability < (int)PlayerAbility.Count; ability++)
                career.MyPlayer.GrowthState.ApplyBaseAbilityChange((PlayerAbility)ability, -100);

            CareerSeasonTransitionService transition = OpenThirdOffseasonMarket(
                career,
                configuration,
                holdCurrentTeamOffer: false);

            Assert.That(transition.Step, Is.EqualTo(SeasonTransitionStep.ContractOffers));
            Assert.That(
                transition.RenewalOffers.Count,
                Is.EqualTo(0),
                BuildOfferFingerprint(transition));
            Assert.That(transition.RookieTryoutAttemptCount, Is.EqualTo(2));
            Assert.That(transition.IsUnsignedRetirementRequired, Is.True);
        }

        [Test]
        public void RookieTryoutEvaluator_같은수요에서도경쟁력이낮은선수만탈락한다()
        {
            var evaluator = new RookieTryoutEvaluator(passingScore: 55d);
            double strongScore = evaluator.CalculateScore(new RookieTryoutEvaluationInput(
                playerOverall: 58,
                positionNeed: 70,
                strongestCompetitorOverall: 55,
                ageAndPotential: 75d,
                durability: 75d,
                recentPerformance: 60d,
                scoutAdjustment: 0d));
            double weakScore = evaluator.CalculateScore(new RookieTryoutEvaluationInput(
                playerOverall: 30,
                positionNeed: 70,
                strongestCompetitorOverall: 55,
                ageAndPotential: 35d,
                durability: 45d,
                recentPerformance: 20d,
                scoutAdjustment: 0d));

            Assert.That(evaluator.IsPassed(strongScore), Is.True);
            Assert.That(evaluator.IsPassed(weakScore), Is.False);
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

        private static CareerState CreateCareer(
            ulong seed,
            bool strongPlayer,
            int initialOfferIndex = 0)
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), seed);
            flow.SubmitIdentity("승격 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(strongPlayer
                ? new BatterAttributes(65, 60, 50, 70, 65, 50)
                : new BatterAttributes(50, 50, 50, 50, 50, 50));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[initialOfferIndex].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static void ReplaceWithLongTermContract(CareerState career, int signedYear)
        {
            PlayerContractState current = career.CurrentContract;
            career.RenewContract(
                new PlayerContractState(
                    NewGameFlow.CurrentSaveVersion,
                    current.TeamId,
                    signedYear,
                    contractYears: 5,
                    signingBonus: 0L,
                    current.AnnualSalary,
                    current.ExpectedRole,
                    hasUpperLeagueReleaseClause: true,
                    current.UpperLeagueReleaseCompensation,
                    hasRelegationTransferRequestClause: true),
                targetLeagueId: career.MyPlayer.CurrentLeagueId);
        }

        private static CareerSeasonTransitionService OpenThirdOffseasonMarket(
            CareerState career,
            NewGameConfiguration configuration,
            bool holdCurrentTeamOffer = true)
        {
            for (int season = 0; season < 2; season++)
            {
                CompleteToOffseason(career, configuration);
                var coveredTransition = new CareerSeasonTransitionService(career, configuration.Balance);
                SeasonTransitionStep coveredStep = coveredTransition.BeginTransition();
                if (coveredStep == SeasonTransitionStep.ContractOffers)
                {
                    ContractOffer continuation = FindOffer(
                        coveredTransition,
                        ContractOfferChannel.ContractContinuation);
                    coveredTransition.SelectRenewalOffer(continuation.Team.TeamId);
                    coveredTransition.SignSelectedOffer();
                }
                else
                {
                    Assert.That(coveredStep, Is.EqualTo(SeasonTransitionStep.Completed));
                }
            }
            CompleteToOffseason(career, configuration);
            var transition = new CareerSeasonTransitionService(career, configuration.Balance);
            SeasonTransitionStep step = transition.BeginTransition();
            if (step == SeasonTransitionStep.CurrentTeamNegotiation)
                transition.OpenMarket(holdCurrentTeamOffer);
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

        private static bool HasTransaction(CareerState career, string sourceId)
        {
            for (int index = 0; index < career.Economy.Transactions.Count; index++)
            {
                if (career.Economy.Transactions[index].SourceId == sourceId)
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

        private static string BuildMovementFingerprint(CareerState career)
        {
            var result = new System.Text.StringBuilder();
            for (int index = 0; index < career.World.MovementLedger.Records.Count; index++)
            {
                PlayerMovementRecord movement = career.World.MovementLedger.Records[index];
                if (movement.PlayerId != career.MyPlayerId)
                    continue;
                result.Append(movement.SeasonId).Append(':')
                    .Append(movement.MovementType).Append(':')
                    .Append(movement.PreviousLeagueId).Append("->")
                    .Append(movement.TargetLeagueId).Append(':')
                    .Append(movement.PreviousTeamId).Append("->")
                    .Append(movement.TargetTeamId).Append(';');
            }
            return result.ToString();
        }
    }
}
