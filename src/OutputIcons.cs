using System;
using System.Text.RegularExpressions;

namespace OneTaskBar
{
    // The symbol a volume bar shows for the current sound output, as Galaxy Buds already get their own.
    // Ordinary speakers return null and keep the level icon, which changes with the volume.
    internal static class OutputIcons
    {
        const RegexOptions Options=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant;
        static readonly Regex Earbuds=new Regex(@"AirPods|Pixel\s*Buds|Earbuds?|Earphones?|WF-\d|Jabra\s*Elite|Galaxy\s*Buds|\bBuds\b|이어버드|이어폰|버즈",Options);
        static readonly Regex Headphones=new Regex(@"Headphones?|Headset|WH-\d|Over-?ear|Beats|헤드폰|헤드셋",Options);
        static readonly Regex Display=new Regex(@"HDMI|DisplayPort|\bDP\b|Monitor|\bTV\b|NVIDIA|AMD\s*High|Display|모니터|디스플레이",Options);
        internal static string Kind(string name)
        {
            if(String.IsNullOrWhiteSpace(name))return null;
            if(Earbuds.IsMatch(name))return "earbuds";
            if(Headphones.IsMatch(name))return "headphones";
            if(Display.IsMatch(name))return "computer";
            return null;
        }
    }
}
