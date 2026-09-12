using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class OwnerDevelopmentPresentationTests
    {
        [TestCase(1280,720,0)]
        [TestCase(1920,1080,1)]
        [TestCase(2560,1440,2)]
        [TestCase(3440,1440,3)]
        [TestCase(1920,1080,4)]
        [TestCase(1920,1080,5)]
        public void 실제성장메뉴를렌더링한다(int width, int height, int tab)
        {
            var root = new GameObject("DevelopmentCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var events = new GameObject("Events", typeof(EventSystem));
            var render = new RenderTexture(width,height,24);
            var previous = RenderTexture.active; Texture2D image = null;
            try
            {
                var manager = root.AddComponent<OwnerModeManager>();
                var type = Assembly.Load("Baseball.Game.Tests").GetType("Baseball.Tests.EditMode.Game.Historical.ManagerHistoricalSaveTests").GetNestedType("Fixture",BindingFlags.NonPublic);
                var fixture = type.GetMethod("Create").Invoke(null,new object[]{WorldRecordMode.SimulatedHistory,false});
                var adapter = (ManagerHistoricalSaveAdapter)fixture.GetType().GetMethod("CreateAdapter").Invoke(fixture,null);
                var runtime = adapter.Restore(adapter.CreateSaveData((ManagerHistoricalRuntimeState)fixture.GetType().GetProperty("State").GetValue(fixture)));
                foreach(var group in runtime.LeagueWorld.Groups) foreach(var game in group.Season.Schedule.Games) if(!game.IsCompleted) game.Complete(1,0);
                typeof(ManagerHistoricalRuntimeState).GetProperty("LeagueWorld").SetValue(runtime,null);
                typeof(OwnerModeManager).GetProperty("Runtime").SetValue(manager,runtime);
                var common=BalanceTable.CreateDefault(); var development=manager.GetDevelopmentBalance();
                var balance=new BalanceTable(common.Version,common.PlateDiscipline,common.BattedBall,common.BaseRunning,
                    common.ContractOffer,common.TeamGeneration,common.PlayerEvaluation,common.CareerSeason,
                    growth:OwnerSkillContent.Compose(common.Growth,development.skillSetBonus),ownerCardGrowth:development.ApplyStudyTiers(common.OwnerCardGrowth));
                typeof(OwnerModeManager).GetField("_balance",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,balance);
                typeof(OwnerModeManager).GetField("_contentProvider",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,fixture.GetType().GetProperty("Provider").GetValue(fixture));
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.targetTexture=render;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.06f,.09f,.14f);
                var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
                var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
                var workspace = new GameObject("Workspace",typeof(RectTransform)).GetComponent<RectTransform>(); workspace.SetParent(root.transform,false);
                workspace.anchorMin=new Vector2(.015f,.08f); workspace.anchorMax=new Vector2(.985f,.86f); workspace.offsetMin=workspace.offsetMax=Vector2.zero;
                Canvas.ForceUpdateCanvases(); Transform view;
                if(tab==5)
                {
                    var catalog=manager.GetSupportCatalog();
                    OwnerSupportService.Purchase(runtime,catalog[0]); OwnerSupportService.Equip(runtime,catalog[0],"");
                    var targets=OwnerSupportService.ResolveTargets(runtime,catalog[0],"");
                    for(int i=0;i<3;i++) { OwnerSupportService.Purchase(runtime,catalog[3]); OwnerSupportService.Equip(runtime,catalog[3],targets[i].CardId); }
                    var support=UI_Scene_OwnerSupportCards.CreateRuntime(workspace); support.Bind(manager); view=support.transform;
                }
                else { var popup=UI_Popup_OwnerDevelopment.Show(workspace,manager); view=popup.transform; popup.GetComponentsInChildren<Button>().Single(b=>b.name=="Tab"+tab).onClick.Invoke(); }
                Canvas.ForceUpdateCanvases(); Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active=render;
                image=new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                string output=Environment.GetEnvironmentVariable("BASEBALL_OFFSEASON_VISUAL_OUTPUT");
                if(!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output,$"development-{tab}-{width}x{height}.png"),image.EncodeToPNG()); }
                foreach(var text in view.GetComponentsInChildren<Text>())
                    Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2),text.name+": "+text.text);
                Assert.That(view.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("읽지 못")),Is.False,"실제 데이터 조회");
                Assert.That(view.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Identity")),Is.False,"Identity 리소스 연결");
                if(tab==2)
                {
                    long money=runtime.Economy.Money;
                    var confirm=view.GetComponentsInChildren<Button>().Single(b=>b.name=="Confirm");
                    Assert.That(confirm.interactable,Is.True);
                    confirm.onClick.Invoke();
                    Assert.That(view.GetComponentsInChildren<Button>().Any(b=>b.name=="Cancel"),Is.True);
                    view.GetComponent<UI_Popup_OwnerDevelopment>().TryHandleCancel();
                    Assert.That(runtime.Economy.Money,Is.EqualTo(money),"미리보기 취소는 자원을 소비하지 않는다.");
                }
            }
            finally
            {
                RenderTexture.active=previous;
                if(image!=null) Object.DestroyImmediate(image);
                Object.DestroyImmediate(root); Object.DestroyImmediate(events); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(render);
            }
        }
    }
}
