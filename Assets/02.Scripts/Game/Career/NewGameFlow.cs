using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Core.Rules;
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
        public const int CurrentSaveVersion = 16;
        public const int MyPlayerId = 1_000_001;

        private readonly NewGameConfiguration _configuration;
        private CareerBakedContent _bakedContent;

        public NewGameFlow(NewGameConfiguration configuration, ulong randomSeed)
            : this(configuration, randomSeed, randomSeed)
        {
        }

        public NewGameFlow(NewGameConfiguration configuration, ulong randomSeed, ulong worldHistorySeed)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Begin(randomSeed, worldHistorySeed);
        }

        public NewGameFlowState State { get; private set; }
        public CareerState Career { get; private set; }
        public string BuildWarning { get; private set; } = string.Empty;

        public CareerCreationRules CareerCreationRules => _configuration.CareerCreationRules;

        /// <summary>
        /// 기존 draft와 확정 커리어를 버리고 지정 Seed로 새 흐름을 시작한다.
        /// </summary>
        public void Begin(ulong randomSeed)
        {
            Begin(randomSeed, randomSeed);
        }

        /// <summary>배경 역사 Seed를 따로 지정해 시작한다. 미리 구운 월드를 쓰는 경로다.</summary>
        public void Begin(ulong randomSeed, ulong worldHistorySeed)
        {
            State = new NewGameFlowState
            {
                Step = NewGameStep.Identity,
                PrimaryPosition = PlayerPosition.Unknown,
                BattingHand = Handedness.Right,
                ThrowingHand = Handedness.Right,
                RandomSeed = randomSeed,
                WorldHistorySeed = worldHistorySeed,
                Draft = new CareerCreationDraft()
            };
            Career = null;
            _bakedContent = null;
            BuildWarning = string.Empty;
        }

        /// <summary>
        /// 1단계의 이름·선수 유형·투타를 한 번에 검증하고 draft에 보관한다.
        /// </summary>
        public void SubmitBasicInformation(
            string playerName,
            PlayerType playerType,
            Handedness battingHand,
            Handedness throwingHand)
        {
            RequireStep(NewGameStep.Identity);
            string trimmedName = ValidatePlayerName(playerName);
            if (!Enum.IsDefined(typeof(PlayerType), playerType))
                throw new ArgumentOutOfRangeException(nameof(playerType));
            if (!Enum.IsDefined(typeof(Handedness), battingHand))
                throw new ArgumentOutOfRangeException(nameof(battingHand));
            if (!Enum.IsDefined(typeof(Handedness), throwingHand))
                throw new ArgumentOutOfRangeException(nameof(throwingHand));
            if (throwingHand == Handedness.Switch)
                throw new ArgumentException("투구 손은 Switch일 수 없습니다.", nameof(throwingHand));

            State.PlayerName = trimmedName;
            State.Nationality = "대한민국";
            State.PlayerType = playerType;
            State.BattingHand = battingHand;
            State.ThrowingHand = throwingHand;
            State.PrimaryPosition = PlayerPosition.Unknown;
            State.BatterAttributes = null;
            State.PitcherAttributes = null;
            State.Draft.PlayerName = trimmedName;
            State.Draft.PlayerType = playerType;
            State.Draft.BatHand = battingHand;
            State.Draft.ThrowHand = throwingHand;
            State.Draft.FieldPosition = PlayerPosition.Unknown;
            State.Draft.SetInitialAttributes(Array.Empty<int>());
            State.Draft.SetPitchRepertoire(Array.Empty<PitchRepertoireEntry>());
            State.UsesGuidedCreation = true;
            State.Step = NewGameStep.Position;
        }

        /// <summary>
        /// 2단계의 타자 포지션 또는 투수 희망 보직을 확정한다.
        /// </summary>
        public void SubmitCreationPosition(PlayerPosition batterPosition, PitcherRole preferredPitcherRole)
        {
            RequireStep(NewGameStep.Position);
            if (State.PlayerType == PlayerType.Pitcher)
            {
                if (preferredPitcherRole is PitcherRole.Swingman ||
                    !Enum.IsDefined(typeof(PitcherRole), preferredPitcherRole))
                {
                    throw new ArgumentException("선택할 수 없는 희망 보직입니다.", nameof(preferredPitcherRole));
                }

                State.Draft.PreferredPitcherRole = preferredPitcherRole;
                State.PrimaryPosition = preferredPitcherRole == PitcherRole.Starter
                    ? PlayerPosition.StartingPitcher
                    : PlayerPosition.ReliefPitcher;
                State.Draft.FieldPosition = State.PrimaryPosition;
            }
            else
            {
                bool isBatterPosition = batterPosition is >= PlayerPosition.Catcher and <= PlayerPosition.DesignatedHitter;
                if (!isBatterPosition)
                    throw new ArgumentException("타자의 수비 포지션을 선택해 주세요.", nameof(batterPosition));

                State.PrimaryPosition = batterPosition;
                State.Draft.FieldPosition = batterPosition;
            }

            State.Step = NewGameStep.AttributeAllocation;
        }

        /// <summary>
        /// 3단계 능력치를 전부 사용했는지 확인하고 현재 시뮬레이션 능력치로 변환한다.
        /// </summary>
        public void SubmitCreationAttributes(int[] values)
        {
            RequireStep(NewGameStep.AttributeAllocation);
            if (!State.PlayerType.HasValue)
                throw new InvalidOperationException("선수 유형이 선택되지 않았습니다.");

            CareerAttributeAllocationRule rule = _configuration.CareerCreationRules.GetRule(State.PlayerType.Value);
            rule.ValidateComplete(values);
            State.Draft.SetInitialAttributes(values);

            if (State.PlayerType == PlayerType.Batter)
            {
                // 생성 화면의 Eye는 경기 모델의 Mental로, Arm은 같은 이름의 수비 능력으로 연결한다.
                State.BatterAttributes = new BatterAttributes(
                    values[0], values[1], values[3], values[5], values[4], values[2]);
                State.PitcherAttributes = null;
            }
            else
            {
                // 4축 모델을 기존 6축 입력에 연결하되 구위→구속, 제구→위기관리 대체값을 사용한다.
                State.PitcherAttributes = new PitcherAttributes(
                    values[3], values[0], values[0], values[2], values[1], values[1]);
                State.BatterAttributes = null;
            }

            State.Step = NewGameStep.PlayerDetails;
        }

        /// <summary>4단계에서 타자의 기본 스타일을 확정한다.</summary>
        public void SubmitBatterDetails(BatterStyle style)
        {
            RequireStep(NewGameStep.PlayerDetails);
            if (State.PlayerType != PlayerType.Batter)
                throw new InvalidOperationException("타자 생성에서만 타격 스타일을 선택할 수 있습니다.");
            if (!Enum.IsDefined(typeof(BatterStyle), style))
                throw new ArgumentOutOfRangeException(nameof(style));
            State.Draft.BatterStyle = style;
            State.Draft.SetPitchRepertoire(Array.Empty<PitchRepertoireEntry>());
            State.Step = NewGameStep.MatchSettings;
        }

        /// <summary>4단계에서 포심을 포함한 3개 구종과 주무기를 확정한다.</summary>
        public void SubmitPitcherDetails(PitchType[] pitchTypes, PitchType primaryPitch)
        {
            RequireStep(NewGameStep.PlayerDetails);
            if (State.PlayerType != PlayerType.Pitcher)
                throw new InvalidOperationException("투수 생성에서만 구종을 선택할 수 있습니다.");
            if (pitchTypes == null || pitchTypes.Length != 3)
                throw new ArgumentException("포심을 포함해 정확히 3개 구종을 선택해 주세요.", nameof(pitchTypes));

            bool hasFourSeam = false;
            bool hasPrimary = false;
            for (int index = 0; index < pitchTypes.Length; index++)
            {
                if (!Enum.IsDefined(typeof(PitchType), pitchTypes[index]))
                    throw new ArgumentException("선택할 수 없는 구종입니다.", nameof(pitchTypes));
                for (int previous = 0; previous < index; previous++)
                {
                    if (pitchTypes[previous] == pitchTypes[index])
                        throw new ArgumentException("같은 구종을 두 번 선택할 수 없습니다.", nameof(pitchTypes));
                }
                hasFourSeam |= pitchTypes[index] == PitchType.FourSeamFastball;
                hasPrimary |= pitchTypes[index] == primaryPitch;
            }
            if (!hasFourSeam)
                throw new ArgumentException("모든 투수는 포심 패스트볼을 기본 보유해야 합니다.", nameof(pitchTypes));
            if (!hasPrimary)
                throw new ArgumentException("선택한 구종 중 하나를 주무기로 지정해 주세요.", nameof(primaryPitch));

            var entries = new PitchRepertoireEntry[pitchTypes.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                bool isPrimary = pitchTypes[index] == primaryPitch;
                entries[index] = new PitchRepertoireEntry(pitchTypes[index], isPrimary ? 55 : 45, isPrimary);
            }
            State.Draft.SetPitchRepertoire(entries);
            State.Step = NewGameStep.MatchSettings;
        }

        /// <summary>5단계 경기 운영 설정을 확정하고 최종 확인으로 이동한다.</summary>
        public void SubmitMatchSettings(
            BattingApproach battingApproach,
            PitchingApproach pitchingApproach,
            MatchProgressMode matchProgressMode,
            int gameSpeed,
            bool autoSlowOnPlayerEvent)
        {
            RequireStep(NewGameStep.MatchSettings);
            State.Draft.GameSettings = new CareerGameSettings(
                battingApproach,
                pitchingApproach,
                matchProgressMode,
                gameSpeed,
                autoSlowOnPlayerEvent);
            State.Step = NewGameStep.FinalConfirmation;
        }

        /// <summary>최종 확인을 완료해 기존 구단 오퍼 생성 단계로 연결한다.</summary>
        public void ConfirmCreation()
        {
            RequireStep(NewGameStep.FinalConfirmation);
            CompletePlayerCard();
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
            State.Draft.PlayerName = State.PlayerName;
            State.Step = NewGameStep.PlayerType;
        }

        /// <summary>
        /// 타자 또는 투수를 선택하고 포지션 선택 단계로 이동한다.
        /// </summary>
        public void SelectPlayerType(PlayerType playerType)
        {
            RequireStep(NewGameStep.PlayerType);
            State.PlayerType = playerType;
            State.Draft.PlayerType = playerType;
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
            State.Draft.FieldPosition = position;
            if (isPitcherPosition)
            {
                State.Draft.PreferredPitcherRole = position == PlayerPosition.StartingPitcher
                    ? PitcherRole.Starter
                    : PitcherRole.MiddleRelief;
            }
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
            State.Draft.BatHand = battingHand;
            State.Draft.ThrowHand = throwingHand;
            State.Step = NewGameStep.AttributeAllocation;
        }

        /// <summary>
        /// 구 생성 호출을 단일 CareerCreationRules 입력으로 변환해 무소속 선수 카드 단계로 이동한다.
        /// </summary>
        public void SubmitBatterAttributes(BatterAttributes attributes)
        {
            RequireStep(NewGameStep.AttributeAllocation);
            if (State.PlayerType != PlayerType.Batter)
                throw new InvalidOperationException("타자 생성에서만 타자 능력치를 배분할 수 있습니다.");

            int[] values =
            {
                attributes.Contact,
                attributes.Power,
                attributes.Mental,
                attributes.Speed,
                attributes.Defense,
                attributes.Arm
            };
            CareerAttributeAllocationRule rule = _configuration.CareerCreationRules.Batter;
            if (!IsCompleteAllocation(rule, values))
            {
                values = rule.CreateWeightedValues(
                    Math.Max(1, attributes.Contact - rule.BaseValue + 1),
                    Math.Max(1, attributes.Power - rule.BaseValue + 1),
                    Math.Max(1, attributes.Mental - rule.BaseValue + 1),
                    Math.Max(1, attributes.Speed - rule.BaseValue + 1),
                    Math.Max(1, attributes.Defense - rule.BaseValue + 1),
                    Math.Max(1, attributes.Arm - rule.BaseValue + 1));
            }
            SubmitCreationAttributes(values);
            CompletePlayerCard();
        }

        private static bool IsCompleteAllocation(CareerAttributeAllocationRule rule, int[] values)
        {
            int spent = 0;
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] < rule.BaseValue || values[index] > rule.MaxValue)
                    return false;
                spent += values[index] - rule.BaseValue;
            }
            return spent == rule.BonusPoints;
        }

        /// <summary>
        /// 투수 능력치 6개를 검증하고 무소속 선수 카드 단계로 이동한다.
        /// </summary>
        public void SubmitPitcherAttributes(PitcherAttributes attributes)
        {
            RequireStep(NewGameStep.AttributeAllocation);
            if (State.PlayerType != PlayerType.Pitcher)
                throw new InvalidOperationException("투수 생성에서만 투수 능력치를 배분할 수 있습니다.");

            CareerAttributeAllocationRule rule = _configuration.CareerCreationRules.Pitcher;
            int[] values = rule.CreateWeightedValues(
                Math.Max(1, attributes.Stuff - rule.BaseValue + 1),
                Math.Max(1, attributes.Control - rule.BaseValue + 1),
                Math.Max(1, attributes.Breaking - rule.BaseValue + 1),
                Math.Max(1, attributes.Stamina - rule.BaseValue + 1));
            SubmitCreationAttributes(values);
            CompletePlayerCard();
        }

        /// <summary>
        /// 같은 Seed로 동일한 Rookie League와 계약 오퍼를 생성한다.
        /// </summary>
        public void GenerateOffers()
        {
            RequireStep(NewGameStep.PlayerCard);
            Player player = CreatePlayer();
            if (_configuration.ContentSource == NewGameContentSource.BakedHistorical)
            {
                _bakedContent = _configuration.BakedContentProvider.Load(
                    new CareerBakedContentRequest(_configuration.WorldRecordMode, State.WorldHistorySeed));
                if (_bakedContent == null)
                    throw new InvalidOperationException("Baked Content Provider가 null 월드를 반환했습니다.");
                if (_bakedContent.WorldHistory.RecordMode != _configuration.WorldRecordMode ||
                    _bakedContent.WorldHistory.WorldHistorySeed != State.WorldHistorySeed)
                {
                    throw new InvalidOperationException(
                        "Baked Content Provider 결과의 WorldRecordMode/WorldHistorySeed가 새 게임 선택과 다릅니다.");
                }
                GeneratedTeam[] teams = CareerBakedContentAdapter.CreateGeneratedTeams(
                    _bakedContent,
                    LeagueGrade.Rookie,
                    _configuration.Balance.PlayerEvaluation);
                var evaluator = new ContractOfferEvaluator(
                    _configuration.Balance.ContractOffer,
                    _configuration.Balance.PlayerEvaluation,
                    new Pcg32Random(State.RandomSeed));
                ContractOffer[] offers = ContractOfferBoard.SelectOffers(
                    _configuration.Balance.ContractOffer,
                    evaluator,
                    player,
                    teams);
                State.SetupResult = new NewGameSetupResult(teams, offers);
            }
            else
            {
                if (_configuration.ContentSource != NewGameContentSource.ExplicitSyntheticTestFixture)
                    throw new InvalidOperationException("알 수 없는 새 게임 Content Source입니다.");

                // 테스트 fixture를 위한 명시적 격리 경로다. Production 새 게임은 이 분기에 진입하지 않는다.
                var setup = new NewGameSetup(
                    _configuration.Balance.ContractOffer,
                    _configuration.Balance.TeamGeneration,
                    _configuration.Balance.PlayerEvaluation,
                    new Pcg32Random(State.RandomSeed));
                int[] emblemIds = TeamEmblemSelector.CreateShuffledIds(
                    _configuration.TeamEmblemCount,
                    State.RandomSeed);
                State.SetupResult = setup.GenerateLeagueAndOffers(
                    player,
                    _configuration.TeamCount,
                    _configuration.Archetypes,
                    _configuration.TeamIdentities,
                    _configuration.PlayerNamePool,
                    emblemIds);
            }
            for (int index = 0; index < State.SetupResult.Offers.Length; index++)
            {
                ContractOffer offer = State.SetupResult.Offers[index];
                State.SetupResult.Offers[index] = offer.WithMovementClauses(
                    hasUpperLeagueReleaseClause: true,
                    upperLeagueReleaseCompensation: offer.AnnualSalary,
                    hasRelegationTransferRequestClause: false);
            }
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
                offer.Team.TeamId,
                LeagueId.RookieMain);
            playerState.AttachGrowthState(
                new PlayerGrowthFactory(_configuration.Balance.Growth).Create(
                    player,
                    _configuration.StartingAge,
                    _configuration.Balance.CareerSeason.InitialCondition));
            var contract = new PlayerContractState(
                CurrentSaveVersion,
                contractId: 1,
                player.PlayerId,
                offer.Team.TeamId,
                LeagueId.RookieMain,
                _configuration.FirstSeasonYear,
                offer.ContractYears,
                offer.SigningBonus,
                offer.AnnualSalary,
                offer.ExpectedRole,
                offer.HasUpperLeagueReleaseClause,
                offer.UpperLeagueReleaseCompensation,
                offer.HasRelegationTransferRequestClause);
            WorldState world = _configuration.ContentSource == NewGameContentSource.BakedHistorical
                ? new CareerWorldFactory(_configuration).CreateNewWorld(
                    State.RandomSeed,
                    _bakedContent ?? throw new InvalidOperationException("Baked 새 게임 월드가 로드되지 않았습니다."),
                    playerState,
                    contract)
                : new CareerWorldFactory(_configuration).CreateNewWorld(
                    State.RandomSeed,
                    State.SetupResult.Teams,
                    playerState,
                    contract);

            Career = new CareerState(
                CurrentSaveVersion,
                playerState,
                world,
                contract,
                availableMoney: offer.SigningBonus,
                creationProfile: State.Draft.CreateProfile());
            State.Step = NewGameStep.ContractComplete;
        }

        /// <summary>
        /// 계약이 끝난 커리어를 Rookie League 정규 시즌 상태로 전환한다.
        /// </summary>
        public void StartRookieSeason()
        {
            RequireStep(NewGameStep.ContractComplete);
            for (int leagueIndex = 0; leagueIndex < Career.World.Leagues.Count; leagueIndex++)
                StartLeagueSeason(Career.World.Leagues[leagueIndex]);

            Career.MyPlayer.InitializeSeasonStatus(
                _configuration.Balance.CareerSeason.InitialCondition,
                _configuration.Balance.CareerSeason.InitialManagerEvaluation);
            new CareerRoleEvaluationService(Career, _configuration.Balance)
                .BeginSeason(requiresInjuryReturnObservation: false);
            Career.CurrentLeague.CurrentSeason.SnapshotRookieEligibility(
                Career.CurrentLeague.Teams,
                Career.MyPlayer,
                _configuration.Balance.SeasonAwards,
                myCareerPlateAppearances: 0,
                myCareerPitchingOuts: 0,
                myRegisteredSeasons: 0);
            Career.TradeState.BeginSeason(
                Career.CurrentLeague.CurrentSeason.SeasonId,
                _configuration.Balance.TradeMarket.TradeDeadlineGame);
            Career.World.Calendar.AdvanceTo(new DateTime(
                _configuration.FirstSeasonYear,
                _configuration.Balance.CareerSeason.SeasonOpeningMonth,
                _configuration.Balance.CareerSeason.SeasonOpeningDay));
            State.Step = NewGameStep.Completed;
        }

        private void StartLeagueSeason(LeagueState league)
        {
            int teamCount = league.Teams.Count;
            var teamIds = new int[teamCount];
            var teamRecords = new TeamSeasonRecordState[teamCount];
            for (int index = 0; index < teamCount; index++)
            {
                int teamId = league.Teams[index].TeamId;
                teamIds[index] = teamId;
                teamRecords[index] = new TeamSeasonRecordState(
                    teamId,
                    DeterministicSeed.Derive(league.RandomSeed, 0x544945425245414BUL ^ (uint)teamId));
            }

            ulong scheduleSeed = DeterministicSeed.Derive(league.RandomSeed, 0x5343484544554C45UL);
            var scheduleGenerator = new SeasonScheduleGenerator(new Pcg32Random(scheduleSeed));
            ScheduledGameDefinition[] definitions = scheduleGenerator.Generate(
                teamIds,
                _configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam);
            var games = new ScheduledGameState[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                ScheduledGameDefinition definition = definitions[index];
                ulong streamId = ((ulong)league.CurrentSeason.SeasonId << 32) |
                                 (uint)definition.GameId;
                games[index] = new ScheduledGameState(
                    definition.GameId,
                    definition.Round,
                    DeterministicSeed.Derive(league.RandomSeed, streamId),
                    definition.AwayTeamId,
                    definition.HomeTeamId);
            }

            PlayerState player = league.LeagueId == Career.MyPlayer.CurrentLeagueId
                ? Career.MyPlayer
                : null;
            league.CurrentSeason.PinVersionStamp(
                SimulationVersionStamp.CreateCurrent(
                    _configuration.Balance.Version,
                    _configuration.Balance.ContentHash));
            league.CurrentSeason.StartRegularSeason(
                new SeasonScheduleState(games),
                teamRecords,
                new PlayerSeasonStatisticsState(),
                player,
                league.Teams);
            if (player == null)
            {
                league.CurrentSeason.SnapshotRookieEligibility(
                    league.Teams,
                    _configuration.Balance.SeasonAwards);
            }
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
                    State.Step = State.UsesGuidedCreation ? NewGameStep.Identity : NewGameStep.PlayerType;
                    break;
                case NewGameStep.Handedness:
                    State.Step = NewGameStep.Position;
                    break;
                case NewGameStep.AttributeAllocation:
                    State.Step = State.UsesGuidedCreation ? NewGameStep.Position : NewGameStep.Handedness;
                    break;
                case NewGameStep.PlayerDetails:
                    State.Step = NewGameStep.AttributeAllocation;
                    break;
                case NewGameStep.MatchSettings:
                    State.Step = NewGameStep.PlayerDetails;
                    break;
                case NewGameStep.FinalConfirmation:
                    State.Step = NewGameStep.MatchSettings;
                    break;
                case NewGameStep.PlayerCard:
                    State.Step = State.UsesGuidedCreation
                        ? NewGameStep.FinalConfirmation
                        : NewGameStep.AttributeAllocation;
                    break;
                case NewGameStep.ContractOffers:
                    State.SetupResult = null;
                    State.SelectedOffer = null;
                    State.Step = State.UsesGuidedCreation
                        ? NewGameStep.FinalConfirmation
                        : NewGameStep.PlayerCard;
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
                    competitors.ToArray(),
                    team.EmblemId);
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

        private static string ValidatePlayerName(string playerName)
        {
            string trimmed = playerName?.Trim() ?? string.Empty;
            if (trimmed.Length < 2 || trimmed.Length > 12)
                throw new ArgumentException("선수 이름은 2~12자로 입력해 주세요.", nameof(playerName));

            for (int index = 0; index < trimmed.Length; index++)
            {
                char value = trimmed[index];
                bool isAllowed = value is >= '가' and <= '힣' or >= 'ㄱ' and <= 'ㅎ' or >= 'ㅏ' and <= 'ㅣ' or
                    >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or ' ';
                if (!isAllowed)
                    throw new ArgumentException("선수 이름에는 한글, 영문, 숫자와 공백만 사용할 수 있습니다.", nameof(playerName));
            }
            return trimmed;
        }
    }
}
