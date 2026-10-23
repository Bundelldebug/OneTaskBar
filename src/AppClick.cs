using System.Windows.Input;
namespace OneTaskBar
{
    internal static class AppClick
    {
        internal static bool Elevate(ModifierKeys keys){return (keys&(ModifierKeys.Control|ModifierKeys.Shift))==(ModifierKeys.Control|ModifierKeys.Shift)&&(keys&(ModifierKeys.Alt|ModifierKeys.Windows))==0;}
    }
}
