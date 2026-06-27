// =============================================================================
//  DashboardTheme.cs — Paleta centralizada (única fonte da verdade)
// =============================================================================
// Regra: NUNCA escreva Color.FromRgb(...) fora deste arquivo.
// Todos os componentes visuais importam daqui.
// =============================================================================

using System.Windows.Media;

namespace D365Assistant.Views.Dashboard.Theme;

/// <summary>
/// Paleta de cores do Dashboard. Imutável e estática — sem instâncias.
/// </summary>
public static class DashboardTheme
{
    // ── Backgrounds ───────────────────────────────────────────────────────────
    public static readonly Color Bg = Color.FromRgb(0x08, 0x0C, 0x12);
    public static readonly Color Surface = Color.FromRgb(0x0F, 0x15, 0x20);
    public static readonly Color Surface2 = Color.FromRgb(0x13, 0x1B, 0x27);
    public static readonly Color Surface3 = Color.FromRgb(0x18, 0x22, 0x30);

    // ── Borders ───────────────────────────────────────────────────────────────
    public static readonly Color Border = Color.FromRgb(0x1E, 0x28, 0x38);
    public static readonly Color Border2 = Color.FromRgb(0x28, 0x36, 0x48);

    // ── Text ──────────────────────────────────────────────────────────────────
    public static readonly Color Text = Color.FromRgb(0xE2, 0xE8, 0xF0);
    public static readonly Color TextSub = Color.FromRgb(0x64, 0x74, 0x8B);

    // ── Semantic ──────────────────────────────────────────────────────────────
    public static readonly Color Accent = Color.FromRgb(0x3B, 0x82, 0xF6);
    public static readonly Color Green = Color.FromRgb(0x22, 0xC5, 0x5E);
    public static readonly Color Red = Color.FromRgb(0xEF, 0x44, 0x44);
    public static readonly Color Yellow = Color.FromRgb(0xF5, 0x9E, 0x0B);
    public static readonly Color Purple = Color.FromRgb(0xA7, 0x8B, 0xFA);
    public static readonly Color Orange = Color.FromRgb(0xF9, 0x73, 0x16);

    // ── Row states ────────────────────────────────────────────────────────────
    public static readonly Color RowHover = Color.FromRgb(0x12, 0x1C, 0x2C);
    public static readonly Color RowSelected = Color.FromRgb(0x16, 0x22, 0x36);

    // ── Brush factory (evita new SolidColorBrush() espalhado) ────────────────
    public static SolidColorBrush Brush(Color c) => new(c);
    public static SolidColorBrush AlphaBrush(Color c, byte alpha) =>
        new(Color.FromArgb(alpha, c.R, c.G, c.B));
}