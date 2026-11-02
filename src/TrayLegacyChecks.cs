using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OneTaskBar
{
    internal static class TrayLegacyChecks
    {
        sealed class Fixture : System.Windows.Forms.Form
        {
            protected override bool ShowWithoutActivation {get {return true;}}
        }

        internal static async Task Run()
        {
            using(var window=new Fixture {ShowInTaskbar=false,Width=350,Height=100})
            using(var toolbar=new System.Windows.Forms.ToolBar {ShowToolTips=true,ButtonSize=new System.Drawing.Size(110,30)})
            {
                var first=new System.Windows.Forms.ToolBarButton {Text="Legacy tray",ToolTipText="Legacy tray"};
                toolbar.Buttons.Add(first);
                toolbar.Buttons.Add(new System.Windows.Forms.ToolBarButton {Text="Hidden",Visible=false});
                window.Controls.Add(toolbar);
                int invoked=0;
                toolbar.ButtonClick+=(sender,args)=>invoked++;
                window.Show();
                await Task.Delay(70);
                var handle=toolbar.Handle;
                var facts=new System.Collections.Generic.List<object>();
                var buttons=await Task.Run(()=>TrayLegacyButton.ReadToolbar(handle,facts));
                AppLog.Json("tray-legacy-fixture.json",new {count=buttons.Count,buttons=buttons.Select(item=>item.InitialName).ToArray(),facts=facts});
                Require(buttons.Count==1&&buttons[0].InitialName=="Legacy tray", "classic toolbar exposes only visible named buttons");
                string name;
                Rect bounds;
                Require(buttons[0].Read(out name,out bounds)&&bounds.Width>0, "button state and physical bounds survive the loader thread");
                Require(!buttons[0].Invoke("Removed icon",false)&&invoked==0, "changed identities cannot invoke a different app");
                Require(buttons[0].Invoke(name,false), "legacy default action is available");
                await Task.Delay(70);
                Require(invoked==1, "legacy activation reaches the actual toolbar button once");
                toolbar.Dispose();
                Require(!buttons[0].Read(out name,out bounds), "closed source windows are harmless");
            }
        }

        static void Require(bool value,string message)
        {
            if(!value)throw new InvalidOperationException("Windows 10 tray: "+message);
        }
    }
}
