using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class EmptySettingsText : IValueConverter
    {
        public object Convert(object value,Type type,object parameter,CultureInfo culture){return String.IsNullOrWhiteSpace(value as string)?Visibility.Collapsed:Visibility.Visible;}
        public object ConvertBack(object value,Type type,object parameter,CultureInfo culture){throw new NotSupportedException();}
    }
    internal sealed partial class WelcomeWindow
    {
        sealed class Section {internal string Name,Search;internal UIElement Content;internal Border Card;internal Button Entry;}
        sealed class Category {internal StackPanel Rows;internal Border Card;internal Section[] Sections;}
        readonly List<Section> sections=new List<Section>();readonly List<Category> categories=new List<Category>();readonly List<UIElement> versionElements=new List<UIElement>();
        StackPanel settingsHome,settingsDetail;SmoothScrollViewer settingsScroll;TextBlock settingsHeading,emptySettings;Button settingsBack;TextBox settingsSearch;
        SettingsPageMotion pageMotion;
        double settingsHomeOffset;
        Border settingsExit;
        internal Button ExitSettingsButton {get;private set;}
        internal bool SettingsAnimating {get{return pageMotion!=null&&pageMotion.Active;}}
        internal SmoothScrollViewer SettingsViewport {get{return settingsScroll;}}
        internal bool SettingsSnapshotHeld {get{return pageMotion!=null&&pageMotion.HoldsSnapshot;}}
        internal string SettingsPage {get;private set;}
        internal TextBox SearchSettings {get{return settingsSearch;}}
        internal int SettingsSections {get{return sections.Count;}}
        internal int VisibleSettingsSections {get{return sections.Count(s=>s.Entry.Visibility==Visibility.Visible);}}
        internal int VisibleSettingsCards {get{return categories.Count(c=>c.Card.Visibility==Visibility.Visible);}}
        internal bool SettingsNoResults {get{return emptySettings!=null&&emptySettings.Visibility==Visibility.Visible;}}
        Grid SettingsHeader()
        {
            var header=new Grid {Height=68,Margin=new Thickness(0,0,0,12),Background=Brushes.Transparent};
            settingsHeading=Paint.TextBlock("설정",28,AppSettingsPalette.Text);settingsHeading.FontWeight=FontWeights.SemiBold;settingsHeading.Margin=new Thickness(7,0,48,0);header.Children.Add(settingsHeading);
            settingsBack=Paint.Button(new Glyph("back",23){Color=AppSettingsPalette.Text},SettingsHome,"설정 홈으로 돌아가기");settingsBack.Width=settingsBack.Height=38;settingsBack.Padding=new Thickness(0);settingsBack.Background=Brushes.Transparent;settingsBack.HorizontalAlignment=HorizontalAlignment.Left;settingsBack.Visibility=Visibility.Collapsed;header.Children.Add(settingsBack);
            var close=Paint.Button(new Glyph("close",19){Color=AppSettingsPalette.Text},()=>Hide(),"설정 닫기");close.Width=close.Height=36;close.Padding=new Thickness(0);close.Background=Brushes.Transparent;close.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(close);
            return header;
        }
        void DragSettingsHeader(object sender,MouseButtonEventArgs e)
        {
            if(e.LeftButton!=MouseButtonState.Pressed||e.GetPosition(this).Y>92)return;
            DependencyObject node=e.OriginalSource as DependencyObject;
            while(node!=null&&node!=this)
            {
                if(node is ButtonBase||node is TextBoxBase||node is ToggleSwitch||node is System.Windows.Controls.Primitives.ScrollBar)return;
                node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);
            }
            DragMove();
            e.Handled=true;
        }
        static IEnumerable<TextBlock> SettingsWords(DependencyObject root)
        {
            var text=root as TextBlock;if(text!=null)yield return text;foreach(object item in LogicalTreeHelper.GetChildren(root)){var child=item as DependencyObject;if(child!=null)foreach(var word in SettingsWords(child))yield return word;}
        }
        static Border SettingSeparator(){return new Border {Height=.6,Background=AppSettingsPalette.Stroke,Margin=new Thickness(16,0,16,0),IsHitTestVisible=false};}
        Border SettingsCard(UIElement content,bool separators=true)
        {
            var stack=content as StackPanel;if(separators&&stack!=null)for(int i=stack.Children.Count-1;i>0;i--)stack.Children.Insert(i,SettingSeparator());
            AppSettingsPalette.Apply(content);return new Border {Background=AppSettingsPalette.Card,CornerRadius=new CornerRadius(24),Padding=new Thickness(3),Child=content,Margin=new Thickness(0,0,0,16)};
        }
        void Group(StackPanel target,string label,UIElement content)
        {
            if(label=="UI 버전"){var heading=Paint.TextBlock(label,14,AppSettingsPalette.Muted);heading.Margin=new Thickness(15,0,15,8);var card=SettingsCard(content);target.Children.Add(heading);target.Children.Add(card);versionElements.Add(heading);versionElements.Add(card);return;}
            sections.Add(new Section {Name=label,Content=content,Search=label+" "+String.Join(" ",SettingsWords(content).Select(t=>t.Text))});
        }
        static string SettingsIcon(string name)
        {
            switch(name){case "데스크톱":return "desktops";case "패널 선택":return "notification";case "시작과 검색":return "search";case "미디어 플레이어":return "play";case "복구":return "shield";case "PC 호환성":return "computer";case "화면":return "brightness";case "움직임":return "motion";case "충전 소리":return "volume";default:return "settings";}
        }
        void FilterSettings()
        {
            if(settingsSearch==null)return;string term=settingsSearch.Text.Trim();bool searching=term.Length>0;foreach(var element in versionElements)element.Visibility=searching?Visibility.Collapsed:Visibility.Visible;
            foreach(var category in categories){category.Rows.Children.Clear();foreach(var section in category.Sections){bool match=!searching||section.Search.IndexOf(term,StringComparison.CurrentCultureIgnoreCase)>=0;section.Entry.Visibility=match?Visibility.Visible:Visibility.Collapsed;if(!match)continue;if(category.Rows.Children.Count>0)category.Rows.Children.Add(SettingSeparator());category.Rows.Children.Add(section.Entry);}category.Card.Visibility=category.Rows.Children.Count==0?Visibility.Collapsed:Visibility.Visible;}
            bool exitMatch=!searching||"종료 Windows 작업표시줄 복원".IndexOf(term,StringComparison.CurrentCultureIgnoreCase)>=0;
            if(settingsExit!=null)settingsExit.Visibility=exitMatch?Visibility.Visible:Visibility.Collapsed;
            emptySettings.Visibility=searching&&VisibleSettingsSections==0&&!exitMatch?Visibility.Visible:Visibility.Collapsed;if(String.IsNullOrEmpty(SettingsPage))settingsHeading.Text=searching?"검색 결과":"설정";
            settingsHomeOffset=0;
            settingsScroll.ResetForNavigation();
        }
        void SettingsPages(DockPanel root,StackPanel groups)
        {
            settingsHome=groups;settingsDetail=new StackPanel();settingsScroll=new SmoothScrollViewer {Content=groups,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,5,0)};settingsScroll.Resources[SystemParameters.VerticalScrollBarWidthKey]=4.0;
            var searchGrid=new Grid {Margin=new Thickness(14,0,8,0)};searchGrid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(30)});searchGrid.ColumnDefinitions.Add(new ColumnDefinition());searchGrid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(30)});searchGrid.Children.Add(new Glyph("search",18){Color=AppSettingsPalette.Muted});
            settingsSearch=new TextBox {FontSize=15,Foreground=AppSettingsPalette.Text,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(4,9,4,9),MaxLength=100};Grid.SetColumn(settingsSearch,1);searchGrid.Children.Add(settingsSearch);
            var hint=Paint.TextBlock("설정 검색",15,AppSettingsPalette.Muted);hint.IsHitTestVisible=false;hint.Margin=new Thickness(4,0,0,0);Grid.SetColumn(hint,1);searchGrid.Children.Add(hint);
            var clear=Paint.Button(new Glyph("close",15){Color=AppSettingsPalette.Muted},()=>settingsSearch.Clear(),"검색어 지우기");clear.Width=clear.Height=28;clear.Padding=new Thickness(0);clear.Background=Brushes.Transparent;clear.Visibility=Visibility.Collapsed;Grid.SetColumn(clear,2);searchGrid.Children.Add(clear);
            settingsSearch.TextChanged+=(s,e)=>{hint.Visibility=settingsSearch.Text.Length==0?Visibility.Visible:Visibility.Collapsed;clear.Visibility=settingsSearch.Text.Length==0?Visibility.Collapsed:Visibility.Visible;if(!String.IsNullOrEmpty(SettingsPage))SettingsHome();FilterSettings();};
            settingsSearch.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Enter){var section=sections.FirstOrDefault(t=>t.Entry.Visibility==Visibility.Visible);if(section!=null)OpenSettings(section.Name);e.Handled=true;}else if(e.Key==Key.Escape&&!String.IsNullOrEmpty(settingsSearch.Text)){settingsSearch.Clear();e.Handled=true;}};
            var searchPill=new Border {Background=AppSettingsPalette.Card,BorderBrush=AppSettingsPalette.Stroke,BorderThickness=new Thickness(.7),CornerRadius=new CornerRadius(23),Height=46,MaxWidth=420,Margin=new Thickness(35,10,35,0),Child=searchGrid};DockPanel.SetDock(searchPill,Dock.Bottom);root.Children.Add(searchPill);
            var colors=new[]{"#FF3478FD","#FFEF741D","#FF7868CF","#FF55AF39","#FF6D63B9","#FFDA9B20","#FF3B92CC","#FF5DA540","#FF7868CF"};
            foreach(var section in sections){int index=sections.IndexOf(section);var row=new Grid {Height=66,Margin=new Thickness(15,5,15,5)};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(46)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(24)});
                row.Children.Add(new Border {Width=32,Height=32,CornerRadius=new CornerRadius(16),Background=Paint.Brush(colors[index%colors.Length]),Child=new Glyph(SettingsIcon(section.Name),19){Color=Paint.ControlWhite},HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center});
                var name=Paint.TextBlock(section.Name,18,AppSettingsPalette.Text);Grid.SetColumn(name,1);row.Children.Add(name);var chevron=new Glyph("chevron",16){Color=AppSettingsPalette.Muted};Grid.SetColumn(chevron,2);row.Children.Add(chevron);
                var selected=section;section.Entry=Paint.Button(row,()=>OpenSettings(selected.Name),section.Name);section.Entry.Background=Brushes.Transparent;section.Entry.Padding=new Thickness(0);section.Entry.HorizontalContentAlignment=HorizontalAlignment.Stretch;section.Entry.VerticalContentAlignment=VerticalAlignment.Stretch;
            }
            for(int start=0;start<sections.Count;start+=3){var category=new Category {Rows=new StackPanel(),Sections=sections.Skip(start).Take(3).ToArray()};category.Card=SettingsCard(category.Rows,false);categories.Add(category);groups.Children.Add(category.Card);}
            emptySettings=Paint.TextBlock("검색 결과가 없습니다",16,AppSettingsPalette.Muted);emptySettings.Margin=new Thickness(15,20,15,20);emptySettings.HorizontalAlignment=HorizontalAlignment.Center;emptySettings.Visibility=Visibility.Collapsed;groups.Children.Add(emptySettings);
            settingsExit=SettingsExitRow();groups.Children.Add(settingsExit);
            pageMotion=new SettingsPageMotion(settingsScroll);root.Children.Add(pageMotion.View);FilterSettings();SettingsHome();
        }
        internal void OpenSettings(string name)
        {
            var section=sections.FirstOrDefault(s=>s.Name==name);
            if(section==null||SettingsPage==name)return;
            if(SettingsPage==null)settingsHomeOffset=settingsScroll.VerticalOffset;
            pageMotion.Prepare();
            SettingsPage=section.Name;
            settingsHeading.Text=section.Name;
            settingsHeading.Margin=new Thickness(48,0,48,0);
            settingsBack.Visibility=Visibility.Visible;
            settingsDetail.Children.Clear();
            if(section.Card==null)section.Card=SettingsCard(section.Content);
            settingsDetail.Children.Add(section.Card);
            settingsScroll.Content=settingsDetail;
            settingsScroll.ResetForNavigation();
            pageMotion.Start(true);
        }
        internal void SettingsHome()
        {
            bool returning=SettingsPage!=null;
            if(pageMotion!=null)
            {
                if(returning)pageMotion.Prepare();
                else pageMotion.Cancel();
            }
            SettingsPage=null;
            if(settingsHeading!=null)
            {
                settingsHeading.Text=settingsSearch!=null&&!String.IsNullOrWhiteSpace(settingsSearch.Text)?"검색 결과":"설정";
                settingsHeading.Margin=new Thickness(7,0,48,0);
            }
            if(settingsBack!=null)settingsBack.Visibility=Visibility.Collapsed;
            if(settingsScroll!=null)
            {
                settingsScroll.Content=settingsHome;
                settingsScroll.ResetForNavigation(settingsHomeOffset);
            }
            if(returning&&pageMotion!=null)pageMotion.Start(false);
        }
        Border SettingsExitRow()
        {
            var ink=Paint.Brush(AppSettingsPalette.Dark?"#FFFF8585":"#FFC52E40");
            var row=new Grid {Margin=new Thickness(15,12,15,12),MinHeight=50};
            row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(46)});row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new Glyph("power",23){Color=ink,HorizontalAlignment=HorizontalAlignment.Left});
            var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            words.Children.Add(Paint.TextBlock("One TaskBar 종료",17,ink));
            var description=Paint.TextBlock("Windows 작업표시줄을 복원합니다",12,AppSettingsPalette.Muted);description.Margin=new Thickness(0,5,0,0);words.Children.Add(description);
            Grid.SetColumn(words,1);row.Children.Add(words);
            var button=Paint.Button(row,()=>Application.Current.Shutdown(),"One TaskBar 종료 · Windows 작업표시줄 복원");
            button.Padding=new Thickness(0);button.Background=Brushes.Transparent;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            ExitSettingsButton=button;return SettingsCard(button,false);
        }
    }
}

