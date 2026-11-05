using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OneTaskBar
{
    // The name shown for the person using the PC: the display name of the signed-in Microsoft account (or the
    // full name set on a local account), not the short folder name of the user profile. Read once and kept.
    internal static class AccountName
    {
        [DllImport("secur32.dll",CharSet=CharSet.Unicode)]
        static extern int GetUserNameEx(int format,StringBuilder name,ref uint size);
        static string cached;
        internal static string Display()
        {
            if(cached!=null)return cached;
            string value=null;
            try
            {
                var builder=new StringBuilder(256);
                uint size=(uint)builder.Capacity;
                // NameDisplay: "Bae jiho", the name Windows shows on the sign-in screen.
                if(GetUserNameEx(3,builder,ref size)!=0)value=builder.ToString().Trim();
            }
            catch(Exception error)
            {
                AppLog.Once("account-name",error.GetType().Name);
            }
            if(String.IsNullOrWhiteSpace(value))
            {
                try
                {
                    using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI"))
                    {
                        var name=key==null?null:key.GetValue("LastLoggedOnDisplayName") as string;
                        if(!String.IsNullOrWhiteSpace(name))value=name.Trim();
                    }
                }
                catch(Exception error)
                {
                    AppLog.Once("account-name-registry",error.GetType().Name);
                }
            }
            return cached=String.IsNullOrWhiteSpace(value)?Environment.UserName:value;
        }
    }
}
