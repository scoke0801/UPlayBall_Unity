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
    /// 실제 Player Loop에서 새 게임 화면의 핵심 레이아웃을 검증한다.
    /// </summary>
    public sealed class NewGameUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator ContractOffers_안내문구와오퍼카드가겹치지않고목록이본문안에남는다()
        {
            yield return null;

            NewGameManager manager = GameManager.EnsureExists()
                .EnsureManager<NewGameManager>("NewGameManager");
            manager.RestartNewGame(424_242UL);
            Assert.That(manager.SubmitIdentity("계약 UI 테스트", "대한민국"), Is.True);
            Assert.That(manager.SelectPlayerType(PlayerType.Batter), Is.True);
            Assert.That(manager.SelectPosition(PlayerPosition.Shortstop), Is.True);
            Assert.That(manager.SelectHandedness(Handedness.Left, Handedness.Right), Is.True);
            Assert.That(manager.SubmitAttributes(new[] { 55, 50, 52, 43, 60, 52 }), Is.True);
            Assert.That(manager.GenerateOffers(), Is.True);

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            UI_Scene_NewGame screen = Object.FindFirstObjectByType<UI_Scene_NewGame>(
                FindObjectsInactive.Include);
            if (screen == null)
            {
                screen = UI_Scene_NewGame.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Scene));
            }

            screen.Show();
            yield return null;

            RectTransform body = GetRect(screen.transform, "NewGamePanel/Body");
            RectTransform subtitle = GetRect(screen.transform, "NewGamePanel/Body/Subtitle");
            RectTransform firstOffer = GetRect(
                screen.transform,
                "NewGamePanel/Body/Offer_" + manager.Offers[0].TeamId);
            RectTransform lastOffer = GetRect(
                screen.transform,
                "NewGamePanel/Body/Offer_" + manager.Offers[manager.Offers.Count - 1].TeamId);

            Assert.That(GetBottom(subtitle), Is.GreaterThan(GetTop(firstOffer)),
                "안내 문구와 첫 오퍼 카드가 겹치면 문구 하단이 카드에 가려진다.");
            Assert.That(GetTop(firstOffer), Is.LessThanOrEqualTo(body.rect.yMax));
            Assert.That(GetBottom(lastOffer), Is.GreaterThanOrEqualTo(body.rect.yMin),
                "최대 오퍼 수에서도 마지막 카드가 본문 밖으로 잘리면 안 된다.");
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.HasInstance)
                Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }
    }
}
