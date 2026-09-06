using System;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>영구·일시 보정 조합에서 상한, 순서 보존 및 기존 확률 입력의 연속성을 검증한다.</summary>
    public sealed class MatchRatingCurveTests
    {
        [Test]
        public void EndgameLayersPreserveOrderingUntilSharedHardCap()
        {
            var caps = EffectiveRatingCapTable.CreateInitial();
            int[] editions = { 0, 1, 3, 5 };
            int[] enhancement = { 0, 5 };
            int[] training = { 0, 3 };
            int[] teamColor = { 0, 17 };
            int[] condition = { -4, 0, 4 };
            foreach (int edition in editions)
            foreach (int level in enhancement)
            foreach (int growth in training)
            foreach (int color in teamColor)
            foreach (int form in condition)
            {
                EffectiveRatingResult low = EffectiveRatingResolver.Resolve(34, edition, growth, level, color, form, 0, caps);
                EffectiveRatingResult high = EffectiveRatingResolver.Resolve(75, edition, growth, level, color, form, 0, caps);
                Assert.That(high.CurveRating, Is.GreaterThan(low.CurveRating));
                Assert.That(high.Rating, Is.LessThanOrEqualTo(140));
            }
            // 서로 다른 투자 수준도 단순 보너스 합산만으로 표본 중앙값 순서를 뒤집지 않는다.
            Assert.That(EffectiveRatingResolver.Resolve(34, 5, 3, 5, 17, 4, 0, caps).CurveRating,
                Is.LessThan(EffectiveRatingResolver.Resolve(75, 0, 0, 0, 0, 0, 0, caps).CurveRating));
        }

        [Test]
        public void CurvePreservesNormalRatingsAndDiminishesOnlyBeyondSoftCap()
        {
            var caps = EffectiveRatingCapTable.CreateInitial();
            for (int rating = 1; rating <= 120; rating++)
                Assert.That(MatchRatingCurve.Resolve(rating, caps), Is.EqualTo(rating));
            Assert.That(MatchRatingCurve.Resolve(121, caps), Is.EqualTo(120.5d));
            Assert.That(MatchRatingCurve.Resolve(140, caps), Is.EqualTo(130d));
            Assert.That(MatchRatingCurve.Resolve(1000, caps), Is.EqualTo(130d));
            Assert.That(MatchRatingCurve.Resolve(120.001d, caps), Is.EqualTo(120.0005d).Within(1e-9));
        }

        [Test]
        public void CurveConsumesInjectedCapsAndRejectsNonFiniteRatings()
        {
            var caps = new EffectiveRatingCapTable(110, 150, 0.25d);
            Assert.That(MatchRatingCurve.Resolve(150, caps), Is.EqualTo(120d));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRatingCurve.Resolve(double.NaN, caps));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRatingCurve.Resolve(double.PositiveInfinity, caps));
        }
    }
}
