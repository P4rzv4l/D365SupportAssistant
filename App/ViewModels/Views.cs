// =============================================================================
//  Views restantes: IncidentsView, AlertsView, TrackerView, AIView, SettingsView
// =============================================================================

using D365Assistant.Core.Models;
using D365Assistant.Core.Models.Alerts;
using D365Assistant.Core.Models.Incident;
using D365Assistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace D365Assistant.Views;

// ── AlertsView ────────────────────────────────────────────────────────────────

public partial class AlertsView : Page
{
    private readonly AlertsViewModel _vm;
    private readonly StackPanel _list;

    public AlertsView(AlertsViewModel vm)
    {
        _vm = vm;
        Title = "Alertas";
        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17));

        var dock = new DockPanel { Margin = new Thickness(20) };

        // Header
        var hdr = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdr.Children.Add(new TextBlock
        {
            Text = "Alertas",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        });
        var clearBtn = new Button
        {
            Content = "Limpar",
            FontSize = 12,
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x21, 0x28)),
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 6, 12, 6),
        };
        clearBtn.Click += (_, _) => { _vm.ClearCommand.Execute(null); _list?.Children.Clear(); };
        Grid.SetColumn(clearBtn, 1);
        hdr.Children.Add(clearBtn);
        DockPanel.SetDock(hdr, Dock.Top);
        dock.Children.Add(hdr);

        // Lista scrollável
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _list = new StackPanel();
        scroll.Content = _list;
        dock.Children.Add(scroll);

        // Observa novos alertas
        _vm.Alerts.CollectionChanged += (_, _) => RenderAlerts();
        RenderAlerts();
        Content = dock;
    }

    private void RenderAlerts()
    {
        _list.Children.Clear();
        foreach (var alert in _vm.Alerts)
            _list.Children.Add(BuildAlertCard(alert));
    }

    private static Border BuildAlertCard(Alert alert)
    {
        var borderColor = (Color)ColorConverter.ConvertFromString(alert.SeverityColor);
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(2, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var inner = new StackPanel();

        // Linha 1: tipo + ticket + tempo
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var typeTicket = new StackPanel { Orientation = Orientation.Horizontal };
        typeTicket.Children.Add(new TextBlock
        {
            Text = $"{alert.TypeLabel}  ",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(borderColor),
        });
        typeTicket.Children.Add(new TextBlock
        {
            Text = alert.TicketNumber,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)),
        });
        Grid.SetColumn(typeTicket, 0);
        top.Children.Add(typeTicket);

        top.Children.Add(new TextBlock
        {
            Text = alert.GeneratedAt.ToLocalTime().ToString("HH:mm:ss"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x4F, 0x58)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(top.Children[^1], 1);

        inner.Children.Add(top);
        inner.Children.Add(new TextBlock
        {
            Text = alert.Message,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            Margin = new Thickness(0, 4, 0, 2),
        });
        if (!string.IsNullOrEmpty(alert.CustomerName))
            inner.Children.Add(new TextBlock
            {
                Text = $"👤 {alert.CustomerName}  •  Score: {alert.PriorityScore}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            });

        card.Child = inner;
        return card;
    }
}

// ── TrackerView ───────────────────────────────────────────────────────────────

// ── TrackerView ───────────────────────────────────────────────────────────────

public partial class TrackerView : Page
{
    private readonly TrackerViewModel _vm;
    private readonly TrackerHistoryViewModel _histVm;

    private string _activeTab = "Hoje";
    private StackPanel? _histPanel;
    private TextBlock? _histTotal;
    private TextBlock? _histLabel;
    private readonly Dictionary<string, Button> _tabBtns = [];

    // KPI TextBlocks para atualização dinâmica
    private TextBlock? _kpiFaturavel;
    private TextBlock? _kpiSessoes;
    private TextBlock? _kpiMeta;
    private StackPanel? _timelinePanel;

    // ── Palette ───────────────────────────────────────────────────────────────
    private static readonly Color CBg = Color.FromRgb(0x0D, 0x11, 0x17);
    private static readonly Color CSurface = Color.FromRgb(0x13, 0x19, 0x20);
    private static readonly Color CSurface2 = Color.FromRgb(0x1A, 0x20, 0x29);
    private static readonly Color CBorder = Color.FromRgb(0x27, 0x2D, 0x38);
    private static readonly Color CText = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private static readonly Color CMuted = Color.FromRgb(0x4B, 0x56, 0x63);
    private static readonly Color CAccent = Color.FromRgb(0xA7, 0x8B, 0xFA);
    private static readonly Color CGreen = Color.FromRgb(0x22, 0xC5, 0x5E);
    private static readonly Color CRed = Color.FromRgb(0xF8, 0x51, 0x49);
    private static readonly Color CBlue = Color.FromRgb(0x58, 0xA6, 0xFF);
    private static readonly Color CYellow = Color.FromRgb(0xF5, 0xA6, 0x23);

    public TrackerView(TrackerViewModel vm, TrackerHistoryViewModel histVm)
    {
        _vm = vm;
        _histVm = histVm;
        DataContext = _vm;
        Title = "Time Tracker";
        Background = new SolidColorBrush(CBg);

        // ── Root scroll ───────────────────────────────────────────────────────
        var rootSv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        rootSv.PreviewMouseWheel += (_, e) =>
        {
            rootSv.ScrollToVerticalOffset(rootSv.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        };

        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        rootSv.Content = root;

        // ── Page header ───────────────────────────────────────────────────────
        var pageHdr = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        pageHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pageHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.Children.Add(pageHdr);

        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = "Time Tracker",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "Controle de tempo e produtividade",
            FontSize = 12,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(0, 2, 0, 0),
        });
        pageHdr.Children.Add(titleStack);

        // Export button in header
        var btnExportHdr = new Button
        {
            Content = "⬇  Exportar",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0xA7, 0x8B, 0xFA)),
            Foreground = new SolidColorBrush(CAccent),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xA7, 0x8B, 0xFA)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(16, 8, 16, 8),
            VerticalAlignment = VerticalAlignment.Center,
        };
        btnExportHdr.MouseEnter += (_, _) => btnExportHdr.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xA7, 0x8B, 0xFA));
        btnExportHdr.MouseLeave += (_, _) => btnExportHdr.Background = new SolidColorBrush(Color.FromArgb(0x18, 0xA7, 0x8B, 0xFA));
        btnExportHdr.Click += (_, _) =>
        {
            _histVm.SelectedPeriod = TrackerHistoryViewModel.PeriodKind.Day;
            _histVm.ReferenceDate = DateTime.Today;
            _histVm.ExportXlsxCommand.Execute(null);
        };
        Grid.SetColumn(btnExportHdr, 1);
        pageHdr.Children.Add(btnExportHdr);

        // ── KPI row ───────────────────────────────────────────────────────────
        root.Children.Add(BuildKpiRow());
        root.Children.Add(new Border { Height = 14 });

        // ── Main area: timer+controls left | timeline right ───────────────────
        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        root.Children.Add(mainGrid);

        var leftCol = new StackPanel();
        Grid.SetColumn(leftCol, 0);
        mainGrid.Children.Add(leftCol);

        leftCol.Children.Add(BuildTimerCard());
        leftCol.Children.Add(new Border { Height = 12 });
        leftCol.Children.Add(BuildControlsCard());

        var rightCol = BuildTimelineCard();
        Grid.SetColumn(rightCol, 2);
        mainGrid.Children.Add(rightCol);

        root.Children.Add(new Border { Height = 14 });

        // ── History card ──────────────────────────────────────────────────────
        root.Children.Add(BuildHistoryCard());

        Content = rootSv;

        _vm.TodayEntries.CollectionChanged += (_, _) => Dispatcher.Invoke(() => { RefreshHistory(); RefreshKpis(); RefreshTimeline(); });
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_vm.IsRunning) or nameof(_vm.StatusPill))
                Dispatcher.Invoke(SyncTimerCard);
            if (e.PropertyName == nameof(_vm.ActiveDescription))
                Dispatcher.Invoke(RefreshTimeline);
        };
        RefreshHistory();
        RefreshKpis();
        RefreshTimeline();
    }

    // ── KPI Row ───────────────────────────────────────────────────────────────

    private UIElement BuildKpiRow()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Tempo Hoje
        var kpiToday = KpiCard("Tempo Hoje", "0h 00m", CAccent, "⏱");
        var kpiTodayVal = (TextBlock)((StackPanel)((Border)kpiToday).Child).Children[2];
        kpiTodayVal.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DayTotal") { Source = _vm });
        Grid.SetColumn(kpiToday, 0);
        grid.Children.Add(kpiToday);

        // Tempo Faturável (calculado de TodayEntries – excluindo is_active)
        var kpiFat = KpiCard("Tempo Registrado", "—", CGreen, "✓");
        _kpiFaturavel = (TextBlock)((StackPanel)((Border)kpiFat).Child).Children[2];
        Grid.SetColumn(kpiFat, 2);
        grid.Children.Add(kpiFat);

        // Sessões hoje
        var kpiSess = KpiCard("Sessões Hoje", "0", CBlue, "#");
        _kpiSessoes = (TextBlock)((StackPanel)((Border)kpiSess).Child).Children[2];
        Grid.SetColumn(kpiSess, 4);
        grid.Children.Add(kpiSess);

        // Meta diária (8h)
        var kpiMeta = KpiCard("Meta (8h)", "0%", CYellow, "◎");
        _kpiMeta = (TextBlock)((StackPanel)((Border)kpiMeta).Child).Children[2];
        Grid.SetColumn(kpiMeta, 6);
        grid.Children.Add(kpiMeta);

        return grid;
    }

    private void RefreshKpis()
    {
        var entries = _vm.TodayEntries.ToList();
        var finishedSecs = entries.Where(e => !e.IsActive).Sum(e => e.Seconds);
        var totalSecs = entries.Sum(e => e.Seconds);
        var sessoes = entries.Count;

        if (_kpiFaturavel != null)
        {
            var ts = TimeSpan.FromSeconds(finishedSecs);
            _kpiFaturavel.Text = finishedSecs == 0 ? "—"
                : ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
                : $"{ts.Minutes}m {ts.Seconds:D2}s";
        }
        if (_kpiSessoes != null)
            _kpiSessoes.Text = sessoes.ToString();
        if (_kpiMeta != null)
        {
            var pct = Math.Min(100, (int)Math.Round(totalSecs / (8.0 * 3600) * 100));
            _kpiMeta.Text = $"{pct}%";
            _kpiMeta.Foreground = new SolidColorBrush(pct >= 100 ? CGreen : pct >= 50 ? CYellow : CRed);
        }
    }

    private Border KpiCard(string label, string value, Color accent, string icon)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 14, 18, 14),
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, accent.R, accent.G, accent.B)),
            Margin = new Thickness(0, 0, 0, 6),
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(0, 0, 0, 4),
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 22,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(accent),
        });
        card.Child = stack;
        return card;
    }

    // ── Timer Card ────────────────────────────────────────────────────────────

    private Border? _timerCard;
    private TextBlock? _pillText;
    private Border? _pill;
    private TextBlock? _activeDescTb;
    private TextBlock? _sessionStartTb;

    private UIElement BuildTimerCard()
    {
        _timerCard = Card();

        var grid = new Grid { Margin = new Thickness(24, 18, 24, 18) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _timerCard.Child = grid;

        // Left: status pill + timer + active ticket + description
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        // Status pill row
        var pillRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        _pill = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 3, 12, 3),
        };
        _pillText = new TextBlock { FontSize = 10, FontWeight = FontWeights.Bold };
        _pill.Child = _pillText;
        pillRow.Children.Add(_pill);

        _sessionStartTb = new TextBlock
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(CMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };
        pillRow.Children.Add(_sessionStartTb);
        left.Children.Add(pillRow);

        // Timer display
        var timerTb = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 52,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 6),
        };
        timerTb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("TimerDisplay") { Source = _vm });
        timerTb.SetBinding(TextBlock.ForegroundProperty,
            new System.Windows.Data.Binding("TimerColor") { Source = _vm, Converter = new ColorStringToBrushConverter() });
        left.Children.Add(timerTb);

        // Active ticket badge + title
        var activeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var activeBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x20, 0x58, 0xA6, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x58, 0xA6, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0),
        };
        var activeBadgeTb = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = new SolidColorBrush(CBlue),
        };
        activeBadgeTb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("ActiveTicket") { Source = _vm });
        activeBadge.Child = activeBadgeTb;
        activeRow.Children.Add(activeBadge);
        var activeTitleTb = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(CMuted),
            VerticalAlignment = VerticalAlignment.Center,
        };
        activeTitleTb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("ActiveTitle") { Source = _vm });
        activeRow.Children.Add(activeTitleTb);
        left.Children.Add(activeRow);

        // Active description
        _activeDescTb = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x76, 0x88)),
            FontStyle = FontStyles.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        };
        _activeDescTb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("ActiveDescription") { Source = _vm });
        left.Children.Add(_activeDescTb);

        // Right: today total box
        var right = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0xA7, 0x8B, 0xFA)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xA7, 0x8B, 0xFA)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24, 16, 24, 16),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 0, 0, 0),
        };
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        var rightStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        rightStack.Children.Add(new TextBlock
        {
            Text = "HOJE",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xA7, 0x8B, 0xFA)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
        });
        var dayTotalTb = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(CAccent),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        dayTotalTb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("DayTotal") { Source = _vm });
        rightStack.Children.Add(dayTotalTb);

        // Meta progress bar
        var metaBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x25, 0x30)),
            CornerRadius = new CornerRadius(3),
            Height = 4,
            Margin = new Thickness(0, 8, 0, 4),
            Width = 80,
        };
        var metaFill = new Border
        {
            Background = new SolidColorBrush(CAccent),
            CornerRadius = new CornerRadius(3),
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0,
        };
        var metaGrid = new Grid();
        metaGrid.Children.Add(metaBar);
        metaGrid.Children.Add(metaFill);
        rightStack.Children.Add(metaGrid);

        var metaLabelTb = new TextBlock
        {
            FontSize = 9,
            Foreground = new SolidColorBrush(CMuted),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        rightStack.Children.Add(metaLabelTb);

        right.Child = rightStack;

        // update meta bar when DayTotal changes
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.DayTotal))
            {
                var entries = _vm.TodayEntries.ToList();
                var totalSec = entries.Sum(x => x.Seconds);
                var pct = Math.Min(1.0, totalSec / (8.0 * 3600));
                metaFill.Width = 80 * pct;
                metaLabelTb.Text = $"{(int)(pct * 100)}% da meta";
                metaLabelTb.Foreground = new SolidColorBrush(pct >= 1.0 ? CGreen : CMuted);
            }
            if (e.PropertyName == nameof(_vm.ActiveTicket))
                Dispatcher.Invoke(UpdateSessionStart);
        };

        SyncTimerCard();
        UpdateSessionStart();
        return _timerCard;
    }

    private void UpdateSessionStart()
    {
        if (_sessionStartTb == null) return;
        if (string.IsNullOrEmpty(_vm.ActiveTicket))
        {
            _sessionStartTb.Text = "";
            return;
        }
        var active = _vm.TodayEntries.FirstOrDefault(e => e.IsActive);
        _sessionStartTb.Text = active != null ? $"desde {active.Start:HH:mm}" : "";
    }

    private void SyncTimerCard()
    {
        if (_pill == null || _pillText == null) return;
        var color = _vm.IsRunning ? CGreen
                  : _vm.StatusPill == "PAUSADO" ? CYellow
                  : Color.FromRgb(0x4B, 0x56, 0x63);
        _pill.Background = new SolidColorBrush(Color.FromArgb(0x25, color.R, color.G, color.B));
        _pill.BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, color.R, color.G, color.B));
        _pill.BorderThickness = new Thickness(1);
        _pillText.Foreground = new SolidColorBrush(color);
        _pillText.Text = _vm.StatusPill;
    }

    // ── Controls Card ─────────────────────────────────────────────────────────

    private UIElement BuildControlsCard()
    {
        var card = Card();
        var stack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        card.Child = stack;

        // Row 1: Chamado + Descrição inline
        var fieldsGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stack.Children.Add(fieldsGrid);

        // Chamado field
        var chamadoStack = new StackPanel();
        chamadoStack.Children.Add(new TextBlock
        {
            Text = "CHAMADO",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(0, 0, 0, 4),
        });
        var ticketInput = new TextBox
        {
            Background = new SolidColorBrush(CBg),
            Foreground = new SolidColorBrush(CText),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            FontSize = 13,
            FontFamily = new FontFamily("Consolas"),
            Padding = new Thickness(10, 8, 10, 8),
            CaretBrush = new SolidColorBrush(CAccent),
        };
        ticketInput.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("TicketInput")
        {
            Source = _vm,
            Mode = System.Windows.Data.BindingMode.TwoWay,
            UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
        });
        ticketInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) _vm.StartCommand.Execute(null); };
        chamadoStack.Children.Add(ticketInput);
        Grid.SetColumn(chamadoStack, 0);
        fieldsGrid.Children.Add(chamadoStack);

        // Descrição field
        var descStack = new StackPanel();
        descStack.Children.Add(new TextBlock
        {
            Text = "DESCRIÇÃO",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(0, 0, 0, 4),
        });
        var descInput = new TextBox
        {
            Background = new SolidColorBrush(CBg),
            Foreground = new SolidColorBrush(CText),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Padding = new Thickness(10, 8, 10, 8),
            CaretBrush = new SolidColorBrush(CAccent),
            MaxLength = 500,
        };
        descInput.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("DescriptionInput")
        {
            Source = _vm,
            Mode = System.Windows.Data.BindingMode.TwoWay,
            UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
        });
        Grid.SetColumn(descStack, 2);
        descStack.Children.Add(descInput);
        fieldsGrid.Children.Add(descStack);

        // Row 2: Buttons
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

        Button ActionBtn(string label, Color fg, Color bg, System.Windows.Input.ICommand cmd)
        {
            var b = new Button
            {
                Content = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromArgb(0x22, bg.R, bg.G, bg.B)),
                Foreground = new SolidColorBrush(fg),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, bg.R, bg.G, bg.B)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Padding = new Thickness(18, 9, 18, 9),
                Margin = new Thickness(0, 0, 8, 0),
                Command = cmd,
            };
            b.MouseEnter += (_, _) => b.Background = new SolidColorBrush(Color.FromArgb(0x40, bg.R, bg.G, bg.B));
            b.MouseLeave += (_, _) => b.Background = new SolidColorBrush(Color.FromArgb(0x22, bg.R, bg.G, bg.B));
            return b;
        }

        btnRow.Children.Add(ActionBtn("▶  Iniciar", CGreen, CGreen, _vm.StartCommand));
        btnRow.Children.Add(ActionBtn("⏸  Pausar", CYellow, CYellow, _vm.PauseCommand));
        btnRow.Children.Add(ActionBtn("⇄  Trocar", CAccent, CAccent, _vm.SwitchCommand));
        btnRow.Children.Add(ActionBtn("⏹  Finalizar", CRed, CRed, _vm.StopCommand));
        stack.Children.Add(btnRow);

        // Row 3: Recent tickets chips
        var recentRow = new WrapPanel();
        recentRow.Children.Add(new TextBlock
        {
            Text = "Recentes:",
            FontSize = 10,
            Foreground = new SolidColorBrush(CMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });

        void RebuildRecents()
        {
            while (recentRow.Children.Count > 1) recentRow.Children.RemoveAt(1);
            foreach (var t in _vm.RecentTickets)
            {
                var chip = new Border
                {
                    Background = new SolidColorBrush(CSurface2),
                    BorderBrush = new SolidColorBrush(CBorder),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 5, 3),
                    Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = t, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(CBlue) },
                };
                var ticket = t;
                chip.MouseLeftButtonUp += (_, _) => _vm.TicketInput = ticket;
                chip.MouseEnter += (_, _) => chip.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x29, 0x36));
                chip.MouseLeave += (_, _) => chip.Background = new SolidColorBrush(CSurface2);
                recentRow.Children.Add(chip);
            }
        }
        _vm.RecentTickets.CollectionChanged += (_, _) => Dispatcher.Invoke(RebuildRecents);
        RebuildRecents();
        stack.Children.Add(recentRow);

        return card;
    }

    // ── Timeline Card ─────────────────────────────────────────────────────────

    private UIElement BuildTimelineCard()
    {
        var card = Card();
        var dock = new DockPanel();
        card.Child = dock;

        // Header
        var hdr = new Border
        {
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0x1A, 0x20, 0x29)),
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(16, 12, 16, 12),
        };
        hdr.Child = new TextBlock
        {
            Text = "Linha do Tempo — Hoje",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
        };
        DockPanel.SetDock(hdr, Dock.Top);
        dock.Children.Add(hdr);

        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 340,
        };
        sv.PreviewMouseWheel += (_, e) =>
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        };

        _timelinePanel = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        sv.Content = _timelinePanel;
        dock.Children.Add(sv);

        return card;
    }

    private void RefreshTimeline()
    {
        if (_timelinePanel == null) return;
        _timelinePanel.Children.Clear();

        var entries = _vm.TodayEntries.ToList().OrderBy(e => e.Start).ToList();
        if (entries.Count == 0)
        {
            _timelinePanel.Children.Add(new TextBlock
            {
                Text = "Nenhuma atividade hoje.",
                FontSize = 11,
                Foreground = new SolidColorBrush(CMuted),
            });
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var isLast = i == entries.Count - 1;

            var itemGrid = new Grid { Margin = new Thickness(0, 0, 0, isLast ? 0 : 12) };
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Dot column with vertical line
            var dotCol = new Grid();
            var lineRect = new System.Windows.Shapes.Rectangle
            {
                Width = 1,
                Fill = new SolidColorBrush(CBorder),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            if (isLast) lineRect.Visibility = Visibility.Hidden;
            dotCol.Children.Add(lineRect);

            Color dotColor = e.IsActive ? CGreen : CAccent;
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(dotColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0),
            };
            dotCol.Children.Add(dot);
            Grid.SetColumn(dotCol, 0);
            itemGrid.Children.Add(dotCol);

            // Content
            var content = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
            var timeTicket = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            timeTicket.Children.Add(new TextBlock
            {
                Text = e.Start.ToString("HH:mm"),
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(CMuted),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            timeTicket.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0x58, 0xA6, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x58, 0xA6, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Child = new TextBlock
                {
                    Text = e.TicketId,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(CBlue),
                },
            });
            if (e.IsActive)
                timeTicket.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x20, 0x22, 0xC5, 0x5E)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x22, 0xC5, 0x5E)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(4, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = "em andamento",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(CGreen),
                    },
                });
            content.Children.Add(timeTicket);

            if (!string.IsNullOrWhiteSpace(e.Description))
                content.Children.Add(new TextBlock
                {
                    Text = e.Description,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0),
                });
            else if (!string.IsNullOrWhiteSpace(e.Title))
                content.Children.Add(new TextBlock
                {
                    Text = e.Title,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(CMuted),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0),
                });

            var durTs = TimeSpan.FromSeconds(e.Seconds);
            var durFmt = durTs.TotalHours >= 1
                ? $"{(int)durTs.TotalHours}h {durTs.Minutes:D2}m"
                : $"{durTs.Minutes}m {durTs.Seconds:D2}s";
            content.Children.Add(new TextBlock
            {
                Text = durFmt,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(e.IsActive ? CGreen : CMuted),
                Margin = new Thickness(0, 2, 0, 0),
            });

            Grid.SetColumn(content, 1);
            itemGrid.Children.Add(content);

            _timelinePanel.Children.Add(itemGrid);
        }
    }

    // ── History Card ──────────────────────────────────────────────────────────

    private UIElement BuildHistoryCard()
    {
        var card = Card();
        var dock = new DockPanel();
        card.Child = dock;

        // Header
        var header = new Border
        {
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0x1A, 0x20, 0x29)),
            CornerRadius = new CornerRadius(10, 10, 0, 0),
        };
        var headerGrid = new Grid { Margin = new Thickness(16, 0, 16, 0) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Child = headerGrid;

        // Tabs
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(tabs, 0);
        headerGrid.Children.Add(tabs);

        foreach (var tab in new[] { "Hoje", "Esta Semana", "Este Mês", "Total" })
        {
            var btn = new Button
            {
                Content = tab,
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(CMuted),
                BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Padding = new Thickness(14, 10, 14, 10),
            };
            var t = tab;
            btn.Click += (_, _) => { _activeTab = t; HighlightTab(t); RefreshHistory(); };
            _tabBtns[tab] = btn;
            tabs.Children.Add(btn);
        }
        HighlightTab("Hoje");

        // Total (center)
        var centerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(centerStack, 1);
        headerGrid.Children.Add(centerStack);

        _histLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(CMuted),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _histTotal = new TextBlock
        {
            FontSize = 13,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CAccent),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        centerStack.Children.Add(_histLabel);
        centerStack.Children.Add(_histTotal);

        // Export buttons
        var exportRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(exportRow, 2);
        headerGrid.Children.Add(exportRow);

        var btnXlsx = ExportBtn("⬇ Excel", CGreen);
        btnXlsx.Click += (_, _) =>
        {
            _histVm.SelectedPeriod = _activeTab switch
            {
                "Esta Semana" => TrackerHistoryViewModel.PeriodKind.Week,
                "Este Mês" => TrackerHistoryViewModel.PeriodKind.Month,
                "Total" => TrackerHistoryViewModel.PeriodKind.Year,
                _ => TrackerHistoryViewModel.PeriodKind.Day,
            };
            _histVm.ReferenceDate = DateTime.Today;
            _histVm.ExportXlsxCommand.Execute(null);
        };
        var btnCsv = ExportBtn("⬇ CSV", CBlue);
        btnCsv.Margin = new Thickness(6, 0, 0, 0);
        btnCsv.Click += (_, _) => ExportCsvHoje();

        exportRow.Children.Add(btnXlsx);
        exportRow.Children.Add(btnCsv);

        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);

        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 480,
        };
        sv.PreviewMouseWheel += (_, e) =>
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        };

        _histPanel = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        sv.Content = _histPanel;
        dock.Children.Add(sv);

        return card;
    }

    // ── Refresh History ───────────────────────────────────────────────────────

    private void RefreshHistory()
    {
        if (_histPanel == null) return;
        _histPanel.Children.Clear();

        List<Core.Models.Time.TimeEntry> entries;
        string label;

        switch (_activeTab)
        {
            case "Esta Semana":
                var dow = (int)DateTime.Today.DayOfWeek;
                var wkMon = DateTime.Today.AddDays(-((dow + 6) % 7));
                entries = _histVm._storage_GetPeriod(wkMon, wkMon.AddDays(6));
                label = $"{wkMon:dd/MM} – {wkMon.AddDays(6):dd/MM}";
                break;
            case "Este Mês":
                var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                entries = _histVm._storage_GetPeriod(first, first.AddMonths(1).AddDays(-1));
                label = DateTime.Today.ToString("MMMM yyyy", new System.Globalization.CultureInfo("pt-BR"));
                break;
            case "Total":
                entries = _histVm._storage_GetAll();
                label = "todos os registros";
                break;
            default:
                entries = _vm.TodayEntries.ToList();
                label = DateTime.Today.ToString("dd/MM/yyyy");
                break;
        }

        if (_histLabel != null) _histLabel.Text = label + "  ·  ";
        var totalSecs = entries.Sum(e => e.Seconds);
        var tsTotal = TimeSpan.FromSeconds(totalSecs);
        if (_histTotal != null)
            _histTotal.Text = totalSecs == 0 ? "—"
                : tsTotal.TotalHours >= 1 ? $"{(int)tsTotal.TotalHours}h {tsTotal.Minutes:D2}m"
                : $"{tsTotal.Minutes}m {tsTotal.Seconds:D2}s";

        if (entries.Count == 0)
        {
            _histPanel.Children.Add(new TextBlock
            {
                Text = "Nenhum registro para este período.",
                FontSize = 12,
                Foreground = new SolidColorBrush(CMuted),
                Margin = new Thickness(0, 8, 0, 0),
            });
            return;
        }

        var byDay = entries.GroupBy(e => e.Start.Date).OrderByDescending(g => g.Key);

        foreach (var dayGroup in byDay)
        {
            var dayDate = dayGroup.Key;
            var daySecs = dayGroup.Sum(e => e.Seconds);
            var dayTs = TimeSpan.FromSeconds(daySecs);
            var dayTotalFmt = dayTs.TotalHours >= 1
                ? $"{(int)dayTs.TotalHours}h {dayTs.Minutes:D2}m"
                : $"{dayTs.Minutes}m {dayTs.Seconds:D2}s";
            var dayLabel = dayDate == DateTime.Today ? "Hoje"
                         : dayDate == DateTime.Today.AddDays(-1) ? "Ontem"
                         : dayDate.ToString("ddd, dd/MM/yyyy", new System.Globalization.CultureInfo("pt-BR"));

            var dayCard = new Border
            {
                Background = new SolidColorBrush(CSurface),
                BorderBrush = new SolidColorBrush(CBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 10),
            };
            var dayStack = new StackPanel();
            dayCard.Child = dayStack;

            // Day header
            var dayHdr = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x14, 0x1C)),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(14, 10, 14, 10),
            };
            var dayHdrGrid = new Grid();
            dayHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dayHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dayHdr.Child = dayHdrGrid;

            var ticketCount = dayGroup.GroupBy(e => e.TicketId).Count();
            var dayLabelFull = $"{dayLabel}  ·  {ticketCount} chamado{(ticketCount != 1 ? "s" : "")}";
            dayHdrGrid.Children.Add(new TextBlock
            {
                Text = dayLabelFull,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CText),
                VerticalAlignment = VerticalAlignment.Center,
            });
            var dayTotalTb = new TextBlock
            {
                Text = dayTotalFmt,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CAccent),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(dayTotalTb, 1);
            dayHdrGrid.Children.Add(dayTotalTb);
            dayStack.Children.Add(dayHdr);

            dayStack.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(CBorder),
            });

            // Ticket rows
            var ticketGroups = dayGroup
                .GroupBy(e => e.TicketId)
                .Select(g => (
                    Ticket: g.Key,
                    Title: g.FirstOrDefault()?.Title ?? "",
                    Secs: g.Sum(e => e.Seconds),
                    Count: g.Count(),
                    First: g.Min(e => e.Start),
                    Last: g.Max(e => e.End ?? e.Start),
                    Descs: g.Where(e => !string.IsNullOrWhiteSpace(e.Description))
                             .Select(e => e.Description.Trim()).Distinct().ToList()
                ))
                .OrderByDescending(g => g.Secs)
                .ToList();

            var maxTicketSecs = ticketGroups.Max(g => g.Secs);

            for (int i = 0; i < ticketGroups.Count; i++)
            {
                var (ticket, title, secs, count, first, last, descs) = ticketGroups[i];

                var ticketRow = new Border { Padding = new Thickness(14, 10, 14, 10) };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                ticketRow.Child = grid;

                var leftCol = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
                Grid.SetColumn(leftCol, 0);
                grid.Children.Add(leftCol);

                // Ticket badge + title
                var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                topRow.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x22, 0x58, 0xA6, 0xFF)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x58, 0xA6, 0xFF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock
                    {
                        Text = ticket,
                        FontSize = 11,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = new SolidColorBrush(CBlue),
                    },
                });
                if (!string.IsNullOrWhiteSpace(title))
                    topRow.Children.Add(new TextBlock
                    {
                        Text = title.Length > 50 ? title[..50] + "…" : title,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                leftCol.Children.Add(topRow);

                // Meta row: time range + sessions
                var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
                metaRow.Children.Add(new TextBlock
                {
                    Text = $"{first:HH:mm} – {(last == first ? "em andamento" : last.ToString("HH:mm"))}",
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(CMuted),
                });
                if (count > 1)
                    metaRow.Children.Add(new TextBlock
                    {
                        Text = $"  ·  {count} sessões",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(CMuted),
                    });
                leftCol.Children.Add(metaRow);

                // Descriptions
                if (descs.Count > 0)
                {
                    var descText = string.Join(" · ", descs);
                    leftCol.Children.Add(new TextBlock
                    {
                        Text = descText.Length > 140 ? descText[..140] + "…" : descText,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x76, 0x88)),
                        Margin = new Thickness(0, 2, 0, 4),
                        TextWrapping = TextWrapping.Wrap,
                        FontStyle = FontStyles.Italic,
                    });
                }

                // Progress bar
                var barTrack = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x25, 0x30)),
                    CornerRadius = new CornerRadius(3),
                    Height = 3,
                    Margin = new Thickness(0, 4, 0, 0),
                };
                var barFill = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x99, 0xA7, 0x8B, 0xFA)),
                    CornerRadius = new CornerRadius(3),
                    Height = 3,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                var ratio = maxTicketSecs > 0 ? (double)secs / maxTicketSecs : 0;
                barTrack.SizeChanged += (_, ev) => barFill.Width = ev.NewSize.Width * ratio;
                barFill.Loaded += (_, _) => barFill.Width = barTrack.ActualWidth * ratio;
                var barGrid = new Grid();
                barGrid.Children.Add(barTrack);
                barGrid.Children.Add(barFill);
                leftCol.Children.Add(barGrid);

                // Duration
                var tspan = TimeSpan.FromSeconds(secs);
                var fmt = tspan.TotalHours >= 1
                    ? $"{(int)tspan.TotalHours}h {tspan.Minutes:D2}m"
                    : $"{tspan.Minutes}m {tspan.Seconds:D2}s";
                var durTb = new TextBlock
                {
                    Text = fmt,
                    FontSize = 16,
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(CAccent),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(durTb, 1);
                grid.Children.Add(durTb);

                dayStack.Children.Add(ticketRow);

                if (i < ticketGroups.Count - 1)
                    dayStack.Children.Add(new System.Windows.Shapes.Rectangle
                    {
                        Height = 1,
                        Fill = new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x29)),
                        Margin = new Thickness(14, 0, 14, 0),
                    });
            }

            _histPanel.Children.Add(dayCard);
        }
    }

    // ── CSV Export ────────────────────────────────────────────────────────────

    private void ExportCsvHoje()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"TimeTracker_{DateTime.Today:yyyy-MM-dd}.csv",
            DefaultExt = ".csv",
            Filter = "CSV (*.csv)|*.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dlg.ShowDialog() != true) return;

        var entries = _activeTab switch
        {
            "Esta Semana" => _histVm._storage_GetPeriod(
                                DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7)),
                                DateTime.Today),
            "Este Mês" => _histVm._storage_GetPeriod(
                                new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                                DateTime.Today),
            "Total" => _histVm._storage_GetAll(),
            _ => _vm.TodayEntries.ToList(),
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Ticket,Título,Descrição,Início,Fim,Duração (hh:mm:ss)");
        foreach (var e in entries)
        {
            var ts = TimeSpan.FromSeconds(e.Seconds);
            sb.AppendLine(
                $"\"{e.TicketId}\",\"{e.Title.Replace("\"", "\"\"")}\",\"{e.Description.Replace("\"", "\"\"")}\"," +
                $"\"{e.Start:HH:mm:ss}\",\"{e.End?.ToString("HH:mm:ss") ?? ""}\",\"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}\"");
        }
        System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Border Card() => new()
    {
        Background = new SolidColorBrush(CSurface),
        BorderBrush = new SolidColorBrush(CBorder),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
    };

    private static UIElement HSep() => new System.Windows.Shapes.Rectangle
    {
        Height = 1,
        Fill = new SolidColorBrush(CBorder),
        Margin = new Thickness(0, 12, 0, 12),
    };

    private static Button ExportBtn(string label, Color accent) => new()
    {
        Content = label,
        FontSize = 11,
        FontFamily = new FontFamily("Segoe UI Semibold"),
        Background = new SolidColorBrush(Color.FromArgb(0x18, accent.R, accent.G, accent.B)),
        Foreground = new SolidColorBrush(accent),
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, accent.R, accent.G, accent.B)),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(12, 5, 12, 5),
    };

    private void HighlightTab(string active)
    {
        foreach (var (name, btn) in _tabBtns)
        {
            if (name == active)
            {
                btn.Foreground = new SolidColorBrush(CAccent);
                btn.BorderBrush = new SolidColorBrush(CAccent);
            }
            else
            {
                btn.Foreground = new SolidColorBrush(CMuted);
                btn.BorderBrush = Brushes.Transparent;
            }
        }
    }
}

