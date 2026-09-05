using System;
using System.Collections.Generic;

namespace Baseball.Game.Guide
{
    /// <summary>Guide 데이터의 실패 위치와 플레이어 빌드를 막아야 하는 이유를 함께 보관한다.</summary>
    public readonly struct GuideValidationIssue
    {
        public GuideValidationIssue(string code, string path, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Path { get; }
        public string Message { get; }

        public override string ToString() => $"[{Code}] {Path}: {Message}";
    }

    /// <summary>JSON Schema의 구조 검증 뒤 Fact·CTA·표현·placeholder 상호 참조를 검사한다.</summary>
    public static class GuideDatasetValidator
    {
        public const int ExpectedCueCount = 100;
        public const int ExpectedVariationCountPerCue = 3;
        public const string WeightedHashStrategy = "WeightedHash";
        public const string HashAlgorithm = "FNV1A64_UTF8";
        public const string NonCriticalMatchPolicy = "queue-until-safe-point";

        public static GuideValidationIssue[] Validate(GuideDatasetData data)
        {
            var issues = new List<GuideValidationIssue>();
            if (data == null)
            {
                Add(issues, "DATASET_NULL", "$", "Dataset을 역직렬화하지 못했습니다.");
                return issues.ToArray();
            }

            RequireText(data.datasetId, "$.datasetId", issues);
            RequireText(data.version, "$.version", issues);
            RequireText(data.language, "$.language", issues);
            ValidateDesignContract(data.designContract, issues);
            ValidateEnumDeclarations(data.enums, issues);
            ValidateCharacter(data.character, issues);

            GuideFactTypeIndexData[] factIndex = data.factTypeIndex ?? Array.Empty<GuideFactTypeIndexData>();
            GuideCueData[] cues = data.cueDefinitions ?? Array.Empty<GuideCueData>();
            if (cues.Length != ExpectedCueCount)
            {
                Add(issues, "CUE_COUNT", "$.cueDefinitions",
                    $"v1 Dataset은 Cue {ExpectedCueCount}개여야 합니다. 현재 {cues.Length}개입니다.");
            }

            var factsByType = ValidateFactIndex(factIndex, issues);
            ValidateCues(cues, factsByType, data, issues);
            ValidateReverseIndex(cues, factIndex, issues);
            return issues.ToArray();
        }

        private static void ValidateDesignContract(
            GuideDesignContractData contract,
            ICollection<GuideValidationIssue> issues)
        {
            if (contract == null)
            {
                Add(issues, "DESIGN_CONTRACT_MISSING", "$.designContract", "설계 계약이 필요합니다.");
                return;
            }
            if (!contract.guideDoesNotJudge || !contract.factProducerOwnsGameLogic)
            {
                Add(issues, "FACT_OWNERSHIP", "$.designContract",
                    "Guide는 판단하지 않고 Application Fact만 소비해야 합니다.");
            }

            GuideVariationSelectionData selection = contract.deterministicVariationSelection;
            if (selection == null || selection.strategy != WeightedHashStrategy ||
                selection.hashAlgorithm != HashAlgorithm)
            {
                Add(issues, "HASH_CONTRACT", "$.designContract.deterministicVariationSelection",
                    $"{WeightedHashStrategy}/{HashAlgorithm} 계약이 필요합니다.");
            }
            else
            {
                ValidateTemplateTokens(
                    selection.seedTemplate,
                    new[] { "worldSeed", "eventId", "cueId" },
                    "$.designContract.deterministicVariationSelection.seedTemplate",
                    issues,
                    requireExactSet: true);
                ValidateTemplateTokens(
                    selection.fallbackSeedTemplate,
                    new[] { "saveId", "sequenceNo", "cueId" },
                    "$.designContract.deterministicVariationSelection.fallbackSeedTemplate",
                    issues,
                    requireExactSet: true);
            }

            if (contract.homeFullDialogueMaxPerEntry < 1)
            {
                Add(issues, "HOME_DIALOGUE_LIMIT", "$.designContract.homeFullDialogueMaxPerEntry",
                    "홈 진입당 FullDialogue 제한은 1 이상이어야 합니다.");
            }
            if (contract.nonCriticalDuringMatch != NonCriticalMatchPolicy)
            {
                Add(issues, "MATCH_QUEUE_POLICY", "$.designContract.nonCriticalDuringMatch",
                    $"지원 정책은 '{NonCriticalMatchPolicy}'입니다.");
            }
            if (!SequenceEquals(contract.priorityOrder, Enum.GetNames(typeof(GuidePriority))))
            {
                Add(issues, "PRIORITY_ORDER", "$.designContract.priorityOrder",
                    "priorityOrder는 런타임 Queue 우선순위와 같은 순서여야 합니다.");
            }
            RequireUniqueTexts(contract.runtimeContextKeys, "$.designContract.runtimeContextKeys", issues);
            RequireUniqueTexts(contract.defaultSuppressionContexts,
                "$.designContract.defaultSuppressionContexts", issues);
        }

        private static void ValidateEnumDeclarations(
            GuideEnumData declarations,
            ICollection<GuideValidationIssue> issues)
        {
            if (declarations == null)
            {
                Add(issues, "ENUMS_MISSING", "$.enums", "Enum 선언이 필요합니다.");
                return;
            }

            ValidateEnumValues<GuideModeScope>(declarations.modeScope, "$.enums.modeScope", issues);
            ValidateEnumValues<GuidePriority>(declarations.priority, "$.enums.priority", issues);
            ValidateEnumValues<GuidePresentationType>(
                declarations.presentationType, "$.enums.presentationType", issues);
            ValidateEnumValues<GuideExpression>(declarations.expressionKey, "$.enums.expressionKey", issues);
            ValidateEnumValues<GuideTone>(declarations.tone, "$.enums.tone", issues);
            ValidateEnumValues<GuideCooldownScope>(declarations.cooldownScope, "$.enums.cooldownScope", issues);
        }

        private static void ValidateCharacter(
            GuideCharacterData character,
            ICollection<GuideValidationIssue> issues)
        {
            if (character == null)
            {
                Add(issues, "CHARACTER_MISSING", "$.character", "Character 정의가 필요합니다.");
                return;
            }
            RequireText(character.characterId, "$.character.characterId", issues);
            RequireText(character.displayNameKey, "$.character.displayNameKey", issues);
            if (!TryParse(character.defaultExpression, out GuideExpression _))
            {
                Add(issues, "DEFAULT_EXPRESSION", "$.character.defaultExpression",
                    "등록된 expressionKey가 아닙니다.");
            }
            if (character.expressions == null)
            {
                Add(issues, "EXPRESSIONS_MISSING", "$.character.expressions", "표정 자산 매핑이 필요합니다.");
                return;
            }
            foreach (GuideExpression expression in Enum.GetValues(typeof(GuideExpression)))
            {
                string path = $"$.character.expressions.{expression}";
                RequireText(character.expressions.GetAssetKey(expression.ToString()), path, issues);
            }
        }

        private static Dictionary<string, GuideFactTypeIndexData> ValidateFactIndex(
            GuideFactTypeIndexData[] entries,
            ICollection<GuideValidationIssue> issues)
        {
            var result = new Dictionary<string, GuideFactTypeIndexData>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Length; index++)
            {
                GuideFactTypeIndexData entry = entries[index];
                string path = $"$.factTypeIndex[{index}]";
                if (entry == null || string.IsNullOrWhiteSpace(entry.factType))
                {
                    Add(issues, "FACT_TYPE_EMPTY", path, "factType이 필요합니다.");
                    continue;
                }
                if (result.ContainsKey(entry.factType))
                {
                    Add(issues, "FACT_TYPE_DUPLICATE", path, $"'{entry.factType}'이 중복됐습니다.");
                    continue;
                }
                result.Add(entry.factType, entry);
                RequireUniqueTexts(entry.requiredPayload, path + ".requiredPayload", issues);
                RequireUniqueTexts(entry.cueIds, path + ".cueIds", issues);
                ValidateModes(entry.modeScopes, path + ".modeScopes", issues);
            }
            return result;
        }

        private static void ValidateCues(
            GuideCueData[] cues,
            IReadOnlyDictionary<string, GuideFactTypeIndexData> factsByType,
            GuideDatasetData data,
            ICollection<GuideValidationIssue> issues)
        {
            var cueIds = new HashSet<string>(StringComparer.Ordinal);
            var variationIds = new HashSet<string>(StringComparer.Ordinal);
            var runtimeKeys = new HashSet<string>(
                data.designContract?.runtimeContextKeys ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var suppressionContexts = new HashSet<string>(
                data.designContract?.defaultSuppressionContexts ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            for (int index = 0; index < cues.Length; index++)
            {
                GuideCueData cue = cues[index];
                string path = $"$.cueDefinitions[{index}]";
                if (cue == null)
                {
                    Add(issues, "CUE_NULL", path, "null Cue는 허용되지 않습니다.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(cue.cueId) || !cueIds.Add(cue.cueId))
                    Add(issues, "CUE_ID", path + ".cueId", "비어 있거나 중복된 CueId입니다.");
                if (!TryParse(cue.modeScope, out GuideModeScope mode))
                    Add(issues, "MODE_SCOPE", path + ".modeScope", "등록되지 않은 modeScope입니다.");
                if (!TryParse(cue.priority, out GuidePriority _))
                    Add(issues, "PRIORITY", path + ".priority", "등록되지 않은 priority입니다.");
                if (!TryParse(cue.presentationType, out GuidePresentationType _))
                    Add(issues, "PRESENTATION_TYPE", path + ".presentationType",
                        "등록되지 않은 presentationType입니다.");
                if (!TryParse(cue.expressionKey, out GuideExpression _))
                    Add(issues, "EXPRESSION_KEY", path + ".expressionKey", "등록되지 않은 expressionKey입니다.");
                else if (string.IsNullOrWhiteSpace(data.character?.expressions?.GetAssetKey(cue.expressionKey)))
                    Add(issues, "EXPRESSION_ASSET", path + ".expressionKey", "표정 자산 키 매핑이 없습니다.");

                string[] requiredPayload = cue.requiredPayload ?? Array.Empty<string>();
                RequireUniqueTexts(requiredPayload, path + ".requiredPayload", issues);
                var payloadSet = new HashSet<string>(requiredPayload, StringComparer.Ordinal);
                ValidateFactReference(cue, mode, factsByType, payloadSet, path, issues);
                ValidateRepeatPolicy(cue.repeatPolicy, payloadSet, runtimeKeys, path, issues);
                ValidateCta(cue.cta, path, issues);
                ValidateSuppression(cue.suppressDuring, suppressionContexts, path, issues);
                ValidateVariations(cue, payloadSet, variationIds, path, issues);

                if (cue.requiresAcknowledgement && cue.autoDismissSec > 0f)
                    Add(issues, "ACK_AUTO_DISMISS", path, "확인 필수 Cue는 자동으로 닫을 수 없습니다.");
                if (cue.autoDismissSec < 0f)
                    Add(issues, "AUTO_DISMISS", path + ".autoDismissSec", "자동 닫기 시간은 음수일 수 없습니다.");
            }
        }

        private static void ValidateFactReference(
            GuideCueData cue,
            GuideModeScope mode,
            IReadOnlyDictionary<string, GuideFactTypeIndexData> factsByType,
            HashSet<string> payloadSet,
            string path,
            ICollection<GuideValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(cue.factType) || !factsByType.TryGetValue(cue.factType, out GuideFactTypeIndexData fact))
            {
                Add(issues, "FACT_TYPE_UNKNOWN", path + ".factType", $"'{cue.factType}' Fact 계약이 없습니다.");
                return;
            }

            if (!SetEquals(payloadSet, fact.requiredPayload))
                Add(issues, "FACT_PAYLOAD_MISMATCH", path + ".requiredPayload", "factTypeIndex와 payload가 다릅니다.");
            if (!Contains(fact.cueIds, cue.cueId))
                Add(issues, "FACT_CUE_INDEX", path + ".cueId", "factTypeIndex의 cueIds에 등록되지 않았습니다.");
            if (!Contains(fact.modeScopes, mode.ToString()))
                Add(issues, "FACT_MODE_INDEX", path + ".modeScope", "factTypeIndex의 modeScopes와 다릅니다.");
        }

        private static void ValidateRepeatPolicy(
            GuideRepeatPolicyData repeat,
            HashSet<string> payloadKeys,
            HashSet<string> runtimeKeys,
            string cuePath,
            ICollection<GuideValidationIssue> issues)
        {
            string path = cuePath + ".repeatPolicy";
            if (repeat == null)
            {
                Add(issues, "REPEAT_POLICY", path, "repeatPolicy가 필요합니다.");
                return;
            }
            if (!TryParse(repeat.cooldownScope, out GuideCooldownScope _))
                Add(issues, "COOLDOWN_SCOPE", path + ".cooldownScope", "등록되지 않은 cooldownScope입니다.");
            if (repeat.maxDisplaysPerScope < 1)
                Add(issues, "DISPLAY_LIMIT", path + ".maxDisplaysPerScope", "표시 횟수는 1 이상이어야 합니다.");
            RequireText(repeat.dedupeKeyTemplate, path + ".dedupeKeyTemplate", issues);

            string[] tokens = GuideTemplate.ExtractTokens(repeat.dedupeKeyTemplate);
            for (int index = 0; index < tokens.Length; index++)
            {
                if (!payloadKeys.Contains(tokens[index]) && !runtimeKeys.Contains(tokens[index]))
                {
                    Add(issues, "DEDUPE_PLACEHOLDER", path + ".dedupeKeyTemplate",
                        $"'{tokens[index]}'은 payload 또는 runtimeContextKeys에 없습니다.");
                }
            }
        }

        private static void ValidateCta(
            GuideCtaData cta,
            string cuePath,
            ICollection<GuideValidationIssue> issues)
        {
            if (cta == null || cta.IsEmpty)
                return;

            if (string.IsNullOrWhiteSpace(cta.action))
                Add(issues, "CTA_ACTION_REQUIRED", cuePath + ".cta.action", "CTA action이 필요합니다.");
            else if (!TryParse(cta.action, out GuideCtaAction _))
                Add(issues, "CTA_UNKNOWN", cuePath + ".cta.action", $"'{cta.action}' CTA Adapter가 없습니다.");
            RequireText(cta.label, cuePath + ".cta.label", issues);
        }

        private static void ValidateSuppression(
            string[] cueContexts,
            HashSet<string> declaredContexts,
            string cuePath,
            ICollection<GuideValidationIssue> issues)
        {
            cueContexts ??= Array.Empty<string>();
            for (int index = 0; index < cueContexts.Length; index++)
            {
                if (!declaredContexts.Contains(cueContexts[index]))
                    Add(issues, "SUPPRESSION_CONTEXT", $"{cuePath}.suppressDuring[{index}]",
                        $"'{cueContexts[index]}'이 전역 억제 문맥에 선언되지 않았습니다.");
            }
        }

        private static void ValidateVariations(
            GuideCueData cue,
            HashSet<string> payloadKeys,
            HashSet<string> allVariationIds,
            string cuePath,
            ICollection<GuideValidationIssue> issues)
        {
            GuideVariationData[] variations = cue.variations ?? Array.Empty<GuideVariationData>();
            if (variations.Length != ExpectedVariationCountPerCue)
            {
                Add(issues, "VARIATION_COUNT", cuePath + ".variations",
                    $"Cue마다 Variation {ExpectedVariationCountPerCue}개가 필요합니다.");
            }

            var usedPayload = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < variations.Length; index++)
            {
                GuideVariationData variation = variations[index];
                string path = $"{cuePath}.variations[{index}]";
                if (variation == null)
                {
                    Add(issues, "VARIATION_NULL", path, "null Variation은 허용되지 않습니다.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(variation.variationId) || !allVariationIds.Add(variation.variationId))
                    Add(issues, "VARIATION_ID", path + ".variationId", "비어 있거나 중복된 VariationId입니다.");
                if (variation.weight <= 0)
                    Add(issues, "VARIATION_WEIGHT", path + ".weight", "가중치는 1 이상이어야 합니다.");
                if (!TryParse(variation.tone, out GuideTone _))
                    Add(issues, "VARIATION_TONE", path + ".tone", "등록되지 않은 tone입니다.");
                RequireText(variation.text, path + ".text", issues);

                string[] tokens = GuideTemplate.ExtractTokens(variation.text);
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    string token = tokens[tokenIndex];
                    if (!payloadKeys.Contains(token))
                        Add(issues, "TEXT_PLACEHOLDER", path + ".text", $"'{token}' payload가 선언되지 않았습니다.");
                    else
                        usedPayload.Add(token);
                }
            }

            foreach (string required in payloadKeys)
            {
                if (!usedPayload.Contains(required))
                    Add(issues, "UNUSED_PAYLOAD", cuePath + ".requiredPayload", $"'{required}'가 문장에 사용되지 않습니다.");
            }
        }

        private static void ValidateReverseIndex(
            GuideCueData[] cues,
            GuideFactTypeIndexData[] facts,
            ICollection<GuideValidationIssue> issues)
        {
            var cueFacts = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < cues.Length; index++)
            {
                if (cues[index] != null && !string.IsNullOrWhiteSpace(cues[index].cueId))
                    cueFacts[cues[index].cueId] = cues[index].factType;
            }

            for (int factIndex = 0; factIndex < facts.Length; factIndex++)
            {
                GuideFactTypeIndexData fact = facts[factIndex];
                if (fact == null)
                    continue;
                string[] cueIds = fact.cueIds ?? Array.Empty<string>();
                for (int cueIndex = 0; cueIndex < cueIds.Length; cueIndex++)
                {
                    if (!cueFacts.TryGetValue(cueIds[cueIndex], out string factType))
                        Add(issues, "INDEX_CUE_UNKNOWN", $"$.factTypeIndex[{factIndex}].cueIds[{cueIndex}]",
                            $"'{cueIds[cueIndex]}' Cue가 없습니다.");
                    else if (!string.Equals(factType, fact.factType, StringComparison.Ordinal))
                        Add(issues, "INDEX_FACT_MISMATCH", $"$.factTypeIndex[{factIndex}].cueIds[{cueIndex}]",
                            "Cue의 factType과 역방향 인덱스가 다릅니다.");
                }
            }
        }

        private static void ValidateTemplateTokens(
            string template,
            string[] expected,
            string path,
            ICollection<GuideValidationIssue> issues,
            bool requireExactSet)
        {
            RequireText(template, path, issues);
            if (string.IsNullOrWhiteSpace(template))
                return;
            string[] tokens = GuideTemplate.ExtractTokens(template);
            if (requireExactSet && !SetEquals(new HashSet<string>(tokens, StringComparer.Ordinal), expected))
                Add(issues, "SEED_TEMPLATE", path, $"필수 token은 {string.Join(", ", expected)}입니다.");
        }

        private static void ValidateModes(
            string[] values,
            string path,
            ICollection<GuideValidationIssue> issues)
        {
            RequireUniqueTexts(values, path, issues);
            values ??= Array.Empty<string>();
            for (int index = 0; index < values.Length; index++)
                if (!TryParse(values[index], out GuideModeScope _))
                    Add(issues, "FACT_MODE", $"{path}[{index}]", "등록되지 않은 modeScope입니다.");
        }

        private static void ValidateEnumValues<T>(
            string[] values,
            string path,
            ICollection<GuideValidationIssue> issues) where T : struct
        {
            RequireUniqueTexts(values, path, issues);
            var actual = new HashSet<string>(values ?? Array.Empty<string>(), StringComparer.Ordinal);
            string[] expected = Enum.GetNames(typeof(T));
            if (!SetEquals(actual, expected))
                Add(issues, "ENUM_CONTRACT", path, "코드 Enum과 Dataset Enum 선언이 다릅니다.");
        }

        private static void RequireUniqueTexts(
            string[] values,
            string path,
            ICollection<GuideValidationIssue> issues)
        {
            if (values == null)
            {
                Add(issues, "ARRAY_MISSING", path, "배열이 필요합니다.");
                return;
            }
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(values[index]) || !unique.Add(values[index]))
                    Add(issues, "ARRAY_VALUE", $"{path}[{index}]", "비어 있거나 중복된 값입니다.");
            }
        }

        private static void RequireText(
            string value,
            string path,
            ICollection<GuideValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(value))
                Add(issues, "TEXT_REQUIRED", path, "비어 있지 않은 문자열이 필요합니다.");
        }

