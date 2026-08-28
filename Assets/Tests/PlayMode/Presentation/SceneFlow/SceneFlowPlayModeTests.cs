using System.Collections;
using System.Linq;
using Baseball.Core.Players;
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
                newGameScreen.GetComponentsInChildren<Text>(true).Any(text => text.text == "선수 기본 정보"),
                Is.True);

            NewGameManager newGameManager = NewGameManager.Instance;
            Assert.That(newGameManager.SubmitIdentity("테스트 선수", "대한민국"), Is.True);
            Assert.That(newGameManager.SelectPlayerType(PlayerType.Batter), Is.True);
            Assert.That(newGameManager.SelectPosition(PlayerPosition.Shortstop), Is.True);
            Assert.That(
                newGameManager.SelectHandedness(Handedness.Right, Handedness.Right),
                Is.True);

            Button[] allocationButtons = newGameScreen.GetComponentsInChildren<Button>(true);
            Assert.That(
                allocationButtons.Count(button => button.name.StartsWith("Preset_")),
                Is.EqualTo(5));
            Assert.That(
                allocationButtons.Count(button => button.name.StartsWith("PlusFive_")),
                Is.EqualTo(CharacterCreationBalance.AttributeCount));
            Assert.That(
                newGameScreen.GetComponentsInChildren<Text>(true)
                    .Any(text => text.text == "추천 · 수비형"),
                Is.True);

            allocationButtons.Single(button => button.name == "Preset_0").onClick.Invoke();
            yield return null;

            Text remaining = newGameScreen.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "Remaining");
            Assert.That(remaining.text, Does.StartWith("남은 포인트  0 / 72"));
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
            newGame.SubmitAttributes(new[] { 55, 50, 52, 43, 60, 52 });
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

            Assert.That(CareerManager.Instance.AdvanceNextGame(), Is.True);
            yield return null;

            Assert.That(
                CareerManager.Instance.CurrentCareer.League.CurrentSeason.PlayerStatistics.TeamGames,
                Is.EqualTo(1));
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
