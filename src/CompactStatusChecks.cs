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
            for(int i=0;i<4;i++){var dot=DuoStatusGlyph.VolumeDot(i);Require(Math.Abs((dot-DuoStatusGlyph.RingCenter).Length-12.7)<.001,"volume dots follow the battery arc curvature");}Require(DuoStatusGlyph.VolumeDot(1).Y>DuoStatusGlyph.VolumeDot(0).Y&&DuoStatusGlyph.VolumeDot(2).Y>DuoStatusGlyph.VolumeDot(3).Y,"center dots are lower than the outer dots");
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
        static void DuoNumbers()
        {
            foreach(bool dark in new[]{false,true})using(Glass95.WithDarkForFixture(dark)){
                var gallery=new StackPanel {Orientation=Orientation.Horizontal,Background=Paint.Brush(dark?"#FF333D48":"#FFD4DDE5")};
                foreach(int level in new[]{0,20,61,100}){var glyph=new DuoStatusGlyph();glyph.Update(new PowerState {Known=true,Level=level},"wifi",100,75,false,true,Glass95.DockForeground);Render(glyph,32,34);var bounds=glyph.BatteryNumberBounds;Require(glyph.BatteryNumberText==level.ToString()&&bounds.Top>=0&&bounds.Bottom<=11&&Math.Abs(bounds.X+bounds.Width/2-16)<.01,"battery number is centered in the upper ring gap without clipping or covering Wi-Fi");if(level>0)gallery.Children.Add(new Viewbox {Width=96,Height=102,Child=glyph});}
                Program.Save(gallery,288,102,Path.Combine(AppLog.Data,"duo-percent-"+(dark?"dark":"light")+".png"));var unknown=new DuoStatusGlyph();unknown.Update(new PowerState(),"wifi",100,50,false,true,Glass95.DockForeground);Render(unknown,32,34);Require(unknown.BatteryNumberText==""&&unknown.BatteryNumberBounds.IsEmpty,"unknown batteries do not claim a percentage");
            }
        }
        static async Task Charging(DuoStatusGlyph glyph)
        {
            bool animations=Preferences.Current.Animations,charge=Preferences.Current.ChargeAnimation;try{using(Motion.WithAnimationsForFixture()){
                Preferences.Current.Animations=Preferences.Current.ChargeAnimation=true;glyph.Update(new PowerState {Known=true,Level=61,Connected=true,Charging=true},"wifi",100,75,false,true,Glass95.DockForeground);glyph.ChargePulse();Require(glyph.ChargeAnimating&&glyph.HasAnimatedProperties&&glyph.Opacity==1,"charging animates the battery ring without blinking the network");await Task.Delay(180);Program.Save(glyph,32,34,Path.Combine(AppLog.Data,"duo-charge.png"));Require(glyph.BatteryNumberBounds.IsEmpty,"charging temporarily substitutes the upper number with a bolt");glyph.Visibility=Visibility.Collapsed;Require(!glyph.ChargeAnimating&&!glyph.HasAnimatedProperties,"hidden icons release the charging animation");glyph.Visibility=Visibility.Visible;glyph.ChargePulse();await Task.Delay(2400);Render(glyph,32,34);Require(!glyph.ChargeAnimating&&!glyph.HasAnimatedProperties&&!glyph.BatteryNumberBounds.IsEmpty&&glyph.BatteryNumberText=="61","charging ends without a retained clock and returns to the percentage");
                var poweredColor=((SolidColorBrush)glyph.BatteryColor).Color;Require(poweredColor.G>poweredColor.R+40&&poweredColor.G>poweredColor.B+30,"the ring stays green after the charging animation finishes");glyph.ChargePulse();glyph.Update(new PowerState {Known=true,Level=61},"wifi",100,75,false,true,Glass95.DockForeground);Require(!glyph.ChargeAnimating&&glyph.BatteryColor==Glass95.DockForeground,"unplugging cancels charging feedback and restores the ordinary ring");Preferences.Current.Animations=false;glyph.ChargePulse();Require(!glyph.ChargeAnimating,"reduced animation preference is respected");Preferences.Current.Animations=true;Preferences.Current.ChargeAnimation=false;glyph.ChargePulse();Require(!glyph.ChargeAnimating,"charging animation can be disabled independently");
            }}finally{glyph.StopPulse();Preferences.Current.Animations=animations;Preferences.Current.ChargeAnimation=charge;glyph.Update(new PowerState {Known=true,Level=87},"wifi",100,75,false,true,Glass95.DockForeground);}
        }
        static async Task Horizontal(ShellController shell,System.Windows.Forms.Screen screen)
        {
            foreach(int edge in new[]{1,3})foreach(bool duo in new[]{false,true}){Preferences.Current.DockEdge=edge;Preferences.Current.DuoStatusIcon=duo;var dock=new DockHost(shell,screen);try{dock.Show();dock.Refresh(shell.Power);await Task.Delay(60);double width=DockLayout.StatusSize(screen.Bounds.Width/dock.Scale,PowerDisplay.DockSlot(shell.Power,0),duo);Require(dock.Battery.Height==23&&!dock.Battery.Compact&&dock.Battery.IsVisible!=duo,"top and bottom classic battery retain their original thickness and charging animation size");Require(Math.Abs(dock.Status.Width-width)<1/dock.Scale+.01,"horizontal status fits the selected icon mode");var root=(Grid)dock.Status.Surface.Child;if(duo)Require(width==172&&root.ColumnDefinitions[1].ActualWidth<=33,"Duo horizontal spacing does not retain the old wide network slot");Program.Save(dock.Status.Surface,(int)width,44,Path.Combine(AppLog.Data,"horizontal-status-"+edge+(duo?"-duo":"-classic")+".png"));}finally{dock.Close();}}
        }
        internal static async Task Run(ShellController shell,System.Windows.Forms.Screen screen)
        {
            bool oldDuo=Preferences.Current.DuoStatusIcon,oldNumbers=Preferences.Current.SliderValueNumbers,oldDesktop=shell.Desktop;int oldEdge=Preferences.Current.DockEdge;var oldMedia=shell.Services.Media;var oldPower=shell.Power;DockHost dock=null;
            try{Numbers();DuoNumbers();shell.Desktop=true;shell.Power=new PowerState {Known=true,Level=87,Connected=false};shell.Services.Media=new MediaState {Available=true,Active=true,Playing=true,Title="Side status fixture",Artist="One TaskBar",Artwork=ArtworkPalette.Sample(Colors.SteelBlue)};await Horizontal(shell,screen);
                foreach(int edge in new[]{0,2})foreach(bool duo in new[]{false,true}){Preferences.Current.DockEdge=edge;Preferences.Current.DuoStatusIcon=duo;dock=new DockHost(shell,screen);dock.Show();dock.Refresh(shell.Power);await Task.Delay(80);int length=duo?106:128;double pixelTolerance=.51/dock.Scale+.02;Require(Math.Abs(dock.Status.Width-44)<=pixelTolerance&&Math.Abs(dock.Status.Height-length)<=pixelTolerance&&dock.Battery.Height==18&&dock.Battery.IsVisible!=duo,"both styles stay narrow and only classic shows the thin battery bar; edge="+edge+" duo="+duo+" width="+dock.Status.Width+" height="+dock.Status.Height+" pill="+dock.Battery.Height+" visible="+dock.Battery.IsVisible);var glyph=Children<DuoStatusGlyph>(dock.Status).Single();Require(glyph.IsVisible==duo,"Duo can be enabled or disabled");Require(!duo||glyph.BatteryVisible,"Duo includes the battery arc");Require(dock.MediaBar!=null&&Math.Abs(dock.MediaBar.Shape.Y-12)<.1,"side media is anchored at the monitor top");dock.MediaBar.SetExpanded(true);await Task.Delay(50);Require(Math.Abs(dock.MediaBar.Shape.Y-12)<.1,"side media expands inward from the top");dock.MediaBar.SetExpanded(false);await Task.Delay(50);Require(Math.Abs(dock.MediaBar.Shape.Y-12)<.1,"side media returns to the top");
                    var root=(Grid)dock.Status.Surface.Child;foreach(FrameworkElement child in root.Children){if(child.Visibility!=Visibility.Visible)continue;var bounds=child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));Require(bounds.Left>=-.1&&bounds.Right<=root.ActualWidth+.1&&bounds.Top>=-.1&&bounds.Bottom<=root.ActualHeight+.1,"status controls are not clipped");}Program.Save(dock.Status.Surface,44,length,Path.Combine(AppLog.Data,"side-status-"+edge+(duo?"-duo":"-classic")+".png"));if(edge==0&&duo)await Charging(glyph);dock.Close();dock=null;
                }
                await CustomStatus(shell,screen);await BudsStatus(shell,screen);
            }finally{if(dock!=null)dock.Close();shell.Power=oldPower;shell.Services.Media=oldMedia;shell.Desktop=oldDesktop;Preferences.Current.DockEdge=oldEdge;Preferences.Current.DuoStatusIcon=oldDuo;Preferences.Current.SliderValueNumbers=oldNumbers;}
        }
        static async Task CustomStatus(ShellController shell,System.Windows.Forms.Screen screen)
        {
            var saved=Preferences.Current.DockStatusItems;try{
                Require(DockStatusItems.Clean(new[]{"invalid","buds","buds","calendar","capture","settings","manager"}).SequenceEqual(new[]{"buds","calendar","capture","settings"}),"custom status rejects unknown and duplicate entries and caps four shortcuts");
                Preferences.Current.DockStatusItems=new[]{"buds","calendar","capture","settings"};foreach(int edge in new[]{0,1,2,3}){Preferences.Current.DockEdge=edge;var dock=new DockHost(shell,screen);try{dock.Show();dock.Refresh(shell.Power);await Task.Delay(50);var root=(Grid)dock.Status.Surface.Child;foreach(FrameworkElement child in root.Children){if(child.Visibility!=Visibility.Visible)continue;var bounds=child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));Require(bounds.Left>=-.5&&bounds.Right<=root.ActualWidth+.5&&bounds.Top>=-.5&&bounds.Bottom<=root.ActualHeight+.5,"four extra shortcuts fit every taskbar edge");}Require(Children<Button>(root).Count(b=>b.ToolTip as string=="달력")==1,"selected shortcuts appear once");}finally{dock.Close();}}
            }finally{Preferences.Current.DockStatusItems=saved;}
        }
        static async Task BudsStatus(ShellController shell,System.Windows.Forms.Screen screen)
        {
            int ui=Preferences.Current.UIVersion;string output=shell.Services.OutputName;try{shell.Services.OutputName="fixture speakers";foreach(int version in ReleaseFeatures.GlassUI?new[]{90,95}:new[]{90}){Preferences.Current.UIVersion=version;var quick=new QuickWindow(shell);try{quick.ShowAt(screen.Bounds,40,40,380,650);foreach(bool connected in new[]{true,false,true}){shell.Buds.UpdateCaption(new[]{new BluetoothPeer {Name="Galaxy Buds4 Pro",Connected=connected},new BluetoothPeer {Name="키보드",Connected=true}});quick.Refresh(shell.Power,50,30,false,true,true);await Task.Delay(20);Require(quick.GridUI.BudsSelected==connected,"Buds selected state follows connection even when another Bluetooth device remains connected");}}finally{quick.Close();}}}finally{Preferences.Current.UIVersion=ui;shell.Services.OutputName=output;shell.Buds.UpdateCaption(new BluetoothPeer[0]);}
        }
    }
}
