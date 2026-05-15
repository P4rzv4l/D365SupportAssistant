// =============================================================================
//  TrackerHistoryView.xaml.cs — Histórico do Time Tracker
//  Filtros: Dia / Semana / Mês / Ano + Exportar XLSX
// =============================================================================

using D365Assistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

public partial class TrackerHistoryView : Page
{
    private readonly TrackerHistoryViewModel _vm;
    private readonly Dictionary<TrackerHistoryViewModel.PeriodKind, Button> _periodBtns = [];

    // ── Palette ───────────────────────────────────────────────────────────────
    private static readonly Color CBackground = Color.FromRgb(0x08, 0x0C, 0x12);
    private static readonly Color CSurface = Color.FromRgb(0x0F, 0x15, 0x20);
    private static readonly Color CBorder = Color.FromRgb(0x1E, 0x26, 0x33);
    private static readonly Color CText = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private static readonly Color CMuted = Color.FromRgb(0x4B, 0x55, 0x63);
    private static readonly Color CAccent = Color.FromRgb(0xA7, 0x8B, 0xFA);
    private static readonly Color CAccentDim = Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA);
    private static readonly Color CGreen = Color.FromRgb(0x22, 0xC5, 0x5E);
    private static readonly Color CBlue = Color.FromRgb(0x3B, 0x82, 0xF6);

    // Panels updated on refresh
    private StackPanel? _groupsPanel;
    private TextBlock? _periodLbl;
    private TextBlock? _totalLbl;
    private TextBlock? _exportStatusLbl;

    public TrackerHistoryView(TrackerHistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BuildUi();
        Refresh();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUi()
    {
        var root = new Grid { Background = new SolidColorBrush(CBackground) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(BuildHeader());
        Grid.SetRow(root.Children[0], 0);

        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = new SolidColorBrush(CBackground),
            Padding = new Thickness(20, 12, 20, 24),
        };
        sv.PreviewMouseWheel += (s, e) =>
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        };

        _groupsPanel = new StackPanel();
        sv.Content = _groupsPanel;
        root.Children.Add(sv);
        Grid.SetRow(sv, 1);

        ((Grid)Content).Children.Add(root);
    }

    private UIElement BuildHeader()
    {
        var header = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 16, 20, 16),
        };

        var outerGrid = new Grid();
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.Child = outerGrid;

        // ── Row 0: Title + Period nav ─────────────────────────────────────────
        var row0 = new Grid();
        row0.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row0.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        outerGrid.Children.Add(row0);
        Grid.SetRow(row0, 0);

        // Title
        var title = new TextBlock
        {
            Text = "Histórico de Horas",
            FontSize = 16,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            Foreground = new SolidColorBrush(CText),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 0);
        row0.Children.Add(title);

        // Period navigation (center)
        var nav = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(nav, 1);
        row0.Children.Add(nav);

        var btnPrev = NavBtn("◀");
        btnPrev.Click += (_, _) => { _vm.PreviousCommand.Execute(null); Refresh(); };
        nav.Children.Add(btnPrev);

        _periodLbl = new TextBlock
        {
            FontSize = 13,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            Foreground = new SolidColorBrush(CAccent),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
            MinWidth = 160,
            TextAlignment = TextAlignment.Center,
        };
        nav.Children.Add(_periodLbl);

        var btnNext = NavBtn("▶");
        btnNext.Click += (_, _) => { _vm.NextCommand.Execute(null); Refresh(); };
        nav.Children.Add(btnNext);

        var btnToday = new Button
        {
            Content = "Hoje",
            FontFamily = new FontFamily("Segoe UI Semibold"),
            FontSize = 11,
            Background = new SolidColorBrush(CAccentDim),
            Foreground = new SolidColorBrush(CAccent),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xA7, 0x8B, 0xFA)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand,
        };
        btnToday.Click += (_, _) => { _vm.TodayCommand.Execute(null); Refresh(); };
        nav.Children.Add(btnToday);

        // Export + Total (right)
        var rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(rightPanel, 2);
        row0.Children.Add(rightPanel);

        _exportStatusLbl = new TextBlock
        {
            FontSize = 11,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(CMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        rightPanel.Children.Add(_exportStatusLbl);

        var btnExport = new Button
        {
            Content = "⬇  Exportar XLSX",
            FontFamily = new FontFamily("Segoe UI Semibold"),
            FontSize = 11,
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x22, 0xC5, 0x5E)),
            Foreground = new SolidColorBrush(CGreen),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x22, 0xC5, 0x5E)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 5, 12, 5),
            Cursor = Cursors.Hand,
        };
        btnExport.Click += (_, _) =>
        {
            _vm.ExportXlsxCommand.Execute(null);
            if (_exportStatusLbl != null)
                _exportStatusLbl.Text = _vm.ExportStatus;
        };
        rightPanel.Children.Add(btnExport);

        // ── Row 1: Period pills + Total ───────────────────────────────────────
        var row1 = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerGrid.Children.Add(row1);
        Grid.SetRow(row1, 1);

        var pills = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(pills, 0);
        row1.Children.Add(pills);

        foreach (var (kind, label) in new[]
        {
            (TrackerHistoryViewModel.PeriodKind.Day,   "Dia"),
            (TrackerHistoryViewModel.PeriodKind.Week,  "Semana"),
            (TrackerHistoryViewModel.PeriodKind.Month, "Mês"),
            (TrackerHistoryViewModel.PeriodKind.Year,  "Ano"),
        })
        {
            var btn = PillBtn(label);
            var k = kind;
            btn.Click += (_, _) => { _vm.SelectedPeriod = k; HighlightPeriod(k); Refresh(); };
            _periodBtns[kind] = btn;
            pills.Children.Add(btn);
        }
        HighlightPeriod(_vm.SelectedPeriod);

        // Total right
        var totalPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(totalPanel, 1);
        row1.Children.Add(totalPanel);

        totalPanel.Children.Add(new TextBlock
        {
            Text = "TOTAL: ",
            FontSize = 11,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            Foreground = new SolidColorBrush(CMuted),
            VerticalAlignment = VerticalAlignment.Center,
        });

        _totalLbl = new TextBlock
        {
            FontSize = 14,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(CAccent),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        totalPanel.Children.Add(_totalLbl);

        return header;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void Refresh()
    {
        _vm.Refresh();

        if (_periodLbl != null) _periodLbl.Text = _vm.PeriodLabel;
        if (_totalLbl != null) _totalLbl.Text = _vm.TotalFormatted;
        if (_groupsPanel == null) return;

        _groupsPanel.Children.Clear();

        if (!_vm.HasData)
        {
            _groupsPanel.Children.Add(new TextBlock
            {
                Text = "Nenhum registro encontrado para este período.",
                FontSize = 13,
                Foreground = new SolidColorBrush(CMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
            return;
        }

        foreach (var group in _vm.Groups)
            _groupsPanel.Children.Add(BuildDayGroup(group));
    }

    // ── Day Group Card ────────────────────────────────────────────────────────

    private UIElement BuildDayGroup(TimeEntryGroup group)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(0),
        };

        var stack = new StackPanel();
        card.Child = stack;

        // Day header
        var dayHeader = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x11, 0x1C)),
            Margin = new Thickness(0),
        };
        dayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var border = new Border
        {
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Background = Brushes.Transparent,
            Padding = new Thickness(16, 10, 16, 10),
        };
        border.Child = dayHeader;
        stack.Children.Add(border);

        var dayLbl = new TextBlock
        {
            Text = group.Label,
            FontSize = 12,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            Foreground = new SolidColorBrush(CText),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(dayLbl, 0);
        dayHeader.Children.Add(dayLbl);

        var dayTotal = new TextBlock
        {
            Text = group.TotalFormatted,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(CAccent),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(dayTotal, 1);
        dayHeader.Children.Add(dayTotal);

        // Divider
        stack.Children.Add(new Border
        {
            Background = new SolidColorBrush(CBorder),
            Height = 1,
        });

        // Ticket rows
        foreach (var ticket in group.Tickets)
        {
            stack.Children.Add(BuildTicketRow(ticket));

            if (ticket != group.Tickets.Last())
                stack.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x12, 0x19, 0x2A)),
                    Height = 1,
                    Margin = new Thickness(16, 0, 16, 0),
                });
        }

        return card;
    }

    private UIElement BuildTicketRow(TicketSummary ticket)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var wrapper = new Border { Padding = new Thickness(16, 10, 16, 10) };
        wrapper.Child = grid;

        // Ticket badge
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x3B, 0x82, 0xF6)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x3B, 0x82, 0xF6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = ticket.TicketId,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(CBlue),
            }
        };
        Grid.SetColumn(badge, 0);
        grid.Children.Add(badge);

        // Title
        var titleTxt = ticket.Title.Length > 0 ? ticket.Title : "(sem título)";
        var titleBlk = new TextBlock
        {
            Text = titleTxt.Length > 80 ? titleTxt[..80] + "…" : titleTxt,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(titleBlk, 1);
        grid.Children.Add(titleBlk);

        // Duration + entry count
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        if (ticket.Entries.Count > 1)
            right.Children.Add(new TextBlock
            {
                Text = $"{ticket.Entries.Count} sessões · ",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(CMuted),
                VerticalAlignment = VerticalAlignment.Center,
            });

        right.Children.Add(new TextBlock
        {
            Text = ticket.Formatted,
            FontSize = 12.5,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(CAccent),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return wrapper;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Button NavBtn(string label) => new()
    {
        Content = label,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 12,
        Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20)),
        Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x33)),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(8, 4, 8, 4),
        Cursor = Cursors.Hand,
    };

    private static Button PillBtn(string label) => new()
    {
        Content = label,
        FontFamily = new FontFamily("Segoe UI Semibold"),
        FontSize = 11,
        Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20)),
        Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x33)),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(14, 5, 14, 5),
        Margin = new Thickness(0, 0, 6, 0),
        Cursor = Cursors.Hand,
    };

    private void HighlightPeriod(TrackerHistoryViewModel.PeriodKind active)
    {
        foreach (var (kind, btn) in _periodBtns)
        {
            if (kind == active)
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA));
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xA7, 0x8B, 0xFA));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x33));
            }
        }
    }
}