// ── AIView ────────────────────────────────────────────────────────────────────

public partial class AIView : Page
{
    private readonly AIViewModel _vm;

    public AIView(AIViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        Title = "Análise com IA";
        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17));

        var dock = new DockPanel { Margin = new Thickness(20) };

        // Header
        var hdr = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        hdr.Children.Add(new TextBlock
        {
            Text = "Análise com IA",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        });
        DockPanel.SetDock(hdr, Dock.Top);
        dock.Children.Add(hdr);

        // Status Gemini
        var statusBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 0, 0),
            Margin = new Thickness(0, 0, 0, 12),
        };
        var statusLbl = new TextBlock { FontSize = 12 };
        var statusBinding = new System.Windows.Data.Binding("StatusText") { Source = _vm };
        statusLbl.SetBinding(TextBlock.TextProperty, statusBinding);
        var statusColorBinding = new System.Windows.Data.Binding("StatusColor")
        {
            Source = _vm,
            Converter = new ColorStringToBrushConverter()
        };
        statusLbl.SetBinding(TextBlock.ForegroundProperty, statusColorBinding);
        statusBar.Child = statusLbl;
        DockPanel.SetDock(statusBar, Dock.Top);
        dock.Children.Add(statusBar);

        // Input card
        var inputCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 0, 0),
            Margin = new Thickness(0, 0, 0, 12),
        };
        var inputRow = new Grid();
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        inputRow.Children.Add(new TextBlock
        {
            Text = "Ticket  ",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var ticketInput = new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            FontSize = 13,
            Padding = new Thickness(10, 7, 10, 7),
        };
        var ticketBinding = new System.Windows.Data.Binding("TicketInput")
        {
            Source = _vm,
            Mode = System.Windows.Data.BindingMode.TwoWay,
            UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
        };
        ticketInput.SetBinding(TextBox.TextProperty, ticketBinding);
        ticketInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) _vm.AnalyzeCommand.Execute(null); };
        Grid.SetColumn(ticketInput, 1);
        inputRow.Children.Add(ticketInput);

        var analyzeBtn = new Button
        {
            Content = "🤖  Analisar",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(8, 0, 0, 0),
            Command = _vm.AnalyzeCommand,
        };
        Grid.SetColumn(analyzeBtn, 2);
        inputRow.Children.Add(analyzeBtn);
        inputCard.Child = inputRow;

        DockPanel.SetDock(inputCard, Dock.Top);
        dock.Children.Add(inputCard);

        // Resultado
        var resultGrid = new Grid();
        resultGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resultGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        resultGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Análise (esquerda)
        var analysisCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
        };
        var analysisInner = new DockPanel();
        analysisInner.Children.Add(new TextBlock
        {
            Text = "Análise",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            Margin = new Thickness(0, 0, 0, 8),
        });
        DockPanel.SetDock(analysisInner.Children[0], Dock.Top);

        var analysisText = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            BorderThickness = new Thickness(0),
            FontSize = 12,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var analysisBinding = new System.Windows.Data.Binding("AnalysisMarkdown") { Source = _vm };
        analysisText.SetBinding(TextBox.TextProperty, analysisBinding);
        analysisInner.Children.Add(analysisText);
        analysisCard.Child = analysisInner;
        Grid.SetColumn(analysisCard, 0);
        resultGrid.Children.Add(analysisCard);

        // Resposta sugerida (direita)
        var responseCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
        };
        var responseInner = new DockPanel();

        var respHdr = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        respHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        respHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        respHdr.Children.Add(new TextBlock
        {
            Text = "Resposta Sugerida",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        });
        var copyBtn = new Button
        {
            Content = "📋 Copiar",
            FontSize = 11,
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x21, 0x28)),
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(10, 4, 10, 4),
            Command = _vm.CopyResponseCommand,
        };
        Grid.SetColumn(copyBtn, 1);
        respHdr.Children.Add(copyBtn);
        DockPanel.SetDock(respHdr, Dock.Top);
        responseInner.Children.Add(respHdr);

        var responseText = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            BorderThickness = new Thickness(0),
            FontSize = 12,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var responseBinding = new System.Windows.Data.Binding("SuggestedResponse") { Source = _vm };
        responseText.SetBinding(TextBox.TextProperty, responseBinding);
        responseInner.Children.Add(responseText);
        responseCard.Child = responseInner;
        Grid.SetColumn(responseCard, 2);
        resultGrid.Children.Add(responseCard);

        dock.Children.Add(resultGrid);
        Content = dock;
    }
}

