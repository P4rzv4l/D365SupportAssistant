// =============================================================================
//  ToolsUiFactory.cs — Primitivos de UI reutilizáveis do ToolsView
// =============================================================================

using D365Assistant.Views.Tools.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views.Tools.Components;

public static class ToolsUiFactory
{
    // ── Buttons ───────────────────────────────────────────────────────────────

    public static Button ActionButton(string text, Color bg) => new()
    {
        Content = text,
        Background = ToolsTheme.Brush(bg),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Cursor = Cursors.Hand,
        Padding = new Thickness(16, 9, 16, 9),
    };

    public static Button TabButton(string text, bool active)
    {
        var indicator = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 2),
            BorderBrush = active ? ToolsTheme.Brush(ToolsTheme.Accent) : Brushes.Transparent,
            Padding = new Thickness(16, 10, 16, 10),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = active
                    ? ToolsTheme.Brush(ToolsTheme.Text)
                    : ToolsTheme.Brush(ToolsTheme.TextSub),
            },
        };

        return new Button
        {
            Content = indicator,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(0),
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = active
                ? ToolsTheme.Brush(ToolsTheme.Text)
                : ToolsTheme.Brush(ToolsTheme.TextSub),
        };
    }

    // ── Inputs ────────────────────────────────────────────────────────────────

    public static TextBox InputBox() => new()
    {
        Background = ToolsTheme.Brush(ToolsTheme.Bg),
        Foreground = ToolsTheme.Brush(ToolsTheme.Text),
        CaretBrush = ToolsTheme.Brush(ToolsTheme.Text),
        BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
        BorderThickness = new Thickness(1),
        FontSize = 13,
        FontFamily = new FontFamily("Consolas"),
        Padding = new Thickness(10, 7, 10, 7),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 10, 0),
    };

    // ── Card ──────────────────────────────────────────────────────────────────

    public static Border Card(Thickness? margin = null) => new()
    {
        Background = ToolsTheme.Brush(ToolsTheme.Surface),
        BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(20, 16, 20, 16),
        Margin = margin ?? new Thickness(0),
    };

    // ── Divider ───────────────────────────────────────────────────────────────

    public static Border VerticalSeparator() => new()
    {
        Width = 1,
        Background = ToolsTheme.Brush(ToolsTheme.Surface2),
        Margin = new Thickness(0, 2, 18, 2),
    };

    // ── GridView column ───────────────────────────────────────────────────────

    public static GridViewColumn GridCol(string header, string binding, double width) => new()
    {
        Header = header,
        Width = width,
        DisplayMemberBinding = new Binding(binding),
    };

    // ── Binding factory ───────────────────────────────────────────────────────

    public static Binding Bind(
        string path,
        bool twoWay = false,
        IValueConverter? converter = null,
        string? stringFormat = null,
        UpdateSourceTrigger trigger = UpdateSourceTrigger.Default)
    {
        var b = new Binding(path)
        {
            Mode = twoWay ? BindingMode.TwoWay : BindingMode.OneWay,
            UpdateSourceTrigger = trigger,
        };
        if (converter != null) b.Converter = converter;
        if (stringFormat != null) b.StringFormat = stringFormat;
        return b;
    }

    // ── Tab highlight ─────────────────────────────────────────────────────────

    public static void SetTabActive(Button btn, bool active)
    {
        btn.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        btn.Foreground = active
            ? ToolsTheme.Brush(ToolsTheme.Text)
            : ToolsTheme.Brush(ToolsTheme.TextSub);

        if (btn.Content is Border bd)
            bd.BorderBrush = active
                ? ToolsTheme.Brush(ToolsTheme.Accent)
                : Brushes.Transparent;
    }
}