using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace OneTaskBar
{
    internal sealed class QuickWidgetTile : Grid
    {
        internal readonly Glyph Icon;
        internal readonly QuickToggleButton Toggle;
        readonly Button main;
        readonly StackPanel words;
        readonly TextBlock name,caption;
        readonly bool hasDetail;
        QuickWidgetSlot slot;
        internal QuickWidgetTile(string icon,string title,Action action,Action iconAction=null)
        {
            hasDetail=iconAction!=null;
            main=Paint.Button(new Border(),action,title);
            main.Background=Brushes.Transparent;
            main.Padding=new Thickness(0);
            main.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            main.VerticalContentAlignment=VerticalAlignment.Stretch;
            Children.Add(main);
            Icon=new Glyph(icon,25);
            Toggle=(QuickToggleButton)Paint.Button(Icon,iconAction??action,title,true);
            Toggle.Padding=new Thickness(0);
            Toggle.Resources["ButtonRadius"]=new CornerRadius(40);
            Toggle.RefreshState(false,true);
            Children.Add(Toggle);
            if(hasDetail)
            {
                var menu=new ContextMenu();
                var detail=new MenuItem
                {
                    Header=title+" 연결 관리"
                };
                detail.Click+=(s,e)=>action();
                menu.Items.Add(detail);
                ContextMenu=menu;
                AutomationProperties.SetHelpText(Toggle,"켜기·끄기. 오른쪽 클릭 또는 Shift+F10으로 연결 관리를 엽니다.");
            }
            name=Paint.TextBlock(title,13,Paint.Text);
            name.FontWeight=FontWeights.SemiBold;
            caption=Paint.TextBlock("",10.5,Paint.Muted);
            caption.Visibility=Visibility.Collapsed;
            name.TextTrimming=caption.TextTrimming=TextTrimming.CharacterEllipsis;
            words=new StackPanel
            {
                IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center
            };
            words.Children.Add(name);
            words.Children.Add(caption);
            Children.Add(words);
            SizeChanged+=(s,e)=>Place();
        }
        internal string Caption
        {
            get
            {
                return caption.Text;
            }
        }
        internal string Title
        {
            get
            {
                return name.Text;
            }
        }
        internal TextBlock NameLabel
        {
            get
            {
                return name;
            }
        }
        internal bool CaptionVisible
        {
            get
            {
                return caption.Visibility==Visibility.Visible;
            }
        }
        internal void Text(string title,string subtitle)
        {
            subtitle=subtitle??"";
            if(name.Text==title&&caption.Text==subtitle)return;
            name.Text=title;
            caption.Text=subtitle;
            caption.Visibility=String.IsNullOrWhiteSpace(subtitle)?Visibility.Collapsed:Visibility.Visible;
            main.ToolTip=title+(String.IsNullOrWhiteSpace(subtitle)?"":" · "+subtitle);
            Toggle.ToolTip=hasDetail?main.ToolTip+" · 오른쪽 클릭: 연결 관리":main.ToolTip;
            AutomationProperties.SetName(main,title);
        }
        internal void State(bool enabled,bool available)
        {
            Toggle.RefreshState(enabled,available);
            main.IsEnabled=available;
            main.Opacity=available?1:.45;
        }
        internal void Shape(QuickWidgetSlot value)
        {
            if(slot!=null&&slot.Columns==value.Columns&&slot.Rows==value.Rows)return;
            slot=value.Copy();
            Place();
        }
        void Place()
        {
            if(slot==null)return;
            bool solo=slot.Columns==1&&slot.Rows==1,tall=slot.Rows>1;
            double size=solo?Math.Max(28,Math.Min(ActualWidth,ActualHeight)-2):Math.Min(42,Math.Max(28,Math.Min(ActualWidth-12,ActualHeight-10)));
            Toggle.Width=Toggle.Height=size;
            Toggle.HorizontalAlignment=solo?HorizontalAlignment.Center:HorizontalAlignment.Left;
            Toggle.VerticalAlignment=tall?VerticalAlignment.Top:VerticalAlignment.Center;
            Toggle.Margin=solo?new Thickness(0):new Thickness(tall?5:7,tall?7:0,0,0);
            Icon.Width=Icon.Height=solo?25:22;
            words.Visibility=solo?Visibility.Collapsed:Visibility.Visible;
            words.VerticalAlignment=tall?VerticalAlignment.Bottom:VerticalAlignment.Center;
            words.Margin=tall?new Thickness(10,0,10,14):new Thickness(size+18,0,8,0);
            name.TextAlignment=caption.TextAlignment=tall?TextAlignment.Right:TextAlignment.Left;
        }
    }
}
