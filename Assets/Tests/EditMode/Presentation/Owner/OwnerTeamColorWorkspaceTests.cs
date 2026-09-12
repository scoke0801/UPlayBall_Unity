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
                        displayName: "2025 서울 마리너스 · 한 시즌의 중심 " + (i + 1),
                        description: "같은 계절을 함께한 선수들이 모여 그해의 호흡을 되살립니다.");
                    candidates[i] = new OwnerTeamColorCandidateSnapshot(definition, i == 11 ? 20 : 25, names, i != 11);
                }
                var snapshot = new OwnerTeamColorSnapshot("기본 라인업", new[] { candidates[0].Id, null }, candidates);
                view.Bind(snapshot);
                Transform workspace = view.transform.Find("OwnerTeamColorWorkspace");
                Button confirm = ButtonAt(workspace, "Actions/ContentSafeRect/Confirm");
                Button equip = ButtonAt(workspace, "DetailPanel/ContentSafeRect/Equip");
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
                Assert.That(equip.interactable, Is.True);
                equip.onClick.Invoke();
                Assert.That(requests, Is.Zero);
                Assert.That(snapshot.EquippedIds[0], Is.EqualTo(candidates[0].Id));
                Assert.That(confirm.interactable, Is.True);
                ButtonAt(workspace, "EquippedSlots/ContentSafeRect/Slot1").onClick.Invoke();
                Assert.That(equip.interactable, Is.False, "중복 장착 사유를 클릭 전에 안내한다.");
                Assert.That(view.TryHandleCancel(), Is.True);
                Assert.That(confirm.interactable, Is.False);
                Assert.That(view.TryHandleCancel(), Is.False);
                equip.onClick.Invoke();
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
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2f), label.name + ": " + label.text);
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
