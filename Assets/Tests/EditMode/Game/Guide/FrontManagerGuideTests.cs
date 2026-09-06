using System.Collections.Generic;
using Baseball.Game.Guide;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game.Guide
{
    /// <summary>Guide 데이터 정본의 결정론·중복 억제·우선순위와 안전 지점 Queue 계약을 검증한다.</summary>
    public sealed class FrontManagerGuideTests
    {
        private const string DatasetPath =
            "Assets/10.Datas/FrontManager/front_manager_guide_dataset_v1.json";

        private GuideDatasetCatalog _catalog;
        private GuideDatasetData _data;

        [SetUp]
        public void SetUp()
        {
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(DatasetPath);
            Assert.IsNotNull(text, "Front Manager Dataset을 찾지 못했습니다.");
            _data = JsonUtility.FromJson<GuideDatasetData>(text.text);
            bool created = GuideDatasetFactory.TryCreate(_data, out _catalog, out GuideValidationIssue[] issues);
            Assert.IsTrue(created, issues.Length > 0 ? issues[0].ToString() : "Catalog 생성 실패");
        }

        [Test]
        public void Dataset_106개Cue와318개Variation을검증한다()
        {
            Assert.AreEqual(106, _data.cueDefinitions.Length);
            Assert.AreEqual(106, _data.factTypeIndex.Length);
            int variations = 0;
            for (int index = 0; index < _data.cueDefinitions.Length; index++)
                variations += _data.cueDefinitions[index].variations.Length;
            Assert.AreEqual(318, variations);
        }

        [Test]
        public void Dataset_NullCta는런타임에서선택지없음으로정규화된다()
        {
            IReadOnlyList<GuideCueDefinition> cues = _catalog.GetCues("GuideRoleExplanation");

            Assert.AreEqual(1, cues.Count);
            Assert.AreEqual("TUTORIAL_GUIDE_ROLE", cues[0].CueId);
            Assert.IsFalse(cues[0].Cta.HasValue);
        }

        [Test]
        public void Dataset_Action만비어있는Cta는검증에서거부한다()
        {
            _data.cueDefinitions[9].cta = new GuideCtaData
            {
                action = string.Empty,
                label = "확인"
            };

            GuideValidationIssue[] issues = GuideDatasetValidator.Validate(_data);

            Assert.IsTrue(System.Array.Exists(
                issues,
                issue => issue.Code == "CTA_ACTION_REQUIRED" &&
                         issue.Path == "$.cueDefinitions[9].cta.action"));
        }

        [Test]
        public void Dataset_모든Cue가선언된Payload와Context로표시된다()
        {
            for (int cueIndex = 0; cueIndex < _data.cueDefinitions.Length; cueIndex++)
            {
                GuideCueData cue = _data.cueDefinitions[cueIndex];
                GuideFactTypeIndexData factContract = FindFactContract(cue.factType);
                GuideModeScope mode = cue.modeScope == nameof(GuideModeScope.Common)
                    ? GuideModeScope.Owner
                    : System.Enum.Parse<GuideModeScope>(cue.modeScope);
                var builder = new GuideFactBuilder(mode, cue.factType, Identity("all-cues-" + cueIndex));
                for (int payloadIndex = 0; payloadIndex < factContract.requiredPayload.Length; payloadIndex++)
                    builder.AddPayload(factContract.requiredPayload[payloadIndex], "1");
                for (int contextIndex = 0; contextIndex < _data.designContract.runtimeContextKeys.Length; contextIndex++)
                    builder.AddContext(_data.designContract.runtimeContextKeys[contextIndex], "1");

                var guide = new FrontManagerGuide(_catalog);
                GuideEnqueueResult result = guide.Enqueue(builder.Build());
                Assert.IsTrue(result.IsAccepted, $"{cue.cueId}: {result.Error}");
                bool found = false;
                while (guide.TryDequeue(SafeContext(), out GuideMessage message))
                    found |= message.CueId == cue.cueId;
                Assert.IsTrue(found, $"{cue.cueId}가 Queue에서 표시되지 않았습니다.");
            }
        }

        [Test]
        public void WeightedHash_같은사건은새Runtime에서도같은Variation을고른다()
        {
            GuideMessage first = ResolveForeignLimit(CreateForeignLimitFact(7, "event-42"));
            GuideMessage second = ResolveForeignLimit(CreateForeignLimitFact(7, "event-42"));

            Assert.AreEqual(first.VariationId, second.VariationId);
            Assert.AreEqual(first.Text, second.Text);
        }

        [Test]
        public void WeightedHash_새사건에서는세Variation이모두섞인다()
        {
            var selected = new HashSet<string>();
            for (int index = 0; index < 300; index++)
                selected.Add(ResolveForeignLimit(CreateForeignLimitFact(index, "event-" + index)).VariationId);

            CollectionAssert.AreEquivalent(
                new[] { "ROSTER_FOREIGN_LIMIT_A", "ROSTER_FOREIGN_LIMIT_B", "ROSTER_FOREIGN_LIMIT_C" },
                selected);
        }

        [Test]
        public void RepeatPolicy_같은Revision은Queue와표시후모두중복제거한다()
        {
            var guide = new FrontManagerGuide(_catalog);
            GuideFact fact = CreateForeignLimitFact(3, "event-repeat");

            GuideEnqueueResult first = guide.Enqueue(fact);
            GuideEnqueueResult queuedDuplicate = guide.Enqueue(fact);
            Assert.AreEqual(1, first.EnqueuedCount);
            Assert.AreEqual(1, queuedDuplicate.DuplicateCount);
            Assert.IsTrue(guide.TryDequeue(SafeContext(), out _));

            GuideEnqueueResult displayedDuplicate = guide.Enqueue(fact);
            Assert.AreEqual(0, displayedDuplicate.EnqueuedCount);
            Assert.AreEqual(1, displayedDuplicate.DuplicateCount);
        }

        [Test]
        public void RosterValid_멤버교체로Revision이바뀌어도Save당한번만표시한다()
        {
            var guide = new FrontManagerGuide(_catalog);
            GuideFact firstRoster = new GuideFactBuilder(
                    GuideModeScope.Owner,
                    "RosterValidated",
                    Identity("roster-valid-1"))
                .AddContext("rosterRevision", 1)
                .Build();
            GuideFact changedRoster = new GuideFactBuilder(
                    GuideModeScope.Owner,
                    "RosterValidated",
                    Identity("roster-valid-2"))
                .AddContext("rosterRevision", 2)
                .Build();

            Assert.AreEqual(1, guide.Enqueue(firstRoster).EnqueuedCount);
            Assert.IsTrue(guide.TryDequeue(SafeContext(), out GuideMessage message));
            Assert.AreEqual("ROSTER_VALID", message.CueId);

            GuideEnqueueResult changedResult = guide.Enqueue(changedRoster);
            Assert.AreEqual(0, changedResult.EnqueuedCount);
            Assert.AreEqual(1, changedResult.DuplicateCount);
        }

        [Test]
        public void Suppression_차단중에는Queue를유지하고해제후표시한다()
        {
            var guide = new FrontManagerGuide(_catalog);
            guide.Enqueue(CreateForeignLimitFact(4, "event-suppressed"));

            Assert.IsFalse(guide.TryDequeue(
                new GuideDisplayContext(new[] { "BlockingCinematic" }, false, false), out _));
            Assert.AreEqual(1, guide.QueuedCount);
            Assert.IsTrue(guide.TryDequeue(SafeContext(), out _));
        }

        [Test]
        public void MatchSafePoint_일반알림은대기하고Critical은먼저표시한다()
        {
            var guide = new FrontManagerGuide(_catalog);
            guide.Enqueue(new GuideFactBuilder(
                    GuideModeScope.Owner,
                    "RosterValidated",
                    Identity("valid"))
                .AddContext("rosterRevision", 1)
                .Build());
            guide.Enqueue(new GuideFactBuilder(
                    GuideModeScope.Owner,
                    "RosterInvalidTotal",
                    Identity("invalid"))
                .AddPayload("current", 24)
                .AddPayload("required", 25)
                .AddContext("rosterRevision", 2)
                .Build());

            var unsafeMatch = new GuideDisplayContext(System.Array.Empty<string>(), true, false);
            Assert.IsTrue(guide.TryDequeue(unsafeMatch, out GuideMessage critical));
            Assert.AreEqual(GuidePriority.Critical, critical.Priority);
            Assert.IsFalse(guide.TryDequeue(unsafeMatch, out _));
            Assert.IsTrue(guide.TryDequeue(new GuideDisplayContext(System.Array.Empty<string>(), true, true), out _));
        }

        [Test]
        public void FactContract_없는Fact와payload누락을거부한다()
        {
            var guide = new FrontManagerGuide(_catalog);
            GuideEnqueueResult unknown = guide.Enqueue(new GuideFact(
                GuideModeScope.Owner,
                "NotExistingFact",
                Identity("unknown")));
            GuideEnqueueResult missing = guide.Enqueue(new GuideFact(
                GuideModeScope.Owner,
                "ForeignPlayerLimitExceeded",
                Identity("missing")));

            Assert.IsFalse(unknown.IsAccepted);
            Assert.IsFalse(missing.IsAccepted);
            StringAssert.Contains("factType", unknown.Error);
            StringAssert.Contains("current", missing.Error);
        }

        [Test]
        public void RepeatState_SaveLoad뒤에도표시이력을유지한다()
        {
            var first = new FrontManagerGuide(_catalog);
            GuideFact fact = CreateForeignLimitFact(9, "persisted");
            first.Enqueue(fact);
            Assert.IsTrue(first.TryDequeue(SafeContext(), out _));

            var restoredState = new GuideRepeatState();
            restoredState.Restore(first.RepeatState.Capture());
            var restored = new FrontManagerGuide(_catalog, restoredState);
            GuideEnqueueResult result = restored.Enqueue(fact);

            Assert.AreEqual(0, result.EnqueuedCount);
            Assert.AreEqual(1, result.DuplicateCount);
        }

        [Test]
        public void ClearPending_대기열만비우고표시이력은유지한다()
        {
            var guide = new FrontManagerGuide(_catalog);
            GuideFact fact = CreateForeignLimitFact(12, "clear-pending");
            guide.Enqueue(fact);
            guide.ClearPending();
            Assert.AreEqual(0, guide.QueuedCount);

            Assert.AreEqual(1, guide.Enqueue(fact).EnqueuedCount);
            Assert.IsTrue(guide.TryDequeue(SafeContext(), out _));
            guide.ClearPending();
            Assert.AreEqual(1, guide.Enqueue(fact).DuplicateCount);
        }

        [Test]
        public void Tutorial_재진입과복원후에는숨기고새게임에서는다시표시한다()
        {
            foreach (GuideCueData cue in _data.cueDefinitions)
            {
                if (cue.repeatPolicy.cooldownScope != "Save")
                    continue;

                GuideModeScope mode = cue.modeScope == "Career" ? GuideModeScope.Career : GuideModeScope.Owner;
                var payload = new Dictionary<string, string>();
                foreach (string key in FindFactContract(cue.factType).requiredPayload)
                    payload.Add(key, "1");
                var firstEntry = new GuideFact(mode, cue.factType, Identity("first-entry"), payload);
                var reentry = new GuideFact(mode, cue.factType, Identity("reentry"), payload);
                var guide = new FrontManagerGuide(_catalog);
                Assert.AreEqual(1, guide.Enqueue(firstEntry).EnqueuedCount, cue.cueId);
                Assert.AreEqual(1, guide.Enqueue(reentry).DuplicateCount, cue.cueId);
                Assert.IsTrue(guide.TryDequeue(SafeContext(), out _), cue.cueId);
                Assert.AreEqual(1, guide.Enqueue(reentry).DuplicateCount, cue.cueId);

                var restored = new GuideRepeatState();
                restored.Restore(guide.RepeatState.Capture());
                var loadedGuide = new FrontManagerGuide(_catalog, restored);
                Assert.AreEqual(1, loadedGuide.Enqueue(reentry).DuplicateCount, cue.cueId);

                // 같은 설정과 Seed로 시작한 새 게임도 이전 게임의 노출 이력을 물려받지 않는다.
                restored.Restore(new GuideRepeatStateData());
                Assert.AreEqual(1, loadedGuide.Enqueue(firstEntry).EnqueuedCount, cue.cueId);
                Assert.IsTrue(loadedGuide.TryDequeue(SafeContext(), out _), cue.cueId);
            }
        }

        [Test]
        public void SkillBlock_연속뽑기는한개안내만보류하고이후획득은반복하지않는다()
        {
            var guide = new FrontManagerGuide(_catalog);
            var revealContext = new GuideDisplayContext(new[] { "BlockingCinematic" }, false, false);
            for (int index = 0; index < 30; index++)
            {
                GuideFact fact = new GuideFactBuilder(GuideModeScope.Owner, "SkillBlockAcquired",
                        Identity("purchase-" + index))
                    .AddPayload("definitionId", "block-" + index)
                    .AddPayload("rarity", "일반")
                    .Build();
                Assert.AreEqual(index == 0 ? 1 : 0, guide.Enqueue(fact).EnqueuedCount);
                Assert.IsFalse(guide.TryDequeue(revealContext, out _));
            }

            Assert.AreEqual(1, guide.QueuedCount);
            Assert.IsTrue(guide.TryDequeue(SafeContext(), out GuideMessage message));
            Assert.AreEqual("SKILL_BLOCK_ACQUIRED", message.CueId);
            Assert.IsFalse(guide.TryDequeue(SafeContext(), out _));
            var nextPurchase = new GuideFactBuilder(GuideModeScope.Owner, "SkillBlockAcquired",
                    Identity("next-purchase"))
                .AddPayload("definitionId", "another-block")
                .AddPayload("rarity", "특수")
                .Build();
            Assert.AreEqual(1, guide.Enqueue(nextPurchase).DuplicateCount);
        }

        [Test]
        public void ScoutNewCard_같은뽑기순번은한번만안내한다()
        {
            var guide = new FrontManagerGuide(_catalog);
            GuideFact first = CreateScoutNewCardFact("CARD_A", "17");
            GuideFact second = CreateScoutNewCardFact("CARD_B", "17");

            Assert.That(guide.Enqueue(first).IsAccepted, Is.True);
            GuideEnqueueResult duplicate = guide.Enqueue(second);

            Assert.That(duplicate.IsAccepted, Is.True, duplicate.Error);
            Assert.That(duplicate.DuplicateCount, Is.EqualTo(1));
            Assert.That(guide.QueuedCount, Is.EqualTo(1));
        }

        private GuideMessage ResolveForeignLimit(GuideFact fact)
        {
            var guide = new FrontManagerGuide(_catalog);
            GuideEnqueueResult result = guide.Enqueue(fact);
            Assert.IsTrue(result.IsAccepted, result.Error);
            Assert.IsTrue(guide.TryDequeue(SafeContext(), out GuideMessage message));
            return message;
        }

        private GuideFactTypeIndexData FindFactContract(string factType)
        {
            for (int index = 0; index < _data.factTypeIndex.Length; index++)
                if (_data.factTypeIndex[index].factType == factType)
                    return _data.factTypeIndex[index];
            Assert.Fail($"{factType} Fact 계약을 찾지 못했습니다.");
            return null;
        }

        private static GuideFact CreateForeignLimitFact(int revision, string eventId)
        {
            return new GuideFactBuilder(
                    GuideModeScope.Owner,
                    "ForeignPlayerLimitExceeded",
                    Identity(eventId))
                .AddPayload("current", 4)
                .AddPayload("limit", 3)
                .AddContext("rosterRevision", revision)
                .Build();
        }

        private static GuideFact CreateScoutNewCardFact(string cardId, string scoutRollId)
        {
            return new GuideFactBuilder(
                    GuideModeScope.Owner,
                    "ScoutNewCardAcquired",
                    Identity("scout-new-card:" + cardId))
                .AddPayload("playerName", cardId)
                .AddPayload("cost", 5)
                .AddPayload("edition", "일반")
                .AddContext("scoutRollId", scoutRollId)
                .Build();
        }

        private static GuideFactIdentity Identity(string eventId) =>
            new(123456789UL, eventId, "test-save", 0);

        private static GuideDisplayContext SafeContext() =>
            new(System.Array.Empty<string>(), false, true);
    }
}
