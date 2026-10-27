using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI.Composition;

namespace OneTaskBar
{
    // A live visual surface stays on the GPU. Bevel masks use local coordinates;
    // the scene brush alone is offset and magnified, keeping every edge continuous.
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
            public void Dispose()
            {
                Visual.Dispose();Composite.Dispose();MaskBrush.Dispose();MaskSurface.Dispose();
                MaskVisual.Dispose();Shape.Dispose();Geometry.Dispose();SceneBrush.Dispose();
            }
        }
        readonly Compositor compositor;
        readonly ContainerVisual parent;
        readonly CompositionVisualSurface surface;
        readonly CompositionColorBrush white;
        readonly List<Bevel> bevels=new List<Bevel>();
        int used;
        internal int Count {get{return used;}}
        internal GlassRefraction(Compositor compositor,ContainerVisual parent,SpriteVisual backdrop)
        {
            this.compositor=compositor;this.parent=parent;
            surface=compositor.CreateVisualSurface();surface.SourceVisual=backdrop;
            white=compositor.CreateColorBrush(Windows.UI.Color.FromArgb(255,255,255,255));
        }
        CompositionSurfaceBrush Brush(CompositionVisualSurface source)
        {
            var brush=compositor.CreateSurfaceBrush(source);
            brush.Stretch=CompositionStretch.None;
            brush.HorizontalAlignmentRatio=0;brush.VerticalAlignmentRatio=0;
            return brush;
        }
        internal void Begin(){used=0;surface.SourceSize=parent.Size;}
        internal void Add(float x,float y,float width,float height,float radius,float thickness)
        {
            if(width<4||height<4)return;
            Bevel bevel;
            if(used==bevels.Count){
                var geometry=compositor.CreateRoundedRectangleGeometry();
                var source=Brush(surface);
                var shape=compositor.CreateSpriteShape(geometry);shape.StrokeBrush=white;
                var mask=compositor.CreateShapeVisual();mask.BorderMode=CompositionBorderMode.Soft;mask.Shapes.Add(shape);
                var maskSurface=compositor.CreateVisualSurface();maskSurface.SourceVisual=mask;
                var maskBrush=Brush(maskSurface);
                var composite=compositor.CreateMaskBrush();composite.Source=source;composite.Mask=maskBrush;
                var visual=compositor.CreateSpriteVisual();visual.Brush=composite;visual.BorderMode=CompositionBorderMode.Soft;visual.Opacity=.48f;
                parent.Children.InsertAtTop(visual);
                bevel=new Bevel {Visual=visual,Geometry=geometry,Shape=shape,SceneBrush=source,MaskVisual=mask,MaskSurface=maskSurface,MaskBrush=maskBrush,Composite=composite};bevels.Add(bevel);
            }else bevel=bevels[used];
            used++;bevel.Visual.IsVisible=true;
            var size=new Vector2(width,height);
            bevel.Visual.Size=size;bevel.Visual.Offset=new Vector3(x,y,0);
            bevel.MaskVisual.Size=size;bevel.MaskSurface.SourceSize=size;
            bevel.SceneBrush.TransformMatrix=Matrix3x2.CreateTranslation(-x,-y)*Matrix3x2.CreateScale(1.012f,new Vector2(width*.5f,height*.5f));
            float inset=thickness*.5f;
            bevel.Geometry.Offset=new Vector2(inset,inset);
            bevel.Geometry.Size=new Vector2(Math.Max(1,width-thickness),Math.Max(1,height-thickness));
            bevel.Geometry.CornerRadius=new Vector2(Math.Max(0,radius-inset));
            bevel.Shape.StrokeThickness=thickness;
        }
        internal void End(){for(int i=used;i<bevels.Count;i++)bevels[i].Visual.IsVisible=false;}
        public void Dispose()
        {
            foreach(var bevel in bevels){parent.Children.Remove(bevel.Visual);bevel.Dispose();}
            bevels.Clear();white.Dispose();surface.Dispose();
        }
    }
}
