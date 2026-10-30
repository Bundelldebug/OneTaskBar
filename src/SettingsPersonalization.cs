using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        void ThemeSetting(StackPanel rows)
        {
            var row=new StackPanel {Margin=new Thickness(15,14,15,14)};row.Children.Add(Paint.TextBlock("One TaskBar 색 모드",15,AppSettingsPalette.Text));var choices=new System.Windows.Controls.Primitives.UniformGrid {Columns=3,Margin=new Thickness(0,10,0,0)};var buttons=new Button[3];
            Action refresh=()=>{for(int i=0;i<3;i++){bool selected=AppTheme.Mode==i;buttons[i].Background=selected?AppSettingsPalette.Accent:AppSettingsPalette.AccentLight;((TextBlock)buttons[i].Content).Foreground=selected?Paint.ControlWhite:AppSettingsPalette.Text;}};
            for(int i=0;i<3;i++){int value=i;buttons[i]=Paint.Action(new[]{"Windows 따르기","라이트","다크"}[i],()=>{Preferences.Set(p=>p.UITheme=value);refresh();});buttons[i].MinHeight=38;buttons[i].Margin=new Thickness(2);choices.Children.Add(buttons[i]);}refresh();row.Children.Add(choices);rows.Children.Add(row);
            var dockRow=new StackPanel {Margin=new Thickness(15,3,15,14),Visibility=Preferences.Current.SeparateDockTheme?Visibility.Visible:Visibility.Collapsed};dockRow.Children.Add(Paint.TextBlock("작업표시줄 색 모드",15,AppSettingsPalette.Text));var dockChoices=new System.Windows.Controls.Primitives.UniformGrid {Columns=3,Margin=new Thickness(0,10,0,0)};var dockButtons=new Button[3];Action dockRefresh=()=>{for(int i=0;i<3;i++){bool selected=Preferences.Current.DockTheme==i;dockButtons[i].Background=selected?AppSettingsPalette.Accent:AppSettingsPalette.AccentLight;((TextBlock)dockButtons[i].Content).Foreground=selected?Paint.ControlWhite:AppSettingsPalette.Text;}};
            Row(rows,"작업표시줄 색 모드 분리","독의 색 모드를 패널·시작 메뉴와 다르게 설정합니다",Preferences.Current.SeparateDockTheme,v=>{Preferences.Set(p=>p.SeparateDockTheme=v);dockRow.Visibility=v?Visibility.Visible:Visibility.Collapsed;});
            for(int i=0;i<3;i++){int value=i;dockButtons[i]=Paint.Action(new[]{"Windows 따르기","라이트","다크"}[i],()=>{Preferences.Set(p=>p.DockTheme=value);dockRefresh();});dockButtons[i].MinHeight=38;dockButtons[i].Margin=new Thickness(2);dockChoices.Children.Add(dockButtons[i]);}dockRefresh();dockRow.Children.Add(dockChoices);rows.Children.Add(dockRow);
        }
        void PersonalizeDock(StackPanel rows)
        {
            DockDisplaySetting(rows);
            DockScaleSetting(rows);
            ClockSetting(rows);
            Row(rows,"패널 색상을 작업표시줄과 맞추기","빠른 설정·알림·시작 메뉴에 선택한 작업표시줄 색상을 사용합니다",Preferences.Current.PanelsUseDockColor,v=>Preferences.Set(p=>p.PanelsUseDockColor=v));
            var placement=new StackPanel {Margin=new Thickness(15,14,15,14)};placement.Children.Add(Paint.TextBlock("작업표시줄 위치",15,AppSettingsPalette.Text));var positions=new System.Windows.Controls.Primitives.UniformGrid {Columns=4,Margin=new Thickness(0,10,0,0)};var positionButtons=new Button[4];
            Action refreshPosition=()=>{for(int n=0;n<4;n++){bool selected=DockGeometry.Edge==n;positionButtons[n].Background=selected?AppSettingsPalette.Accent:AppSettingsPalette.AccentLight;((TextBlock)positionButtons[n].Content).Foreground=selected?Paint.ControlWhite:AppSettingsPalette.Text;}};
            for(int n=0;n<4;n++){int edge=n;positionButtons[n]=Paint.Action(new[]{"왼쪽","위쪽","오른쪽","아래쪽"}[n],()=>{Preferences.Set(p=>p.DockEdge=edge);refreshPosition();});positionButtons[n].MinHeight=38;positionButtons[n].Margin=new Thickness(2);positions.Children.Add(positionButtons[n]);}refreshPosition();placement.Children.Add(positions);rows.Children.Add(placement);
            var group=new StackPanel {Margin=new Thickness(15,14,15,14)};group.Children.Add(Paint.TextBlock("작업표시줄 배경",15,AppSettingsPalette.Text));
            var kinds=new System.Windows.Controls.Primitives.UniformGrid {Columns=4,Margin=new Thickness(0,10,0,10)};var buttons=new Button[4];
            Action refresh=()=>{for(int i=0;i<buttons.Length;i++){bool selected=DockAppearance.Mode==i;buttons[i].Background=selected?AppSettingsPalette.Accent:AppSettingsPalette.AccentLight;((TextBlock)buttons[i].Content).Foreground=selected?Paint.ControlWhite:AppSettingsPalette.Text;}};
            for(int i=0;i<4;i++){int kind=i;buttons[i]=Paint.Action(new[]{"기본 재질","단색","이미지","아이콘만"}[i],()=>{Preferences.Set(p=>{p.DockBackgroundKind=kind;p.DockSolidBackground=kind==1;});refresh();});buttons[i].Margin=new Thickness(2);buttons[i].MinHeight=38;kinds.Children.Add(buttons[i]);}group.Children.Add(kinds);refresh();
            var actions=new StackPanel {Orientation=Orientation.Horizontal};var color=Paint.Action("색상 선택",()=>{Color current;DockAppearance.TryColor(Preferences.Current.DockBackgroundColor,out current);using(var picker=new System.Windows.Forms.ColorDialog {Color=System.Drawing.Color.FromArgb(current.R,current.G,current.B),FullOpen=true}){if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;string selected="#FF"+picker.Color.R.ToString("X2")+picker.Color.G.ToString("X2")+picker.Color.B.ToString("X2");Preferences.Set(p=>{p.DockBackgroundColor=selected;p.DockBackgroundKind=1;p.DockSolidBackground=true;});refresh();}});color.Margin=new Thickness(0,0,8,0);actions.Children.Add(color);
            actions.Children.Add(Paint.Action("이미지 선택",()=>{var picker=new Microsoft.Win32.OpenFileDialog {Title="작업표시줄 배경 이미지",Filter="이미지|*.png;*.jpg;*.jpeg;*.bmp;*.webp",CheckFileExists=true};if(picker.ShowDialog(this)!=true)return;Preferences.Set(p=>{p.DockBackgroundImage=picker.FileName;p.DockBackgroundKind=2;p.DockSolidBackground=false;});refresh();Feedback(Path.GetFileName(picker.FileName));}));group.Children.Add(actions);rows.Children.Add(group);
        }
    }
}
