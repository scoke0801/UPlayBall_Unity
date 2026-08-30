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
    /// <summary>기록 화면의 상세 지표, 범위 전환, 스크롤 계층이 실제 Player Loop에서 유지되는지 검증한다.</summary>
    public sealed class CareerRecordsUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator RecordsTab_전체지표스크롤과경기범위전환을제공한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 94_004UL);
            var seasonService = new CareerSeasonService(career, configuration.Balance);
            for (int index = 0; index < 12; index++)
                seasonService.AdvanceNextRound();

            CareerManager careerManager = GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager");
            careerManager.BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);
            UI_Scene_CareerRecords records = Object.FindFirstObjectByType<UI_Scene_CareerRecords>(
                FindObjectsInactive.Include);
            if (records == null)
                records = UI_Scene_CareerRecords.CreateRuntime(sceneLayer);

            records.Show();
            yield return null;

            Transform expandedTable = records.transform.Find("Content/Leaderboard/LeaderboardTable");
            Assert.That(expandedTable, Is.Not.Null);
            Assert.That(expandedTable.Find("HorizontalScrollbar"), Is.Not.Null,
                "전체 지표는 한 화면에 억지로 축소하지 않고 가로 스크롤을 제공해야 한다.");
            Assert.That(records.transform.Find("Content/MyRecord/MetricScroll"), Is.Not.Null);

            records.transform.Find("Content/CategoryMenu/ViewMode_Basic")
                .GetComponent<Button>()
                .onClick.Invoke();
            yield return null;

            Transform basicTable = records.transform.Find("Content/Leaderboard/LeaderboardTable");
            Assert.That(basicTable, Is.Not.Null);
            Assert.That(basicTable.Find("HorizontalScrollbar"), Is.Null,
                "핵심 지표 화면은 불필요한 가로 스크롤 없이 읽혀야 한다.");

            records.transform.Find("Content/CategoryMenu/Scope_Postseason")
                .GetComponent<Button>()
                .onClick.Invoke();
            yield return null;

            Text[] texts = records.GetComponentsInChildren<Text>(true);
            bool hasEmptyState = false;
            for (int index = 0; index < texts.Length; index++)
            {
                if (texts[index].text.Contains("선택한 경기 범위에 아직 기록이 없습니다"))
                {
                    hasEmptyState = true;
                    break;
                }
            }
            Assert.That(hasEmptyState, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.HasInstance)
                Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }

        private static CareerState CreateStartedCareer(
            NewGameConfiguration configuration,
            ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("기록 UI 테스트", "대한민국");
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
