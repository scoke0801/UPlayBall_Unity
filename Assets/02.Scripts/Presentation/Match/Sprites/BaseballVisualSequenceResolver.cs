using Baseball.Core.Players;
using Baseball.Simulation.Match;

namespace Baseball.Presentation.Match.Sprites
{
    /// <summary>공식 기록에서 그림의 종류만 선택하고 결과를 재판정하지 않는 경계다.</summary>
    public static class BaseballVisualSequenceResolver
    {
        /// <summary>공식 투구손에 대응하는 모션 ID다.</summary>
        public static string PitchClip(Handedness hand) => hand == Handedness.Left ? "Pitcher.Pitch.L" : "Pitcher.Pitch.R";
        /// <summary>스위치 타자의 실제 타석 방향까지 해석된 입력을 받는다.</summary>
        public static string SwingClip(Handedness hand) => hand == Handedness.Left ? "Batter.DuelSwing.L" : "Batter.DuelSwing.R";
        /// <summary>공식 타구 종류에 맞는 수비 자세를 선택한다.</summary>
        public static string FieldingClip(BattedBallType type) =>
            type is BattedBallType.FlyBall or BattedBallType.PopUp or BattedBallType.LineDrive
                ? "Fielder.OutfieldFlyCatch" : "Fielder.InfieldGrounder";

        /// <summary>타구 종류를 구별할 시각적 높이만 정한다.</summary>
        public static float GetBallHeight(BattedBallType type, float maximum) => type switch
        {
            BattedBallType.FlyBall or BattedBallType.PopUp => maximum,
            BattedBallType.LineDrive => maximum * 0.22f,
            _ => 0f
        };

        /// <summary>공의 소유권 전환 사건이 없는 시트는 실제 경기에서 사용하지 않는다.</summary>
        public static bool CanPresent(SpriteAnimationCatalog catalog, Handedness throwing, Handedness batting)
        {
            return catalog != null && catalog.fieldLayout != null && catalog.fieldLayout.background != null &&
                catalog.TryGetClip(PitchClip(throwing), out var pitch) && HasMatchingHand(pitch, throwing) && pitch.TryGetEventTime(SpriteAnimationEvent.BallRelease, out _) &&
                catalog.TryGetClip(SwingClip(batting), out var swing) && HasMatchingHand(swing, batting) && swing.TryGetEventTime(SpriteAnimationEvent.BatContact, out _) &&
                catalog.TryGetClip("Fielder.InfieldGrounder", out var ground) && ground.TryGetEventTime(SpriteAnimationEvent.GloveContact, out _) &&
                catalog.TryGetClip("Fielder.OutfieldFlyCatch", out var fly) && fly.TryGetEventTime(SpriteAnimationEvent.GloveContact, out _);
        }

        private static bool HasMatchingHand(SpriteClipDefinition clip, Handedness hand) =>
            clip.handedness == (hand == Handedness.Left ? SpriteHandedness.Left : SpriteHandedness.Right);
    }
}
