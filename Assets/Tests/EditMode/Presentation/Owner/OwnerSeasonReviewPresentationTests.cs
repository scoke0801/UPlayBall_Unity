using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class OwnerSeasonReviewPresentationTests
    {
        [Test]
        public void Bind_페넌트레이스부터포스트시즌과결산까지한팝업에서탐색한다()
        {
            var hostObject = new GameObject("PopupHost", typeof(RectTransform));
            UI_Popup_OwnerSeasonReview view = UI_Popup_OwnerSeasonReview.CreateRuntime(
                hostObject.GetComponent<RectTransform>());
            var snapshot = new OwnerSeasonReviewSnapshot(
                3, LeagueGrade.Rookie, LeagueGrade.Minor, "TEAM-A",
                2, 10, 87, 2, 55, 633, 588,
                true, true, true, OwnerTeamPostseasonResult.RunnerUp, "TEAM-B",
                new[]
                {
                    new OwnerPostseasonSeriesReview("championship", OwnerPostseasonRound.Championship,
                        "TEAM-A", "TEAM-B", 2, 3, 3, true)
                });

            view.Bind(snapshot, key => key == "TEAM-A" ? "서울 베어스" : "부산 마리너스", 0);
            view.Show();

            Text summary = view.transform.Find("SeasonReview/ResultHero/Summary").GetComponent<Text>();
            Button primary = view.transform.Find("SeasonReview/Primary").GetComponent<Button>();
            Assert.That(summary.text, Does.Contain("2위"));

            primary.onClick.Invoke();
            Assert.That(summary.text, Is.EqualTo("포스트시즌 준우승"));
            primary.onClick.Invoke();
            Assert.That(summary.text, Does.Contain("정규시즌 2위"));

            Object.DestroyImmediate(hostObject);
        }

        [Test]
        public void Bind_우리조만완료된경우남은월드포스트시즌을다시진행할수있다()
        {
            var hostObject = new GameObject("PopupHost", typeof(RectTransform));
            UI_Popup_OwnerSeasonReview view = UI_Popup_OwnerSeasonReview.CreateRuntime(
                hostObject.GetComponent<RectTransform>());
            var snapshot = new OwnerSeasonReviewSnapshot(
                1, LeagueGrade.Rookie, null, "TEAM-A",
                2, 10, 80, 62, 2, 615, 537,
                true, true, false, 1, 4, true,
                OwnerTeamPostseasonResult.Champion, "TEAM-A",
                new[]
                {
                    new OwnerPostseasonSeriesReview("championship", OwnerPostseasonRound.Championship,
                        "TEAM-A", "TEAM-B", 3, 1, 3, true)
                });
            bool requested = false;
            view.PostseasonRequested += () => requested = true;

            view.Bind(snapshot, key => key, 1);
            Button primary = view.transform.Find("SeasonReview/Primary").GetComponent<Button>();
            Text label = primary.transform.Find("Label").GetComponent<Text>();

            Assert.That(label.text, Is.EqualTo("남은 리그 마감"));
            primary.onClick.Invoke();
            Assert.That(requested, Is.True);

            Object.DestroyImmediate(hostObject);
        }
    }
}
