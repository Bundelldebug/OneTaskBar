using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
namespace OneTaskBar
{
    internal static class ReleaseUIFeedbackChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static void CheckDisplayedSurface(GlassWindow window)
        {
            var brush=window.Surface.Background as LinearGradientBrush;
            Require(window.HasBlur&&Native.RealtimeBackdrop(window)&&Native.BlurOpacity(window)>.99,"The displayed material retains its live backdrop");
            double opacity=PanelMaterial.ReadablePanel(window)?(window.Dark?PanelMaterial.DarkPanelOpacity:PanelMaterial.LightPanelOpacity)/100.0:.30;Require(brush!=null&&brush.Opacity>.99&&brush.GradientStops.All(s=>Math.Abs(s.Color.A/255.0-opacity)<.003),"The displayed material uses the readable panel palette or the original dock tint for its role");
        }
        internal static void Validate(List<string> checks)
        {
            YouTubeArtworkChecks.Validate(checks);
            foreach(bool windows in new[]{false,true})foreach(bool panels in new[]{false,true}){
                Require(AppTheme.ResolveDock(false,2,panels,windows)==panels,"Linked dock follows panels");
                Require(!AppTheme.ResolveDock(true,1,panels,windows)&&AppTheme.ResolveDock(true,2,panels,windows)&&AppTheme.ResolveDock(true,0,panels,windows)==windows,"Independent dock palette never alters panels");
            }
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
            foreach(int legacy in new[]{0,45,70,100}){var migrated=Preferences.Normalize(serializer.Deserialize<PreferenceData>("{\"UITransparency\":"+legacy+",\"UITheme\":2,\"DockEdge\":1,\"Animations\":false}"));Require(migrated.UITheme==2&&migrated.DockEdge==1&&!migrated.Animations,"Retired transparency does not discard existing preferences");Require(!serializer.Serialize(migrated).Contains("UITransparency"),"Retired transparency is no longer saved");}
            Require(PanelMaterial.Transparency==70,"Classic light panel and dock transparency is fixed at seventy percent");
            foreach(bool modern in new[]{false,true})foreach(bool dark in new[]{false,true}){var surface=(LinearGradientBrush)PanelMaterial.Surface(dark,modern);Require(surface.IsFrozen&&Object.ReferenceEquals(surface,PanelMaterial.Surface(dark,modern)),"Fixed material reuses a frozen palette brush");Require(surface.GradientStops.All(s=>Math.Abs(s.Color.A/255.0-.30)<.003),"All base-tint stops have thirty percent opacity in every UI palette");}
            var now=new DateTime(2026,10,25,0,0,0,DateTimeKind.Utc);var retry=new ArtworkUpgradeSchedule();Require(retry.Due(now)&&!retry.Pending(now),"New track has an initial cover attempt");retry.Attempt(now);Require(!retry.Due(now.AddSeconds(1))&&retry.Pending(now.AddSeconds(2)),"Late browser history is retried after two seconds");retry.Attempt(now.AddSeconds(2));Require(!retry.Pending(now.AddSeconds(7))&&retry.Pending(now.AddSeconds(8)),"Second cover retry backs off");retry.Attempt(now.AddSeconds(8));Require(!retry.Pending(now.AddDays(1)),"Artwork lookup stops after three attempts");retry.Clear();retry.Complete();Require(!retry.Due(now.AddDays(1)),"Successful artwork stops retrying");
            bool consent=Preferences.Current.YouTubeHighResArtwork;string track=Preferences.Current.YouTubeArtworkTrack;try{Preferences.Current.YouTubeHighResArtwork=false;Preferences.Current.YouTubeArtworkTrack=null;Require(!YouTubeArtwork.Allowed("Internet","Current")&&YouTubeArtwork.Read("Internet","Current")==null,"Unapproved cover never reads history or requests a server");Preferences.Current.YouTubeArtworkTrack=YouTubeArtwork.TrackKey("Internet","Current");Require(YouTubeArtwork.Allowed("Internet","Current")&&!YouTubeArtwork.Allowed("Internet","Another"),"Single-track authorization is exact");Preferences.Current.YouTubeHighResArtwork=true;Require(YouTubeArtwork.Allowed("samsunginternet.exe","Current")&&!YouTubeArtwork.Allowed("OtherBrowser","Current"),"Cover lookup is limited to supported Samsung Browser identities");}finally{Preferences.Current.YouTubeHighResArtwork=consent;Preferences.Current.YouTubeArtworkTrack=track;}
            checks.Add("Independent dock palettes, fixed seventy-percent dock transparency and preference migration, bounded delayed artwork retries and exact-track privacy pass");
        }
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject{var own=root as T;if(own!=null)yield return own;foreach(object item in LogicalTreeHelper.GetChildren(root)){var child=item as DependencyObject;if(child!=null)foreach(var node in Children<T>(child))yield return node;}}
        static IEnumerable<TextBlock> VisibleLabels(DependencyObject root){var label=root as TextBlock;if(label!=null&&label.IsVisible)yield return label;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var text in VisibleLabels(VisualTreeHelper.GetChild(root,i)))yield return text;}
        internal static async Task Run(ShellController shell)
        {
            int ui=Preferences.Current.UIVersion;try{
                foreach(int version in ReleaseFeatures.GlassUI?new[]{85,90,95}:new[]{85,90})foreach(bool dark in new[]{false,true})using(Glass95.WithDarkForFixture(dark)){
                    Preferences.Current.UIVersion=version;var wifi=WifiNetworkView.Create(new WifiNetwork {Name="Connected network",Connected=true,Generation=7,Signal=80,Secure=true},()=>{});var bluetooth=BluetoothDeviceView.Create(new BluetoothPeer {Name="Connected headphones",Connected=true,Paired=true,Icon="headphones"},()=>{});
                    var card=(LinearGradientBrush)Glass95.Card;Require(card.GradientStops.All(s=>s.Color.A<=38),"Glass card film remains light enough to reveal the backdrop");Require(((SolidColorBrush)Glass95.Text).Color.A==255&&((SolidColorBrush)Glass95.Muted).Color.A==255,"Text retains full opacity independently of the material");
                    LightPanel.Apply(wifi);LightPanel.Apply(bluetooth);Glass95.RefreshColors(wifi);Glass95.RefreshColors(bluetooth);
                    Require(Children<Glyph>(wifi).First().Color==Paint.OnLight&&Children<Glyph>(bluetooth).First().Color==Paint.OnLight,"Connected radio icons retain dark ink inside white circles in every palette");
                    var settings=new QuickWidgetTile("settings","Windows settings",()=>{});var devices=new QuickWidgetTile("phone","Devices",()=>{});var buds=new QuickWidgetTile("headphones","Galaxy Buds",()=>{});Require(((SolidColorBrush)settings.Toggle.Background).Color==((SolidColorBrush)devices.Toggle.Background).Color&&((SolidColorBrush)buds.Toggle.Background).Color==((SolidColorBrush)devices.Toggle.Background).Color,"Single action tiles start with identical circle materials");
                    settings.State(true,true);LightPanel.Apply(settings);Glass95.RefreshColors(settings);Require(settings.Icon.Color==Paint.OnLight,"Selected circles retain dark ink after palette recoloring");
                }
            }finally{Preferences.Current.UIVersion=ui;}
            var picker=shell.Welcome.VersionPicker;picker.UpdateLayout();Require(VisibleLabels(picker).Any(t=>t.Text==((UIVersionChoice)picker.SelectedItem).Label)&&!VisibleLabels(picker).Any(t=>t.Text.Contains("OneTaskBar.UIVersionChoice")),"Closed version list displays the selected version label");picker.IsDropDownOpen=true;await Task.Delay(80);Require(picker.IsDropDownOpen&&picker.ItemContainerGenerator.ContainerFromIndex(0) is ComboBoxItem,"Version list opens and generates selectable entries");var popup=picker.Template.FindName("PART_Popup",picker) as Popup;Require(popup!=null&&popup.IsOpen&&popup.Child is Border&&((Border)popup.Child).Background!=null,"Version list uses a themed opaque popup");picker.IsDropDownOpen=false;
            shell.Quick.RefreshGlassMaterial();CheckDisplayedSurface(shell.Quick);Require(Children<TextBlock>(shell.Quick).All(t=>t.Effect==null)&&Children<Glyph>(shell.Quick).All(g=>g.Effect==null),"Fixed transparency has no per-label glow effects");
            var scroller=Children<ScrollViewer>(shell.Welcome).OrderByDescending(s=>s.ScrollableHeight).First();scroller.ScrollToVerticalOffset(scroller.ScrollableHeight/2);await Task.Delay(40);if(scroller.ScrollableHeight>1){var fade=scroller.OpacityMask as LinearGradientBrush;Require(fade!=null,"Settings scroll edge retains its gradual fade");var layer=System.Windows.Documents.AdornerLayer.GetAdornerLayer(scroller);Require(layer==null||layer.GetAdorners(scroller)==null||layer.GetAdorners(scroller).Length==0,"Scroll edges do not duplicate and blur the full settings content");}scroller.ScrollToTop();await Task.Delay(40);Program.Save(shell.Welcome.Surface,760,720,System.IO.Path.Combine(AppLog.Data,"ui-version-selector.png"));
            var previousQuick=shell.Quick;var previousStart=shell.StartMenu;shell.StartMenu=null;try{using(MenuDismiss.WithSyntheticPointerForFixture()){
                shell.ToggleStart(null);await Task.Delay(140);var start=shell.StartMenu;Require(start!=null&&start.IsVisible,"Keyboard route opens custom Start");CheckDisplayedSurface(start);start.ObserveOutsideForFixture();Native.RectI bounds;Native.GetWindowRect(new WindowInteropHelper(start).Handle,out bounds);MenuDismiss.PointerPressed(new Native.PointI {X=bounds.Left+30,Y=bounds.Top+30});Require(start.IsVisible&&!start.IsDismissing,"Inside clicks preserve Start");
                MenuDismiss.PointerPressed(new Native.PointI {X=bounds.Right+80,Y=bounds.Top+10});await Task.Delay(300);Require(!start.IsVisible&&!MenuDismiss.Active,"Outside click dismisses keyboard Start and releases its observer");
                start.Close();start=new StartWindow(shell);shell.StartMenu=start;start.ShowActivated=false;start.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,20,20,940,700);start.ObserveOutsideForFixture();await Task.Delay(100);Require(!start.IsActive,"Outside dismissal is tested without foreground activation");Native.GetWindowRect(new WindowInteropHelper(start).Handle,out bounds);var menu=new ContextMenu {PlacementTarget=start.Surface,Placement=PlacementMode.AbsolutePoint,HorizontalOffset=bounds.Right+10,VerticalOffset=bounds.Top+40};menu.Items.Add(new MenuItem {Header="Keep Start open"});try{menu.IsOpen=true;await Task.Delay(100);var source=PresentationSource.FromVisual(menu) as HwndSource;Native.RectI menuBounds;Require(source!=null&&Native.GetWindowRect(source.Handle,out menuBounds),"Start context menu is open");Native.GetWindowRect(source.Handle,out menuBounds);MenuDismiss.PointerPressed(new Native.PointI {X=menuBounds.Left+20,Y=menuBounds.Top+20});Require(start.IsVisible&&!start.IsDismissing,"Clicks inside a Start context menu preserve Start");}finally{menu.IsOpen=false;MenuTheme.Release(menu);}
                MenuDismiss.PointerPressed(new Native.PointI {X=bounds.Right+80,Y=bounds.Bottom+40});await Task.Delay(300);Require(!start.IsVisible&&!MenuDismiss.Active,"Outside click also dismisses nonactivated Start");start.Close();shell.StartMenu=null;
            }}finally{if(shell.StartMenu!=null&&shell.StartMenu!=previousStart)shell.StartMenu.Close();shell.StartMenu=previousStart;shell.Quick=previousQuick;}
        }
    }
}
