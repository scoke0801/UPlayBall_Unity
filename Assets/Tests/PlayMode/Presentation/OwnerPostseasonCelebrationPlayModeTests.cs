using System.Collections;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Career;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baseball.Tests.PlayMode.Presentation
{
    /// <summary>실제 프레임에서 일시정지·취소·재개방의 연출 수명을 검증한다.</summary>
    public sealed class OwnerPostseasonCelebrationPlayModeTests
    {
        [UnityTest]
        public IEnumerator 우승연출은일시정지중에도완료되고취소와재개방에잔여동작이없다()
        {
            var host = new GameObject("CeremonyTest", typeof(RectTransform), typeof(Canvas));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            GameObject eventsObject = EventSystem.current == null ? new GameObject("CeremonyEvents", typeof(EventSystem)) : null;
            var oldMode = CareerPresentationSettings.Mode;
            float oldTimeScale = Time.timeScale;
            try
            {
                CareerPresentationSettings.Mode = CareerPresentationMode.Full;
                Time.timeScale = 0f;
                var before = Snapshot(2, false);
                var result = OwnerPostseasonCelebration.Create(before, Snapshot(3, true));
                var view = UI_Popup_OwnerPostseasonCelebration.CreateRuntime(host.GetComponent<RectTransform>());
                int continued = 0, records = 0;
                view.ContinueRequested += () => { continued++; view.Hide(); };
                view.RecordsRequested += () => { records++; view.Hide(); };
                view.Show(result, key => "테스트 구단");
                yield return null;
                Assert.That(view.IsAnimating, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Continue"));
                view.OnCancel(new BaseEventData(EventSystem.current));
                Assert.That(view.IsAnimating, Is.False);
                Assert.That(records, Is.Zero);
                view.OnCancel(new BaseEventData(EventSystem.current));
                Assert.That(records, Is.EqualTo(1));
                view.Show(result, key => "테스트 구단");
                yield return new WaitForSecondsRealtime(OwnerPostseasonPresentationData.Load().championshipDuration + 0.5f);
                Assert.That(view.IsAnimating, Is.False, "Time.timeScale이 0이어도 연출은 끝나야 한다.");
                foreach (CanvasGroup group in view.GetComponentsInChildren<CanvasGroup>()) Assert.That(group.alpha, Is.EqualTo(1f));
                view.transform.Find("Celebration/Continue").GetComponent<Button>().onClick.Invoke();
                Assert.That(continued, Is.EqualTo(1));
                view.Show(result, key => "테스트 구단");
                host.SetActive(false);
                yield return null;
                Assert.That(view.IsAnimating, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = oldTimeScale;
                CareerPresentationSettings.Mode = oldMode;
                Object.Destroy(host);
                if (eventsObject != null) Object.Destroy(eventsObject);
            }
        }

        private static OwnerSeasonReviewSnapshot Snapshot(int wins, bool completed)
        {
            return new OwnerSeasonReviewSnapshot(1, LeagueGrade.Rookie, null, "A", 1, 4,
                80, 60, 4, 700, 650, true, completed, true, null, null,
                new[] { new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship,
                    "A", "B", wins, 1, 3, completed) });
        }
    }
}
