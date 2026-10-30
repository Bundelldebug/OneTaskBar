using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Media.Devices;

namespace OneTaskBar
{
    internal sealed class AudioService : IDisposable
    {
        IAudioEndpointVolume endpoint;
        readonly AudioVolumeCallback callback;
        IntPtr callbackPointer;
        string deviceId;
        DateTime nextDeviceCheck,nextRead;
        int stateDirty=1,deviceDirty=1,deviceRevision;
        bool deviceEvents,disposed;
        internal Action Changed;
        internal int ReadCount {get;private set;}
        internal int DeviceRevision {get{return Volatile.Read(ref deviceRevision);}}
        internal bool NotificationsRegistered {get{return callbackPointer!=IntPtr.Zero;}}
        internal double ExactValue {get{Ensure();float value;Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out value));return value*100.0;}}
        public bool Available,Muted;
        public double Value;

        public AudioService()
        {
            callback=new AudioVolumeCallback(()=>
            {
                Interlocked.Exchange(ref stateDirty,1);
                Notify();
            });
            try
            {
                MediaDevice.DefaultAudioRenderDeviceChanged+=DeviceChanged;
                deviceEvents=true;
            }
            catch { }
        }
        void Notify()
        {
            var changed=Changed;
            if(!disposed&&changed!=null)changed();
        }
        void DeviceChanged(object sender,DefaultAudioRenderDeviceChangedEventArgs args)
        {
            Interlocked.Exchange(ref deviceDirty,1);
            Interlocked.Exchange(ref stateDirty,1);
            Interlocked.Increment(ref deviceRevision);
            Notify();
        }
        internal void RefreshDevice()
        {
            if(disposed)throw new ObjectDisposedException("AudioService");
            DeviceChanged(null,null);
            Read();
        }
        void Ensure(bool forceDeviceCheck=false)
        {
            if(disposed)throw new ObjectDisposedException("AudioService");
            if(!forceDeviceCheck&&endpoint!=null&&Volatile.Read(ref deviceDirty)==0&&DateTime.UtcNow<nextDeviceCheck)return;
            Interlocked.Exchange(ref deviceDirty,0);
            IMMDeviceEnumerator enumerator=null;
            IMMDevice device=null;
            try
            {
                enumerator=(IMMDeviceEnumerator)new DeviceEnumerator();
                Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0,1,out device));
                string id;
                device.GetId(out id);
                if(id!=deviceId||endpoint==null)
                {
                    ReleaseEndpoint();
                    object result;
                    var iid=typeof(IAudioEndpointVolume).GUID;
                    Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,IntPtr.Zero,out result));
                    endpoint=(IAudioEndpointVolume)result;
                    deviceId=id;
                    Interlocked.Exchange(ref stateDirty,1);
                    try
                    {
                        callbackPointer=Marshal.GetComInterfaceForObject(callback,typeof(IAudioVolumeCallback));
                        Marshal.ThrowExceptionForHR(endpoint.RegisterControlChangeNotify(callbackPointer));
                    }
                    catch
                    {
                        if(callbackPointer!=IntPtr.Zero)Marshal.Release(callbackPointer);
                        callbackPointer=IntPtr.Zero;
                    }
                }
                nextDeviceCheck=DateTime.UtcNow.AddSeconds(deviceEvents?60:3);
            }
            finally
            {
                if(device!=null)Marshal.ReleaseComObject(device);
                if(enumerator!=null)Marshal.ReleaseComObject(enumerator);
            }
        }
        public void Read()
        {
            if(disposed)return;
            bool changed=Interlocked.Exchange(ref stateDirty,0)!=0||Volatile.Read(ref deviceDirty)!=0;
            if(!changed&&DateTime.UtcNow<nextRead)return;
            try
            {
                Ensure();
                float value;
                bool muted;
                Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out value));
                Marshal.ThrowExceptionForHR(endpoint.GetMute(out muted));
                ReadCount++;
                Value=Math.Round(value*100);
                Muted=muted;
                Available=true;
                nextRead=DateTime.UtcNow.AddSeconds(NotificationsRegistered&&deviceEvents?30:1);
            }
            catch
            {
                Available=false;
                ReleaseEndpoint();
                nextRead=DateTime.UtcNow.AddSeconds(1);
            }
        }
        public void Set(double value)
        {
            Ensure();
            var context=Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar((float)(Math.Max(0,Math.Min(100,value))/100),ref context));
            Interlocked.Exchange(ref stateDirty,1);
        }
        public void Mute(bool muted)
        {
            Ensure(true);
            CommitMute(muted);
        }
        internal void ToggleMute()
        {
            Ensure(true);
            Muted=AudioMute.Toggle(endpoint);
            Available=true;
            Interlocked.Exchange(ref stateDirty,1);
            Notify();
        }
        void CommitMute(bool muted)
        {
            Muted=AudioMute.Write(endpoint,muted);
            Available=true;
            Interlocked.Exchange(ref stateDirty,1);
            Notify();
        }
        void ReleaseEndpoint()
        {
            if(endpoint==null)return;
            if(callbackPointer!=IntPtr.Zero)
            {
                try {endpoint.UnregisterControlChangeNotify(callbackPointer);}catch { }
                Marshal.Release(callbackPointer);
                callbackPointer=IntPtr.Zero;
            }
            Marshal.ReleaseComObject(endpoint);
            endpoint=null;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            if(deviceEvents)MediaDevice.DefaultAudioRenderDeviceChanged-=DeviceChanged;
            ReleaseEndpoint();
        }
    }
}
