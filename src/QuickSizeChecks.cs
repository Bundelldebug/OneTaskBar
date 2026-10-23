using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class QuickSizeChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Quick size: "+message);}
        internal static void Validate(List<string> checks)
        {
            var json=new JavaScriptSerializer();var legacy=json.Deserialize<PreferenceData>("{}");Require(QuickToggleSize.Columns(legacy)==4&&QuickToggleSize.Rows(legacy)==1,"existing settings keep the original four-toggle row");
            var resized=json.Deserialize<PreferenceData>("{\"QuickToggleWidthPercent\":75,\"QuickToggleCollapsedHeight\":124}");Require(QuickToggleSize.Columns(resized)==3&&QuickToggleSize.Rows(resized)==2,"previous width and height migrate to the nearest grid");
            foreach(int columns in new[]{-1,0,1,2,3,4,200})foreach(int rows in new[]{-1,1,2,3,4,5,400})foreach(double available in new[]{0.0,180,250,344,600}){double result=QuickToggleSize.Width(available,columns);Require(result>=0&&result<=available&&QuickToggleSize.Height(rows)>=62&&QuickToggleSize.Height(rows)<=310,"bounded grid dimensions");}
            Require(QuickToggleSize.SnapColumns(249,400,2)==2&&QuickToggleSize.SnapColumns(261,400,2)==3&&QuickToggleSize.SnapColumns(249,400,3)==3&&QuickToggleSize.SnapColumns(239,400,3)==2,"resize boundary dead band");
            Require(QuickToggleSize.SnapRows(105,1)==2&&QuickToggleSize.SnapRows(92,2)==2&&QuickToggleSize.SnapRows(85,2)==1,"independent row snapping");
            checks.Add("Corner-resized quick toggles allow all 20 combinations up to 4 columns × 5 rows, retain legacy settings and resist boundary jitter");
        }
        static void Invoke(System.Windows.Controls.Button button){var peer=new ButtonAutomationPeer(button);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();}
        static bool Hits(FrameworkElement root,FrameworkElement element,Point point)
        {
            var result=VisualTreeHelper.HitTest(root,element.TranslatePoint(point,root));for(DependencyObject node=result==null?null:result.VisualHit;node!=null;node=VisualTreeHelper.GetParent(node))if(node==element)return true;return false;
        }
        internal static int Run(bool inspect=false)
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;QuickWindow quick=null,again=null;
            int columns=Preferences.Current.QuickToggleColumns,rows=Preferences.Current.QuickToggleRows,width=Preferences.Current.QuickToggleWidthPercent,height=Preferences.Current.QuickToggleCollapsedHeight;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Directory.CreateDirectory(AppLog.Data);Program.Inspectable=true;Validate(new List<string>());shell=new ShellController(true);quick=new QuickWindow(shell);var screen=System.Windows.Forms.Screen.PrimaryScreen;quick.ShowAt(screen.Bounds,30,30,380,780);quick.Refresh(shell.Power,60,30,false,true,true);quick.SetCollapsedGrid(4,1);await Task.Delay(100);double fullWidth=quick.ActionWidth;
                    var shapes=new List<object>();for(int c=1;c<=4;c++)for(int r=1;r<=5;r++){quick.SetCollapsedGrid(c,r);await Task.Delay(50);bool fit=Math.Abs(quick.ActionHeight-r*62)<.1&&Math.Abs(quick.ActionWidth/fullWidth-c/4.0)<.005&&quick.VisibleActions==Math.Min(10,c*r)&&quick.ActionsFit&&quick.LayoutFits()&&!quick.ResizeGrip.IsVisible;if(!fit){AppLog.Json("quick-size-failed.json",new {fullWidth=fullWidth,layout=quick.LayoutSnapshot(),buttons=quick.ActionBounds()});Program.Save(quick.Surface,380,780,Path.Combine(AppLog.Data,"quick-size-failed.png"));}Require(fit,"grid "+c+"x"+r);if((c==1&&r==3)||(c==2&&r==2)||(c==3&&r==4)||(c==4&&r==5))Program.Save(quick.Surface,380,780,Path.Combine(AppLog.Data,"quick-grid-"+c+"x"+r+".png"));shapes.Add(quick.LayoutSnapshot());}
                    quick.SetCollapsedGrid(1,3);quick.SetExpanded(true);await Task.Delay(370);bool expanded=Math.Abs(quick.ActionWidth-fullWidth)<1&&quick.ActionHeight==232&&quick.ActionsFit&&quick.VisibleActions==10;Require(expanded,"all toggles expand at full width from a vertical group");quick.SetExpanded(false);await Task.Delay(220);Require(Math.Abs(quick.ActionWidth/fullWidth-.25)<.005&&quick.ActionHeight==186&&quick.VisibleActions==3&&quick.ActionsFit,"folding restores the vertical group");
                    quick.SetCollapsedGrid(4,1);quick.EditLayout(true);await Task.Delay(80);double editWidth=quick.ActionWidth;bool editor=quick.ResizeGrip.IsVisible&&Hits(quick.Surface,quick.ResizeGrip,new Point(20,20))&&Hits(quick.Surface,quick.ActionEditor,new Point(quick.ActionEditor.ActualWidth/2,quick.ActionEditor.ActualHeight/2));Require(editor,"corner handle and center edit button accept hits");
                    quick.BeginToggleResize();quick.ResizeToggleBy(-editWidth/2,62);await Task.Delay(70);bool dragged=QuickToggleSize.Columns(Preferences.Current)==2&&QuickToggleSize.Rows(Preferences.Current)==2&&Math.Abs(quick.ActionWidth-editWidth/2)<1&&quick.ActionHeight==124&&quick.VisibleActions==4&&quick.ActionsFit&&quick.LayoutFits();Require(dragged,"corner resize changes both grid axes");quick.FinishToggleResize(false);
                    quick.BeginToggleResize();quick.ResizeToggleBy(1000,1000);await Task.Delay(50);Require(quick.VisibleActions==10&&quick.ActionHeight==310,"drag clamps at four columns and five rows");quick.FinishToggleResize(true);await Task.Delay(50);bool cancelled=QuickToggleSize.Columns(Preferences.Current)==2&&QuickToggleSize.Rows(Preferences.Current)==2&&quick.ActionHeight==124;Require(cancelled,"cancel restores the previous shape");
                    var source=PresentationSource.FromVisual(quick.ResizeGrip);quick.ResizeGrip.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.Down){RoutedEvent=Keyboard.KeyDownEvent});await Task.Delay(60);bool keyboard=QuickToggleSize.Rows(Preferences.Current)==3&&quick.ActionHeight==186;Require(keyboard,"keyboard arrow resizes one row");
                    Invoke(quick.ActionEditor);await Task.Delay(80);bool reorder=quick.VisibleActions==10&&quick.ActionHeight==232&&!quick.ResizeGrip.IsVisible&&quick.ActionsFit;Require(reorder,"center Edit opens the full reorder grid");quick.EditActions(false);quick.SetCollapsedGrid(2,2);await Task.Delay(80);Program.Save(quick.Surface,380,780,Path.Combine(AppLog.Data,"quick-size-editor.png"));
                    quick.EditLayout(false);await Task.Delay(60);quick.Close();quick=null;var saved=new JavaScriptSerializer().Deserialize<PreferenceData>(File.ReadAllText(Path.Combine(AppLog.Data,"settings.json")));again=new QuickWindow(shell);again.ShowAt(screen.Bounds,30,30,380,780);await Task.Delay(100);bool persist=saved.QuickToggleColumns==2&&saved.QuickToggleRows==2&&again.ActionHeight==124&&Math.Abs(again.ActionWidth/fullWidth-.5)<.005&&again.VisibleActions==4&&again.ActionsFit;Require(persist,"new panel and persisted settings retain both grid axes");
                    AppLog.Json("quick-size-probe.json",new {passed=true,referenceShapes=shapes,cornerHandleHitTest=editor,cornerDragChangesBothAxes=dragged,cancelRestoresShape=cancelled,keyboardResizing=keyboard,fullToggleReorderGrid=reorder,fullExpandedGrid=expanded,settingsPersistAcrossPanels=persist,noClippedButtons=true});result=0;
                    if(inspect){again.Title="One TaskBar · 기능 토글 크기 검사";again.ShowInTaskbar=true;again.EditLayout(true);var window=new System.Windows.Interop.WindowInteropHelper(again).Handle;Native.SetWindowLong(window,-20,(Native.GetWindowLong(window,-20)&~0x80)|0x40000);var closed=new TaskCompletionSource<bool>();again.Closed+=(s,e)=>{again=null;closed.TrySetResult(true);};await Task.WhenAny(closed.Task,Task.Delay(180000));AppLog.Json("quick-size-input.json",new {columns=QuickToggleSize.Columns(Preferences.Current),rows=QuickToggleSize.Rows(Preferences.Current)});}
                }catch(Exception error){AppLog.Json("quick-size-probe.json",new {passed=false,error=error.ToString()});}finally{if(quick!=null)quick.Close();if(again!=null)again.Close();Preferences.Set(p=>{p.QuickToggleColumns=columns;p.QuickToggleRows=rows;p.QuickToggleWidthPercent=width;p.QuickToggleCollapsedHeight=height;});if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
