using System.Collections;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.Career;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baseball.Tests.PlayMode.Presentation
{
    /// <summary>실제 Player Loop에서 타석 단위 자동 중계와 내 선수 입력 정지를 검증한다.</summary>
    public sealed class CareerMatchUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerFocus_1회부터타자결과를자동중계한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 94_001UL);
            CareerManager careerManager = GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager");
            careerManager.BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);
            UI_Scene_CareerMatch screen = Object.FindFirstObjectByType<UI_Scene_CareerMatch>(
                FindObjectsInactive.Include);
            if (screen == null)
                screen = UI_Scene_CareerMatch.CreateRuntime(sceneLayer);

            Assert.That(careerManager.PrepareNextGame(), Is.True, careerManager.LastError);
            screen.Show();
            yield return null;

            Button start = screen.transform.Find("Content/PreparationCard/Start").GetComponent<Button>();
            start.onClick.Invoke();
            yield return null;

            Text waitReason = screen.transform.Find("Content/Scoreboard/WaitReason").GetComponent<Text>();
            Assert.That(waitReason.text, Does.Contain("자동 중계"));
            Assert.That(screen.transform.Find("Content/FieldPanel/Diamond/First"), Is.Not.Null);
            Assert.That(screen.transform.Find("Content/FieldPanel/Diamond/Second"), Is.Not.Null);
            Assert.That(screen.transform.Find("Content/FieldPanel/Diamond/Third"), Is.Not.Null);

            yield return new WaitForSecondsRealtime(0.55f);

            Transform firstLog = screen.transform.Find("Content/LogPanel/Log0");
            Assert.That(firstLog, Is.Not.Null);
            Assert.That(firstLog.GetComponent<Text>().text, Does.StartWith("1회"));
            Assert.That(careerManager.ActiveMatch.Mode, Is.EqualTo(CareerMatchMode.PlayerFocus));
        }

        [UnityTest]
        public IEnumerator Bench_PlayerFocus에서속도를바꾸고경기결과로즉시진행한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 94_101UL);
            ScheduledGameState nextGame = career.CurrentLeague.CurrentSeason.Schedule.GetNextGameForTeam(
                career.MyPlayer.CurrentTeamId);
            nextGame.PlanPlayerRole(PlayerGameRole.Bench);
            CareerManager careerManager = GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager");
            careerManager.BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);
            UI_Scene_CareerMatch screen = Object.FindFirstObjectByType<UI_Scene_CareerMatch>(
                FindObjectsInactive.Include);
            if (screen == null)
                screen = UI_Scene_CareerMatch.CreateRuntime(sceneLayer);

            Assert.That(careerManager.PrepareNextGame(), Is.True, careerManager.LastError);
            Assert.That(careerManager.ActiveMatch.PlayerRole, Is.EqualTo(PlayerGameRole.Bench));
            screen.Show();
            yield return null;

            Button start = screen.transform.Find("Content/PreparationCard/Start").GetComponent<Button>();
            start.onClick.Invoke();
            yield return null;

            Button slow = screen.transform.Find("Content/CommandPanel/Speed_Slow").GetComponent<Button>();
            Assert.That(slow, Is.Not.Null);
            slow.onClick.Invoke();
            yield return null;

            Text speedLabel = screen.transform.Find("Content/CommandPanel/PlaybackSpeedLabel")
                .GetComponent<Text>();
            Assert.That(speedLabel.text, Does.Contain("느리게"));
            Button finish = screen.transform.Find("Content/CommandPanel/FinishMatch").GetComponent<Button>();
            Assert.That(finish, Is.Not.Null);
            finish.onClick.Invoke();
            yield return null;

            Assert.That(screen.transform.Find("Content/NextDay"), Is.Not.Null);
            Assert.That(careerManager.ActiveMatch.IsCommitted, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.HasInstance)
                Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }

        private static CareerState CreateStartedCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("경기 중계 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}
