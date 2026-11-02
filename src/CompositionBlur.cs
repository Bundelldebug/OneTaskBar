using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Effects;
using Windows.UI.Composition;

namespace OneTaskBar
{
    [ComVisible(true),Guid("2FC57384-A068-44D7-A331-30982FCF7177"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGraphicsEffectInterop
    {
        void GetEffectId(out Guid id);
        void GetNamedPropertyMapping([MarshalAs(UnmanagedType.LPWStr)]string name,out uint index,out uint mapping);
        void GetPropertyCount(out uint count);
        void GetProperty(uint index,out IntPtr value);
        void GetSource(uint index,out IGraphicsEffectSource source);
        void GetSourceCount(out uint count);
    }
    // Direct2D effect metadata consumed by the compositor. Only the backdrop
    // is blurred; text, icons, hit targets, and the WPF surface stay sharp.
    [ComVisible(true),ClassInterface(ClassInterfaceType.None)]
    public sealed class CompositionBlur : IGraphicsEffect,IGraphicsEffectSource,IGraphicsEffectInterop
    {
        // Use the ABI factory directly: .NET's PropertyValue projection unboxes
        // numbers, which loses the IPropertyValue pointer required by Direct2D.
        [ComImport,Guid("629BDBC8-D932-4FF4-96B9-8D96C5C1E858"),InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
        interface IPropertyFactory
        {
            void CreateEmpty(out IntPtr value);
            void CreateUInt8(byte input,out IntPtr value);
            void CreateInt16(short input,out IntPtr value);
            void CreateUInt16(ushort input,out IntPtr value);
            void CreateInt32(int input,out IntPtr value);
            void CreateUInt32(uint input,out IntPtr value);
            void CreateInt64(long input,out IntPtr value);
            void CreateUInt64(ulong input,out IntPtr value);
            void CreateSingle(float input,out IntPtr value);
        }
        readonly IGraphicsEffectSource source;
        internal CompositionBlur(IGraphicsEffectSource source){this.source=source;Name="SurfaceBlur";}
        public string Name {get;set;}
        public void GetEffectId(out Guid id){id=new Guid("1FEB6D69-2FE6-4AC9-8C58-1D7F93E7A6A5");}
        public void GetNamedPropertyMapping(string name,out uint index,out uint mapping)
        {
            if(name!="BlurAmount")throw new ArgumentException("Unknown effect property");
            index=0;mapping=1;
        }
        public void GetPropertyCount(out uint count){count=3;}
        public void GetProperty(uint index,out IntPtr value)
        {
            var factory=(IPropertyFactory)System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeMarshal.GetActivationFactory(typeof(Windows.Foundation.PropertyValue));
            if(index==0)factory.CreateSingle(16,out value);
            else if(index==1||index==2)factory.CreateUInt32(1,out value);
            else throw new ArgumentOutOfRangeException("index");
        }
        public void GetSource(uint index,out IGraphicsEffectSource value)
        {
            if(index!=0)throw new ArgumentOutOfRangeException("index");value=source;
        }
        public void GetSourceCount(out uint count){count=1;}
        internal static CompositionEffectBrush Create(Compositor compositor,CompositionBrush backdrop)
        {
            var effect=new CompositionBlur(new CompositionEffectSourceParameter("Backdrop"));
            using(var factory=compositor.CreateEffectFactory(effect,new[]{"SurfaceBlur.BlurAmount"})){
                var brush=factory.CreateBrush();brush.SetSourceParameter("Backdrop",backdrop);return brush;
            }
        }
    }
}
