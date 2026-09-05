using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Data;
using Baseball.Game.Manager;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// Presentation이 한 번의 입력으로 적용할 초기 능력치 배분안이다.
    /// </summary>
    public readonly struct AttributeAllocationPresetView
    {
        private readonly int[] _values;

        public AttributeAllocationPresetView(string label, bool isRecommended, int[] values)
        {
            Label = label;
            IsRecommended = isRecommended;
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public string Label { get; }
        public bool IsRecommended { get; }

        public int GetValue(int index)
        {
            if ((uint)index >= (uint)_values.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _values[index];
        }
    }

    /// <summary>
    /// Presentation이 Simulation 타입을 직접 참조하지 않고 표시할 계약 오퍼 정보다.
    /// </summary>
    public readonly struct ContractOfferView
    {
        public ContractOfferView(
            int teamId,
            string teamName,
            TeamColor primaryColor,
            TeamArchetype archetype,
            int developmentRating,
            int positionNeed,
            long signingBonus,
            long annualSalary,
            int contractYears,
            ExpectedRole expectedRole,
            double offerScore,
            string evaluationOpportunitySummary,
            string competitorSummary,
            bool isSelected,
            int emblemId = 0)
        {
            TeamId = teamId;
            TeamName = teamName;
            PrimaryColor = primaryColor;
            Archetype = archetype;
            DevelopmentRating = developmentRating;
            PositionNeed = positionNeed;
            SigningBonus = signingBonus;
            AnnualSalary = annualSalary;
            ContractYears = contractYears;
            ExpectedRole = expectedRole;
            OfferScore = offerScore;
            EvaluationOpportunitySummary = evaluationOpportunitySummary;
            CompetitorSummary = competitorSummary;
            IsSelected = isSelected;
            EmblemId = emblemId;
        }

        public int TeamId { get; }
        public string TeamName { get; }
        public TeamColor PrimaryColor { get; }
        public TeamArchetype Archetype { get; }
        public int DevelopmentRating { get; }
        public int PositionNeed { get; }
        public long SigningBonus { get; }
        public long AnnualSalary { get; }
        public int ContractYears { get; }
        public ExpectedRole ExpectedRole { get; }
        public double OfferScore { get; }
        public string EvaluationOpportunitySummary { get; }
        public string CompetitorSummary { get; }
        public bool IsSelected { get; }
        public int EmblemId { get; }
    }

    /// <summary>
    /// 계약 완료 화면과 첫 대시보드에 필요한 커리어 요약이다.
    /// </summary>
    public readonly struct CareerSummaryView
    {
        public CareerSummaryView(
            string playerName,
            string nationality,
            PlayerPosition position,
            string teamName,
            int seasonYear,
            LeagueLevel leagueLevel,
            SeasonPhase seasonPhase,
            long availableMoney,
            long annualSalary,
            ExpectedRole expectedRole,
            int emblemId = 0)
        {
            PlayerName = playerName;
            Nationality = nationality;
            Position = position;
            TeamName = teamName;
            SeasonYear = seasonYear;
            LeagueLevel = leagueLevel;
            SeasonPhase = seasonPhase;
            AvailableMoney = availableMoney;
            AnnualSalary = annualSalary;
            ExpectedRole = expectedRole;
            EmblemId = emblemId;
        }

        public string PlayerName { get; }
        public string Nationality { get; }
        public PlayerPosition Position { get; }
        public string TeamName { get; }
        public int SeasonYear { get; }
        public LeagueLevel LeagueLevel { get; }
        public SeasonPhase SeasonPhase { get; }
        public long AvailableMoney { get; }
        public long AnnualSalary { get; }
        public ExpectedRole ExpectedRole { get; }
        public int EmblemId { get; }
    }

    /// <summary>
    /// 새 게임 상태 머신을 영속 GameRoot에서 소유하고 UI용 읽기 모델을 제공한다.
    /// </summary>
    public sealed class NewGameManager : ManagerBehaviour<NewGameManager>
    {
        private NewGameConfiguration _configuration;
        private NewGameFlow _flow;
        private ContractOfferView[] _offerViews = Array.Empty<ContractOfferView>();
        private bool _isAtTitle;

        public override int InitializationOrder => -25;
        public NewGameStep CurrentStep => _flow?.State.Step ?? NewGameStep.Identity;
        public bool IsAtTitle => _isAtTitle;
        public CareerCreationDraft Draft => _flow?.State.Draft;
        public string PlayerName => _flow?.State.PlayerName ?? string.Empty;
        public string Nationality => _flow?.State.Nationality ?? string.Empty;
        public PlayerType? PlayerType => _flow?.State.PlayerType;
        public PlayerPosition PrimaryPosition => _flow?.State.PrimaryPosition ?? PlayerPosition.Unknown;
        public Handedness BattingHand => _flow?.State.BattingHand ?? Handedness.Right;
        public Handedness ThrowingHand => _flow?.State.ThrowingHand ?? Handedness.Right;
        public BatterAttributes? BatterAttributes => _flow?.State.BatterAttributes;
        public PitcherAttributes? PitcherAttributes => _flow?.State.PitcherAttributes;
        public ulong RandomSeed => _flow?.State.RandomSeed ?? 0UL;
        public CareerCreationRules CareerCreationRules => _configuration.CareerCreationRules;
        public CareerAttributeAllocationRule CurrentCreationAttributeRule =>
            CareerCreationRules.GetRule(PlayerType ?? Baseball.Core.Players.PlayerType.Batter);
        public IReadOnlyList<AttributeAllocationPresetView> CreationAttributeAllocationPresets =>
            CreateCreationAttributeAllocationPresets();
        public IReadOnlyList<AttributeAllocationPresetView> AttributeAllocationPresets =>
            CreateCreationAttributeAllocationPresets();
        public IReadOnlyList<ContractOfferView> Offers => _offerViews;
        public string BuildWarning => _flow?.BuildWarning ?? string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public CareerState CurrentCareer => _flow?.Career;
        public CareerSummaryView? CareerSummary => BuildCareerSummary();

        public event Action FlowChanged;

        protected override void OnInitialize()
        {
            _configuration = NewGameDefinition.LoadConfiguration();
            RestartNewGame(CreateRuntimeSeed());
        }

        protected override void OnShutdown()
        {
            FlowChanged = null;
            _offerViews = Array.Empty<ContractOfferView>();
            _flow = null;
        }

        /// <summary>
        /// 테스트·재시작에서 지정한 Seed로 draft를 초기화한다.
        /// </summary>
        public void RestartNewGame(ulong randomSeed)
        {
            _flow = new NewGameFlow(_configuration, randomSeed, CreateWorldHistorySeed(randomSeed));
            _isAtTitle = true;
            LastError = string.Empty;
            RebuildOfferViews();
            FlowChanged?.Invoke();
        }

        /// <summary>타이틀에서 선수 커리어 생성을 시작한다.</summary>
        public void StartPlayerCareerCreation()
        {
            if (_flow == null || _flow.State.Step != NewGameStep.Identity)
                _flow = CreateFlow();
            _isAtTitle = false;
            LastError = string.Empty;
            RebuildOfferViews();
            FlowChanged?.Invoke();
        }

        /// <summary>확정 전 draft를 버리고 타이틀로 돌아간다.</summary>
        public void DiscardDraftAndShowTitle()
        {
            _flow = CreateFlow();
            _isAtTitle = true;
            LastError = string.Empty;
            RebuildOfferViews();
            FlowChanged?.Invoke();
        }

        public bool SubmitBasicInformation(
            string playerName,
            PlayerType playerType,
            Handedness battingHand,
            Handedness throwingHand)
        {
            return TryAdvance(() => _flow.SubmitBasicInformation(
                playerName,
                playerType,
                battingHand,
                throwingHand));
        }

        public bool SubmitCreationPosition(PlayerPosition batterPosition, PitcherRole preferredPitcherRole)
        {
            return TryAdvance(() => _flow.SubmitCreationPosition(batterPosition, preferredPitcherRole));
        }

        public bool SubmitCreationAttributes(int[] values)
        {
            return TryAdvance(() => _flow.SubmitCreationAttributes(values));
        }

        public bool SubmitBatterDetails(BatterStyle style)
        {
            return TryAdvance(() => _flow.SubmitBatterDetails(style));
        }

        public bool SubmitPitcherDetails(PitchType[] pitchTypes, PitchType primaryPitch)
        {
            return TryAdvance(() => _flow.SubmitPitcherDetails(pitchTypes, primaryPitch));
        }

        public bool SubmitMatchSettings(
            BattingApproach battingApproach,
            PitchingApproach pitchingApproach,
            MatchProgressMode matchProgressMode,
            int gameSpeed,
            bool autoSlowOnPlayerEvent)
        {
            return TryAdvance(() => _flow.SubmitMatchSettings(
                battingApproach,
                pitchingApproach,
                matchProgressMode,
                gameSpeed,
                autoSlowOnPlayerEvent));
        }

        public bool ConfirmCreationAndGenerateOffers()
        {
            return TryAdvance(() =>
            {
                _flow.ConfirmCreation();
                _flow.GenerateOffers();
            });
        }

        public bool SubmitIdentity(string playerName, string nationality)
        {
            return TryAdvance(() => _flow.SubmitIdentity(playerName, nationality));
        }

        public bool SelectPlayerType(PlayerType playerType)
        {
            return TryAdvance(() => _flow.SelectPlayerType(playerType));
        }

        public bool SelectPosition(PlayerPosition position)
        {
            return TryAdvance(() => _flow.SelectPosition(position));
        }

        public bool SelectHandedness(Handedness battingHand, Handedness throwingHand)
        {
            return TryAdvance(() => _flow.SelectHandedness(battingHand, throwingHand));
        }

        /// <summary>
        /// 현재 선수 유형에 맞춰 순서가 고정된 능력치 6개를 제출한다.
        /// </summary>
        public bool SubmitAttributes(int[] values)
        {
            int required = CurrentCreationAttributeRule.AttributeCount;
            if (values == null || values.Length < required)
                return Fail($"능력치 {required}개가 필요합니다.");
            var submitted = new int[required];
            Array.Copy(values, submitted, required);
            return TryAdvance(() => _flow.SubmitCreationAttributes(submitted));
        }

        public bool GenerateOffers()
        {
            return TryAdvance(_flow.GenerateOffers);
        }

        public bool SelectOffer(int teamId)
        {
            return TryAdvance(() => _flow.SelectOffer(teamId));
        }

        public bool SignSelectedOffer()
        {
            return TryAdvance(_flow.SignSelectedOffer);
        }

        public bool StartRookieSeason()
        {
            try
            {
                _flow.StartRookieSeason();
                GameManager.EnsureExists()
                    .EnsureManager<CareerManager>("CareerManager")
                    .BeginCareer(_flow.Career, _configuration.Balance);
                LastError = string.Empty;
                RebuildOfferViews();
                FlowChanged?.Invoke();
                return true;
            }
            catch (ArgumentException exception)
            {
                return Fail(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool GoBack()
        {
            if (!_isAtTitle && _flow.State.Step == NewGameStep.Identity)
            {
                DiscardDraftAndShowTitle();
                return true;
            }
            if (!_flow.GoBack())
                return false;

            LastError = string.Empty;
            RebuildOfferViews();
            FlowChanged?.Invoke();
            return true;
        }

        private bool TryAdvance(Action action)
        {
            try
            {
                action();
                _isAtTitle = false;
                LastError = string.Empty;
                RebuildOfferViews();
                FlowChanged?.Invoke();
                return true;
            }
            catch (ArgumentException exception)
            {
                return Fail(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        private AttributeAllocationPresetView[] CreateCreationAttributeAllocationPresets()
        {
            CareerAttributeAllocationRule rule = CurrentCreationAttributeRule;
            if (PlayerType == Baseball.Core.Players.PlayerType.Pitcher)
            {
                return new[]
                {
                    CreateCreationPreset("선발 균형형", PrimaryPosition == PlayerPosition.StartingPitcher, rule, 3, 3, 3, 4),
                    CreateCreationPreset("이닝이터형", false, rule, 2, 3, 2, 6),
                    CreateCreationPreset("제구형", false, rule, 2, 6, 3, 3),
                    CreateCreationPreset("파워 피처형", false, rule, 6, 2, 3, 2),
                    CreateCreationPreset("변화구형", false, rule, 3, 2, 6, 2),
                    CreateCreationPreset("불펜 특화형", PrimaryPosition == PlayerPosition.ReliefPitcher, rule, 6, 4, 4, 1)
                };
            }

            return new[]
            {
                CreateCreationPreset("균형형", false, rule, 1, 1, 1, 1, 1, 1),
                CreateCreationPreset("교타자", false, rule, 6, 2, 3, 3, 2, 2),
                CreateCreationPreset("장타자", IsPowerPosition(PrimaryPosition), rule, 3, 6, 2, 2, 2, 3),
                CreateCreationPreset("선구안형", false, rule, 3, 2, 6, 2, 2, 2),
                CreateCreationPreset("호타준족형", PrimaryPosition == PlayerPosition.CenterField, rule, 4, 2, 2, 6, 3, 2),
                CreateCreationPreset("수비형", IsDefensePosition(PrimaryPosition), rule, 2, 1, 2, 3, 6, 5)
            };
        }

        private static AttributeAllocationPresetView CreateCreationPreset(
            string label,
            bool isRecommended,
            CareerAttributeAllocationRule rule,
            params int[] weights)
        {
            return new AttributeAllocationPresetView(
                label,
                isRecommended,
                rule.CreateWeightedValues(weights));
        }

        private static bool IsPowerPosition(PlayerPosition position)
        {
            return position is PlayerPosition.FirstBase or PlayerPosition.ThirdBase or
                PlayerPosition.LeftField or PlayerPosition.RightField or PlayerPosition.DesignatedHitter;
        }

        private static bool IsDefensePosition(PlayerPosition position)
        {
            return position is PlayerPosition.Catcher or PlayerPosition.SecondBase or PlayerPosition.Shortstop;
        }

        private bool Fail(string message)
        {
            LastError = message;
            FlowChanged?.Invoke();
            return false;
        }

        private void RebuildOfferViews()
        {
            NewGameSetupResult result = _flow?.State.SetupResult;
            if (result == null)
            {
                _offerViews = Array.Empty<ContractOfferView>();
                return;
            }

            ContractOffer? selected = _flow.State.SelectedOffer;
            _offerViews = new ContractOfferView[result.Offers.Length];
            for (int index = 0; index < result.Offers.Length; index++)
            {
                ContractOffer offer = result.Offers[index];
                IReadOnlyList<RosterCompetitor> competitors = offer.Team.GetPositionCompetitors(
                    _flow.State.PrimaryPosition);
                _offerViews[index] = new ContractOfferView(
                    offer.Team.TeamId,
                    offer.Team.Name,
                    offer.Team.PrimaryColor,
                    offer.Team.Archetype.Archetype,
                    offer.Team.Archetype.Development,
                    offer.Team.GetPositionNeed(_flow.State.PrimaryPosition),
                    offer.SigningBonus,
                    offer.AnnualSalary,
                    offer.ContractYears,
                    offer.ExpectedRole,
                    offer.OfferScore,
                    BuildEvaluationOpportunitySummary(offer.ExpectedRole),
                    BuildCompetitorSummary(competitors),
                    selected.HasValue && selected.Value.Team.TeamId == offer.Team.TeamId,
                    offer.Team.EmblemId);
            }
        }

        private CareerSummaryView? BuildCareerSummary()
        {
            CareerState career = _flow?.Career;
            if (career == null)
                return null;

            TeamState selectedTeam = null;
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = career.CurrentLeague.Teams[index];
                if (team.TeamId == career.MyPlayer.CurrentTeamId)
                {
                    selectedTeam = team;
                    break;
                }
            }
            if (selectedTeam == null)
                throw new InvalidOperationException("계약한 구단을 현재 리그에서 찾을 수 없습니다.");

            SeasonState season = career.CurrentLeague.CurrentSeason;
            return new CareerSummaryView(
                career.MyPlayer.Name,
                career.MyPlayer.Nationality,
                career.MyPlayer.PrimaryPosition,
                selectedTeam.Name,
                season.Year,
                season.LeagueLevel,
                season.Phase,
                career.AvailableMoney,
                career.CurrentContract.AnnualSalary,
                career.CurrentContract.ExpectedRole,
                selectedTeam.EmblemId);
        }

        private static string BuildCompetitorSummary(IReadOnlyList<RosterCompetitor> competitors)
        {
            if (competitors == null || competitors.Count == 0)
                return "동일 포지션 경쟁자 없음";

            var builder = new StringBuilder(64);
            for (int index = 0; index < competitors.Count; index++)
            {
                if (index > 0)
                    builder.Append(" · ");
                builder.Append(competitors[index].Name);
                builder.Append(" OVR ");
                builder.Append(competitors[index].Overall);
            }

            return builder.ToString();
        }

        private string BuildEvaluationOpportunitySummary(ExpectedRole expectedRole)
        {
            var balance = _configuration.Balance.CareerSeason;
            int interval = expectedRole switch
            {
                ExpectedRole.StartingCompetition => balance.StartingCompetitionEvaluationInterval,
                ExpectedRole.RosterCompetition => balance.RosterCompetitionEvaluationInterval,
                _ => balance.BenchCompetitionEvaluationInterval
            };
            int gameInterval = interval * balance.StartingRotationSize;
            return $"컨디션 {balance.EvaluationOpportunityMinimumCondition}+ · 약 {gameInterval}경기마다 평가";
        }

        /// <summary>
        /// 로딩 화면에서 현재 Seed의 커리어 Content를 미리 만들어 둔다.
        /// "구단 오퍼 확인"이 누르는 순간 같은 Seed의 캐시를 그대로 쓴다.
        /// </summary>
        public void PrewarmCareerContent()
        {
            if (_configuration?.ContentSource != NewGameContentSource.BakedHistorical || _flow == null)
                return;
            _configuration.BakedContentProvider.Load(new CareerBakedContentRequest(
                _configuration.WorldRecordMode,
                _flow.State.WorldHistorySeed));
        }

        private NewGameFlow CreateFlow()
        {
            ulong randomSeed = CreateRuntimeSeed();
            return new NewGameFlow(_configuration, randomSeed, CreateWorldHistorySeed(randomSeed));
        }

        private static ulong CreateRuntimeSeed()
        {
            // 시스템 시간은 Seed를 고르는 Game 레이어 경계에서만 사용하며, 선택된 값은 즉시 상태에 저장한다.
            // 커리어 진행(리그 시드, 일정, 오퍼 RNG)은 매 플레이스루마다 달라야 하므로 항상 새로 뽑는다.
            return unchecked((ulong)DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 배경 역사만 담당하는 Seed다. Pool이 설정돼 있으면 그중 하나를 골라 미리 구운 월드가 적중하게 하고,
        /// 비어 있으면 지금까지처럼 커리어 Seed를 그대로 써서 매번 새 월드를 만든다.
        /// 커리어 Seed와 분리돼 있으므로 Pool을 써도 플레이스루의 다양성은 줄지 않는다.
        /// </summary>
        private static ulong CreateWorldHistorySeed(ulong careerSeed)
        {
            return NewGameDefinition.TrySelectCareerWorldSeedFromResources(careerSeed, out ulong pooledSeed)
                ? pooledSeed
                : careerSeed;
        }
    }
}
