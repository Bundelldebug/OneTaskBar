using System;
using System.Runtime.InteropServices;

namespace OneTaskBar
{
    internal static class AudioMute
    {
        internal static bool Toggle(IAudioEndpointVolume endpoint)
        {
            bool current;
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out current));
            return Write(endpoint,!current);
        }
        internal static bool Write(IAudioEndpointVolume endpoint,bool muted)
        {
            var context=Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.SetMute(muted,ref context));
            bool actual;
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out actual));
            if(actual!=muted)throw new InvalidOperationException("Windows가 출력 장치의 음소거 변경을 적용하지 못했습니다.");
            return actual;
        }
    }
}
