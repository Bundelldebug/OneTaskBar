using System;
using System.Collections.Generic;

namespace OneTaskBar
{
    internal static class RuntimeCadence
    {
        internal static int Pulse(bool desktop,bool battery,bool interactive,bool hidden,bool displayAwake)
        {
            if(!displayAwake)return 30000;
            if(!desktop)return 2000;
            if(interactive)return battery?200:100;
            return hidden?(battery?2000:1000):(battery?1000:500);
        }
        internal static void Check(List<string> checks)
        {
            if(Pulse(true,true,false,false,true)!=1000||Pulse(true,true,false,true,true)!=2000||Pulse(true,true,true,false,true)!=200||Pulse(true,false,false,false,true)!=500||Pulse(true,true,false,false,false)!=30000)throw new Exception("Idle/panel/display-off cadence regression");
            checks.Add("Battery idle/fullscreen/display-off polling slows down; interactive panels retain fast updates");
        }
    }
}
