using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
namespace OneTaskBar
{
    internal static class IdentityPackage
    {
        [StructLayout(LayoutKind.Sequential)]struct Settings {[MarshalAs(UnmanagedType.Bool)]public bool ForceZip32;public IntPtr HashMethod;}
        [ComImport,Guid("BEB94909-E451-438B-B5A7-D79E767B75D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IFactory {void CreatePackageWriter(IStream output,ref Settings settings,out IWriter writer);}
        [ComImport,Guid("9099E33B-246F-41E4-881A-008EB613F858"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IWriter {void AddPayloadFile([MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string contentType,int compression,IStream input);void Close(IStream manifest);}
        [DllImport("shlwapi.dll",CharSet=CharSet.Unicode,PreserveSig=false)]static extern void SHCreateStreamOnFileEx(string path,uint mode,uint attributes,[MarshalAs(UnmanagedType.Bool)]bool create,IStream template,out IStream stream);
        [DllImport("urlmon.dll",CharSet=CharSet.Unicode,PreserveSig=false)]static extern void CreateUri(string value,uint flags,IntPtr reserved,out IntPtr uri);
        static IStream Read(string path){IStream stream;SHCreateStreamOnFileEx(path,0x40,0,false,null,out stream);return stream;}
        internal static string Build(string manifest,string directory)
        {
            string path=Path.Combine(directory,"OneTaskBar.Identity.msix");object instance=null;IWriter writer=null;IStream output=null,xml=null;IntPtr hash=IntPtr.Zero;
            try {
                instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("5842A140-FF9F-4166-8F5C-62F5B7B0C781")));SHCreateStreamOnFileEx(path,0x1012,0x80,true,null,out output);CreateUri("http://www.w3.org/2001/04/xmlenc#sha256",0,IntPtr.Zero,out hash);var settings=new Settings {ForceZip32=true,HashMethod=hash};((IFactory)instance).CreatePackageWriter(output,ref settings,out writer);
                foreach(string name in new[]{"StoreLogo.png","Square150x150Logo.png","Square44x44Logo.png"}){var input=Read(Path.Combine(AppLog.Root,"assets",name));try{writer.AddPayloadFile("assets\\"+name,"image/png",1,input);}finally{Marshal.ReleaseComObject(input);}}
                xml=Read(manifest);writer.Close(xml);return path;
            }finally{if(xml!=null)Marshal.ReleaseComObject(xml);if(writer!=null)Marshal.ReleaseComObject(writer);if(output!=null)Marshal.ReleaseComObject(output);if(instance!=null)Marshal.ReleaseComObject(instance);if(hash!=IntPtr.Zero)Marshal.Release(hash);}
        }
        internal static int Probe(){try{string directory=Path.Combine(AppLog.Data,"identity-package-check");Directory.CreateDirectory(directory);string manifest=Path.Combine(directory,"AppxManifest.xml");File.WriteAllText(manifest,NotificationIdentity.Manifest(),new System.Text.UTF8Encoding(false));string package=Build(manifest,directory);bool passed=File.Exists(package)&&new FileInfo(package).Length>1000;AppLog.Json("identity-package-probe.json",new {passed=passed,bytes=new FileInfo(package).Length,uri=NotificationIdentity.FileUri(package).AbsoluteUri,registrationAttempted=false});return passed?0:1;}catch(Exception error){AppLog.Json("identity-package-probe.json",new {passed=false,error=error.ToString()});return 1;}}
    }
}
