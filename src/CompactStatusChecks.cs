using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class CompactStatusChecks
    {
        static void Require(bool value,string text){if(!value)throw new InvalidOperationException("Compact status: "+text);}
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject{var own=root as T;if(own!=null)yield return own;foreach(object item in LogicalTreeHelper.GetChildren(root)){var node=item as DependencyObject;if(node!=null)foreach(var child in Children<T>(node))yield return child;}}
        internal static void Validate(List<string> checks)
        {
            Require(!new PreferenceData().SliderValueNumbers,"slider numbers default off");var serializer=new JavaScriptSerializer();foreach(bool enabled in new[]{false,true}){var settings=serializer.Deserialize<PreferenceData>(serializer.Serialize(new PreferenceData {DuoStatusIcon=enabled,SliderValueNumbers=enabled}));Require(settings.DuoStatusIcon==enabled&&settings.SliderValueNumbers==enabled,"both independent options round-trip");}
            foreach(double volume in new[]{0.0,1,25,26,50,51,75,76,100}){int expected=volume<=0?0:volume<=25?1:volume<=50?2:volume<=75?3:4;Require(DuoStatusGlyph.Dots(volume,false,true)==expected&&DuoStatusGlyph.Dots(volume,true,true)==0&&DuoStatusGlyph.Dots(volume,false,false)==0,"volume dots reflect endpoints, mute and missing devices");}
            Require(DuoStatusGlyph.Dots(Double.NaN,false,true)==0&&DuoStatusGlyph.Dots(300,false,true)==4,"invalid volume is bounded");Require(DockGeometry.SideStatusWidth==44&&DockGeometry.SideStatusLength(58)==128&&DockGeometry.SideStatusLength(0)==106,"side status remains narrow with or without power data");var date=new DateTime(2026,10,26,13,52,2);Require(DockGeometry.ClockLabel(date,true,true).Split('\n').Length==3&&DockGeometry.ClockLabel(date,true,false).Contains("2026-10-26"),"compact clock fits three lines while horizontal clock keeps the year");
            for(int i=0;i<4;i++){var dot=DuoStatusGlyph.VolumeDot(i);Require(Math.Abs((dot-new Point(16,14.5)).Length-12.7)<.001,"volume dots follow the battery arc curvature");}Require(DuoStatusGlyph.VolumeDot(1).Y>DuoStatusGlyph.VolumeDot(0).Y&&DuoStatusGlyph.VolumeDot(2).Y>DuoStatusGlyph.VolumeDot(3).Y,"center dots are lower than the outer dots");
            checks.Add("Optional Duo battery/network/volume icon, compact side status and default-off slider numbers persist and respect state boundaries");
        }
        static void Render(FrameworkElement element,double width,double height){element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*2),(int)Math.Ceiling(height*2),192,192,PixelFormats.Pbgra32);bitmap.Render(element);}
        static void Numbers()
        {
            int ui=Preferences.Current.UIVersion;bool old=Preferences.Current.SliderValueNumbers;try{
                foreach(int version in ReleaseFeatures.GlassUI?new[]{85,90,95}:new[]{85,90})foreach(bool dark in new[]{false,true})using(Glass95.WithDarkForFixture(dark)){
                    Preferences.Current.UIVersion=version;foreach(bool enabled in new[]{false,true}){Preferences.Current.SliderValueNumbers=enabled;foreach(string kind in new[]{"volume","brightness","mute"})foreach(var size in new[]{new Size(300,60),new Size(60,60),new Size(60,300)})foreach(double value in new[]{0.0,50,100}){
                        var slider=new PillSlider(kind){Width=size.Width,Height=size.Height,MinWidth=0,Value=value,Vertical=size.Height>size.Width,LinearFill=true,Light=true,AnimateValue=false};Render(slider,size.Width,size.Height);Require(slider.ValueNumberVisible==enabled,"numbers follow the option in every UI");Require(enabled?new Rect(new Point(),size).Contains(slider.ValueNumberBounds):slider.ValueNumberBounds.IsEmpty,"value text stays inside long, square and vertical controls");Require(slider.Value==value,"drawing numbers cannot change the value");
                    }}
                    Preferences.Current.SliderValueNumbers=true;var progress=new PillSlider("progress"){Width=280,Height=26,Slim=true};Render(progress,280,26);Require(!progress.ValueNumberVisible,"media seek bars are excluded");var disabled=new PillSlider("brightness"){Width=280,Height=60,IsEnabled=false,MinWidth=0};Render(disabled,280,60);Require(disabled.ValueNumberText=="–","unavailable brightness does not claim zero");
                }
                Preferences.Current.UIVersion=ReleaseFeatures.GlassUI?95:90;Preferences.Current.SliderValueNumbers=true;var gallery=new StackPanel {Background=Paint.Brush(Glass95.Dark?"#FF333D48":"#FFD4DDE5")};foreach(string kind in new[]{"volume","brightness"})gallery.Children.Add(new PillSlider(kind){Width=300,Height=60,Margin=new Thickness(8),MinWidth=0,Value=kind=="volume"?43:78,Light=true,LinearFill=true,AnimateValue=false});Program.Save(gallery,316,152,Path.Combine(AppLog.Data,"slider-value-numbers.png"));
            }finally{Preferences.Current.UIVersion=ui;Preferences.Current.SliderValueNumbers=old;}
        }
        internal static async Task Run(ShellController shell,System.Windows.Forms.Screen screen)
        {
            bool oldDuo=Preferences.Current.DuoStatusIcon,oldNumbers=Preferences.Current.SliderValueNumbers,oldDesktop=shell.Desktop;int oldEdge=Preferences.Current.DockEdge;var oldMedia=shell.Services.Media;var oldPower=shell.Power;DockHost dock=null;
            try{Numbers();shell.Desktop=true;shell.Power=new PowerState {Known=true,Level=87,Connected=false};shell.Services.Media=new MediaState {Available=true,Active=true,Playing=true,Title="Side status fixture",Artist="One TaskBar",Artwork=ArtworkPalette.Sample(Colors.SteelBlue)};
                foreach(int edge in new[]{0,2})foreach(bool duo in new[]{false,true}){Preferences.Current.DockEdge=edge;Preferences.Current.DuoStatusIcon=duo;dock=new DockHost(shell,screen);dock.Show();dock.Refresh(shell.Power);await Task.Delay(80);int length=duo?106:128;double pixelTolerance=.51/dock.Scale+.02;Require(Math.Abs(dock.Status.Width-44)<=pixelTolerance&&Math.Abs(dock.Status.Height-length)<=pixelTolerance&&dock.Battery.Height==18&&dock.Battery.IsVisible!=duo,"both styles stay narrow and only classic shows the thin battery bar; edge="+edge+" duo="+duo+" width="+dock.Status.Width+" height="+dock.Status.Height+" pill="+dock.Battery.Height+" visible="+dock.Battery.IsVisible);var glyph=Children<DuoStatusGlyph>(dock.Status).Single();Require(glyph.IsVisible==duo,"Duo can be enabled or disabled");Require(!duo||glyph.BatteryVisible,"Duo includes the battery arc");Require(dock.MediaBar!=null&&Math.Abs(dock.MediaBar.Shape.Y-12)<.1,"side media is anchored at the monitor top");dock.MediaBar.SetExpanded(true);await Task.Delay(50);Require(Math.Abs(dock.MediaBar.Shape.Y-12)<.1,"side media expands inward from the top");dock.MediaBar.SetExpanded(false);await Task.Delay(50);Require(Math.Abs(dock.MediaBar.Shape.Y-12)<.1,"side media returns to the top");
                    var root=(Grid)dock.Status.Surface.Child;foreach(FrameworkElement child in root.Children){if(child.Visibility!=Visibility.Visible)continue;var bounds=child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));Require(bounds.Left>=-.1&&bounds.Right<=root.ActualWidth+.1&&bounds.Top>=-.1&&bounds.Bottom<=root.ActualHeight+.1,"status controls are not clipped");}Program.Save(dock.Status.Surface,44,length,Path.Combine(AppLog.Data,"side-status-"+edge+(duo?"-duo":"-classic")+".png"));dock.Close();dock=null;
                }
            }finally{if(dock!=null)dock.Close();shell.Power=oldPower;shell.Services.Media=oldMedia;shell.Desktop=oldDesktop;Preferences.Current.DockEdge=oldEdge;Preferences.Current.DuoStatusIcon=oldDuo;Preferences.Current.SliderValueNumbers=oldNumbers;}
        }
    }
}
