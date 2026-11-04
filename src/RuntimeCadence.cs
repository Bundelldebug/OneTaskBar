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
            return hidden?(battery?3000:1500):(battery?1500:750);
        }
        // Seconds between background reads. Open panels keep their fast path; events and
        // a foreground change still refresh immediately, so slower reads only save idle work.
        internal static double Services(bool interactive,bool battery){return interactive?.5:battery?5:3;}
        internal static double Wifi(bool interactive,bool battery){return interactive?1:battery?20:10;}
        internal static double RunningApps(bool battery){return battery?10:5;}
        internal static double Pins(bool battery){return battery?120:60;}
        internal static double OsdScan(bool battery){return battery?30:10;}
        internal static double LiveState(bool battery){return battery?120:60;}
        internal static void Check(List<string> checks)
        {
            if(Pulse(true,true,false,false,true)!=1500||Pulse(true,true,false,true,true)!=3000||Pulse(true,true,true,false,true)!=200||Pulse(true,false,false,false,true)!=750||Pulse(true,true,false,false,false)!=30000)throw new Exception("Idle/panel/display-off cadence regression");
            if(Services(true,true)!=.5||Services(false,true)<=Services(false,false)||Wifi(false,true)<=Wifi(false,false)||RunningApps(true)<=RunningApps(false)||Pins(true)<=Pins(false)||LiveState(true)<=LiveState(false))throw new Exception("Battery background reads must be slower than plugged-in reads");
            checks.Add("Battery idle/fullscreen/display-off polling slows down; interactive panels retain fast updates");
        }
    }
}
