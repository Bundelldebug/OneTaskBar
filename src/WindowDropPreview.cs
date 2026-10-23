using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    internal sealed class WindowDropPreview : IDisposable
    {
        TransientGlass preview,hint;WindowDropKind shown;string monitor;Rect bounds;bool disposed;
        internal bool Visible {get{return preview!=null&&preview.IsVisible;}}
        internal Window PreviewWindow {get{return preview;}}
        internal bool BlurAvailable {get{return preview!=null&&preview.HasBlur;}}
        internal void Update(WindowDropPlan plan,AppEntry entry,Forms.Screen screen)
        {
            if(disposed)return;if(plan.Kind==WindowDropKind.None){Hide();return;}
            if(hint==null||monitor!=screen.DeviceName){if(hint!=null)hint.Close();hint=new TransientGlass {Title="One TaskBar · 드래그 취소",Radius=22};hint.Surface.CornerRadius=new CornerRadius(22);hint.Surface.Background=QuickPanel.Glass;hint.Surface.Child=Paint.TextBlock("×  취소 · Esc",13,Paint.Text);((TextBlock)hint.Surface.Child).TextAlignment=TextAlignment.Center;double scale=Native.PrepareMonitor(hint,screen.Bounds);hint.ShowAt(screen.Bounds,((plan.Work.Left-screen.Bounds.Left)+plan.Work.Width/2.0)/scale-70,(plan.Work.Bottom-screen.Bounds.Top)/scale-46,140,34);hint.Reveal();}
            monitor=screen.DeviceName;hint.Surface.BorderBrush=plan.Kind==WindowDropKind.Cancel?Paint.ControlWhite:Paint.Brush("#60FFFFFF");if(!plan.Valid){if(preview!=null){preview.FadeAway();preview=null;}shown=plan.Kind;return;}
            var next=plan.Preview;if(preview!=null&&shown==plan.Kind&&next==bounds)return;if(preview!=null)preview.Close();preview=new TransientGlass {Title="One TaskBar · 창 배치 미리보기",Radius=24};preview.Surface.CornerRadius=new CornerRadius(24);preview.Surface.Background=Paint.Brush("#755A6868");preview.Surface.BorderBrush=Paint.Brush("#DDFFFFFF");preview.Surface.BorderThickness=new Thickness(1.5);
            var content=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MaxWidth=280};var icon=AppIcons.Visual(entry,52);icon.HorizontalAlignment=HorizontalAlignment.Center;content.Children.Add(icon);var name=Paint.TextBlock(entry.Name,16,Paint.Text);name.TextAlignment=TextAlignment.Center;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(12,14,12,6);content.Children.Add(name);var label=Paint.TextBlock(plan.Caption,12,Paint.Muted);label.TextAlignment=TextAlignment.Center;content.Children.Add(label);preview.Surface.Child=content;
            double dpi=Native.PrepareMonitor(preview,screen.Bounds);preview.ShowAt(screen.Bounds,(next.Left-screen.Bounds.Left)/dpi,(next.Top-screen.Bounds.Top)/dpi,next.Width/dpi,next.Height/dpi);preview.Reveal();shown=plan.Kind;bounds=next;
        }
        internal void Hide(){if(preview!=null){preview.FadeAway();preview=null;}if(hint!=null){hint.FadeAway();hint=null;}shown=WindowDropKind.None;monitor=null;}
        public void Dispose(){if(disposed)return;disposed=true;if(preview!=null)preview.Close();if(hint!=null)hint.Close();preview=hint=null;}
    }
}
