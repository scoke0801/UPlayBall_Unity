using System;
using System.Collections;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using Baseball.Simulation.Career;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>정규시즌 종료부터 정산까지 결과를 순서대로 공개하는 전체화면 결산 레이어다.</summary>
    public sealed partial class UI_Scene_CareerDashboard
    {
        private static readonly Color SilverColor = new(0.72f, 0.79f, 0.86f, 1f);

        private static bool IsSeasonReviewOverlayVisible(CareerDashboardView view)
        {
            if (view?.SeasonReview == null || view.SeasonReviewStep == SeasonReviewStep.Finished)
                return false;
            if (view.SeasonPhase == SeasonPhase.Postseason)
                return view.SeasonReviewStep != SeasonReviewStep.PostseasonInProgress;
            if (view.SeasonPhase == SeasonPhase.SeasonReview)
                return true;
            return view.SeasonPhase == SeasonPhase.Offseason &&
                   view.SeasonReviewStep == SeasonReviewStep.IncomeSettlement;
        }

        private static bool CanSkipSeasonReview(CareerDashboardView view)
        {
            return view != null && view.SeasonReviewStep is not (
                SeasonReviewStep.SeasonSummary or
                SeasonReviewStep.IncomeSettlement or
                SeasonReviewStep.Finished);
        }

        private void RenderSeasonReviewOverlay(CareerDashboardView view)
        {
            RectTransform blocker = CreateImage(
                "SeasonReviewBlocker",
                _content,
                new Color(0.003f, 0.016f, 0.029f, 0.99f),
                new Vector2(1920f, 920f),
                new Vector2(0f, -40f));
            blocker.GetComponent<Image>().raycastTarget = true;

            RectTransform root = CreateReviewPanel(
                "SeasonReviewRoot",
                blocker,
                new Vector2(1740f, 830f),
                new Vector2(0f, 5f));
            CanvasGroup canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            StartCoroutine(FadeInSeasonReview(canvasGroup));

            RenderSeasonReviewProgress(root, view.SeasonReviewStep);
            switch (view.SeasonReviewStep)
            {
                case SeasonReviewStep.RegularSeasonIntro:
                    RenderRegularSeasonIntro(root, view.SeasonReview);
                    break;
                case SeasonReviewStep.RegularSeasonResult:
                    RenderRegularSeasonResult(root, view.SeasonReview);
                    break;
                case SeasonReviewStep.PostseasonEntry:
                    RenderPostseasonEntry(root, view.SeasonReview);
                    break;
                case SeasonReviewStep.PostseasonRecap:
                    RenderPostseasonRecap(root, view);
                    break;
                case SeasonReviewStep.PostseasonResult:
                    RenderPostseasonResult(root, view.SeasonReview);
                    break;
                case SeasonReviewStep.Awards:
                    RenderAwards(root, view);
                    break;
                case SeasonReviewStep.SeasonSummary:
                    RenderSeasonSummary(root, view.SeasonReview);
                    break;
                case SeasonReviewStep.IncomeSettlement:
                    RenderIncomeSettlement(root, view.SeasonReview);
                    break;
            }

            if (CanSkipSeasonReview(view))
            {
                float actionButtonY = GetReviewAdvanceButtonY(root);
                Button skip = CreateButton(
                    "SkipSeasonReview",
                    root,
                    "연출 건너뛰기   ESC",
                    new Vector2(210f, 40f),
                    new Vector2(720f, actionButtonY),
                    PanelDarkColor,
                    out Text skipLabel);
                skipLabel.fontSize = 13;
                skipLabel.color = MutedColor;
                skip.onClick.AddListener(() =>
                {
                    _isSeasonReviewSkipConfirmationVisible = true;
                    Render();
                });
            }

            CareerUiSkin.Apply(root);
        }

        private static float GetReviewAdvanceButtonY(Transform root)
        {
            Transform advanceButton = root.Find("AdvanceSeasonReview");
            return advanceButton is RectTransform rect ? rect.anchoredPosition.y : -343f;
        }

        private static void RenderSeasonReviewProgress(Transform root, SeasonReviewStep step)
        {
            string[] labels = { "정규시즌", "포스트시즌", "시상식", "최종 결산" };
            int activeStage = GetReviewStage(step);
            CreateText(
                "ReviewProgressCount",
                root,
                $"{activeStage + 1}/4",
                12,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Vector2(80f, 24f),
                new Vector2(716f, 379f),
                SecondaryTextColor);

            for (int index = 0; index < labels.Length; index++)
            {
                float x = -570f + index * 380f;
                Color color = index < activeStage
                    ? RoleColor
                    : index == activeStage ? GoldColor : MutedColor;
                if (index < labels.Length - 1)
                {
                    CreateImage(
                        "ProgressLine_" + index,
                        root,
                        index < activeStage ? RoleColor : DividerColor,
                        new Vector2(285f, 2f),
                        new Vector2(x + 190f, 340f));
                }
                CreateImage(
                    "ProgressDot_" + index,
                    root,
                    color,
                    new Vector2(index == activeStage ? 18f : 12f, index == activeStage ? 18f : 12f),
                    new Vector2(x, 340f));
                CreateText(
                    "ProgressLabel_" + index,
                    root,
                    labels[index],
                    14,
                    index == activeStage ? FontStyle.Bold : FontStyle.Normal,
                    TextAnchor.MiddleCenter,
                    new Vector2(180f, 28f),
                    new Vector2(x, 310f),
                    color);
            }
            CreateImage("ProgressDivider", root, DividerColor, new Vector2(1620f, 1f), new Vector2(0f, 286f));
        }

        private static int GetReviewStage(SeasonReviewStep step)
        {
            return step switch
            {
                SeasonReviewStep.RegularSeasonIntro or
                    SeasonReviewStep.RegularSeasonResult or
                    SeasonReviewStep.PostseasonEntry => 0,
                SeasonReviewStep.PostseasonInProgress or
                    SeasonReviewStep.PostseasonRecap or
                    SeasonReviewStep.PostseasonResult => 1,
                SeasonReviewStep.Awards => 2,
                _ => 3
            };
        }

        private static IEnumerator FadeInSeasonReview(CanvasGroup canvasGroup)
        {
            const float duration = 0.24f;
            float elapsed = 0f;
            while (canvasGroup != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        private void RenderRegularSeasonIntro(Transform root, SeasonReviewSnapshot snapshot)
        {
            CreateText(
                "SceneEyebrow", root, "REGULAR SEASON COMPLETE", 16, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(700f, 30f), new Vector2(0f, 195f), AccentColor);
            Text title = CreateText(
                "SceneTitle", root, $"{snapshot.Year} 정규시즌 종료", 46, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(900f, 70f), new Vector2(0f, 125f), PrimaryTextColor);
            AddTextOutline(title, new Color(0.02f, 0.25f, 0.48f, 1f), 1.5f);
            CreateImage("TitleLine", root, AccentColor, new Vector2(180f, 3f), new Vector2(0f, 77f));
            CreateText(
                "Description",
                root,
                "모든 정규시즌 경기가 종료되었습니다.\n최종 순위와 포스트시즌 진출 결과를 확인합니다.",
                21,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(800f, 90f),
                new Vector2(0f, -5f),
                SecondaryTextColor);
            CreateReviewAdvanceButton(root, "시즌 리뷰 시작", new Vector2(0f, -245f));
        }

        private void RenderRegularSeasonResult(Transform root, SeasonReviewSnapshot snapshot)
        {
            SeasonStandingSnapshot player = FindPlayerStanding(snapshot);
            RectTransform hero = CreateReviewCard(root, "RegularSeasonHero", new Vector2(660f, 480f), new Vector2(-425f, 15f));
            string league = GetLeagueLabel(snapshot.LeagueLevel);
            CreateText(
                "League", hero, $"{snapshot.Year} {league} LEAGUE", 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(500f, 28f), new Vector2(0f, 175f), AccentColor);
            CreateText(
                "ResultLabel", hero, "정규시즌", 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(260f, 30f), new Vector2(0f, 120f), SecondaryTextColor);
            Text rank = CreateText(
                "Rank", hero, player.Rank.ToString(), 112, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(190f, 130f), new Vector2(-30f, 43f),
                player.Rank == 1 ? GoldColor : AccentColor);
            AddTextOutline(rank, player.Rank == 1 ? new Color(0.42f, 0.25f, 0.04f, 1f) : PanelDarkColor, 2f);
            CreateText(
                "RankSuffix", hero, "위", 34, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(70f, 60f), new Vector2(80f, 17f), PrimaryTextColor);
            CreateText(
                "TeamName", hero, snapshot.PlayerTeamName, 29, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(560f, 50f), new Vector2(0f, -60f), PrimaryTextColor);
            CreateText(
                "Record", hero,
                $"{player.Wins}승 {player.Losses}패{(player.Ties > 0 ? $" {player.Ties}무" : string.Empty)}   ·   승률 {player.WinningPercentage:.000}",
                20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(550f, 36f), new Vector2(0f, -108f), SecondaryTextColor);
            string seed = snapshot.PostseasonSeed > 0
                ? $"POSTSEASON SEED {snapshot.PostseasonSeed}   ·   포스트시즌 진출"
                : "정규시즌 종료   ·   포스트시즌 미진출";
            CreateText(
                "Seed", hero, seed, 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(570f, 40f), new Vector2(0f, -165f),
                snapshot.PostseasonSeed > 0 ? GoldColor : SecondaryTextColor);

            RectTransform standings = CreateReviewCard(root, "Standings", new Vector2(720f, 480f), new Vector2(360f, 15f));
            int visible = Math.Min(snapshot.Standings.Count, 8);
            for (int index = 0; index < visible; index++)
            {
                SeasonStandingSnapshot row = snapshot.Standings[index];
                float y = 157f - index * 47f;
                bool isPlayerTeam = row.TeamId == snapshot.PlayerTeamId;
                if (isPlayerTeam)
                {
                    CreateImage(
                        "PlayerStanding_" + index,
                        standings,
                        new Color(0.08f, 0.22f, 0.29f, 1f),
                        new Vector2(650f, 41f),
                        new Vector2(0f, y));
                }
                Color rankColor = row.Rank == 1 ? GoldColor : isPlayerTeam ? RoleColor : SecondaryTextColor;
                CreateText("Rank_" + index, standings, row.Rank.ToString(), 18, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(45f, 30f), new Vector2(-290f, y), rankColor);
                CreateText("Team_" + index, standings, row.TeamName, 16,
                    isPlayerTeam ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(300f, 30f), new Vector2(-90f, y), PrimaryTextColor);
                CreateText("Record_" + index, standings, $"{row.Wins}승 {row.Losses}패", 15, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(140f, 30f), new Vector2(175f, y), SecondaryTextColor);
                CreateText("Pct_" + index, standings, row.WinningPercentage.ToString(".000"), 14, FontStyle.Normal,
                    TextAnchor.MiddleRight, new Vector2(75f, 30f), new Vector2(265f, y), MutedColor);
            }
            CreatePlayerSeasonLine(root, snapshot, new Vector2(0f, -254f));
            CreateReviewAdvanceButton(
                root,
                snapshot.PostseasonSeed > 0 ? "포스트시즌으로" : "포스트시즌 결과 확인",
                new Vector2(0f, -343f));
        }

        private void RenderPostseasonEntry(Transform root, SeasonReviewSnapshot snapshot)
        {
            string title = snapshot.PostseasonSeed > 0 ? "포스트시즌 진출" : "포스트시즌 관전";
            Color accent = snapshot.PostseasonSeed > 0 ? GoldColor : SecondaryTextColor;
            CreateText(
                "SceneEyebrow", root, "POSTSEASON", 17, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(500f, 30f), new Vector2(0f, 200f), GoldColor);
            Text titleText = CreateText(
                "SceneTitle", root, title, 50, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(900f, 72f), new Vector2(0f, 122f), PrimaryTextColor);
            AddTextOutline(titleText, accent, 1.8f);
            CreateTeamBadge(
                root, snapshot.PlayerTeamName, GetTeamEmblemId(snapshot.PlayerTeamId),
                new Vector2(0f, 5f), 132f);
            CreateText(
                "Team", root, snapshot.PlayerTeamName, 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(620f, 45f), new Vector2(0f, -85f), PrimaryTextColor);
            string description = snapshot.PostseasonSeed > 0
                ? $"정규시즌 {snapshot.PostseasonSeed}번 시드로 포스트시즌에 진출했습니다.\n내 구단 경기는 일반 경기와 같은 흐름으로 진행합니다."
                : "포스트시즌 진출에는 실패했습니다.\n남은 대진을 진행해 리그 우승 구단을 확인합니다.";
            CreateText(
                "Description", root, description, 19, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(800f, 75f), new Vector2(0f, -155f), SecondaryTextColor);
            CreateReviewAdvanceButton(root, "포스트시즌 일정 확인", new Vector2(0f, -285f));
        }

        private void RenderPostseasonRecap(Transform root, CareerDashboardView view)
        {
            SeasonReviewSnapshot snapshot = view.SeasonReview;
            RectTransform bracket = CreateReviewCard(root, "PostseasonBracket", new Vector2(780f, 490f), new Vector2(-405f, 15f));
            CreateText(
                "Eyebrow", bracket, "POSTSEASON ROAD", 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(240f, 25f), new Vector2(-245f, 208f), GoldColor);
            int seriesCount = Math.Min(snapshot.PostseasonSeries.Count, 3);
            for (int index = 0; index < seriesCount; index++)
            {
                PostseasonSeriesReviewSnapshot series = snapshot.PostseasonSeries[index];
                float y = 126f - index * 126f;
                string round = series.Round == PostseasonRound.ChampionshipSeries ? "결승" : $"준결승 {index + 1}";
                Color winnerColor = series.WinnerTeamId == snapshot.PlayerTeamId ? GoldColor : AccentColor;
                CreateText(
                    "Round_" + index, bracket, round, 12, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(90f, 22f), new Vector2(-312f, y + 25f), MutedColor);
                CreateText(
                    "Higher_" + index, bracket,
                    $"{series.HigherSeedTeamName}   {series.HigherSeedWins}", 17, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(430f, 28f), new Vector2(-70f, y + 17f),
                    series.WinnerTeamId == series.HigherSeedTeamId ? winnerColor : SecondaryTextColor);
                CreateText(
                    "Lower_" + index, bracket,
                    $"{series.LowerSeedTeamName}   {series.LowerSeedWins}", 17, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(430f, 28f), new Vector2(-70f, y - 17f),
                    series.WinnerTeamId == series.LowerSeedTeamId ? winnerColor : SecondaryTextColor);
                CreateImage("Line_" + index, bracket, DividerColor, new Vector2(650f, 1f), new Vector2(0f, y - 45f));
            }

            RectTransform result = CreateReviewCard(root, "PostseasonGameCard", new Vector2(650f, 490f), new Vector2(425f, 15f));
            int revealed = view.RevealedPostseasonGameCount;
            if (snapshot.PlayerTeamPostseasonGames.Count == 0)
            {
                CreateText(
                    "NoPlayerGames", result, "내 구단 포스트시즌 경기 없음", 25, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(560f, 55f), new Vector2(0f, 55f), SecondaryTextColor);
                CreateText(
                    "Guide", result, "대진표에서 리그 포스트시즌 결과를 확인합니다.", 17, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(540f, 50f), new Vector2(0f, -20f), MutedColor);
            }
            else if (revealed == 0)
            {
                CreateText(
                    "Ready", result, "포스트시즌 경기 결과", 27, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(540f, 55f), new Vector2(0f, 70f), PrimaryTextColor);
                CreateText(
                    "Guide", result, $"{snapshot.PlayerTeamPostseasonGames.Count}경기의 결과를 한 장씩 공개합니다.",
                    17, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(540f, 45f), new Vector2(0f, 5f), SecondaryTextColor);
            }
            else
            {
                PostseasonGameReviewSnapshot game = snapshot.PlayerTeamPostseasonGames[revealed - 1];
                string round = game.Round == PostseasonRound.ChampionshipSeries ? "FINAL" : "SEMIFINAL";
                CreateText(
                    "GameEyebrow", result, $"POSTSEASON {round} · GAME {game.GameNumber}", 13, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(520f, 28f), new Vector2(0f, 180f), GoldColor);
                CreateText(
                    "Away", result, game.AwayTeamName, 20, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(360f, 36f), new Vector2(-90f, 105f), PrimaryTextColor);
                CreateText(
                    "AwayScore", result, game.AwayRuns.ToString(), 34, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(80f, 45f), new Vector2(225f, 105f), PrimaryTextColor);
                CreateText(
                    "Home", result, game.HomeTeamName, 20, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(360f, 36f), new Vector2(-90f, 55f), PrimaryTextColor);
                CreateText(
                    "HomeScore", result, game.HomeRuns.ToString(), 34, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(80f, 45f), new Vector2(225f, 55f), PrimaryTextColor);
                bool playerWon = game.AwayTeamId == snapshot.PlayerTeamId
                    ? game.AwayRuns > game.HomeRuns
                    : game.HomeRuns > game.AwayRuns;
                CreateText(
                    "Outcome", result, playerWon ? "승리" : "패배", 28, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(300f, 45f), new Vector2(0f, -20f),
                    playerWon ? RoleColor : SilverColor);
                string playerLine = game.HasPlayerLine
                    ? BuildPlayerGameLine(snapshot, game.PlayerLine)
                    : "개인 경기 기록은 시즌 포스트시즌 기록에서 확인할 수 있습니다.";
                CreateText(
                    "PlayerLine", result, $"{snapshot.PlayerName} · {GetSeasonReviewPositionLabel(snapshot.PlayerPosition)}\n{playerLine}",
                    16, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(560f, 70f), new Vector2(0f, -105f), SecondaryTextColor);
                CreateText(
                    "Counter", result, $"{revealed} / {snapshot.PlayerTeamPostseasonGames.Count}", 12, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(100f, 24f), new Vector2(0f, -190f), MutedColor);
            }

            string button = revealed < snapshot.PlayerTeamPostseasonGames.Count
                ? revealed == 0 ? "첫 경기 결과 확인" : "다음 경기 결과"
                : "포스트시즌 최종 결과";
            CreateReviewAdvanceButton(root, button, new Vector2(0f, -343f));
        }

        private void RenderPostseasonResult(Transform root, SeasonReviewSnapshot snapshot)
        {
            string eyebrow;
            string title;
            string description;
            Color accent;
            switch (snapshot.PlayerTeamPostseasonResult)
            {
                case PlayerTeamPostseasonResult.Champion:
                    eyebrow = $"{snapshot.Year} CHAMPION";
                    title = snapshot.IsIntegratedChampion ? "통합 우승" : "포스트시즌 우승";
                    description = snapshot.IsIntegratedChampion
                        ? "정규시즌 1위와 포스트시즌 우승을 모두 차지했습니다."
                        : "포스트시즌 마지막 승부를 이기고 정상에 올랐습니다.";
                    accent = GoldColor;
                    break;
                case PlayerTeamPostseasonResult.RunnerUp:
                    eyebrow = "POSTSEASON FINAL";
                    title = "포스트시즌 준우승";
                    description = $"결승까지 진출했지만 마지막 승부에서\n{snapshot.ChampionTeamName}에 패했습니다.";
                    accent = SilverColor;
                    break;
                case PlayerTeamPostseasonResult.SemifinalElimination:
                    eyebrow = "POSTSEASON RESULT";
                    title = "플레이오프 탈락";
                    description = $"정규시즌 {snapshot.PlayerTeamRank}위로 진출했지만\n결승에는 오르지 못했습니다.";
                    accent = new Color(0.36f, 0.56f, 0.7f, 1f);
                    break;
                default:
                    eyebrow = "POSTSEASON RESULT";
                    title = "포스트시즌 종료";
                    description = "정규시즌 성적으로 포스트시즌에 진출하지 못했습니다.";
                    accent = SecondaryTextColor;
                    break;
            }

            CreateText(
                "Eyebrow", root, eyebrow, 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(600f, 30f), new Vector2(0f, 210f), accent);
            Text titleText = CreateText(
                "Title", root, title, 54, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(900f, 75f), new Vector2(0f, 128f), PrimaryTextColor);
            AddTextOutline(titleText, accent, 2f);
            CreateTeamBadge(
                root, snapshot.PlayerTeamName, GetTeamEmblemId(snapshot.PlayerTeamId),
                new Vector2(0f, 0f), 138f);
            CreateText(
                "Team", root, snapshot.PlayerTeamName, 29, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(650f, 45f), new Vector2(0f, -92f), PrimaryTextColor);
            CreateText(
                "Description", root, description, 19, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(840f, 70f), new Vector2(0f, -157f), SecondaryTextColor);
            string achievement = snapshot.PlayerTeamPostseasonResult switch
            {
                PlayerTeamPostseasonResult.Champion => $"정규시즌 {snapshot.PlayerTeamRank}위   ·   포스트시즌 우승",
                PlayerTeamPostseasonResult.RunnerUp => $"정규시즌 {snapshot.PlayerTeamRank}위   ·   포스트시즌 준우승",
                _ => $"최종 {snapshot.PlayerTeamFinalRank}위   ·   {snapshot.Year} 우승팀 {snapshot.ChampionTeamName}"
            };
            CreateText(
                "Achievement", root, achievement, 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(760f, 35f), new Vector2(0f, -215f), accent);
            CreateReviewAdvanceButton(root, "시상식 결과", new Vector2(0f, -310f));
        }

        private void RenderAwards(Transform root, CareerDashboardView view)
        {
            SeasonReviewSnapshot snapshot = view.SeasonReview;
            int revealed = view.RevealedAwardCount;
            CreateText(
                "Eyebrow", root, $"{snapshot.Year} {GetLeagueLabel(snapshot.LeagueLevel)} LEAGUE AWARDS",
                15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(720f, 28f), new Vector2(0f, 212f), GoldColor);

            if (snapshot.PlayerAwards.Count == 0)
            {
                CreateText(
                    "Title", root, $"{snapshot.Year} 시즌 개인 성과", 42, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(800f, 62f), new Vector2(0f, 140f), PrimaryTextColor);
                CreateText(
                    "NoAward", root, "개인 수상 없음", 18, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(300f, 34f), new Vector2(0f, 91f), SecondaryTextColor);
                RenderPlayerStatisticsCard(root, snapshot, new Vector2(0f, -35f));
                CreateText(
                    "SeasonMessage", root, $"{snapshot.PlayerName}의 {snapshot.Year} 시즌을 마쳤습니다.",
                    17, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(650f, 35f), new Vector2(0f, -220f), SecondaryTextColor);
                CreateReviewAdvanceButton(root, "최종 시즌 요약", new Vector2(0f, -320f));
                return;
            }

            if (revealed == 0)
            {
                CreateText(
                    "Title", root, "개인 수상 발표", 46, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(800f, 70f), new Vector2(0f, 105f), PrimaryTextColor);
                CreateText(
                    "Guide", root, "시즌의 개인 수상 결과를 중요도 순서로 공개합니다.", 19, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(760f, 50f), new Vector2(0f, 28f), SecondaryTextColor);
                CreateReviewAdvanceButton(root, "첫 수상 결과 확인", new Vector2(0f, -245f));
                return;
            }

            SeasonAwardReviewSnapshot award = snapshot.PlayerAwards[revealed - 1];
            Text awardName = CreateText(
                "AwardName", root, award.AwardName, 52, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(900f, 75f), new Vector2(0f, 130f), GoldColor);
            AddTextOutline(awardName, new Color(0.42f, 0.25f, 0.04f, 1f), 1.8f);
            CreateText(
                "Player", root, snapshot.PlayerName, 31, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(650f, 45f), new Vector2(0f, 57f), PrimaryTextColor);
            CreateText(
                "TeamPosition", root,
                $"{snapshot.PlayerTeamName} · {GetSeasonReviewPositionLabel(snapshot.PlayerPosition)}", 16, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(600f, 30f), new Vector2(0f, 20f), SecondaryTextColor);
            RenderPlayerStatisticsCard(root, snapshot, new Vector2(0f, -105f));
            CreateText(
                "AwardCounter", root, $"{snapshot.PlayerAwards.Count}관왕 중 {revealed}번째 공개", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(300f, 26f), new Vector2(0f, -245f), MutedColor);
            string button = revealed < snapshot.PlayerAwards.Count ? "다음 수상 결과" : "수상 결과 종합";
            CreateReviewAdvanceButton(root, button, new Vector2(0f, -330f));
        }

        private void RenderSeasonSummary(Transform root, SeasonReviewSnapshot snapshot)
        {
            CreateText(
                "Eyebrow", root, $"{snapshot.Year} SEASON SUMMARY", 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(600f, 28f), new Vector2(0f, 220f), AccentColor);
            CreateText(
                "Team", root, snapshot.PlayerTeamName, 36, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(750f, 55f), new Vector2(0f, 170f), PrimaryTextColor);

            SeasonStandingSnapshot standing = FindPlayerStanding(snapshot);
            RenderSummaryCard(
                root, "RegularSummary", "정규시즌", $"{standing.Rank}위",
                $"{standing.Wins}승 {standing.Losses}패 · {standing.WinningPercentage:.000}",
                new Vector2(-510f, -16f), standing.Rank == 1 ? GoldColor : AccentColor);
            string postseason = GetPostseasonSummaryLabel(snapshot.PlayerTeamPostseasonResult);
            RenderSummaryCard(
                root, "PostseasonSummary", "포스트시즌", postseason,
                snapshot.PlayerTeamPostseasonResult == PlayerTeamPostseasonResult.Champion
                    ? "CHAMPION"
                    : $"우승팀 {snapshot.ChampionTeamName}",
                new Vector2(0f, -16f),
                snapshot.PlayerTeamPostseasonResult == PlayerTeamPostseasonResult.Champion ? GoldColor : SilverColor);
            string personal = snapshot.PlayerAwards.Count > 0
                ? $"{snapshot.PlayerAwards.Count}관왕"
                : "시즌 개인 성과";
            PlayerSeasonReviewStatistics stats = snapshot.PlayerStatistics;
            string personalDetail = stats.IsPitcher
                ? $"평균자책 {stats.EarnedRunAverage:0.00} · 탈삼진 {stats.PitchingStrikeouts}"
                : $"홈런 {stats.HomeRuns} · 타점 {stats.RunsBattedIn}";
            RenderSummaryCard(
                root, "PersonalSummary", "개인 성과", personal, personalDetail,
                new Vector2(510f, -16f), snapshot.PlayerAwards.Count > 0 ? GoldColor : AccentColor);

            CreateText(
                "Narrative", root, BuildSeasonNarrative(snapshot), 18, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(1300f, 70f), new Vector2(0f, -190f), SecondaryTextColor);
            CreateReviewAdvanceButton(root, "성장·수입 결산", new Vector2(0f, -310f));
        }

        private void RenderIncomeSettlement(Transform root, SeasonReviewSnapshot snapshot)
        {
            CreateText(
                "Eyebrow", root, $"{snapshot.Year} SEASON SETTLEMENT", 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(650f, 28f), new Vector2(0f, 224f), RoleColor);
            CreateText(
                "Title", root, "성장·수입 결산", 40, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(760f, 60f), new Vector2(0f, 178f), PrimaryTextColor);

            RectTransform income = CreateReviewCard(root, "IncomeSettlement", new Vector2(720f, 420f), new Vector2(-390f, -48f));
            CreateText(
                "Title", income, "시즌 수입", 19, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(240f, 35f), new Vector2(-205f, 170f), RoleColor);
            int incomeRows = Math.Min(snapshot.SettlementEntries.Count, 6);
            for (int index = 0; index < incomeRows; index++)
            {
                SettlementEntry entry = snapshot.SettlementEntries[index];
                float y = 120f - index * 43f;
                CreateText(
                    "IncomeLabel_" + index, income, GetSettlementLabel(entry.Type), 15, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(370f, 30f), new Vector2(-125f, y), SecondaryTextColor);
                CreateText(
                    "IncomeValue_" + index, income, "+" + FormatMoney(entry.Amount), 16, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(210f, 30f), new Vector2(220f, y), RoleColor);
            }
            CreateImage("IncomeLine", income, DividerColor, new Vector2(630f, 1f), new Vector2(0f, -150f));
            CreateText(
                "TotalLabel", income, "시즌 총수입", 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(270f, 34f), new Vector2(-165f, -178f), PrimaryTextColor);
            CreateText(
                "Total", income, "+" + FormatMoney(snapshot.SalaryIncome + snapshot.BonusIncome), 21, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(280f, 38f), new Vector2(175f, -178f), GoldColor);

            RectTransform growth = CreateReviewCard(root, "GrowthSettlement", new Vector2(720f, 420f), new Vector2(390f, -48f));
            CreateText(
                "Title", growth, "시즌 성장", 19, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(240f, 35f), new Vector2(-205f, 170f), AccentColor);
            int shown = 0;
            for (int index = 0; index < snapshot.AbilityChanges.Count && shown < 6; index++)
            {
                SeasonAbilityChangeSnapshot change = snapshot.AbilityChanges[index];
                if (change.Change == 0 || !IsPlayerAbility(snapshot, change.Ability))
                    continue;
                float y = 120f - shown * 43f;
                CreateText(
                    "AbilityLabel_" + shown, growth, GetAbilityLabel(change.Ability), 15, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(260f, 30f), new Vector2(-180f, y), SecondaryTextColor);
                CreateText(
                    "AbilityValue_" + shown, growth, $"{change.Before}  →  {change.After}", 18, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(250f, 30f), new Vector2(70f, y), PrimaryTextColor);
                CreateText(
                    "AbilityDelta_" + shown, growth, change.Change > 0 ? $"+{change.Change}" : change.Change.ToString(),
                    15, FontStyle.Bold, TextAnchor.MiddleRight, new Vector2(90f, 30f), new Vector2(280f, y),
                    change.Change > 0 ? RoleColor : SilverColor);
                shown++;
            }
            if (shown == 0)
            {
                CreateText(
                    "NoAbilityChange", growth, "이번 자연 성장에서 변동된 능력치가 없습니다.", 16, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(580f, 45f), new Vector2(0f, 70f), SecondaryTextColor);
            }
            CreateImage("EvaluationLine", growth, DividerColor, new Vector2(630f, 1f), new Vector2(0f, -150f));
            CreateText(
                "EvaluationLabel", growth, "계약 평가 반영", 17, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(300f, 32f), new Vector2(-150f, -178f), PrimaryTextColor);
            CreateText(
                "Evaluation", growth,
                snapshot.ContractEvaluationBonus > 0 ? $"+{snapshot.ContractEvaluationBonus} · 시장 가치 상승" : "변화 없음",
                17, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(330f, 32f), new Vector2(145f, -178f),
                snapshot.ContractEvaluationBonus > 0 ? RoleColor : SecondaryTextColor);
            CreateReviewAdvanceButton(root, "오프시즌 시작", new Vector2(0f, -343f));
        }

        private void RenderSeasonReviewSkipConfirmation(CareerDashboardView view)
        {
            RectTransform blocker = CreateImage(
                "SeasonReviewSkipBlocker", _content, new Color(0f, 0.01f, 0.02f, 0.86f),
                Vector2.zero, Vector2.zero, stretch: true);
            blocker.GetComponent<Image>().raycastTarget = true;
            RectTransform modal = CreatePanel(
                "SeasonReviewSkipModal", "SKIP PRESENTATION", "결산 연출 건너뛰기",
                new Vector2(760f, 390f), Vector2.zero);
            string destination = view.SeasonPhase == SeasonPhase.Postseason
                ? "포스트시즌 진행 화면으로 이동합니다."
                : "최종 시즌 요약으로 이동합니다.";
            CreateText(
                "Guide", modal,
                $"{destination}\n건너뛰어도 수상·보상·뉴스 결과는 정상 반영됩니다.",
                19, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(650f, 90f), new Vector2(0f, 45f), SecondaryTextColor);
            Button cancel = CreateButton(
                "CancelSkip", modal, "계속 보기   ESC", new Vector2(260f, 60f), new Vector2(-150f, -115f),
                PanelDarkColor, out Text cancelLabel);
            cancelLabel.color = SecondaryTextColor;
            cancel.onClick.AddListener(() =>
            {
                _isSeasonReviewSkipConfirmationVisible = false;
                Render();
            });
            Button confirm = CreateButton(
                "ConfirmSkip", modal, "건너뛰기   ENTER", new Vector2(280f, 60f), new Vector2(155f, -115f),
                new Color(0.18f, 0.26f, 0.32f, 1f), out _);
            confirm.onClick.AddListener(() =>
            {
                _isSeasonReviewSkipConfirmationVisible = false;
                _manager.SkipSeasonReview();
            });
            CareerUiSkin.Apply(modal);
        }

        private void CreateReviewAdvanceButton(Transform root, string label, Vector2 position)
        {
            Button button = CreateButton(
                "AdvanceSeasonReview", root, label + "   ENTER",
                new Vector2(440f, 66f), position, new Color(0.055f, 0.34f, 0.27f, 1f), out Text text);
            text.fontSize = 21;
            button.onClick.AddListener(() =>
            {
                button.interactable = false;
                _manager.AdvanceSeasonReview();
            });
        }

        private static RectTransform CreateReviewCard(Transform parent, string name, Vector2 size, Vector2 position)
        {
            return CreateReviewPanel(name, parent, size, position);
        }

        private static RectTransform CreateReviewPanel(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            RectTransform panel = CreateRect(name, parent, size, position);
            RectTransform frame = CreateImage(
                "DecorativeFrame", panel, Color.white, Vector2.zero, Vector2.zero, stretch: true);
            MarkVisual(frame, CareerUiVisualRole.DecorativeFrame);
            return panel;
        }

        private static void RenderSummaryCard(
            Transform root,
            string name,
            string label,
            string value,
            string detail,
            Vector2 position,
            Color accent)
        {
            RectTransform card = CreateReviewCard(root, name, new Vector2(450f, 270f), position);
            CreateImage("Accent", card, accent, new Vector2(160f, 3f), new Vector2(0f, 118f));
            CreateText("Label", card, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(320f, 30f), new Vector2(0f, 82f), SecondaryTextColor);
            CreateText("Value", card, value, 34, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(390f, 55f), new Vector2(0f, 25f), accent);
            CreateText("Detail", card, detail, 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(390f, 45f), new Vector2(0f, -53f), SecondaryTextColor);
        }

        private static void RenderPlayerStatisticsCard(
            Transform root,
            SeasonReviewSnapshot snapshot,
            Vector2 position)
        {
            RectTransform card = CreateReviewCard(root, "PlayerStatistics", new Vector2(860f, 190f), position);
            PlayerSeasonReviewStatistics stats = snapshot.PlayerStatistics;
            if (stats.IsPitcher)
            {
                RenderStat(card, "Era", "평균자책", stats.EarnedRunAverage.ToString("0.00"), -285f);
                RenderStat(card, "WinLoss", "승-패", $"{stats.Wins}-{stats.Losses}", -95f);
                RenderStat(card, "InningsPitched", "이닝", $"{stats.OutsRecorded / 3}.{stats.OutsRecorded % 3}", 95f);
                RenderStat(card, "Strikeouts", "탈삼진", stats.PitchingStrikeouts.ToString(), 285f);
            }
            else
            {
                RenderStat(card, "BattingAverage", "타율", stats.BattingAverage.ToString(".000"), -285f);
                RenderStat(card, "Ops", "OPS", stats.OnBasePlusSlugging.ToString(".000"), -95f);
                RenderStat(card, "HomeRuns", "홈런", stats.HomeRuns.ToString(), 95f);
                RenderStat(card, "RunsBattedIn", "타점", stats.RunsBattedIn.ToString(), 285f);
            }
        }

        private static void RenderStat(
            Transform parent,
            string name,
            string label,
            string value,
            float x)
        {
            CreateText(name + "Label", parent, label, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(130f, 25f), new Vector2(x, 43f), MutedColor);
            CreateText(name + "Value", parent, value, 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(150f, 43f), new Vector2(x, -10f), PrimaryTextColor);
        }

        private static void CreatePlayerSeasonLine(
            Transform root,
            SeasonReviewSnapshot snapshot,
            Vector2 position)
        {
            PlayerSeasonReviewStatistics stats = snapshot.PlayerStatistics;
            string line = stats.IsPitcher
                ? $"평균자책 {stats.EarnedRunAverage:0.00}  |  {stats.Wins}승 {stats.Losses}패  |  탈삼진 {stats.PitchingStrikeouts}"
                : $"타율 {stats.BattingAverage:.000}  |  OPS {stats.OnBasePlusSlugging:.000}  |  홈런 {stats.HomeRuns}  |  타점 {stats.RunsBattedIn}";
            CreateText(
                "PlayerSeasonLine", root,
                $"{snapshot.PlayerName} · {GetSeasonReviewPositionLabel(snapshot.PlayerPosition)}    {line}",
                16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(1100f, 40f), position, SecondaryTextColor);
        }

        private static string GetSeasonReviewPositionLabel(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                PlayerPosition.StartingPitcher => "선발투수",
                PlayerPosition.ReliefPitcher => "구원투수",
                _ => "미정"
            };
        }

        private static SeasonStandingSnapshot FindPlayerStanding(SeasonReviewSnapshot snapshot)
        {
            for (int index = 0; index < snapshot.Standings.Count; index++)
            {
                if (snapshot.Standings[index].TeamId == snapshot.PlayerTeamId)
                    return snapshot.Standings[index];
            }
            return default;
        }

        private static string BuildPlayerGameLine(SeasonReviewSnapshot snapshot, PlayerGameLogState line)
        {
            bool isPitcher = snapshot.PlayerPosition is Baseball.Core.Players.PlayerPosition.StartingPitcher or
                Baseball.Core.Players.PlayerPosition.ReliefPitcher;
            return isPitcher
                ? $"{line.OutsRecorded / 3}.{line.OutsRecorded % 3}이닝 · {line.EarnedRuns}자책 · {line.Strikeouts}탈삼진"
                : $"{line.AtBats}타수 {line.Hits}안타 · {line.HomeRuns}홈런 · {line.RunsBattedIn}타점";
        }

        private static string GetPostseasonSummaryLabel(PlayerTeamPostseasonResult result)
        {
            return result switch
            {
                PlayerTeamPostseasonResult.Champion => "우승",
                PlayerTeamPostseasonResult.RunnerUp => "준우승",
                PlayerTeamPostseasonResult.SemifinalElimination => "준결승 탈락",
                _ => "미진출"
            };
        }

        private static string BuildSeasonNarrative(SeasonReviewSnapshot snapshot)
        {
            return snapshot.PlayerTeamPostseasonResult switch
            {
                PlayerTeamPostseasonResult.Champion when snapshot.IsIntegratedChampion =>
                    $"{snapshot.PlayerTeamName}는 정규시즌 1위에 이어 포스트시즌까지 제패하며 통합 우승으로 시즌을 마쳤습니다.",
                PlayerTeamPostseasonResult.Champion =>
                    $"{snapshot.PlayerTeamName}는 정규시즌 {snapshot.PlayerTeamRank}위에서 출발해 포스트시즌 우승을 차지했습니다.",
                PlayerTeamPostseasonResult.RunnerUp =>
                    $"{snapshot.PlayerTeamName}는 정규시즌 {snapshot.PlayerTeamRank}위를 기록하고 결승까지 진출했지만, {snapshot.ChampionTeamName}에 패해 준우승으로 시즌을 마쳤습니다.",
                PlayerTeamPostseasonResult.SemifinalElimination =>
                    $"{snapshot.PlayerTeamName}는 정규시즌 {snapshot.PlayerTeamRank}위로 포스트시즌에 진출했지만 준결승에서 시즌을 마쳤습니다.",
                _ =>
                    $"{snapshot.PlayerTeamName}는 정규시즌 {snapshot.PlayerTeamRank}위로 시즌을 마쳤고, {snapshot.ChampionTeamName}가 포스트시즌 우승을 차지했습니다."
            };
        }

        private static string GetSettlementLabel(SettlementEntryType type)
        {
            return type switch
            {
                SettlementEntryType.Salary => "기본 연봉",
                SettlementEntryType.PerformanceBonus => "계약 성과 보너스",
                SettlementEntryType.AwardBonus => "개인 수상 보너스",
                SettlementEntryType.PostseasonBonus => "포스트시즌 진출 보너스",
                SettlementEntryType.ChampionshipBonus => "우승 보너스",
                _ => "시즌 보너스"
            };
        }

        private static bool IsPlayerAbility(SeasonReviewSnapshot snapshot, PlayerAbility ability)
        {
            return snapshot.PlayerStatistics.IsPitcher
                ? PlayerAbilityCatalog.IsPitcherAbility(ability)
                : PlayerAbilityCatalog.IsBatterAbility(ability);
        }

        private static string GetAbilityLabel(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "교타력",
                PlayerAbility.Power => "장타력",
                PlayerAbility.Speed => "주력",
                PlayerAbility.Arm => "송구",
                PlayerAbility.Defense => "수비력",
                PlayerAbility.BatterMental or PlayerAbility.PitcherMental => "정신력",
                PlayerAbility.Stamina => "체력",
                PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위",
                PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구력",
                _ => ability.ToString()
            };
        }
    }
}
