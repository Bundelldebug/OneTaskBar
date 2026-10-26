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
    internal sealed partial class QuickGridSurface
    {
        internal void SetEditing(bool value)
        {
            if(Board.ContentsEditing)EditActions(false);
            if(value&&Expanded)ExpandToggles(false,false);
            Board.SetEditing(value);
            foreach(var pair in outsideMenus)Board.Frames[pair.Key].ContextMenu=value?pair.Value:null;
            ((TextBlock)EditButton.Content).Text=value?"완료":"배치 편집";
            hint.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        }
        internal void EditActions(bool value)
        {
            if(value&&!Board.Editing)return;
            Board.SetContentsEditing(value);
            ExpandToggles(value,false);
            if(value)
            {
                Board.Frames["toggles"].BringIntoView();
                string first=ActionOrder.Members.FirstOrDefault();
                if(first!=null)ActionItem(first).Focus();
            }
        }
        internal QuickWidgetPicker CreatePicker(Action close)
        {
            return new QuickWidgetPicker(Board.Layout.Items.Where(i=>i.Hidden).ToArray(),Title,key=>
            {
                if(ActionOrder.Contains(key))ExtractAction(key,null);else Board.AddWidget(key);close();
            });
        }
        void ShowAdd()
        {
            if(addMenu!=null)addMenu.IsOpen=false;
            var menu=new ContextMenu
            {
                PlacementTarget=EditButton
            };
            addMenu=menu;
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentSourceProperty,"Header");
            var item=new MenuItem
            {
                Header=CreatePicker(()=>menu.IsOpen=false),StaysOpenOnClick=true,Focusable=false,Padding=new Thickness(0),Template=new ControlTemplate(typeof(MenuItem))
                {
                    VisualTree=presenter
                }
            };
            menu.Items.Add(item);
            menu.IsOpen=true;
        }
        void CloseMenus()
        {
            if(addMenu!=null)addMenu.IsOpen=false;
            foreach(var tile in tiles.Values)if(tile.ContextMenu!=null)tile.ContextMenu.IsOpen=false;
            foreach(var menu in insideMenus.Values.Concat(outsideMenus.Values))menu.IsOpen=false;
        }
        string Title(string key)
        {
            return tiles.ContainsKey(key)?tiles[key].Title:key=="brightness"?"밝기":key=="volume"?"음량":key=="media"?"미디어 플레이어":"기능 토글";
        }
        void PresentToggles(QuickWidgetSlot slot)
        {
            if(presentedColumns==slot.Columns&&presentedRows==slot.Rows&&presentedExpanded==Expanded&&presentedContents==Board.ContentsEditing)return;
            presentedColumns=slot.Columns;
            presentedRows=slot.Rows;
            presentedExpanded=Expanded;
            presentedContents=Board.ContentsEditing;
            actionGrid.Children.Clear();
            var members=ActionOrder.Members;
            actionScroll.VerticalScrollBarVisibility=Expanded?ScrollBarVisibility.Hidden:ScrollBarVisibility.Disabled;
            actionGrid.MinHeight=Expanded&&members.Length>16?Math.Ceiling(members.Length/4.0)*86:0;
            actionGrid.Columns=slot.Rows==1?Math.Max(1,Math.Min(slot.Columns,members.Length)):slot.Columns;
            actionGrid.Rows=Expanded?Math.Max(1,(members.Length+3)/4):slot.Rows;
            double inset=slot.Rows==1?0:7;
            actionGrid.Margin=Expanded?new Thickness(10,12,10,Board.ContentsEditing?28:22):new Thickness(3,inset,3,inset);
            foreach(var item in actionItems)
            {
                item.Visibility=Visibility.Collapsed;
                item.ContextMenu=null;
            }
            int count=0;
            foreach(string key in members)
            {
                var item=actionItems.First(i=>(string)i.Tag==key);
                bool visible=count++<slot.Columns*slot.Rows||Expanded;
                item.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
                if(visible)actionGrid.Children.Add(item);
                item.Focusable=Board.ContentsEditing;
                ContextMenu menu;
                item.ContextMenu=Board.ContentsEditing&&insideMenus.TryGetValue(key,out menu)?menu:tiles.ContainsKey(key)?tiles[key].ContextMenu:null;
                Toggles[key].IsHitTestVisible=Toggles[key].Focusable=!Board.ContentsEditing;
                ((TextBlock)item.Children[1]).Visibility=Expanded||slot.Rows>1?Visibility.Visible:Visibility.Collapsed;
                ((TextBlock)item.Children[1]).Margin=new Thickness(0,Expanded?6:0,0,0);
            }
            emptyActions.Visibility=members.Length==0?Visibility.Visible:Visibility.Collapsed;
            groupMore.Content=Board.ContentsEditing?(UIElement)groupBack:groupBar;
            groupMore.Width=Board.ContentsEditing?160:52;
            groupMore.Height=Board.ContentsEditing?23:13;
            groupMore.ToolTip=Board.ContentsEditing?"크기 조절로 돌아가기":Expanded?"기능 토글 접기":"기능 토글 펼치기";
            AutomationProperties.SetName(groupMore,(string)groupMore.ToolTip);
            SizeActions();
        }
        internal bool ExtractAction(string key,Point? destination)
        {
            if(!ActionOrder.Extract(key))return false;
            bool wasHidden=Board.Layout.Find(key).Hidden;
            if(Board.ContentsEditing)EditActions(false);
            Board.Layout.SetHidden(key,false);
            if(wasHidden)
            {
                Board.Layout.Resize(key,1,1,false);
                Board.Layout.PlaceFirst(key);
            }
            if(destination.HasValue)
            {
                var group=Board.Layout.Find("toggles");
                Point cell=destination.HasValue?Board.CellAt(destination.Value):new Point(group.X,group.Y+group.Rows);
                Board.Layout.Move(key,(int)cell.X,(int)cell.Y);
            }
            presentedColumns=0;
            Board.Save();
            Board.Reflow(true);
            PresentToggles(Board.DisplayLayout.Find("toggles"));
            Board.Frames[key].BringIntoView();
            return true;
        }
        internal bool RegroupAction(string key)
        {
            if(!QuickToggleOrder.Defaults.Contains(key))return false;
            if(!ActionOrder.Contains(key)&&!ActionOrder.Regroup(key))return false;
            if(Board.ContentsEditing)EditActions(false);
            Board.Layout.SetHidden(key,true);
            Board.Layout.SetHidden("toggles",false);
            presentedColumns=0;
            Board.Save();
            Board.Reflow(true);
            PresentToggles(Board.DisplayLayout.Find("toggles"));
            Board.Frames["toggles"].BringIntoView();
            return true;
        }
        void SizeActions()
        {
            if(actionGrid.Columns<1||actionGrid.Rows<1)return;
            bool labels=Expanded||actionGrid.Rows>1;
            double size=Math.Max(28,Math.Min(48,Math.Min(actionGrid.ActualWidth/actionGrid.Columns-4,actionGrid.ActualHeight/actionGrid.Rows-(labels?(Expanded?22:16):0)-2)));
            foreach(var button in Toggles.Values)
            {
                button.Width=button.Height=size;
                button.Resources["ButtonRadius"]=new CornerRadius(size/2);
                var glyph=button.Content as Glyph;
                if(glyph!=null)glyph.Width=glyph.Height=size*.5;
            }
        }
        internal bool MoveAction(string source,string target,bool after)
        {
            var old=actionGrid.Children.OfType<FrameworkElement>().ToDictionary(i=>(string)i.Tag,i=>i.TranslatePoint(new Point(),actionGrid));
            if(!ActionOrder.Move(source,target,after))return false;
            presentedColumns=0;
            PresentToggles(Board.DisplayLayout.Find("toggles"));
            actionGrid.UpdateLayout();
            foreach(FrameworkElement item in actionGrid.Children)
            {
                Point from;
                if(!old.TryGetValue((string)item.Tag,out from))continue;
                Point to=item.TranslatePoint(new Point(),actionGrid);
                var previous=item.RenderTransform as TranslateTransform;
                double x=from.X-to.X+(previous==null?0:previous.X),y=from.Y-to.Y+(previous==null?0:previous.Y);
                var move=new TranslateTransform();
                item.RenderTransform=move;
                if(Motion.Enabled&&host.IsVisible)
                {
                    move.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(x,0,170));
                    move.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(y,0,170));
                }
            }
            if(Board.ContentsEditing)ActionItem(source).Focus();
            return true;
        }
        internal void ExpandToggles(bool value,bool animate=true)
        {
            Board.ExpandedToggleRows=Math.Max(2,Math.Min(5,(ActionOrder.Members.Length+3)/4+1));
            Board.SetExpanded(value,animate);
            PresentToggles(Board.DisplayLayout.Find("toggles"));
            if(animate&&host.IsVisible)Motion.Fade(actionGrid,.7,1,170);
        }
    }
}
