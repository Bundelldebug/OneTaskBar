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
        internal void Feedback(string text)
        {
            notice.Text=text;
            notice.Visibility=Visibility.Visible;
        }
        async void LoadBuds()
        {
            budsRequested=true;
            try
            {
                var symbol=await BackgroundWork.Run(BudsDeviceData.ShortcutIcon);
                if(!closed)
                {
                    budsSymbol=symbol;
                    ApplyBuds();
                }
            }
            catch(Exception e)
            {
                AppLog.Once("quick9-buds-symbol",e.GetType().Name);
            }
        }
        internal bool BudsSelected
        {
            get
            {
                return tiles["buds"].Toggle.Checked;
            }
        }
        void ApplyBuds()
        {
            tiles["buds"].Icon.SetDeviceIcon(budsSymbol);
            QuickToggleButton grouped;
            if(Toggles.TryGetValue("buds",out grouped))((Glyph)grouped.Content).SetDeviceIcon(budsSymbol);
            Volume.SetDeviceIcon(Volume.IsEnabled&&BudsInfo.IsBuds(shell.Services.OutputName)?budsSymbol:null);
        }
        async void SamsungIcon(AppEntry app)
        {
            try
            {
                var image=app.Icon??await BackgroundWork.Run(()=>AppIcons.Get(app));
                if(!closed&&app.Key==samsungKey)
                {
                    samsungIcon=image;
                    if(image!=null)samsung.Content=AppIcons.Visual(image,24);
                }
            }
            catch
            {
            }
        }
        internal void Refresh(PowerState power,int bright,double sound,bool muted,bool audioAvailable,bool brightAvailable)
        {
            var now=DateTime.Now;
            if(now.Ticks/TimeSpan.TicksPerSecond!=clock)
            {
                clock=now.Ticks/TimeSpan.TicksPerSecond;
                time.Text=now.ToString("H:mm:ss");
            }
            if(day!=now.Date.Ticks)
            {
                day=now.Date.Ticks;
                date.Text=now.ToString("M월 d일 dddd");
            }
            battery.Update(power);
            batteryButton.Visibility=PowerDisplay.Slot(power,Preferences.Current.DesktopPowerWatts)>0?Visibility.Visible:Visibility.Collapsed;
            Brightness.IsEnabled=brightAvailable;
            Brightness.Value=bright<0?0:bright;
            Volume.IsEnabled=audioAvailable;
            Volume.Value=muted?0:sound;
            Volume.Kind=SoundIcon.For(sound,muted,audioAvailable);
            Volume.InvalidateVisual();
            Brightness.WheelAdjust=Volume.WheelAdjust=Preferences.Current.QuickSliderScroll;
            bool wifi=shell.Services.WifiOn??shell.Wifi!=null,bluetooth=shell.Services.BluetoothOn??false;
            tiles["wifi"].Text("Wi-Fi",shell.Wifi??(wifi?"연결 안 됨":"꺼짐"));
            tiles["bluetooth"].Text("블루투스",shell.Services.BluetoothOn.HasValue?(bluetooth?(shell.Buds.ConnectedCaption??"연결 안 됨"):"꺼짐"):"연결 설정");
            var theme=AppTheme.Read();
            tiles["theme"].Text("다크 모드",theme.Dark?"켜짐":"꺼짐");
            tiles["mute"].Text("음소거",muted?"켜짐":"꺼짐");
            tiles["mute"].Icon.Update(SoundIcon.For(sound,muted,audioAvailable),Glass95.ToggleInk(tiles["mute"].Toggle.Checked));
            ((Glyph)Toggles["mute"].Content).Update(SoundIcon.For(sound,muted,audioAvailable),Glass95.ToggleInk(Toggles["mute"].Checked));
            var efficiency=shell.Toggles.Power();
            var disturb=shell.Toggles.Disturb.State;
            foreach(var pair in Toggles)
            {
                string key=pair.Key;
                var setting=QuickSettingCatalog.Find(key);
                if(setting!=null)
                {
                    bool selected=setting.Read(Preferences.Current);
                    pair.Value.RefreshState(selected,true);
                    tiles[key].State(selected,true);
                    tiles[key].Text(setting.Title,selected?"켜짐":"꺼짐");
                    continue;
                }
                if(QuickActionCatalog.Commands.Contains(key))
                {
                    pair.Value.RefreshState(key=="buds"&&(shell.Buds.HasConnectedBuds||BudsInfo.IsBuds(shell.Services.OutputName)),QuickActionCatalog.Available(key,shell));
                    continue;
                }
                bool on=key=="wifi"?wifi:key=="bluetooth"?bluetooth:key=="theme"?theme.Dark:key=="mute"?muted:key=="efficiency"?efficiency.Selected==QuickActions.EfficientMode:key=="dnd"?disturb.Enabled:key=="awake"?shell.Toggles.Awake:key=="animations"?Preferences.Current.Animations:key=="startup"?shell.Toggles.Startup:Preferences.Current.QuickSliderScroll;
                bool available=!host.TogglePending(key)&&(key=="wifi"?shell.Services.WifiPresent!=false:key=="bluetooth"?shell.Services.BluetoothPresent!=false:key=="theme"?theme.Available:key=="mute"?audioAvailable:key=="efficiency"?efficiency.Available:key=="dnd"?disturb.Available:true);
                if(tiles.ContainsKey(key))
                {
                    tiles[key].State(on,available);
                    if(key!="wifi"&&key!="bluetooth"&&key!="theme"&&key!="mute")tiles[key].Text(tiles[key].Title,available?(on?"켜짐":"꺼짐"):"사용할 수 없음");
                }
                pair.Value.RefreshState(on,available);
            }
            tiles["devices"].Text("주변 기기 연결","");
            tiles["devices"].State(false,shell.SamsungDevices()!=null);
            bool budsConnected=shell.Buds.HasConnectedBuds||BudsInfo.IsBuds(shell.Services.OutputName);
            tiles["buds"].Text("Galaxy Buds",budsConnected?shell.Buds.BudsCaption??"연결됨":"연결 안 됨");
            tiles["buds"].State(budsConnected,shell.GalaxyBuds()!=null);
            if(!budsRequested&&(shell.GalaxyBuds()!=null||BudsInfo.IsBuds(shell.Services.OutputName)))LoadBuds();
            ApplyBuds();
            var app=shell.SamsungSettings();
            samsung.Visibility=app!=null?Visibility.Visible:Visibility.Collapsed;
            if(app!=null&&samsungKey!=app.Key)
            {
                samsungKey=app.Key;
                SamsungIcon(app);
            }
            media.Update(shell.Services.Media,shell.Services.OutputName);
            var state=shell.Services.Media??new MediaState();
            miniTitle.Text=state.Active?state.Title:"재생 중인 미디어 없음";
            miniPlayGlyph.Update(state.Playing?"pause":"play",Paint.Text);
            miniPlay.IsEnabled=state.Available&&state.CanPlay;
            miniArtwork.IsEnabled=state.Active&&state.Artwork!=null&&!String.IsNullOrEmpty(state.AppId);
            if(!miniArtReady||miniArtSource!=state.Artwork)
            {
                miniArtReady=true;
                miniArtSource=state.Artwork;
                miniArt.Background=state.Artwork==null?(Brush)Brushes.Transparent:new ImageBrush(state.Artwork)
                {
                    Stretch=Stretch.UniformToFill
                };
                miniArt.Child=state.Artwork==null?missingArt:null;
            }
            bool changed=mediaIdle!=!state.Active;
            mediaIdle=!state.Active;
            Board.SetMediaIdle(mediaIdle);
            if(changed)MediaShape();
        }
        internal object Snapshot()
        {
            return new
            {
                ui="9.0",editing=Board.Editing,expanded=Expanded,inlineToggles=true,controlHeight=QuickWidgetBoard.ControlHeight,gestureActive=Board.GestureActive,noOverlap=Board.DisplayLayout.Fits(),separateSliderButtons=true,borderlessSliders=Brightness.NoTrackBorder&&Volume.NoTrackBorder,items=Board.Layout.Items.Select(i=>new
                {
                    key=i.Key,x=i.X,y=i.Y,columns=i.Columns,rows=i.Rows,hidden=i.Hidden
                }).ToArray()
            };
        }
    }
}