        private static bool TryParse<T>(string value, out T parsed) where T : struct =>
            Enum.TryParse(value, false, out parsed);

        private static bool Contains(string[] values, string expected)
        {
            values ??= Array.Empty<string>();
            for (int index = 0; index < values.Length; index++)
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool SetEquals(HashSet<string> left, string[] right) =>
            left.SetEquals(right ?? Array.Empty<string>());

        private static bool SequenceEquals(string[] left, string[] right)
        {
            left ??= Array.Empty<string>();
            right ??= Array.Empty<string>();
            if (left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                    return false;
            return true;
        }

        private static void Add(
            ICollection<GuideValidationIssue> issues,
            string code,
            string path,
            string message) => issues.Add(new GuideValidationIssue(code, path, message));
    }

    /// <summary>검증을 통과한 저작 DTO만 런타임 Catalog로 변환한다.</summary>
    public static class GuideDatasetFactory
    {
        public static bool TryCreate(
            GuideDatasetData data,
            out GuideDatasetCatalog catalog,
            out GuideValidationIssue[] issues)
        {
            issues = GuideDatasetValidator.Validate(data);
            if (issues.Length > 0)
            {
                catalog = null;
                return false;
            }

            GuideFactContract[] facts = CreateFacts(data.factTypeIndex);
            GuideCueDefinition[] cues = CreateCues(data.cueDefinitions, data.character.expressions);
            catalog = new GuideDatasetCatalog(
                data.datasetId,
                data.version,
                data.character.characterId,
                data.designContract.deterministicVariationSelection.seedTemplate,
                data.designContract.deterministicVariationSelection.fallbackSeedTemplate,
                data.designContract.homeFullDialogueMaxPerEntry,
                Copy(data.designContract.defaultSuppressionContexts),
                facts,
                cues);
            return true;
        }

        private static GuideFactContract[] CreateFacts(GuideFactTypeIndexData[] source)
        {
            var result = new GuideFactContract[source.Length];
            for (int index = 0; index < result.Length; index++)
            {
                GuideFactTypeIndexData item = source[index];
                var modes = new GuideModeScope[item.modeScopes.Length];
                for (int modeIndex = 0; modeIndex < modes.Length; modeIndex++)
                    Enum.TryParse(item.modeScopes[modeIndex], false, out modes[modeIndex]);
                result[index] = new GuideFactContract(item.factType, Copy(item.requiredPayload), modes);
            }
            return result;
        }

        private static GuideCueDefinition[] CreateCues(
            GuideCueData[] source,
            GuideExpressionAssetData expressions)
        {
            var result = new GuideCueDefinition[source.Length];
            for (int index = 0; index < result.Length; index++)
            {
                GuideCueData item = source[index];
                Enum.TryParse(item.modeScope, false, out GuideModeScope mode);
                Enum.TryParse(item.priority, false, out GuidePriority priority);
                Enum.TryParse(item.presentationType, false, out GuidePresentationType presentation);
                Enum.TryParse(item.expressionKey, false, out GuideExpression expression);
                Enum.TryParse(item.repeatPolicy.cooldownScope, false, out GuideCooldownScope cooldown);

                GuideCta? cta = null;
                if (item.cta != null && !item.cta.IsEmpty)
                {
                    Enum.TryParse(item.cta.action, false, out GuideCtaAction action);
                    cta = new GuideCta(action, item.cta.label);
                }

                var variations = new GuideVariationDefinition[item.variations.Length];
                for (int variationIndex = 0; variationIndex < variations.Length; variationIndex++)
                {
                    GuideVariationData variation = item.variations[variationIndex];
                    Enum.TryParse(variation.tone, false, out GuideTone tone);
                    variations[variationIndex] = new GuideVariationDefinition(
                        variation.variationId,
                        variation.weight,
                        tone,
                        variation.text);
                }

                result[index] = new GuideCueDefinition(
                    item.cueId,
                    item.factType,
                    mode,
                    priority,
                    presentation,
                    expression,
                    expressions.GetAssetKey(item.expressionKey),
                    Copy(item.requiredPayload),
                    new GuideRepeatPolicy(item.repeatPolicy.dedupeKeyTemplate, cooldown,
                        item.repeatPolicy.maxDisplaysPerScope),
                    cta,
                    item.requiresAcknowledgement,
                    item.autoDismissSec,
                    Copy(item.suppressDuring),
                    variations);
            }
            return result;
        }

        private static string[] Copy(string[] source) =>
            source == null ? Array.Empty<string>() : (string[])source.Clone();
    }
}
