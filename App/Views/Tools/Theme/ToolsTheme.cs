// =============================================================================
//  ToolsTheme.cs — Paleta de cores do ToolsView
// =============================================================================

using System.Windows.Media;

namespace D365Assistant.Views.Tools.Theme;

public static class ToolsTheme
{
    // ── Backgrounds ───────────────────────────────────────────────────────────
    public static readonly Color Bg = Parse("#0D1117");
    public static readonly Color Surface = Parse("#161B22");
    public static readonly Color Surface2 = Parse("#21262D");

    // ── Borders ───────────────────────────────────────────────────────────────
    public static readonly Color Border = Parse("#30363D");

    // ── Text ──────────────────────────────────────────────────────────────────
    public static readonly Color Text = Parse("#E6EDF3");
    public static readonly Color TextSub = Parse("#8B949E");
    public static readonly Color TextMuted = Parse("#484F58");

    // ── Semantic ──────────────────────────────────────────────────────────────
    public static readonly Color Accent = Parse("#7C3AED");
    public static readonly Color Blue = Parse("#58A6FF");
    public static readonly Color Yellow = Parse("#F0DB4F");
    public static readonly Color Purple = Parse("#B392F0");
    public static readonly Color Gray = Parse("#8B949E");

    // ── Brush factory ─────────────────────────────────────────────────────────
    public static SolidColorBrush Brush(Color c) => new(c);
    public static SolidColorBrush Brush(string hex) => new(Parse(hex));

    private static Color Parse(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex);
}