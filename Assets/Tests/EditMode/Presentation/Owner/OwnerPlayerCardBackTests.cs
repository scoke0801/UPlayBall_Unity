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
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Navigation_실행중같은프레임에이전카드를분리한다()
        {
            yield return new UnityEngine.TestTools.EnterPlayMode();
            var host = new GameObject("NavigationCanvas", typeof(RectTransform), typeof(Canvas));
            UI_Popup_OwnerPlayerCard view = null;
            try
            {
                UI_Popup_OwnerPlayerCard.Show(host.transform,
                    new[] { CreatePitcher(5), CreateHitter("H", "다음타자") }, 0);
                // 실행 중에는 공용 UIManager가 팝업을 자신의 레이어로 옮긴다.
                view = Object.FindFirstObjectByType<UI_Popup_OwnerPlayerCard>();
                Transform popup = view.transform;
                Transform front = popup.Find("CardDetail/Front");
                Transform back = popup.Find("CardDetail/Back");
                Transform oldFrame = front.Find("MainFrame");
                Transform oldDecoration = front.Find("CardDecoration");
                Transform oldRole = back.Find("RoleInformation");
                popup.Find("NextCard").GetComponent<Button>().onClick.Invoke();

                // 프레임을 넘기기 전에 검사해야 Destroy 지연으로 인한 재참조를 재현할 수 있다.
                Assert.That(oldFrame.parent, Is.Null);
                Assert.That(oldFrame.gameObject.activeSelf, Is.False);
                Assert.That(oldDecoration.parent, Is.Null);
                Assert.That(front.Find("CardDecoration"), Is.Not.Null);
                Assert.That(front.Find("CardDecoration"), Is.Not.SameAs(oldDecoration));
                Assert.That(oldRole.parent, Is.Null);
                Assert.That(front.Find("MainFrame"), Is.Not.SameAs(oldFrame));
                Assert.That(front.Find("Name").GetComponent<Text>().text, Is.EqualTo("다음타자"));
                Assert.That(front.Find("Enhancement"), Is.Null);
                Assert.That(back.Find("RoleInformation/PitchSlot0"), Is.Null);

                popup.Find("PreviousCard").GetComponent<Button>().onClick.Invoke();
                Assert.That(front.Find("Name").GetComponent<Text>().text, Is.EqualTo("가상투수"));
                Assert.That(front.Find("Enhancement"), Is.Not.Null);
                Assert.That(back.Find("RoleInformation/PitchSlot4"), Is.Not.Null);
                int frameCount = 0;
                foreach (Transform child in front)
                    if (child.name == "MainFrame") frameCount++;
                Assert.That(frameCount, Is.EqualTo(1));
            }
            finally
            {
                if (view != null) view.Close();
                Object.Destroy(host);
            }
            yield return null;
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }

        [TestCase(1280, 720, 5)]
        [TestCase(1920, 1080, 5)]
        [TestCase(2560, 1440, 6)]
        [TestCase(3440, 1440, 2)]
        [TestCase(1280, 720, 0, false)]
        [TestCase(1920, 1080, 0, false)]
        [TestCase(2560, 1440, 0, false)]
        [TestCase(3440, 1440, 0, false)]
        public void PitcherBack_구종과구속을해상도별로렌더링한다(int width, int height, int count, bool pitcher = true)
        {
            var host = new GameObject("BackCanvas", typeof(Canvas));
            var cameraObject = new GameObject("BackCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(25, 29, 36, 255);
                camera.targetTexture = target;
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                var card = new GameObject("Back", typeof(RectTransform)).GetComponent<RectTransform>();
                card.SetParent(host.transform, false);
                card.sizeDelta = new Vector2(400, 600);
                card.localScale = Vector3.one * (height * .9f / 600f);
                typeof(UI_Popup_OwnerPlayerCard).GetMethod("BuildReferenceBack",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { card, pitcher ? CreatePitcher(count, true) : CreateVisualSkillHitter(), pitcher });
                Canvas.ForceUpdateCanvases();
                // EditMode에서는 프레임이 흐르지 않으므로 동적 폰트 아틀라스 갱신 뒤 메시를 한 번 더 만든다.
                camera.Render();
                foreach (Text label in card.GetComponentsInChildren<Text>()) label.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                if (!pitcher)
                {
                    var field = card.Find("RoleInformation/DefenseDiagram/Field").GetComponent<UICardDefenseField>();
                    Assert.That(field.sprite, Is.Not.Null);
                    Assert.That(field.sprite.name, Is.EqualTo("PlayerCard_DefenseField"));
                    Assert.That(field.preserveAspect, Is.True);
                    Assert.That(field.raycastTarget, Is.False);
                    var corners = new Vector3[4];
                    field.rectTransform.GetWorldCorners(corners);
                    RectTransform area = (RectTransform)field.transform.parent;
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 local = area.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(area.rect.xMin - .1f, area.rect.xMax + .1f));
                        Assert.That(local.y, Is.InRange(area.rect.yMin - .1f, area.rect.yMax + .1f));
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    Transform slot = card.Find("RoleInformation/PitchSlot" + i);
                    foreach (string name in new[] { "PitchName", "Grade", "Velocity" })
                    {
                        Text label = slot.Find(name).GetComponent<Text>();
                        Assert.That(label.cachedTextGenerator.vertexCount, Is.GreaterThan(0), name);
                        Assert.That(label.cachedTextGenerator.lineCount, Is.EqualTo(1), name);
                    }
                    var rect = (RectTransform)slot;
                    Assert.That(rect.anchorMin.x, Is.GreaterThanOrEqualTo(0));
                    Assert.That(rect.anchorMin.y, Is.GreaterThanOrEqualTo(0));
                    Assert.That(rect.anchorMax.x, Is.LessThanOrEqualTo(1));
                    Assert.That(rect.anchorMax.y, Is.LessThanOrEqualTo(1));
                }
                camera.Render();
                string folder = System.Environment.GetEnvironmentVariable("BASEBALL_CARD_BACK_CAPTURE");
                if (!string.IsNullOrEmpty(folder))
                {
                    var previous = RenderTexture.active;
                    var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        image.Apply();
                        System.IO.Directory.CreateDirectory(folder);
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, (pitcher ? "" : "hitter-") + width + "x" + height + ".png"), image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

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

                Transform block = canvasObject.transform.Find(
                    "UI_Popup_OwnerPlayerCard/CardDetail/Back/SkillBoardInformation/Grid/PlacedBlock_0")
                    .transform;
                RawImage[] tiles = block.GetComponentsInChildren<RawImage>();
                Assert.That(tiles.Length, Is.EqualTo(4));
                string mark = rarity == SkillBlockRarity.Elite || rarity == SkillBlockRarity.Unique ||
                              rarity == SkillBlockRarity.Legendary ? "star" : "circle";
                foreach (RawImage tile in tiles)
                {
                    Assert.That(tile.texture, Is.Not.Null);
                    Assert.That(tile.texture.name, Is.EqualTo("skill_tile_" + mark + "_v3"));
                    Assert.That(tile.color, Is.EqualTo((Color)new Color32((byte)red, (byte)green, (byte)blue, 255)));
                    Assert.That(tile.raycastTarget, Is.False);
                    Assert.That(Mathf.DeltaAngle(tile.transform.localEulerAngles.z, -rotation * 90f),
                        Is.EqualTo(0f).Within(.01f));
                }
                Assert.That(Mathf.DeltaAngle(block.localEulerAngles.z, rotation * 90f),
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
                    CreateAbilities(), abilityBreakdowns: breakdowns, abilityGraphMaximum: AbilityRatings.Maximum);

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

        [TestCase(55, 17, 250, 72)]
        [TestCase(55, 0, 250, 55)]
        [TestCase(240, 17, 250, 250)]
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

        [TestCase(59, 41, .3933333f, .6666667f)]
        [TestCase(60, 60, .4f, .8f)]
        [TestCase(69, 91, .46f, .9f)]
        [TestCase(70, 130, .4666667f, 1f)]
        [TestCase(89, 161, .5933333f, 1f)]
        [TestCase(90, 30, .6f, .8f)]
        public void Detail_기본등급색과성장구간을유지하고게이지만압축한다(
            int baseStat, int bonus, float baseRatio, float totalRatio)
        {
            var host = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var source = new GameObject("Source", typeof(RectTransform));
            source.transform.SetParent(host.transform, false);
            try
            {
                var breakdowns = new OwnerAbilityBreakdownSnapshot[PlayerAbilityCatalog.AbilityCount];
                for (int i = 0; i < breakdowns.Length; i++)
                    breakdowns[i] = new OwnerAbilityBreakdownSnapshot(baseStat, bonus, 0, 0, 0, 0);
                var card = new OwnerCollectionCardSnapshot("Gauge", "GaugePlayer", "성장비교선수", 2025,
                    PlayerPosition.Shortstop, 8, PlayerCardEdition.Normal, 0, 0, false, false,
                    CreateAbilities(), abilityBreakdowns: breakdowns, abilityGraphMaximum: 250);
                UI_Popup_OwnerPlayerCard.Show(source.transform, card);
                Transform front = host.transform.Find("UI_Popup_OwnerPlayerCard/CardDetail/Front");
                var baseFill = (RectTransform)front.Find("BaseFill0");
                var bonusFill = (RectTransform)front.Find("TrainingFill0");
                Assert.That(baseFill.anchorMax.x, Is.EqualTo(.205f + .565f * baseRatio).Within(.00001f));
                Assert.That(bonusFill.anchorMin.x, Is.EqualTo(baseFill.anchorMax.x));
                Assert.That(bonusFill.anchorMax.x, Is.EqualTo(.205f + .565f * totalRatio).Within(.00001f));
                Text value = front.Find("Value0").GetComponent<Text>();
                Color expectedColor = baseStat >= 90 ? new Color32(255, 100, 100, 255)
                    : baseStat >= 70 ? new Color32(255, 166, 76, 255)
                    : baseStat >= 60 ? new Color32(255, 222, 92, 255) : Color.white;
                Assert.That(value.color, Is.EqualTo(expectedColor));
                Assert.That(value.text, Is.EqualTo((baseStat + bonus).ToString()));
                Assert.That(front.Find("GrowthValue0").GetComponent<Text>().text, Is.EqualTo("+" + bonus));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static OwnerCollectionCardSnapshot CreateVisualSkillHitter()
        {
            return new OwnerCollectionCardSnapshot(
                "FIELD", "P-FIELD", "수비필드", 2025, PlayerPosition.Shortstop, 8,
                PlayerCardEdition.Normal, 0, 0, false, false, CreateAbilities(),
                placedSkillBlockCount: 4,
                skillBlockPlacements: new[]
                {
                    new OwnerSkillBlockPlacementSnapshot(TetrominoShapeCatalog.CreateCells(TetrominoShape.I), 0, 0, 0, SkillBlockRarity.Rare),
                    new OwnerSkillBlockPlacementSnapshot(TetrominoShapeCatalog.CreateCells(TetrominoShape.O), 0, 1, 0, SkillBlockRarity.Unique),
                    new OwnerSkillBlockPlacementSnapshot(TetrominoShapeCatalog.CreateCells(TetrominoShape.O), 2, 1, 0, SkillBlockRarity.Normal),
                    new OwnerSkillBlockPlacementSnapshot(TetrominoShapeCatalog.CreateCells(TetrominoShape.I), 0, 3, 0, SkillBlockRarity.Legendary)
                });
        }

        private static OwnerCollectionCardSnapshot CreateHitter(string cardId, string displayName)
        {
            return new OwnerCollectionCardSnapshot(
                cardId, "P-" + cardId, displayName, 2025, PlayerPosition.Shortstop, 8,
                PlayerCardEdition.Normal, 0, 0, false, false, CreateAbilities());
        }

        private static OwnerCollectionCardSnapshot CreatePitcher(int pitchCount, bool usePitchNames = false)
        {
            var pitches = new List<OwnerPitchCardSnapshot>(pitchCount);
            string[] names = { "포심 패스트볼", "투심 패스트볼", "커터", "슬라이더", "커브", "체인지업" };
            for (int index = 0; index < pitchCount; index++)
                pitches.Add(new OwnerPitchCardSnapshot(
                    (PitchType)index, usePitchNames ? names[index] : "긴 구종 이름 " + index,
                    index == pitchCount - 1 ? "SS" : "B+", 151d - index * 4d));
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
