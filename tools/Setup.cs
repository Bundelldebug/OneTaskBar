using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class Setup : Form
{
    readonly Button install=new Button();
    readonly CheckBox startup=new CheckBox(),shortcut=new CheckBox(),launch=new CheckBox();
    readonly Label status=new Label();
    readonly ProgressBar progress=new ProgressBar();
    bool complete,busy;
    static string Quote(string value){return "\""+value.Replace("\"","")+"\"";}
    static string Extract(string destination)
    {
        string root=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OneTaskBar.Payload.zip"))
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))
        {
            foreach(var entry in zip.Entries){if(entry.FullName.EndsWith("/"))continue;string file=Path.GetFullPath(Path.Combine(root,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));if(!file.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid payload path");Directory.CreateDirectory(Path.GetDirectoryName(file));using(var source=entry.Open())using(var target=File.Create(file))source.CopyTo(target);}
        }
        foreach(string file in new[]{"OneTaskBar.exe","OneTaskBar.Guard.exe","Install.ps1","Uninstall.ps1","LICENSE"})if(!File.Exists(Path.Combine(root,file)))throw new InvalidDataException("Incomplete installation payload");
        return root;
    }
    static int Install(bool noStartup,bool noShortcut,bool noLaunch,string staging)
    {
        string temporary=Path.Combine(Path.GetTempPath(),"OneTaskBar-setup-"+Guid.NewGuid().ToString("N"));
        try{
            string root=Extract(temporary);
            string arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(Path.Combine(root,"Install.ps1"));
            if(staging!=null)arguments+=" -StagingOnly -Destination "+Quote(staging);
            else arguments+=noStartup?" -NoStartup":" -EnableStartup";
            if(noShortcut)arguments+=" -NoShortcut";if(noLaunch)arguments+=" -NoLaunch";
            var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe"),arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            // Child output is small; drain both pipes concurrently to avoid a full-buffer deadlock.
            using(var process=Process.Start(info)){var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();process.WaitForExit();Task.WaitAll(stdout,stderr);if(process.ExitCode!=0)throw new InvalidOperationException(stderr.Result+Environment.NewLine+stdout.Result);return 0;}
        }finally{
            // The only cleanup target is our uniquely-created temp directory.
            string allowed=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(Path.GetFullPath(temporary).StartsWith(allowed,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(temporary).StartsWith("OneTaskBar-setup-",StringComparison.Ordinal))try{Directory.Delete(temporary,true);}catch{}
        }
    }
    Setup()
    {
        Text="One TaskBar 설치";ClientSize=new Size(560,430);Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(244,246,249);ForeColor=Color.FromArgb(30,40,53);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
        Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
        var image=new PictureBox {Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(30,32,64,64)};Controls.Add(image);
        Controls.Add(new Label {Text="One TaskBar",Font=new Font(Font.FontFamily,22,FontStyle.Bold),AutoSize=true,Location=new Point(110,30)});
        Controls.Add(new Label {Text="내 PC를 위한 가벼운 독과 런처 · 0.3.0",AutoSize=true,Location=new Point(113,76)});
        Controls.Add(new Label {Text="현재 사용자에게 설치합니다. 관리자 권한은 필요하지 않습니다.",AutoSize=true,Location=new Point(30,122)});
        Controls.Add(new Label {Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OneTaskBar","App"),MaximumSize=new Size(500,44),AutoSize=true,Location=new Point(30,152),ForeColor=Color.FromArgb(85,97,113)});
        bool auto=true;try{using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OneTaskBar","Data","installation.json");if(File.Exists(data))auto=key!=null&&key.GetValue("OneTaskBar")!=null;}}catch{}
        startup.Text="Windows 로그인 시 자동 실행";startup.Checked=auto;startup.SetBounds(30,207,480,26);Controls.Add(startup);
        shortcut.Text="바탕 화면에 바로가기 만들기";shortcut.Checked=true;shortcut.SetBounds(30,239,480,26);Controls.Add(shortcut);
        launch.Text="설치 후 One TaskBar 실행";launch.Checked=true;launch.SetBounds(30,271,480,26);Controls.Add(launch);
        status.Text="설정을 유지하며 업데이트합니다.";status.SetBounds(30,311,500,44);Controls.Add(status);
        progress.SetBounds(30,362,330,8);progress.Visible=false;Controls.Add(progress);
        install.Text="설치";install.SetBounds(390,357,140,44);install.FlatStyle=FlatStyle.Flat;install.FlatAppearance.BorderSize=0;install.BackColor=Color.FromArgb(44,105,218);install.ForeColor=Color.White;install.Cursor=Cursors.Hand;Controls.Add(install);AcceptButton=install;
        install.Click+=async(s,e)=>{if(complete){Close();return;}busy=true;install.Enabled=startup.Enabled=shortcut.Enabled=launch.Enabled=false;status.Text="설치 중…";progress.Style=ProgressBarStyle.Marquee;progress.Visible=true;bool noStartup=!startup.Checked,noShortcut=!shortcut.Checked,noLaunch=!launch.Checked;try{await Task.Run(()=>Install(noStartup,noShortcut,noLaunch,null));complete=true;status.Text="설치가 완료되었습니다. Windows 설정의 앱 목록에서 제거할 수 있습니다.";install.Text="완료";}catch(Exception error){status.Text="설치를 완료하지 못했습니다. 다시 시도해 주세요.";MessageBox.Show(this,error.Message,"One TaskBar",MessageBoxButtons.OK,MessageBoxIcon.Error);startup.Enabled=shortcut.Enabled=launch.Enabled=true;}finally{busy=false;install.Enabled=true;progress.Visible=false;}};
        FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
    }
    [STAThread]static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try{
            if(args.Length==2&&args[0]=="--verify-payload")return Install(true,true,true,args[1]);
            if(Array.IndexOf(args,"--quiet")>=0)return Install(Array.IndexOf(args,"--no-startup")>=0,Array.IndexOf(args,"--no-shortcut")>=0,Array.IndexOf(args,"--no-launch")>=0,null);
            Application.Run(new Setup());return 0;
        }catch(Exception error){try{File.WriteAllText(Path.Combine(Path.GetTempPath(),"OneTaskBar-setup-error.log"),error.ToString());}catch{}if(Array.IndexOf(args,"--quiet")<0&&Array.IndexOf(args,"--verify-payload")<0)MessageBox.Show(error.Message,"One TaskBar 설치",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}