// ── SettingsView ──────────────────────────────────────────────────────────────

public partial class SettingsView : Page
{
    private readonly SettingsViewModel _vm;

    public SettingsView(SettingsViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        Title = "Configurações";
        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17));

        var outerScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(20),
        };
        var stack = new StackPanel();

        stack.Children.Add(TitleText("Configurações"));

        // Seções
        var sections = new[]
        {
            ("🔐  Azure AD / Autenticação", new[]
            {
                ("Tenant ID", nameof(_vm.TenantId)),
                ("Client ID", nameof(_vm.ClientId)),
            }),
            ("🌐  Dataverse", new[]
            {
                ("URL do ambiente", nameof(_vm.DataverseUrl)),
                ("Versão da API",   nameof(_vm.ApiVersion)),
                ("User ID monitorado", nameof(_vm.UserId)),
            }),
            ("⏰  Monitoramento", new[]
            {
                ("Intervalo (minutos)",    nameof(_vm.PollInterval)),
                ("Alerta SLA (horas)",     nameof(_vm.SlaWarning)),
                ("Chamado parado (horas)", nameof(_vm.StaleHours)),
            }),
            ("🔔  Notificações Teams", new[]
            {
                ("Webhook URL", nameof(_vm.TeamsWebhook)),
            }),
            ("🤖  Inteligência Artificial", new[]
            {
                ("Gemini API Key", nameof(_vm.GeminiApiKey)),
                ("Modelo",         nameof(_vm.GeminiModel)),
            }),
        };

        foreach (var (title, fields) in sections)
        {
            stack.Children.Add(SectionTitle(title));
            var card = MakeCard();
            var cardStack = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };

            foreach (var (label, propName) in fields)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                row.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
                    VerticalAlignment = VerticalAlignment.Center,
                });

                var isPassword = propName.Contains("Key") || propName.Contains("Webhook");
                Control input;
                if (isPassword)
                {
                    var pb = new PasswordBox
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
                        BorderThickness = new Thickness(1, 1, 1, 1),
                        Padding = new Thickness(10, 7, 10, 7),
                    };
                    // PasswordBox não tem binding nativo — carrega via código
                    pb.Loaded += (_, _) =>
                    {
                        var prop = _vm.GetType().GetProperty(propName);
                        pb.Password = prop?.GetValue(_vm)?.ToString() ?? "";
                        pb.PasswordChanged += (_, _) =>
                            prop?.SetValue(_vm, pb.Password);
                    };
                    input = pb;
                }
                else
                {
                    var tb = new TextBox
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
                        BorderThickness = new Thickness(1, 1, 1, 1),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        Padding = new Thickness(10, 7, 10, 7),
                    };
                    tb.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(propName)
                    {
                        Source = _vm,
                        Mode = System.Windows.Data.BindingMode.TwoWay,
                        UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                    });
                    input = tb;
                }

                Grid.SetColumn(input, 1);
                row.Children.Add(input);
                cardStack.Children.Add(row);
            }
            card.Child = cardStack;
            stack.Children.Add(card);
        }

        // Botões
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 20, 0, 0),
        };

        var saveBtn = new Button
        {
            Content = "💾  Salvar configurações",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(20, 10, 20, 10),
            Margin = new Thickness(0, 0, 10, 0),
            Command = _vm.SaveCommand,
        };
        btnRow.Children.Add(saveBtn);

        var tokenBtn = new Button
        {
            Content = "🗑  Excluir cache de token",
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x0A, 0x0A)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(16, 10, 16, 10),
            Command = _vm.DeleteTokenCacheCommand,
        };
        btnRow.Children.Add(tokenBtn);
        stack.Children.Add(btnRow);

        // Status salvar
        var saveStatus = new TextBlock
        {
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
        };
        saveStatus.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("SaveStatus") { Source = _vm });
        stack.Children.Add(saveStatus);

        outerScroll.Content = stack;
        Content = outerScroll;
    }

    private static TextBlock TitleText(string text) => new()
    {
        Text = text,
        FontSize = 22,
        FontWeight = FontWeights.Bold,
        Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        Margin = new Thickness(0, 0, 0, 16),
    };

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        Margin = new Thickness(0, 16, 0, 6),
    };

    private static Border MakeCard() => new()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
        BorderThickness = new Thickness(1, 1, 1, 1),
        CornerRadius = new CornerRadius(8),
    };
}

