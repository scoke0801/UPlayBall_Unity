using System;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>은퇴 선언·확정과 불변 회고 스냅샷 공개를 CareerManager에 연결한다.</summary>
    public sealed partial class CareerManager
    {
        public RetirementRecapSnapshot RetirementRecap => CurrentCareer?.Retirement?.Snapshot;
        public bool HasRetirementRecap => RetirementRecap != null;
        public bool IsFinalSeasonDeclared => CurrentCareer?.Retirement?.IsFinalSeasonDeclared == true;

        public event Action RetirementRecapReady;

        /// <summary>현재 시즌을 마지막 시즌으로 선언하고 플레이어의 직접 선택을 기억에 남긴다.</summary>
        public bool DeclareFinalSeason()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            if (season.Phase is not (SeasonPhase.Preseason or SeasonPhase.RegularSeason))
                return Fail("마지막 시즌 선언은 정규 시즌이 끝나기 전에 할 수 있습니다.");

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
