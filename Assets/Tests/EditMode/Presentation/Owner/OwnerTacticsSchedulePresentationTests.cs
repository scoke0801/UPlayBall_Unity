using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>작전카드 화면이 앞으로 열릴 경기별 계획과 직접 장착 UX를 제공하는지 검증한다.</summary>
    public sealed class OwnerTacticsSchedulePresentationTests
    {
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void Bind_보유카드를클릭하면선택슬롯에즉시장착하고실제이미지를표시한다(int width, int height)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                var definition = new TacticCardDefinition("TEST-TACTIC", "강심장",
                    TacticCardCategory.Pitching, TacticTier.Normal, "개발용 설명", "Control +2, Mental +1",
                    Array.Empty<TacticTriggerCondition>(), TacticTargetRule.CurrentPitcher,
                    new[] { new TacticStatModifier(PlayerAbility.Control, 2), new TacticStatModifier(PlayerAbility.PitcherMental, 1) },
                    Array.Empty<TacticBehaviorModifier>(), TacticDurationRule.CurrentPlateAppearance,
                    Array.Empty<string>(), false);
                UI_Scene_OwnerTactics view = UI_Scene_OwnerTactics.CreateRuntime(canvasObject.transform);
                view.Bind(new OwnerTacticsSnapshot("기본", Array.Empty<string>(),
                    new[] { new OwnerTacticCardSnapshot(definition, 1) },
                    new[] { new OwnerTacticScheduleRowSnapshot(101, 1, "부산 하버스", false, false, 0, 0, true, Array.Empty<string>()) }));
                Transform workspace = view.transform.Find("OwnerTacticsWorkspace");
                workspace.Find("ScheduleTitle/NextGame").GetComponent<Button>().onClick.Invoke();
                Transform editor = workspace.Find("CardSettingOverlay");
                Assert.That(editor.Find("Detail/Equip"), Is.Null);
                for (int index = 0; index < 2; index++)
                {
                    Transform slot = editor.Find("Loadout/Slot" + index);
                    Assert.That(slot.Find("Label").GetComponent<Text>().text, Does.Contain("비어 있음"));
                    Assert.That(slot.Find("CardArtworkHolder").gameObject.activeSelf, Is.False);
                }
                ScrollRect scroll = editor.Find("Detail/DescriptionScroll").GetComponent<ScrollRect>();
                Text description = scroll.content.Find("Description").GetComponent<Text>();
                Assert.That(description.text, Does.Contain("제구 +2").And.Contain("투수 정신력 +1"));
                Assert.That(description.text, Does.Not.Contain("Control").And.Not.Contain("Mental").And.Not.Contain("개발용 설명"));
                description.text += new string('\n', 40) + "긴 설명 끝";
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(scroll.GetComponent<Mask>(), Is.Not.Null);
                string[] confirmed = null;
                int confirmedGameId = 0;
                view.SelectionConfirmed += (gameId, ids) =>
                {
                    confirmedGameId = gameId;
                    confirmed = ids;
                };
                editor.Find("CardCatalog/Scroll/Viewport/Content/Card0").GetComponent<Button>().onClick.Invoke();
                Assert.That(editor.Find("Loadout/Slot0/Label").GetComponent<Text>().text, Does.Contain("강심장"));
                editor.Find("Confirm").GetComponent<Button>().onClick.Invoke();
                Assert.That(confirmed, Is.EqualTo(new[] { definition.CardId }));
                Assert.That(confirmedGameId, Is.EqualTo(101));
                view.Bind(new OwnerTacticsSnapshot("기본", Array.Empty<string>(),
                    new[] { new OwnerTacticCardSnapshot(definition, 1) },
                    new[] { new OwnerTacticScheduleRowSnapshot(101, 1, "부산 하버스", false, false, 0, 0, true, confirmed) }));
                workspace.Find("ScheduleTitle/NextGame").GetComponent<Button>().onClick.Invoke();
                Assert.That(editor.Find("Loadout/Slot0/Label").GetComponent<Text>().text, Does.Contain("강심장"));
                Assert.That(editor.Find("Loadout/Slot0/CardArtworkHolder").gameObject.activeSelf, Is.True);
                Assert.That(editor.Find("Loadout/Slot0/CardArtworkHolder/CardArtwork").GetComponent<RawImage>().texture,
                    Is.EqualTo(TacticCardArtwork.Load(TacticCardArtwork.PitchingKey)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void Bind_앞으로열릴최대10경기를모두설정할수있다()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                UI_Scene_OwnerTactics view = UI_Scene_OwnerTactics.CreateRuntime(canvasObject.transform);
                view.Bind(new OwnerTacticsSnapshot(
                    "기본",
                    Array.Empty<string>(),
                    Array.Empty<OwnerTacticCardSnapshot>(),
                    CreateUpcomingRows(10)));

                Transform rows = view.transform.Find("OwnerTacticsWorkspace/ScheduleTable/Rows/Content");
                Assert.That(rows, Is.Not.Null);
                Assert.That(rows.childCount, Is.EqualTo(10));
                for (int index = 0; index < 10; index++)
                    Assert.That(rows.Find("GameRow" + index + "/CardSetting").GetComponent<Button>().interactable, Is.True);
                Assert.That(rows.Find("GameRow0/Schedule").GetComponent<Text>().text, Is.EqualTo("다음 경기"));
                Assert.That(rows.Find("GameRow1/Schedule").GetComponent<Text>().text, Is.EqualTo("예정"));

                Transform editor = view.transform.Find("OwnerTacticsWorkspace/CardSettingOverlay");
                Transform scrim = view.transform.Find("OwnerTacticsWorkspace/CardSettingScrim");
                Assert.That(editor.gameObject.activeSelf, Is.False);
                Assert.That(scrim.gameObject.activeSelf, Is.False);
                rows.Find("GameRow9/CardSetting").GetComponent<Button>().onClick.Invoke();
                Assert.That(editor.gameObject.activeSelf, Is.True);
                Assert.That(scrim.gameObject.activeSelf, Is.True);
                Assert.That(editor.Find("OverlayTitle").GetComponent<Text>().text, Does.Contain("제 19경기"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static OwnerTacticScheduleRowSnapshot[] CreateUpcomingRows(int count)
        {
            var rows = new OwnerTacticScheduleRowSnapshot[count];
            for (int index = 0; index < count; index++)
                rows[index] = new OwnerTacticScheduleRowSnapshot(
                    100 + index, 10 + index, "상대 " + index, index % 2 == 0,
                    false, 0, 0, true, Array.Empty<string>());
            return rows;
        }
    }
}