// ── Converter: string hex → SolidColorBrush ───────────────────────────────────

public class ColorStringToBrushConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
    {
        try
        {
            if (value is string hex)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
        }
        catch { }
        return Brushes.White;
    }
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotImplementedException();
}

// ── NotesView ─────────────────────────────────────────────────────────────────

public partial class NotesView : Page
{
    private readonly NotesViewModel _vm;

    // paleta compartilhada
    private static readonly Color CBg = Color.FromRgb(0x0D, 0x11, 0x17);
    private static readonly Color CSurface = Color.FromRgb(0x13, 0x19, 0x20);
    private static readonly Color CSurface2 = Color.FromRgb(0x1A, 0x20, 0x29);
    private static readonly Color CBorder = Color.FromRgb(0x27, 0x2D, 0x38);
    private static readonly Color CText = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private static readonly Color CMuted = Color.FromRgb(0x4B, 0x56, 0x63);
    private static readonly Color CAccent = Color.FromRgb(0xA7, 0x8B, 0xFA);
    private static readonly Color CGreen = Color.FromRgb(0x22, 0xC5, 0x5E);
    private static readonly Color CRed = Color.FromRgb(0xF8, 0x51, 0x49);
    private static readonly Color CBlue = Color.FromRgb(0x58, 0xA6, 0xFF);

