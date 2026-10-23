using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal sealed class SearchWindow : GlassWindow
    {
        readonly ShellController shell;
        readonly SearchHistory history=new SearchHistory(Path.Combine(AppLog.Data,"search-history.json"));
        readonly StackPanel cards=new StackPanel {MaxWidth=1000,HorizontalAlignment=HorizontalAlignment.Stretch};
        Border filesCard;
        readonly ScrollViewer scroll;
        readonly Border field;
        readonly TextBlock placeholder=Paint.TextBlock("무엇을 찾고 있나요?",16,Paint.Brush("#FF557D9A"));
        readonly DispatcherTimer debounce=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(260)};
        StackPanel files=new StackPanel();
        internal readonly TextBox Query=new TextBox {FontFamily=SystemFonts.MessageFontFamily,FontSize=16,Foreground=Paint.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0,11,0,11),MaxLength=120,VerticalContentAlignment=VerticalAlignment.Center};
        CancellationTokenSource cancellation;int generation;bool closed;
        internal readonly SearchNavigation Navigation;
        internal SearchWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 검색";Width=720;Height=680;Radius=32;Dark=true;StableSurface=true;Surface.CornerRadius=new CornerRadius(32);Surface.BorderBrush=Paint.Brush("#58FFFFFF");Surface.BorderThickness=new Thickness(.7);Surface.Background=QuickPanel.Glass;
            var root=new DockPanel {Margin=new Thickness(24,20,24,16)};
            var header=new Grid {Height=36,Margin=new Thickness(4,0,4,12)};var title=Paint.TextBlock("검색",19,Paint.Text);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);
            var back=Paint.Action("앱 화면",()=>shell.ToggleDrawer(null));back.HorizontalAlignment=HorizontalAlignment.Right;back.Margin=new Thickness(0,0,62,0);header.Children.Add(back);var close=Paint.Button(new Glyph("close",19),()=>Hide(),"검색 닫기");close.HorizontalAlignment=HorizontalAlignment.Right;close.Width=40;close.Padding=new Thickness(8);header.Children.Add(close);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var search=new Grid {Margin=new Thickness(20,3,12,3)};search.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(29)});search.ColumnDefinitions.Add(new ColumnDefinition());search.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(36)});
            search.Children.Add(new Glyph("search",20){Color=Paint.Ink,VerticalAlignment=VerticalAlignment.Center});
            var input=new Grid();input.Children.Add(Query);placeholder.IsHitTestVisible=false;input.Children.Add(placeholder);Grid.SetColumn(input,1);search.Children.Add(input);
            var clear=Paint.Button(new Glyph("close",17){Color=Paint.Ink},()=> {Query.Clear();Query.Focus();},"검색어 지우기");clear.Width=clear.Height=30;clear.Padding=new Thickness(6);clear.Background=Paint.Brush("#14000000");Grid.SetColumn(clear,2);search.Children.Add(clear);
            field=new Border {CornerRadius=new CornerRadius(29),Height=56,Width=620,Background=new LinearGradientBrush(Color.FromArgb(240,232,236,255),Color.FromArgb(240,218,251,247),0),Child=search,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,15,0,0)};DockPanel.SetDock(field,Dock.Bottom);root.Children.Add(field);
            scroll=new SmoothScrollViewer {Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);Surface.Child=root;TransitionContent=root;
            Query.TextChanged+=(s,e)=> {placeholder.Visibility=Query.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Reload();};

            Navigation=new SearchNavigation(this,Query);
            debounce.Tick+=async(s,e)=> {debounce.Stop();await FindFiles();};
            IsVisibleChanged+=(s,e)=> {if(!IsVisible){Cancel();Navigation.Clear();cards.Children.Clear();files.Children.Clear();}};Closed+=(s,e)=> {closed=true;Cancel();Navigation.Clear();};
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&IsVisible&&!IsMouseOver)Hide();};
        }
        internal void Configure(double width,double height) {field.Width=Math.Max(220,Math.Min(620,width-76));cards.MaxWidth=Math.Max(240,Math.Min(1000,width-84));}
        internal void Reset() {Query.Text="";Reload();scroll.ScrollToTop();}
        void Cancel() {debounce.Stop();++generation;if(cancellation!=null){cancellation.Cancel();cancellation.Dispose();cancellation=null;}}
        internal void Reload()
        {
            Cancel();Navigation.Clear();cards.Children.Clear();string query=Query.Text.Trim();
            var apps=query.Length==0?Recommendations():LauncherSearch.Apps(shell.Apps,query);
            if(apps.Count>0)AppCard(query.Length==0?"추천 앱":"검색된 앱",apps);
            if(query.Length==0){SettingCard("주제별 설정",LauncherSearch.Settings.Take(6));RecentCard();return;}
            var settings=LauncherSearch.Settings.Where(s=>LauncherSearch.Matches(s.Name+" "+s.Keywords,query)).ToList();if(settings.Count>0)SettingCard("설정",settings);
            files=new StackPanel();files.Children.Add(Label("파일과 사진을 찾고 있습니다…",12,LightPanel.Muted));filesCard=Card("내 파일",files);
            debounce.Start();
        }
        List<AppEntry> Recommendations() {var choices=new List<AppEntry>();foreach(var app in shell.Pins.Concat(shell.Running).Concat(shell.Apps)){var installed=shell.Apps.FirstOrDefault(a=>Catalog.Same(a,app))??app;if(!choices.Any(a=>Catalog.Same(a,installed)))choices.Add(installed);if(choices.Count==8)break;}return choices;}
        Border Card(string title,UIElement body,Action extra=null)
        {
            var panel=new StackPanel();var head=new Grid {Margin=new Thickness(0,0,0,16)};var text=Label(title,17,Paint.Ink);text.FontWeight=FontWeights.SemiBold;head.Children.Add(text);
            if(extra!=null){var clear=Paint.Action("모두 삭제",extra);clear.Background=Brushes.Transparent;clear.HorizontalAlignment=HorizontalAlignment.Right;clear.Padding=new Thickness(8,2,8,2);head.Children.Add(clear);}panel.Children.Add(head);panel.Children.Add(body);
            var card=new Border {Background=QuickPanel.Card,CornerRadius=new CornerRadius(28),Padding=new Thickness(16,16,16,16),Margin=new Thickness(0,0,0,12),Child=panel};cards.Children.Add(card);return card;
        }
        static TextBlock Label(string text,double size,Brush color) {var label=Paint.TextBlock(text,size,color==Paint.Ink?Paint.Text:color==LightPanel.Muted?Paint.Muted:color);label.TextTrimming=TextTrimming.CharacterEllipsis;return label;}
        void AppCard(string title,List<AppEntry> apps)
        {
            var wrap=new WrapPanel();foreach(var entry in apps){var app=entry;var body=new StackPanel {Width=88,Height=96};FrameworkElement icon=AppIcons.Visual(app,44);icon.HorizontalAlignment=HorizontalAlignment.Center;icon.Margin=new Thickness(0,4,0,9);body.Children.Add(icon);var text=Label(app.Name,12,Paint.Ink);text.TextAlignment=TextAlignment.Center;text.TextWrapping=TextWrapping.Wrap;text.MaxHeight=34;body.Children.Add(text);
                Action launch=()=>Open(()=>app.Launch(),app.Name);var button=Paint.Button(body,launch,app.Name);button.Padding=new Thickness(3);button.Margin=new Thickness(2);button.Background=Brushes.Transparent;button.ContextMenu=WindowPicker.Menu(app,shell,allowAdmin:true);Navigation.Add(button,launch);wrap.Children.Add(button);}Card(title,wrap);
        }
        void SettingCard(string title,IEnumerable<SearchSetting> settings)
        {
            var wrap=new WrapPanel();foreach(var entry in settings){var setting=entry;var row=new StackPanel {Orientation=Orientation.Horizontal};row.Children.Add(new Glyph(setting.Icon,18){Color=Paint.Text,Margin=new Thickness(0,0,7,0)});row.Children.Add(Label(setting.Name,13,Paint.Ink));Action launch=()=>Open(()=> {if(setting.Uri==null)shell.ShowWelcome();else Native.Open(setting.Uri);},setting.Name);var button=Paint.Button(row,launch,setting.Name);button.Background=Paint.Brush(QuickPanel.Inactive);button.Margin=new Thickness(0,0,8,7);button.Padding=new Thickness(13,9,13,9);Navigation.Add(button,launch);wrap.Children.Add(button);}Card(title,wrap);
        }
        void RecentCard()
        {
            var wrap=new WrapPanel();if(history.Items.Count==0)wrap.Children.Add(Label("검색 결과를 열면 여기에 최근 검색이 표시됩니다",12,LightPanel.Muted));
            foreach(string saved in history.Items.ToArray()){string query=saved;var row=new StackPanel {Orientation=Orientation.Horizontal};var repeat=Paint.Action(query,()=> {Query.Text=query;Query.Focus();Query.CaretIndex=Query.Text.Length;});repeat.Background=Brushes.Transparent;repeat.Padding=new Thickness(12,7,5,7);Navigation.Add(repeat,()=> {Query.Text=query;Query.Focus();Query.CaretIndex=Query.Text.Length;});row.Children.Add(repeat);var remove=Paint.Button(new Glyph("close",13){Color=Paint.Text},()=> {history.Remove(query);Reload();},"최근 검색 삭제");remove.Background=Brushes.Transparent;remove.Padding=new Thickness(6);row.Children.Add(remove);wrap.Children.Add(new Border {CornerRadius=new CornerRadius(20),Background=Paint.Brush(QuickPanel.Inactive),Child=row,Margin=new Thickness(0,0,8,7)});}Card("최근 검색",wrap,()=> {history.Clear();Reload();});
        }
        async Task FindFiles()
        {
            if(closed)return;string query=Query.Text.Trim();int ticket=generation;var source=new CancellationTokenSource();cancellation=source;
            try {var matches=await Task.Run(()=>LauncherSearch.Files(query,source.Token));if(closed||ticket!=generation||source.IsCancellationRequested)return;
                files.Children.Clear();if(matches.Count==0){files.Children.Add(Label("일치하는 파일이 없습니다",13,LightPanel.Muted));return;}
                var pictures=matches.Where(f=>f.Picture).Take(8).ToList();var ordinary=matches.Where(f=>!pictures.Contains(f)).ToList();filesCard.Visibility=ordinary.Count==0&&pictures.Count>0?Visibility.Collapsed:Visibility.Visible;foreach(var item in ordinary){var file=item;var row=new Grid {Margin=new Thickness(0,2,0,2)};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});row.ColumnDefinitions.Add(new ColumnDefinition());FrameworkElement icon=file.Icon!=null?AppIcons.Visual(file.Icon,31):new Glyph("window",27){Color=Paint.Text};row.Children.Add(icon);var labels=new StackPanel {Margin=new Thickness(8,0,0,0)};labels.Children.Add(Label(file.Name,14,Paint.Ink));labels.Children.Add(Label(Path.GetDirectoryName(file.Path),10,LightPanel.Muted));Grid.SetColumn(labels,1);row.Children.Add(labels);Action launch=()=>Open(()=>Native.Open(file.Path),file.Name);var button=Paint.Button(row,launch,file.Path);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Background=Brushes.Transparent;button.Padding=new Thickness(0,8,0,8);Navigation.Add(button,launch);files.Children.Add(button);files.Children.Add(new Border {Height=.5,Background=Paint.Brush("#24FFFFFF")});}
                if(pictures.Count>0){var wrap=new WrapPanel();foreach(var item in pictures){var file=item;var thumb=new Border {Width=126,Height=98,CornerRadius=new CornerRadius(15),Background=file.Icon!=null?(Brush)new ImageBrush(file.Icon){Stretch=Stretch.UniformToFill}:Paint.Brush("#24FFFFFF")};var body=new StackPanel();body.Children.Add(thumb);var caption=Label(file.Name,11,Paint.Ink);caption.Width=126;caption.Margin=new Thickness(0,5,0,0);body.Children.Add(caption);Action launch=()=>Open(()=>Native.Open(file.Path),file.Name);var button=Paint.Button(body,launch,file.Path);button.Padding=new Thickness(0);button.Margin=new Thickness(0,8,10,0);button.Background=Brushes.Transparent;Navigation.Add(button,launch);wrap.Children.Add(button);}Card("사진",wrap);}
            }catch(OperationCanceledException){}catch(Exception){if(ticket==generation){files.Children.Clear();files.Children.Add(Label("파일 검색을 사용할 수 없습니다. 앱과 설정은 계속 검색할 수 있습니다",12,LightPanel.Muted));}}
            finally {if(cancellation==source)cancellation=null;source.Dispose();}
        }
        async void Open(Action action,string fallback) {history.Add(Query.Text.Trim().Length>0?Query.Text.Trim():fallback);if(await DismissAsync()&&!closed)action();}
    }
}
