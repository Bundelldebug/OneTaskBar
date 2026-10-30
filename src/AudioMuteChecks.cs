using System;
using System.Collections.Generic;

namespace OneTaskBar
{
    internal static class AudioMuteChecks
    {
        // Exercise the Windows endpoint contract without muting the user's playback.
        sealed class Endpoint : IAudioEndpointVolume
        {
            internal bool Muted,IgnoreWrites;
            internal float Volume=.72f;
            public int SetMute(bool value,ref Guid context){if(!IgnoreWrites)Muted=value;return 0;}
            public int GetMute(out bool value){value=Muted;return 0;}
            public int GetMasterVolumeLevelScalar(out float value){value=Volume;return 0;}
            public int SetMasterVolumeLevelScalar(float value,ref Guid context){Volume=value;return 0;}
            public int RegisterControlChangeNotify(IntPtr callback){return 0;}
            public int UnregisterControlChangeNotify(IntPtr callback){return 0;}
            public int GetChannelCount(out uint count){count=2;return 0;}
            public int SetMasterVolumeLevel(float value,ref Guid context){throw new NotSupportedException();}
            public int GetMasterVolumeLevel(out float value){throw new NotSupportedException();}
            public int SetChannelVolumeLevel(uint channel,float value,ref Guid context){throw new NotSupportedException();}
            public int SetChannelVolumeLevelScalar(uint channel,float value,ref Guid context){throw new NotSupportedException();}
            public int GetChannelVolumeLevel(uint channel,out float value){throw new NotSupportedException();}
            public int GetChannelVolumeLevelScalar(uint channel,out float value){throw new NotSupportedException();}
            public int GetVolumeStepInfo(out uint step,out uint count){throw new NotSupportedException();}
            public int VolumeStepUp(ref Guid context){throw new NotSupportedException();}
            public int VolumeStepDown(ref Guid context){throw new NotSupportedException();}
            public int QueryHardwareSupport(out uint mask){throw new NotSupportedException();}
            public int GetVolumeRange(out float minimum,out float maximum,out float increment){throw new NotSupportedException();}
        }
        static void Require(bool value,string message)
        {
            if(!value)throw new InvalidOperationException("Audio mute: "+message);
        }
        internal static void Validate(List<string> checks)
        {
            var endpoint=new Endpoint();
            Require(AudioMute.Toggle(endpoint)&&endpoint.Muted&&endpoint.Volume==.72f,"nonzero speaker volume was not muted independently of its slider level");
            Require(!AudioMute.Toggle(endpoint)&&!endpoint.Muted&&endpoint.Volume==.72f,"unmuting lost the user's previous level");
            endpoint.Muted=true;
            Require(!AudioMute.Toggle(endpoint),"a native Windows mute change was toggled from stale UI state");
            endpoint.IgnoreWrites=true;
            bool rejected=false;
            try {AudioMute.Write(endpoint,true);}
            catch(InvalidOperationException){rejected=true;}
            Require(rejected,"a rejected native mute write was reported as successful");
            checks.Add("Mute acts on current Windows endpoint state at nonzero volume, preserves the level, and verifies accepted writes");
        }
        internal static int HardwareProbe()
        {
            bool previous=false,restore=false,verified=false,rebound=false,restored=false;
            double level=0;
            string error=null;
            using(var audio=new AudioService())
            {
                try
                {
                    audio.Read();
                    if(!audio.Available)throw new InvalidOperationException("No default output endpoint");
                    previous=audio.Muted;
                    level=audio.ExactValue;
                    restore=true;
                    audio.Mute(true);
                    using(var observer=new AudioService())
                    {
                        observer.Read();
                        verified=observer.Available&&observer.Muted&&Math.Abs(observer.ExactValue-level)<.01;
                    }
                    audio.RefreshDevice();
                    audio.Mute(true);
                    audio.Read();
                    rebound=audio.Available&&audio.Muted;
                }
                catch(Exception failure){error=failure.Message;}
                finally
                {
                    if(restore)
                    {
                        try
                        {
                            audio.Mute(previous);
                            using(var observer=new AudioService())
                            {
                                observer.Read();
                                restored=observer.Available&&observer.Muted==previous&&Math.Abs(observer.ExactValue-level)<.01;
                            }
                        }
                        catch(Exception failure){error=failure.Message;}
                    }
                }
            }
            bool passed=verified&&rebound&&restored&&error==null;
            AppLog.Json("audio-mute-probe.json",new {passed=passed,originalVolume=level,nativeMuteVerified=verified,controlSurvivedRebind=rebound,originalStateRestored=restored,error=error});
            return passed?0:1;
        }
    }
}
