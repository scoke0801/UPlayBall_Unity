using Baseball.Presentation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 구단 엠블럼이 리그 접두사 대신 연고지를 표시하는지 검증한다.
    /// </summary>
    public sealed class CareerTeamNameFormatterTests
    {
        [TestCase("창원 블레이즈", "창원")]
        [TestCase("마이너 창원 블레이즈", "창원")]
        [TestCase("메이저 서울 블루윙스", "서울")]
        [TestCase("제주", "제주")]
        public void GetMonogram_연고지두글자를반환한다(string teamName, string expected)
        {
            Assert.That(CareerTeamNameFormatter.GetMonogram(teamName), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void GetMonogram_구단이름이없으면기본문구를반환한다(string teamName)
        {
            Assert.That(CareerTeamNameFormatter.GetMonogram(teamName), Is.EqualTo("UP"));
        }

        [TestCase("2024 LG 트윈스", "2024LG트윈스")]
        [TestCase("1988 빙그레 이글스", "1988빙그레이글스")]
        [TestCase("2024 부산 오로라", "2024부산오로라")]
        public void GetCompactName_연도와전체구단명을보존하고공백만제거한다(
            string teamName,
            string expected)
        {
            Assert.That(CareerTeamNameFormatter.GetCompactName(teamName), Is.EqualTo(expected));
        }
    }
}
