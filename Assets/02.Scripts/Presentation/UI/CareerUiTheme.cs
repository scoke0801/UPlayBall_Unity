using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>커리어 화면이 공유하는 색상, 간격과 프레임 안전 영역 토큰이다.</summary>
    public static class CareerUiTheme
    {
        public const float Space1 = 4f;
        public const float Space2 = 8f;
        public const float Space3 = 12f;
        public const float Space4 = 16f;
        public const float Space5 = 24f;
        public const float Space6 = 32f;

        public static readonly Color Background = new(0.006f, 0.02f, 0.034f, 1f);
        public static readonly Color TopBar = new(0.008f, 0.027f, 0.052f, 1f);
        public static readonly Color Panel = new(0.018f, 0.065f, 0.108f, 0.99f);
        public static readonly Color PanelDark = new(0.009f, 0.035f, 0.061f, 0.99f);
        public static readonly Color Surface = new(0.024f, 0.086f, 0.139f, 0.74f);
        public static readonly Color SurfaceSubtle = new(0.018f, 0.062f, 0.098f, 0.66f);
        public static readonly Color SurfaceSelected = new(0.025f, 0.20f, 0.36f, 0.88f);
        public static readonly Color PortraitBackdrop = new(0.18f, 0.25f, 0.32f, 1f);
        public static readonly Color Border = new(0.28f, 0.46f, 0.62f, 1f);
        public static readonly Color Divider = new(0.14f, 0.31f, 0.45f, 0.72f);
        public static readonly Color Primary = new(0.13f, 0.55f, 0.92f, 1f);
        public static readonly Color PrimaryBright = new(0.12f, 0.67f, 1f, 1f);
        public static readonly Color Success = new(0.27f, 0.77f, 0.47f, 1f);
        public static readonly Color AccentGold = new(0.84f, 0.67f, 0.32f, 1f);
        public static readonly Color Warning = new(0.94f, 0.56f, 0.16f, 1f);
        public static readonly Color Loss = new(0.82f, 0.27f, 0.31f, 1f);
        public static readonly Color TextPrimary = new(0.94f, 0.97f, 1f, 1f);
        public static readonly Color TextSecondary = new(0.62f, 0.71f, 0.8f, 1f);
        public static readonly Color TextMuted = new(0.34f, 0.40f, 0.49f, 1f);
        public static readonly Color Error = new(1f, 0.42f, 0.42f, 1f);
        public static readonly Color TopGlow = new(0.02f, 0.18f, 0.31f, 0.24f);
        public static readonly Color BottomGlow = new(0.02f, 0.16f, 0.28f, 0.2f);
        public static readonly Color PrimaryOutline = new(0.05f, 0.34f, 0.62f, 0.9f);
        public static readonly Color StrongOutline = new(0.02f, 0.16f, 0.34f, 1f);
        public static readonly Color MetricOutline = new(0.04f, 0.25f, 0.5f, 1f);
        public static readonly Color RoleBand = new(0.025f, 0.13f, 0.2f, 1f);
        public static readonly Color PrimaryAction = new(0.025f, 0.31f, 0.61f, 1f);
        public static readonly Color SecondaryAction = new(0.12f, 0.16f, 0.2f, 1f);
        public static readonly Color SpecialAction = new(0.42f, 0.25f, 0.04f, 1f);
        public static readonly Color SuccessAction = new(0.08f, 0.34f, 0.28f, 1f);
        public static readonly Color InputBlocker = new(0f, 0.01f, 0.02f, 0.82f);
        public static readonly Color FeedSurface = new(0.01f, 0.045f, 0.078f, 0.92f);
        public static readonly Color CurrentRow = new(0.035f, 0.13f, 0.22f, 1f);
        public static readonly Color TeamBadgeSurface = new(0.015f, 0.12f, 0.2f, 0.88f);
        public static readonly Color ProgressTrack = new(0.11f, 0.16f, 0.2f, 1f);
        public static readonly Color RatingMid = new(0.38f, 0.67f, 0.86f, 1f);

        // Vector4 순서는 left, bottom, right, top이다.
        public static readonly Vector4 UniversalFramePadding = new(32f, 28f, 32f, 76f);
        public static readonly Vector4 HeroFramePadding = new(40f, 32f, 40f, 80f);
    }
}
