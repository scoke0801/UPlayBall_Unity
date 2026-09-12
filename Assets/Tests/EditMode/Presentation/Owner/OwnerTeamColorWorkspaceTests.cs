using System;
using System.IO;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>팀 컬러 장착의 저장 경계와 실제 해상도별 보드 배치를 검증한다.</summary>
    public sealed class OwnerTeamColorWorkspaceTests
    {
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 기본시너지본문은스크롤없이전체높이로표시된다(int width, int height)
        {
            var root = new GameObject("TeamColorCanvas", typeof(RectTransform));
            UI_Scene_OwnerTeamColor view = null;
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                view = UI_Scene_OwnerTeamColor.CreateRuntime(root.transform);
                view.Bind(new OwnerTeamColorSnapshot("기본", new string[2], Array.Empty<OwnerTeamColorCandidateSnapshot>()));
                var scroll = view.transform.Find("OwnerTeamColorWorkspace/DetailPanel/ContentSafeRect/EffectScroll").GetComponent<ScrollRect>();
                Text label = scroll.content.Find("Description").GetComponent<Text>();
                label.text = "현재 활성 효과 2개 · 2025 2025 LG 트윈스 · 완성된 연대기 + 2025 2025 LG 트윈스 · 한 시즌의 중심\n야수: 올 스탯 +17\n투수: 올 스탯 +17";
                for (int pass = 0; pass < 3; pass++)
                {
                    typeof(UI_Scene_OwnerTeamColor).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                    typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scroll, null);
                }
                Assert.That(label.gameObject.activeInHierarchy, Is.True);
                Assert.That(label.rectTransform.rect.height, Is.GreaterThan(0f));
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height));
                Assert.That(scroll.content.rect.height, Is.EqualTo(scroll.viewport.rect.height).Within(.1f));
                AssertInside(scroll.viewport, label.rectTransform);
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 같은계열교체는기존슬롯에서만허용하고되돌리기는두슬롯을복원한다()
        {
            var root = new GameObject("TeamColorCanvas", typeof(RectTransform));
            UI_Scene_OwnerTeamColor view = null;
            try
            {
                view = UI_Scene_OwnerTeamColor.CreateRuntime(root.transform);
                var first = new TeamColorDefinition("FIRST", TeamColorFamily.Generation, 1,
                    TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 1), TeamColorStatBonus.Create(),
                    upgradeGroupId: "GROUP", stackPolicy: TeamColorStackPolicy.HighestOnly, displayName: "첫 단계");
                var next = new TeamColorDefinition("NEXT", TeamColorFamily.Generation, 1,
                    TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 7), TeamColorStatBonus.Create(),
                    upgradeGroupId: "GROUP", stackPolicy: TeamColorStackPolicy.HighestOnly, displayName: "완성 단계");
                view.Bind(new OwnerTeamColorSnapshot("기본", new[] { first.TeamColorId, null }, new[]
                {
                    new OwnerTeamColorCandidateSnapshot(first, 1, Array.Empty<string>(), true),
                    new OwnerTeamColorCandidateSnapshot(next, 1, Array.Empty<string>(), true)
                }));
                Transform workspace = view.transform.Find("OwnerTeamColorWorkspace");
                ButtonAt(workspace, "CandidateList/ContentSafeRect/Scroll/Viewport/Content/Candidate0").onClick.Invoke();
                Button replace = ButtonAt(workspace, "DetailPanel/ContentSafeRect/Equip");
                Button other = ButtonAt(workspace, "DetailPanel/ContentSafeRect/EquipSecond");
                Assert.That(replace.interactable, Is.True);
                Assert.That(other.interactable, Is.False);
                Assert.That(workspace.Find("DetailPanel/ContentSafeRect/EquipReason").GetComponent<Text>().text, Does.Contain("같은 계열"));
                other.onClick.Invoke();
                Assert.That(view.HasUnappliedGuideChanges, Is.False, "입력 이벤트를 직접 호출해도 충돌을 막는다.");
                replace.onClick.Invoke();
                Assert.That(view.HasUnappliedGuideChanges, Is.True);
                Assert.That(view.TryHandleCancel(), Is.True);
                Assert.That(workspace.Find("EquippedSlots/ContentSafeRect/Slot0/Label").GetComponent<Text>().text, Is.EqualTo("첫 단계"));
                Assert.That(workspace.Find("EquippedSlots/ContentSafeRect/Slot1/Label").GetComponent<Text>().text, Is.EqualTo("빈 슬롯"));
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 효과검색과대상필터및정렬은함께적용되고초기화할수있다()
        {
            var root = new GameObject("TeamColorCanvas", typeof(RectTransform));
            UI_Scene_OwnerTeamColor view = null;
            try
            {
                view = UI_Scene_OwnerTeamColor.CreateRuntime(root.transform);
                var low = new TeamColorDefinition("LOW", TeamColorFamily.Generation, 1,
                    TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 1), TeamColorStatBonus.Create(), displayName: "가벼운 발걸음");
                var high = new TeamColorDefinition("HIGH", TeamColorFamily.Generation, 10,
                    TeamColorStatBonus.Create(), TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 10), displayName: "최고의 마운드");
                var locked = new TeamColorDefinition("LOCKED", TeamColorFamily.Generation, 20,
                    TeamColorStatBonus.Create(), TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 12), displayName: "내일의 마운드");
                view.Bind(new OwnerTeamColorSnapshot("기본", new string[2], new[]
                {
                    new OwnerTeamColorCandidateSnapshot(low, 25, Array.Empty<string>(), true),
                    new OwnerTeamColorCandidateSnapshot(locked, 19, Array.Empty<string>(), false),
                    new OwnerTeamColorCandidateSnapshot(high, 10, Array.Empty<string>(), true)
                }));
                Transform workspace = view.transform.Find("OwnerTeamColorWorkspace");
                Transform panel = workspace.Find("CandidateList/ContentSafeRect");
                Transform content = panel.Find("Scroll/Viewport/Content");
                Text FirstName() => content.Find("Candidate0/Label").GetComponent<Text>();
                Assert.That(FirstName().text, Is.EqualTo("최고의 마운드"), "초과 충족 인원보다 등급을 우선한다.");
                InputField search = panel.Find("Search").GetComponent<InputField>();
                search.text = "제구";
                Assert.That(panel.Find("ResultCount").GetComponent<Text>().text, Does.StartWith("2개"), "전체 능력치 효과도 검색된다.");
                panel.Find("TargetFilter").GetComponent<Dropdown>().value = 1;
                Assert.That(panel.Find("Empty").gameObject.activeSelf, Is.True);
                ButtonAt(panel, "ResetFilters").onClick.Invoke();
                Assert.That(search.text, Is.Empty);
                Assert.That(panel.Find("ResultCount").GetComponent<Text>().text, Does.StartWith("3개"));
                panel.Find("Sort").GetComponent<Dropdown>().value = 2;
                Assert.That(FirstName().text, Is.EqualTo("내일의 마운드"));
                ButtonAt(panel, "Active").onClick.Invoke();
                Assert.That(FirstName().text, Is.EqualTo("최고의 마운드"));
                ButtonAt(content, "Candidate0").onClick.Invoke();
                ButtonAt(workspace, "DetailPanel/ContentSafeRect/EquipSecond").onClick.Invoke();
                string[] submitted = null;
                view.SelectionConfirmed += ids => submitted = ids;
                ButtonAt(workspace, "Actions/ContentSafeRect/Confirm").onClick.Invoke();
                Assert.That(submitted, Is.EqualTo(new[] { null, high.TeamColorId }), "슬롯 사전 선택 없이 두 번째 슬롯에 장착한다.");
                Assert.That(ButtonAt(workspace, "DetailPanel/ContentSafeRect/Equip").interactable, Is.False);
                ButtonAt(workspace, "EquippedSlots/ContentSafeRect/ClearSecond").onClick.Invoke();
                Assert.That(view.HasUnappliedGuideChanges, Is.False);
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 장착선택은스크롤을유지하고확정전에는저장되지않는다(int width, int height)
        {
            var root = new GameObject("TeamColorCanvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("TeamColorCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            UI_Scene_OwnerTeamColor view = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                shell.SetInspectorVisible(false);
                shell.SetActionBarVisible(false);
                view = UI_Scene_OwnerTeamColor.CreateRuntime(shell.MainWorkspaceHost);
                var names = new string[25];
                for (int i = 0; i < names.Length; i++) names[i] = "김선수" + (i + 1);
                var candidates = new OwnerTeamColorCandidateSnapshot[12];
                for (int i = 0; i < candidates.Length; i++)
                {
                    var definition = new TeamColorDefinition("COLOR_" + i.ToString("D2"), TeamColorFamily.Generation,
                        i == 11 ? 25 : 20, TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 7),
                        TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 5),
                        displayName: "2025 서울 마리너스 · 한 시즌의 중심 " + (i + 1).ToString("D2"),
                        description: "같은 계절을 함께한 선수들이 모여 그해의 호흡을 되살립니다.");
                    candidates[i] = new OwnerTeamColorCandidateSnapshot(definition, i == 11 ? 20 : 25, names, i != 11);
                }
                var snapshot = new OwnerTeamColorSnapshot("기본 라인업", new[] { candidates[0].Id, null }, candidates);
                view.Bind(snapshot);
                Transform workspace = view.transform.Find("OwnerTeamColorWorkspace");
                Button confirm = ButtonAt(workspace, "Actions/ContentSafeRect/Confirm");
                Button equip = ButtonAt(workspace, "DetailPanel/ContentSafeRect/Equip");
                Button equipSecond = ButtonAt(workspace, "DetailPanel/ContentSafeRect/EquipSecond");
                int requests = 0;
                string[] submitted = null;
                view.SelectionConfirmed += ids => { requests++; submitted = ids; };
                Assert.That(confirm.interactable, Is.False);
                var scroll = workspace.Find("CandidateList/ContentSafeRect/Scroll").GetComponent<ScrollRect>();
                Button row = ButtonAt(workspace, "CandidateList/ContentSafeRect/Scroll/Viewport/Content/Candidate1");
                scroll.content.anchoredPosition = new Vector2(0f, 50f);
                row.onClick.Invoke();
                Assert.That(ButtonAt(workspace, "CandidateList/ContentSafeRect/Scroll/Viewport/Content/Candidate1"), Is.SameAs(row));
                Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(50f));
                Transform playerScroll = workspace.Find("DetailPanel/ContentSafeRect/PlayerScroll");
                Transform firstPlayerCard = playerScroll.Find("Viewport/Content/EligiblePlayer0");
                Assert.That(firstPlayerCard.GetComponent<PlayerMiniCardView>(), Is.Not.Null);
                Assert.That(firstPlayerCard.Find("Name").GetComponent<Text>().text, Is.EqualTo("김선수1"));
                Assert.That(equip.interactable, Is.True);
                equip.onClick.Invoke();
                Assert.That(requests, Is.Zero);
                Assert.That(snapshot.EquippedIds[0], Is.EqualTo(candidates[0].Id));
                Assert.That(confirm.interactable, Is.True);
                Assert.That(equipSecond.interactable, Is.False, "다른 슬롯의 중복 장착 사유를 클릭 전에 안내한다.");
                Assert.That(view.TryHandleCancel(), Is.True);
                Assert.That(confirm.interactable, Is.False);
                Assert.That(view.TryHandleCancel(), Is.False);
                equipSecond.onClick.Invoke();
                confirm.onClick.Invoke();
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(submitted[1], Is.EqualTo(candidates[1].Id));
                scroll.content.anchoredPosition = Vector2.zero;

                CareerUiSkin.Apply(view.transform);
                for (int pass = 0; pass < 3; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    typeof(UI_Scene_OwnerTeamColor).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                    foreach (ScrollRect scroller in view.GetComponentsInChildren<ScrollRect>())
                        typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scroller, null);
                    foreach (OwnerUiButtonSkin skin in view.GetComponentsInChildren<OwnerUiButtonSkin>()) skin.Refresh();
                }
                Canvas.ForceUpdateCanvases();
                Assert.That(scroll.verticalScrollbar.size, Is.LessThan(1f), "목록 길이에 맞는 스크롤 손잡이를 표시한다.");
                Assert.That(playerScroll.GetComponent<ScrollRect>().horizontalScrollbar.size, Is.LessThan(1f),
                    "효과 대상 Mini Card가 많으면 가로 스크롤 손잡이를 표시한다.");
                foreach (string panelName in new[] { "EquippedSlots", "CandidateList", "DetailPanel", "Actions" })
                    AssertInside((RectTransform)workspace, (RectTransform)workspace.Find(panelName));
                foreach (Button button in workspace.GetComponentsInChildren<Button>())
                {
                    if (button.GetComponentInParent<ScrollRect>() != null) continue;
                    AssertInside((RectTransform)button.transform.parent, (RectTransform)button.transform);
                }
                foreach (Text label in workspace.GetComponentsInChildren<Text>())
                {
                    if (string.IsNullOrEmpty(label.text)) continue;
                    float renderedHeight = label.preferredHeight;
                    if (label.resizeTextForBestFit)
                    {
                        // 공용 미니 카드의 실제 자동 맞춤 글자 크기로 측정한다.
                        TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                        settings.fontSize = label.cachedTextGenerator.fontSizeUsedForBestFit;
                        settings.resizeTextForBestFit = false;
                        using (var generator = new TextGenerator())
                            renderedHeight = generator.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                    }
                    Assert.That(renderedHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), label.name + ": " + label.text);
                }
                string output = Environment.GetEnvironmentVariable("BASEBALL_TEAM_COLOR_CAPTURE");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, width + "x" + height + ".png"), image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                    RenderTexture.active = previous;
                }
                Button last = ButtonAt(workspace, "CandidateList/ContentSafeRect/Scroll/Viewport/Content/Candidate11");
                ExecuteEvents.Execute(last.gameObject, new BaseEventData(null), ExecuteEvents.selectHandler);
                Bounds focused = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)last.transform);
                Assert.That(focused.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1f));
                last.onClick.Invoke();
                Assert.That(equip.interactable, Is.False);
                Assert.That(workspace.Find("DetailPanel/ContentSafeRect/Progress").GetComponent<Text>().text, Does.Contain("5명"));
                view.Bind(new OwnerTeamColorSnapshot("빈 라인업", new string[2], Array.Empty<OwnerTeamColorCandidateSnapshot>()));
                Assert.That(workspace.Find("CandidateList/ContentSafeRect/Empty").gameObject.activeSelf, Is.True);
                Assert.That(equip.interactable, Is.False);
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Button ButtonAt(Transform root, string path) => root.Find(path).GetComponent<Button>();

        private static void AssertInside(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - 1f, parent.rect.xMax + 1f), child.name);
                Assert.That(local.y, Is.InRange(parent.rect.yMin - 1f, parent.rect.yMax + 1f), child.name);
            }
        }
    }
}
