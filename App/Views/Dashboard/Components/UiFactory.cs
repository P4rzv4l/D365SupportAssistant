// =============================================================================
//  UiFactory.cs — Fábrica de primitivos de UI reutilizáveis
// =============================================================================
// Regra: NENHUM dado de negócio aqui. Só forma, cor, cursor, layout.
// Cada método retorna um controle autossuficiente, sem referências externas.
// =============================================================================

using D365Assistant.Views.Dashboard.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FontAwesome.Sharp;

namespace D365Assistant.Views.Dashboard.Components;

public static class UiFactory
{
    // ── Badge ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Badge colorido dark-mode. Aceita hex strings para manter compatibilidade
    /// com <see cref="Helpers.IncidentDisplayMappers"/>.
    /// </summary>
    public static Border Badge(IconChar icon, string text, string fgHex, string bgHex,
                               Thickness? margin = null)
    {
        //var fg = ParseColor(fgHex);
        //return new Border
        //{
        //    Background = DashboardTheme.Brush(ParseColor(bgHex)),
        //    BorderBrush = DashboardTheme.AlphaBrush(fg, 0x44),
        //    BorderThickness = new Thickness(1),
        //    CornerRadius = new CornerRadius(4),
        //    Padding = new Thickness(8, 3, 8, 3),
        //    Margin = margin ?? new Thickness(0),
        //    Child = new TextBlock
        //    {
        //        Text = text,
        //        FontSize = 10.5,
        //        FontWeight = FontWeights.SemiBold,
        //        Foreground = DashboardTheme.Brush(fg),
        //    }
        //};

        var fg = ParseColor(fgHex);

        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        var iconControl = new IconBlock
        {
            Icon = icon,
            Width = 11,
            Height = 11,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(fg),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        };

        Grid.SetColumn(iconControl, 0);

        var label = new TextBlock
        {
            Text = text,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(fg),
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(label, 1);

        grid.Children.Add(iconControl);
        grid.Children.Add(label);

        return new Border
        {
            Background = DashboardTheme.Brush(ParseColor(bgHex)),
            BorderBrush = DashboardTheme.AlphaBrush(fg, 0x44),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = margin ?? new Thickness(0),
            Child = grid
        };
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    public static Button OutlineButton(string label) => new()
    {
        Content = label,
        FontSize = 11,
        Background = DashboardTheme.Brush(DashboardTheme.Surface2),
        Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(12, 6, 12, 6),
        Margin = new Thickness(8, 0, 0, 0),
    };

    //public static Button ActionButton(IconChar icon,string label, Color accent) => new()
    //{
    //    Content = label,
    //    FontSize = 11,
    //    FontFamily = new FontFamily("Segoe UI Semibold"),
    //    Background = DashboardTheme.AlphaBrush(accent, 0x20),
    //    Foreground = DashboardTheme.Brush(accent),
    //    BorderBrush = DashboardTheme.AlphaBrush(accent, 0x44),
    //    BorderThickness = new Thickness(1),
    //    Cursor = Cursors.Hand,
    //    Padding = new Thickness(12, 6, 12, 6),
    //};

    public static Button ActionButton(IconChar icon, string label, Color accent)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        content.Children.Add(new IconBlock
        {
            Icon = icon,
            Width = 14,
            Height = 14,
            Foreground = DashboardTheme.Brush(accent),
            VerticalAlignment = VerticalAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = 11,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            Foreground = DashboardTheme.Brush(accent),
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Button
        {
            Content = content,
            Background = DashboardTheme.AlphaBrush(accent, 0x20),
            BorderBrush = DashboardTheme.AlphaBrush(accent, 0x44),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(12, 6, 12, 6)
        };
    }

    public static Button GhostButton(string label) => new()
    {
        Content = label,
        FontSize = 10,
        Background = Brushes.Transparent,
        Foreground = DashboardTheme.Brush(DashboardTheme.Accent),
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Padding = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Left,
    };

    /// <summary>Botão de paginação pequeno.</summary>
    public static Button PageButton(string text) => new()
    {
        Content = text,
        FontSize = 12,
        MinWidth = 30,
        Background = DashboardTheme.Brush(DashboardTheme.Surface2),
        Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(7, 4, 7, 4),
        Margin = new Thickness(2, 0, 2, 0),
    };

    // ── Filter label ──────────────────────────────────────────────────────────

    public static TextBlock FilterLabel(string text) => new()
    {
        Text = text,
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        Margin = new Thickness(0, 0, 0, 5),
    };

    // ── Divider ───────────────────────────────────────────────────────────────

    public static Border HorizontalDivider(Thickness? margin = null) => new()
    {
        Height = 1,
        Background = DashboardTheme.Brush(DashboardTheme.Border),
        Margin = margin ?? new Thickness(0, 4, 0, 14),
    };

    // ── Dropdown ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Dropdown customizado dark-mode.
    /// Retorna: (host control, getValue, setValue, setItems).
    /// </summary>
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
            FontSize = 11,
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

        var innerGrid = TwoColumnGrid();
        innerGrid.Children.Add(selectedTb); Grid.SetColumn(selectedTb, 0);
        innerGrid.Children.Add(arrow); Grid.SetColumn(arrow, 1);

        var host = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 32,
            Cursor = Cursors.Hand,
            Child = innerGrid,
        };

        host.MouseEnter += (_, _) => host.Background = DashboardTheme.Brush(DashboardTheme.Surface3);
        host.MouseLeave += (_, _) => host.Background = DashboardTheme.Brush(DashboardTheme.Surface2);
        host.MouseLeftButtonUp += (_, _) =>
        {
            RebuildPopup();
            popup!.IsOpen = !popup.IsOpen;
        };

        void RebuildPopup()
        {
            popup?.Let(p => p.IsOpen = false);

            var list = new StackPanel
            {
                Background = DashboardTheme.Brush(DashboardTheme.Surface),
            };

            for (int i = 0; i < items.Count; i++)
            {
                var idx = i;
                var item = items[i];

                var rowBorder = BuildDropdownRow(item, idx == selectedIdx);

                rowBorder.MouseEnter += (_, _) =>
                {
                    if (idx != selectedIdx)
                        rowBorder.Background = DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x18);
                };
                rowBorder.MouseLeave += (_, _) =>
                {
                    rowBorder.Background = idx == selectedIdx
                        ? DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x30)
                        : Brushes.Transparent;
                };
                rowBorder.MouseLeftButtonUp += (_, _) =>
                {
                    selectedIdx = idx;
                    selectedTb.Text = item;
                    popup?.Let(p => p.IsOpen = false);
                    onSelected?.Invoke(item);
                };

                list.Children.Add(rowBorder);

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
                        BlurRadius = 16,
                        Opacity = 0.4,
                        ShadowDepth = 4,
                    },
                },
                PlacementTarget = host,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                MinWidth = host.ActualWidth > 0 ? host.ActualWidth : 160,
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

        string? getValue() =>
            selectedIdx < items.Count ? items[selectedIdx] : null;

        void setValue(string v)
        {
            var i = items.IndexOf(v);
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

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static Border BuildDropdownRow(string text, bool isSelected) => new()
    {
        Padding = new Thickness(12, 8, 12, 8),
        Background = isSelected
            ? DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x30)
            : Brushes.Transparent,
        Cursor = Cursors.Hand,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = isSelected
                ? DashboardTheme.Brush(DashboardTheme.Purple)
                : DashboardTheme.Brush(DashboardTheme.Text),
        },
    };

    private static Grid TwoColumnGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        return g;
    }

    private static Color ParseColor(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex);
}

/// <summary>Extensão nula-segura para evitar null-checks em cascata.</summary>
file static class PopupExtensions
{
    public static void Let<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj is not null) action(obj);
    }
}