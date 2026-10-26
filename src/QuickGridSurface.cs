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
        readonly SmoothScrollViewer actionScroll=new SmoothScrollViewer
        {
            VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalContentAlignment=VerticalAlignment.Stretch
        };
        internal ScrollViewer ActionScroller
        {
            get
            {
                return actionScroll;
            }
        }
        readonly QuickWindow host;
        readonly ShellController shell;
        internal readonly QuickWidgetBoard Board;
        internal readonly QuickToggleOrder ActionOrder=new QuickToggleOrder(Path.Combine(AppLog.Data,"quick-toggle-order-9.json"));
        internal readonly DockPanel Root=new DockPanel
        {
            Margin=new Thickness(18,19,18,14)
        };
        internal readonly PillSlider Brightness=new PillSlider("brightness")
        {
            Height=Double.NaN,MinWidth=0,Light=true,NoTrackBorder=true,LinearFill=true
        },Volume=new PillSlider("volume")
        {
            Height=Double.NaN,MinWidth=0,Light=true,NoTrackBorder=true,LinearFill=true
        };
        internal readonly Dictionary<string,QuickToggleButton> Toggles=new Dictionary<string,QuickToggleButton>();
        readonly Dictionary<string,QuickWidgetTile> tiles=new Dictionary<string,QuickWidgetTile>();
        readonly BatteryPill battery=new BatteryPill
        {
            Width=62,Height=25
        };
        readonly TextBlock time=Paint.TextBlock("",23,Paint.Text),date=Paint.TextBlock("",11,Paint.Muted),notice=Paint.TextBlock("",11,Paint.Muted);
        readonly Button batteryButton,samsung;
        internal readonly Button EditButton;
        readonly Border hint;
        readonly Button groupMore;
        readonly Border groupBar=new Border
        {
            Width=27,Height=2.5,CornerRadius=new CornerRadius(1.25),Background=Paint.Brush("#88FFFFFF")
        };
        readonly TextBlock groupBack=Paint.TextBlock("크기 조절로 돌아가기",11,Paint.Text);
        readonly UniformGrid actionGrid=new UniformGrid();
        readonly List<StackPanel> actionItems=new List<StackPanel>();
        readonly Dictionary<string,ContextMenu> insideMenus=new Dictionary<string,ContextMenu>(),outsideMenus=new Dictionary<string,ContextMenu>();
        readonly TextBlock emptyActions=Paint.TextBlock("단독 토글에서 ‘그룹에 넣기’를 선택하세요",11,Paint.Muted);
        readonly MediaPlayerView media;
        readonly Grid mediaBody=new Grid(),miniMedia=new Grid();
        readonly Border richMedia;
        readonly Border mediaGlow=new Border
        {
            IsHitTestVisible=false
        };
        readonly Border miniArt=new Border
        {
            Width=36,Height=36,CornerRadius=new CornerRadius(10)
        };
        readonly Button miniArtwork,miniPlay,miniOutput;
        readonly Glyph miniPlayGlyph=new Glyph("play",22);
        readonly Glyph missingArt=new Glyph("volume",22);
        readonly TextBlock miniTitle=Paint.TextBlock("재생 중인 미디어 없음",12,Paint.Text);
        ContextMenu addMenu;
        bool closed,budsRequested,miniArtReady,presentedExpanded,presentedContents,mediaIdle;
        ImageSource budsSymbol,samsungIcon,miniArtSource;
        string samsungKey;
        long clock=-1,day=-1;
        int mediaColumns=4,mediaRows=2,presentedColumns,presentedRows;
        internal string BluetoothCaption
        {
            get
            {
                return tiles["bluetooth"].Caption;
            }
        }
        internal string DevicesLabel
        {
            get
            {
                return tiles["devices"].Title;
            }
        }
        internal bool BudsIconLoaded
        {
            get
            {
                return tiles["buds"].Icon.HasDeviceIcon;
            }
        }
        internal bool SamsungIconLoaded
        {
            get
            {
                return samsungIcon!=null;
            }
        }
        internal bool MediaBudsIcon
        {
            get
            {
                return media.OutputBudsIcon;
            }
        }
        internal bool MediaBackdropVisible
        {
            get
            {
                return mediaGlow.Visibility==Visibility.Visible&&mediaGlow.Background!=null;
            }
        }
        internal bool MediaPosterVisible
        {
            get
            {
                return media.LargeCoverVisible;
            }
        }
        internal bool MediaControlsFit
        {
            get
            {
                return media.PlayButton.ActualWidth+media.PlayButton.Margin.Left+media.PlayButton.Margin.Right+media.PreviousButton.ActualWidth+media.NextButton.ActualWidth<=Math.Max(0,media.ActualWidth)+.5;
            }
        }
        internal bool Expanded
        {
            get
            {
                return Board.Expanded;
            }
        }
        internal int VisibleActions
        {
            get
            {
                return actionItems.Count(i=>i.Visibility==Visibility.Visible);
            }
        }
        internal string[] VisibleActionKeys
        {
            get
            {
                return actionGrid.Children.OfType<StackPanel>().Select(i=>(string)i.Tag).ToArray();
            }
        }
        internal FrameworkElement ActionItem(string key)
        {
            return actionItems.First(i=>(string)i.Tag==key);
        }
        internal QuickGridSurface(QuickWindow window,ShellController controller)
        {
            host=window;
            shell=controller;
            Board=new QuickWidgetBoard(new QuickWidgetLayout(Path.Combine(AppLog.Data,"quick-layout-9.json")));
            Board.Changed+=()=>Feedback("배치를 저장하지 못했습니다. 다시 시도해 주세요.");
            Board.EditContents=()=>EditActions(true);
            var header=new Grid
            {
                Height=47,Margin=new Thickness(4,0,2,12)
            };
            var clockText=new StackPanel();
            time.FontWeight=FontWeights.SemiBold;
            clockText.Children.Add(time);
            clockText.Children.Add(date);
            var calendar=Paint.Button(clockText,()=>shell.ToggleCalendar(host),"달력 열기");
            calendar.Background=Brushes.Transparent;
            calendar.Padding=new Thickness(0);
            calendar.HorizontalAlignment=HorizontalAlignment.Left;
            header.Children.Add(calendar);
            var tools=new StackPanel
            {
                Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center
            };
            samsung=Tool("settings","삼성 설정",shell.OpenSamsungSettings);
            tools.Children.Add(samsung);
            var power=Tool("power","전원",()=>
            {
            });
            power.Click+=(s,e)=>host.BuildPowerMenu(power).IsOpen=true;
            tools.Children.Add(power);
            tools.Children.Add(Tool("home","One TaskBar 설정",shell.OpenHomeSettings));
            header.Children.Add(tools);
            DockPanel.SetDock(header,Dock.Top);
            Root.Children.Add(header);
            var footer=new Grid
            {
                Height=31,Margin=new Thickness(3,9,3,0)
            };
            batteryButton=Paint.Button(battery,()=>shell.ShowDetail("power"),"배터리 및 전원");
            batteryButton.Background=Brushes.Transparent;
            batteryButton.Padding=new Thickness(0);
            batteryButton.HorizontalAlignment=HorizontalAlignment.Left;
            footer.Children.Add(batteryButton);
            EditButton=Paint.Action("배치 편집",()=>SetEditing(!Board.Editing));
            EditButton.HorizontalAlignment=HorizontalAlignment.Center;
            EditButton.Padding=new Thickness(10,3,10,3);
            ((TextBlock)EditButton.Content).FontSize=11;
            footer.Children.Add(EditButton);
            var notifications=Paint.Action("알림  ›",()=>shell.ToggleNotifications(null));
            notifications.HorizontalAlignment=HorizontalAlignment.Right;
            notifications.Background=Brushes.Transparent;
            notifications.Padding=new Thickness(8,3,0,3);
            footer.Children.Add(notifications);
            DockPanel.SetDock(footer,Dock.Bottom);
            Root.Children.Add(footer);
            var scrolled=new DockPanel();
            notice.TextWrapping=TextWrapping.Wrap;
            notice.Visibility=Visibility.Collapsed;
            notice.Margin=new Thickness(4,3,4,9);
            DockPanel.SetDock(notice,Dock.Top);
            scrolled.Children.Add(notice);
            var editor=new StackPanel
            {
                Margin=new Thickness(3,2,3,10)
            };
            var instructions=Paint.TextBlock("메뉴 드래그 · 모서리로 크기 조절 · 기능 토글 안의 편집",10.5,Paint.Muted);
            instructions.TextWrapping=TextWrapping.Wrap;
            editor.Children.Add(instructions);
            var editTools=new StackPanel
            {
                Orientation=Orientation.Horizontal,Margin=new Thickness(0,6,0,0)
            };
            editTools.Children.Add(Paint.Action("항목 추가",ShowAdd));
            var reset=Paint.Action("배치 초기화",()=>
            {
                if(Board.ContentsEditing)EditActions(false);Board.Reset();
            });
            reset.Margin=new Thickness(6,0,0,0);
            editTools.Children.Add(reset);
            editor.Children.Add(editTools);
            hint=new Border
            {
                Child=editor,Visibility=Visibility.Collapsed
            };
            DockPanel.SetDock(hint,Dock.Top);
            scrolled.Children.Add(hint);
            var viewer=new SmoothScrollViewer
            {
                Content=Board,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false
            };
            Board.Scroller=viewer;
            scrolled.Children.Add(viewer);
            Root.Children.Add(scrolled);
            AddTile("wifi","wifi","Wi-Fi",()=>shell.ShowDetail("wifi"),()=>host.InvokeToggle("wifi"));
            AddTile("bluetooth","bluetooth","블루투스",()=>shell.ShowDetail("bluetooth"),()=>host.InvokeToggle("bluetooth"));
            AddTile("theme","moon","다크 모드",()=>host.InvokeToggle("theme"));
            AddTile("mute","volume","음소거",()=>host.InvokeToggle("mute"));
            AddTile("devices","devices","주변 기기 연결",()=>shell.OpenQuickDevices(false));
            AddTile("buds","bluetooth","Galaxy Buds",()=>shell.OpenQuickDevices(true));
            AddTile("windows","windows-settings","Windows 설정",shell.OpenWindowsSettings);
            AddTile("capture","screenshot","화면 캡처",shell.OpenScreenCapture);
            AddTile("manager","task-manager","작업 관리자",shell.OpenTaskManager);
            foreach(var spec in new[]
            {
                new[]
                {
                    "dnd","dnd","방해 금지"
                },new[]
                {
                    "efficiency","leaf","전력 절약"
                },new[]
                {
                    "awake","window","화면 유지"
                },new[]
                {
                    "animations","play","독 애니메이션"
                },new[]
                {
                    "startup","startup","자동 실행"
                },new[]
                {
                    "slider-scroll","scroll","스크롤 조절"
                }
            })
            {
                string key=spec[0];
                AddTile(key,spec[1],spec[2],()=>host.InvokeToggle(key));
            }
            foreach(var setting in QuickSettingCatalog.All)
            {
                var spec=setting;
                AddTile(spec.Key,spec.Icon,spec.Title,()=>host.InvokeToggle(spec.Key));
            }
            var grouped=new Grid();
            actionScroll.Content=actionGrid;
            grouped.Children.Add(actionScroll);
            emptyActions.TextWrapping=TextWrapping.Wrap;
            emptyActions.HorizontalAlignment=HorizontalAlignment.Center;
            emptyActions.VerticalAlignment=VerticalAlignment.Center;
            emptyActions.Margin=new Thickness(10);
            emptyActions.Visibility=Visibility.Collapsed;
            grouped.Children.Add(emptyActions);
            groupMore=Paint.Button(groupBar,()=>
            {
                if(Board.ContentsEditing)EditActions(false);else ExpandToggles(!Expanded);
            },"기능 토글 펼치기");
            groupMore.Width=52;
            groupMore.Height=13;
            groupMore.Padding=new Thickness(0);
            groupMore.Background=Brushes.Transparent;
            groupMore.HorizontalAlignment=HorizontalAlignment.Center;
            groupMore.VerticalAlignment=VerticalAlignment.Bottom;
            grouped.Children.Add(groupMore);
            actionGrid.SizeChanged+=(s,e)=>SizeActions();
            foreach(var item in QuickActionCatalog.Specs)
            {
                string key=item[0];
                var content=new StackPanel
                {
                    VerticalAlignment=VerticalAlignment.Center,Tag=key,Background=Brushes.Transparent,FocusVisualStyle=null
                };
                var button=(QuickToggleButton)Paint.Button(new Glyph(item[1],24),()=>host.InvokeToggle(key),item[2],true);
                button.Width=button.Height=48;
                button.Padding=new Thickness(0);
                button.HorizontalAlignment=HorizontalAlignment.Center;
                button.Resources["ButtonRadius"]=new CornerRadius(24);
                Toggles[key]=button;
                content.Children.Add(button);
                var label=Paint.TextBlock(item[2],11,Paint.Muted);
                label.HorizontalAlignment=HorizontalAlignment.Center;
                label.TextTrimming=TextTrimming.CharacterEllipsis;
                content.Children.Add(label);
                actionItems.Add(content);
                DragReorder.Attach(content,content,"quick9.actions",key,Orientation.Horizontal,()=>Board.ContentsEditing,(source,target,after)=>MoveAction(source,target,after));
                content.KeyDown+=(s,e)=>
                {
                    if(!Board.ContentsEditing)return;
                    int delta=e.Key==System.Windows.Input.Key.Left?-1:e.Key==System.Windows.Input.Key.Right?1:e.Key==System.Windows.Input.Key.Up?-4:e.Key==System.Windows.Input.Key.Down?4:0;
                    if(delta==0)return;
                    var members=ActionOrder.Members;
                    int index=Array.IndexOf(members,key),next=Math.Max(0,Math.Min(members.Length-1,index+delta));
                    if(index>=0&&next!=index)MoveAction(key,members[next],delta>0);
                    e.Handled=true;
                };
                AutomationProperties.SetName(content,item[2]+" 순서 변경");
                AutomationProperties.SetHelpText(content,"드래그 또는 방향키로 순서를 변경합니다.");
            }
            Board.Add("toggles","기능 토글",grouped,PresentToggles);
            Board.Add("brightness","밝기",Brightness,slot=>
            {
                Brightness.Vertical=slot.Rows>1;Brightness.InvalidateVisual();
            });
            Board.Add("volume","음량",Volume.WithOutputAction(()=>shell.ShowDetail("sound")),slot=>
            {
                Volume.Vertical=slot.Rows>1;Volume.InvalidateVisual();
            });
            Brightness.Changed+=shell.Bright;
            Volume.Changed+=shell.Volume;
            media=new MediaPlayerView(shell.Media,shell.SeekMedia,()=>shell.ShowDetail("sound"),condensed:true,openApp:shell.OpenMediaApplication,dense:true,sourceApp:id=>MediaApplication.Resolve(id,shell.Running.Concat(shell.Apps)));
            media.HorizontalAlignment=HorizontalAlignment.Stretch;
            media.VerticalAlignment=VerticalAlignment.Center;
            mediaBody.Children.Add(mediaGlow);
            richMedia=new Border
            {
                Child=media
            };
            mediaBody.Children.Add(richMedia);
            media.PaletteChanged=palette=>
            {
                mediaGlow.Background=media.IsCompact?null:media.MediaBackground;
                mediaGlow.Visibility=media.IsCompact||shell.Services.Media==null||shell.Services.Media.Artwork==null?Visibility.Collapsed:Visibility.Visible;
            };
            mediaBody.SizeChanged+=(s,e)=>RefreshMediaLayout();
            miniMedia.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width=new GridLength(46)
            });
            miniMedia.ColumnDefinitions.Add(new ColumnDefinition());
            miniMedia.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width=new GridLength(35)
            });
            miniMedia.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width=new GridLength(28)
            });
            miniMedia.Margin=new Thickness(10,5,8,5);
            miniArtwork=Paint.Button(miniArt,()=>shell.OpenMediaApplication(shell.Services.Media.AppId),"현재 재생 앱 열기");
            miniArtwork.Padding=new Thickness(0);
            miniArtwork.Background=Brushes.Transparent;
            miniMedia.Children.Add(miniArtwork);
            miniTitle.TextTrimming=TextTrimming.CharacterEllipsis;
            miniTitle.VerticalAlignment=VerticalAlignment.Center;
            Grid.SetColumn(miniTitle,1);
            miniMedia.Children.Add(miniTitle);
            miniPlay=Paint.Button(miniPlayGlyph,()=>shell.Media("play"),"재생 · 일시 정지");
            miniPlay.Padding=new Thickness(0);
            miniPlay.Background=Brushes.Transparent;
            Grid.SetColumn(miniPlay,2);
            miniMedia.Children.Add(miniPlay);
            miniOutput=Tool("audio-output","출력 기기 변경",()=>shell.ShowDetail("sound"));
            Grid.SetColumn(miniOutput,3);
            miniMedia.Children.Add(miniOutput);
            mediaBody.Children.Add(miniMedia);
            Board.Add("media","미디어 플레이어",mediaBody,slot=>
            {
                mediaColumns=slot.Columns;mediaRows=slot.Rows;MediaShape();
            });
            foreach(string name in QuickToggleOrder.Defaults)
            {
                string key=name;
                var inside=new ContextMenu();
                var extract=new MenuItem
                {
                    Header="단독 토글로 빼기"
                };
                extract.Click+=(s,e)=>ExtractAction(key,null);
                inside.Items.Add(extract);
                insideMenus[key]=inside;
                var outside=new ContextMenu();
                var regroup=new MenuItem
                {
                    Header="기능 토글 그룹에 넣기"
                };
                regroup.Click+=(s,e)=>RegroupAction(key);
                outside.Items.Add(regroup);
                outsideMenus[key]=outside;
            }
            DragReorder.DropArea(Board,"quick9.actions",()=>Board.ContentsEditing,(key,point)=>
            {
                var frame=Board.Frames["toggles"];var start=frame.TranslatePoint(new Point(),Board);if(new Rect(start,new Size(frame.ActualWidth,frame.ActualHeight)).Contains(point))return false;return ExtractAction(key,point);
            });
            host.IsVisibleChanged+=(s,e)=>
            {
                if(!host.IsVisible)
                {
                    Board.Cancel();
                    ExpandToggles(false,false);
                    SetEditing(false);
                    CloseMenus();
                }
            };
            host.Closed+=(s,e)=>
            {
                closed=true;
                Board.Cancel();
                ExpandToggles(false,false);
                CloseMenus();
                mediaGlow.Background=null;
            };
        }
        static Button Tool(string icon,string label,Action action)
        {
            var button=Paint.Button(new Glyph(icon,21),action,label);
            button.Width=button.Height=32;
            button.Padding=new Thickness(0);
            button.Background=Brushes.Transparent;
            return button;
        }
        void AddTile(string key,string icon,string title,Action action,Action circle=null)
        {
            var tile=new QuickWidgetTile(icon,title,action,circle);
            tiles[key]=tile;
            if(key=="wifi"||key=="bluetooth"||key=="theme"||key=="mute")Toggles[key]=tile.Toggle;
            Board.Add(key,title,tile,tile.Shape);
        }
        void MediaShape()
        {
            bool rich=mediaRows>1&&mediaColumns>1;
            richMedia.Visibility=rich?Visibility.Visible:Visibility.Collapsed;
            miniMedia.Visibility=rich?Visibility.Collapsed:Visibility.Visible;
            bool single=mediaColumns==1;
            miniArtwork.Visibility=miniTitle.Visibility=single?Visibility.Collapsed:Visibility.Visible;
            miniOutput.Visibility=mediaColumns>=3?Visibility.Visible:Visibility.Collapsed;
            miniPlay.Visibility=mediaIdle?Visibility.Collapsed:Visibility.Visible;
            miniMedia.ColumnDefinitions[0].Width=new GridLength(single?0:46);
            miniMedia.ColumnDefinitions[2].Width=mediaIdle?new GridLength(0):single?new GridLength(1,GridUnitType.Star):new GridLength(35);
            Grid.SetColumn(miniPlay,single?1:2);
            miniMedia.ColumnDefinitions[3].Width=new GridLength(mediaColumns>=3?28:0);
        }
        void RefreshMediaLayout()
        {
            if(media==null)return;
            media.FitWidget(mediaBody.ActualWidth,mediaBody.ActualHeight);
            media.Update(shell.Services.Media,shell.Services.OutputName);
        }
    }
}
