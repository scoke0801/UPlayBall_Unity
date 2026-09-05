using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerDashboard
    {
        private Text _fastForwardDateText;
        private Text _fastForwardProgressText;
        private Text _fastForwardTeamText;
        private Text _fastForwardPlayerText;
        private Text _fastForwardNewsText;
        private Image _fastForwardProgressFill;
        private int _lastRenderedFastForwardSteps = -1;

        private void AdvanceSeasonFastForwardFrame()
        {
            if (!_manager.IsSeasonFastForwardRunning)
            {
                _isSeasonFastForwardProgressVisible = false;
                Render();
                return;
            }

            bool succeeded = _manager.AdvanceSeasonFastForwardFrame();
            if (!succeeded || !_manager.IsSeasonFastForwardRunning)
            {
                _isSeasonFastForwardProgressVisible = false;
                Render();
                return;
            }

            UpdateSeasonFastForwardProgress(_manager.SeasonFastForwardProgress);
        }

        private void StopSeasonFastForward()
        {
            _manager.StopSeasonFastForward();
            _isSeasonFastForwardProgressVisible = false;
            Render();
        }

        private void RenderSeasonFastForwardProgress(SeasonFastForwardProgressView view)
        {
            _lastRenderedFastForwardSteps = -1;
            RectTransform blocker = CreateImage(
                "SeasonFastForwardBlocker",
                _content,
                CareerUiTheme.InputBlocker,
                Vector2.zero,
                Vector2.zero,
                stretch: true);
            blocker.GetComponent<Image>().raycastTarget = true;
            MarkVisual(blocker, CareerUiVisualRole.InputBlocker);

            RectTransform modal = CreatePanel(
                "SeasonFastForwardModal",
                "FAST FORWARD",
                view.Progress.TargetPhase == SeasonPhase.RegularSeason
                    ? "정규시즌 진행 중"
                    : "포스트시즌 진행 중",
                new Vector2(900f, 590f),
                Vector2.zero);

            _fastForwardDateText = CreateText(
                "Date",
                modal,
                string.Empty,
                27,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(760f, 48f),
                new Vector2(0f, 142f),
                PrimaryTextColor);

            RectTransform progressTrack = CreateImage(
                "ProgressTrack",
                modal,
                PanelDarkColor,
                new Vector2(720f, 22f),
                new Vector2(0f, 88f));
            RectTransform progressFill = CreateImage(
                "ProgressFill",
                progressTrack,
                BrightAccentColor,
                new Vector2(708f, 12f),
                Vector2.zero);
            _fastForwardProgressFill = progressFill.GetComponent<Image>();
            _fastForwardProgressFill.type = Image.Type.Filled;
            _fastForwardProgressFill.fillMethod = Image.FillMethod.Horizontal;
            _fastForwardProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            CareerUiSkin.ApplyProgressBar(
                progressTrack.GetComponent<Image>(),
                _fastForwardProgressFill);

            _fastForwardProgressText = CreateText(
                "Progress",
                modal,
                string.Empty,
                16,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(760f, 34f),
                new Vector2(0f, 55f),
                SecondaryTextColor);

            RectTransform summary = CreateImage(
                "Summary",
                modal,
                CareerUiTheme.SurfaceSubtle,
                new Vector2(720f, 150f),
                new Vector2(0f, -35f));
            CreateDivider(
                "SummaryDivider",
                summary,
                DividerColor,
                new Vector2(2f, 116f),
                Vector2.zero);
            _fastForwardTeamText = CreateText(
                "Team",
                summary,
                string.Empty,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(330f, 110f),
                new Vector2(-180f, 0f),
                PrimaryTextColor);
            _fastForwardPlayerText = CreateText(
                "Player",
                summary,
                string.Empty,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(330f, 110f),
                new Vector2(180f, 0f),
                PrimaryTextColor);

            _fastForwardNewsText = CreateText(
                "LatestNews",
                modal,
                string.Empty,
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(760f, 34f),
                new Vector2(0f, -128f),
                GoldColor);

            CreateText(
                "SafeStopGuide",
                modal,
                "완료된 월드 라운드만 확정합니다. 중단하면 현재 안전 지점까지의 결과가 유지됩니다.",
                14,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(760f, 32f),
                new Vector2(0f, -163f),
                MutedColor);
            Button stop = CreateButtonWithKeyPrompt(
                "Stop",
                modal,
                "현재 라운드까지 진행 후 중단",
                "ESC",
                new Vector2(430f, 62f),
                new Vector2(0f, -218f),
                PanelDarkColor,
                out Text stopLabel);
            stopLabel.color = SecondaryTextColor;
            stop.onClick.AddListener(StopSeasonFastForward);

            UpdateSeasonFastForwardProgress(view);
        }

        private void UpdateSeasonFastForwardProgress(SeasonFastForwardProgressView view)
        {
            if (_fastForwardDateText == null)
                return;

            SeasonFastForwardStepResult progress = view.Progress;
            if (_lastRenderedFastForwardSteps == progress.CompletedSteps)
                return;
            _lastRenderedFastForwardSteps = progress.CompletedSteps;
            float ratio = GetProgressRatio(progress);
            _fastForwardProgressFill.fillAmount = ratio;
            _fastForwardDateText.text = progress.LastCompletedRound > 0
                ? $"{view.CurrentDate:M월 d일} · {progress.LastCompletedRound}라운드 완료"
                : $"{view.CurrentDate:M월 d일} · 준비 중";
            _fastForwardProgressText.text = progress.HasKnownTotal
                ? $"월드 경기 {progress.ProcessedWorldGames:N0} / {progress.TotalWorldGames:N0}"
                : $"포스트시즌 {progress.CompletedSteps:N0}경기 진행";
            _fastForwardTeamText.text =
                $"{view.TeamName}\n{view.TeamRank}위  {view.TeamWins}승 {view.TeamLosses}패 {view.TeamTies}무";
            _fastForwardPlayerText.text = BuildPlayerProgressText(view);
            _fastForwardNewsText.text = string.IsNullOrEmpty(view.LatestNewsHeadline)
                ? "주요 뉴스 집계 중"
                : $"최근 뉴스 · {view.LatestNewsHeadline}";
        }

        private static float GetProgressRatio(SeasonFastForwardStepResult progress)
        {
            if (progress.IsCompleted)
                return 1f;
            if (progress.HasKnownTotal)
                return Mathf.Clamp01((float)progress.ProcessedWorldGames / progress.TotalWorldGames);
            return progress.CompletedSteps <= 0
                ? 0f
                : 1f - 1f / (progress.CompletedSteps + 1f);
        }

        private static string BuildPlayerProgressText(SeasonFastForwardProgressView view)
        {
            PlayerSeasonStatisticsView statistics = view.Statistics;
            return statistics.IsPitcher
                    ? $"{view.PlayerName}\n{statistics.Wins}승 {statistics.Losses}패  평균자책 {statistics.EarnedRunAverage:0.00}  탈삼진 {statistics.PitchingStrikeouts}"
                    : $"{view.PlayerName}\n타율 {statistics.BattingAverage:0.000}  홈런 {statistics.HomeRuns}  타점 {statistics.RunsBattedIn}";
        }
    }
}
