using System;
using System.Collections.Generic;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>은퇴 선언·확정과 불변 회고 스냅샷 공개를 CareerManager에 연결한다.</summary>
    public sealed partial class CareerManager
    {
        public RetirementRecapSnapshot RetirementRecap => CurrentCareer?.Retirement?.Snapshot;
        public bool HasRetirementRecap => RetirementRecap != null;
        public bool IsFinalSeasonDeclared => CurrentCareer?.Retirement?.IsFinalSeasonDeclared == true;

        /// <summary>자발적 은퇴를 선택할 수 있는 최소 나이다. AI 선수의 은퇴 판정 기준과 같은 값을 쓴다.</summary>
        public int RetirementEligibleAge => _balance?.PlayerLifecycle.RetirementMinimumAge ?? 0;

        public bool IsRetirementEligible =>
            CurrentCareer != null && _balance != null &&
            CurrentCareer.MyPlayer.Age >= _balance.PlayerLifecycle.RetirementMinimumAge;

        /// <summary>계약 오퍼를 비교하는 중이며 그 자리에서 은퇴를 확정할 수 있는지다.</summary>
        public bool CanRetireFromContractOffers =>
            IsRetirementEligible &&
            !CurrentCareer.Retirement.IsRetired &&
            _seasonTransitionService?.Step is SeasonTransitionStep.CurrentTeamNegotiation or
                SeasonTransitionStep.ContractOffers;

        public event Action RetirementRecapReady;

        /// <summary>현재 시즌을 마지막 시즌으로 선언하고 플레이어의 직접 선택을 기억에 남긴다.</summary>
        public bool DeclareFinalSeason()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            if (season.Phase is not (SeasonPhase.Preseason or SeasonPhase.RegularSeason))
                return Fail("마지막 시즌 선언은 정규 시즌이 끝나기 전에 할 수 있습니다.");
            if (!IsRetirementEligible)
                return Fail($"자발적 마지막 시즌 선언은 {RetirementEligibleAge}세부터 할 수 있습니다.");

            try
            {
                CurrentCareer.Retirement.DeclareFinalSeason(season.SeasonId);
                CurrentCareer.Retirement.MemoryLog.Append(new CareerMemoryRecord(
                    $"final_season_declared:{season.SeasonId}",
                    CurrentCareer.MyPlayerId,
                    season.Year,
                    GetRetirementDateIndex(season),
                    CurrentCareer.MyPlayer.CurrentTeamId,
                    CareerMemoryType.FinalSeasonDeclared,
                    "career.memory.final_season_declared.title",
                    "career.memory.final_season_declared.narrative",
                    0,
                    string.Empty,
                    0,
                    78,
                    92,
                    100,
                    65,
                    92,
                    Array.Empty<MemoryStatValue>(),
                    new[] { "player_choice", "retirement" },
                    "career_retirement"));
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>시즌 결산 또는 오프시즌에서 선수의 자발적·의료·미계약 은퇴를 확정한다.</summary>
        public bool RetireImmediately(RetirementReason reason = RetirementReason.Voluntary)
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            if (_activeMatch != null)
                return Fail("준비하거나 진행 중인 경기를 먼저 마쳐야 합니다.");
            SeasonPhase phase = CurrentCareer.CurrentLeague.CurrentSeason.Phase;
            if (phase is not (SeasonPhase.SeasonReview or SeasonPhase.Offseason))
                return Fail("즉시 은퇴는 시즌 결산 또는 오프시즌에 확정할 수 있습니다.");
            if (reason == RetirementReason.Voluntary && !IsRetirementEligible)
                return Fail($"자발적 은퇴는 {RetirementEligibleAge}세부터 선택할 수 있습니다.");

            try
            {
                CompleteRetirement(reason);
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>계약 오퍼 단계에서 남은 제안을 모두 거절하고 그 자리에서 현역 은퇴를 확정한다.</summary>
        /// <remarks>
        /// 만료된 계약의 다음 조건을 눈앞에 두고 "한 시즌 더 뛸 값어치가 있는가"를 저울질하는 순간이
        /// 은퇴 결정의 자연스러운 자리다. 이 단계의 시즌 전환은 아직 커리어 상태를 바꾸지 않았으므로
        /// (BeginTransition은 계획만 세운다) 전환을 버리고 은퇴만 확정하면 세이브가 반쯤 넘어가지 않는다.
        /// </remarks>
        public bool RetireFromContractOffers()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            if (_seasonTransitionService?.Step is not SeasonTransitionStep.CurrentTeamNegotiation and
                not SeasonTransitionStep.ContractOffers)
                return Fail("계약 오퍼를 확인하는 중에만 이 자리에서 은퇴할 수 있습니다.");
            if (!IsRetirementEligible && !_seasonTransitionService.IsUnsignedRetirementRequired)
                return Fail($"자발적 은퇴는 {RetirementEligibleAge}세부터 선택할 수 있습니다.");

            try
            {
                RecordDeclinedOffers(_seasonTransitionService.RenewalOffers);
                CompleteRetirement(_seasonTransitionService.IsUnsignedRetirementRequired
                    ? RetirementReason.Unsigned
                    : RetirementReason.Voluntary);
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>은퇴로 흘려보낸 제안들을 회고에 남긴다. 어떤 조건을 마다했는지가 마지막 선택의 무게다.</summary>
        private void RecordDeclinedOffers(IReadOnlyList<ContractOffer> offers)
        {
            var recap = new RetirementRecapService(_balance);
            for (int index = 0; index < offers.Count; index++)
            {
                ContractOffer offer = offers[index];
                recap.RecordContractChoice(
                    CurrentCareer,
                    offer,
                    isAccepted: false,
                    offer.Team.TeamId == CurrentCareer.MyPlayer.CurrentTeamId);
            }
        }

        private void TryCompleteDeclaredRetirement()
        {
            if (CurrentCareer?.Retirement?.IsFinalSeasonDeclared != true ||
                CurrentCareer.Retirement.IsRetired)
            {
                return;
            }

            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            if (CurrentCareer.Retirement.DeclaredFinalSeasonId != season.SeasonId ||
                season.Phase is not (SeasonPhase.SeasonReview or SeasonPhase.Offseason))
            {
                return;
            }
            CompleteRetirement(RetirementReason.DeclaredFinalSeason);
        }

        private void CompleteRetirement(RetirementReason reason)
        {
            if (CurrentCareer.Retirement.IsRetired)
                throw new InvalidOperationException("이미 은퇴가 확정되었습니다.");
            PlayerState player = CurrentCareer.MyPlayer;
            if (player.CareerStatus != PlayerCareerStatus.ActiveRoster)
                throw new InvalidOperationException("현재 은퇴 확정 경로에는 마지막 소속 구단이 필요합니다.");

            LeagueId lastLeagueId = player.CurrentLeagueId;
            int lastTeamId = player.CurrentTeamId;
            int seasonId = CurrentCareer.CurrentLeague.CurrentSeason.SeasonId;
            ExpectedRole previousRole = CurrentCareer.CurrentExpectedRole;
            RetirementRecapSnapshot snapshot = new RetirementRecapService(_balance)
                .CreateSnapshot(CurrentCareer, reason);
            CurrentCareer.Retirement.Complete(snapshot, lastLeagueId, lastTeamId);
            CurrentCareer.World.RetirePlayer(
                player.PlayerId,
                seasonId,
                previousRole,
                GetRetirementReasonKey(reason));

            _seasonService = null;
            _postseasonService = null;
            _seasonTransitionService = null;
            _activeMatch = null;
            _lastSeasonAutoCompletion = null;
            LastError = string.Empty;
            RetirementRecapReady?.Invoke();
        }

        private static int GetRetirementDateIndex(SeasonState season)
        {
            int result = 0;
            if (season.Schedule == null)
                return result;
            for (int index = 0; index < season.Schedule.Games.Count; index++)
            {
                ScheduledGameState game = season.Schedule.Games[index];
                if (game.IsCompleted && game.Round > result)
                    result = game.Round;
            }
            return result;
        }

        private static string GetRetirementReasonKey(RetirementReason reason)
        {
            return reason switch
            {
                RetirementReason.DeclaredFinalSeason => "declared_final_season",
                RetirementReason.Medical => "medical",
                RetirementReason.Unsigned => "unsigned",
                _ => "voluntary"
            };
        }
    }
}
