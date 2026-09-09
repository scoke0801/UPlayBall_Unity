using System;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>상점·선수단·컬렉션에서 카드 등급을 동일한 한국어 이름으로 표시한다.</summary>
    public static class PlayerCardEditionText
    {
        public static string Get(PlayerCardEdition edition) => edition switch
        {
            PlayerCardEdition.Normal => "일반",
            PlayerCardEdition.AllStar => "올스타",
            PlayerCardEdition.GoldenGlove => "골든글러브",
            PlayerCardEdition.Mvp => "MVP",
            PlayerCardEdition.Rare => "레어",
            PlayerCardEdition.Ex => "EX",
            PlayerCardEdition.CareerHigh => "커리어하이",
            PlayerCardEdition.Legend => "레전드",
            _ => throw new ArgumentOutOfRangeException(nameof(edition))
        };
    }
}
