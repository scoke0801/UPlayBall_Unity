using Baseball.Core.Players;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Presentation.Match
{
    /// <summary>구단주 중계의 보조 화면에 표시할 클로즈업 종류다.</summary>
    public enum OwnerMatchHighlightKind { None, BatContact, GloveCatch, HomeRun, Slide, GreatCatch, Throw, Bunt }

    /// <summary>이미 공개한 사건만 소비해 결과를 미리 알리지 않는 삽입 컷 선택기다.</summary>
    public static class OwnerMatchHighlightCue
    {
        /// <summary>선행 조회 데이터 없이 공개 사건 자체의 근거로 장면을 고른다.</summary>
        public static OwnerMatchHighlightKind Resolve(in MatchEvent revealed)
        {
            // 시도 공개에는 접촉·성공을 단정하지 않는 번트 준비 자세만 보여준다.
            if (revealed.EventType == MatchEventType.BuntAttempted)
                return OwnerMatchHighlightKind.Bunt;
            if (revealed.EventType == MatchEventType.Pitch && revealed.PitchResult == PitchResult.InPlay)
                return OwnerMatchHighlightKind.BatContact;
            if (revealed.EventType == MatchEventType.Hit && revealed.PlateAppearanceResult == PlateAppearanceResult.HomeRun)
                return OwnerMatchHighlightKind.HomeRun;
            if (revealed.EventType is MatchEventType.StealSucceeded or MatchEventType.CaughtStealing)
                return OwnerMatchHighlightKind.Slide;
            if (revealed.EventType != MatchEventType.PlateAppearanceEnded) return OwnerMatchHighlightKind.None;
            if (revealed.PlateAppearanceResult is PlateAppearanceResult.Double or PlateAppearanceResult.Triple)
                return OwnerMatchHighlightKind.Slide;

            BallInPlayEventData play = revealed.BallInPlayData;
            if (!play.HasValue || !play.Fielding.HasValue || play.Fielding.FailureType != FieldingFailureType.None)
                return OwnerMatchHighlightKind.None;
            if (revealed.PlateAppearanceResult is PlateAppearanceResult.FlyOut or PlateAppearanceResult.BuntPopOut)
            {
                // 낮게 뻗는 비정형 직선 타구의 성공에만 낮은 호수비 컷을 쓴다. 다이빙 여부는 새로 단정하지 않는다.
                return !play.Fielding.WasRoutine && play.BattedBall.Type == BattedBallType.LineDrive
                    ? OwnerMatchHighlightKind.GreatCatch : OwnerMatchHighlightKind.GloveCatch;
            }
            if (revealed.PlateAppearanceResult is PlateAppearanceResult.GroundOut or PlateAppearanceResult.SacrificeBunt)
            {
                // 1루수가 직접 베이스를 밟은 아웃에 송구 그림을 붙이지 않는다.
                return play.Fielding.FielderPosition == PlayerPosition.FirstBase
                    ? OwnerMatchHighlightKind.GloveCatch : OwnerMatchHighlightKind.Throw;
            }
            return OwnerMatchHighlightKind.None;
        }
    }
}
