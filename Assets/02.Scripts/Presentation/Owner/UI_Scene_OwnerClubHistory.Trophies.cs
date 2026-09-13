using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerClubHistory
    {
        private const float TrophyRoomMaximumWidth = 1560f;
        private readonly Text[] _honorCounts = new Text[3];
        private readonly Text[] _honorHints = new Text[3];
        private readonly Text[,] _leagueHonors = new Text[3, 10];
        private readonly RawImage[] _trophyArt = new RawImage[3];
        private readonly Image[] _honorSelection = new Image[3];

        private void BuildTrophies(RectTransform root)
        {
            _trophies = OwnerRuntimeUiFactory.CreateRect("TrophyRoom", root);
            Place(_trophies, .03f, .27f, .97f, .70f);
            string[] artNames = { "pennant", "champion", "runner-up" };
            for (int i = 0; i < _honors.Length; i++)
            {
                int captured = i;
                // 공용 네이비 프레임 안에서 세 트로피의 전시 높이와 명판 위치를 통일한다.
                var button = Button("Honor" + i, _trophies, HonorNames[i], () =>
                { _honor = _honor == captured ? -1 : captured; Render(); });
                _honors[i] = button;
                Place((RectTransform)button.transform, i / 3f + .004f, 0f, (i + 1) / 3f - .004f, 1f);
                OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Secondary);
                var content = OwnerRuntimeUiFactory.CreateRect("ContentSafeRect", button.transform);
                OwnerRuntimeUiFactory.Stretch(content, Vector2.one * CareerUiTheme.Space4,
                    -Vector2.one * CareerUiTheme.Space4);
                var label = button.transform.Find("Label").GetComponent<Text>();
                label.transform.SetParent(content, false);
                label.fontSize = 18;
                Place(label.rectTransform, 0f, .91f, 1f, 1f);
                _honorCounts[i] = TrophyText("Count", content, 23, CareerUiTheme.Number);
                Place(_honorCounts[i].rectTransform, 0f, .81f, 1f, .91f);

                var artHost = OwnerRuntimeUiFactory.CreateRect("TrophySlot" + i, content);
                Place(artHost, .20f, .24f, .80f, .80f);
                var art = OwnerRuntimeUiFactory.CreateRect("TrophyArt", artHost);
                var raw = art.gameObject.AddComponent<RawImage>();
                raw.texture = Resources.Load<Texture2D>("UI/ClubHistory/" + artNames[i]);
                raw.raycastTarget = false;
                _trophyArt[i] = raw;
                var aspect = art.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = raw.texture == null ? 1f : raw.texture.width / (float)raw.texture.height;

                var shelf = OwnerRuntimeUiFactory.CreateImage("ShelfEdge", content, CareerUiTheme.ShellGold);
                Place(shelf.rectTransform, .08f, .225f, .92f, .23f);
                for (int grade = 0; grade < 10; grade++)
                {
                    var text = TrophyText("LeagueHonor" + grade, content, 12, CareerUiTheme.RosterTextSecondary);
                    int column = grade % 5;
                    float bottom = grade < 5 ? .14f : .07f;
                    Place(text.rectTransform, column / 5f, bottom, (column + 1) / 5f, bottom + .07f);
                    _leagueHonors[i, grade] = text;
                }
                _honorHints[i] = TrophyText("SelectionHint", content, 12, CareerUiTheme.RosterTextSecondary);
                Place(_honorHints[i].rectTransform, 0f, 0f, 1f, .07f);
                _honorSelection[i] = OwnerRuntimeUiFactory.CreateImage("SelectedIndicator", content, CareerUiTheme.Number);
                Place(_honorSelection[i].rectTransform, .20f, .995f, .80f, 1f);
            }
        }

        private void RenderTrophies()
        {
            for (int i = 0; i < _honors.Length; i++)
            {
                int count = _model.CountHonors(_grade, i);
                _honorCounts[i].text = (_grade.HasValue ? "리그 " : "통산 ") + count + "회";
                _honorCounts[i].color = count > 0 ? CareerUiTheme.Number : CareerUiTheme.RosterTextSecondary;
                _trophyArt[i].color = count > 0 ? Color.white : CareerUiTheme.TextMuted;
                _honorSelection[i].gameObject.SetActive(_honor == i);
                _honorHints[i].text = _honor == i ? "달성 시즌 표시 중 · 다시 눌러 전체 보기" :
                    count > 0 ? "달성 시즌 보기  ›" : "아직 획득하지 못한 타이틀";
                for (int grade = 0; grade < 10; grade++)
                {
                    int leagueCount = _model.CountHonors((LeagueGrade)grade, i);
                    var text = _leagueHonors[i, grade];
                    text.text = OwnerLeagueDisplayNameFormatter.FormatFull((LeagueGrade)grade).Replace(" 리그", "") + " " + leagueCount;
                    text.color = leagueCount > 0 && (!_grade.HasValue || (int)_grade.Value == grade)
                        ? CareerUiTheme.Number : CareerUiTheme.RosterTextSecondary;
                }
            }
        }

        private static Text TrophyText(string name, Transform parent, int size, Color color)
        {
            var text = OwnerRuntimeUiFactory.CreateText(name, parent, string.Empty, size, FontStyle.Normal,
                TextAnchor.MiddleCenter, color);
            text.color = color;
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            return text;
        }
    }
}
