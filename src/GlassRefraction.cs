using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics.Effects;
using Windows.UI.Composition;

namespace OneTaskBar
{
    // The compositor samples the live host backdrop. Only the bevel is displaced;
    // text and icons stay on WPF's undistorted foreground layer.
    [ComImport, ComVisible(true), Guid("2FC57384-A068-44D7-A331-30982FCF7177"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEffectInterop
    {
        void GetEffectId(out Guid id);
        void GetNamedPropertyMapping([MarshalAs(UnmanagedType.LPWStr)]string name,out uint index,out uint mapping);
        void GetPropertyCount(out uint count);
        void GetProperty(uint index,out IntPtr value);
        void GetSource(uint index,[MarshalAs(UnmanagedType.IInspectable)]out object source);
        void GetSourceCount(out uint count);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class BevelTransform : IGraphicsEffect, IEffectInterop, ICustomQueryInterface
    {
        readonly IGraphicsEffectSource source=new CompositionEffectSourceParameter("Scene");
        public string Name {get;set;}
        internal BevelTransform(){Name="Bevel";}
        CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref Guid iid,out IntPtr pointer)
        {
            pointer=IntPtr.Zero;
            if(iid!=typeof(IEffectInterop).GUID)return CustomQueryInterfaceResult.NotHandled;
            pointer=Marshal.GetComInterfaceForObject(this,typeof(IEffectInterop),CustomQueryInterfaceMode.Ignore);
            return CustomQueryInterfaceResult.Handled;
        }
        public void GetEffectId(out Guid id){id=new Guid("6AA97485-6354-4CFC-908C-E4A74F62C96C");}
        public void GetNamedPropertyMapping(string name,out uint index,out uint mapping)
        {
            if(name!="TransformMatrix")throw new ArgumentException("Unknown bevel property");
            index=2;mapping=1;
        }
        public void GetPropertyCount(out uint count){count=4;}
        public void GetProperty(uint index,out IntPtr value)
        {
            switch(index){
                case 0:value=EffectValues.UInt32(1);break;
                case 1:value=EffectValues.UInt32(1);break;
                case 2:value=EffectValues.Singles(new[]{1f,0f,0f,1f,0f,0f});break;
                case 3:value=EffectValues.Single(0);break;
                default:throw new ArgumentOutOfRangeException("index");
            }
        }
        public void GetSource(uint index,out object value){if(index!=0)throw new ArgumentOutOfRangeException("index");value=source;}
        public void GetSourceCount(out uint count){count=1;}
    }

    // Return native IPropertyValue boxes. Projecting PropertyValue.Create* as
    // System.Object unboxes scalars in .NET Framework and loses the ABI pointer.
    internal static class EffectValues
    {
        [DllImport("combase.dll",CharSet=CharSet.Unicode)]static extern int WindowsCreateString(string value,int length,out IntPtr text);
        [DllImport("combase.dll")]static extern int WindowsDeleteString(IntPtr text);
        [DllImport("combase.dll")]static extern int RoGetActivationFactory(IntPtr text,ref Guid iid,out IntPtr factory);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int UIntBox(IntPtr factory,uint value,out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SingleBox(IntPtr factory,float value,out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int ArrayBox(IntPtr factory,uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)]float[] value,out IntPtr result);
        static readonly IntPtr factory=Factory();
        static IntPtr Factory()
        {
            IntPtr text,value;const string name="Windows.Foundation.PropertyValue";
            Marshal.ThrowExceptionForHR(WindowsCreateString(name,name.Length,out text));
            try{var iid=new Guid("629BDBC8-D932-4FF4-96B9-8D96C5C1E858");Marshal.ThrowExceptionForHR(RoGetActivationFactory(text,ref iid,out value));return value;}
            finally{WindowsDeleteString(text);}
        }
        static T Method<T>(int slot)where T:class{return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory),slot*IntPtr.Size),typeof(T)) as T;}
        internal static IntPtr UInt32(uint value){IntPtr result;Marshal.ThrowExceptionForHR(Method<UIntBox>(11)(factory,value,out result));return result;}
        internal static IntPtr Single(float value){IntPtr result;Marshal.ThrowExceptionForHR(Method<SingleBox>(14)(factory,value,out result));return result;}
        internal static IntPtr Singles(float[] value){IntPtr result;Marshal.ThrowExceptionForHR(Method<ArrayBox>(33)(factory,(uint)value.Length,value,out result));return result;}
    }

    internal sealed class GlassRefraction : IDisposable
    {
        sealed class Bevel : IDisposable
        {
            internal SpriteVisual Visual;
            internal ShapeVisual MaskVisual;
            internal CompositionVisualSurface MaskSurface;
            internal CompositionSurfaceBrush MaskBrush,SceneBrush;
            internal CompositionMaskBrush Composite;
            internal CompositionRoundedRectangleGeometry Geometry;
            internal CompositionSpriteShape Shape;
            internal CompositionEffectBrush Brush;
            public void Dispose(){Visual.Dispose();Composite.Dispose();MaskBrush.Dispose();MaskSurface.Dispose();MaskVisual.Dispose();Shape.Dispose();Geometry.Dispose();Brush.Dispose();SceneBrush.Dispose();}
        }
        readonly Compositor compositor;
        readonly ContainerVisual parent;
        readonly CompositionEffectFactory factory;
        readonly CompositionVisualSurface surface;
        readonly CompositionSurfaceBrush scene;
        readonly CompositionColorBrush white;
        readonly List<Bevel> bevels=new List<Bevel>();
        int used;
        internal int Count {get{return used;}}
        internal GlassRefraction(Compositor compositor,ContainerVisual parent,SpriteVisual backdrop)
        {
            this.compositor=compositor;this.parent=parent;
            factory=compositor.CreateEffectFactory(new BevelTransform(),new[]{"Bevel.TransformMatrix"});
            surface=compositor.CreateVisualSurface();surface.SourceVisual=backdrop;
            scene=compositor.CreateSurfaceBrush(surface);scene.Stretch=CompositionStretch.None;
            white=compositor.CreateColorBrush(Windows.UI.Color.FromArgb(255,255,255,255));
        }
        internal void Begin(){used=0;surface.SourceSize=parent.Size;}
        internal void Add(float x,float y,float width,float height,float radius,float thickness)
        {
            if(width<4||height<4)return;
            Bevel bevel;
            if(used==bevels.Count){
                var geometry=compositor.CreateRoundedRectangleGeometry();
                var source=compositor.CreateSurfaceBrush(surface);source.Stretch=CompositionStretch.None;
                var brush=factory.CreateBrush();brush.SetSourceParameter("Scene",source);
                var shape=compositor.CreateSpriteShape(geometry);shape.StrokeBrush=white;
                var mask=compositor.CreateShapeVisual();mask.BorderMode=CompositionBorderMode.Soft;mask.Shapes.Add(shape);
                var maskSurface=compositor.CreateVisualSurface();maskSurface.SourceVisual=mask;
                var maskBrush=compositor.CreateSurfaceBrush(maskSurface);maskBrush.Stretch=CompositionStretch.None;
                var composite=compositor.CreateMaskBrush();composite.Source=brush;composite.Mask=maskBrush;
                var visual=compositor.CreateSpriteVisual();visual.Brush=composite;visual.BorderMode=CompositionBorderMode.Soft;parent.Children.InsertAtTop(visual);
                bevel=new Bevel {Visual=visual,Geometry=geometry,Shape=shape,Brush=brush,SceneBrush=source,MaskVisual=mask,MaskSurface=maskSurface,MaskBrush=maskBrush,Composite=composite};bevels.Add(bevel);
            }else bevel=bevels[used];
            used++;bevel.Visual.IsVisible=true;
            var size=new Vector2(width,height);bevel.Visual.Size=size;bevel.Visual.Offset=new Vector3(x,y,0);
            bevel.MaskVisual.Size=size;bevel.MaskSurface.SourceSize=size;
            bevel.SceneBrush.TransformMatrix=Matrix3x2.CreateTranslation(-x,-y);
            float inset=thickness*.5f;
            bevel.Geometry.Offset=new Vector2(inset,inset);
            bevel.Geometry.Size=new Vector2(Math.Max(1,width-thickness),Math.Max(1,height-thickness));
            bevel.Geometry.CornerRadius=new Vector2(Math.Max(0,radius-inset));
            bevel.Shape.StrokeThickness=thickness;
            // A convex bevel magnifies the scene toward the center. The outer
            // highlight is rendered separately, so brightness cannot wash out ink.
            float scale=1.045f;
            var matrix=Matrix3x2.CreateScale(scale,new Vector2(width*.5f,height*.5f));
            bevel.Brush.Properties.InsertMatrix3x2("Bevel.TransformMatrix",matrix);
        }
        internal void End(){for(int i=used;i<bevels.Count;i++)bevels[i].Visual.IsVisible=false;}
        public void Dispose()
        {
            foreach(var bevel in bevels){parent.Children.Remove(bevel.Visual);bevel.Dispose();}
            bevels.Clear();white.Dispose();scene.Dispose();surface.Dispose();factory.Dispose();
        }
    }
}
