using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>기존 구단주 카드 Frame과 성장판을 보존하며 구종 수만 동적으로 반영하는지 검증한다.</summary>
    public sealed class OwnerPlayerCardBackTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void PitcherBack_CreatesOnlyBakedPitchCells(int pitchCount)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject sourceObject = new GameObject("Source", typeof(RectTransform));
            sourceObject.transform.SetParent(canvasObject.transform, false);
            try
            {
                UI_Popup_OwnerPlayerCard.Show(sourceObject.transform, CreatePitcher(pitchCount));
                Transform role = canvasObject.transform.Find(
                    "UI_Popup_OwnerPlayerCard/CardDetail/Back/RoleInformation");
                Assert.That(role, Is.Not.Null);
                Transform back = role.parent;
                Assert.That(back.Find("SkillBoardInformation/Grid/Cell_0_0"), Is.Not.Null);
                for (int index = 0; index < pitchCount; index++)
                    Assert.That(role.Find("PitchSlot" + index), Is.Not.Null);
                Assert.That(role.Find("PitchSlot" + pitchCount), Is.Null);
                Assert.That(role.Find("PitchUnavailable"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void HitterBack_UsesExistingNeutralFrameAndActualRecordFields()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject sourceObject = new GameObject("Source", typeof(RectTransform));
            sourceObject.transform.SetParent(canvasObject.transform, false);
            try
            {
                var record = new[]
                {
                    new OwnerCardRecordFieldSnapshot("PA", "512"),
                    new OwnerCardRecordFieldSnapshot("AVG", "0.301")
                };
                var card = new OwnerCollectionCardSnapshot(
                    "C-H", "P-H", "가상타자", 2025, PlayerPosition.Shortstop, 8,
                    PlayerCardEdition.Normal, 0, 0, false, false,
                    CreateAbilities(), "2025 · 월드 기록", "PS-H", null,
                    Handedness.Right, Handedness.Left, null, record);
                UI_Popup_OwnerPlayerCard.Show(sourceObject.transform, card);
                Transform back = canvasObject.transform.Find("UI_Popup_OwnerPlayerCard/CardDetail/Back");
                Assert.That(back.Find("NeutralFrame").GetComponent<Image>().sprite.name,
                    Is.EqualTo("PlayerCard_Back_Neutral"));
                Assert.That(back.Find("RecordValue0").GetComponent<Text>().text, Is.EqualTo("512"));
                Assert.That(back.Find("RecordValue1").GetComponent<Text>().text, Is.EqualTo("0.301"));
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    Assert.That(back.Find($"SkillBoardInformation/Grid/Cell_{x}_{y}"), Is.Not.Null);
                Assert.That(back.Find("RoleInformation/Position4"), Is.Not.Null);
                Assert.That(back.Find("RoleInformation/PitchSlot0"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        private static OwnerCollectionCardSnapshot CreatePitcher(int pitchCount)
        {
            var pitches = new List<OwnerPitchCardSnapshot>(pitchCount);
            for (int index = 0; index < pitchCount; index++)
                pitches.Add(new OwnerPitchCardSnapshot(
                    (PitchType)index, "긴 구종 이름 " + index, index == pitchCount - 1 ? "SS" : "B+", 151d - index * 4d));
            return new OwnerCollectionCardSnapshot(
                "C-P", "P-P", "가상투수", 2025, PlayerPosition.StartingPitcher, 9,
                PlayerCardEdition.Mvp, 5, 0, false, false,
                CreateAbilities(), "2025 · 월드 기록", "PS-P", PitcherRole.Starter,
                Handedness.Right, Handedness.Right, pitches,
                new[] { new OwnerCardRecordFieldSnapshot("ERA", "2.91") });
        }

        private static AbilityRatings CreateAbilities()
        {
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < values.Length; index++)
                values[index] = 70 + index % 6;
            return new AbilityRatings(values);
        }
    }
}
