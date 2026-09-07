using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Presentation.Career;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>새 게임 스킨 재적용이 미니 카드의 프레임·선택·우클릭 입력을 보존하는지 검증한다.</summary>
    public sealed class OwnerNewGameSkinTests
    {
        [Test]
        public void Skin_구단주버튼과카드의독립된입력을보존한다()
        {
            var root = new GameObject("OwnerNewGamePanel", typeof(RectTransform));
            var events = new GameObject("Events", typeof(EventSystem));
            try
            {
                var buttonObject = new GameObject("ConfirmMainCards", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(buttonObject.transform, false);
                label.GetComponent<Text>().text = "메인 카드 확정";
                var card = PlayerMiniCardView.CreateRuntime(root.transform);
                card.UseLineupSlotLayout();
                card.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
                card.Bind(new PlayerMiniCardModel("CARD", "김가람", "유격수", "2024", "비용 6", "일반",
                    "✓ 선택", visualState: PlayerMiniCardVisualState.Selected,
                    frameEdition: PlayerCardEdition.Normal, cost: 6));
                Sprite frame = card.transform.Find("LineupSubFrame").GetComponent<Image>().sprite;
                Color selected = card.GetComponent<Image>().color;
                int selectionCount = 0;
                int detailCount = 0;
                card.Selected += _ => selectionCount++;
                card.DetailRequested += _ => detailCount++;

                typeof(UI_Scene_NewGame).GetMethod("ApplyOwnerNewGameSkin", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { (RectTransform)root.transform });
                CareerUiSkin.Apply(root.transform);
                CareerUiSkin.Apply(root.transform);

                Assert.That(buttonObject.GetComponent<OwnerUiButtonSkin>(), Is.Not.Null);
                Assert.That(((Image)buttonObject.GetComponent<Button>().targetGraphic).sprite.name,
                    Does.StartWith("OwnerButton_"));
                Assert.That(card.GetComponent<OwnerUiButtonSkin>(), Is.Null);
                Assert.That(card.transform.Find("LineupSubFrame").GetComponent<Image>().sprite, Is.SameAs(frame));
                Assert.That(card.GetComponent<Image>().color, Is.EqualTo(selected));
                var pointer = new PointerEventData(events.GetComponent<EventSystem>())
                    { button = PointerEventData.InputButton.Right };
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(detailCount, Is.EqualTo(1));
                Assert.That(selectionCount, Is.Zero);
                pointer.button = PointerEventData.InputButton.Left;
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(selectionCount, Is.EqualTo(1));
                Assert.That(detailCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(events);
            }
        }
    }
}
