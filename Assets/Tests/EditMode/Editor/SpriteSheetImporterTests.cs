using System;
using System.IO;
using Baseball.Editor.SpriteSheets;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Editor
{
    /// <summary>잘못된 처리 결과가 실제 경기 에셋에 진입하기 전에 차단되는지 확인한다.</summary>
    public sealed class SpriteSheetImporterTests
    {
        [Test]
        public void Validate_NeedsReviewCannotBeApproved()
        {
            var clip = CreateClip();
            clip.handedness = "NeedsReview";
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
            clip.reviewStatus = "NeedsReview";
            Assert.DoesNotThrow(() => SpriteSheetBatchImporter.Validate(clip));
        }

        [Test]
        public void Validate_UnknownHandednessDoesNotBecomeShared()
        {
            var clip = CreateClip(); clip.handedness = "Unknown";
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
        }

        [Test]
        public void Validate_ReleaseBeforeCatchIsRejected()
        {
            var clip = CreateClip();
            clip.events = new[]
            {
                new SpriteSheetBatchImporter.ProcessedEvent { name = "GloveContact", frameIndex = 1 },
                new SpriteSheetBatchImporter.ProcessedEvent { name = "ThrowRelease", frameIndex = 0 }
            };
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
        }

        [Test]
        public void Validate_EventOutsideFramesIsRejected()
        {
            var clip = CreateClip();
            clip.events = new[] { new SpriteSheetBatchImporter.ProcessedEvent { name = "BallRelease", frameIndex = 2 } };
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
        }

        [Test]
        public void Validate_InvalidTimingAndDuplicateCellsAreRejected()
        {
            var clip = CreateClip(); clip.frames[1].cellIndex = 0;
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
            clip.frames[1].cellIndex = 1; clip.frames[1].durationMs = float.NaN;
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
        }

        [Test]
        public void Validate_EventAnchorRequiresValidSourceGeometry()
        {
            var clip = CreateClip();
            clip.events = new[] { new SpriteSheetBatchImporter.ProcessedEvent
            {
                name = "BallRelease", frameIndex = 1, hasSourcePosition = true,
                sourcePositionNormalized = new SpriteSheetBatchImporter.Pivot { x = 0.75f, y = 0.4f }
            } };
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
            clip.frames[1].sourceCellRect.width = 362; clip.frames[1].sourceCellRect.height = 362;
            Assert.DoesNotThrow(() => SpriteSheetBatchImporter.Validate(clip));
            clip.events[0].sourcePositionNormalized.y = float.NaN;
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
        }

        [Test]
        public void Validate_EventWithoutAnchorPreservesFallback()
        {
            var clip = CreateClip();
            clip.events = new[] { new SpriteSheetBatchImporter.ProcessedEvent { name = "BallRelease", frameIndex = 1 } };
            Assert.DoesNotThrow(() => SpriteSheetBatchImporter.Validate(clip));
        }

        [Test]
        public void Validate_JsonWithoutAnchorDoesNotInventDefaultCoordinates()
        {
            var clip = UnityEngine.JsonUtility.FromJson<SpriteSheetBatchImporter.ProcessedClip>(
                "{\"sheetId\":\"Pitcher.Pitch.R\",\"sourceHash\":\"test\",\"handedness\":\"R\",\"reviewStatus\":\"NeedsReview\"," +
                "\"frames\":[{\"file\":\"frame_000.png\",\"cellIndex\":0,\"durationMs\":100}]," +
                "\"events\":[{\"name\":\"BallRelease\",\"frameIndex\":0}]}");
            Assert.That(clip.events[0].hasSourcePosition, Is.False);
            Assert.DoesNotThrow(() => SpriteSheetBatchImporter.Validate(clip));
            clip.events[0].hasSourcePosition = true;
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
        }

        [Test]
        public void Validate_UnknownEventReportsClipAndMarker()
        {
            var clip = CreateClip();
            clip.events = new[] { new SpriteSheetBatchImporter.ProcessedEvent { name = "InvalidMarker", frameIndex = 1 } };
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.Validate(clip));
            StringAssert.Contains(clip.sheetId, exception.Message);
            StringAssert.Contains("InvalidMarker", exception.Message);
            StringAssert.Contains("frameIndex=1", exception.Message);
        }

        [Test]
        public void GetMetadataPaths_IndexExcludesStaleFilesAndRejectsExternalPaths()
        {
            string folder = Path.Combine(Path.GetTempPath(), "SpriteImporterTest_" + Guid.NewGuid().ToString("N"));
            string processed = Path.Combine(folder, "Processed");
            Directory.CreateDirectory(processed);
            try
            {
                string active = Path.Combine(processed, "active.json");
                File.WriteAllText(active, "{}");
                File.WriteAllText(Path.Combine(processed, "stale.json"), "{}");
                string index = Path.Combine(folder, "index.json");
                File.WriteAllText(index, "{\"clips\":[{\"metadataFile\":\"Processed/active.json\"}]}");
                CollectionAssert.AreEqual(new[] { active }, SpriteSheetBatchImporter.GetMetadataPaths(processed));
                File.WriteAllText(Path.Combine(folder, "outside.json"), "{}");
                File.WriteAllText(index, "{\"clips\":[{\"metadataFile\":\"outside.json\"}]}");
                Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.GetMetadataPaths(processed));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void ResolveContainedPath_TraversalIsRejected()
        {
            string root = Path.GetFullPath("output/sprite-sheets/Processed");
            Assert.Throws<InvalidDataException>(() => SpriteSheetBatchImporter.ResolveContainedPath(root, "../outside.png"));
            StringAssert.StartsWith(root, SpriteSheetBatchImporter.ResolveContainedPath(root, "clip/frame_000.png"));
        }

        private static SpriteSheetBatchImporter.ProcessedClip CreateClip() => new SpriteSheetBatchImporter.ProcessedClip
        {
            sheetId = "Pitcher.Pitch.R", handedness = "R", reviewStatus = "Approved", sourceHash = "test",
            frames = new[]
            {
                new SpriteSheetBatchImporter.ProcessedFrame { file = "frame_000.png", cellIndex = 0 },
                new SpriteSheetBatchImporter.ProcessedFrame { file = "frame_001.png", cellIndex = 1 }
            }
        };
    }
}
