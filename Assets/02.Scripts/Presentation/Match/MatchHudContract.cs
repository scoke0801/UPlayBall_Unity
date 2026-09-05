namespace Baseball.Presentation.Match
{
    /// <summary>모드와 무관한 경기 HUD 표시 계약이다.</summary>
    public interface IMatchHudView
    {
        MatchHudPresentationModel CurrentModel { get; }
        void Present(MatchHudPresentationModel model);
    }
}
