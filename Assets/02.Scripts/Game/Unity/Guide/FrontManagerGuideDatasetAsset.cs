using System;
using Baseball.Game.Guide;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>원본 JSON과 Schema를 Player Build 의존성에 포함하는 읽기 전용 참조 Asset이다.</summary>
    [CreateAssetMenu(
        fileName = "FrontManagerGuideDataset",
        menuName = "Baseball/Data/Front Manager Guide Dataset")]
    public sealed class FrontManagerGuideDatasetAsset : ScriptableObject
    {
        public const string ResourcePath = "FrontManager/FrontManagerGuideDataset";

        [SerializeField] private TextAsset _dataset;
        [SerializeField] private TextAsset _schema;

        public TextAsset Dataset => _dataset;
        public TextAsset Schema => _schema;

        public void Configure(TextAsset dataset, TextAsset schema)
        {
            _dataset = dataset != null ? dataset : throw new ArgumentNullException(nameof(dataset));
            _schema = schema != null ? schema : throw new ArgumentNullException(nameof(schema));
        }

        public static bool TryLoadCatalog(
            out GuideDatasetCatalog catalog,
            out GuideValidationIssue[] issues)
        {
            FrontManagerGuideDatasetAsset asset = Resources.Load<FrontManagerGuideDatasetAsset>(ResourcePath);
            if (asset == null || asset._dataset == null)
            {
                catalog = null;
                issues = new[]
                {
                    new GuideValidationIssue(
                        "DATASET_ASSET_MISSING",
                        ResourcePath,
                        "FrontManager Guide Dataset 참조 Asset 또는 JSON이 없습니다.")
                };
                return false;
            }

            GuideDatasetData data;
            try
            {
                data = JsonUtility.FromJson<GuideDatasetData>(asset._dataset.text);
            }
            catch (ArgumentException exception)
            {
                catalog = null;
                issues = new[]
                {
                    new GuideValidationIssue("JSON_PARSE", asset._dataset.name, exception.Message)
                };
                return false;
            }
            return GuideDatasetFactory.TryCreate(data, out catalog, out issues);
        }
    }
}
