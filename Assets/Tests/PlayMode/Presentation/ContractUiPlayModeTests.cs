using System.Collections;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.Career;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baseball.Tests.PlayMode.Presentation
{
    /// <summary>
    /// 실제 Player Loop에서 계약 화면의 핵심 패널과 공용 탭 전환을 검증한다.
    /// </summary>
    public sealed class ContractUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator ContractTab_현재계약상여시장정보를렌더하고홈과왕복한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 92_001UL);
            GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager")
                .BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);

            UI_Scene_CareerDashboard home = Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(
                FindObjectsInactive.Include);
            if (home == null)
                home = UI_Scene_CareerDashboard.CreateRuntime(sceneLayer);
            UI_Scene_Contract contract = Object.FindFirstObjectByType<UI_Scene_Contract>(
                FindObjectsInactive.Include);
            if (contract == null)
                contract = UI_Scene_Contract.CreateRuntime(sceneLayer);

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Contract), Is.True);
            yield return null;

            Assert.That(contract.IsVisible, Is.True);
            Assert.That(home.IsVisible, Is.False);
            Assert.That(contract.transform.Find("Content/PlayerPanel"), Is.Not.Null);
            Assert.That(contract.transform.Find("Content/CurrentContract"), Is.Not.Null);
            Assert.That(contract.transform.Find("Content/SalaryHistory"), Is.Not.Null);
            Assert.That(contract.transform.Find("Content/Bonus"), Is.Not.Null);
            Assert.That(contract.transform.Find("Content/Market"), Is.Not.Null);
            Assert.That(contract.transform.Find("Content/Tabs/Tab_계약/ActiveGlow"), Is.Not.Null);
            AssertPlayerPanelElementsDoNotOverlap(contract.transform);
            AssertContractOverviewElementsStayInBounds(contract.transform);

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Home), Is.True);
            yield return null;
            Assert.That(contract.IsVisible, Is.False);
            Assert.That(home.IsVisible, Is.True);
        }

        private static void AssertPlayerPanelElementsDoNotOverlap(Transform contract)
        {
            RectTransform playerCard = GetRect(contract, "Content/PlayerPanel/PlayerCard");
            RectTransform teamRow = GetRect(contract, "Content/PlayerPanel/Info_소속 구단");
            RectTransform ageRow = GetRect(contract, "Content/PlayerPanel/Info_나이");
            RectTransform roleRow = GetRect(contract, "Content/PlayerPanel/Info_계약 역할");
            RectTransform expirationRow = GetRect(contract, "Content/PlayerPanel/Info_계약 만료");
            RectTransform negotiation = GetRect(contract, "Content/PlayerPanel/Negotiation");

            Assert.That(GetTop(teamRow), Is.LessThan(GetBottom(playerCard)),
                "선수 카드와 소속 구단 행이 겹치면 선수 이름을 읽을 수 없다.");
            Assert.That(GetTop(ageRow), Is.LessThan(GetBottom(teamRow)));
            Assert.That(GetTop(roleRow), Is.LessThan(GetBottom(ageRow)));
            Assert.That(GetTop(expirationRow), Is.LessThan(GetBottom(roleRow)));
            Assert.That(GetTop(negotiation), Is.LessThan(GetBottom(expirationRow)),
                "계약 만료 행과 협상 버튼은 서로 분리되어야 한다.");
        }

        private static void AssertContractOverviewElementsStayInBounds(Transform contract)
        {
            RectTransform title = GetRect(contract, "Content/Title");
            RectTransform subtitle = GetRect(contract, "Content/Subtitle");
            Assert.That(GetRight(title), Is.LessThan(GetLeft(subtitle)),
                "계약 화면 제목과 설명 영역은 겹치면 안 된다.");

            RectTransform contractHeader = GetRect(contract, "Content/CurrentContract/Header");
            RectTransform firstMetric = GetRect(contract, "Content/CurrentContract/Metric_계약 기간");
            Assert.That(GetTop(firstMetric), Is.LessThan(GetBottom(contractHeader)),
                "현재 계약의 첫 번째 지표 행은 패널 헤더를 침범하면 안 된다.");

            Transform bonusPanel = contract.Find("Content/Bonus");
            Transform firstBonusRow = null;
            for (int index = 0; index < bonusPanel.childCount; index++)
            {
                Transform child = bonusPanel.GetChild(index);
                if (child.name.StartsWith("Bonus_", System.StringComparison.Ordinal) &&
                    child.name != "BonusFooter")
                {
                    firstBonusRow = child;
                    break;
                }
            }
            Assert.That(firstBonusRow, Is.Not.Null);
            RectTransform progress = GetRect(firstBonusRow, "Progress");
            RectTransform track = GetRect(firstBonusRow, "Track");
            Assert.That(GetRight(progress), Is.LessThan(GetLeft(track)),
                "상여 달성 문구와 진행 막대는 서로 겹치면 안 된다.");

            RectTransform market = GetRect(contract, "Content/Market");
            RectTransform status = GetRect(contract, "Content/Market/Status");
            Assert.That(GetBottom(status), Is.GreaterThanOrEqualTo(market.rect.yMin),
                "계약 상태 박스는 시장 가치 패널의 하단을 넘어가면 안 된다.");
        }

        private static RectTransform GetRect(Transform root, string path)
        {
            Transform target = root.Find(path);
            Assert.That(target, Is.Not.Null, path);
            return (RectTransform)target;
        }

        private static float GetTop(RectTransform rect)
        {
            return rect.anchoredPosition.y + rect.rect.yMax;
        }

        private static float GetBottom(RectTransform rect)
        {
            return rect.anchoredPosition.y + rect.rect.yMin;
        }

        private static float GetLeft(RectTransform rect)
        {
            return rect.anchoredPosition.x + rect.rect.xMin;
        }

        private static float GetRight(RectTransform rect)
        {
            return rect.anchoredPosition.x + rect.rect.xMax;
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
            flow.SubmitIdentity("계약 UI 테스트", "대한민국");
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
