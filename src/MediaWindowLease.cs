using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace OneTaskBar
{
    // Per-window DWM state only. The crash guard restores these leases as well as
    // the desktop reservation, so an interrupted animation cannot hide an app.
    internal sealed class MediaWindowLease : IDisposable
    {
        public sealed class Entry
        {
            public long Window;
            public uint Process;
            public int Transitions;
            public bool Hidden;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        [DllImport("dwmapi.dll")]
        static extern int DwmFlush();

        readonly Dictionary<IntPtr,Entry> entries=new Dictionary<IntPtr,Entry>();
        static string Journal {get{return Path.Combine(AppLog.Data,"media-window-lease.json");}}

        static bool Set(IntPtr window,int attribute,int value)
        {
            return DwmSetWindowAttribute(window,attribute,ref value,4)==0;
        }

        internal bool Arm(IntPtr window)
        {
            if(entries.ContainsKey(window))return true;
            uint process;
            if(!Native.IsWindow(window)||Native.GetWindowThreadProcessId(window,out process)==0)return false;
            int transitions;
            // Older DWM versions cannot read this flag. Its documented default is
            // enabled; failed writes leave the native transition untouched.
            if(Native.DwmGetWindowAttribute(window,3,out transitions,4)!=0)transitions=0;
            var entry=new Entry {Window=window.ToInt64(),Process=process,Transitions=transitions};
            entries[window]=entry;
            if(!Save()||!Set(window,3,1))
            {
                entries.Remove(window);
                Save();
                return false;
            }
            return true;
        }

        internal bool HideRepresentation(IntPtr window)
        {
            Entry entry;
            if(!entries.TryGetValue(window,out entry))return false;
            int cloaked;
            int read=Native.DwmGetWindowAttribute(window,14,out cloaked,4);
            if(read!=0||cloaked!=0){AppLog.Write("media-cloak","read="+read+" state="+cloaked);return false;}
            entry.Hidden=true;
            if(!Save())
            {
                entry.Hidden=false;
                return false;
            }
            // Cloaking keeps the source composed for the DWM thumbnail. Do not
            // change the application's placement, layered style, or opacity.
            int hidden=1;int write=DwmSetWindowAttribute(window,13,ref hidden,4);
            if(write==0)return true;
            AppLog.Write("media-cloak","write="+write);
            entry.Hidden=false;
            Save();
            return false;
        }

        internal void RevealAll()
        {
            bool changed=false;
            foreach(var pair in entries)
            {
                if(!pair.Value.Hidden)continue;
                Set(pair.Key,13,0);
                pair.Value.Hidden=false;
                changed=true;
            }
            if(changed)Save();
        }

        internal static void FlushFirstFrame(){DwmFlush();}

        bool Save()
        {
            string staging=Journal+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                Directory.CreateDirectory(AppLog.Data);
                File.WriteAllText(staging,new JavaScriptSerializer().Serialize(entries.Values.ToArray()));
                for(int attempt=0;;attempt++)
                {
                    try
                    {
                        if(File.Exists(Journal))File.Replace(staging,Journal,null);
                        else File.Move(staging,Journal);
                        break;
                    }
                    catch(IOException)
                    {
                        // Antivirus/indexing can briefly hold a freshly written
                        // journal. Never cloak unless recovery state is durable.
                        if(attempt==2)throw;
                        System.Threading.Thread.Sleep(5*(attempt+1));
                    }
                }
                return true;
            }
            catch(Exception error)
            {
                AppLog.Once("media-window-journal",error.GetType().Name);
                return false;
            }
            finally
            {
                try{if(File.Exists(staging))File.Delete(staging);}catch(IOException){}
            }
        }

        static void Restore(Entry entry)
        {
            var window=new IntPtr(entry.Window);
            uint process;
            if(!Native.IsWindow(window)||Native.GetWindowThreadProcessId(window,out process)==0||process!=entry.Process)return;
            if(entry.Hidden)Set(window,13,0);
            Set(window,3,entry.Transitions);
        }

        internal static void Recover()
        {
            try
            {
                if(!File.Exists(Journal))return;
                var saved=new JavaScriptSerializer().Deserialize<Entry[]>(File.ReadAllText(Journal));
                foreach(var entry in saved??new Entry[0])Restore(entry);
                File.Delete(Journal);
            }
            catch(Exception error){AppLog.Once("media-window-recover",error.GetType().Name);}
        }

        public void Dispose()
        {
            if(entries.Count==0)return;
            foreach(var entry in entries.Values)Restore(entry);
            entries.Clear();
            Save();
        }
    }
}
