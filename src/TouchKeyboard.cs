using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OneTaskBar
{
    internal static class TouchKeyboard
    {
        // Explorer's explicit touch-keyboard toggle. Unlike InputPane.TryShow,
        // this entry point also works with a hardware keyboard attached.
        [ComImport,Guid("37C994E7-432B-4834-A2F7-DCE1F13B834B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ITipInvocation {void Toggle(IntPtr window);}
        static bool pending;

        static bool Toggle(IntPtr target)
        {
            object instance=null;
            try
            {
                instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("4CE576FA-83DC-4F88-951C-9D0782B4E376")));
                ((ITipInvocation)instance).Toggle(target);
                return true;
            }
            catch(COMException error){AppLog.Once("touch-keyboard",error.ErrorCode.ToString("X8"));return false;}
            finally{if(instance!=null&&Marshal.IsComObject(instance))Marshal.ReleaseComObject(instance);}
        }

        internal static async Task<bool> Open()
        {
            if(pending)return true;
            pending=true;
            try
            {
                var target=Native.GetForegroundWindow();
                if(Toggle(target))return true;
                string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),@"Microsoft Shared\ink\TabTip.exe");
                if(!File.Exists(path))return false;
                using(Process.Start(new ProcessStartInfo(path){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden})){}
                await Task.Delay(250);
                return Toggle(target);
            }
            catch(Exception error){AppLog.Once("touch-keyboard-launch",error.GetType().Name);return false;}
            finally{pending=false;}
        }
    }

    internal sealed partial class ShellController
    {
        internal async void OpenStatusSystemPanel(bool cast)
        {
            CloseTrayOverflow();CloseDesktopOverview();Previews.Close();
            foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer,Finder,StartMenu})if(panel!=null)panel.HideImmediately();
            bool opened=cast?await Task.Run(()=>SystemPanels.Cast()):await TouchKeyboard.Open();
            if(disposed||opened)return;
            ShowWelcome();
            Welcome.Feedback(cast?"캐스트를 열지 못했습니다. 잠시 후 다시 눌러 주세요.":"터치 키보드를 열지 못했습니다. Windows의 터치 키보드 설정을 확인해 주세요.");
        }
    }
}
