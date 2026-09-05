using System;

namespace Baseball.Game.Guide
{
    /// <summary>JsonUtility가 프런트 매니저 JSON 원문을 손실 없이 읽기 위한 저작 DTO다.</summary>
    [Serializable]
    public sealed class GuideDatasetData
    {
        public string datasetId;
        public string version;
        public string language;
        public string status;
        public GuideDesignContractData designContract;
        public GuideEnumData enums;
        public GuideCharacterData character;
        public GuideCueData[] cueDefinitions;
        public GuideFactTypeIndexData[] factTypeIndex;
    }

    [Serializable]
    public sealed class GuideDesignContractData
    {
        public bool guideDoesNotJudge;
        public bool factProducerOwnsGameLogic;
        public GuideVariationSelectionData deterministicVariationSelection;
        public string[] defaultSuppressionContexts;
        public string[] priorityOrder;
        public int homeFullDialogueMaxPerEntry;
        public string nonCriticalDuringMatch;
        public string[] runtimeContextKeys;
    }

    [Serializable]
    public sealed class GuideVariationSelectionData
    {
        public string strategy;
        public string hashAlgorithm;
        public string seedTemplate;
        public string fallbackSeedTemplate;
    }

    [Serializable]
    public sealed class GuideEnumData
    {
        public string[] modeScope;
        public string[] priority;
        public string[] presentationType;
        public string[] expressionKey;
        public string[] tone;
        public string[] cooldownScope;
    }

    [Serializable]
    public sealed class GuideCharacterData
    {
        public string characterId;
        public string displayNameKey;
        public string defaultExpression;
        public GuideExpressionAssetData expressions;
    }

    [Serializable]
    public sealed class GuideExpressionAssetData
    {
        public string Neutral;
        public string Welcome;
        public string Analysis;
        public string Concerned;
        public string Warning;
        public string Celebrate;
        public string Surprised;
        public string Calm;

        public string GetAssetKey(string expressionKey)
        {
            return expressionKey switch
            {
                "Neutral" => Neutral,
                "Welcome" => Welcome,
                "Analysis" => Analysis,
                "Concerned" => Concerned,
                "Warning" => Warning,
                "Celebrate" => Celebrate,
                "Surprised" => Surprised,
                "Calm" => Calm,
                _ => string.Empty
            };
        }
    }

    [Serializable]
    public sealed class GuideCueData
    {
        public string cueId;
        public string factType;
        public string modeScope;
        public string priority;
        public string presentationType;
        public string expressionKey;
        public string[] requiredPayload;
        public GuideRepeatPolicyData repeatPolicy;
        public GuideCtaData cta;
        public bool requiresAcknowledgement;
        public float autoDismissSec;
        public string[] suppressDuring;
        public string[] tags;
        public GuideVariationData[] variations;
    }

    [Serializable]
    public sealed class GuideRepeatPolicyData
    {
        public string dedupeKeyTemplate;
        public string cooldownScope;
        public int maxDisplaysPerScope;
    }

    [Serializable]
    public sealed class GuideCtaData
    {
        public string action;
        public string label;

        /// <summary>JsonUtility가 JSON null을 빈 DTO로 복원한 경우 선택지가 없는 것으로 정규화한다.</summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(action) && string.IsNullOrWhiteSpace(label);
    }

    [Serializable]
    public sealed class GuideVariationData
    {
        public string variationId;
        public int weight;
        public string tone;
        public string text;
    }

    [Serializable]
    public sealed class GuideFactTypeIndexData
    {
        public string factType;
        public string[] requiredPayload;
        public string[] cueIds;
        public string[] modeScopes;
    }
}
