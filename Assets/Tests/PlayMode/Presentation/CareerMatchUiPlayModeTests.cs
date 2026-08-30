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

        [UnityTest]
        public IEnumerator MiniGame_타격준비전에는투구하지않고와인드업뒤공을보여준다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 94_151UL);
            career.GameSettings.SetMatchProgressMode(MatchProgressMode.MiniGame);
            career.GameSettings.SetMiniGameScope(MiniGameScope.AllInvolvement);
            ScheduledGameState nextGame = career.CurrentLeague.CurrentSeason.Schedule.GetNextGameForTeam(
                career.MyPlayer.CurrentTeamId);
            nextGame.PlanPlayerRole(PlayerGameRole.StartingBatter);

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
            screen.transform.Find("Content/PreparationCard/Start").GetComponent<Button>().onClick.Invoke();

            float timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!careerManager.ActiveMatch.PendingSwingExecution.HasValue &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(careerManager.ActiveMatch.PendingSwingExecution.HasValue, Is.True,
                "내 타석의 SwingExecution 요청까지 도달해야 합니다.");
            Transform readyTransform = screen.transform.Find("Content/ControlPanel/BattingReady");
            Transform ballTransform = screen.transform.Find("Content/StagePanel/BattingPlane/Ball");
            Assert.That(readyTransform, Is.Not.Null);
            Assert.That(ballTransform, Is.Not.Null);
            Assert.That(ballTransform.gameObject.activeSelf, Is.False,
                "타격 준비 전에는 공이 자동으로 출발하면 안 됩니다.");

            yield return new WaitForSecondsRealtime(0.55f);

            Assert.That(careerManager.ActiveMatch.PendingSwingExecution.HasValue, Is.True);
            Assert.That(ballTransform.gameObject.activeSelf, Is.False);
            readyTransform.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Transform trackingState = screen.transform.Find("Content/ControlPanel/PitchTrackingState");
            ballTransform = screen.transform.Find("Content/StagePanel/BattingPlane/Ball");
            Assert.That(trackingState, Is.Not.Null);
            Assert.That(ballTransform.gameObject.activeSelf, Is.False,
                "준비 직후에는 먼저 투수의 와인드업이 보여야 합니다.");

            yield return new WaitForSecondsRealtime(0.78f);

            Assert.That(ballTransform.gameObject.activeSelf, Is.True,
                "릴리스 뒤에는 공이 이동하는 화면이 보여야 합니다.");
            Assert.That(screen.transform.Find("Content/StagePanel/TimingTrack/TimingMarker"), Is.Not.Null);
            Assert.That(careerManager.ActiveMatch.Mode, Is.EqualTo(CareerMatchMode.MiniGame));
        }

        [UnityTest]
        public IEnumerator MiniGame_투구확정뒤와인드업과실제궤적을재생하고제구결과를표시한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedPitcherCareer(configuration, 94_181UL);
            career.GameSettings.SetMatchProgressMode(MatchProgressMode.MiniGame);
            career.GameSettings.SetMiniGameScope(MiniGameScope.AllInvolvement);
            ScheduledGameState nextGame = career.CurrentLeague.CurrentSeason.Schedule.GetNextGameForTeam(
                career.MyPlayer.CurrentTeamId);
            nextGame.PlanPlayerRole(PlayerGameRole.StartingPitcher);

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
            screen.transform.Find("Content/PreparationCard/Start").GetComponent<Button>().onClick.Invoke();

            float timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!careerManager.ActiveMatch.PendingPitchSelection.HasValue &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(careerManager.ActiveMatch.PendingPitchSelection.HasValue, Is.True,
                "내 투구의 PitchSelection 요청까지 도달해야 합니다.");
            Transform ready = screen.transform.Find("Content/ControlPanel/BeginPitchSelection");
            Transform ball = screen.transform.Find(
                "ControlLayer/PitchPresentationLayer/PitchPresentationBall");
            Transform preview = screen.transform.Find(
                "ControlLayer/PitchPresentationLayer/PitchPreviewDot_0");
            Assert.That(ready, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(ball.gameObject.activeSelf, Is.False,
                "투구 준비 전에는 공이 출발하면 안 됩니다.");
            Assert.That(preview.gameObject.activeSelf, Is.False,
                "준비 전에는 구종 대표 궤적도 입력을 재촉하지 않아야 합니다.");

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(careerManager.ActiveMatch.PendingPitchSelection.HasValue, Is.True);
            ready.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Transform confirm = screen.transform.Find("Content/ControlPanel/ConfirmPitch");
            Transform ellipse = screen.transform.Find(
                "ControlLayer/PitchPresentationLayer/PitchCommandEllipse");
            Assert.That(confirm, Is.Not.Null);
            Assert.That(preview.gameObject.activeSelf, Is.True);
            Assert.That(ellipse.gameObject.activeSelf, Is.True);

            confirm.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.That(screen.transform.Find("Content/ControlPanel/PitchLockedAction"), Is.Not.Null);
            Assert.That(ball.gameObject.activeSelf, Is.False,
                "확정 직후에는 공보다 와인드업이 먼저 보여야 합니다.");

            yield return new WaitForSecondsRealtime(0.62f);
            Assert.That(ball.gameObject.activeSelf, Is.True,
                "릴리스 뒤에는 Descriptor 기반 공 비행이 시작되어야 합니다.");

            Transform actual = screen.transform.Find(
                "ControlLayer/PitchPresentationLayer/PitchActualPoint");
            timeoutAt = Time.realtimeSinceStartup + 2f;
            while (!actual.gameObject.activeSelf && Time.realtimeSinceStartup < timeoutAt)
                yield return null;

            Assert.That(actual.gameObject.activeSelf, Is.True,
                "도착 뒤에는 실제 PlatePoint가 표시되어야 합니다.");
            Assert.That(screen.transform.Find(
                "ControlLayer/PitchPresentationLayer/PitchCommandConnector").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator StartingPitcher_선수패널에투구기록을표시한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedPitcherCareer(configuration, 94_201UL);
            ScheduledGameState nextGame = career.CurrentLeague.CurrentSeason.Schedule.GetNextGameForTeam(
                career.MyPlayer.CurrentTeamId);
            nextGame.PlanPlayerRole(PlayerGameRole.StartingPitcher);
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
            Assert.That(careerManager.ActiveMatch.PlayerRole, Is.EqualTo(PlayerGameRole.StartingPitcher));
            screen.Show();
            yield return null;

            Button start = screen.transform.Find("Content/PreparationCard/Start").GetComponent<Button>();
            start.onClick.Invoke();
            yield return null;

            Text today = screen.transform.Find("Content/PlayerPanel/Today/Value").GetComponent<Text>();
            Text detail = screen.transform.Find("Content/PlayerPanel/Today/Detail").GetComponent<Text>();
            Text season = screen.transform.Find("Content/PlayerPanel/Season").GetComponent<Text>();
            Assert.That(today.text, Does.Contain("이닝"));
            Assert.That(today.text, Does.Contain("피안타"));
            Assert.That(today.text, Does.Not.Contain("타석"));
            Assert.That(detail.text, Does.Contain("투구"));
            Assert.That(season.text, Does.Contain("평균자책"));
            Assert.That(season.text, Does.Not.Contain("홈런"));
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

        private static CareerState CreateStartedPitcherCareer(
            NewGameConfiguration configuration,
            ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("경기 중계 투수 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Pitcher);
            flow.SelectPosition(PlayerPosition.StartingPitcher);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitPitcherAttributes(new PitcherAttributes(63, 61, 59, 59, 61, 57));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}
