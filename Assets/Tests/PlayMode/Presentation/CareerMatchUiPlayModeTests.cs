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

            Transform flowStatusTransform = screen.transform.Find("Content/Scoreboard/FlowStatus");
            Assert.That(flowStatusTransform, Is.Not.Null,
                "경기 흐름은 제거된 WaitReason 대신 스코어보드의 FlowStatus로 표시해야 합니다.");
            Text flowStatus = flowStatusTransform.GetComponent<Text>();
            Assert.That(flowStatus.text, Does.StartWith("AUTO"));
            Assert.That(screen.transform.Find("Content/StagePanel/Diamond/First"), Is.Not.Null);
            Assert.That(screen.transform.Find("Content/StagePanel/Diamond/Second"), Is.Not.Null);
            Assert.That(screen.transform.Find("Content/StagePanel/Diamond/Third"), Is.Not.Null);

            yield return new WaitForSecondsRealtime(0.55f);

            Transform firstInningHeader = screen.transform.Find("Content/TimelinePanel/InningHeader0");
            Assert.That(firstInningHeader, Is.Not.Null);
            Assert.That(firstInningHeader.GetComponent<Text>().text, Does.StartWith("1회"));
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

            Button slow = screen.transform
                .Find("ControlLayer/ControlHost/AutoProgress/Speed_0").GetComponent<Button>();
            Assert.That(slow, Is.Not.Null);
            slow.onClick.Invoke();
            yield return null;

            Button finish = screen.transform
                .Find("ControlLayer/ControlHost/FinishMatch").GetComponent<Button>();
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
            flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}
