using System;
using System.Collections.Generic;
using System.IO;
using Baseball.Editor.Tools;
using Baseball.Presentation.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Editor.UI
{
    /// <summary>커리어 UI의 중첩 장식 프레임과 레거시 Box 표현 재유입을 정적으로 검사한다.</summary>
    public static class CareerUiLegacyVisualValidator
    {
        private const string DashboardSourcePath =
            "Assets/02.Scripts/Presentation/Career/UI_Scene_CareerDashboard.cs";

        private static readonly string[] DecoratedCareerSourcePaths =
        {
            "Assets/02.Scripts/Presentation/Career/UI_Scene_League.cs",
            "Assets/02.Scripts/Presentation/Career/UI_Scene_Contract.cs",
            "Assets/02.Scripts/Presentation/Career/UI_Scene_Team.cs",
            "Assets/02.Scripts/Presentation/Career/UI_Scene_CareerGrowth.Rendering.cs",
            "Assets/02.Scripts/Presentation/Career/UI_Scene_CareerSchedule.cs"
        };

        private static readonly string[] FlatCareerSourcePaths =
        {
            "Assets/02.Scripts/Presentation/Career/UI_Scene_Team.Helpers.cs",
            "Assets/02.Scripts/Presentation/Career/UI_Popup_CareerNews.cs"
        };

        private static readonly string[] LegacyObjectNames =
        {
            "PanelShadow",
            "AdvanceFrame",
            "BoxPanel",
            "Bevel"
        };

        [BaseballEditorTool(
            "UI",
            "Career UI 레거시 시각 요소 검증",
            "중첩 프레임, Legacy Sprite, 장식 Image Raycast와 ContentSafeArea 위반을 경로와 함께 보고합니다.",
            order: 110,
            impact: ToolImpact.ReadOnly)]
        public static void ValidateFromToolsLauncher()
        {
            List<string> issues = CollectIssues();
            LogResult(issues);
            EditorUtility.DisplayDialog(
                "Career UI 검증",
                issues.Count == 0
                    ? "Career UI 레거시 시각 요소 위반이 없습니다."
                    : $"{issues.Count}개 위반을 발견했습니다. Console의 경로를 확인하세요.",
                "확인");
        }

        /// <summary>CI에서 위반을 빌드 실패로 처리하기 위한 진입점이다.</summary>
        public static void ValidateForCi()
        {
            List<string> issues = CollectIssues();
            LogResult(issues);
            if (issues.Count > 0)
                throw new BuildFailedException($"Career UI 레거시 시각 요소 위반 {issues.Count}개");
        }

        private static List<string> CollectIssues()
        {
            var issues = new List<string>();
            var inspectedFrames = new HashSet<int>();
            ValidateCareerUiPrefabs(issues, inspectedFrames);
            ValidateLoadedFrames(issues, inspectedFrames);
            ValidateRuntimeDashboardSource(issues);
            ValidateDecoratedCareerSources(issues);
            return issues;
        }

        private static void ValidateCareerUiPrefabs(List<string> issues, HashSet<int> inspectedFrames)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int index = 0; index < prefabGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                if (!IsUiPrefabPath(path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                ValidateLegacySpriteReferences(prefab.transform, path, issues);
                CareerUiFrame[] frames = prefab.GetComponentsInChildren<CareerUiFrame>(true);
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                    ValidateFrame(frames[frameIndex], path, issues, inspectedFrames);
            }
        }

        private static void ValidateLoadedFrames(List<string> issues, HashSet<int> inspectedFrames)
        {
            CareerUiFrame[] frames = Resources.FindObjectsOfTypeAll<CareerUiFrame>();
            for (int index = 0; index < frames.Length; index++)
            {
                CareerUiFrame frame = frames[index];
                if (frame == null)
                    continue;
                ValidateFrame(frame, "LoadedObject", issues, inspectedFrames);
            }
        }

        private static void ValidateFrame(
            CareerUiFrame frame,
            string owner,
            List<string> issues,
            HashSet<int> inspectedFrames)
        {
            if (frame == null || !inspectedFrames.Add(frame.GetInstanceID()))
                return;

            string framePath = BuildHierarchyPath(frame.transform);
            Image decorativeFrame = frame.DecorativeFrame;
            if (decorativeFrame == null)
            {
                AddIssue(issues, owner, framePath, "DecorativeFrame Image 참조가 없습니다.");
            }
            else
            {
                CareerUiVisualElement visual = decorativeFrame.GetComponent<CareerUiVisualElement>();
                if (visual == null || visual.Role != CareerUiVisualRole.DecorativeFrame)
                {
                    AddIssue(issues, owner, BuildHierarchyPath(decorativeFrame.transform),
                        "외곽 Image의 Visual Role이 DecorativeFrame이 아닙니다.");
                }
                if (decorativeFrame.raycastTarget)
                {
                    AddIssue(issues, owner, BuildHierarchyPath(decorativeFrame.transform),
                        "장식 프레임의 Raycast Target이 활성화되어 있습니다.");
                }
            }

            int outerFrameCount = CountOwnedDecorativeFrames(frame);
            if (outerFrameCount != 1)
            {
                AddIssue(issues, owner, framePath,
                    $"한 카드가 소유한 외곽 장식 프레임이 {outerFrameCount}개입니다. 정확히 1개여야 합니다.");
            }

            RectTransform content = frame.ContentSafeArea;
            RectTransform header = frame.HeaderRoot;
            RectTransform interaction = frame.InteractionRoot;
            if (content == null || header == null || interaction == null)
            {
                AddIssue(issues, owner, framePath,
                    "HeaderRoot, ContentSafeArea, InteractionRoot 중 하나가 누락되었습니다.");
                return;
            }

            CareerUiFrame[] nestedFrames = content.GetComponentsInChildren<CareerUiFrame>(true);
            for (int index = 0; index < nestedFrames.Length; index++)
            {
                AddIssue(issues, owner, BuildHierarchyPath(nestedFrames[index].transform),
                    "ContentSafeArea 아래에 장식 프레임이 중첩되어 있습니다.");
            }

            Image[] contentImages = content.GetComponentsInChildren<Image>(true);
            for (int index = 0; index < contentImages.Length; index++)
                ValidateContentImage(contentImages[index], owner, issues);

            ValidateSafeAreaOwnership(frame, owner, issues);
        }

        private static int CountOwnedDecorativeFrames(CareerUiFrame owner)
        {
            int count = 0;
            CareerUiVisualElement[] visuals = owner.GetComponentsInChildren<CareerUiVisualElement>(true);
            for (int index = 0; index < visuals.Length; index++)
            {
                CareerUiVisualElement visual = visuals[index];
                if (visual.Role == CareerUiVisualRole.DecorativeFrame &&
                    visual.GetComponentInParent<CareerUiFrame>() == owner)
                {
                    count++;
                }
            }
            return count;
        }

        private static void ValidateContentImage(Image image, string owner, List<string> issues)
        {
            if (image == null)
                return;

            string path = BuildHierarchyPath(image.transform);
            CareerUiVisualElement visual = image.GetComponent<CareerUiVisualElement>();
            if (visual != null && visual.Role == CareerUiVisualRole.DecorativeFrame)
                AddIssue(issues, owner, path, "ContentSafeArea 안에 DecorativeFrame이 있습니다.");

            if (ContainsLegacyName(image.name))
                AddIssue(issues, owner, path, "레거시 Box/Shadow/Bevel 이름의 Image가 남아 있습니다.");

            Outline outline = image.GetComponent<Outline>();
            if (outline != null && outline.enabled && image.GetComponentInParent<Button>() == null)
                AddIssue(issues, owner, path, "일반 데이터 Image에 Outline이 활성화되어 있습니다.");

            string spritePath = image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : string.Empty;
            if (IsLegacySpritePath(spritePath))
                AddIssue(issues, owner, path, $"Legacy Sprite 참조: {spritePath}");
        }

        private static void ValidateSafeAreaOwnership(CareerUiFrame frame, string owner, List<string> issues)
        {
            Text[] texts = frame.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                Transform target = texts[index].transform;
                if (!IsInsideFrameSlot(frame, target))
                    AddIssue(issues, owner, BuildHierarchyPath(target), "Text가 프레임 안전 슬롯 밖에 있습니다.");
            }

            Button[] buttons = frame.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Transform target = buttons[index].transform;
                if (!IsInsideFrameSlot(frame, target))
                    AddIssue(issues, owner, BuildHierarchyPath(target), "Button이 프레임 안전 슬롯 밖에 있습니다.");
            }
        }

        private static bool IsInsideFrameSlot(CareerUiFrame frame, Transform target)
        {
            return IsSelfOrChildOf(target, frame.HeaderRoot)
                || IsSelfOrChildOf(target, frame.ContentSafeArea)
                || IsSelfOrChildOf(target, frame.InteractionRoot);
        }

        private static bool IsSelfOrChildOf(Transform target, Transform parent)
        {
            return target != null && parent != null && (target == parent || target.IsChildOf(parent));
        }

        private static void ValidateLegacySpriteReferences(Transform root, string owner, List<string> issues)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];
                string spritePath = image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : string.Empty;
                if (IsLegacySpritePath(spritePath))
                    AddIssue(issues, owner, BuildHierarchyPath(image.transform), $"Legacy Sprite 참조: {spritePath}");
            }
        }

        private static void ValidateRuntimeDashboardSource(List<string> issues)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string absolutePath = Path.Combine(projectRoot, DashboardSourcePath);
            if (!File.Exists(absolutePath))
            {
                AddIssue(issues, DashboardSourcePath, string.Empty, "런타임 Dashboard 소스를 찾을 수 없습니다.");
                return;
            }

            string source = File.ReadAllText(absolutePath);
            string[] forbiddenTokens = { "PanelShadow", "AdvanceFrame", "CreateInfoChip" };
            for (int index = 0; index < forbiddenTokens.Length; index++)
            {
                if (source.IndexOf(forbiddenTokens[index], StringComparison.Ordinal) >= 0)
                {
                    AddIssue(issues, DashboardSourcePath, forbiddenTokens[index],
                        "런타임 생성 소스에 레거시 시각 구조 토큰이 남아 있습니다.");
                }
            }

            string[] requiredTokens =
            {
                "CareerUiFrame",
                "ContentSafeArea",
                "CareerUiVisualRole.DecorativeFrame",
                "CreateVerticalScrollArea"
            };
            for (int index = 0; index < requiredTokens.Length; index++)
            {
                if (source.IndexOf(requiredTokens[index], StringComparison.Ordinal) < 0)
                {
                    AddIssue(issues, DashboardSourcePath, requiredTokens[index],
                        "필수 프레임 또는 안전 영역 구조가 소스에서 확인되지 않습니다.");
                }
            }
        }

        private static void ValidateDecoratedCareerSources(List<string> issues)
        {
            string[] forbiddenTokens = { "name + \"Shadow\"", "PanelShadow" };
            string[] decoratedTokens =
            {
                "CareerUiVisualRole.DecorativeFrame",
                "ContentSafeArea"
            };
            for (int index = 0; index < DecoratedCareerSourcePaths.Length; index++)
            {
                ValidateRuntimeSource(
                    DecoratedCareerSourcePaths[index], forbiddenTokens, decoratedTokens, issues);
            }

            string[] flatTokens = { "CareerUiVisualRole.FlatSurface" };
            for (int index = 0; index < FlatCareerSourcePaths.Length; index++)
            {
                ValidateRuntimeSource(
                    FlatCareerSourcePaths[index], forbiddenTokens, flatTokens, issues);
            }
        }

        private static void ValidateRuntimeSource(
            string sourcePath,
            IReadOnlyList<string> forbiddenTokens,
            IReadOnlyList<string> requiredTokens,
            List<string> issues)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string absolutePath = Path.Combine(projectRoot, sourcePath);
            if (!File.Exists(absolutePath))
            {
                AddIssue(issues, sourcePath, string.Empty, "런타임 Career UI 소스를 찾을 수 없습니다.");
                return;
            }

            string source = File.ReadAllText(absolutePath);
            for (int index = 0; index < forbiddenTokens.Count; index++)
            {
                string token = forbiddenTokens[index];
                if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    AddIssue(issues, sourcePath, token,
                        "런타임 생성 소스에 레거시 시각 구조 토큰이 남아 있습니다.");
                }
            }

            for (int index = 0; index < requiredTokens.Count; index++)
            {
                string token = requiredTokens[index];
                if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                {
                    AddIssue(issues, sourcePath, token,
                        "필수 장식 프레임 또는 평면 시각 역할이 소스에서 확인되지 않습니다.");
                }
            }
        }

        private static bool IsUiPrefabPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsLegacyName(string name)
        {
            for (int index = 0; index < LegacyObjectNames.Length; index++)
            {
                if (name.IndexOf(LegacyObjectNames[index], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool IsLegacySpritePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            string normalized = path.Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(normalized);
            return normalized.IndexOf("/Legacy/", StringComparison.OrdinalIgnoreCase) >= 0
                || fileName.IndexOf("BoxPanel", StringComparison.OrdinalIgnoreCase) >= 0
                || fileName.IndexOf("Bevel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
                return string.Empty;
            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static void AddIssue(List<string> issues, string owner, string path, string message)
        {
            issues.Add($"[{owner}] {path} - {message}");
        }

        private static void LogResult(List<string> issues)
        {
            if (issues.Count == 0)
            {
                Debug.Log("[CareerUiLegacyVisualValidator] 위반 없음");
                return;
            }

            for (int index = 0; index < issues.Count; index++)
                Debug.LogError($"[CareerUiLegacyVisualValidator] {issues[index]}");
            Debug.LogError($"[CareerUiLegacyVisualValidator] 총 {issues.Count}개 위반");
        }
    }
}
