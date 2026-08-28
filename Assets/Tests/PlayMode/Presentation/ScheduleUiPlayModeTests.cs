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
    /// <summary>실제 Player Loop에서 일정 화면의 렌더와 주요 조회 전환을 검증한다.</summary>
    public sealed class ScheduleUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator ScheduleTab_달력목록리그필터와월이동을렌더한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 92_801UL);
            var seasonService = new CareerSeasonService(career, configuration.Balance);
            for (int index = 0; index < 12; index++)
                seasonService.AdvanceNextRound();

            GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager")
                .BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);

            UI_Scene_CareerDashboard home = Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(
                FindObjectsInactive.Include);
            if (home == null)
                home = UI_Scene_CareerDashboard.CreateRuntime(sceneLayer);
            UI_Scene_CareerSchedule schedule = Object.FindFirstObjectByType<UI_Scene_CareerSchedule>(
                FindObjectsInactive.Include);
            if (schedule == null)
                schedule = UI_Scene_CareerSchedule.CreateRuntime(sceneLayer);

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Schedule), Is.True);
            yield return null;

            Assert.That(schedule.IsVisible, Is.True);
            Assert.That(home.IsVisible, Is.False);
            Assert.That(schedule.transform.Find("Content/Calendar"), Is.Not.Null);
            Assert.That(schedule.transform.Find("Content/TeamSummary/NextGame"), Is.Not.Null);
            Assert.That(schedule.transform.Find("Content/TeamSummary/MonthSummary"), Is.Not.Null);
            Assert.That(schedule.transform.Find("Content/Tabs/Tab_일정/ActiveGlow"), Is.Not.Null);

            Button listButton = schedule.transform.Find("Content/ViewTabs/Tab_목록").GetComponent<Button>();
            listButton.onClick.Invoke();
            yield return null;
            Assert.That(schedule.transform.Find("Content/ScheduleList"), Is.Not.Null);

            Button leagueScope = schedule.transform.Find("Content/ScheduleFilters/Scope_EntireLeague")
                .GetComponent<Button>();
            leagueScope.onClick.Invoke();
            yield return null;
            Assert.That(schedule.transform.Find("Content/ScheduleFilters/Scope_EntireLeague"), Is.Not.Null);

            Text monthText = schedule.transform.Find("Content/MonthNavigation/Month").GetComponent<Text>();
            string previousMonth = monthText.text;
            Button nextMonth = schedule.transform.Find("Content/MonthNavigation/NextMonth").GetComponent<Button>();
            Assert.That(nextMonth.interactable, Is.True);
            nextMonth.onClick.Invoke();
            yield return null;
            string movedMonth = schedule.transform.Find("Content/MonthNavigation/Month").GetComponent<Text>().text;
            Assert.That(movedMonth, Is.Not.EqualTo(previousMonth));
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
            flow.SubmitIdentity("일정 UI 테스트", "대한민국");
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
