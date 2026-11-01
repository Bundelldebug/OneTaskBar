using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // These brushes belong to a live window. A binding keeps a cached drawing
    // from freezing its borrowed brush while retaining compositor color updates.
    internal sealed class BackdropInkPalette : DependencyObject
    {
        public static readonly DependencyProperty PrimaryColorProperty=DependencyProperty.Register("PrimaryColor",typeof(Color),typeof(BackdropInkPalette));
        public static readonly DependencyProperty SecondaryColorProperty=DependencyProperty.Register("SecondaryColor",typeof(Color),typeof(BackdropInkPalette));
        public Color PrimaryColor {get{return (Color)GetValue(PrimaryColorProperty);}set{SetValue(PrimaryColorProperty,value);}}
        public Color SecondaryColor {get{return (Color)GetValue(SecondaryColorProperty);}set{SetValue(SecondaryColorProperty,value);}}
        internal readonly SolidColorBrush Primary,Secondary;

        internal BackdropInkPalette()
        {
            PrimaryColor=Color.FromRgb(245,247,252);
            SecondaryColor=Color.FromRgb(209,217,231);
            Primary=Create("PrimaryColor");
            Secondary=Create("SecondaryColor");
        }
        SolidColorBrush Create(string path)
        {
            var brush=new SolidColorBrush();
            BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding(path) {Source=this,Mode=BindingMode.OneWay});
            return brush;
        }
        internal static bool Owns(Brush brush)
        {
            if(!(brush is SolidColorBrush))return false;
            var binding=BindingOperations.GetBinding(brush,SolidColorBrush.ColorProperty);
            return binding!=null&&binding.Source is BackdropInkPalette;
        }
        internal void Set(bool dark,bool animate)
        {
            Set(Primary,PrimaryColorProperty,dark?Color.FromRgb(23,35,54):Color.FromRgb(245,247,252),animate);
            Set(Secondary,SecondaryColorProperty,dark?Color.FromRgb(49,63,79):Color.FromRgb(218,225,235),animate);
        }
        void Set(SolidColorBrush brush,DependencyProperty property,Color target,bool animate)
        {
            if((Color)GetValue(property)==target)return;
            Color current=brush.Color;
            brush.BeginAnimation(SolidColorBrush.ColorProperty,null);
            SetValue(property,target);
            if(animate&&Motion.Enabled)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(current,target,TimeSpan.FromMilliseconds(160))
                {
                    FillBehavior=FillBehavior.Stop,EasingFunction=new SineEase {EasingMode=EasingMode.EaseInOut}
                });
            }
        }
        internal void Stop()
        {
            // Also tolerate a brush supplied by an older cached visual at shutdown.
            if(!Primary.IsFrozen)Primary.BeginAnimation(SolidColorBrush.ColorProperty,null);
            if(!Secondary.IsFrozen)Secondary.BeginAnimation(SolidColorBrush.ColorProperty,null);
        }
    }
}
