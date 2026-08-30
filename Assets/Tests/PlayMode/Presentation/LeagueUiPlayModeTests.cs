using System.Collections;
using Baseball.Core.Players;
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
    /// <summary>실제 Player Loop에서 리그 화면 생성과 홈 왕복 탭 전환을 검증한다.</summary>
    public sealed class LeagueUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator LeagueTab_순위리더보드일정을렌더하고홈과왕복한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 91_001UL);
            var seasonService = new CareerSeasonService(career, configuration.Balance);
            for (int index = 0; index < 8; index++)
                seasonService.AdvanceNextRound();

            CareerManager careerManager = GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager");
            careerManager.BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);

            UI_Scene_CareerDashboard home = Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(
                FindObjectsInactive.Include);
            if (home == null)
                home = UI_Scene_CareerDashboard.CreateRuntime(sceneLayer);
            UI_Scene_League league = Object.FindFirstObjectByType<UI_Scene_League>(FindObjectsInactive.Include);
            if (league == null)
                league = UI_Scene_League.CreateRuntime(sceneLayer);

            Assert.That(CareerTabNavigation.Show(CareerMainTab.League), Is.True);
            yield return null;

            Assert.That(league.IsVisible, Is.True);
            Assert.That(home.IsVisible, Is.False);
            Assert.That(league.transform.Find("Content/Standings"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/BattingLeaders"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/PitchingLeaders"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/TeamMetrics"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/LeagueFocus"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/Schedule"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/Tabs/Tab_리그/ActiveGlow"), Is.Not.Null);

            Button stolenBasesTab = league.transform
                .Find("Content/BattingLeaders/Category_도루")
                .GetComponent<Button>();
            stolenBasesTab.onClick.Invoke();
            yield return null;

            LeagueBattingLeaderView stolenBasesLeader = careerManager.LeagueHub
                .GetBattingLeaderboard(LeagueBattingCategory.StolenBases)
                .Leaders[0];
            Text stolenBasesValue = league.transform
                .Find($"Content/BattingLeaders/Batter_{stolenBasesLeader.PlayerId}/StolenBases")
                .GetComponent<Text>();
            Assert.That(stolenBasesValue.text, Is.EqualTo(stolenBasesLeader.StolenBases.ToString()));

            Button savesTab = league.transform
                .Find("Content/PitchingLeaders/Category_세이브")
                .GetComponent<Button>();
            savesTab.onClick.Invoke();
            yield return null;

            LeaguePitchingLeaderView savesLeader = careerManager.LeagueHub
                .GetPitchingLeaderboard(LeaguePitchingCategory.Saves)
                .Leaders[0];
            Text savesValue = league.transform
                .Find($"Content/PitchingLeaders/Pitcher_{savesLeader.PlayerId}/Saves")
                .GetComponent<Text>();
            Assert.That(savesValue.text, Is.EqualTo(savesLeader.Saves.ToString()));

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Home), Is.True);
            yield return null;
            Assert.That(league.IsVisible, Is.False);
            Assert.That(home.IsVisible, Is.True);
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
            flow.SubmitIdentity("리그 UI 테스트", "대한민국");
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
