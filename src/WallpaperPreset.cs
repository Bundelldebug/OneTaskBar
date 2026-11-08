using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OneTaskBar
{
    // One saved pair: a desktop wallpaper and a lock screen picture, applied together on request.
    internal static class WallpaperPreset
    {
        [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SystemParametersInfoW")]
        static extern bool GetInfo(int action,int length,StringBuilder result,int flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SystemParametersInfoW")]
        static extern bool SetInfo(int action,int length,string value,int flags);
        internal static bool Saved {get{return !String.IsNullOrEmpty(Preferences.Current.PresetDesktopWallpaper)||!String.IsNullOrEmpty(Preferences.Current.PresetLockScreen);}}
        internal static string CurrentDesktop()
        {
            var buffer=new StringBuilder(32768);
            return GetInfo(0x73,buffer.Capacity,buffer,0)?buffer.ToString():null;
        }
        internal static bool Usable(string file){return !String.IsNullOrWhiteSpace(file)&&Path.IsPathRooted(file)&&File.Exists(file);}
        // Returns a short result for the settings window.
        internal static async Task<string> Apply()
        {
            string desktop=Preferences.Current.PresetDesktopWallpaper,lockScreen=Preferences.Current.PresetLockScreen;
            if(!Saved)return "저장한 프리셋이 없습니다";
            string result="";
            if(!String.IsNullOrEmpty(desktop))
            {
                if(!Usable(desktop))result+="배경화면 파일을 찾을 수 없습니다. ";
                else if(!SetInfo(0x14,0,desktop,3))result+="배경화면을 바꾸지 못했습니다. ";
            }
            if(!String.IsNullOrEmpty(lockScreen))
            {
                if(!Usable(lockScreen))result+="잠금화면 파일을 찾을 수 없습니다. ";
                else
                {
                    try
                    {
                        var file=await SystemServices.Wait(Windows.Storage.StorageFile.GetFileFromPathAsync(lockScreen));
                        var done=new TaskCompletionSource<bool>();
                        var action=Windows.System.UserProfile.LockScreen.SetImageFileAsync(file);
                        action.Completed=(info,status)=>
                        {
                            if(status==Windows.Foundation.AsyncStatus.Completed)done.TrySetResult(true);
                            else done.TrySetException(new InvalidOperationException("lock screen "+status));
                        };
                        await done.Task;
                    }
                    catch(Exception error){AppLog.Once("lock-screen-preset",error.GetType().Name);result+="잠금화면을 바꾸지 못했습니다. ";}
                }
            }
            return result.Length==0?"프리셋을 적용했습니다":result.Trim();
        }
    }
}
