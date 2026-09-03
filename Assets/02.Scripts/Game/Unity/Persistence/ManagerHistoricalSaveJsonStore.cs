using System;
using System.IO;
using System.Text;
using Baseball.Game.Historical;
using UnityEngine;

namespace Baseball.Game.Unity.Persistence
{
    /// <summary>주입된 파일 경로에 감독모드 DTO를 UTF-8 JSON으로 저장하고 복원한다.</summary>
    public sealed class ManagerHistoricalSaveJsonStore
    {
        private readonly string _filePath;

        public ManagerHistoricalSaveJsonStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("세이브 파일 경로는 비어 있을 수 없습니다.", nameof(filePath));
            _filePath = Path.GetFullPath(filePath);
        }

        public string FilePath => _filePath;
        public bool Exists => File.Exists(_filePath);

        public void Save(ManagerHistoricalSaveData saveData)
        {
            if (saveData == null)
                throw new ArgumentNullException(nameof(saveData));

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(_filePath, Serialize(saveData), new UTF8Encoding(false));
        }

        public ManagerHistoricalSaveData Load()
        {
            if (!File.Exists(_filePath))
                throw new FileNotFoundException("감독모드 세이브 파일을 찾을 수 없습니다.", _filePath);
            return Deserialize(File.ReadAllText(_filePath, Encoding.UTF8));
        }

        public static string Serialize(ManagerHistoricalSaveData saveData)
        {
            if (saveData == null)
                throw new ArgumentNullException(nameof(saveData));
            return JsonUtility.ToJson(saveData);
        }

        public static ManagerHistoricalSaveData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("감독모드 세이브 JSON이 비어 있습니다.");

            ManagerHistoricalSaveData saveData;
            try
            {
                saveData = JsonUtility.FromJson<ManagerHistoricalSaveData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("감독모드 세이브 JSON 형식이 잘못되었습니다.", exception);
            }
            return saveData ?? throw new InvalidDataException("감독모드 세이브 JSON을 복원하지 못했습니다.");
        }
    }

    /// <summary>Unity persistentDataPath를 기본 세이브 경로로 바꾸는 얇은 경계다.</summary>
    public static class ManagerHistoricalSavePath
    {
        public static string GetDefaultFilePath()
        {
            return Path.Combine(Application.persistentDataPath, "Saves", "manager_historical.json");
        }
    }
}