    // estado das abas
    private Core.Models.Notes.Note? _activeNote;
    private readonly Dictionary<int, TabEntry> _tabs = [];  // noteId → entry

    // elementos de layout
    private StackPanel? _tabStrip;
    private Grid? _editorArea;
    private TextBox? _titleBox;
    private TextBox? _contentBox;
    private TextBlock? _metaTb;
    private Border? _incidentBadge;
    private TextBlock? _incidentBadgeTb;
    private bool _suppressSave;

    private record TabEntry(Core.Models.Notes.Note Note, Border Tab, TextBlock Label);

    public NotesView(NotesViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        Title = "Notas";
        Background = new SolidColorBrush(CBg);

        // ── root layout ───────────────────────────────────────────────────────
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 0: header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 1: tab strip
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2: editor

        root.Children.Add(BuildHeader());

        // ── tab strip ─────────────────────────────────────────────────────────
        var tabStripBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x15, 0x1C)),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        Grid.SetRow(tabStripBorder, 1);
        root.Children.Add(tabStripBorder);

        var tabStripScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _tabStrip = new StackPanel { Orientation = Orientation.Horizontal };
        tabStripScroll.Content = _tabStrip;
        tabStripBorder.Child = tabStripScroll;

        // ── editor area ───────────────────────────────────────────────────────
        _editorArea = new Grid { Margin = new Thickness(0) };
        _editorArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // toolbar
        _editorArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // textarea
        _editorArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // statusbar
        Grid.SetRow(_editorArea, 2);
        root.Children.Add(_editorArea);

        BuildEditorArea();

        Content = root;

        // Carrega notas existentes como abas
        foreach (var note in _vm.Notes)
            AddTab(note, activate: false);

        // Abre primeira aba ou placeholder
        if (_tabs.Count > 0)
            ActivateTab(_tabs.Values.First().Note);
        else
            ShowEmptyState();
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private UIElement BuildHeader()
    {
        var hdr = new Grid
        {
            Background = new SolidColorBrush(CSurface),
            Margin = new Thickness(0),
        };
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Margin = new Thickness(24, 14, 0, 14) };
        titleStack.Children.Add(new TextBlock
        {
            Text = "Notas",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "Bloco de notas pessoal — vinculável a chamados",
            FontSize = 12,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(titleStack, 0);
        hdr.Children.Add(titleStack);

        var btnNew = new Button
        {
            Content = "+ Nova nota",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromArgb(0x25, CAccent.R, CAccent.G, CAccent.B)),
            Foreground = new SolidColorBrush(CAccent),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, CAccent.R, CAccent.G, CAccent.B)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(18, 8, 18, 8),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 24, 0),
        };
        btnNew.MouseEnter += (_, _) => btnNew.Background = new SolidColorBrush(Color.FromArgb(0x40, CAccent.R, CAccent.G, CAccent.B));
        btnNew.MouseLeave += (_, _) => btnNew.Background = new SolidColorBrush(Color.FromArgb(0x25, CAccent.R, CAccent.G, CAccent.B));
        btnNew.Click += (_, _) => NewNote();
        Grid.SetColumn(btnNew, 1);
        hdr.Children.Add(btnNew);

        Grid.SetRow(hdr, 0);
        return hdr;
    }

    // ── Editor Area ───────────────────────────────────────────────────────────

    // _pickerContainer is a Border whose .Child is replaced on each open — no reuse of visual nodes
    private Border? _pickerContainer;

    private void BuildEditorArea()
    {
        if (_editorArea == null) return;

        // ── Row 0: Toolbar ────────────────────────────────────────────────────
        var toolbar = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 10, 20, 10),
        };
        Grid.SetRow(toolbar, 0);
        _editorArea.Children.Add(toolbar);

        var toolbarContent = new Grid();
        toolbarContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbarContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Child = toolbarContent;

        var leftBar = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(leftBar, 0);
        toolbarContent.Children.Add(leftBar);

        _titleBox = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(CText),
            BorderThickness = new Thickness(0),
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(0),
            CaretBrush = new SolidColorBrush(CAccent),
            IsEnabled = false,
        };
        _titleBox.TextChanged += OnTitleChanged;
        leftBar.Children.Add(_titleBox);

        _incidentBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x20, CBlue.R, CBlue.G, CBlue.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, CBlue.R, CBlue.G, CBlue.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 6, 0, 0),
            Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed,
        };
        _incidentBadgeTb = new TextBlock
        {
            FontSize = 11,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(CBlue),
        };
        _incidentBadge.Child = _incidentBadgeTb;
        _incidentBadge.MouseLeftButtonUp += (_, _) => ToggleIncidentPicker();
        leftBar.Children.Add(_incidentBadge);

        _metaTb = new TextBlock
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(0, 4, 0, 0),
        };
        leftBar.Children.Add(_metaTb);

        var rightBar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(rightBar, 1);
        toolbarContent.Children.Add(rightBar);

        var btnLink = ToolbarBtn("🔗 Vincular chamado", CBlue);
        btnLink.Click += (_, _) => ToggleIncidentPicker();
        rightBar.Children.Add(btnLink);

        var btnUnlink = ToolbarBtn("✕ Desvincular", CMuted);
        btnUnlink.Margin = new Thickness(6, 0, 0, 0);
        btnUnlink.Click += (_, _) => UnlinkIncident();
        rightBar.Children.Add(btnUnlink);

        var btnDelete = ToolbarBtn("🗑 Excluir", CRed);
        btnDelete.Margin = new Thickness(6, 0, 0, 0);
        btnDelete.Click += (_, _) => DeleteActive();
        rightBar.Children.Add(btnDelete);

        // ── Row 1: editorStack (picker + textarea) ────────────────────────────
        var editorStack = new Grid();
        editorStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: picker
        editorStack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: textarea
        Grid.SetRow(editorStack, 1);
        _editorArea.Children.Add(editorStack);

        // Picker container — child is rebuilt fresh each open; never reused across parents
        _pickerContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1C, 0x25)),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            MaxHeight = 240,
            Visibility = Visibility.Collapsed,
        };
        Grid.SetRow(_pickerContainer, 0);
        editorStack.Children.Add(_pickerContainer);

        // Textarea
        _contentBox = new TextBox
        {
            Background = new SolidColorBrush(CBg),
            Foreground = new SolidColorBrush(CText),
            BorderThickness = new Thickness(0),
            FontSize = 14,
            FontFamily = new FontFamily("Consolas"),
            Padding = new Thickness(24, 18, 24, 18),
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            CaretBrush = new SolidColorBrush(CAccent),
            IsEnabled = false,
        };
        _contentBox.TextChanged += OnContentChanged;
        Grid.SetRow(_contentBox, 1);
        editorStack.Children.Add(_contentBox);

        // ── Row 2: Status bar ─────────────────────────────────────────────────
        var statusBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x15, 0x1C)),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 5, 20, 5),
        };
        Grid.SetRow(statusBar, 2);
        _editorArea.Children.Add(statusBar);

        var statusGrid = new Grid();
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusBar.Child = statusGrid;

        var savedTb = new TextBlock
        {
            Text = "Salvo automaticamente",
            FontSize = 10,
            Foreground = new SolidColorBrush(CMuted),
        };
        Grid.SetColumn(savedTb, 0);
        statusGrid.Children.Add(savedTb);

        var charCountTb = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(CMuted) };
        Grid.SetColumn(charCountTb, 1);
        statusGrid.Children.Add(charCountTb);

        _contentBox.TextChanged += (_, _) =>
        {
            if (_contentBox == null) return;
            var chars = _contentBox.Text.Length;
            var lines = _contentBox.Text.Split('\n').Length;
            charCountTb.Text = $"{chars} chars · {lines} linhas";
        };
    }

    // ── Tab management ────────────────────────────────────────────────────────

    private void AddTab(Core.Models.Notes.Note note, bool activate = true)
    {
        if (_tabStrip == null) return;

        var label = new TextBlock
        {
            Text = TruncateTitle(note.Title),
            FontSize = 12,
            Foreground = new SolidColorBrush(CMuted),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 140,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // Close button
        var closeBtn = new TextBlock
        {
            Text = "×",
            FontSize = 14,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var tabInner = new StackPanel { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrEmpty(note.TicketNumber))
        {
            tabInner.Children.Add(new TextBlock
            {
                Text = "🔗 ",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0),
                Opacity = 0.6,
            });
        }
        tabInner.Children.Add(label);
        tabInner.Children.Add(closeBtn);

        var tab = new Border
        {
            Background = new SolidColorBrush(CSurface2),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1, 1, 1, 0),
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Padding = new Thickness(12, 7, 10, 7),
            Margin = new Thickness(0, 4, 3, 0),
            Cursor = Cursors.Hand,
            Child = tabInner,
        };

        var entry = new TabEntry(note, tab, label);
        _tabs[note.Id] = entry;

        tab.MouseLeftButtonUp += (_, e) =>
        {
            if (e.Source == closeBtn) return;
            ActivateTab(note);
        };
        closeBtn.MouseLeftButtonUp += (_, _) => CloseTab(note);
        closeBtn.MouseEnter += (_, _) => closeBtn.Foreground = new SolidColorBrush(CRed);
        closeBtn.MouseLeave += (_, _) => closeBtn.Foreground = new SolidColorBrush(CMuted);
        tab.MouseEnter += (_, _) => { if (_activeNote?.Id != note.Id) tab.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x33)); };
        tab.MouseLeave += (_, _) => { if (_activeNote?.Id != note.Id) tab.Background = new SolidColorBrush(CSurface2); };

        // Insert before any "+" button placeholder
        _tabStrip.Children.Add(tab);

        if (activate)
            ActivateTab(note);
    }

    private void ActivateTab(Core.Models.Notes.Note note)
    {
        // Deactivate all
        foreach (var e in _tabs.Values)
        {
            e.Tab.Background = new SolidColorBrush(CSurface2);
            e.Tab.BorderBrush = new SolidColorBrush(CBorder);
            e.Label.Foreground = new SolidColorBrush(CMuted);
        }

        if (!_tabs.TryGetValue(note.Id, out var entry)) return;

        // Activate
        entry.Tab.Background = new SolidColorBrush(CSurface);
        entry.Tab.BorderBrush = new SolidColorBrush(CAccent);
        entry.Label.Foreground = new SolidColorBrush(CText);

        _activeNote = note;
        LoadNoteIntoEditor(note);
        HideEmptyState();
    }

    private void CloseTab(Core.Models.Notes.Note note)
    {
        if (!_tabs.TryGetValue(note.Id, out var entry)) return;

        // Save before closing
        if (_activeNote?.Id == note.Id)
            SaveActive();

        _tabStrip?.Children.Remove(entry.Tab);
        _tabs.Remove(note.Id);

        if (_activeNote?.Id == note.Id)
        {
            _activeNote = null;
            if (_tabs.Count > 0)
                ActivateTab(_tabs.Values.Last().Note);
            else
                ShowEmptyState();
        }
    }

    private void LoadNoteIntoEditor(Core.Models.Notes.Note note)
    {
        if (_titleBox == null || _contentBox == null) return;

        _suppressSave = true;
        _titleBox.IsEnabled = true;
        _contentBox.IsEnabled = true;
        _titleBox.Text = note.Title;
        _contentBox.Text = note.Content;
        _suppressSave = false;

        if (_metaTb != null)
            _metaTb.Text = $"Criada {note.CreatedAt:dd/MM/yyyy HH:mm}  ·  Editada {note.UpdatedAt:dd/MM/yyyy HH:mm}";

        UpdateIncidentBadge(note);
        RebuildIncidentPicker();
    }

    private void UpdateIncidentBadge(Core.Models.Notes.Note note)
    {
        if (_incidentBadge == null || _incidentBadgeTb == null) return;
        if (!string.IsNullOrEmpty(note.TicketNumber))
        {
            _incidentBadgeTb.Text = $"🔗 {note.TicketNumber}  {note.IncidentTitle ?? ""}".Trim();
            _incidentBadge.Visibility = Visibility.Visible;
        }
        else
        {
            _incidentBadge.Visibility = Visibility.Collapsed;
        }
    }

    // ── Incident picker ───────────────────────────────────────────────────────

    private bool _pickerOpen = false;

    private void ToggleIncidentPicker()
    {
        if (_pickerContainer == null) return;
        _pickerOpen = !_pickerOpen;
        if (_pickerOpen)
        {
            RebuildIncidentPicker();
            _pickerContainer.Visibility = Visibility.Visible;
        }
        else
        {
            _pickerContainer.Visibility = Visibility.Collapsed;
            _pickerContainer.Child = null;
        }
    }

    private void RebuildIncidentPicker()
    {
        if (_pickerContainer == null) return;

        // Build completely fresh every time — no reuse of visual nodes
        var outer = new Grid();
        outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // search
        outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // list

        var searchBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
            Foreground = new SolidColorBrush(CText),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            FontSize = 12,
            Padding = new Thickness(14, 8, 14, 8),
            CaretBrush = new SolidColorBrush(CAccent),
        };
        Grid.SetRow(searchBox, 0);
        outer.Children.Add(searchBox);

        var listPanel = new StackPanel();
        var listSv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 180,
            Content = listPanel,
        };
        Grid.SetRow(listSv, 1);
        outer.Children.Add(listSv);

        void PopulateList(string filter)
        {
            listPanel.Children.Clear();
            var matches = _vm.Incidents
                .Where(i => string.IsNullOrEmpty(filter) ||
                    i.TicketNumber.ToLower().Contains(filter) ||
                    i.Title.ToLower().Contains(filter) ||
                    (i.CustomerDisplayName ?? "").ToLower().Contains(filter))
                .Take(20);

            foreach (var inc in matches)
            {
                var row = new Border
                {
                    Padding = new Thickness(14, 8, 14, 8),
                    Cursor = Cursors.Hand,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x33)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                };
                var rowContent = new StackPanel { Orientation = Orientation.Horizontal };
                rowContent.Children.Add(new TextBlock
                {
                    Text = inc.TicketNumber,
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(CBlue),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
                rowContent.Children.Add(new TextBlock
                {
                    Text = inc.Title.Length > 60 ? inc.Title[..60] + "…" : inc.Title,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(CText),
                    VerticalAlignment = VerticalAlignment.Center,
                });
                row.Child = rowContent;

                var captured = inc;
                row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x33));
                row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonUp += (_, _) => LinkIncident(captured);
                listPanel.Children.Add(row);
            }

            if (!listPanel.Children.Cast<UIElement>().Any())
                listPanel.Children.Add(new TextBlock
                {
                    Text = "Nenhum chamado encontrado.",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(CMuted),
                    Padding = new Thickness(14, 8, 14, 8),
                });
        }

        searchBox.TextChanged += (_, _) => PopulateList(searchBox.Text.ToLower());
        PopulateList("");

        // Assign fresh tree to container — no prior parent
        _pickerContainer.Child = outer;
    }

    private void LinkIncident(IncidentSnapshot inc)
    {
        if (_activeNote == null) return;
        _activeNote.IncidentId = inc.IncidentId;
        _activeNote.IncidentTitle = inc.Title;
        _activeNote.TicketNumber = inc.TicketNumber;
        SaveActive();
        UpdateIncidentBadge(_activeNote);
        UpdateTabIcon(_activeNote);
        ToggleIncidentPicker(); // close
    }

    private void UnlinkIncident()
    {
        if (_activeNote == null) return;
        _activeNote.IncidentId = null;
        _activeNote.IncidentTitle = null;
        _activeNote.TicketNumber = null;
        SaveActive();
        UpdateIncidentBadge(_activeNote);
        UpdateTabIcon(_activeNote);
    }

    private void UpdateTabIcon(Core.Models.Notes.Note note)
    {
        if (!_tabs.TryGetValue(note.Id, out var entry)) return;
        if (entry.Tab.Child is not StackPanel sp) return;
        // Rebuild inner
        sp.Children.Clear();
        if (!string.IsNullOrEmpty(note.TicketNumber))
            sp.Children.Add(new TextBlock
            {
                Text = "🔗 ",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0),
                Opacity = 0.6,
            });
        sp.Children.Add(entry.Label);
        var closeX = new TextBlock
        {
            Text = "×",
            FontSize = 14,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        closeX.MouseLeftButtonUp += (_, _) => CloseTab(note);
        closeX.MouseEnter += (_, _) => closeX.Foreground = new SolidColorBrush(CRed);
        closeX.MouseLeave += (_, _) => closeX.Foreground = new SolidColorBrush(CMuted);
        sp.Children.Add(closeX);
    }

    // ── Save / Delete ─────────────────────────────────────────────────────────

    private System.Threading.Timer? _autosaveTimer;

    private void OnTitleChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSave || _activeNote == null || _titleBox == null) return;
        _activeNote.Title = _titleBox.Text;
        // Update tab label
        if (_tabs.TryGetValue(_activeNote.Id, out var entry))
            entry.Label.Text = TruncateTitle(_activeNote.Title);
        ScheduleAutosave();
    }

    private void OnContentChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSave || _activeNote == null || _contentBox == null) return;
        _activeNote.Content = _contentBox.Text;
        ScheduleAutosave();
    }

    private void ScheduleAutosave()
    {
        _autosaveTimer?.Dispose();
        _autosaveTimer = new System.Threading.Timer(_ =>
            Dispatcher.Invoke(SaveActive), null, 800, System.Threading.Timeout.Infinite);
    }

    private void SaveActive()
    {
        if (_activeNote == null) return;
        _vm.SaveNote(_activeNote);
        if (_metaTb != null)
            _metaTb.Text = $"Criada {_activeNote.CreatedAt:dd/MM/yyyy HH:mm}  ·  Editada {_activeNote.UpdatedAt:dd/MM/yyyy HH:mm}";
    }

    private void DeleteActive()
    {
        if (_activeNote == null) return;
        var res = MessageBox.Show(
            $"Excluir a nota \"{_activeNote.Title}\"?",
            "Confirmar exclusão",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        var toDelete = _activeNote;
        CloseTab(toDelete);
        _vm.DeleteNote(toDelete);
    }

    // ── New note ──────────────────────────────────────────────────────────────

    private void NewNote()
    {
        var note = _vm.CreateNote();
        AddTab(note, activate: true);
    }

    // ── Empty state ───────────────────────────────────────────────────────────

    private Border? _emptyState;

    private void ShowEmptyState()
    {
        if (_titleBox != null) { _titleBox.Text = ""; _titleBox.IsEnabled = false; }
        if (_contentBox != null) { _contentBox.Text = ""; _contentBox.IsEnabled = false; }

        if (_emptyState != null || _editorArea == null) return;

        _emptyState = new Border
        {
            Background = new SolidColorBrush(CBg),
        };
        var center = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        center.Children.Add(new TextBlock
        {
            Text = "📝",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
        });
        center.Children.Add(new TextBlock
        {
            Text = "Nenhuma nota aberta",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CMuted),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
        });
        center.Children.Add(new TextBlock
        {
            Text = "Clique em \"+ Nova nota\" para começar",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x42, 0x4F)),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        _emptyState.Child = center;
        Grid.SetRow(_emptyState, 1);
        Grid.SetRowSpan(_emptyState, 2);
        _editorArea.Children.Add(_emptyState);
    }

    private void HideEmptyState()
    {
        if (_emptyState == null || _editorArea == null) return;
        _editorArea.Children.Remove(_emptyState);
        _emptyState = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string TruncateTitle(string t)
        => string.IsNullOrWhiteSpace(t) ? "Nova nota"
         : t.Length > 22 ? t[..22] + "…"
         : t;

    private static Button ToolbarBtn(string label, Color fg) => new()
    {
        Content = label,
        FontSize = 11,
        Background = new SolidColorBrush(Color.FromArgb(0x18, fg.R, fg.G, fg.B)),
        Foreground = new SolidColorBrush(fg),
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, fg.R, fg.G, fg.B)),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(10, 5, 10, 5),
    };
}