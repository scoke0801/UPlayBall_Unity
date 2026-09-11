using System.Linq;
using System.Reflection;
using Baseball.Editor.HistoricalDatabase;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Editor
{
    /// <summary>연구 보충의 원기록 미확보와 일반 선수의 기록 필수 계약을 검증한다.</summary>
    public sealed class HistoricalResearchSupplementValidationTests
    {
        private HistoricalArchiveData _archive;

        [SetUp]
        public void LoadRuntimeArchive()
        {
            _archive = new HistoricalArchiveRepository().Load(
                "Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Runtime");
        }

        [Test]
        public void RuntimeArchive_AcceptsExplicitlyUnavailableResearchRecords()
        {
            Assert.That(_archive.PlayerRows.Any(row => row.Season.SourceDataKind == "ResearchCardSupplement"), Is.True);
            Assert.That(new HistoricalDatabaseValidationService().Validate(_archive).ErrorCount, Is.Zero);
        }

        [TestCase("")]
        [TestCase("Available")]
        public void ResearchRecord_RejectsMissingOrAvailableStatus(string status)
        {
            HistoricalPlayerSeason season = FindResearchSeason();
            SetField(season, "sourceRecordAvailability", status);
            AssertJoinError(season);
        }

        [Test]
        public void OrdinarySeason_RejectsMissingRecordEvenWithUnavailableStatus()
        {
            HistoricalPlayerSeason season = FindResearchSeason();
            SetField(season, "sourceDataKind", "");
            AssertJoinError(season);
        }

        [Test]
        public void ResearchSeason_RejectsAttachedRecord()
        {
            HistoricalPlayerSeason season = _archive.PlayerRows.First(row => row.Record != null).Season;
            SetField(season, "sourceDataKind", "ResearchCardSupplement");
            SetField(season, "sourceRecordAvailability", "Unavailable");
            AssertJoinError(season);
        }

        private HistoricalPlayerSeason FindResearchSeason() => _archive.PlayerRows
            .First(row => row.Season.SourceDataKind == "ResearchCardSupplement").Season;

        private void AssertJoinError(HistoricalPlayerSeason season)
        {
            Assert.That(new HistoricalDatabaseValidationService().Validate(_archive).Issues.Any(issue =>
                issue.Severity == HistoricalValidationSeverity.Error && issue.Category == "Join" &&
                issue.EntityId == season.PlayerSeasonId), Is.True);
        }

        private static void SetField(HistoricalPlayerSeason season, string name, string value)
        {
            typeof(HistoricalPlayerSeason).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(season, value);
        }
    }
}
