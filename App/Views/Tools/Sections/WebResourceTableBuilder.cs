// =============================================================================
//  WebResourceTableBuilder.cs — Tabela estilizada dark de Web Resources
// =============================================================================

using D365Assistant.Core.Models.WebResource;
using D365Assistant.Views.Tools.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Tools.Sections;

public sealed class WebResourceTableBuilder
{
    // ── Column definitions ────────────────────────────────────────────────────
    private static readonly (string Header, double Width, bool Star)[] Columns =
    [
        ("Nome lógico",  2.5, true),
        ("Display Name", 1.5, true),
        ("Tipo",         80,  false),
        ("Solução",      120, false),
        ("Modificado",   130, false),
    ];

    // ── Row selection state ───────────────────────────────────────────────────
    private WebResource? _selected;
    private readonly Dictionary<string, WpfBorder> _rowMap = [];

    // ── Callbacks ─────────────────────────────────────────────────────────────
    private readonly Action<WebResource> _onSelect;

    // ── UI refs ───────────────────────────────────────────────────────────────
    private StackPanel? _body;

    public WebResourceTableBuilder(Action<WebResource> onSelect)
    {
        _onSelect = onSelect;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    public UIElement Build()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var header = BuildHeader();
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Body scroll
        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = ToolsTheme.Brush(ToolsTheme.Bg),
        };
        sv.PreviewMouseWheel += (_, e) =>
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        };
        _body = new StackPanel();
        sv.Content = _body;
        Grid.SetRow(sv, 1);
        root.Children.Add(sv);

        return root;
    }

    public void Populate(IEnumerable<WebResource> items, WebResource? keepSelected = null)
    {
        if (_body == null) return;
        _body.Children.Clear();
        _rowMap.Clear();

        foreach (var item in items)
        {
            var row = BuildRow(item);
            _rowMap[item.WebResourceId] = row;
            _body.Children.Add(row);
        }

        // Restore selection highlight
        if (keepSelected != null && _rowMap.TryGetValue(keepSelected.WebResourceId, out var sel))
        {
            sel.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1F, 0x32));
            sel.Tag = "selected";
        }
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private static UIElement BuildHeader()
    {
        var border = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        var g = ColGrid();
        border.Child = g;

        for (int i = 0; i < Columns.Length; i++)
        {
            var tb = new TextBlock
            {
                Text = Columns[i].Header,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(i == 0 ? 16 : 0, 9, 8, 9),
            };
            Grid.SetColumn(tb, i);
            g.Children.Add(tb);
        }

        return border;
    }

    // ── Row ───────────────────────────────────────────────────────────────────

    private WpfBorder BuildRow(WebResource item)
    {
        var row = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 2, 0, 2),
            Cursor = Cursors.Hand,
        };

        row.MouseEnter += (_, _) =>
        {
            if (row.Tag is not "selected")
                row.Background = new SolidColorBrush(Color.FromRgb(0x12, 0x18, 0x26));
        };
        row.MouseLeave += (_, _) =>
        {
            if (row.Tag is not "selected")
                row.Background = ToolsTheme.Brush(ToolsTheme.Surface);
        };
        row.MouseLeftButtonUp += (_, _) => SelectRow(item, row);

        var g = ColGrid();
        row.Child = g;

        // Col 0: Name (monospaced + accent)
        var nameTb = new TextBlock
        {
            Text = item.Name,
            FontSize = 11,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            Foreground = ToolsTheme.Brush(ToolsTheme.Blue),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 10, 8, 10),
        };
        Grid.SetColumn(nameTb, 0);
        g.Children.Add(nameTb);

        // Col 1: Display name
        var displayTb = new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(displayTb, 1);
        g.Children.Add(displayTb);

        // Col 2: Type badge
        var (typeFg, typeBg) = TypeColors(item.TypeCode);
        var typeBadge = new WpfBorder
        {
            Background = new SolidColorBrush(typeBg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, typeFg.R, typeFg.G, typeFg.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3, 7, 3),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = item.TypeLabel,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(typeFg),
            },
        };
        Grid.SetColumn(typeBadge, 2);
        g.Children.Add(typeBadge);

        // Col 3: Managed badge
        var (mgFg, mgBg, mgLabel) = item.IsManaged
            ? (Color.FromRgb(0x94, 0xA3, 0xB8), Color.FromRgb(0x1E, 0x2A, 0x38), "Gerenciado")
            : (Color.FromRgb(0x86, 0xEF, 0xAC), Color.FromRgb(0x0A, 0x20, 0x10), "Não gerenciado");

        var mgBadge = new WpfBorder
        {
            Background = new SolidColorBrush(mgBg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, mgFg.R, mgFg.G, mgFg.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3, 7, 3),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock
            {
                Text = mgLabel,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(mgFg),
            },
        };
        Grid.SetColumn(mgBadge, 3);
        g.Children.Add(mgBadge);

        // Col 4: Modified date
        var dateTb = new TextBlock
        {
            Text = item.ModifiedOnFormatted,
            FontSize = 10.5,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(dateTb, 4);
        g.Children.Add(dateTb);

        return row;
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    private void SelectRow(WebResource item, WpfBorder row)
    {
        foreach (var (_, r) in _rowMap)
        {
            r.Background = ToolsTheme.Brush(ToolsTheme.Surface);
            r.Tag = null;
        }

        _selected = item;
        row.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1F, 0x32));
        row.Tag = "selected";

        _onSelect(item);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Grid ColGrid()
    {
        var g = new Grid();
        foreach (var (_, w, star) in Columns)
            g.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = star
                    ? new GridLength(w, GridUnitType.Star)
                    : new GridLength(w),
            });
        return g;
    }

    private static (Color fg, Color bg) TypeColors(int code) => code switch
    {
        1 => (ToolsTheme.Blue, Color.FromRgb(0x0C, 0x1F, 0x3A)),
        2 => (ToolsTheme.Purple, Color.FromRgb(0x1E, 0x12, 0x45)),
        3 => (ToolsTheme.Yellow, Color.FromRgb(0x3B, 0x2A, 0x00)),
        11 => (Color.FromRgb(0x6E, 0xE7, 0xB7), Color.FromRgb(0x0A, 0x1F, 0x14)),
        _ => (ToolsTheme.TextSub, ToolsTheme.Surface2),
    };
}