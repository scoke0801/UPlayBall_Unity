// PitchArsenalBalance.json에서 pitch_balance_codegen.py로 생성. 수치는 JSON에서 변경한다.
using Baseball.Core.Players;
namespace Baseball.Core.Balance
{
    public sealed partial class PitchArsenalBalance
    {
        /// <summary>동일 JSON의 자동 생성된 Headless/기존 저장 호환 기본값이다.</summary>
        public static PitchArsenalBalance CreateDefault()
        {
            return new PitchArsenalBalance("pitch-arsenal-v1", new[]
            {
                new PitchTypeDefinition(PitchType.FourSeamFastball, "포심 패스트볼", 100.0d, 0.0d, 0.0d, 1.0d, 1.0d, 1.0d, 0.25d, 0.005d, 0.12d, 0.0d, 0.0d, 0.0d, false, 1.0d, 1.0d, 1.0d, 1.0d),
                new PitchTypeDefinition(PitchType.TwoSeamFastball, "투심 패스트볼", 35.0d, 0.0d, 0.0d, 0.95d, 1.05d, 1.0d, 0.2d, 0.05d, 0.15d, 0.002d, 2.0d, 0.0d, false, 1.0d, 1.0d, 1.0d, 1.0d),
                new PitchTypeDefinition(PitchType.Cutter, "커터", 23.0d, 0.2d, 0.0d, 0.95d, 1.12d, 0.7d, 0.18d, 0.16d, 0.13d, 0.006d, 5.0d, 0.0d, false, 0.5d, 1.0d, 1.0d, 1.2d),
                new PitchTypeDefinition(PitchType.Slider, "슬라이더", 85.0d, 0.0d, 0.0d, 1.15d, 1.0d, 0.35d, 0.16d, 0.38d, 0.1d, 0.01d, 13.0d, 0.0d, false, 1.0d, 1.0d, 1.0d, 1.2d),
                new PitchTypeDefinition(PitchType.Curveball, "커브", 48.0d, 0.1d, 1.0d, 1.12d, 1.05d, 0.15d, 0.12d, 0.4d, 0.12d, 0.015d, 24.0d, 0.0d, false, 1.0d, 1.0d, 1.2d, 1.0d),
                new PitchTypeDefinition(PitchType.Changeup, "체인지업", 50.0d, 0.1d, 0.0d, 1.0d, 1.15d, 0.22d, 0.08d, 0.24d, 0.3d, 0.008d, 16.0d, 0.0d, false, 1.0d, 1.0d, 1.2d, 1.0d),
                new PitchTypeDefinition(PitchType.Splitter, "스플리터", 13.0d, 0.5d, 2.0d, 1.15d, 1.35d, 0.4d, 0.25d, 0.3d, 0.1d, 0.018d, 12.0d, 0.0d, true, 1.0d, 1.0d, 1.0d, 1.2d),
                new PitchTypeDefinition(PitchType.Sinker, "싱커", 22.0d, 0.0d, 0.0d, 0.98d, 1.1d, 0.8d, 0.2d, 0.18d, 0.16d, 0.006d, 5.0d, 0.0d, false, 1.0d, 1.0d, 1.0d, 1.0d),
                new PitchTypeDefinition(PitchType.Sweeper, "스위퍼", 8.0d, 0.25d, 1.0d, 1.05d, 1.3d, 0.28d, 0.15d, 0.42d, 0.09d, 0.017d, 16.0d, 0.0d, false, 0.03d, 1.5d, 1.0d, 1.0d),
                new PitchTypeDefinition(PitchType.Slurve, "슬러브", 6.0d, 0.1d, 1.0d, 1.02d, 1.15d, 0.2d, 0.14d, 0.4d, 0.12d, 0.014d, 20.0d, 0.0d, false, 1.0d, 1.0d, 1.0d, 1.0d),
                new PitchTypeDefinition(PitchType.KnuckleCurve, "너클 커브", 3.0d, 0.5d, 3.0d, 1.12d, 1.45d, 0.16d, 0.15d, 0.44d, 0.12d, 0.02d, 23.0d, 0.0d, true, 1.0d, 1.0d, 1.0d, 1.0d),
                new PitchTypeDefinition(PitchType.CircleChangeup, "서클 체인지업", 12.0d, 0.15d, 0.0d, 1.0d, 1.2d, 0.2d, 0.08d, 0.24d, 0.32d, 0.008d, 17.0d, 0.0d, false, 1.0d, 1.0d, 1.2d, 1.0d),
                new PitchTypeDefinition(PitchType.Forkball, "포크볼", 8.0d, 0.25d, 1.0d, 0.98d, 1.3d, 0.32d, 0.22d, 0.34d, 0.1d, 0.02d, 15.0d, 0.0d, true, 1.0d, 1.0d, 1.0d, 1.2d),
                new PitchTypeDefinition(PitchType.Screwball, "스크루볼", 1.2d, 0.1d, 2.0d, 0.8d, 1.5d, 0.18d, 0.1d, 0.4d, 0.18d, 0.024d, 22.0d, 0.0d, true, 1.0d, 1.0d, 1.0d, 1.0d),
                new PitchTypeDefinition(PitchType.Knuckleball, "너클볼", 0.35d, 0.3d, 4.0d, 0.65d, 1.8d, 0.08d, 0.12d, 0.46d, 0.06d, 0.04d, 38.0d, 0.0d, true, 1.0d, 1.0d, 1.0d, 1.0d),
            }, new PitchGradeBalance(new[] { "D", "C", "B", "A", "S", "SS" }, new double[] { 0.0d, 35.0d, 50.0d, 65.0d, 80.0d, 95.0d }),
                new PitchGrowthBalance(1.15d, 1.0d, 0.9d, 0.78d, 0.56d), 146.0d, 0.32d, 50.0d, 0.7d);
        }
    }
}
