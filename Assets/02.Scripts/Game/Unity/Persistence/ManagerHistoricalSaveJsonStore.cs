using System;
using System.IO;
using System.Text;
using Baseball.Game.Historical;
using UnityEngine;

namespace Baseball.Game.Unity.Persistence
{
    /// <summary>주입된 파일 경로에 구단주 모드 DTO를 UTF-8 JSON으로 저장하고 복원한다.</summary>
    public sealed class ManagerHistoricalSaveJsonStore
    {
        private readonly string _filePath;
        private string _slotOnePath;

        public ManagerHistoricalSaveJsonStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("세이브 파일 경로는 비어 있을 수 없습니다.", nameof(filePath));
            _filePath = Path.GetFullPath(filePath);
            _slotOnePath = _filePath;
        }

        /// <summary>주입된 슬롯 1 경로를 유지하면서 독립 슬롯을 연다.</summary>
        public ManagerHistoricalSaveJsonStore ForSlot(int slot) =>
            new ManagerHistoricalSaveJsonStore(SaveSlotPaths.GetFilePath(_slotOnePath, slot))
                { _slotOnePath = _slotOnePath };

        public long SavedAtUtcTicks => Exists ? File.GetLastWriteTimeUtc(_filePath).Ticks : 0;

        public string FilePath => _filePath;
        public bool Exists => File.Exists(_filePath);

        public void Save(ManagerHistoricalSaveData saveData)
        {
            if (saveData == null)
                throw new ArgumentNullException(nameof(saveData));

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporaryPath = _filePath + ".temporary";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(Serialize(saveData));
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (Exists)
                    File.Replace(temporaryPath, _filePath, null);
                else
                    File.Move(temporaryPath, _filePath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public ManagerHistoricalSaveData Load()
        {
            if (!File.Exists(_filePath))
                throw new FileNotFoundException("구단주 모드 세이브 파일을 찾을 수 없습니다.", _filePath);
            return Deserialize(File.ReadAllText(_filePath, Encoding.UTF8));
        }

        /// <summary>사용자 확인이 끝난 구단주 모드 선택한 저장 슬롯을 삭제한다.</summary>
        public void Delete()
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
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
                throw new InvalidDataException("구단주 모드 세이브 JSON이 비어 있습니다.");

            ManagerHistoricalSaveData saveData;
            try
            {
                saveData = JsonUtility.FromJson<ManagerHistoricalSaveData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("구단주 모드 세이브 JSON 형식이 잘못되었습니다.", exception);
            }
            return saveData ?? throw new InvalidDataException("구단주 모드 세이브 JSON을 복원하지 못했습니다.");
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
