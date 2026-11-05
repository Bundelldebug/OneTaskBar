using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed class DesktopWidgetGallery : GlassWindow
    {
        readonly DesktopWidgets widgets;
        // Where the widget menu was opened on the desktop; new widgets start there.
        internal System.Drawing.Point? Anchor;
        readonly TextBlock status;
        readonly ComboBox display;
        readonly List<KeyValuePair<string,Button>> choices=new List<KeyValuePair<string,Button>>();
        readonly List<InfoTile> previews=new List<InfoTile>();
        internal DesktopWidgetGallery(DesktopWidgets manager)
        {
            widgets=manager;
            Closed+=(s,e)=>
            {
                foreach(var preview in previews)preview.Retire();
            };
            Title=Program.AppName+" · 바탕화면 위젯";
            Width=540;
            Height=680;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var root=new DockPanel {Margin=new Thickness(22)};
            var heading=new Grid {Margin=new Thickness(0,0,0,16)};
            var title=Paint.TextBlock("바탕화면 위젯",23,Paint.Text);
            title.FontWeight=FontWeights.SemiBold;
            heading.Children.Add(title);
            var done=Paint.Action("완료",Close);
            done.HorizontalAlignment=HorizontalAlignment.Right;
            heading.Children.Add(done);
            DockPanel.SetDock(heading,Dock.Top);
            root.Children.Add(heading);
            var bottom=new StackPanel {Margin=new Thickness(0,14,0,0)};
            status=Paint.TextBlock("",12,Paint.Muted);
            status.TextWrapping=TextWrapping.Wrap;
            bottom.Children.Add(status);
            var help=Paint.TextBlock("바탕화면에서 위젯을 클릭하면 편집합니다. 끌면 옮겨집니다.",12,Paint.Muted);
            help.TextWrapping=TextWrapping.Wrap;
            help.Margin=new Thickness(0,10,0,0);
            bottom.Children.Add(help);
            DockPanel.SetDock(bottom,Dock.Bottom);
            root.Children.Add(bottom);
            var tools=new StackPanel();
            display=new ComboBox {DisplayMemberPath="Label",MinHeight=38,Margin=new Thickness(0,0,0,12)};
            SettingsPickerStyle.Apply(display);
            int index=0;
            foreach(var screen in Forms.Screen.AllScreens)
            {
                display.Items.Add(new PickerChoice {Label="디스플레이 "+(++index)+(screen.Primary?" · 기본":""),Value=screen});
            }
            display.SelectedIndex=0;
            tools.Children.Add(display);
            var search=new TextBox {Background=Brushes.Transparent,Foreground=Paint.OnLight,BorderThickness=new Thickness(0),FontSize=14,Padding=new Thickness(14,9,14,9),ToolTip="추가할 위젯 검색"};
            System.Windows.Automation.AutomationProperties.SetName(search,"추가할 위젯 검색");
            search.TextChanged+=(s,e)=>
            {
                string query=search.Text.Trim();
                foreach(var choice in choices)choice.Value.Visibility=choice.Key.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0?Visibility.Visible:Visibility.Collapsed;
            };
            var searchBody=new Grid();
            var placeholder=Paint.TextBlock("위젯 검색",14,Paint.Brush("#FF586960"));
            placeholder.IsHitTestVisible=false;
            placeholder.VerticalAlignment=VerticalAlignment.Center;
            placeholder.Margin=new Thickness(14,0,0,0);
            searchBody.Children.Add(search);
            searchBody.Children.Add(placeholder);
            search.TextChanged+=(s,e)=>placeholder.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
            tools.Children.Add(new Border {Background=Paint.ControlWhite,CornerRadius=new CornerRadius(22),Child=searchBody,Margin=new Thickness(0,0,0,12)});
            DockPanel.SetDock(tools,Dock.Top);
            root.Children.Add(tools);
            var tiles=new UniformGrid {Columns=3};
            var now=DateTime.Now;
            foreach(string key in DesktopWidgetCatalog.Keys.Where(k=>k!="clock"&&k!="battery"&&k!="media"))
            {
                string selected=key,label=DesktopWidgetCatalog.Title(key);
                var body=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
                if(InfoTileCatalog.Known(key))
                {
                    var scheme=TileScheme.Desktop();
                    var preview=InfoTileCatalog.Create(key,false,widgets.Shell,scheme);
                    preview.Refresh(widgets.Shell,now);
                    previews.Add(preview);
                    body.Children.Add(new Viewbox {Height=66,MaxWidth=136,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,IsHitTestVisible=false,
                        Child=new Border {Background=scheme.Surface,CornerRadius=new CornerRadius(InfoTile.Radius(InfoTileCatalog.Columns(key),InfoTileCatalog.Rows(key))),Child=preview}});
                }
                else body.Children.Add(new Border {Width=44,Height=44,CornerRadius=new CornerRadius(22),Background=Paint.ControlWhite,
                    HorizontalAlignment=HorizontalAlignment.Center,Child=new Glyph(DesktopWidgetCatalog.Icon(key),24){Color=Paint.OnLight}});
                var name=Paint.TextBlock(label,12,Paint.Text);
                name.TextAlignment=TextAlignment.Center;
                name.TextWrapping=TextWrapping.Wrap;
                name.Margin=new Thickness(2,7,2,0);
                body.Children.Add(name);
                var button=Paint.Button(body,()=>
                {
                    var target=display.SelectedItem as PickerChoice;
                    var screen=Anchor.HasValue?Forms.Screen.FromPoint(Anchor.Value):target==null?Forms.Screen.PrimaryScreen:(Forms.Screen)target.Value;
                    widgets.Add(selected,screen,Anchor);
                    RefreshStatus();
                },label+" 위젯 추가");
                button.Height=128;
                button.Padding=new Thickness(4);
                button.Margin=new Thickness(3);
                button.Resources["ButtonRadius"]=new CornerRadius(22);
                tiles.Children.Add(button);
                choices.Add(new KeyValuePair<string,Button>(label,button));
            }
            root.Children.Add(new SmoothScrollViewer {Content=tiles,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            Surface.Child=root;
            RefreshStatus();
        }
        internal void RefreshStatus()
        {
            status.Text=widgets.Status;

        }
    }
}
