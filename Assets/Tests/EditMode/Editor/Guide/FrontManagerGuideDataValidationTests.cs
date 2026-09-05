using Baseball.Editor.Guide;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Editor.Guide
{
    /// <summary>CI EditMode 경로에서 JSON Schema와 Guide 의미 계약을 실제 원본 파일로 검증한다.</summary>
    public sealed class FrontManagerGuideDataValidationTests
    {
        [Test]
        public void Dataset_Schema와상호참조검증을통과한다()
        {
            string[] errors = FrontManagerGuideDataValidation.Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Schema_필수CueId누락을검출한다()
        {
            const string instance = "{\"cueDefinitions\":[{}]}";
            const string schema = "{\"type\":\"object\",\"properties\":{\"cueDefinitions\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"required\":[\"cueId\"]}}}}";

            string[] errors = JsonSchemaSubsetValidator.Validate(instance, schema);

            Assert.IsNotEmpty(errors);
            StringAssert.Contains("cueId", errors[0]);
        }
    }
}
