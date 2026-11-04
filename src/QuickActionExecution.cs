using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneTaskBar
{
    internal sealed partial class ShellController
    {
        readonly HashSet<string> quickActionsPending=new HashSet<string>();
        internal bool QuickActionPending(string key)
        {
            return quickActionsPending.Contains(key);
        }
        // Quick Settings and desktop widgets use the same device/setting path.
        internal async Task InvokeQuickAction(string key)
        {
            if(disposed||quickActionsPending.Contains(key))return;
            quickActionsPending.Add(key);
            try
            {
                if(QuickActionCatalog.Commands.Contains(key))
                {
                    QuickActionCatalog.Invoke(key,this);
                    return;
                }
                var setting=QuickSettingCatalog.Find(key);
                if(setting!=null)
                {
                    Preferences.Set(p=>setting.Write(p,!setting.Read(p)));
                    return;
                }
                switch(key)
                {
                    case "mute": Audio.ToggleMute(); Audio.Read(); break;
                    case "theme": AppTheme.Toggle(); break;
                    case "efficiency": Toggles.ToggleEfficiency(); break;
                    case "dnd": await Toggles.Disturb.Toggle(); break;
                    case "awake": Toggles.SetAwake(!Toggles.Awake); break;
                    case "animations": Preferences.Set(p=>p.Animations=!p.Animations); break;
                    case "startup": Toggles.ToggleStartup(); break;
                    case "slider-scroll": Preferences.Set(p=>p.QuickSliderScroll=!p.QuickSliderScroll); break;
                    case "wifi":
                    case "bluetooth":
                        if(!await Services.ToggleRadio(key))throw new InvalidOperationException("Windows가 이 무선 장치의 제어를 허용하지 않았습니다.");
                        await Services.Refresh();
                        break;
                }
            }
            finally
            {
                quickActionsPending.Remove(key);
            }
        }
    }
}
