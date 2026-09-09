using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Baseball.Presentation.Match.Sprites;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Baseball.Editor.SpriteSheets
{
    /// <summary>처리 메타데이터를 검증하고 고정 경로의 Sprite·Atlas·카탈로그를 갱신한다.</summary>
    public static class SpriteSheetBatchImporter
    {
        public const string ProductionPath = "Assets/Resources/UI/SpriteMatch/AnimationCatalog.asset";
        public const string ReviewPath = "Assets/10.Datas/SpriteMatch/ReviewCatalog.asset";
        public const string LayoutPath = "Assets/10.Datas/SpriteMatch/FieldLayout.asset";
        private const string ImageRoot = "Assets/04.Images/SpriteMatch";
        private const string AtlasPath = "Assets/10.Datas/SpriteMatch/SpriteMatch.spriteatlas";
        private const string ReceiptPath = "Assets/10.Datas/SpriteMatch/ImportReceipt.asset";

        /// <summary>처리 폴더를 가져오며 미검수 모션은 검수 카탈로그에만 남긴다.</summary>
        public static SpriteAnimationCatalog Import(string processedRoot)
        {
            string root = Path.GetFullPath(processedRoot);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            var manifests = new List<(string path, ProcessedClip clip)>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in GetMetadataPaths(root))
            {
                ProcessedClip clip = JsonUtility.FromJson<ProcessedClip>(File.ReadAllText(path));
                if (clip == null || string.IsNullOrEmpty(clip.sheetId) || clip.frames == null) continue;
                Validate(clip);
                if (!ids.Add(clip.sheetId)) throw new InvalidDataException("중복 clip ID: " + clip.sheetId);
                foreach (ProcessedFrame frame in clip.frames)
                    if (!File.Exists(ResolveContainedPath(Path.GetDirectoryName(path), frame.file)))
                        throw new FileNotFoundException("처리 프레임 누락: " + frame.file);
                manifests.Add((path, clip));
            }
            if (manifests.Count == 0) throw new InvalidDataException("처리된 clip 메타데이터가 없습니다.");
            float pixelsPerUnit = manifests[0].clip.importSettings.pixelsPerUnit;
            if (manifests.Any(item => item.clip.importSettings.pixelsPerUnit != pixelsPerUnit)) throw new InvalidDataException("모든 모션은 동일한 PixelsPerUnit을 사용해야 합니다.");
            EnsureFolder(ImageRoot);
            EnsureFolder(Path.GetDirectoryName(ReviewPath).Replace('\\', '/'));
            SpriteAnimationCatalog review = LoadOrCreate<SpriteAnimationCatalog>(ReviewPath);
            SpriteSheetImportReceipt receipt = LoadOrCreate<SpriteSheetImportReceipt>(ReceiptPath);
            FieldLayoutDefinition layout = LoadOrCreate<FieldLayoutDefinition>(LayoutPath);
            ImportBackground(layout);
            string fingerprint = ComputeFingerprint(manifests);
            if (receipt.fingerprint == fingerprint && AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath) != null && review.clips != null && review.clips.Length == manifests.Count &&
                review.clips.All(clip => clip != null && clip.frames != null && clip.frames.Length > 0 && clip.frames.All(frame => frame != null && frame.sprite != null)))
            {
                Publish(review); AssetDatabase.SaveAssets(); return review;
            }
            var clips = new List<SpriteClipDefinition>();
            var textures = new List<UnityEngine.Object>();
            foreach (var item in manifests)
            {
                ProcessedClip source = item.clip;
                string folder = ImageRoot + "/" + source.sheetId;
                EnsureFolder(folder);
                var frames = new SpriteFrameDefinition[source.frames.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    ProcessedFrame frame = source.frames[i];
                    string target = folder + $"/frame_{i:D3}.png";
                    CopyChanged(ResolveContainedPath(Path.GetDirectoryName(item.path), frame.file), target);
                    ConfigureTexture(target, source.importSettings, frame.pivot);
                    frames[i] = new SpriteFrameDefinition
                    {
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target),
                        durationMs = frame.durationMs,
                        events = (source.events ?? Array.Empty<ProcessedEvent>()).Where(marker => marker.frameIndex == i)
                            .Select(marker => (SpriteAnimationEvent)Enum.Parse(typeof(SpriteAnimationEvent), marker.name)).ToArray()
                    };
                    textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(target));
                }
                clips.Add(new SpriteClipDefinition
                {
                    clipId = source.sheetId, sourceHash = source.sourceHash,
                    handedness = ParseHandedness(source.handedness), approved = source.reviewStatus == "Approved",
                    loop = source.loop, frames = frames, eventAnchors = BuildEventAnchors(source),
                    referenceHeightPixels = source.frames[0].sourceCellRect.height > 0 ? source.frames[0].sourceCellRect.height : frames[0].sprite.rect.height
                });
            }
            string before = EditorJsonUtility.ToJson(review);
            review.clips = clips.ToArray(); review.fieldLayout = layout;
            if (before != EditorJsonUtility.ToJson(review)) EditorUtility.SetDirty(review);
            UpdateAtlas(textures.ToArray());
            Publish(review);
            receipt.fingerprint = fingerprint; EditorUtility.SetDirty(receipt);
            AssetDatabase.SaveAssets();
            return review;
        }

        /// <summary>미검수·잘못된 모션을 제외하고 승인 모션의 독립 복사본을 경기 경로에 저장한다.</summary>
        public static void Publish(SpriteAnimationCatalog review)
        {
            if (review == null) throw new ArgumentNullException(nameof(review));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var clips = new List<SpriteClipDefinition>();
            foreach (SpriteClipDefinition clip in review.clips ?? Array.Empty<SpriteClipDefinition>())
            {
                if (clip == null || !clip.IsProductionReady) continue;
                if (!ids.Add(clip.clipId)) throw new InvalidDataException("중복 승인 clip ID: " + clip.clipId);
                ValidateEvents(clip);
                clips.Add(CloneClip(clip));
            }
            EnsureFolder(Path.GetDirectoryName(ProductionPath).Replace('\\', '/'));
            SpriteAnimationCatalog production = LoadOrCreate<SpriteAnimationCatalog>(ProductionPath);
            string before = EditorJsonUtility.ToJson(production);
            production.clips = clips.OrderBy(clip => clip.clipId, StringComparer.Ordinal).ToArray();
            production.fieldLayout = review.fieldLayout;
            string after = EditorJsonUtility.ToJson(production);
            if (before != after) EditorUtility.SetDirty(production);
        }

        private static SpriteClipDefinition CloneClip(SpriteClipDefinition clip)
        {
            var frames = new SpriteFrameDefinition[clip.frames.Length];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = new SpriteFrameDefinition
                {
                    sprite = clip.frames[i].sprite, durationMs = clip.frames[i].durationMs,
                    events = (SpriteAnimationEvent[])(clip.frames[i].events ?? Array.Empty<SpriteAnimationEvent>()).Clone()
                };
            var anchors = new List<SpriteEventAnchorDefinition>();
            foreach (SpriteEventAnchorDefinition anchor in clip.eventAnchors ?? Array.Empty<SpriteEventAnchorDefinition>())
                if (anchor != null) anchors.Add(new SpriteEventAnchorDefinition
                {
                    marker = anchor.marker, frameIndex = anchor.frameIndex, sourcePositionNormalized = anchor.sourcePositionNormalized,
                    sourceCellSize = anchor.sourceCellSize, sourceRootNormalized = anchor.sourceRootNormalized
                });
            return new SpriteClipDefinition
            {
                clipId = clip.clipId, sourceHash = clip.sourceHash, approved = clip.approved,
                handedness = clip.handedness, loop = clip.loop, referenceHeightPixels = clip.referenceHeightPixels, frames = frames,
                eventAnchors = anchors.ToArray()
            };
        }

        private static SpriteEventAnchorDefinition[] BuildEventAnchors(ProcessedClip clip)
        {
            var anchors = new List<SpriteEventAnchorDefinition>();
            foreach (ProcessedEvent marker in clip.events ?? Array.Empty<ProcessedEvent>())
            {
                if (!marker.hasSourcePosition) continue;
                ProcessedFrame frame = clip.frames[marker.frameIndex];
                anchors.Add(new SpriteEventAnchorDefinition
                {
                    marker = (SpriteAnimationEvent)Enum.Parse(typeof(SpriteAnimationEvent), marker.name), frameIndex = marker.frameIndex,
                    sourcePositionNormalized = new Vector2(marker.sourcePositionNormalized.x, marker.sourcePositionNormalized.y),
                    sourceCellSize = new Vector2(frame.sourceCellRect.width, frame.sourceCellRect.height),
                    sourceRootNormalized = new Vector2(frame.pivot.x, 1f - frame.pivot.y)
                });
            }
            return anchors.ToArray();
        }

        /// <summary>Unity 에셋 변경 전에 손잡이·순서·사건·임포트 정책을 검증한다.</summary>
        public static void Validate(ProcessedClip clip)
        {
            if (clip == null || string.IsNullOrWhiteSpace(clip.sheetId) || clip.sheetId.Any(c => !(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')) || clip.sheetId.Contains(".."))
                throw new InvalidDataException("안전하지 않은 sheetId입니다.");
            SpriteHandedness handedness = ParseHandedness(clip.handedness);
            if (string.IsNullOrWhiteSpace(clip.sourceHash)) throw new InvalidDataException("원본 Source hash가 없습니다.");
            if (clip.reviewStatus != "Approved" && clip.reviewStatus != "NeedsReview") throw new InvalidDataException("알 수 없는 검수 상태입니다.");
            if (clip.reviewStatus == "Approved" && handedness == SpriteHandedness.NeedsReview) throw new InvalidDataException("NeedsReview 손잡이는 승인할 수 없습니다.");
            if (clip.frames == null || clip.frames.Length == 0) throw new InvalidDataException("빈 프레임입니다.");
            var cells = new HashSet<int>();
            foreach (ProcessedFrame frame in clip.frames)
            {
                if (frame == null || string.IsNullOrEmpty(frame.file) || !cells.Add(frame.cellIndex) || frame.durationMs <= 0 || float.IsNaN(frame.durationMs) || float.IsInfinity(frame.durationMs))
                    throw new InvalidDataException("프레임 경로·순서·시간이 잘못되었습니다.");
                if (frame.pivot == null || float.IsNaN(frame.pivot.x) || float.IsNaN(frame.pivot.y) || frame.pivot.x < 0 || frame.pivot.x > 1 || frame.pivot.y < 0 || frame.pivot.y > 1)
                    throw new InvalidDataException("pivot 범위가 잘못되었습니다.");
            }
            var markers = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProcessedEvent marker in clip.events ?? Array.Empty<ProcessedEvent>())
            {
                if (marker == null) throw new InvalidDataException($"{clip.sheetId}: null 사건이 있습니다.");
                string context = $"{clip.sheetId} / {marker.name ?? "<이름 없음>"} / frameIndex={marker.frameIndex}";
                if (!Enum.TryParse(marker.name, out SpriteAnimationEvent value) || !Enum.IsDefined(typeof(SpriteAnimationEvent), value))
                    throw new InvalidDataException(context + ": 정의되지 않은 사건 이름입니다.");
                if (!markers.Add(marker.name)) throw new InvalidDataException(context + ": 같은 사건이 중복 지정되었습니다.");
                if (marker.frameIndex < 0 || marker.frameIndex >= clip.frames.Length)
                    throw new InvalidDataException(context + $": 프레임 범위 0..{clip.frames.Length - 1} 밖입니다.");
                if (marker.hasSourcePosition)
                {
                    Pivot point = marker.sourcePositionNormalized;
                    CellRect cell = clip.frames[marker.frameIndex].sourceCellRect;
                    if (point == null || float.IsNaN(point.x) || float.IsNaN(point.y) || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1 ||
                        cell == null || cell.width <= 0 || cell.height <= 0 || float.IsNaN(cell.width) || float.IsNaN(cell.height) || float.IsInfinity(cell.width) || float.IsInfinity(cell.height))
                        throw new InvalidDataException(context + ": 사건 접점 좌표·원본 셀 크기가 잘못되었습니다.");
                }
            }
            if (clip.importSettings == null || clip.importSettings.pixelsPerUnit <= 0 || float.IsNaN(clip.importSettings.pixelsPerUnit) || float.IsInfinity(clip.importSettings.pixelsPerUnit)) throw new InvalidDataException("PixelsPerUnit이 잘못되었습니다.");
            if (!Enum.TryParse(clip.importSettings.filterMode, out FilterMode filter) || !Enum.IsDefined(typeof(FilterMode), filter) ||
                !Enum.TryParse(clip.importSettings.compression, out TextureImporterCompression compression) || !Enum.IsDefined(typeof(TextureImporterCompression), compression))
                throw new InvalidDataException("알 수 없는 텍스처 임포트 정책입니다.");
            RequireMetadataOrder(clip, "SwingWindowOpen", "BatContact");
            RequireMetadataOrder(clip, "GloveContact", "Transfer");
            RequireMetadataOrder(clip, "Transfer", "ThrowRelease");
            RequireMetadataOrder(clip, "GloveContact", "ThrowRelease");
        }

        /// <summary>메타데이터가 처리 폴더 밖의 파일을 참조하지 못하게 한다.</summary>
        public static string ResolveContainedPath(string root, string relative)
        {
            string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(basePath, relative ?? ""));
            if (!result.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(relative)) throw new InvalidDataException("처리 폴더 외부 참조를 거부합니다.");
            return result;
        }

        /// <summary>목록 파일이 있으면 거기에 선언한 모션만 읽고 오래된 출력 폴더는 무시한다.</summary>
        public static string[] GetMetadataPaths(string processedRoot)
        {
            string root = Path.GetFullPath(processedRoot);
            string indexRoot = root;
            string indexPath = Path.Combine(indexRoot, "index.json");
            if (!File.Exists(indexPath))
            {
                indexRoot = Directory.GetParent(root)?.FullName;
                indexPath = indexRoot == null ? null : Path.Combine(indexRoot, "index.json");
            }
            if (indexPath == null || !File.Exists(indexPath))
                return Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            ProcessedIndex index = JsonUtility.FromJson<ProcessedIndex>(File.ReadAllText(indexPath));
            if (index?.clips == null) throw new InvalidDataException(indexPath + ": clips 목록이 없습니다.");
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ProcessedIndexEntry entry in index.clips)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.metadataFile)) throw new InvalidDataException(indexPath + ": metadataFile 경로가 없습니다.");
                string path = ResolveContainedPath(indexRoot, entry.metadataFile);
                // 부모 index를 읽더라도 요청된 Processed 루트 밖의 메타데이터는 가져오지 않는다.
                string relative = Path.GetRelativePath(root, path);
                path = ResolveContainedPath(root, relative);
                if (!seen.Add(path)) throw new InvalidDataException(indexPath + ": 중복 metadataFile " + entry.metadataFile);
                if (!File.Exists(path)) throw new FileNotFoundException(indexPath + ": 메타데이터 누락 " + entry.metadataFile, path);
                paths.Add(path);
            }
            return paths.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static SpriteHandedness ParseHandedness(string value) => value switch
        {
            "L" or "Left" => SpriteHandedness.Left, "R" or "Right" => SpriteHandedness.Right,
            "Shared" => SpriteHandedness.Shared, "NeedsReview" => SpriteHandedness.NeedsReview,
            _ => throw new InvalidDataException("알 수 없는 손잡이: " + value)
        };

        private static void RequireMetadataOrder(ProcessedClip clip, string first, string second)
        {
            ProcessedEvent a = clip.events?.FirstOrDefault(marker => marker.name == first);
            ProcessedEvent b = clip.events?.FirstOrDefault(marker => marker.name == second);
            if (a != null && b != null && a.frameIndex > b.frameIndex)
                throw new InvalidDataException($"{clip.sheetId}: 사건 순서 오류 {first}[{a.frameIndex}] → {second}[{b.frameIndex}]");
        }

        private static string ComputeFingerprint(List<(string path, ProcessedClip clip)> manifests)
        {
            using SHA256 hash = SHA256.Create();
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer);
            writer.Write("SpriteSheetImporter.v3.ExplicitEventAnchors");
            foreach (var item in manifests)
            {
                writer.Write(item.clip.sheetId);
                writer.Write(hash.ComputeHash(File.ReadAllBytes(item.path)));
                foreach (ProcessedFrame frame in item.clip.frames)
                    writer.Write(hash.ComputeHash(File.ReadAllBytes(ResolveContainedPath(Path.GetDirectoryName(item.path), frame.file))));
            }
            writer.Flush();
            return BitConverter.ToString(hash.ComputeHash(buffer.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        private static void ValidateEvents(SpriteClipDefinition clip)
        {
            var seen = new HashSet<SpriteAnimationEvent>();
            foreach (SpriteFrameDefinition frame in clip.frames)
                foreach (SpriteAnimationEvent marker in frame.events ?? Array.Empty<SpriteAnimationEvent>())
                    if (!Enum.IsDefined(typeof(SpriteAnimationEvent), marker) || !seen.Add(marker)) throw new InvalidDataException("중복/미정의 사건: " + clip.clipId);
            RequireOrder(clip, SpriteAnimationEvent.SwingWindowOpen, SpriteAnimationEvent.BatContact);
            RequireOrder(clip, SpriteAnimationEvent.GloveContact, SpriteAnimationEvent.Transfer);
            RequireOrder(clip, SpriteAnimationEvent.Transfer, SpriteAnimationEvent.ThrowRelease);
            RequireOrder(clip, SpriteAnimationEvent.GloveContact, SpriteAnimationEvent.ThrowRelease);
        }

        private static void RequireOrder(SpriteClipDefinition clip, SpriteAnimationEvent first, SpriteAnimationEvent second)
        {
            if (clip.TryGetEventTime(first, out float a) && clip.TryGetEventTime(second, out float b) && a > b)
                throw new InvalidDataException("사건 순서 오류: " + clip.clipId);
        }

        private static void ConfigureTexture(string path, ImportSettings policy, Pivot pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            TextureImporterSettings settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            FilterMode filter = (FilterMode)Enum.Parse(typeof(FilterMode), policy.filterMode);
            TextureImporterCompression compression = (TextureImporterCompression)Enum.Parse(typeof(TextureImporterCompression), policy.compression);
            Vector2 targetPivot = new Vector2(pivot.x, pivot.y);
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && importer.alphaIsTransparency &&
                !importer.mipmapEnabled && importer.spritePixelsPerUnit == policy.pixelsPerUnit && importer.filterMode == filter &&
                importer.textureCompression == compression && importer.spritePivot == targetPivot && settings.spriteAlignment == (int)SpriteAlignment.Custom) return;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.spritePixelsPerUnit = policy.pixelsPerUnit;
            importer.filterMode = filter; importer.textureCompression = compression; importer.wrapMode = TextureWrapMode.Clamp;
            importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = targetPivot;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }

        private static void ImportBackground(FieldLayoutDefinition layout)
        {
            const string source = "docs/design/sprite_sheet_ingame/경기장.png";
            if (!File.Exists(source)) return;
            string path = ImageRoot + "/Field.png";
            CopyChanged(source, path); AssetDatabase.ImportAsset(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (layout.background == texture) return;
            layout.background = texture; EditorUtility.SetDirty(layout);
        }

        private static void UpdateAtlas(UnityEngine.Object[] textures)
        {
            SpriteAtlas atlas = LoadOrCreateAtlas();
            UnityEngine.Object[] old = atlas.GetPackables();
            if (old.Length == textures.Length && old.SequenceEqual(textures)) return;
            atlas.Remove(old); atlas.Add(textures); EditorUtility.SetDirty(atlas);
        }

        private static SpriteAtlas LoadOrCreateAtlas()
        {
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            if (atlas != null) return atlas;
            atlas = new SpriteAtlas();
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings { readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear });
            AssetDatabase.CreateAsset(atlas, AtlasPath); return atlas;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void CopyChanged(string source, string target)
        {
            byte[] bytes = File.ReadAllBytes(source);
            if (File.Exists(target))
            {
                using SHA256 hash = SHA256.Create();
                if (hash.ComputeHash(bytes).SequenceEqual(hash.ComputeHash(File.ReadAllBytes(target)))) return;
            }
            File.WriteAllBytes(target, bytes);
        }

        [Serializable] public sealed class ProcessedClip
        {
            public string sheetId; public string sourceHash; public string handedness; public string reviewStatus;
            public string sourceFile; public int rows; public int cols;
            public bool loop; public ProcessedFrame[] frames; public ProcessedEvent[] events;
            public ImportSettings importSettings = new ImportSettings();
        }
        [Serializable] public sealed class ProcessedFrame { public string file; public int cellIndex; public float durationMs = 100; public Pivot pivot = new Pivot(); public CellRect sourceCellRect = new CellRect(); }
        // JsonUtility의 중첩 객체 기본 생성과 실제 접점 지정은 별도 플래그로 구분한다.
        [Serializable] public sealed class ProcessedEvent { public string name; public int frameIndex; public bool hasSourcePosition; public Pivot sourcePositionNormalized; }
        [Serializable] public sealed class Pivot { public float x = 0.5f; public float y = 0.08f; }
        [Serializable] public sealed class CellRect { public float x; public float y; public float width; public float height; }
        [Serializable] public sealed class ImportSettings { public float pixelsPerUnit = 100; public string filterMode = "Bilinear"; public string compression = "Uncompressed"; }
        [Serializable] public sealed class ProcessedIndex { public ProcessedIndexEntry[] clips; }
        [Serializable] public sealed class ProcessedIndexEntry { public string metadataFile; }
    }
}
