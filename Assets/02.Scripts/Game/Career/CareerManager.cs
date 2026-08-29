using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career.Narrative;
using Baseball.Game.Career.News;
using Baseball.Game.Manager;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 새 게임 이후의 영속 CareerState와 정규 시즌 진행을 소유한다.
    /// </summary>
    public sealed partial class CareerManager : ManagerBehaviour<CareerManager>
    {
        private CareerSeasonService _seasonService;
        private CareerPostseasonService _postseasonService;
        private CareerSeasonTransitionService _seasonTransitionService;
        private BalanceTable _balance;
        private CareerGameAdvanceResult? _lastGame;
        private CareerMatchSession _activeMatch;
        private CareerSeasonAutoCompletionResult? _lastSeasonAutoCompletion;

        public override int InitializationOrder => -20;
        public bool HasActiveCareer => CurrentCareer != null && CurrentCareer.Retirement.IsRetired == false;
        public CareerState CurrentCareer { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public CareerDashboardView Dashboard => HasRetirementRecap ? null : BuildDashboard();
        public CareerContractView Contract => HasRetirementRecap ? null : BuildContractView();
        public CareerMatchSession ActiveMatch => _activeMatch;
        public bool HasActiveMatch => _activeMatch != null;
        public LeagueHubView LeagueHub => HasRetirementRecap ? null : BuildLeagueHub();
        public TeamOverviewView TeamOverview => HasRetirementRecap ? null : BuildTeamOverview();

        public event Action CareerChanged;

        /// <summary>
        /// 저장되거나 새로 시작한 커리어를 현재 시즌 단계 그대로 인수한다.
        /// </summary>
        public void BeginCareer(CareerState career, BalanceTable balance)
        {
            CurrentCareer = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _seasonService = null;
            _postseasonService = null;
            _seasonTransitionService = null;
            RefreshSeasonServices();
            _seasonService?.EnsureNextGamePlan();
            _lastGame = null;
            _activeMatch = null;
            _lastSeasonAutoCompletion = null;
            LastError = string.Empty;
            CareerChanged?.Invoke();
        }

        /// <summary>
        /// 다음 경기 라운드를 즉시 시뮬레이션하고 대시보드를 갱신한다.
        /// </summary>
        public bool AdvanceNextGame()
        {
            if (_seasonService == null)
                return Fail("진행 중인 정규 시즌이 없습니다.");
            if (_activeMatch != null)
                return Fail("준비하거나 진행 중인 경기를 먼저 마쳐야 합니다.");
            if (CurrentCareer?.Narrative.PendingReaction != null)
                return Fail("먼저 경기 후 질문에 답해 주세요.");

            try
            {
                _lastGame = _seasonService.AdvanceNextRound();
                new RetirementRecapService(_balance)
                    .RecordCompletedGame(CurrentCareer, _lastGame.Value);
                RefreshSeasonServices();
                TryCompleteDeclaredRetirement();
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 현재 정규시즌 또는 포스트시즌 단계만 자동 진행하고 다음 단계에서 멈춘다.
        /// </summary>
        public bool AutoCompleteCurrentSeasonPhase()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            if (_activeMatch != null)
                return Fail("준비하거나 진행 중인 경기를 먼저 마쳐야 합니다.");
            if (CurrentCareer.Narrative.PendingReaction != null)
                return Fail("먼저 경기 후 질문에 답해 주세요.");

            try
            {
                _lastSeasonAutoCompletion = new CareerSeasonAutoCompletionService(CurrentCareer, _balance)
                    .CompleteCurrentPhase();
                RefreshSeasonServices();
                TryCompleteDeclaredRetirement();
                _lastGame = null;
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>정규시즌 공개부터 시상식·정산까지 저장된 결산 장면을 한 단계 진행한다.</summary>
        public bool AdvanceSeasonReview()
        {
            if (CurrentCareer == null)
                return Fail("진행 중인 커리어가 없습니다.");
            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            SeasonReviewState review = season.Review;
            if (review == null)
                return Fail("진행할 시즌 리뷰가 없습니다.");

            try
            {
                if (season.Phase == SeasonPhase.Postseason)
                {
                    if (review.Step == SeasonReviewStep.PostseasonInProgress)
                        return Fail("포스트시즌 경기를 먼저 진행해 주세요.");
                    review.Advance(season.ReviewSnapshot);
                }
                else if (season.Phase == SeasonPhase.SeasonReview)
                {
                    if (review.Step == SeasonReviewStep.SeasonSummary)
                        return SettleSeasonAndBeginOffseason();

                    SeasonReviewStep previous = review.Step;
                    review.Advance(season.ReviewSnapshot);
                    if (previous == SeasonReviewStep.PostseasonResult)
                        PublishPendingReviewNews(NewsReleaseGate.AfterPostseasonReveal);
                    if (previous == SeasonReviewStep.Awards &&
                        review.Step == SeasonReviewStep.SeasonSummary)
                    {
                        PublishPendingReviewNews(NewsReleaseGate.AfterAwardReveal);
                    }
                }
                else if (season.Phase == SeasonPhase.Offseason &&
                         review.Step == SeasonReviewStep.IncomeSettlement)
                {
                    review.Complete();
                }
                else
                {
                    return Fail("현재 시즌 단계에서는 리뷰를 진행할 수 없습니다.");
                }

                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>보상은 건너뛰지 않고 현재 시즌 단계의 연출만 안전한 도착 화면까지 생략한다.</summary>
        public bool SkipSeasonReview()
        {
            if (CurrentCareer == null)
                return Fail("진행 중인 커리어가 없습니다.");
            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            SeasonReviewState review = season.Review;
            if (review == null)
                return Fail("건너뛸 시즌 리뷰가 없습니다.");

            try
            {
                if (season.Phase == SeasonPhase.Postseason)
                {
                    review.SkipToPostseasonInProgress();
                }
                else if (season.Phase == SeasonPhase.SeasonReview)
                {
                    PublishPendingReviewNews(NewsReleaseGate.AfterPostseasonReveal);
                    PublishPendingReviewNews(NewsReleaseGate.AfterAwardReveal);
                    review.SkipToSeasonSummary();
                }
                else
                {
                    return Fail("현재 화면에서는 시즌 리뷰를 건너뛸 수 없습니다.");
                }

                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        private void PublishPendingReviewNews(NewsReleaseGate gate)
        {
            int seasonId = CurrentCareer.CurrentLeague.CurrentSeason.SeasonId;
            IReadOnlyList<NewsEvent> pending = CurrentCareer.News.PendingEvents;
            var dates = new List<CareerDate>();
            for (int index = 0; index < pending.Count; index++)
            {
                NewsEvent newsEvent = pending[index];
                if (newsEvent.OccurredAt.Cycle.SeasonId != seasonId || newsEvent.ReleaseGate != gate)
                    continue;
                bool alreadyAdded = false;
                for (int dateIndex = 0; dateIndex < dates.Count; dateIndex++)
                    alreadyAdded |= dates[dateIndex].Equals(newsEvent.OccurredAt);
                if (!alreadyAdded)
                    dates.Add(newsEvent.OccurredAt);
            }

            var service = new CareerNewsService(CurrentCareer);
            for (int index = 0; index < dates.Count; index++)
                service.PublishCycle(dates[index], gate);
        }

        /// <summary>
        /// 정규시즌 또는 내 구단 포스트시즌 경기를 기록 변경 없이 준비 화면 상태로 연다.
        /// </summary>
        public bool PrepareNextGame()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            if (_activeMatch != null)
                return Fail("이미 준비하거나 진행 중인 경기가 있습니다.");
            if (CurrentCareer.Narrative.PendingReaction != null)
                return Fail("먼저 경기 후 질문에 답해 주세요.");

            try
            {
                SeasonPhase phase = CurrentCareer.CurrentLeague.CurrentSeason.Phase;
                if (phase == SeasonPhase.RegularSeason)
                {
                    _seasonService ??= new CareerSeasonService(CurrentCareer, _balance);
                    _activeMatch = _seasonService.PrepareNextGame();
                }
                else if (phase == SeasonPhase.Postseason)
                {
                    _postseasonService ??= new CareerPostseasonService(CurrentCareer, _balance);
                    _activeMatch = _postseasonService.PrepareNextPlayerGame();
                }
                else
                {
                    return Fail("진행할 정규시즌 또는 포스트시즌 경기가 없습니다.");
                }

                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 준비된 경기를 선택한 관전 방식으로 시작한다.
        /// </summary>
        public bool StartPreparedGame(CareerMatchMode mode)
        {
            return MutateActiveMatch(match => match.Start(mode));
        }

        /// <summary>커리어 생성 또는 설정 화면에서 고른 기본 진행 방식으로 준비된 경기를 시작한다.</summary>
        public bool StartPreparedGameFromSettings()
        {
            if (CurrentCareer == null)
                return Fail("진행 중인 커리어가 없습니다.");
            return StartPreparedGame(ToCareerMatchMode(CurrentCareer.GameSettings.MatchProgressMode));
        }

        /// <summary>경기 관전과 선수 방침 설정을 커리어 런타임 상태에 반영한다.</summary>
        public bool UpdateGameSettings(
            BattingApproach battingApproach,
            PitchingApproach pitchingApproach,
            MatchProgressMode progressMode,
            int gameSpeed,
            bool autoSlowOnPlayerEvent)
        {
            if (CurrentCareer == null)
                return Fail("진행 중인 커리어가 없습니다.");

            try
            {
                CareerGameSettings settings = CurrentCareer.GameSettings;
                settings.SetBattingApproach(battingApproach);
                settings.SetPitchingApproach(pitchingApproach);
                settings.SetMatchProgressMode(progressMode);
                settings.SetGameSpeed(gameSpeed);
                settings.SetAutoSlowOnPlayerEvent(autoSlowOnPlayerEvent);
                _activeMatch?.UpdateApproaches(battingApproach, pitchingApproach);
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>저장되지 않은 경기와 커리어 런타임 상태를 버리고 타이틀 복귀가 가능한 상태로 만든다.</summary>
        public void EndCareer()
        {
            ResetCareerRuntime();
            CareerChanged?.Invoke();
        }

        /// <summary>
        /// 현재 투구에 선택한 타격 방식을 적용한다.
        /// </summary>
        public bool SubmitBattingApproach(BattingApproach approach)
        {
            return MutateActiveMatch(match => match.SubmitBattingApproach(approach));
        }

        /// <summary>플레이어 투수의 현재 이닝을 선택한 방침으로 진행한다.</summary>
        public bool AutoCompleteCurrentPitchingInning(PitchingApproach approach)
        {
            return MutateActiveMatch(match => match.AutoCompleteCurrentPitchingInning(approach));
        }

        /// <summary>
        /// 현재 타석의 남은 투구를 균형 타격으로 자동 진행한다.
        /// </summary>
        public bool AutoCompleteCurrentPlateAppearance()
        {
            return MutateActiveMatch(match => match.AutoCompleteCurrentPlateAppearance());
        }

        /// <summary>
        /// 이미 내린 선택은 유지하고 남은 경기를 자동 진행한다.
        /// </summary>
        public bool AutoCompleteActiveMatch()
        {
            return MutateActiveMatch(match => match.AutoCompleteMatch());
        }

        /// <summary>확인된 요청에 따라 준비 중이거나 진행 중인 경기의 남은 부분을 즉시 계산한다.</summary>
        public bool CompleteActiveMatchInstantly()
        {
            return MutateActiveMatch(match => match.CompleteInstantly());
        }

        /// <summary>
        /// 시작 전 경기 준비를 닫고 홈으로 돌아간다.
        /// </summary>
        public bool CancelPreparedGame()
        {
            if (_activeMatch == null || _activeMatch.Phase != CareerMatchPhase.Preparation)
                return Fail("닫을 수 있는 경기 준비 화면이 없습니다.");

            _activeMatch = null;
            LastError = string.Empty;
            CareerChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 기록 반영이 끝난 결과 화면을 닫고 갱신된 홈으로 돌아간다.
        /// </summary>
        public bool ReturnHomeFromCompletedMatch()
        {
            if (_activeMatch == null || !_activeMatch.IsCommitted)
                return Fail("홈으로 돌아갈 수 있는 경기 결과가 없습니다.");

            _activeMatch = null;
            LastError = string.Empty;
            CareerChanged?.Invoke();
            return true;
        }

        /// <summary>중요 경기 뒤 선택한 인터뷰 답변의 작은 관계 효과를 한 번만 반영한다.</summary>
        public bool ResolveCareerReaction(int optionIndex)
        {
            if (CurrentCareer?.Narrative.PendingReaction == null)
                return Fail("답변할 경기 후 질문이 없습니다.");
            try
            {
                new CareerReactionService(CurrentCareer).Resolve(optionIndex);
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 오프시즌을 마감하고 다음 시즌 정규 시즌으로 전환한 뒤 대시보드를 갱신한다.
        /// </summary>
        public bool CompleteOffseasonAndAdvanceToNextSeason()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            try
            {
                _seasonTransitionService ??= new CareerSeasonTransitionService(CurrentCareer, _balance);
                if (_seasonTransitionService.Step == SeasonTransitionStep.NotStarted)
                {
                    SeasonTransitionStep step = _seasonTransitionService.BeginTransition();
                    if (step is SeasonTransitionStep.CurrentTeamNegotiation or
                        SeasonTransitionStep.ContractOffers)
                    {
                        LastError = string.Empty;
                        CareerChanged?.Invoke();
                        return true;
                    }
                }

                if (_seasonTransitionService.Step is SeasonTransitionStep.CurrentTeamNegotiation or
                    SeasonTransitionStep.ContractOffers)
                {
                    LastError = string.Empty;
                    CareerChanged?.Invoke();
                    return true;
                }

                CompleteSeasonTransition();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 만료 계약의 다음 시즌 오퍼를 생성하고 플레이어 선택을 기다린다.
        /// </summary>
        public bool BeginContractNegotiation()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");

            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            if (season.Phase != SeasonPhase.Offseason)
                return Fail("시즌 결산과 오프시즌이 시작된 뒤 계약 오퍼를 확인할 수 있습니다.");
            if (CurrentCareer.CurrentContract.EndYear > season.Year)
                return Fail($"현재 계약은 {CurrentCareer.CurrentContract.EndYear} 시즌까지 유효합니다.");

            try
            {
                _seasonTransitionService ??= new CareerSeasonTransitionService(CurrentCareer, _balance);
                _seasonTransitionService.BeginTransition();
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 만료 후 제시된 오퍼 중 다음 계약 후보를 선택한다.
        /// </summary>
        public bool SelectContractOffer(int teamId)
        {
            if (_seasonTransitionService?.Step is not SeasonTransitionStep.CurrentTeamNegotiation and
                not SeasonTransitionStep.ContractOffers)
                return Fail("선택할 수 있는 계약 오퍼가 없습니다.");
            try
            {
                _seasonTransitionService.SelectRenewalOffer(teamId);
                LastError = string.Empty;
                CareerChanged?.Invoke();
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

        /// <summary>
        /// 선택한 오퍼에 서명하고 소속·계약 이력·다음 시즌을 한 번에 확정한다.
        /// </summary>
        public bool SignSelectedContractOffer()
        {
            if (_seasonTransitionService?.Step is not SeasonTransitionStep.CurrentTeamNegotiation and
                not SeasonTransitionStep.ContractOffers)
                return Fail("서명할 계약 오퍼가 없습니다.");
            try
            {
                ContractOffer offer = _seasonTransitionService.SelectedOffer ??
                                      throw new InvalidOperationException("먼저 계약할 구단을 선택해 주세요.");
                new RetirementRecapService(_balance)
                    .RecordContractChoice(
                        CurrentCareer,
                        offer,
                        isAccepted: true,
                        offer.Team.TeamId == CurrentCareer.MyPlayer.CurrentTeamId);
                _seasonTransitionService.SignSelectedOffer();
                CompleteSeasonTransition();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 기존 구단 제안을 보류하거나 거절하고 외부 구단 공개 시장을 연다.
        /// </summary>
        public bool OpenContractMarket(bool holdCurrentTeamOffer)
        {
            if (_seasonTransitionService?.Step != SeasonTransitionStep.CurrentTeamNegotiation)
                return Fail("먼저 기존 구단의 우선 협상 제안을 확인해 주세요.");
            try
            {
                ContractOffer? currentTeamOffer = _seasonTransitionService.CurrentTeamOffer;
                if (!holdCurrentTeamOffer && currentTeamOffer.HasValue)
                {
                    new RetirementRecapService(_balance)
                        .RecordContractChoice(
                            CurrentCareer,
                            currentTeamOffer.Value,
                            isAccepted: false,
                            isCurrentTeamOffer: true);
                }
                _seasonTransitionService.OpenMarket(holdCurrentTeamOffer);
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 에이전트에게 전달할 트레이드 태도를 바꾸고 적극 요청의 관계 비용을 반영한다.
        /// </summary>
        public bool SetTradePreference(TradePreference preference)
        {
            if (CurrentCareer?.CurrentLeague?.CurrentSeason?.Phase != SeasonPhase.RegularSeason)
                return Fail("트레이드 태도는 정규 시즌 중에만 바꿀 수 있습니다.");

            TradePreference previous = CurrentCareer.TradeState.Preference;
            if (previous == preference)
                return true;
            CurrentCareer.TradeState.SetPreference(preference);
            new RetirementRecapService(_balance)
                .RecordTradePreference(CurrentCareer, preference);
            int evaluationDelta = GetTradePreferenceTrustModifier(preference) -
                                  GetTradePreferenceTrustModifier(previous);
            if (evaluationDelta != 0)
            {
                CurrentCareer.MyPlayer.ApplyGameFeedback(
                    conditionDelta: 0,
                    managerEvaluationDelta: evaluationDelta,
                    _balance.CareerSeason.MinimumCondition);
            }
            LastError = string.Empty;
            CareerChanged?.Invoke();
            return true;
        }

        private static int GetTradePreferenceTrustModifier(TradePreference preference)
        {
            return preference switch
            {
                TradePreference.RequestTrade => -5,
                TradePreference.PreferToStay => 2,
                _ => 0
            };
        }

        public bool AcceptCurrentTeamExtension()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            try
            {
                var renewal = new ContractRenewalService(CurrentCareer, _balance);
                ContractOffer offer = renewal.BuildExtensionOffer() ??
                                      throw new InvalidOperationException("수락할 수 있는 연장 계약이 없습니다.");
                TeamState team = GetTeam(CurrentCareer.MyPlayer.CurrentTeamId);
                new RetirementRecapService(_balance)
                    .RecordContractChoice(CurrentCareer, offer, isAccepted: true, isCurrentTeamOffer: true);
                renewal.AcceptExtension();
                PublishSignedExtension(team, offer);
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool DeclineCurrentTeamExtension()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            try
            {
                TeamState team = GetTeam(CurrentCareer.MyPlayer.CurrentTeamId);
                var renewal = new ContractRenewalService(CurrentCareer, _balance);
                ContractOffer offer = renewal.BuildExtensionOffer() ??
                                      throw new InvalidOperationException("거절할 수 있는 연장 계약이 없습니다.");
                new RetirementRecapService(_balance)
                    .RecordContractChoice(CurrentCareer, offer, isAccepted: false, isCurrentTeamOffer: true);
                renewal.DeclineExtension();
                PublishDeclinedExtension(team);
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        protected override void OnShutdown()
        {
            CareerChanged = null;
            RetirementRecapReady = null;
            ResetCareerRuntime();
        }

        private void ResetCareerRuntime()
        {
            ResetGrowthRuntime();
            CurrentCareer = null;
            _seasonService = null;
            _postseasonService = null;
            _seasonTransitionService = null;
            _balance = null;
            _lastGame = null;
            _activeMatch = null;
            _lastSeasonAutoCompletion = null;
            LastError = string.Empty;
        }

        private static CareerMatchMode ToCareerMatchMode(MatchProgressMode mode)
        {
            return mode switch
            {
                MatchProgressMode.FullGameWatch => CareerMatchMode.FullGameWatch,
                MatchProgressMode.InterveneOnPlayer => CareerMatchMode.InterveneOnPlayer,
                MatchProgressMode.PlayerFocusAutomatic => CareerMatchMode.PlayerFocusAutomatic,
                MatchProgressMode.InstantResult => CareerMatchMode.ResultsOnly,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }

        private bool Fail(string message)
        {
            LastError = message;
            CareerChanged?.Invoke();
            return false;
        }

        private void CompleteSeasonTransition()
        {
            RefreshSeasonServices();
            _seasonService.EnsureNextGamePlan();
            _seasonTransitionService = null;
            _lastGame = null;
            _lastSeasonAutoCompletion = null;
            LastError = string.Empty;
            CareerChanged?.Invoke();
        }

        private CareerContractView BuildContractView()
        {
            if (CurrentCareer == null || _balance == null)
                return null;
            return new CareerContractViewBuilder(CurrentCareer, _balance)
                .Build(_seasonTransitionService, LastError);
        }

        private bool MutateActiveMatch(Action<CareerMatchSession> mutation)
        {
            if (_activeMatch == null)
                return Fail("준비하거나 진행 중인 경기가 없습니다.");

            try
            {
                mutation(_activeMatch);
                if (_activeMatch.IsComplete && !_activeMatch.IsCommitted)
                {
                    if (_activeMatch.CompetitionScope == CompetitionScope.Postseason)
                    {
                        _postseasonService ??= new CareerPostseasonService(CurrentCareer, _balance);
                        _lastGame = _postseasonService.CompletePreparedGame(_activeMatch);
                    }
                    else
                    {
                        _lastGame = _seasonService.CompletePreparedGame(_activeMatch);
                    }
                    MatchNarrativeSnapshot narrative = CurrentCareer.CurrentLeague.CurrentSeason
                        .FindMatchNarrative(_lastGame.Value.GameId) ??
                        throw new InvalidOperationException("완료 경기의 내러티브 스냅샷을 찾지 못했습니다.");
                    _activeMatch.MarkCommitted(
                        _lastGame.Value,
                        _lastGame.Value.ConditionAfter,
                        _lastGame.Value.ManagerEvaluationAfter,
                        narrative);
                    new RetirementRecapService(_balance)
                        .RecordCompletedGame(CurrentCareer, _activeMatch);
                    RefreshSeasonServices();
                    TryCompleteDeclaredRetirement();
                }

                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        private void RefreshSeasonServices()
        {
            if (CurrentCareer?.Retirement?.IsRetired == true)
            {
                _seasonService = null;
                _postseasonService = null;
                return;
            }
            SeasonPhase phase = CurrentCareer.CurrentLeague.CurrentSeason.Phase;
            if (phase == SeasonPhase.RegularSeason)
            {
                _seasonService ??= new CareerSeasonService(CurrentCareer, _balance);
                _postseasonService = null;
                return;
            }

            _seasonService = null;
            if (phase == SeasonPhase.Postseason)
                _postseasonService ??= new CareerPostseasonService(CurrentCareer, _balance);
            else
                _postseasonService = null;
        }

        private CareerDashboardView BuildDashboard()
        {
            if (CurrentCareer == null || _balance == null || CurrentCareer.Retirement.IsRetired)
                return null;

            PlayerState player = CurrentCareer.MyPlayer;
            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            TeamState playerTeam = GetTeam(player.CurrentTeamId);
            TeamSeasonRecordState teamRecord = season.GetTeamRecord(player.CurrentTeamId);
            var evaluator = new PlayerValueEvaluator(_balance.PlayerEvaluation);
            Player currentPlayer = BuildStablePlayer();
            return new CareerDashboardView
            {
                PlayerName = player.Name,
                Age = player.Age,
                Position = player.PrimaryPosition,
                BattingHand = player.BattingHand,
                ThrowingHand = player.ThrowingHand,
                BatterAttributes = currentPlayer.BatterAttributes,
                PitcherAttributes = currentPlayer.PitcherAttributes,
                Overall = evaluator.CalculatePositionValue(currentPlayer),
                Condition = player.Condition,
                ManagerEvaluation = player.ManagerEvaluation,
                ExpectedRole = CurrentCareer.CurrentExpectedRole,
                TeamName = playerTeam.Name,
                SeasonYear = season.Year,
                LeagueLevel = season.LeagueLevel,
                SeasonPhase = season.Phase,
                AvailableMoney = CurrentCareer.AvailableMoney,
                TeamRank = CalculateRank(teamRecord),
                TeamWins = teamRecord?.Wins ?? 0,
                TeamLosses = teamRecord?.Losses ?? 0,
                TeamTies = teamRecord?.Ties ?? 0,
                NextGame = BuildNextGameView(currentPlayer),
                Statistics = new PlayerSeasonStatisticsView(
                    season.PlayerStatistics,
                    player.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher),
                Competition = BuildCompetition(playerTeam, player, evaluator),
                UpcomingGames = BuildUpcomingGames(player.CurrentTeamId),
                RecentGames = BuildRecentGames(season.PlayerStatistics),
                LastGame = _lastGame,
                RemainingRegularSeasonGames = CountRemainingRegularSeasonGames(player.CurrentTeamId),
                LastSeasonAutoCompletion = _lastSeasonAutoCompletion,
                SeasonProgress = BuildSeasonProgressView(season),
                SeasonReview = season.ReviewSnapshot,
                SeasonReviewStep = season.Review?.Step ?? SeasonReviewStep.Finished,
                RevealedPostseasonGameCount = season.Review?.RevealedPostseasonGameCount ?? 0,
                RevealedAwardCount = season.Review?.RevealedAwardCount ?? 0,
                PendingReaction = CurrentCareer.Narrative.PendingReaction,
                NarrativeConfidence = CurrentCareer.Narrative.Confidence,
                MediaStanding = CurrentCareer.Narrative.MediaStanding,
                FanSupport = CurrentCareer.Narrative.FanSupport,
                TeamChemistry = CurrentCareer.Narrative.TeamChemistry
            };
        }

        private CareerSeasonProgressView BuildSeasonProgressView(SeasonState season)
        {
            PostseasonState postseason = season.Postseason;
            SeasonReviewSnapshot review = season.ReviewSnapshot;
            bool isQualified = review?.PostseasonSeed > 0;
            int postseasonGames = 0;
            if (postseason != null)
            {
                if (review == null)
                {
                    for (int index = 0; index < postseason.SeedTeamIds.Count; index++)
                        isQualified |= postseason.SeedTeamIds[index] == CurrentCareer.MyPlayer.CurrentTeamId;
                }
                for (int index = 0; index < postseason.Series.Count; index++)
                    postseasonGames += postseason.Series[index].Games.Count;
            }

            string championTeamName = review?.IsPostseasonFinalized == true
                ? review.ChampionTeamName
                : postseason?.ChampionTeamId > 0
                    ? GetTeam(postseason.ChampionTeamId).Name
                    : string.Empty;
            int playerAwardCount = 0;
            if (review?.IsPostseasonFinalized == true)
            {
                playerAwardCount = review.PlayerAwards.Count;
            }
            else if (season.Awards != null)
            {
                for (int index = 0; index < season.Awards.Results.Count; index++)
                {
                    if (season.Awards.Results[index].IncludesWinner(CurrentCareer.MyPlayer.PlayerId))
                        playerAwardCount++;
                }
            }

            int remainingWeeks = 0;
            if (CurrentCareer.CurrentOffseason != null && !CurrentCareer.CurrentOffseason.IsCompleted)
            {
                remainingWeeks = CurrentCareer.CurrentOffseason.TotalWeeks -
                                 CurrentCareer.CurrentOffseason.CurrentWeek + 1;
            }

            return new CareerSeasonProgressView(
                isQualified,
                postseason?.CanTeamPlayNextGame(CurrentCareer.MyPlayer.CurrentTeamId) == true,
                championTeamName,
                review?.IsPostseasonFinalized == true
                    ? review.PlayerTeamPostseasonResult
                    : postseason?.PlayerTeamResult ?? PlayerTeamPostseasonResult.DidNotQualify,
                postseasonGames,
                playerAwardCount,
                season.Settlement.SalaryIncome,
                season.Settlement.BonusIncome,
                remainingWeeks,
                season.Phase == SeasonPhase.Offseason &&
                CurrentCareer.CurrentContract.EndYear <= season.Year);
        }

        private LeagueHubView BuildLeagueHub()
        {
            if (CurrentCareer == null || _balance == null)
                return null;
            return new LeagueHubService(CurrentCareer, _balance).Build();
        }

        private TeamOverviewView BuildTeamOverview()
        {
            if (CurrentCareer == null || _balance == null)
                return null;
            return new TeamOverviewBuilder(_balance).Build(CurrentCareer);
        }

        private NextCareerGameView? BuildNextGameView(Player currentPlayer)
        {
            ScheduledGameState game = _seasonService?.NextPlayerGame;
            if (game == null)
                return null;
            int playerTeamId = CurrentCareer.MyPlayer.CurrentTeamId;
            bool isHome = game.HomeTeamId == playerTeamId;
            int opponentTeamId = isHome ? game.AwayTeamId : game.HomeTeamId;
            return new NextCareerGameView(
                game.GameId,
                GetGameDate(CurrentCareer.CurrentLeague.CurrentSeason.Year, game.Round),
                GetTeam(game.AwayTeamId).Name,
                GetTeam(game.HomeTeamId).Name,
                GetTeam(opponentTeamId).Name,
                isHome,
                game.PlannedPlayerRole,
                GetPlayerBattingOrder(game, currentPlayer));
        }

        private int GetPlayerBattingOrder(ScheduledGameState game, Player currentPlayer)
        {
            if (game.PlannedPlayerRole != PlayerGameRole.StartingBatter)
                return 0;

            TeamState team = GetTeam(CurrentCareer.MyPlayer.CurrentTeamId);
            var lineupAi = new ManagerLineupAi(_balance.ManagerLineup);
            Lineup lineup = CareerLineupPlan.BuildStartingLineup(
                team,
                currentPlayer,
                game.PlannedPlayerRole,
                lineupAi);
            return CareerLineupPlan.GetPlayerBattingOrder(lineup, currentPlayer.PlayerId);
        }

        private PositionCompetitionView[] BuildCompetition(
            TeamState team,
            PlayerState player,
            PlayerValueEvaluator evaluator)
        {
            int count = 1;
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                if (team.RosterCompetitors[index].Position == player.PrimaryPosition)
                    count++;
            }

            var result = new PositionCompetitionView[count];
            result[0] = new PositionCompetitionView(
                player.Name,
                evaluator.CalculatePositionValue(BuildStablePlayer()),
                true);
            int resultIndex = 1;
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                RosterCompetitorState competitor = team.RosterCompetitors[index];
                if (competitor.Position != player.PrimaryPosition)
                    continue;
                result[resultIndex++] = new PositionCompetitionView(
                    competitor.Name,
                    competitor.Overall,
                    false);
            }
            return result;
        }

        private UpcomingGameView[] BuildUpcomingGames(int playerTeamId)
        {
            var games = CurrentCareer.CurrentLeague.CurrentSeason.Schedule.Games;
            int count = 0;
            for (int index = 0; index < games.Count && count < 5; index++)
            {
                if (!games[index].IsCompleted && games[index].IncludesTeam(playerTeamId))
                    count++;
            }

            var result = new UpcomingGameView[count];
            int resultIndex = 0;
            for (int index = 0; index < games.Count && resultIndex < count; index++)
            {
                ScheduledGameState game = games[index];
                if (game.IsCompleted || !game.IncludesTeam(playerTeamId))
                    continue;
                bool isHome = game.HomeTeamId == playerTeamId;
                int opponentTeamId = isHome ? game.AwayTeamId : game.HomeTeamId;
                result[resultIndex] = new UpcomingGameView(
                    GetGameDate(CurrentCareer.CurrentLeague.CurrentSeason.Year, game.Round),
                    GetTeam(opponentTeamId).Name,
                    isHome,
                    resultIndex == 0);
                resultIndex++;
            }
            return result;
        }

        private int CountRemainingRegularSeasonGames(int playerTeamId)
        {
            IReadOnlyList<ScheduledGameState> games = CurrentCareer.CurrentLeague.CurrentSeason.Schedule?.Games;
            if (games == null)
                return 0;

            int count = 0;
            for (int index = 0; index < games.Count; index++)
            {
                if (!games[index].IsCompleted && games[index].IncludesTeam(playerTeamId))
                    count++;
            }
            return count;
        }

        private static PlayerGameLogState[] BuildRecentGames(PlayerSeasonStatisticsState statistics)
        {
            var result = new PlayerGameLogState[statistics.RecentGames.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = statistics.RecentGames[result.Length - 1 - index];
            return result;
        }

        private int CalculateRank(TeamSeasonRecordState playerRecord)
        {
            if (playerRecord == null)
                return 1;
            int rank = 1;
            var records = CurrentCareer.CurrentLeague.CurrentSeason.TeamRecords;
            for (int index = 0; index < records.Count; index++)
            {
                TeamSeasonRecordState other = records[index];
                if (other.TeamId == playerRecord.TeamId)
                    continue;
                if (other.WinningPercentage > playerRecord.WinningPercentage ||
                    Math.Abs(other.WinningPercentage - playerRecord.WinningPercentage) < 0.000001d &&
                    other.Wins > playerRecord.Wins)
                {
                    rank++;
                }
            }
            return rank;
        }

        private TeamState GetTeam(int teamId)
        {
            for (int index = 0; index < CurrentCareer.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = CurrentCareer.CurrentLeague.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private DateTime GetGameDate(int year, int round)
        {
            int playedDays = round - 1;
            int restDays = playedDays / _balance.CareerSeason.GamesBetweenRestDays;
            return new DateTime(
                    year,
                    _balance.CareerSeason.SeasonOpeningMonth,
                    _balance.CareerSeason.SeasonOpeningDay)
                .AddDays(playedDays + restDays);
        }
    }
}
