using Baseball.Core.Balance;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    /// <summary>배포 기본 성장 콘텐츠가 정적 정합성 검사를 통과하는지 검증한다.</summary>
    public sealed class GrowthContentValidatorTests
    {
        [Test]
        public void Validate_기본성장콘텐츠에는오류가없다()
        {
            ContentValidationIssue[] issues = new GrowthContentValidator()
                .Validate(GrowthBalanceTable.CreateDefault());

            Assert.That(issues, Is.Empty);
        }
    }
}
