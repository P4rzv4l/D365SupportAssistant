// =============================================================================
//  TodoUiFactory.cs — Primitivos de UI do TodoView
// =============================================================================
// Sem lógica de negócio. Só forma, cor, cursor, layout.
// Reutiliza DashboardTheme como fonte única de cores.
// =============================================================================

using D365Assistant.Views.Dashboard.Theme;
using D365Assistant.Views.Todo.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace D365Assistant.Views.Todo.Components;

public static class TodoUiFactory
{
    // ── Buttons ───────────────────────────────────────────────────────────────

    public static Button PrimaryButton(string label) => new()
    {
        Content = label,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Background = DashboardTheme.Brush(DashboardTheme.Purple),
        Foreground = DashboardTheme.Brush(Color.FromRgb(0x0F, 0x08, 0x20)),
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Padding = new Thickness(16, 8, 16, 8),
    };

    public static Button OutlineButton(string label) => new()
    {
        Content = label,
        FontSize = 12,
        Background = DashboardTheme.Brush(DashboardTheme.Surface2),
        Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border2),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(14, 7, 14, 7),
    };

    public static Button LinkButton(string label, Color? color = null) => new()
    {
        Content = label,
        FontSize = 11,
        Background = Brushes.Transparent,
        Foreground = DashboardTheme.Brush(color ?? DashboardTheme.Accent),
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Padding = new Thickness(6, 4, 6, 4),
        Margin = new Thickness(2, 0, 2, 0),
    };

    // ── Badge ─────────────────────────────────────────────────────────────────

    public static Border Badge(string text, string fgHex, string bgHex) => new()
    {
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 3, 8, 3),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex)),
        },
    };

    public static Border Badge(BadgeInfo info) =>
        Badge(info.Label, info.FgHex, info.BgHex);

    // ── Form inputs ───────────────────────────────────────────────────────────

    public static TextBox FormInput(string placeholder = "") => new()
    {
        Background = DashboardTheme.Brush(DashboardTheme.Bg),
        Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = new Thickness(1),
        FontSize = 12,
        Padding = new Thickness(10, 8, 10, 8),
        Margin = new Thickness(0, 0, 0, 16),
    };

    public static TextBlock FormLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        Margin = new Thickness(0, 0, 0, 5),
    };

    // ── Checkbox ──────────────────────────────────────────────────────────────

    public static Border Checkbox(bool done) => new()
    {
        Width = 16,
        Height = 16,
        CornerRadius = new CornerRadius(3),
        BorderThickness = new Thickness(1.5),
        BorderBrush = done
            ? DashboardTheme.Brush(DashboardTheme.Green)
            : new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)),
        Background = done
            ? DashboardTheme.AlphaBrush(DashboardTheme.Green, 0x20)
            : Brushes.Transparent,
        Margin = new Thickness(0, 0, 10, 0),
        Cursor = Cursors.Hand,
        Child = done ? new TextBlock
        {
            Text = "✓",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Green),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        } : null,
    };

    // ── Dropdown ──────────────────────────────────────────────────────────────

    public static (Border host,
                   Func<string?> getValue,
                   Action<string> setValue,
                   Action<List<string>> setItems)
        Dropdown(Action<string>? onSelected = null)
    {
        var items = new List<string>();
        var selectedIdx = 0;
        Popup? popup = null;

        var selectedTb = new TextBlock
        {
            FontSize = 12,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var arrow = new TextBlock
        {
            Text = "⌄",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        var innerGrid = new Grid();
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        innerGrid.Children.Add(selectedTb); Grid.SetColumn(selectedTb, 0);
        innerGrid.Children.Add(arrow); Grid.SetColumn(arrow, 1);

        var host = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 34,
            Cursor = Cursors.Hand,
            Child = innerGrid,
        };

        host.MouseEnter += (_, _) => host.Background = DashboardTheme.Brush(DashboardTheme.Surface3);
        host.MouseLeave += (_, _) => host.Background = DashboardTheme.Brush(DashboardTheme.Surface2);
        host.MouseLeftButtonUp += (_, _) => { RebuildPopup(); popup!.IsOpen = !popup.IsOpen; };

        void RebuildPopup()
        {
            popup?.Let(p => p.IsOpen = false);
            var list = new StackPanel { Background = DashboardTheme.Brush(DashboardTheme.Surface) };

            for (int i = 0; i < items.Count; i++)
            {
                var idx = i;
                var item = items[i];

                var row = new Border
                {
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = idx == selectedIdx
                        ? DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x30)
                        : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = item,
                        FontSize = 12,
                        Foreground = idx == selectedIdx
                            ? DashboardTheme.Brush(DashboardTheme.Purple)
                            : DashboardTheme.Brush(DashboardTheme.Text),
                    },
                };

                row.MouseEnter += (_, _) =>
                {
                    if (idx != selectedIdx)
                        row.Background = DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x18);
                };
                row.MouseLeave += (_, _) =>
                {
                    row.Background = idx == selectedIdx
                        ? DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x30)
                        : Brushes.Transparent;
                };
                row.MouseLeftButtonUp += (_, _) =>
                {
                    selectedIdx = idx;
                    selectedTb.Text = item;
                    popup?.Let(p => p.IsOpen = false);
                    onSelected?.Invoke(item);
                };

                list.Children.Add(row);
                if (i < items.Count - 1)
                    list.Children.Add(new Border
                    {
                        Height = 1,
                        Background = DashboardTheme.Brush(DashboardTheme.Border),
                    });
            }

            popup = new Popup
            {
                Child = new Border
                {
                    Child = list,
                    Background = DashboardTheme.Brush(DashboardTheme.Surface),
                    BorderBrush = DashboardTheme.Brush(DashboardTheme.Border2),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 12,
                        Opacity = 0.15,
                        ShadowDepth = 3,
                    },
                },
                PlacementTarget = host,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                MinWidth = host.ActualWidth > 0 ? host.ActualWidth : 150,
            };

            popup.Opened += (_, _) =>
            {
                host.BorderBrush = DashboardTheme.Brush(DashboardTheme.Accent);
                arrow.Text = "⌃";
            };
            popup.Closed += (_, _) =>
            {
                host.BorderBrush = DashboardTheme.Brush(DashboardTheme.Border);
                arrow.Text = "⌄";
            };
        }

        string? getValue() => selectedIdx < items.Count ? items[selectedIdx] : null;

        void setValue(string val)
        {
            var i = items.IndexOf(val);
            if (i >= 0) { selectedIdx = i; selectedTb.Text = items[i]; }
        }

        void setItems(List<string> n)
        {
            items.Clear();
            items.AddRange(n);
            if (n.Count > 0) { selectedIdx = 0; selectedTb.Text = n[0]; }
        }

        return (host, getValue, setValue, setItems);
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    /// <summary>Grid de duas colunas iguais com gap fixo entre elas.</summary>
    public static Grid TwoColumnForm(double gap = 14)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return g;
    }

    public static Border HorizontalDivider() => new()
    {
        Height = 1,
        Background = DashboardTheme.Brush(DashboardTheme.Border),
    };
}

file static class PopupExtensions
{
    public static void Let<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj is not null) action(obj);
    }
}