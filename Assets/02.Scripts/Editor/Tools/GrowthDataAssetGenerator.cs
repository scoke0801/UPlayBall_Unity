using System.IO;
using Baseball.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Baseball.Editor.Tools
{
    /// <summary>성장 저작 에셋의 표준 경로와 참조 그래프를 한 번에 복원한다.</summary>
    public static class GrowthDataAssetGenerator
    {
        private const string DirectoryPath = "Assets/10.Datas/Resources/NewGame/Growth";
        private const string NewGamePath = "Assets/10.Datas/Resources/NewGame/NewGameDefinition.asset";

        [BaseballEditorTool(
            "프로젝트 기반",
            "성장 저작 데이터 생성",
            "훈련·티어·승격 보상·블록·뽑기·Trait 에셋을 만들고 NewGameDefinition에 연결합니다.",
            order: 21,
            impact: ToolImpact.BulkWrite)]
        public static void EnsureAssets()
        {
            Directory.CreateDirectory(DirectoryPath);
            TrainingProgramCatalogAsset programs = Ensure<TrainingProgramCatalogAsset>("TrainingProgramCatalog");
            TrainingAccessTierAsset access = Ensure<TrainingAccessTierAsset>("TrainingAccessTier");
            LeagueGrowthMilestoneAsset milestones = Ensure<LeagueGrowthMilestoneAsset>("LeagueGrowthMilestone");
            SkillBlockCatalogAsset blocks = Ensure<SkillBlockCatalogAsset>("SkillBlockCatalog");
            SkillGachaOfferCatalogAsset gacha = Ensure<SkillGachaOfferCatalogAsset>("SkillGachaOfferCatalog");
            TraitDefinitionCatalogAsset traits = Ensure<TraitDefinitionCatalogAsset>("TraitDefinitionCatalog");
            GrowthBalanceAsset growth = Ensure<GrowthBalanceAsset>("GrowthBalance");

            var growthSerialized = new SerializedObject(growth);
            growthSerialized.FindProperty("_trainingPrograms").objectReferenceValue = programs;
            growthSerialized.FindProperty("_trainingAccess").objectReferenceValue = access;
            growthSerialized.FindProperty("_leagueMilestones").objectReferenceValue = milestones;
            growthSerialized.FindProperty("_skillBlocks").objectReferenceValue = blocks;
            growthSerialized.FindProperty("_skillGachaOffers").objectReferenceValue = gacha;
            growthSerialized.FindProperty("_traits").objectReferenceValue = traits;
            growthSerialized.ApplyModifiedPropertiesWithoutUndo();

            NewGameDefinition newGame = AssetDatabase.LoadAssetAtPath<NewGameDefinition>(NewGamePath);
            if (newGame != null)
            {
                var newGameSerialized = new SerializedObject(newGame);
                newGameSerialized.FindProperty("_growthBalance").objectReferenceValue = growth;
                newGameSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(growth);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GrowthDataAssetGenerator] 성장 저작 에셋 연결 완료: {DirectoryPath}");
        }

        private static T Ensure<T>(string fileName) where T : ScriptableObject
        {
            string path = $"{DirectoryPath}/{fileName}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
