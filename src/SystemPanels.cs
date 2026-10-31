using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace OneTaskBar
{
    // Use Windows' documented Win+A / Win+N entry points. Explorer's buttons may be virtualized
    // while its taskbar is hidden; sending the shortcut does not reveal or restyle the taskbar.
    internal static class SystemPanels
    {
        [StructLayout(LayoutKind.Sequential)]struct KeyInput {internal ushort Key,Scan;internal uint Flags,Time;internal UIntPtr Extra;}
        [StructLayout(LayoutKind.Explicit,Size=32)]struct InputUnion {[FieldOffset(0)]internal KeyInput Keyboard;}
        [StructLayout(LayoutKind.Sequential)]struct Input {internal uint Type;internal InputUnion Data;}
        [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,Input[] inputs,int size);
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        internal const uint InputMarker=0x4f554948;
        static Input Key(ushort key,uint flags) {return new Input {Type=1,Data=new InputUnion {Keyboard=new KeyInput {Key=key,Flags=flags,Extra=new UIntPtr(InputMarker)}}};}
        static Input Scan(ushort scan,uint flags){return new Input{Type=1,Data=new InputUnion{Keyboard=new KeyInput{Scan=scan,Flags=8|flags,Extra=new UIntPtr(InputMarker)}}};}
        static bool ModifiersHeld() {foreach(int key in new[]{0x10,0x11,0x12,0x5b,0x5c})if((GetAsyncKeyState(key)&0x8000)!=0)return true;return false;}
        internal static bool Toggle(bool notifications)
        {return Shortcut((ushort)(notifications?0x4e:0x41));}
        internal static bool TaskView(){return Shortcut(0x09);}
        internal static bool Cast(){return Shortcut(0x4b);}
        internal static bool ShowDesktop(){return Shortcut(0x44);}
        internal static bool ContextMenu()
        {
            if(ModifiersHeld())return false;
            var keys=new[]{Key(0x10,0),Key(0x79,0),Key(0x79,2),Key(0x10,2)};
            return SendInput(4,keys,Marshal.SizeOf(typeof(Input)))==4;
        }
        internal static bool DesktopCommand(int steps,int operation)
        {
            if(operation<0||operation>2||Math.Abs(steps)>256)return false;
            for(int i=0;i<50&&ModifiersHeld();i++)Thread.Sleep(20);
            if(ModifiersHeld())return false;
            if(operation==1)return DesktopChord(0x44);
            for(int i=0;i<Math.Abs(steps);i++){if(!DesktopChord((ushort)(steps<0?0x25:0x27)))return false;Thread.Sleep(140);}
            return operation!=2||DesktopChord(0x73);
        }
        static bool DesktopChord(ushort key)
        {
            if(ModifiersHeld())return false;
            var inputs=new[]{Key(0x5b,1),Key(0x11,0),Key(key,0),Key(key,2),Key(0x11,2),Key(0x5b,3)};
            uint sent=SendInput(6,inputs,Marshal.SizeOf(typeof(Input)));
            if(sent>0&&sent<6)SendInput(3,new[]{Key(key,2),Key(0x11,2),Key(0x5b,3)},Marshal.SizeOf(typeof(Input)));
            return sent==6;
        }
        internal static bool Start()
        {
            // Ctrl+Esc is Windows' native Start shortcut and bypasses solo-Win remapping.
            for(int i=0;i<50&&ModifiersHeld();i++)Thread.Sleep(20);if(ModifiersHeld())return false;var input=new[]{Scan(0x1d,0),Scan(0x01,0),Scan(0x01,2),Scan(0x1d,2)};return SendInput(4,input,Marshal.SizeOf(typeof(Input)))==4;
        }
        internal static bool Search(string query=null)
        {
            if(!Shortcut(0x53))return false;if(String.IsNullOrEmpty(query))return true;
            for(int i=0;i<60;i++){uint pid;Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid);try{using(var process=System.Diagnostics.Process.GetProcessById((int)pid)){string name=process.ProcessName;if(name=="SearchHost"||name=="SearchApp"||name=="SearchUI"){var input=new System.Collections.Generic.List<Input>();foreach(char character in query){var down=Key(0,4);var data=down.Data;var keyboard=data.Keyboard;keyboard.Scan=character;data.Keyboard=keyboard;down.Data=data;var up=down;data=up.Data;keyboard=data.Keyboard;keyboard.Flags=6;data.Keyboard=keyboard;up.Data=data;input.Add(down);input.Add(up);}return SendInput((uint)input.Count,input.ToArray(),Marshal.SizeOf(typeof(Input)))==input.Count;}}}catch{}Thread.Sleep(20);}return false;
        }
        internal static bool ProbeEmergency()
        {var down=new[]{Key(0x11,0),Key(0x10,0),Key(0x09,0),Key(0x31,0)};var up=new[]{Key(0x31,2),Key(0x09,2),Key(0x10,2),Key(0x11,2)};bool sent=SendInput(4,down,Marshal.SizeOf(typeof(Input)))==4;Thread.Sleep(80);bool released=SendInput(4,up,Marshal.SizeOf(typeof(Input)))==4;return sent&&released;}
        internal static bool ProbeText(string text)
        {var inputs=new System.Collections.Generic.List<Input>();foreach(char c in text){var down=Key(0,4);var data=down.Data;var keyboard=data.Keyboard;keyboard.Scan=c;data.Keyboard=keyboard;down.Data=data;var up=down;data=up.Data;keyboard=data.Keyboard;keyboard.Flags=6;data.Keyboard=keyboard;up.Data=data;inputs.Add(down);inputs.Add(up);}return SendInput((uint)inputs.Count,inputs.ToArray(),Marshal.SizeOf(typeof(Input)))==inputs.Count;}
        internal static void CloseSearchProbe()
        {uint pid;Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid);try{using(var process=System.Diagnostics.Process.GetProcessById((int)pid)){if(process.ProcessName!="SearchHost"&&process.ProcessName!="SearchApp"&&process.ProcessName!="SearchUI")return;var inputs=new[]{Key(0x1b,0),Key(0x1b,2)};SendInput(2,inputs,Marshal.SizeOf(typeof(Input)));}}catch{}}
        internal static bool ProbeStartTap(ushort win=0x5b)
        {
            if(ModifiersHeld())return false;var inputs=new[]{Key(win,1),Key(win,3)};
            for(int i=0;i<inputs.Length;i++){var data=inputs[i].Data;var keyboard=data.Keyboard;keyboard.Extra=new UIntPtr(InputMarker+1);data.Keyboard=keyboard;inputs[i].Data=data;}
            return SendInput(2,inputs,Marshal.SizeOf(typeof(Input)))==2;
        }
        static Input ProbeKey(ushort key,uint flags){var input=Key(key,flags);var data=input.Data;var keyboard=data.Keyboard;keyboard.Extra=new UIntPtr(InputMarker+1);data.Keyboard=keyboard;input.Data=data;return input;}
        internal static bool ProbeStartChord(bool shift=false)
        {
            if(ModifiersHeld())return false;bool sent=SendInput(1,new[]{ProbeKey(0x5b,1)},Marshal.SizeOf(typeof(Input)))==1;Thread.Sleep(65);
            if(shift){sent&=SendInput(1,new[]{ProbeKey(0x10,0)},Marshal.SizeOf(typeof(Input)))==1;Thread.Sleep(40);}sent&=SendInput(1,new[]{ProbeKey(0x87,0)},Marshal.SizeOf(typeof(Input)))==1;Thread.Sleep(60);
            var release=shift?new[]{ProbeKey(0x87,2),ProbeKey(0x10,2),ProbeKey(0x5b,3)}:new[]{ProbeKey(0x87,2),ProbeKey(0x5b,3)};sent&=SendInput((uint)release.Length,release,Marshal.SizeOf(typeof(Input)))==release.Length;return sent;
        }
        internal static bool ForwardStartChord(ushort win,ushort key,ushort scan,uint hookFlags)
        {
            // Replay the buffered modifier and the first chord key as one ordered batch.
            // Letting the physical chord key pass first could type it before Windows sees Win.
            uint flags=(hookFlags&1)!=0?1u:0u;var next=scan!=0?Scan(scan,flags):Key(key,flags);var inputs=new[]{Key(win,1),next};uint sent=SendInput(2,inputs,Marshal.SizeOf(typeof(Input)));
            if(sent>0&&sent<2)SendInput(1,new[]{Key(win,3)},Marshal.SizeOf(typeof(Input)));return sent==2;
        }
        static bool Shortcut(ushort letter)
        {
            // Ctrl+Alt+Q must be released first. Never release or replace a key held by the user.
            for(int i=0;i<50&&ModifiersHeld();i++)Thread.Sleep(20);if(ModifiersHeld())return false;
            var keys=new[]{Key(0x5b,1),Key(letter,0),Key(letter,2),Key(0x5b,3)};
            uint sent=SendInput((uint)keys.Length,keys,Marshal.SizeOf(typeof(Input)));
            if(sent>0&&sent<4){var release=sent>=2?new[]{Key(letter,2),Key(0x5b,3)}:new[]{Key(0x5b,3)};SendInput((uint)release.Length,release,Marshal.SizeOf(typeof(Input)));}
            return sent==4;
        }
    }
}
