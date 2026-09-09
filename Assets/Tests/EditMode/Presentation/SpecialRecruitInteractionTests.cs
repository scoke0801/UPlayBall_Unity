using System;
using System.Linq;
using System.Reflection;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>실제 버튼 입력이 Game 거래를 확정하고 재료를 정확히 한 번 소비하는지 검증한다.</summary>
    public sealed class SpecialRecruitInteractionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SpecialCards_AutoSelectAndConfirmRecruitThroughView(bool legend)
        {
            // Game 테스트의 검증된 거래 Fixture를 재사용하여 표현 테스트에 별도 야구 월드를 만들지 않는다.
            Type fixture = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(
                "Baseball.Tests.EditMode.Game.Historical.ManagerHistoricalSaveTests")).First(t => t != null);
            object[] arguments = { null, null, legend };
            var runtime = (ManagerHistoricalRuntimeState)fixture.GetMethod("CreateSpecialCardRuntime",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
            string target = (string)arguments[0];
            string[] materials = (string[])arguments[1];
            var host = new GameObject("SpecialRecruitTest", typeof(RectTransform));
            var managerObject = new GameObject("RecruitManager");
            try
            {
                var manager = managerObject.AddComponent<OwnerModeManager>();
                typeof(OwnerModeManager).GetProperty("Runtime").SetValue(manager, runtime);
                var view = UI_Scene_OwnerSpecialRecruit.CreateRuntime((RectTransform)host.transform);
                view.ShowRoute(legend ? UI_Scene_OwnerSpecialRecruit.LegendRoute : UI_Scene_OwnerSpecialRecruit.CareerHighRoute);
                view.Bind(manager);
                var content = view.transform.Find("IssuedRecruitContent");
                var confirm = content.Find("ConfirmRecruit").GetComponent<Button>();
                Assert.That(confirm.interactable, Is.False);
                content.Find("AutoSelect").GetComponent<Button>().onClick.Invoke();
                Assert.That(confirm.interactable, Is.True);
                confirm.onClick.Invoke();
                Assert.That(runtime.TryGetOwnedCard(target, out _), Is.False, "최종 확인 전에는 소비하지 않는다.");
                confirm.onClick.Invoke();
                Assert.That(runtime.TryGetOwnedCard(target, out var acquired), Is.True);
                Assert.That(acquired.IsLocked, Is.True);
                Assert.That(materials.All(id => !runtime.TryGetOwnedCard(id, out _)), Is.True);
                Assert.That(confirm.interactable, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }
    }
}
