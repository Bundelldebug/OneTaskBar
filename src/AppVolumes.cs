using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // The method order of each interface below is the native COM vtable order.
    [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr group,uint flags,out IntPtr session);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr group,uint flags,out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        [PreserveSig] int RegisterSessionNotification(IntPtr callback);
        [PreserveSig] int UnregisterSessionNotification(IntPtr callback);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr callback);
        [PreserveSig] int UnregisterDuckNotification(IntPtr callback);
    }
    [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index,out IAudioSessionControl2 session);
    }
    [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string name,ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)]string path,ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid group);
        [PreserveSig] int SetGroupingParam(ref Guid group,ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr callback);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr callback);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)]bool optOut);
    }
    [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level,ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)]out bool mute);
    }

    // One app's sound on the default output: every audio session of its processes is moved together.
    internal sealed class AppVolume
    {
        internal string Name;
        internal ImageSource Icon;
        internal float Level;
        internal bool Muted,Active;
        internal readonly List<ISimpleAudioVolume> Sessions=new List<ISimpleAudioVolume>();
        static Guid context=Guid.Empty;
        internal void Apply(float level,bool muted)
        {
            Level=level;Muted=muted;var list=Sessions.ToArray();
            Task.Run(()=>
            {
                foreach(var session in list)
                {
                    try{var none=Guid.Empty;session.SetMasterVolume(level,ref none);session.SetMute(muted,ref none);}
                    catch(Exception error){AppLog.Once("app-volume-set",error.GetType().Name);}
                }
            });
        }
    }
    internal static class AppVolumes
    {
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        // Read off the UI thread: it asks the audio service and opens each app's file for its name and icon.
        internal static List<AppVolume> Read()
        {
            var result=new Dictionary<string,AppVolume>(StringComparer.OrdinalIgnoreCase);
            object enumeratorObject=null,managerObject=null;IMMDevice device=null;
            try
            {
                enumeratorObject=new DeviceEnumerator();var enumerator=(IMMDeviceEnumerator)enumeratorObject;
                if(enumerator.GetDefaultAudioEndpoint(0,1,out device)!=0||device==null)return new List<AppVolume>();
                var iid=typeof(IAudioSessionManager2).GUID;
                if(device.Activate(ref iid,23,IntPtr.Zero,out managerObject)!=0||managerObject==null)return new List<AppVolume>();
                IAudioSessionEnumerator sessions;
                if(((IAudioSessionManager2)managerObject).GetSessionEnumerator(out sessions)!=0||sessions==null)return new List<AppVolume>();
                int count;sessions.GetCount(out count);
                int own=Process.GetCurrentProcess().Id;
                for(int i=0;i<count;i++)
                {
                    try
                    {
                        IAudioSessionControl2 control;
                        if(sessions.GetSession(i,out control)!=0||control==null)continue;
                        int state;control.GetState(out state);
                        // 2 is an expired session; the system sounds session belongs to Windows itself.
                        if(state==2||control.IsSystemSoundsSession()==0)continue;
                        uint pid;control.GetProcessId(out pid);
                        if(pid==0||pid==(uint)own)continue;
                        var volume=control as ISimpleAudioVolume;if(volume==null)continue;
                        string path=null,name=null;
                        try
                        {
                            using(var process=Process.GetProcessById((int)pid))
                            {
                                name=process.ProcessName;
                                try{path=process.MainModule.FileName;}catch(Exception){}
                            }
                        }
                        catch(Exception){continue;}
                        string key=path??name;AppVolume app;
                        if(!result.TryGetValue(key,out app))
                        {
                            app=new AppVolume {Name=Friendly(path,name)};app.Icon=Icon(path);
                            float level;bool muted;volume.GetMasterVolume(out level);volume.GetMute(out muted);app.Level=level;app.Muted=muted;
                            result[key]=app;
                        }
                        app.Sessions.Add(volume);if(state==1)app.Active=true;
                    }
                    catch(Exception error){AppLog.Once("app-volume-read",error.GetType().Name);}
                }
            }
            catch(Exception error){AppLog.Once("app-volume-manager",error.GetType().Name);}
            finally{if(device!=null&&Marshal.IsComObject(device))Marshal.ReleaseComObject(device);}
            return result.Values.OrderByDescending(a=>a.Active).ThenBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).Take(8).ToList();
        }
        static string Friendly(string path,string fallback)
        {
            try
            {
                if(path!=null)
                {
                    string text=FileVersionInfo.GetVersionInfo(path).FileDescription;
                    if(!String.IsNullOrWhiteSpace(text))return text.Trim();
                }
            }
            catch(Exception){}
            return fallback;
        }
        static ImageSource Icon(string path)
        {
            if(String.IsNullOrEmpty(path))return null;
            try
            {
                using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if(icon==null)return null;
                    var source=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();return source;
                }
            }
            catch(Exception){return null;}
        }
    }
    // The "apps" block at the bottom of the sound page: the app icon mutes that app, the slider sets its volume.
    internal static class AppVolumeView
    {
        internal static UIElement Build(IList<AppVolume> apps)
        {
            var box=new StackPanel();
            if(apps==null||apps.Count==0)
            {
                var none=Paint.TextBlock("소리를 내는 앱이 없습니다. 앱에서 소리를 재생하면 여기에 나타납니다",13,AudioOutputView.Muted);none.TextWrapping=TextWrapping.Wrap;none.Margin=new Thickness(5,10,5,4);box.Children.Add(none);return box;
            }
            var hint=Paint.TextBlock("앱 아이콘을 누르면 그 앱만 음소거됩니다",12,AudioOutputView.Muted);hint.Margin=new Thickness(5,0,5,8);box.Children.Add(hint);
            var card=new StackPanel();
            for(int i=0;i<apps.Count;i++)
            {
                var app=apps[i];
                var row=new Grid {MinHeight=50,Margin=new Thickness(0,1,0,1)};
                row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(48)});row.ColumnDefinitions.Add(new ColumnDefinition());
                var picture=new Grid {Width=26,Height=26};
                if(app.Icon!=null)picture.Children.Add(new Image {Source=app.Icon,Stretch=Stretch.Uniform});else picture.Children.Add(new Glyph("volume",20){Color=AudioOutputView.Ink});
                var slash=new Glyph("mute",16){Color=AudioOutputView.Ink,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,-5,-5),Visibility=app.Muted?Visibility.Visible:Visibility.Collapsed};
                picture.Children.Add(slash);picture.Opacity=app.Muted?.45:1;
                var slider=new PillSlider("volume") {Value=app.Muted?0:app.Level*100,Height=22,ShowIcon=false,Light=true,DarkTrack=true,NoTrackBorder=true,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center};
                Action refresh=()=>{picture.Opacity=app.Muted?.45:1;slash.Visibility=app.Muted?Visibility.Visible:Visibility.Collapsed;};
                var mute=Paint.Button(picture,()=>
                {
                    bool muted=!app.Muted;
                    app.Apply(app.Level,muted);slider.Value=muted?0:app.Level*100;refresh();
                },app.Name+" 음소거 켜기 · 끄기");
                mute.Width=mute.Height=40;mute.Padding=new Thickness(0);mute.Background=Brushes.Transparent;mute.VerticalAlignment=VerticalAlignment.Center;
                slider.Changed+=value=>
                {
                    float level=(float)Math.Max(0,Math.Min(1,value/100.0));
                    // Moving the slider of a muted app brings its sound back.
                    app.Apply(level,level>0?false:app.Muted);refresh();
                };
                var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
                var name=Paint.TextBlock(app.Name,12,AudioOutputView.Ink);name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(0,0,8,3);name.ToolTip=app.Name;
                words.Children.Add(name);words.Children.Add(slider);Grid.SetColumn(words,1);
                row.Children.Add(mute);row.Children.Add(words);
                card.Children.Add(row);
            }
            box.Children.Add(new Border {CornerRadius=new CornerRadius(23),Background=AudioOutputView.Selection,Padding=new Thickness(5,5,5,5),Child=card});
            return box;
        }
    }
}
