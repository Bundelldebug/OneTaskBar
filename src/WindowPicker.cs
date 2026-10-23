using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class WindowPicker
    {
        internal static ContextMenu Menu(AppEntry entry,ShellController shell,bool windowsOnly=false,bool allowAdmin=false)
        {
            var menu=new ContextMenu {MinWidth=220,MaxWidth=380};
            foreach(var h in entry.Windows.Where(Native.IsWindow)) {
                var title=new StringBuilder(512);Native.GetWindowText(h,title,title.Capacity);var handle=h;
                var titleText=new TextBlock {Text=title.ToString(),TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=290};
                var item=new MenuItem {Header=titleText,ToolTip=title.ToString(),Icon=entry.Icon==null?(object)new Glyph("window",18){Color=Paint.Ink}:AppIcons.Visual(entry.Icon,18)}; item.Click+=(s,e)=>Native.Activate(handle);menu.Items.Add(item);
            }
            if(!windowsOnly) {
                if(menu.Items.Count>0)menu.Items.Add(new Separator());
                var launch=new MenuItem {Header="새로 열기",Icon=new Glyph("window",18){Color=Paint.Ink}};launch.Click+=(s,e)=> {var copy=entry.Copy();copy.Windows.Clear();copy.Launch();};menu.Items.Add(launch);
                if(allowAdmin){bool supported=AdminLaunch.Prepare(entry)!=null;var admin=new MenuItem {Header="관리자 권한으로 실행",Icon=new Glyph("shield",18){Color=Paint.Ink},IsEnabled=supported,ToolTip=supported?"Windows 관리자 권한으로 새 프로세스를 엽니다":(entry.AppID??"").Contains("!")?"이 Windows 패키지 앱은 관리자 권한 실행을 지원하지 않습니다":"이 앱의 실행 파일이나 바로가기를 찾지 못했습니다"};admin.Click+=(s,e)=>AdminLaunch.Start(entry,shell);menu.Items.Add(admin);var remove=new MenuItem {Header="앱 제거…",Icon=new Glyph("trash",18){Color=Paint.Ink},ToolTip="Windows의 앱 제거 화면 열기"};remove.Click+=(s,e)=>AppRemoval.Open(entry,shell);menu.Items.Add(remove);}
                var startPin=new MenuItem {Header=shell.StartPins.Contains(entry)?"시작 화면에서 고정 해제":"시작 화면에 고정",Icon=new Glyph("pin",18){Color=Paint.Ink}};startPin.Click+=(s,e)=>shell.PinStart(entry,!shell.StartPins.Contains(entry));menu.Items.Add(startPin);
                var pin=new MenuItem {Header=shell.Pins.Any(a=>Catalog.Same(a,entry))?"독에서 고정 해제":"독에 고정",Icon=new Glyph("pin",18){Color=Paint.Ink}};pin.Click+=(s,e)=>shell.Pin(entry,!shell.Pins.Any(a=>Catalog.Same(a,entry)));menu.Items.Add(pin);
                menu.Opened+=(s,e)=> {startPin.Header=shell.StartPins.Contains(entry)?"시작 화면에서 고정 해제":"시작 화면에 고정";pin.Header=shell.Pins.Any(a=>Catalog.Same(a,entry))?"독에서 고정 해제":"독에 고정";};
                var taskManager=new MenuItem {Header="작업 관리자 열기",Icon=new Glyph("window",18){Color=Paint.Ink}};taskManager.Click+=(s,e)=>shell.OpenTaskManager();menu.Items.Add(taskManager);
                if(entry.Windows.Count>0) {var close=new MenuItem {Header="창 닫기",Foreground=Paint.Brush("#FF9A3025"),Icon=new Glyph("close",18) {Color=Paint.Brush("#FF9A3025")}};close.Click+=(s,e)=> {foreach(var h in entry.Windows) Native.PostMessage(h,0x10,IntPtr.Zero,IntPtr.Zero);};menu.Items.Add(close);}
            }
            return menu;
        }
        internal static void ShowFor(AppEntry entry,FrameworkElement anchor,ShellController shell)
        {
            var menu=Menu(entry,shell,true);menu.PlacementTarget=anchor;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Top;menu.IsOpen=true;
        }
    }
}
