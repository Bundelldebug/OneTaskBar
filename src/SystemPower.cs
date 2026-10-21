using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
namespace OneTaskBar
{
    internal enum SystemPowerAction {ShutDown,Sleep,Lock}
    internal static class SystemPower
    {
        [DllImport("user32.dll",SetLastError=true)]static extern bool LockWorkStation();
        internal static void Execute(SystemPowerAction action)
        {
            if(action==SystemPowerAction.Lock){if(!LockWorkStation())throw new Win32Exception(Marshal.GetLastWin32Error());return;}
            if(action==SystemPowerAction.Sleep){if(!System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend,false,false))throw new InvalidOperationException("이 PC에서 절전 모드를 시작하지 못했습니다.");return;}
            if(action!=SystemPowerAction.ShutDown)throw new ArgumentOutOfRangeException("action");
            // No /f: Windows can let applications with unsaved work block shutdown.
            using(var process=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"shutdown.exe"),"/s /t 0"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){}
        }
        internal static int Probe()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};var shell=new ShellController(true);var quick=new QuickWindow(shell);var menu=quick.BuildPowerMenu(quick.Surface);var items=menu.Items.OfType<MenuItem>().ToArray();bool passed=items.Length==4&&items.Take(3).Select(i=>(SystemPowerAction)i.Tag).SequenceEqual(new[]{SystemPowerAction.ShutDown,SystemPowerAction.Sleep,SystemPowerAction.Lock});
            Program.Save(quick.Surface,380,650,Path.Combine(AppLog.Data,"gray-power-preview.png"));AppLog.Json("power-menu-probe.json",new {passed=passed,labels=items.Select(i=>i.Header.ToString()).ToArray(),executedSystemActions=false});quick.Close();shell.Dispose();return passed?0:1;
        }
    }
}
