using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Baseball.Game.Career.Persistence;
using UnityEngine;

namespace Baseball.Game.Unity.Persistence
{
    /// <summary>선수 커리어 DTO를 무결성 Hash와 자동 백업을 포함한 UTF-8 JSON으로 저장한다.</summary>
    public sealed class CareerSaveJsonStore
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);
        private readonly string _primaryPath;
        private readonly string _backupPath;
        private readonly string _temporaryPath;
        private string _slotOnePath;

        public CareerSaveJsonStore(string primaryPath)
        {
            if (string.IsNullOrWhiteSpace(primaryPath))
                throw new ArgumentException("세이브 파일 경로는 비어 있을 수 없습니다.", nameof(primaryPath));
            _primaryPath = Path.GetFullPath(primaryPath);
            _slotOnePath = _primaryPath;
            string directory = Path.GetDirectoryName(_primaryPath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(_primaryPath);
            string extension = Path.GetExtension(_primaryPath);
            _backupPath = Path.Combine(directory, name + ".backup" + extension);
            _temporaryPath = Path.Combine(directory, name + ".temporary" + extension);
        }

        /// <summary>동일 모드의 독립 저장 슬롯을 연다.</summary>
        public CareerSaveJsonStore ForSlot(int slot) =>
            new CareerSaveJsonStore(SaveSlotPaths.GetFilePath(_slotOnePath, slot)) { _slotOnePath = _slotOnePath };

        public bool Exists => File.Exists(_primaryPath);
        public string FilePath => _primaryPath;
        public bool BackupExists => File.Exists(_backupPath);

        public void SaveAtomic(CareerSaveData saveData)
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));
            string directory = Path.GetDirectoryName(_primaryPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            WriteDurable(_temporaryPath, SerializeWithIntegrity(saveData));
            try
            {
                if (File.Exists(_primaryPath))
                    ReplaceFile(_temporaryPath, _primaryPath, _backupPath);
                else
                    File.Move(_temporaryPath, _primaryPath);
            }
            finally
            {
                if (File.Exists(_temporaryPath))
                    File.Delete(_temporaryPath);
            }
        }

        public CareerSaveData LoadPrimary() => Load(_primaryPath, "선수 커리어 세이브");

        public CareerSaveData LoadBackup() => Load(_backupPath, "선수 커리어 백업");

        public void PromoteBackupToPrimaryAtomic()
        {
            if (!File.Exists(_backupPath))
                throw new FileNotFoundException("복구할 선수 커리어 백업이 없습니다.", _backupPath);
            File.Copy(_backupPath, _temporaryPath, overwrite: true);
            try
            {
                if (File.Exists(_primaryPath))
                    ReplaceFile(_temporaryPath, _primaryPath, destinationBackupPath: null);
                else
                    File.Move(_temporaryPath, _primaryPath);
            }
            finally
            {
                if (File.Exists(_temporaryPath))
                    File.Delete(_temporaryPath);
            }
        }

        public void DeleteAll()
        {
            DeleteIfExists(_primaryPath);
            DeleteIfExists(_backupPath);
            DeleteIfExists(_temporaryPath);
        }

        private static CareerSaveData Load(string path, string label)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(label + " 파일을 찾을 수 없습니다.", path);
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException(label + " 파일이 비어 있습니다.");

            CareerSaveData saveData;
            try
            {
                saveData = JsonUtility.FromJson<CareerSaveData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(label + " JSON 형식이 잘못되었습니다.", exception);
            }
            if (saveData == null)
                throw new InvalidDataException(label + "를 복원하지 못했습니다.");
            VerifyIntegrity(saveData);
            return saveData;
        }

        private static string SerializeWithIntegrity(CareerSaveData saveData)
        {
            saveData.payloadSha256 = string.Empty;
            string payload = JsonUtility.ToJson(saveData);
            saveData.payloadSha256 = CalculateSha256(payload);
            return JsonUtility.ToJson(saveData);
        }

        private static void VerifyIntegrity(CareerSaveData saveData)
        {
            string expected = saveData.payloadSha256;
            if (string.IsNullOrWhiteSpace(expected))
                throw new InvalidDataException("세이브 파일에 무결성 Hash가 없습니다.");
            saveData.payloadSha256 = string.Empty;
            string actual = CalculateSha256(JsonUtility.ToJson(saveData));
            saveData.payloadSha256 = expected;
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("세이브 파일의 무결성 Hash가 일치하지 않습니다.");
        }

        private static string CalculateSha256(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Utf8WithoutBom.GetBytes(value));
            var result = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
                result.Append(hash[index].ToString("x2"));
            return result.ToString();
        }

        private static void WriteDurable(string path, string content)
        {
            byte[] bytes = Utf8WithoutBom.GetBytes(content);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        private static void ReplaceFile(string sourcePath, string destinationPath, string destinationBackupPath)
        {
            try
            {
                File.Replace(sourcePath, destinationPath, destinationBackupPath, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                if (!string.IsNullOrEmpty(destinationBackupPath))
                    File.Copy(destinationPath, destinationBackupPath, overwrite: true);
                File.Delete(destinationPath);
                File.Move(sourcePath, destinationPath);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>Unity persistentDataPath 아래의 고정 선수 커리어 슬롯 경로를 제공한다.</summary>
    public static class CareerSavePath
    {
        public static string GetDefaultFilePath() =>
            Path.Combine(Application.persistentDataPath, "Saves", "player_career_01.json");
    }
}
