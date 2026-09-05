using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>두 게임 모드가 공유하는 중립형 야구 관리 UI 색상과 간격 토큰이다.</summary>
    public static class CareerUiTheme
    {
        public const float Space1 = 4f;
        public const float Space2 = 8f;
        public const float Space3 = 12f;
        public const float Space4 = 16f;
        public const float Space5 = 24f;
        public const float Space6 = 32f;
        public const float SharedShellChromeHeight = 164f;

        public static readonly Color Background = new(0.055f, 0.063f, 0.066f, 1f);
        public static readonly Color TopBar = new(0.09f, 0.105f, 0.11f, 0.99f);
        public static readonly Color Panel = new(0.125f, 0.15f, 0.16f, 0.99f);
        public static readonly Color PanelDark = new(0.075f, 0.09f, 0.095f, 0.99f);
        public static readonly Color Surface = new(0.17f, 0.195f, 0.20f, 0.92f);
        public static readonly Color SurfaceSubtle = new(0.125f, 0.145f, 0.15f, 0.88f);
        public static readonly Color SurfaceSelected = new(0.20f, 0.285f, 0.235f, 0.96f);
        public static readonly Color ContextSurface = new(0.86f, 0.85f, 0.81f, 1f);
        public static readonly Color PortraitBackdrop = new(0.22f, 0.245f, 0.24f, 1f);
        public static readonly Color Border = new(0.42f, 0.46f, 0.46f, 1f);
        public static readonly Color Divider = new(0.27f, 0.31f, 0.31f, 0.82f);
        public static readonly Color Primary = new(0.34f, 0.53f, 0.39f, 1f);
        public static readonly Color PrimaryBright = new(0.47f, 0.64f, 0.51f, 1f);
        public static readonly Color Success = new(0.39f, 0.65f, 0.45f, 1f);
        public static readonly Color AccentGold = new(0.75f, 0.62f, 0.34f, 1f);
        public static readonly Color Warning = new(0.86f, 0.60f, 0.27f, 1f);
        public static readonly Color Loss = new(0.72f, 0.31f, 0.31f, 1f);
        public static readonly Color TextPrimary = new(0.94f, 0.93f, 0.88f, 1f);
        public static readonly Color Number = new(0.93f, 0.82f, 0.56f, 1f);
        public static readonly Color TextSecondary = new(0.72f, 0.75f, 0.73f, 1f);
        public static readonly Color TextMuted = new(0.46f, 0.50f, 0.49f, 1f);
        public static readonly Color TextOnLight = new(0.12f, 0.14f, 0.15f, 1f);
        public static readonly Color Error = new(0.90f, 0.38f, 0.36f, 1f);
        public static readonly Color TopGlow = new(0.28f, 0.39f, 0.31f, 0.10f);
        public static readonly Color BottomGlow = new(0.20f, 0.25f, 0.22f, 0.08f);
        public static readonly Color PrimaryOutline = new(0.27f, 0.46f, 0.32f, 0.9f);
        public static readonly Color StrongOutline = new(0.14f, 0.18f, 0.17f, 1f);
        public static readonly Color MetricOutline = new(0.31f, 0.38f, 0.35f, 1f);
        public static readonly Color RoleBand = new(0.11f, 0.17f, 0.14f, 1f);
        public static readonly Color PrimaryAction = new(0.20f, 0.42f, 0.27f, 1f);
        public static readonly Color SecondaryAction = new(0.16f, 0.18f, 0.18f, 1f);
        public static readonly Color SpecialAction = new(0.38f, 0.30f, 0.13f, 1f);
        public static readonly Color SuccessAction = new(0.16f, 0.36f, 0.22f, 1f);
        public static readonly Color InputBlocker = new(0.02f, 0.025f, 0.025f, 0.84f);
        public static readonly Color FeedSurface = new(0.085f, 0.105f, 0.105f, 0.95f);
        public static readonly Color CurrentRow = new(0.16f, 0.24f, 0.19f, 1f);
        public static readonly Color TeamBadgeSurface = new(0.12f, 0.17f, 0.14f, 0.94f);
        public static readonly Color ProgressTrack = new(0.18f, 0.20f, 0.20f, 1f);
        public static readonly Color RatingMid = new(0.53f, 0.65f, 0.57f, 1f);

        // Shared Game Shell 전용 토큰. 개별 화면의 기존 색 계약과 분리해 Shell을 한 번에 조정한다.
        public static readonly Color ShellBackdropTint = new(0.42f, 0.45f, 0.41f, 0.22f);
        public static readonly Color ShellHeader = new(0.065f, 0.075f, 0.075f, 0.985f);
        public static readonly Color ShellNavigation = new(0.085f, 0.10f, 0.10f, 0.985f);
        public static readonly Color ShellContext = new(0.88f, 0.89f, 0.88f, 0.98f);
        public static readonly Color ShellWorkspace = new(0.88f, 0.89f, 0.88f, 0.98f);
        public static readonly Color ShellInspector = new(0.88f, 0.89f, 0.88f, 0.98f);
        public static readonly Color ShellActionBar = new(0.91f, 0.92f, 0.91f, 1f);
        public static readonly Color ShellTab = new(0.12f, 0.145f, 0.145f, 1f);
        public static readonly Color ShellTabSelected = new(0.06f, 0.20f, 0.39f, 1f);
        public static readonly Color ShellBorder = new(0.33f, 0.36f, 0.34f, 0.95f);
        public static readonly Color ShellDivider = new(0.24f, 0.27f, 0.25f, 0.92f);
        public static readonly Color ShellGold = new(0.69f, 0.57f, 0.30f, 1f);
        public static readonly Color ShellField = new(0.31f, 0.49f, 0.34f, 1f);

        // docs/디자인/ref의 PC 관리 화면을 기준으로 한 밝은 업무 영역 토큰이다.
        public static readonly Color ReferenceCanvas = new(0.88f, 0.89f, 0.88f, 0.98f);
        public static readonly Color ReferencePanel = new(0.965f, 0.965f, 0.945f, 0.97f);
        public static readonly Color ReferencePanelHeader = new(0.91f, 0.92f, 0.91f, 1f);
        public static readonly Color ReferenceBorder = new(0.42f, 0.48f, 0.54f, 1f);
        public static readonly Color ReferenceAccent = new(0.06f, 0.20f, 0.39f, 1f);
        public static readonly Color ReferenceAccentLight = new(0.16f, 0.39f, 0.62f, 1f);
        public static readonly Color ReferenceText = new(0.10f, 0.12f, 0.14f, 1f);
        public static readonly Color ReferenceTextSecondary = new(0.31f, 0.35f, 0.39f, 1f);
        public static readonly Color ReferenceButton = new(0.82f, 0.83f, 0.82f, 1f);
        public static readonly Color ReferenceButtonHighlight = new(0.91f, 0.92f, 0.90f, 1f);

        // Vector4 순서는 left, bottom, right, top이다.
        public static readonly Vector4 UniversalFramePadding = new(32f, 28f, 32f, 76f);
        public static readonly Vector4 HeroFramePadding = new(40f, 32f, 40f, 80f);
        public static readonly Vector4 DenseFramePadding = new(20f, 24f, 20f, 68f);
        public static readonly Vector4 WideFramePadding = new(24f, 28f, 24f, 72f);
        public static readonly Vector4 PopupFramePadding = new(40f, 36f, 40f, 88f);
    }
}
