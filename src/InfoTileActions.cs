using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class PlayerTile : InfoTile
    {
        readonly ShellController shell;
        readonly Border art=new Border();
        readonly Button artButton;
        readonly TextBlock title=Label("미디어 없음",12);
        readonly Glyph play=new Glyph("play",22){Color=TileScheme.Creating.Ink};
        System.Windows.Media.Imaging.BitmapSource shownArt;
        string shownTitle;
        bool shownPlaying;
        internal PlayerTile(int columns,int rows,ShellController controller):base(columns,rows)
        {
            shell=controller;
            title.FontWeight=FontWeights.SemiBold;
            var transport=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
            transport.Children.Add(Control(new Glyph("previous",18){Color=TileScheme.Creating.Ink},"previous","이전"));
            transport.Children.Add(Control(play,"play","재생 · 일시정지"));
            transport.Children.Add(Control(new Glyph("next",18){Color=TileScheme.Creating.Ink},"next","다음"));
            art.Background=TileScheme.Creating.Track;
            artButton=Paint.Button(new Border(),OpenApp,"현재 재생 앱 열기");
            artButton.Background=Brushes.Transparent;
            artButton.Padding=new Thickness(0);
            artButton.IsEnabled=false;
            if(rows==2)
            {
                RowDefinitions.Add(new RowDefinition());
                RowDefinitions.Add(new RowDefinition {Height=new GridLength(70)});
                Children.Add(art);
                Children.Add(artButton);
                var bottom=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
                title.HorizontalAlignment=HorizontalAlignment.Center;
                title.Margin=new Thickness(10,0,10,2);
                bottom.Children.Add(title);
                bottom.Children.Add(transport);
                var panel=new Border {Background=TileScheme.Creating.Surface,Child=bottom};
                Grid.SetRow(panel,1);
                Children.Add(panel);
                return;
            }
            art.Width=art.Height=68;
            art.CornerRadius=new CornerRadius(34);
            art.HorizontalAlignment=HorizontalAlignment.Left;
            art.Margin=new Thickness(10,0,0,0);
            Children.Add(art);
            artButton.Width=artButton.Height=68;
            artButton.HorizontalAlignment=HorizontalAlignment.Left;
            artButton.Margin=art.Margin;
            artButton.Resources["ButtonRadius"]=new CornerRadius(34);
            Children.Add(artButton);
            var side=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(90,0,24,0)};
            title.HorizontalAlignment=HorizontalAlignment.Center;
            side.Children.Add(title);
            transport.HorizontalAlignment=HorizontalAlignment.Center;
            side.Children.Add(transport);
            Children.Add(side);
        }
        void OpenApp()
        {
            var media=shell.Services.Media;
            if(media!=null&&media.Active&&!String.IsNullOrEmpty(media.AppId))shell.OpenMediaApplication(media.AppId);
        }
        internal bool CanOpenApp {get{return artButton.IsEnabled;}}
        Button Control(UIElement glyph,string command,string hint)
        {
            var button=Paint.Button(glyph,()=>shell.Media(command),hint);
            button.Width=button.Height=38;
            button.Padding=new Thickness(0);
            button.Background=Brushes.Transparent;
            button.Resources["ButtonRadius"]=new CornerRadius(19);
            return button;
        }
        internal override void Refresh(ShellController controller,DateTime now)
        {
            var media=controller.Services.Media;
            bool active=media!=null&&media.Active;
            bool openable=active&&!String.IsNullOrEmpty(media.AppId);
            if(artButton.IsEnabled!=openable)artButton.IsEnabled=openable;
            string text=!active?"미디어 없음":String.IsNullOrEmpty(media.Artist)?media.Title:media.Title+" - "+media.Artist;
            if(text!=shownTitle)
            {
                shownTitle=text;
                title.Text=text;
            }
            bool playing=active&&media.Playing;
            if(playing!=shownPlaying)
            {
                shownPlaying=playing;
                play.Update(playing?"pause":"play",Scheme.Ink);
            }
            var picture=active?media.Artwork:null;
            if(picture==shownArt)return;
            shownArt=picture;
            if(picture==null)
            {
                art.Background=Scheme.Track;
                return;
            }
            var brush=new ImageBrush(picture) {Stretch=Stretch.UniformToFill};
            brush.Freeze();
            art.Background=brush;
        }
    }

    internal sealed class SearchTile : InfoTile
    {
        internal SearchTile(int columns,int rows,ShellController shell):base(columns,rows)
        {
            var line=new DockPanel {Margin=new Thickness(18,0,18,0),HorizontalAlignment=columns==4?HorizontalAlignment.Stretch:HorizontalAlignment.Center};
            var glyph=new Glyph("search",26){Color=TileScheme.Creating.Ink};
            DockPanel.SetDock(glyph,Dock.Left);
            line.Children.Add(glyph);
            var hint=Label(columns==4?"앱, 파일, 설정 검색":"검색",14,Faint);
            hint.Margin=new Thickness(10,0,0,0);
            line.Children.Add(hint);
            var button=Paint.Button(line,()=>shell.OpenSearchPanel(),"검색 열기");
            button.Background=Brushes.Transparent;
            button.Padding=new Thickness(0);
            button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            button.VerticalContentAlignment=VerticalAlignment.Center;
            button.Resources["ButtonRadius"]=new CornerRadius(Radius(columns,rows));
            Children.Add(button);
        }
        internal override void Refresh(ShellController shell,DateTime now){}
    }

    internal static class TodoStore
    {
        internal const int Limit=50,TextLimit=120;
        static List<string> items;
        internal static event Action Changed;
        static string StoreFile{get{return Path.Combine(AppLog.Data,"fullscreen-todo.json");}}
        internal static List<string> Items
        {
            get
            {
                if(items==null)items=Load(StoreFile);
                return items;
            }
        }
        internal static List<string> Load(string file)
        {
            try
            {
                if(!File.Exists(file)||new FileInfo(file).Length>65536)return new List<string>();
                var values=new JavaScriptSerializer().Deserialize<List<string>>(File.ReadAllText(file));
                return values==null?new List<string>():values.Where(v=>!String.IsNullOrWhiteSpace(v)).Select(Clean).Take(Limit).ToList();
            }
            catch(Exception e)
            {
                AppLog.Once("todo-load",e.GetType().Name);
                return new List<string>();
            }
        }
        internal static string Clean(string text)
        {
            text=(text??"").Trim();
            return text.Length>TextLimit?text.Substring(0,TextLimit):text;
        }
        internal static bool Add(string text)
        {
            text=Clean(text);
            if(text.Length==0||Items.Count>=Limit)return false;
            Items.Add(text);
            Save();
            return true;
        }
        internal static void Remove(string text)
        {
            if(Items.Remove(text))Save();
        }
        static void Save()
        {
            try
            {
                Directory.CreateDirectory(AppLog.Data);
                File.WriteAllText(StoreFile,new JavaScriptSerializer().Serialize(items));
            }
            catch(Exception e)
            {
                AppLog.Write("todo-save",e.Message);
            }
            var notify=Changed;
            if(notify!=null)notify();
        }
    }

    internal sealed class TodoTile : InfoTile
    {
        readonly StackPanel rows=new StackPanel();
        readonly TextBox input;
        readonly bool acceptsText;
        bool loaded;
        internal TodoTile(int columns,int spanRows,bool text):base(columns,spanRows)
        {
            acceptsText=text&&spanRows==2;
            var stack=new DockPanel {Margin=spanRows==2?new Thickness(14,12,14,12):new Thickness(22,8,22,8),VerticalAlignment=spanRows==2?VerticalAlignment.Stretch:VerticalAlignment.Center};
            if(spanRows==2)
            {
                var header=new Grid {Height=26};
                var title=Label("리마인더",13);
                title.FontWeight=FontWeights.SemiBold;
                header.Children.Add(title);
                DockPanel.SetDock(header,Dock.Top);
                stack.Children.Add(header);
                if(acceptsText)
                {
                    var add=Paint.Button(new Glyph("close",12){Color=TileScheme.Creating.Ink,RenderTransform=new RotateTransform(45,6,6)},ToggleInput,"할 일 추가");
                    add.Width=add.Height=24;
                    add.Padding=new Thickness(0);
                    add.Background=TileScheme.Creating.Track;
                    add.Resources["ButtonRadius"]=new CornerRadius(12);
                    add.HorizontalAlignment=HorizontalAlignment.Right;
                    header.Children.Add(add);
                    input=new TextBox {Visibility=Visibility.Collapsed,Background=TileScheme.Creating.Track,Foreground=TileScheme.Creating.Ink,CaretBrush=TileScheme.Creating.Ink,BorderThickness=new Thickness(0),FontSize=12,Padding=new Thickness(6,4,6,4),Margin=new Thickness(0,2,0,4),MaxLength=TodoStore.TextLimit};
                    System.Windows.Automation.AutomationProperties.SetName(input,"새 할 일");
                    input.KeyDown+=(s,e)=>
                    {
                        if(e.Key==Key.Enter)
                        {
                            TodoStore.Add(input.Text);
                            CloseInput();
                            e.Handled=true;
                        }
                        else if(e.Key==Key.Escape)
                        {
                            CloseInput();
                            e.Handled=true;
                        }
                    };
                    DockPanel.SetDock(input,Dock.Top);
                    stack.Children.Add(input);
                }
            }
            stack.Children.Add(new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            Children.Add(stack);
            TodoStore.Changed+=Rebuild;
        }
        internal bool InputVisible {get{return input!=null&&input.Visibility==Visibility.Visible;}}
        internal int RowCount {get{return rows.Children.Count;}}
        void CloseInput()
        {
            input.Text="";
            input.Visibility=Visibility.Collapsed;
        }
        void ToggleInput()
        {
            input.Visibility=input.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;
            if(input.Visibility==Visibility.Visible)input.Focus();
        }
        void Rebuild()
        {
            TileScheme.Creating=Scheme;
            rows.Children.Clear();
            foreach(string item in TodoStore.Items)
            {
                string text=item;
                var row=new DockPanel {Margin=new Thickness(0,2,0,2)};
                var mark=new Border {Width=15,Height=15,CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1.4),BorderBrush=TileScheme.Creating.Ink,Background=Brushes.Transparent};
                var done=Paint.Button(mark,()=>TodoStore.Remove(text),"완료: "+text);
                done.Padding=new Thickness(0);
                done.Background=Brushes.Transparent;
                done.Margin=new Thickness(0,0,7,0);
                DockPanel.SetDock(done,Dock.Left);
                row.Children.Add(done);
                row.Children.Add(Label(text,12));
                rows.Children.Add(row);
            }
            if(rows.Children.Count==0&&!acceptsText)rows.Children.Add(Label("전체화면 미디어에서 추가",11,Faint));
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            if(loaded)return;
            loaded=true;
            Rebuild();
        }
        internal override void Retire()
        {
            TodoStore.Changed-=Rebuild;
        }
    }
}
