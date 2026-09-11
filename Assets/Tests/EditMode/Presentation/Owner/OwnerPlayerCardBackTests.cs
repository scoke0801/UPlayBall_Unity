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
    /// <summary>카드 시각 구성 변경 뒤에도 실제 구종·기록·성장판 정보가 보존되는지 검증한다.</summary>
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
        public void HitterBack_UsesActualRecordFieldsAndSkillBoard()
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
                    Handedness.Right, Handedness.Left, null, record, condition: 73, conditionLabel: "좋음");
                UI_Popup_OwnerPlayerCard.Show(sourceObject.transform, card);
                Transform back = canvasObject.transform.Find("UI_Popup_OwnerPlayerCard/CardDetail/Back");
                Assert.That(back.Find("RecordValue0").GetComponent<Text>().text, Is.EqualTo("512"));
                Assert.That(back.Find("RecordValue1").GetComponent<Text>().text, Is.EqualTo("0.301"));
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    Transform cell = back.Find($"SkillBoardInformation/Grid/Cell_{x}_{y}");
                    Assert.That(cell, Is.Not.Null);
                    Assert.That(cell.Find("TraitSocket"), Is.Null);
                    Assert.That(cell.GetComponent<Image>().color,
                        Is.EqualTo(back.Find("SkillBoardInformation/Grid/Cell_0_0").GetComponent<Image>().color));
                }
                Assert.That(back.Find("RoleInformation/DefenseDiagram/Field").GetComponent<UICardDefenseField>(), Is.Not.Null);
                Assert.That(back.Find("RoleInformation/DefenseDiagram/Field/PositionLabel").GetComponent<Text>().text,
                    Is.EqualTo("유격수"));
                Assert.That(back.Find("RoleInformation/Position4"), Is.Null);
                Assert.That(back.Find("RoleInformation/DefenseDiagram/Field/PositionBall").GetComponent<Image>().sprite,
                    Is.Not.Null);
                Transform front = back.parent.Find("Front");
                Assert.That(front.Find("ConditionPanel/Value").GetComponent<Text>().text, Is.EqualTo("73"));
                Assert.That(front.Find("ConditionPanel/State").GetComponent<Text>().text, Is.EqualTo("좋음"));
                Assert.That(back.Find("RoleInformation/PitchSlot0"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [TestCase(TetrominoShape.I, SkillBlockRarity.Normal, 99, 165, 68, 0)]
        [TestCase(TetrominoShape.O, SkillBlockRarity.Rare, 61, 139, 210, 1)]
        [TestCase(TetrominoShape.T, SkillBlockRarity.Elite, 177, 83, 185, 2)]
        [TestCase(TetrominoShape.S, SkillBlockRarity.Unique, 224, 160, 44, 3)]
        [TestCase(TetrominoShape.Z, SkillBlockRarity.Legendary, 217, 79, 102, 0)]
        [TestCase(TetrominoShape.J, SkillBlockRarity.Normal, 99, 165, 68, 1)]
        [TestCase(TetrominoShape.L, SkillBlockRarity.Rare, 61, 139, 210, 3)]
        public void SkillBoard_장착블록의리소스와등급색상및회전을표시한다(
            TetrominoShape shape, SkillBlockRarity rarity, int red, int green, int blue, int rotation)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject sourceObject = new GameObject("Source", typeof(RectTransform));
            sourceObject.transform.SetParent(canvasObject.transform, false);
            try
            {
                var placement = new OwnerSkillBlockPlacementSnapshot(
                    TetrominoShapeCatalog.CreateCells(shape), 0, 0, rotation, rarity);
                var card = new OwnerCollectionCardSnapshot(
                    "C-B", "P-B", "블록타자", 2025, PlayerPosition.Shortstop, 8,
                    PlayerCardEdition.Normal, 0, 0, false, false,
                    CreateAbilities(), "루키 리그 · 현재 시즌", "PS-B", null,
                    Handedness.Right, Handedness.Right, null, null,
                    placedSkillBlockCount: 1,
                    skillBlockPlacements: new[] { placement });

                UI_Popup_OwnerPlayerCard.Show(sourceObject.transform, card);

                Image block = canvasObject.transform.Find(
                    "UI_Popup_OwnerPlayerCard/CardDetail/Back/SkillBoardInformation/Grid/PlacedBlock_0")
                    .GetComponent<Image>();
                Assert.That(block.sprite, Is.Not.Null);
                Assert.That(block.sprite.name, Is.EqualTo("SkillBlock_" + shape));
                Assert.That(block.color, Is.EqualTo((Color)new Color32((byte)red, (byte)green, (byte)blue, 255)));
                Assert.That(Mathf.DeltaAngle(block.rectTransform.localEulerAngles.z, rotation * 90f),
                    Is.EqualTo(0f).Within(.01f));
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void Navigation_첫카드와마지막카드에서바깥방향버튼을비활성화한다()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject sourceObject = new GameObject("Source", typeof(RectTransform));
            sourceObject.transform.SetParent(canvasObject.transform, false);
            try
            {
                OwnerCollectionCardSnapshot[] cards =
                {
                    CreateHitter("C-1", "1번 타자"),
                    CreateHitter("C-2", "2번 타자"),
                    CreateHitter("C-3", "벤치 5번")
                };
                UI_Popup_OwnerPlayerCard.Show(sourceObject.transform, cards, 0);
                Transform popup = canvasObject.transform.Find("UI_Popup_OwnerPlayerCard");
                Button previous = popup.Find("PreviousCard").GetComponent<Button>();
                Button next = popup.Find("NextCard").GetComponent<Button>();
                var card = (RectTransform)popup.Find("CardDetail");
                var close = (RectTransform)popup.Find("Close");

                Assert.That(previous.interactable, Is.False);
                Assert.That(next.interactable, Is.True);
                Assert.That(popup.Find("FlipHint"), Is.Null);
                Assert.That(previous.GetComponent<RectTransform>().anchoredPosition.x, Is.LessThan(-card.rect.width * .5f));
                Assert.That(next.GetComponent<RectTransform>().anchoredPosition.x, Is.GreaterThan(card.rect.width * .5f));
                Assert.That(close.anchoredPosition.x, Is.GreaterThanOrEqualTo(card.rect.width * .5f));
                Assert.That(close.anchoredPosition.y, Is.GreaterThan(0f));
                next.onClick.Invoke();
                next.onClick.Invoke();

                Assert.That(popup.Find("CardDetail/Front/Name").GetComponent<Text>().text, Is.EqualTo("벤치 5번"));
                Assert.That(previous.interactable, Is.True);
                Assert.That(next.interactable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [TestCase(1)]
        [TestCase(17)]
        public void Detail_화면중앙모달에성장출처별막대를표시한다(int teamColor)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject sourceObject = new GameObject("Source", typeof(RectTransform));
            sourceObject.transform.SetParent(canvasObject.transform, false);
            try
            {
                var breakdowns = new OwnerAbilityBreakdownSnapshot[PlayerAbilityCatalog.AbilityCount];
                for (int index = 0; index < breakdowns.Length; index++)
                    breakdowns[index] = new OwnerAbilityBreakdownSnapshot(70, 2, 3, teamColor, 2, 2);
                var card = new OwnerCollectionCardSnapshot(
                    "C-G", "P-G", "성장타자", 2025, PlayerPosition.Shortstop, 8,
                    PlayerCardEdition.Normal, 2, 0, false, false,
                    CreateAbilities(), abilityBreakdowns: breakdowns, abilityGraphMaximum: 140);

                UI_Popup_OwnerPlayerCard.Show(sourceObject.transform, card);

                Transform popup = canvasObject.transform.Find("UI_Popup_OwnerPlayerCard");
                Transform front = popup.Find("CardDetail/Front");
                Assert.That(popup.GetComponent<Image>(), Is.Null);
                var backdrop = (RectTransform)popup.Find("DetailDrawer");
                Assert.That(backdrop, Is.Not.Null);
                Assert.That(backdrop.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(backdrop.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(((RectTransform)front.parent).anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(popup.GetComponent<UI_Popup_OwnerPlayerCard>().BlocksLowerInput, Is.True);
                Assert.That(front.Find("TrainingFill0"), Is.Not.Null);
                Assert.That(front.Find("SkillBlockFill0"), Is.Not.Null);
                Assert.That(front.Find("TeamColorFill0"), Is.Not.Null);
                Assert.That(front.Find("StudyFill0"), Is.Not.Null);
                Assert.That(front.Find("EnhancementFill0"), Is.Not.Null);
                Assert.That(front.Find("Value0").GetComponent<Text>().text, Is.EqualTo((79 + teamColor).ToString()));
                Assert.That(front.Find("GrowthValue0").GetComponent<Text>().text, Is.EqualTo("+" + (9 + teamColor)));
                Assert.That(front.Find("TeamColorFill3"), Is.Not.Null);
                Assert.That(front.Find("Value3").GetComponent<Text>().text, Is.EqualTo((79 + teamColor).ToString()));
                Assert.That(front.Find("GrowthValue3").GetComponent<Text>().text, Is.EqualTo("+" + (9 + teamColor)));
                Assert.That(card.GetBuntAbilityBreakdown().Value.TeamColor, Is.EqualTo(teamColor));
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [TestCase(55, 17, 140, 72)]
        [TestCase(55, 0, 140, 55)]
        [TestCase(98, 17, 100, 100)]
        public void 번트_자체보너스만적용하고표시상한을지킨다(
            int rating, int bonus, int maximum, int expectedTotal)
        {
            var breakdowns = new OwnerAbilityBreakdownSnapshot[PlayerAbilityCatalog.AbilityCount];
            breakdowns[(int)PlayerAbility.Contact] = new OwnerAbilityBreakdownSnapshot(90, 0, 0, 10, 0, 0);
            breakdowns[(int)PlayerAbility.BatterMental] = new OwnerAbilityBreakdownSnapshot(90, 0, 0, 10, 0, 0);
            breakdowns[(int)PlayerAbility.Bunt] = new OwnerAbilityBreakdownSnapshot(rating, 0, 0, bonus, 0, 0);
            var card = new OwnerCollectionCardSnapshot(
                "C-B", "P-B", "번트타자", 2025, PlayerPosition.Shortstop, 8,
                PlayerCardEdition.Normal, 0, 0, false, false, CreateAbilities(),
                abilityBreakdowns: breakdowns, abilityGraphMaximum: maximum);
            OwnerAbilityBreakdownSnapshot bunt = card.GetBuntAbilityBreakdown().Value;
            Assert.That(bunt.TeamColor, Is.EqualTo(bonus));
            Assert.That(bunt.Total, Is.EqualTo(rating + bonus));
            Assert.That(card.GetBuntAbility(), Is.EqualTo(expectedTotal));
        }

        private static OwnerCollectionCardSnapshot CreateHitter(string cardId, string displayName)
        {
            return new OwnerCollectionCardSnapshot(
                cardId, "P-" + cardId, displayName, 2025, PlayerPosition.Shortstop, 8,
                PlayerCardEdition.Normal, 0, 0, false, false, CreateAbilities());
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
