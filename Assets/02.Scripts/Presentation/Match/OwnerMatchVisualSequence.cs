using Baseball.Simulation.PlateAppearance;

namespace Baseball.Presentation.Match
{
    /// <summary>타석 결과를 재생할 2D 경기 이미지 연출 종류로 분류한다.</summary>
    public enum OwnerMatchVisualSequenceKind
    {
        PitchOnly = 0,
        SwingMiss = 1,
        ContactOnly = 2,
        BallCaught = 3,
        SafeHit = 4
    }

    /// <summary>공식 타석 결과와 이미지 연출의 대응을 한 곳에서 관리한다.</summary>
    public static class OwnerMatchVisualSequenceResolver
    {
        public static OwnerMatchVisualSequenceKind Resolve(PlateAppearanceResult result)
        {
            return result switch
            {
                PlateAppearanceResult.Strikeout => OwnerMatchVisualSequenceKind.SwingMiss,
                PlateAppearanceResult.FlyOut or PlateAppearanceResult.BuntPopOut =>
                    OwnerMatchVisualSequenceKind.BallCaught,
                PlateAppearanceResult.Single or PlateAppearanceResult.Double or PlateAppearanceResult.Triple or
                    PlateAppearanceResult.BuntSingle => OwnerMatchVisualSequenceKind.SafeHit,
                PlateAppearanceResult.GroundOut or PlateAppearanceResult.HomeRun or
                    PlateAppearanceResult.ReachedOnError or PlateAppearanceResult.FieldersChoice or
                    PlateAppearanceResult.SacrificeBunt => OwnerMatchVisualSequenceKind.ContactOnly,
                _ => OwnerMatchVisualSequenceKind.PitchOnly
            };
        }
    }
}
