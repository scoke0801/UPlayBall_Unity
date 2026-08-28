using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 캐릭터 생성부터 구단 계약과 Rookie League 시작까지의 상태 전이를 소유한다.
    /// </summary>
    public sealed class NewGameFlow
    {
        public const int CurrentSaveVersion = 6;
        public const int MyPlayerId = 1_000_001;

        private readonly NewGameConfiguration _configuration;

        public NewGameFlow(NewGameConfiguration configuration, ulong randomSeed)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Begin(randomSeed);
        }

        public NewGameFlowState State { get; private set; }
        public CareerState Career { get; private set; }
        public string BuildWarning { get; private set; } = string.Empty;

        /// <summary>
        /// 기존 draft와 확정 커리어를 버리고 지정 Seed로 새 흐름을 시작한다.
        /// </summary>
        public void Begin(ulong randomSeed)
        {
            State = new NewGameFlowState
            {
                Step = NewGameStep.Identity,
                PrimaryPosition = PlayerPosition.Unknown,
                BattingHand = Handedness.Right,
                ThrowingHand = Handedness.Right,
                RandomSeed = randomSeed
            };
            Career = null;
            BuildWarning = string.Empty;
        }

        /// <summary>
        /// 선수 이름과 국적을 확정한다.
        /// </summary>
        public void SubmitIdentity(string playerName, string nationality)
        {
            RequireStep(NewGameStep.Identity);
            if (string.IsNullOrWhiteSpace(playerName))
                throw new ArgumentException("선수 이름을 입력해 주세요.", nameof(playerName));
            if (string.IsNullOrWhiteSpace(nationality))
                throw new ArgumentException("국적을 입력해 주세요.", nameof(nationality));

            State.PlayerName = playerName.Trim();
            State.Nationality = nationality.Trim();
            State.Step = NewGameStep.PlayerType;
        }

        /// <summary>
        /// 타자 또는 투수를 선택하고 포지션 선택 단계로 이동한다.
        /// </summary>
        public void SelectPlayerType(PlayerType playerType)
        {
            RequireStep(NewGameStep.PlayerType);
            State.PlayerType = playerType;
            State.PrimaryPosition = PlayerPosition.Unknown;
            State.BatterAttributes = null;
            State.PitcherAttributes = null;
            State.Step = NewGameStep.Position;
        }

        /// <summary>
        /// 선수 유형과 일치하는 주 포지션 또는 투수 역할을 선택한다.
        /// </summary>
        public void SelectPosition(PlayerPosition position)
        {
            RequireStep(NewGameStep.Position);
            if (position == PlayerPosition.Unknown)
                throw new ArgumentException("포지션을 선택해 주세요.", nameof(position));

            bool isPitcherPosition = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            if (State.PlayerType == PlayerType.Pitcher != isPitcherPosition)
                throw new ArgumentException("선수 유형과 포지션이 일치하지 않습니다.", nameof(position));

            State.PrimaryPosition = position;
            State.Step = NewGameStep.Handedness;
        }

        /// <summary>
        /// 타격·투구 손을 확정한다. 투구 손은 Switch를 허용하지 않는다.
        /// </summary>
        public void SelectHandedness(Handedness battingHand, Handedness throwingHand)
        {
            RequireStep(NewGameStep.Handedness);
            if (throwingHand == Handedness.Switch)
                throw new ArgumentException("투구 손은 Switch일 수 없습니다.", nameof(throwingHand));

            State.BattingHand = battingHand;
            State.ThrowingHand = throwingHand;
            State.Step = NewGameStep.AttributeAllocation;
        }

        /// <summary>
        /// 타자 능력치 6개를 검증하고 무소속 선수 카드 단계로 이동한다.
        /// </summary>
        public void SubmitBatterAttributes(BatterAttributes attributes)
        {
            RequireStep(NewGameStep.AttributeAllocation);
            if (State.PlayerType != PlayerType.Batter)
                throw new InvalidOperationException("타자 생성에서만 타자 능력치를 배분할 수 있습니다.");

            AttributeAllocation.Validate(
                _configuration.Balance.CharacterCreation,
                attributes.Contact,
                attributes.Power,
                attributes.Speed,
                attributes.Bunt,
                attributes.Defense,
                attributes.Mental);
            State.BatterAttributes = attributes;
            State.PitcherAttributes = null;
            CompletePlayerCard();
        }

        /// <summary>
        /// 투수 능력치 6개를 검증하고 무소속 선수 카드 단계로 이동한다.
        /// </summary>
        public void SubmitPitcherAttributes(PitcherAttributes attributes)
        {
            RequireStep(NewGameStep.AttributeAllocation);
            if (State.PlayerType != PlayerType.Pitcher)
                throw new InvalidOperationException("투수 생성에서만 투수 능력치를 배분할 수 있습니다.");

            AttributeAllocation.Validate(
                _configuration.Balance.CharacterCreation,
                attributes.Stamina,
                attributes.Velocity,
                attributes.Stuff,
                attributes.Breaking,
                attributes.Control,
                attributes.Mental);
            State.PitcherAttributes = attributes;
            State.BatterAttributes = null;
            CompletePlayerCard();
        }

        /// <summary>
        /// 같은 Seed로 동일한 Rookie League와 계약 오퍼를 생성한다.
        /// </summary>
        public void GenerateOffers()
        {
            RequireStep(NewGameStep.PlayerCard);
            Player player = CreatePlayer();
            var setup = new NewGameSetup(
                _configuration.Balance.ContractOffer,
                _configuration.Balance.TeamGeneration,
                _configuration.Balance.PlayerEvaluation,
                new Pcg32Random(State.RandomSeed));
            State.SetupResult = setup.GenerateLeagueAndOffers(
                player,
                _configuration.TeamCount,
                _configuration.Archetypes,
                _configuration.TeamIdentities,
                _configuration.PlayerNamePool);
            State.SelectedOffer = null;
            State.Step = NewGameStep.ContractOffers;
        }

        /// <summary>
        /// 표시된 오퍼 중 계약 대상으로 삼을 구단을 선택한다.
        /// </summary>
        public void SelectOffer(int teamId)
        {
            RequireStep(NewGameStep.ContractOffers);
            for (int index = 0; index < State.SetupResult.Offers.Length; index++)
            {
                ContractOffer offer = State.SetupResult.Offers[index];
                if (offer.Team.TeamId != teamId)
                    continue;

                State.SelectedOffer = offer;
                return;
            }

            throw new ArgumentException("선택할 수 없는 계약 오퍼입니다.", nameof(teamId));
        }

        /// <summary>
        /// 선택한 오퍼를 계약으로 확정하고 세이브 가능한 커리어 상태를 만든다.
        /// </summary>
        public void SignSelectedOffer()
        {
            RequireStep(NewGameStep.ContractOffers);
            if (!State.SelectedOffer.HasValue)
                throw new InvalidOperationException("먼저 계약할 구단을 선택해 주세요.");

            ContractOffer offer = State.SelectedOffer.Value;
            TeamState[] teams = CreateTeamStates(State.SetupResult.Teams);
            var season = new SeasonState(
                CurrentSaveVersion,
                seasonId: 1,
                _configuration.FirstSeasonYear,
                LeagueLevel.Rookie);
            var league = new LeagueState(
                CurrentSaveVersion,
                _configuration.FirstSeasonYear,
                State.RandomSeed,
                teams,
                season);
            Player player = CreatePlayer();
            var playerState = new PlayerState(
                CurrentSaveVersion,
                player.PlayerId,
                player.Name,
                player.Nationality,
                _configuration.StartingAge,
                player.PrimaryPosition,
                player.BattingHand,
                player.ThrowingHand,
                player.BatterAttributes,
                player.PitcherAttributes,
                offer.Team.TeamId);
            playerState.AttachGrowthState(
                new PlayerGrowthFactory(_configuration.Balance.Growth).Create(
                    player,
                    _configuration.StartingAge,
                    _configuration.Balance.CareerSeason.InitialCondition));
            var contract = new PlayerContractState(
                CurrentSaveVersion,
                offer.Team.TeamId,
                _configuration.FirstSeasonYear,
                offer.ContractYears,
                offer.SigningBonus,
                offer.AnnualSalary,
                offer.ExpectedRole);

            Career = new CareerState(
                CurrentSaveVersion,
                playerState,
                league,
                contract,
                availableMoney: offer.SigningBonus);
            State.Step = NewGameStep.ContractComplete;
        }

        /// <summary>
        /// 계약이 끝난 커리어를 Rookie League 정규 시즌 상태로 전환한다.
        /// </summary>
        public void StartRookieSeason()
        {
            RequireStep(NewGameStep.ContractComplete);
            int teamCount = Career.League.Teams.Count;
            var teamIds = new int[teamCount];
            var teamRecords = new TeamSeasonRecordState[teamCount];
            for (int index = 0; index < teamCount; index++)
            {
                int teamId = Career.League.Teams[index].TeamId;
                teamIds[index] = teamId;
                teamRecords[index] = new TeamSeasonRecordState(
                    teamId,
                    DeterministicSeed.Derive(State.RandomSeed, 0x544945425245414BUL ^ (uint)teamId));
            }

            ulong scheduleSeed = DeterministicSeed.Derive(State.RandomSeed, 0x5343484544554C45UL);
            var scheduleGenerator = new SeasonScheduleGenerator(new Pcg32Random(scheduleSeed));
            ScheduledGameDefinition[] definitions = scheduleGenerator.Generate(
                teamIds,
                _configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam);
            var games = new ScheduledGameState[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                ScheduledGameDefinition definition = definitions[index];
                ulong streamId = ((ulong)Career.League.CurrentSeason.SeasonId << 32) |
                                 (uint)definition.GameId;
                games[index] = new ScheduledGameState(
                    definition.GameId,
                    definition.Round,
                    DeterministicSeed.Derive(State.RandomSeed, streamId),
                    definition.AwayTeamId,
                    definition.HomeTeamId);
            }

            Career.MyPlayer.InitializeSeasonStatus(
                _configuration.Balance.CareerSeason.InitialCondition,
                _configuration.Balance.CareerSeason.InitialManagerEvaluation);
            Career.League.CurrentSeason.StartRegularSeason(
                new SeasonScheduleState(games),
                teamRecords,
                new PlayerSeasonStatisticsState(),
                Career.MyPlayer);
            Career.League.CurrentSeason.SnapshotRookieEligibility(
                Career.League.Teams,
                Career.MyPlayer,
                _configuration.Balance.SeasonAwards,
                myCareerPlateAppearances: 0,
                myCareerPitchingOuts: 0,
                myRegisteredSeasons: 0);
            State.Step = NewGameStep.Completed;
        }

        /// <summary>
        /// 계약 확정 전 단계에서만 한 단계 뒤로 이동하고 이후 산출물을 무효화한다.
        /// </summary>
        public bool GoBack()
        {
            switch (State.Step)
            {
                case NewGameStep.PlayerType:
                    State.Step = NewGameStep.Identity;
                    break;
                case NewGameStep.Position:
                    State.Step = NewGameStep.PlayerType;
                    break;
                case NewGameStep.Handedness:
                    State.Step = NewGameStep.Position;
                    break;
                case NewGameStep.AttributeAllocation:
                    State.Step = NewGameStep.Handedness;
                    break;
                case NewGameStep.PlayerCard:
                    State.Step = NewGameStep.AttributeAllocation;
                    break;
                case NewGameStep.ContractOffers:
                    State.SetupResult = null;
                    State.SelectedOffer = null;
                    State.Step = NewGameStep.PlayerCard;
                    break;
                default:
                    return false;
            }

            return true;
        }

        private void CompletePlayerCard()
        {
            Player player = CreatePlayer();
            BuildWarning = PlayerBuildAdvisor.GetWarning(player);
            State.Step = NewGameStep.PlayerCard;
        }

        private Player CreatePlayer()
        {
            if (!State.IsCharacterReady())
                throw new InvalidOperationException("선수 카드 생성에 필요한 선택이 끝나지 않았습니다.");

            return new Player(
                MyPlayerId,
                State.PlayerName,
                State.PrimaryPosition,
                State.BattingHand,
                State.ThrowingHand,
                State.BatterAttributes ?? default,
                State.PitcherAttributes ?? default,
                secondaryPositions: null,
                nationality: State.Nationality);
        }

        private static TeamState[] CreateTeamStates(GeneratedTeam[] generatedTeams)
        {
            var result = new TeamState[generatedTeams.Length];
            for (int teamIndex = 0; teamIndex < generatedTeams.Length; teamIndex++)
            {
                GeneratedTeam team = generatedTeams[teamIndex];
                var positionNeeds = new int[(int)PlayerPosition.ReliefPitcher + 1];
                var competitors = new List<RosterCompetitorState>();
                for (int rawPosition = (int)PlayerPosition.Catcher;
                     rawPosition <= (int)PlayerPosition.ReliefPitcher;
                     rawPosition++)
                {
                    var position = (PlayerPosition)rawPosition;
                    positionNeeds[rawPosition] = team.GetPositionNeed(position);
                    IReadOnlyList<RosterCompetitor> positionCompetitors = team.GetPositionCompetitors(position);
                    for (int competitorIndex = 0; competitorIndex < positionCompetitors.Count; competitorIndex++)
                    {
                        RosterCompetitor competitor = positionCompetitors[competitorIndex];
                        competitors.Add(new RosterCompetitorState(
                            competitor.PlayerId,
                            competitor.Name,
                            competitor.Position,
                            competitor.Overall));
                    }
                }

                result[teamIndex] = new TeamState(
                    CurrentSaveVersion,
                    team.TeamId,
                    team.Name,
                    team.Archetype,
                    team.PrimaryColor,
                    positionNeeds,
                    competitors.ToArray());
            }

            return result;
        }

        private void RequireStep(NewGameStep expected)
        {
            if (State.Step != expected)
            {
                throw new InvalidOperationException(
                    $"현재 단계({State.Step})에서는 {expected} 작업을 수행할 수 없습니다.");
            }
        }
    }
}
