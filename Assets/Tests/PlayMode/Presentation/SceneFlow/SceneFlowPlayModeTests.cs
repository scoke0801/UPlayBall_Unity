using System.Collections;
using System.Linq;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.Career;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Baseball.Tests.PlayMode.Presentation.SceneFlow
{
    /// <summary>
    /// 실제 Player Loop에서 Boot부터 첫 콘텐츠 Scene까지의 전환을 검증한다.
    /// </summary>
    public sealed class SceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Boot_Loading을거쳐Management로진입한다()
        {
            GameManager.EnsureExists().EnsureManager<SceneLoadManager>("SceneLoadManager");
            UnitySceneManager.LoadScene(SceneCatalog.BootSceneName);

            float timeoutAt = Time.realtimeSinceStartup + 10f;
            while (UnitySceneManager.GetActiveScene().name != SceneCatalog.ManagementSceneName &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(
                UnitySceneManager.GetActiveScene().name,
                Is.EqualTo(SceneCatalog.ManagementSceneName));
            Assert.That(SceneLoadManager.Instance, Is.Not.Null);
            Assert.That(SceneLoadManager.Instance.CurrentSceneId, Is.EqualTo(SceneId.Management));
            Assert.That(SceneLoadManager.Instance.IsLoading, Is.False);
            Assert.That(SceneLoadManager.Instance.LoadState, Is.EqualTo(SceneLoadState.Completed));
            UI_Scene_NewGame newGameScreen =
                Object.FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include);
            Assert.That(newGameScreen, Is.Not.Null);
            Assert.That(newGameScreen.IsVisible, Is.True);
            Assert.That(
                newGameScreen.GetComponentsInChildren<Text>(true).Any(text => text.text == "커리어를 선택하세요"),
                Is.True,
                "Management 진입 직후에는 타이틀 화면이 보여야 한다.");

            NewGameManager newGameManager = NewGameManager.Instance;
            newGameManager.StartPlayerCareerCreation();
            yield return null;

            Assert.That(
                newGameScreen.GetComponentsInChildren<Text>(true).Any(text => text.text == "1단계 · 기본 정보"),
                Is.True);
            Assert.That(
                newGameManager.SubmitBasicInformation(
                    "테스트 선수", PlayerType.Batter, Handedness.Right, Handedness.Right),
                Is.True);
            Assert.That(
                newGameManager.SubmitCreationPosition(PlayerPosition.Shortstop, PitcherRole.Starter),
                Is.True);
            yield return null;

            CareerAttributeAllocationRule rule = newGameManager.CurrentCreationAttributeRule;
            Button[] allocationButtons = newGameScreen.GetComponentsInChildren<Button>(true);
            Assert.That(
                allocationButtons.Count(button => button.name.StartsWith("Preset_")),
                Is.EqualTo(newGameManager.CreationAttributeAllocationPresets.Count));
            Assert.That(
                allocationButtons.Count(button => button.name.StartsWith("Plus_")),
                Is.EqualTo(rule.AttributeCount));
            Assert.That(
                allocationButtons.Count(button => button.name.StartsWith("Minus_")),
                Is.EqualTo(rule.AttributeCount));
            Assert.That(
                newGameScreen.GetComponentsInChildren<Text>(true)
                    .Any(text => text.text == "추천 · 수비형"),
                Is.True,
                "유격수는 수비형 배분을 추천받아야 한다.");

            allocationButtons.Single(button => button.name == "Preset_0").onClick.Invoke();
            yield return null;

            Text[] allocationTexts = newGameScreen.GetComponentsInChildren<Text>(true);
            Assert.That(
                allocationTexts.Single(text => text.name == "Remaining").text,
                Is.EqualTo("0 P"),
                "추천 배분은 배분 포인트를 전부 사용해야 한다.");
            Assert.That(
                allocationTexts.Single(text => text.name == "Rule").text,
                Does.StartWith($"총 {rule.BonusPoints} P"));
        }

        [UnityTest]
        public IEnumerator RookieSeason시작_대시보드를열고다음경기를진행한다()
        {
            UnitySceneManager.LoadScene(SceneCatalog.ManagementSceneName);
            yield return null;

            GameManager gameManager = GameManager.EnsureExists();
            NewGameManager newGame = gameManager.EnsureManager<NewGameManager>("NewGameManager");
            gameManager.EnsureManager<CareerManager>("CareerManager");
            newGame.RestartNewGame(24680UL);
            newGame.SubmitIdentity("대시보드 테스트", "대한민국");
            newGame.SelectPlayerType(PlayerType.Batter);
            newGame.SelectPosition(PlayerPosition.Shortstop);
            newGame.SelectHandedness(Handedness.Left, Handedness.Right);
            newGame.SubmitAttributes(new[] { 63, 58, 60, 53, 66, 60 });
            newGame.GenerateOffers();
            newGame.SelectOffer(newGame.Offers[0].TeamId);
            newGame.SignSelectedOffer();
            Assert.That(newGame.StartRookieSeason(), Is.True);
            yield return null;

            UI_Scene_NewGame newGameScreen =
                Object.FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include);
            UI_Scene_CareerDashboard dashboard =
                Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(FindObjectsInactive.Include);
            Assert.That(newGameScreen.IsVisible, Is.False);
            Assert.That(dashboard, Is.Not.Null);
            Assert.That(dashboard.IsVisible, Is.True);
            Assert.That(
                dashboard.GetComponentsInChildren<Text>(true).Any(text => text.text == "NEXT GAME"),
                Is.True);
            Text[] dashboardTexts = dashboard.GetComponentsInChildren<Text>(true);
            Assert.That(
                dashboardTexts.Any(text => text.name == "SampleSize" &&
                                           text.text.Contains("타석") &&
                                           text.text.Contains("타수")),
                Is.True);
            Assert.That(dashboardTexts.Any(text => text.text == "볼넷"), Is.True);
            Assert.That(dashboardTexts.Any(text => text.text == "삼진"), Is.True);
            Assert.That(dashboardTexts.Any(text => text.text == "도루 / 실패"), Is.True);
            Assert.That(dashboardTexts.Any(text => text.text == "실책"), Is.True);

            Assert.That(CareerManager.Instance.AdvanceNextGame(), Is.True);
            yield return null;

            Assert.That(
                CareerManager.Instance.CurrentCareer.CurrentLeague.CurrentSeason.PlayerStatistics.TeamGames,
                Is.EqualTo(1));
            Transform reactionPanel = dashboard.transform.Find("Content/ReactionPanel");
            Assert.That(reactionPanel, Is.Not.Null,
                "중요 경기 뒤에는 시즌 현황을 다시 그리기 전에 커리어 반응을 선택해야 합니다.");
            Transform reactionOption = reactionPanel.Find("ReactionOption_0");
            Assert.That(reactionOption, Is.Not.Null);
            reactionOption.GetComponent<Button>().onClick.Invoke();
            yield return null;

            PlayerSeasonStatisticsState statistics =
                CareerManager.Instance.CurrentCareer.CurrentLeague.CurrentSeason.PlayerStatistics;
            Text[] updatedDashboardTexts = dashboard.GetComponentsInChildren<Text>(true);
            Assert.That(
                updatedDashboardTexts.Single(text => text.name == "SampleSize").text,
                Does.Contain($"{statistics.PlateAppearances}타석").And
                    .Contain($"{statistics.AtBats}타수"));
            Assert.That(
                updatedDashboardTexts.Single(text => text.name == "DetailValue_0").text,
                Is.EqualTo(statistics.Walks.ToString()));
            Assert.That(
                updatedDashboardTexts.Single(text => text.name == "DetailValue_2").text,
                Is.EqualTo($"{statistics.StolenBases} / {statistics.CaughtStealing}"));
            Assert.That(
                updatedDashboardTexts.Single(text => text.name == "DetailValue_3").text,
                Is.EqualTo(statistics.FieldingErrors.ToString()));
            Assert.That(
                dashboard.GetComponentsInChildren<Text>(true)
                    .Any(text => text.text.Contains("최근 경기")),
                Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.HasInstance)
                Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }
    }
}
