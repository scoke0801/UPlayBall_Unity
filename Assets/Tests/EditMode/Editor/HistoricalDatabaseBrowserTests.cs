using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Baseball.Editor.HistoricalDatabase;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baseball.Tests.EditMode.Editor
{
    /// <summary>Historical Archive의 로드·조회·분석·검증 경계를 실제 JSON으로 회귀 검증한다.</summary>
    public sealed class HistoricalDatabaseBrowserTests
    {
        private const string ArchiveRelativePath =
            "Assets/Editor Default Resources/HistoricalSimulation/1982-2025";
        private const string RuntimeArchiveRelativePath =
            "Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Runtime";

        private static HistoricalArchiveData _archive;

        [OneTimeSetUp]
        public void LoadArchive()
        {
            string path = Path.GetFullPath(ArchiveRelativePath);
            _archive = new HistoricalArchiveRepository().Load(path);
        }

        [Test]
        public void Repository_LoadsEveryArchiveEntityAndJoin()
        {
            TestContext.Out.WriteLine($"ArchiveLoadMs={_archive.LoadElapsed.TotalMilliseconds:0.0}");
            Assert.That(_archive.Manifest.Summary.YearCount, Is.EqualTo(44));
            Assert.That(_archive.Persons.Count, Is.EqualTo(3510));
            Assert.That(_archive.PlayerRows.Count, Is.EqualTo(17333));
            Assert.That(_archive.Teams.Count, Is.EqualTo(363));
            Assert.That(_archive.Cards.Count, Is.EqualTo(17333));
            Assert.That(_archive.Records.Count, Is.EqualTo(17333));
            Assert.That(_archive.Awards.Count, Is.EqualTo(555));
            Assert.That(_archive.PlayerRows.All(row => row.Person != null), Is.True);
            Assert.That(_archive.PlayerRows.All(row => row.Record != null), Is.True);
            Assert.That(_archive.Manifest.SourceManifest.NameDataPolicy,
                Is.EqualTo("editor-original-source-v2"));
            Assert.That(_archive.Persons.All(person => !string.IsNullOrWhiteSpace(person.OriginalName)), Is.True);
            Assert.That(_archive.Persons.All(person => string.IsNullOrWhiteSpace(person.FictionalName)), Is.True);
            Assert.That(_archive.PlayerRows.All(row => row.IsOriginalSource), Is.True);
            Assert.That(_archive.PlayerRows.All(row => row.SourceReferenceNames.Count == 1), Is.True);
            Assert.That(_archive.PlayerRows.All(row => row.SourceReferenceNames[0] == row.Name), Is.True);
            Assert.That(_archive.PlayerRows.All(row => row.TrainingCeiling.Length == 0), Is.True);
        }

        [Test]
        public void Query_OriginalNameIsVisibleAndRuntimeAliasDoesNotLeak()
        {
            HistoricalPlayerRow expected = _archive.PlayerRows.Single(row =>
                row.Name == "봉중근" && row.OriginYear == 2008);
            var viewModel = new HistoricalDatabaseViewModel();
            viewModel.SetData(_archive);

            viewModel.Filter.SearchText = "봉중근";
            IReadOnlyList<HistoricalPlayerRow> visibleNameResults = viewModel.ApplyQuery();
            Assert.That(visibleNameResults.Count, Is.EqualTo(10));
            Assert.That(visibleNameResults.All(row => row.Name.Contains("봉중근")), Is.True);
            Assert.That(visibleNameResults.Any(row => row.PlayerSeasonId == expected.PlayerSeasonId), Is.True);

            viewModel.Filter.SearchText = "ref:봉중근";
            Assert.That(viewModel.ApplyQuery().Any(row => row.PlayerSeasonId == expected.PlayerSeasonId), Is.True);

            viewModel.Filter.SearchText = "alias:봉중근";
            Assert.That(viewModel.ApplyQuery(), Is.Empty);
        }

        [Test]
        public void Repository_PreservesBongJungGeun2008OriginalRecord()
        {
            HistoricalPlayerRow row = _archive.PlayerRows.Single(candidate =>
                candidate.Name == "봉중근" && candidate.OriginYear == 2008);
            HistoricalSeasonRecord record = row.Record;

            Assert.That(row.OriginFranchiseId, Is.EqualTo("LG"));
            Assert.That(row.Position, Is.EqualTo("P"));
            Assert.That(row.PitcherRole, Is.EqualTo("Starter"));
            Assert.That(record.IsOriginalSourceRecord, Is.True);
            Assert.That(record.Games, Is.EqualTo(28));
            Assert.That(record.GamesStarted, Is.EqualTo(28));
            Assert.That(record.PitchingOuts, Is.EqualTo(559));
            Assert.That(record.Wins, Is.EqualTo(11));
            Assert.That(record.Losses, Is.EqualTo(8));
            Assert.That(record.HitsAllowed, Is.EqualTo(153));
            Assert.That(record.HomeRunsAllowed, Is.EqualTo(13));
            Assert.That(record.PitchingWalks, Is.EqualTo(68));
            Assert.That(record.PitchingStrikeouts, Is.EqualTo(140));
            Assert.That(record.EarnedRuns, Is.EqualTo(55));
            Assert.That(record.HasStoredEarnedRunAverage, Is.True);
            Assert.That(record.StoredEarnedRunAverage, Is.EqualTo(2.66d));
            Assert.That(record.HasStoredWhip, Is.True);
            Assert.That(record.StoredWhip, Is.EqualTo(1.19d));
        }

        [Test]
        public void Repository_LoadsLeeDaeHo2020AbilityAndCostTrace()
        {
            HistoricalPlayerRow row = _archive.PlayerRows.Single(candidate =>
                candidate.Name == "이대호" && candidate.OriginYear == 2020);
            HistoricalAbilityDerivationTrace speed = row.Season.AbilityDerivationTrace.Single(trace =>
                trace.Attribute == "Speed");
            HistoricalAbilityComponentTrace success = speed.Components.Single(component =>
                component.Metric == "StolenBaseSuccessRate");

            Assert.That(row.GetBaseAbility(2), Is.EqualTo(speed.RatingAfterClamp));
            Assert.That(row.GetBaseAbility(2), Is.LessThan(60));
            Assert.That(success.Numerator, Is.EqualTo(1d));
            Assert.That(success.Denominator, Is.EqualTo(1d));
            Assert.That(success.Reliability, Is.EqualTo(1d / 21d).Within(1e-8));
            Assert.That(row.Season.CostDerivationTrace.PopulationCount, Is.EqualTo(567));
            Assert.That(row.Cost, Is.EqualTo(row.Season.CostDerivationTrace.Cost));
            Assert.That(row.Season.CostDerivationTrace.Rank,
                Is.InRange(1, row.Season.CostDerivationTrace.PopulationCount));
        }

        [Test]
        public void Roster_2012SkUsesAssignedSlotsWithoutMutatingNaturalRoles()
        {
            HistoricalTeamSeason team = _archive.Teams.Single(candidate =>
                candidate.OriginYear == 2012 && candidate.FranchiseId == "SK");
            IReadOnlyList<HistoricalPlayerRow> roster = team.Core25CardIds
                .Select(cardId => _archive.CardsById[cardId].PlayerSeasonId)
                .Select(playerSeasonId => _archive.PlayersBySeasonId[playerSeasonId])
                .ToArray();
            string[] requiredSlots =
            {
                "StartingHitter:C", "StartingHitter:1B", "StartingHitter:2B", "StartingHitter:3B",
                "StartingHitter:SS", "StartingHitter:LF", "StartingHitter:CF", "StartingHitter:RF",
                "StartingHitter:DH"
            };
            HistoricalPlayerRow chaeByungYong = _archive.PlayerRows.Single(candidate =>
                candidate.Name == "채병용" && candidate.OriginYear == 2012);

            Assert.That(requiredSlots.All(slot => roster.Count(player => player.RosterRole == slot) == 1), Is.True);
            Assert.That(team.RosterSelectionTrace.StartingSlots.Count, Is.EqualTo(8));
            Assert.That(chaeByungYong.PitcherRole, Is.EqualTo("Starter"));
            Assert.That(chaeByungYong.RosterRole, Does.StartWith("ReservePitcher:"));
            Assert.That(
                chaeByungYong.Season.PositionRoleDerivationTrace.SelectedNaturalPitcherRole,
                Is.EqualTo("Starter"));
        }

        [Test]
        public void Manifest_ContainsEveryDerivationCacheVersion()
        {
            HistoricalSourceManifest manifest = _archive.Manifest.SourceManifest;

            Assert.That(_archive.Manifest.ContentSchemaVersion, Is.EqualTo(4));
            Assert.That(manifest.ReferenceDataVersion, Is.EqualTo("kbo-normalized-v3"));
            Assert.That(manifest.RawDataVersion, Has.Length.EqualTo(64));
            Assert.That(manifest.NormalizedContentHash, Has.Length.EqualTo(64));
            Assert.That(manifest.AbilityFormulaVersion, Is.EqualTo("historical-ability-v3"));
            Assert.That(manifest.PositionRoleClassifierVersion, Is.EqualTo("season-position-role-v4"));
            Assert.That(manifest.RosterBuilderVersion, Is.EqualTo("position-first-core25-v2"));
            Assert.That(manifest.CostFormulaVersion, Is.EqualTo("historical-role-composite-v3"));
            Assert.That(manifest.DerivationBalanceVersion, Is.EqualTo("historical-derivation-balance-v4"));
            Assert.That(manifest.SourceIdentityPolicyVersion, Is.EqualTo("editor-source-identity-v1"));
            Assert.That(manifest.SourceAllocationPolicyVersion, Is.EqualTo("official-source-team-audit-v1"));
            Assert.That(manifest.ReplacementGeneratorVersion, Is.EqualTo("replacement-generation-v1"));
            Assert.That(manifest.ReplacementPopulationPolicyVersion, Is.EqualTo("origin-year-position-role-source-only-v1"));
            Assert.That(manifest.SourceBackedPlayerPersonCount, Is.EqualTo(_archive.Persons.Count));
            Assert.That(manifest.SourceBackedPlayerSeasonCount, Is.EqualTo(_archive.PlayerRows.Count));
            Assert.That(manifest.ReplacementGeneratedPlayerPersonCount, Is.Zero);
            Assert.That(manifest.ReplacementGeneratedPlayerSeasonCount, Is.Zero);
        }

        [Test]
        public void CostTrace_DeserializesSourcePopulationContract()
        {
            const string json =
                "{\"dataProvenance\":\"ReplacementGenerated\"," +
                "\"costPopulationSource\":\"OriginYearSourceBacked\"," +
                "\"sourcePopulationSize\":141," +
                "\"replacementExcludedFromThresholdCalculation\":true," +
                "\"thresholds\":[{\"upperExclusive\":0.05,\"cost\":1," +
                "\"sourceCompositeAtBoundary\":42.75}]}";

            HistoricalCostDerivationTrace trace =
                JsonUtility.FromJson<HistoricalCostDerivationTrace>(json);

            Assert.That(trace.DataProvenance, Is.EqualTo("ReplacementGenerated"));
            Assert.That(trace.CostPopulationSource, Is.EqualTo("OriginYearSourceBacked"));
            Assert.That(trace.SourcePopulationSize, Is.EqualTo(141));
            Assert.That(trace.ReplacementExcludedFromThresholdCalculation, Is.True);
            Assert.That(trace.Thresholds.Count, Is.EqualTo(1));
            Assert.That(trace.Thresholds[0].UpperExclusive, Is.EqualTo(0.05d));
            Assert.That(trace.Thresholds[0].Cost, Is.EqualTo(1));
            Assert.That(trace.Thresholds[0].SourceCompositeAtBoundary, Is.EqualTo(42.75d));
        }

        [Test]
        public void Query_CombinesSearchFilterAndStableSort()
        {
            HistoricalPlayerRow expected = _archive.PlayerRows.First(row => row.HasAward("GoldenGlove"));
            var viewModel = new HistoricalDatabaseViewModel();
            viewModel.SetData(_archive);
            viewModel.Filter.SearchText = expected.PlayerPersonId.Substring(0, 12);
            viewModel.Filter.Year = expected.OriginYear;
            viewModel.Filter.Position = expected.Position;
            viewModel.Filter.MinimumCost = expected.Cost;
            viewModel.Filter.MaximumCost = expected.Cost;
            viewModel.Filter.AwardType = "GoldenGlove";
            viewModel.SortField = HistoricalPlayerSortField.Name;
            viewModel.SortDirection = HistoricalSortDirection.Ascending;

            IReadOnlyList<HistoricalPlayerRow> result = viewModel.ApplyQuery();

            Assert.That(result.Any(row => row.PlayerSeasonId == expected.PlayerSeasonId), Is.True);
            Assert.That(result.All(row => row.OriginYear == expected.OriginYear), Is.True);
            Assert.That(result.All(row => row.Cost == expected.Cost), Is.True);
            Assert.That(result.All(row => row.HasAward("GoldenGlove")), Is.True);
            Assert.That(result.Select(row => row.Name), Is.Ordered.Ascending);
        }

        [Test]
        public void Query_AbilityAndPitcherRoleFiltersRespectPlayerFamily()
        {
            var viewModel = new HistoricalDatabaseViewModel();
            viewModel.SetData(_archive);
            viewModel.Filter.PitcherRole = "MiddleRelief";
            viewModel.Filter.AbilityIndex = 7;
            viewModel.Filter.MinimumAbility = 0;
            viewModel.Filter.MaximumAbility = 100;

            IReadOnlyList<HistoricalPlayerRow> result = viewModel.ApplyQuery();

            Assert.That(result.Count, Is.GreaterThan(0));
            Assert.That(result.All(row => row.IsPitcher), Is.True);
            Assert.That(result.All(row => row.PitcherRole == "MiddleRelief"), Is.True);
        }

        [Test]
        public void Sort_DerivedStatisticKeepsUnavailableRowsLastInBothDirections()
        {
            List<HistoricalPlayerRow> ascending = HistoricalPlayerSorter.Sort(
                _archive.PlayerRows,
                HistoricalPlayerSortField.EarnedRunAverage,
                HistoricalSortDirection.Ascending);
            List<HistoricalPlayerRow> descending = HistoricalPlayerSorter.Sort(
                _archive.PlayerRows,
                HistoricalPlayerSortField.EarnedRunAverage,
                HistoricalSortDirection.Descending);

            Assert.That(ascending[0].EarnedRunAverage.HasValue, Is.True);
            Assert.That(descending[0].EarnedRunAverage.HasValue, Is.True);
            Assert.That(ascending[ascending.Count - 1].EarnedRunAverage.HasValue, Is.False);
            Assert.That(descending[descending.Count - 1].EarnedRunAverage.HasValue, Is.False);
        }

        [Test]
        public void Analyzer_ReturnsCompleteDistributions()
        {
            var analyzer = new HistoricalDatabaseAnalyzer();

            var stopwatch = Stopwatch.StartNew();
            HistoricalDatabaseAnalysisResult result = analyzer.Analyze(_archive);
            stopwatch.Stop();
            TestContext.Out.WriteLine($"AnalysisMs={stopwatch.Elapsed.TotalMilliseconds:0.0}");

            Assert.That(result.PlayerCount, Is.EqualTo(17333));
            Assert.That(result.CostDistribution.Sum(bucket => bucket.Count), Is.EqualTo(17333));
            Assert.That(result.PositionDistribution.Sum(bucket => bucket.Count), Is.EqualTo(17333));
            Assert.That(result.AwardDistribution.Sum(bucket => bucket.Count), Is.EqualTo(555));
            Assert.That(result.Abilities.Count, Is.EqualTo(12));
            Assert.That(result.Abilities.All(summary => summary.Count > 0), Is.True);
            Assert.That(result.SeasonStatistics.Count, Is.EqualTo(4));
            Assert.That(result.SeasonStatistics.All(summary => summary.Count > 0), Is.True);
        }

        [Test]
        public void Validator_AcceptsCurrentOriginalArchiveAndRepresentativeRoster()
        {
            var validator = new HistoricalDatabaseValidationService();

            HistoricalDatabaseValidationReport report = validator.Validate(_archive);
            TestContext.Out.WriteLine($"ValidationMs={report.Elapsed.TotalMilliseconds:0.0}");
            HistoricalTeamSeason sourceTeam = _archive.Teams.First(team =>
                team.OriginYear == 2012 && team.FranchiseId == "SK");
            HistoricalTeamValidationResult team = validator.ValidateTeam(_archive, sourceTeam);

            Assert.That(report.ErrorCount, Is.EqualTo(0),
                string.Join("\n", report.Issues
                    .Where(issue => issue.Severity == HistoricalValidationSeverity.Error)
                    .Take(10)
                    .Select(issue => issue.Message)));
            Assert.That(team.IsValid, Is.True);
            Assert.That(team.TotalCount, Is.EqualTo(25));
            Assert.That(team.DuplicatePersonCount, Is.EqualTo(0));
            Assert.That(sourceTeam.Core25CardIds.All(sourceTeam.AllNormalCardIds.Contains), Is.True);
        }

        [Test]
        public void Validator_AcceptsSourceBackedRuntimeArchiveAndReplacementIds()
        {
            HistoricalArchiveData runtimeArchive = new HistoricalArchiveRepository().Load(
                Path.GetFullPath(RuntimeArchiveRelativePath));
            HistoricalDatabaseValidationReport report =
                new HistoricalDatabaseValidationService().Validate(runtimeArchive);

            Assert.That(runtimeArchive.Manifest.SourceManifest.NameDataPolicy,
                Is.EqualTo("runtime-fictional-only-v2"));
            Assert.That(runtimeArchive.PlayerRows.Any(row =>
                row.Season.DataProvenance == "ReplacementGenerated" &&
                row.PlayerPersonId.StartsWith("REPL-PERSON-", StringComparison.Ordinal) &&
                row.PlayerSeasonId.StartsWith("REPL-SEASON-", StringComparison.Ordinal)), Is.True);
            Assert.That(report.ErrorCount, Is.EqualTo(0),
                string.Join("\n", report.Issues
                    .Where(issue => issue.Severity == HistoricalValidationSeverity.Error)
                    .Take(10)
                    .Select(issue => issue.Message)));
        }

        [Test]
        public void Validator_RejectsNormalCardMissingFromOriginTeamPool()
        {
            HistoricalTeamSeason team = _archive.Teams.First(candidate =>
                candidate.AllNormalCardIds.Length > candidate.Core25CardIds.Length);
            string[] pool = team.AllNormalCardIds;
            int reserveIndex = Array.FindIndex(pool, cardId => !team.Core25CardIds.Contains(cardId));
            string missingCardId = pool[reserveIndex];
            string original = pool[reserveIndex];
            pool[reserveIndex] = pool[0];
            try
            {
                HistoricalDatabaseValidationReport report =
                    new HistoricalDatabaseValidationService().Validate(_archive);

                Assert.That(report.Issues.Any(issue =>
                    issue.Severity == HistoricalValidationSeverity.Error &&
                    issue.Category == "Roster" &&
                    issue.EntityId == missingCardId), Is.True);
            }
            finally
            {
                pool[reserveIndex] = original;
            }
        }

        [Test]
        public void Validator_RejectsAwardWithMissingPlayerSeasonReference()
        {
            HistoricalAwardRecord award = _archive.Awards[0];
            FieldInfo seasonIdField = typeof(HistoricalAwardRecord).GetField(
                "playerSeasonId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(seasonIdField, Is.Not.Null);
            string original = award.PlayerSeasonId;
            seasonIdField.SetValue(award, "SEASON_00000000000000000000");
            try
            {
                HistoricalDatabaseValidationReport report =
                    new HistoricalDatabaseValidationService().Validate(_archive);

                Assert.That(report.Issues.Any(issue =>
                    issue.Severity == HistoricalValidationSeverity.Error &&
                    issue.Category == "원본 수상" &&
                    issue.EntityId.Contains("SEASON_00000000000000000000")), Is.True);
            }
            finally
            {
                seasonIdField.SetValue(award, original);
            }
        }

        [Test]
        public void Validator_UsesRawManifestPathInArchiveHash()
        {
            HistoricalArchiveYearEntry entry = _archive.Manifest.Years[0];
            FieldInfo pathField = typeof(HistoricalArchiveYearEntry).GetField(
                "path",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pathField, Is.Not.Null);
            string original = entry.Path;
            pathField.SetValue(entry, original.Replace('/', '\\'));
            try
            {
                HistoricalDatabaseValidationReport report =
                    new HistoricalDatabaseValidationService().Validate(_archive);

                Assert.That(report.Issues.Any(issue =>
                    issue.Severity == HistoricalValidationSeverity.Error &&
                    issue.EntityId == "assetArchiveHash"), Is.True);
            }
            finally
            {
                pathField.SetValue(entry, original);
            }
        }

        [Test]
        public void RawJson_ReturnsExactSelectedSourceObject()
        {
            HistoricalPlayerRow player = _archive.PlayerRows[0];
            var viewModel = new HistoricalDatabaseViewModel();
            viewModel.SetData(_archive);

            bool success = viewModel.TryGetRawJson(player, out string rawJson, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(rawJson, Does.StartWith("{"));
            Assert.That(rawJson, Does.Contain($"\"playerSeasonId\":\"{player.PlayerSeasonId}\""));
            Assert.That(rawJson, Does.Contain("\"baseAttributes\""));
        }

        [Test]
        public void Schema_DoesNotPretendUnavailableHiddenOrPitchDataExists()
        {
            string[] personProperties = typeof(HistoricalPlayerPerson).GetProperties()
                .Select(property => property.Name)
                .ToArray();
            string[] seasonProperties = typeof(HistoricalPlayerSeason).GetProperties()
                .Select(property => property.Name)
                .ToArray();

            Assert.That(personProperties, Does.Not.Contain("HiddenStats"));
            Assert.That(personProperties, Does.Not.Contain("Personality"));
            Assert.That(seasonProperties, Does.Not.Contain("PitchArsenal"));
            Assert.That(seasonProperties, Does.Not.Contain("PitchRepertoire"));
        }

        [Test]
        public void PathValidation_ExplainsMissingManifest()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                HistoricalArchivePathValidation result =
                    new HistoricalArchiveRepository().ValidatePath(directory);

                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Message, Does.Contain("manifest.json"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Repository_ReportsInvalidYearJsonWithoutBreakingTheWindowLayer()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "player_persons.json"), "[]");
                Directory.CreateDirectory(Path.Combine(directory, "Years"));
                File.WriteAllText(Path.Combine(directory, "Years", "2000.json"), "not-json");
                File.WriteAllText(
                    Path.Combine(directory, "manifest.json"),
                    "{\"assetFormatVersion\":1,\"contentSchemaVersion\":1," +
                    "\"assetArchiveHash\":\"\",\"playerPersons\":{" +
                    "\"path\":\"player_persons.json\",\"sha256\":\"\",\"byteLength\":2,\"count\":0}," +
                    "\"sourceManifest\":{},\"summary\":{\"yearCount\":1},\"years\":[{" +
                    "\"year\":2000,\"path\":\"Years/2000.json\",\"sha256\":\"\",\"byteLength\":8}]}" );

                TestDelegate load = () => new HistoricalArchiveRepository().Load(directory);

                Assert.That(load, Throws.TypeOf<InvalidDataException>());
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Window_CreatesCompleteVisualTreeWithoutStartingAutomaticLoad()
        {
            const string preferenceKey = "Baseball.Editor.HistoricalDatabase.LastSource";
            bool hadPreference = EditorPrefs.HasKey(preferenceKey);
            string previousPreference = EditorPrefs.GetString(preferenceKey, string.Empty);
            EditorPrefs.DeleteKey(preferenceKey);
            HistoricalDatabaseBrowserWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<HistoricalDatabaseBrowserWindow>();
                window.CreateGUI();

                Assert.That(window.rootVisualElement.Q<MultiColumnListView>("player-list"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<DropdownField>("player-ability-filter"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<VisualElement>("season-statistics-summary"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<MultiColumnListView>("validation-list"), Is.Not.Null);
            }
            finally
            {
                if (window != null)
                    ScriptableObject.DestroyImmediate(window);
                if (hadPreference)
                    EditorPrefs.SetString(preferenceKey, previousPreference);
                else
                    EditorPrefs.DeleteKey(preferenceKey);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "UPlayBall_HistoricalDatabaseTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
