using System.Windows;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Gere o modo Escuro/Claro e as Cores de Destaque (Accent Colors) do Trin0 Master APP.
/// </summary>
public static class ThemeManager
{
    public static bool IsLight { get; private set; }
    public static int AccentIndex { get; private set; }

    public static readonly (string Name, string HexDark, string HexLight)[] Accents =
    {
        ("Ciano Elétrico", "#00E5FF", "#0284C7"),
        ("Roxo Cyber",     "#8B5CF6", "#7C3AED"),
        ("Verde Neon",     "#10B981", "#059669"),
        ("Vermelho ROG",   "#F43F5E", "#E11D48"),
        ("Laranja Fogo",   "#F97316", "#EA580C"),
        ("Monocromático",  "#F5F5F5", "#111111"),
    };

    private static readonly (object Key, string Dark, string Light)[] BasePalette =
    {
        ("Bg",         "#07090E", "#F8FAFC"),
        ("Panel",      "#0C0F17", "#F1F5F9"),
        ("Card",       "#111622", "#FFFFFF"),
        ("Hover",      "#1A2234", "#E2E8F0"),
        ("Line",       "#1E293B", "#CBD5E1"),
        ("LineStrong", "#334155", "#94A3B8"),
        ("Fg",         "#F8FAFC", "#0F172A"),
        ("Muted",      "#94A3B8", "#475569"),
        ("Faint",      "#475569", "#64748B"),
        ("Ink",        "#05070B", "#FFFFFF"),
        ("ThumbBrush", "#334155", "#CBD5E1"),
        ("ThumbHover", "#64748B", "#94A3B8"),
        ("ThumbDrag",  "#94A3B8", "#64748B"),
        (SystemColors.ControlBrushKey, "#07090E", "#F8FAFC"),
        (SystemColors.HighlightBrushKey, "#1A2234", "#E2E8F0"),
        (SystemColors.InactiveSelectionHighlightBrushKey, "#1A2234", "#E2E8F0"),
    };

    public static string CurrentAccentName => Accents[AccentIndex].Name;

    public static void NextAccent()
    {
        AccentIndex = (AccentIndex + 1) % Accents.Length;
        Apply(IsLight);
    }

    public static void Apply(bool light)
    {
        IsLight = light;
        var res = Application.Current.Resources;
        foreach (var (key, dark, lightHex) in BasePalette)
        {
            var color = (Color)ColorConverter.ConvertFromString(light ? lightHex : dark);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            res[key] = brush;
        }

        var acc = Accents[AccentIndex];
        var accColor = (Color)ColorConverter.ConvertFromString(light ? acc.HexLight : acc.HexDark);
        var accBrush = new SolidColorBrush(accColor);
        accBrush.Freeze();
        res["Accent"] = accBrush;

        var glowColor = Color.FromArgb(light ? (byte)0x22 : (byte)0x38, accColor.R, accColor.G, accColor.B);
        var glowBrush = new SolidColorBrush(glowColor);
        glowBrush.Freeze();
        res["AccentSoft"] = glowBrush;
    }
}