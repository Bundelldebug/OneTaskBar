using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
namespace OneTaskBar
{
    internal static class QuickSettingIcons
    {
        // Distinct 24-DIP line symbols share the rest of the taskbar's stroke weight.
        static readonly Dictionary<string,Geometry> paths=new Dictionary<string,Geometry>();
        static readonly string[][] definitions={
            new[]{"blur","M4,5 L4,19 M9,3 L9,21 M14,5 L14,19 M19,8 L19,16"},
            new[]{"charge-motion","M14,1 L6,13 12,13 10,23 19,10 13,10 Z M3,6 L1,10 M21,14 L23,18"},
            new[]{"hover","M4,2 L4,19 9,14 13,22 17,20 13,12 21,11 Z"},
            new[]{"running-dots","M3,3 L21,3 21,17 3,17 Z M6,21 L7,21 M11,21 L12,21 M16,21 L17,21"},
            new[]{"seconds","M12,2 A10,10 0 1 1 11.9,2 M12,5 L12,12 17,16 M12,2 L12,3"},
            new[]{"notification-popups","M2,4 L22,4 22,18 14,18 9,22 9,18 2,18 Z M6,9 L18,9 M6,13 L14,13"},
            new[]{"buds-popup","M3,5 C3,1 9,1 9,5 L9,13 6,17 6,9 M15,5 C15,1 21,1 21,5 L21,13 18,17 18,9 M9,21 L15,21"},
            new[]{"dock-theme","M12,2 A10,10 0 1 1 11.9,2 M12,2 L12,22 M3,8 L9,8 M2,12 L9,12 M3,16 L9,16"},
            new[]{"solid-dock","M3,4 L21,4 21,20 3,20 Z M4,5 L20,19 M9,5 L20,14 M4,10 L14,20"},
            new[]{"duo","M4,17 A9,9 0 1 1 20,17 M8,12 Q12,8 16,12 M10,15 Q12,13 14,15 M8,21 L8.2,21 M12,22 L12.2,22 M16,21 L16.2,21"},
            new[]{"numbers","M9,2 L5,22 M18,2 L14,22 M3,8 L22,8 M1,16 L20,16"},
            new[]{"window-drop","M2,4 L22,4 22,21 2,21 Z M12,4 L12,21 M5,10 L8,13 5,16 M19,10 L16,13 19,16"},
            new[]{"cover","M2,2 L22,2 22,22 2,22 Z M5,18 L10,11 14,15 18,9 21,14 M7,6 A1,1 0 1 1 6.9,6"},
            new[]{"charge-sound","M9,1 L3,12 8,12 6,22 15,9 10,9 Z M17,8 Q22,12 17,16 M19,4 Q27,12 19,20"},
            new[]{"emergency-exit","M10,2 L3,2 3,22 10,22 M9,12 L23,12 M18,7 L23,12 18,17"},
            new[]{"native-start","M2,5 L22,5 22,20 2,20 Z M8,9 L12,9 12,13 8,13 Z M14,9 L18,9 18,13 14,13 Z M8,16 L18,16"},
            new[]{"native-search","M10,3 A7,7 0 1 1 9.9,3 M15,15 L22,22 M6,7 L13,7 M6,11 L13,11"},
            new[]{"native-switcher","M2,7 L17,7 17,22 2,22 Z M7,2 L22,2 22,17 M7,12 L12,12 M10,10 L12,12 10,14"},
            new[]{"native-center","M2,3 L22,3 22,21 2,21 Z M8,3 L8,21 M12,8 L19,8 M12,12 L19,12 M12,16 L16,16"},
            new[]{"native-quick","M3,3 L9,3 9,9 3,9 Z M15,3 L21,3 21,9 15,9 Z M3,15 L9,15 9,21 3,21 Z M18,14 L18,22 M14,18 L22,18"},
            new[]{"native-button","M3,3 L10,3 10,10 3,10 Z M14,3 L21,3 21,10 14,10 Z M3,14 L10,14 10,21 3,21 Z M15,14 L15,22 18,19 22,19 Z"},
            new[]{"notification-sync","M6,16 L6,9 A6,6 0 0 1 18,9 L18,16 21,18 3,18 Z M9,21 Q12,24 15,21 M1,9 L1,13 M23,9 L23,13"}
        };
        static QuickSettingIcons(){foreach(var entry in definitions){var geometry=Geometry.Parse(entry[1]);geometry.Freeze();paths[entry[0]]=geometry;}}
        internal static void Draw(DrawingContext context,string key,Pen pen){Geometry path;if(paths.TryGetValue(key,out path))context.DrawGeometry(null,pen,path);}
    }
}
