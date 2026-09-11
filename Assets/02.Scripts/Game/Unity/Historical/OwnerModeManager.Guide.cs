using System;
using Baseball.Game.Guide;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        /// <summary>현재 적용된 로스터·프리셋의 공개 검증으로 안내 상태를 갱신한다.</summary>
        public GuideProgressState RefreshGuideProgress()
        {
            var runtime = RequireRuntime();
            var season = runtime.ManagerMode.LiveSeason;
            string scope = season.SeasonId + ":" + (season.NextPlayerGame?.GameId.ToString() ?? "end");
            var preset = runtime.ManagerMode.GetSelectedLineupPreset();
            var goals = OwnerGuideGoalProvider.Create(BuildRosterStatus().Validation,
                ValidateLineupPreset(preset), season.NextPlayerGame != null, runtime.GuideProgress.PendingMatchKey);
            runtime.GuideProgress.Reconcile(scope, season.CurrentWeekIndex, goals, season.SeasonId);
            return runtime.GuideProgress;
        }

        /// <summary>안내 선택 저장 실패 시 안내 이력만 되돌리고 경기 상태는 건드리지 않는다.</summary>
        public void ChangeGuideProgress(Action<GuideProgressState> change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            var runtime = RequireRuntime();
            var previous = runtime.GuideProgress.Capture();
            // 안내 선택은 선수단 초안을 무효화하는 RuntimeChanged를 발행하지 않는다.
            try { change(runtime.GuideProgress); _saveStore.Save(_saveAdapter.CreateSaveData(runtime)); }
            catch { runtime.RestoreGuideProgress(previous); throw; }
        }

        /// <summary>표현 계층이 공식 결과 공개를 마친 뒤 최근 경기 점수를 안내 기록에 남긴다.</summary>
        public void PublishGuideMatchResult()
        {
            if (LastMatch == null) return;
            var match = LastMatch.Match;
            ChangeGuideProgress(progress => progress.PublishMatch(
                match.Input.SeasonId + ":" + match.Input.GameId, match.HomeBoxScore.Runs, match.AwayBoxScore.Runs));
        }
    }
}
