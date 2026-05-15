// =============================================================================
//  Views restantes: IncidentsView, AlertsView, TrackerView, AIView, SettingsView
// =============================================================================

using D365Assistant.Core.Models;
using D365Assistant.Core.Models.Alerts;
using D365Assistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

// ── IncidentsView ─────────────────────────────────────────────────────────────

public partial class IncidentsView : Page
{
    private readonly IncidentsViewModel _vm;

    public IncidentsView(IncidentsViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        Title = "Chamados";
        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17));

        var dock = new DockPanel { Margin = new Thickness(20) };

        // Header
        var hdr = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "Chamados",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        };
        Grid.SetColumn(title, 0);
        hdr.Children.Add(title);

        // Busca
        var search = new TextBox
        {
            Width = 220,
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x21, 0x28)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            FontSize = 13,
            Padding = new Thickness(10, 7, 10, 7),
        };
        search.TextChanged += (_, _) => _vm.SearchText = search.Text;
        Grid.SetColumn(search, 1);
        hdr.Children.Add(search);

        DockPanel.SetDock(hdr, Dock.Top);
        dock.Children.Add(hdr);

        // Lista
        var listView = new ListView
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        };

        var gv = new GridView();
        gv.Columns.Add(new GridViewColumn
        {
            Header = "Ticket",
            Width = 130,
            DisplayMemberBinding = new System.Windows.Data.Binding("TicketNumber")
        });
        gv.Columns.Add(new GridViewColumn
        {
            Header = "Cliente",
            Width = 160,
            DisplayMemberBinding = new System.Windows.Data.Binding("CustomerDisplayName")
        });
        gv.Columns.Add(new GridViewColumn
        {
            Header = "Título",
            Width = 350,
            DisplayMemberBinding = new System.Windows.Data.Binding("Title")
        });
        gv.Columns.Add(new GridViewColumn
        {
            Header = "Prioridade",
            Width = 90,
            DisplayMemberBinding = new System.Windows.Data.Binding("PriorityCode")
        });
        gv.Columns.Add(new GridViewColumn
        {
            Header = "Status",
            Width = 130,
            DisplayMemberBinding = new System.Windows.Data.Binding("StatusCode")
        });

        listView.View = gv;
        listView.ItemsSource = _vm.Items;
        dock.Children.Add(listView);

        Content = dock;
    }
}

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

public partial class TrackerView : Page
{
    private readonly TrackerViewModel _vm;
    private readonly TrackerHistoryViewModel _histVm;

    private string _activeTab = "Hoje";
    private StackPanel? _histPanel;
    private TextBlock? _histTotal;
    private TextBlock? _histLabel;
    private readonly Dictionary<string, Button> _tabBtns = [];

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

        // Root: 3 rows — timer card | controls card | history card
        var root = new Grid { Margin = new Thickness(24, 20, 24, 20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // timer
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });        // gap
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // controls
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });        // gap
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // history

        var timerCard = BuildTimerCard();
        var controlsCard = BuildControlsCard();
        var historyCard = BuildHistoryCard();

        Grid.SetRow(timerCard, 0);
        Grid.SetRow(controlsCard, 2);
        Grid.SetRow(historyCard, 4);

        root.Children.Add(timerCard);
        root.Children.Add(controlsCard);
        root.Children.Add(historyCard);

        Content = root;

        _vm.TodayEntries.CollectionChanged += (_, _) => Dispatcher.Invoke(RefreshHistory);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_vm.IsRunning) or nameof(_vm.StatusPill))
                Dispatcher.Invoke(SyncTimerCard);
        };
        RefreshHistory();
    }

    // ── Timer Card ────────────────────────────────────────────────────────────

    private Border? _timerCard;
    private TextBlock? _pillText;
    private Border? _pill;

    private UIElement BuildTimerCard()
    {
        _timerCard = Card();

        var grid = new Grid { Margin = new Thickness(28, 20, 28, 20) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _timerCard.Child = grid;

        // Left: status + timer + active ticket
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        _pill = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 3, 12, 3),
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _pillText = new TextBlock { FontSize = 10, FontWeight = FontWeights.Bold };
        _pill.Child = _pillText;
        left.Children.Add(_pill);

        var timerTb = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 52,
            FontWeight = FontWeights.Bold,
        };
        timerTb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("TimerDisplay") { Source = _vm });
        timerTb.SetBinding(TextBlock.ForegroundProperty,
            new System.Windows.Data.Binding("TimerColor") { Source = _vm, Converter = new ColorStringToBrushConverter() });
        left.Children.Add(timerTb);

        var activeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
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

        // Right: day total summary
        var right = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0xA7, 0x8B, 0xFA)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xA7, 0x8B, 0xFA)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20, 14, 20, 14),
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
        right.Child = rightStack;

        SyncTimerCard();
        return _timerCard;
    }

    private void SyncTimerCard()
    {
        if (_pill == null || _pillText == null) return;
        var isRunning = _vm.IsRunning;
        var color = isRunning ? CGreen : CYellow;
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
        var outer = new Grid { Margin = new Thickness(20, 16, 20, 16) };
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        card.Child = outer;

        // Left: input + recents
        var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(leftStack, 0);
        outer.Children.Add(leftStack);

        var inputRow = new Grid();
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var inputLabel = new TextBlock
        {
            Text = "Chamado",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(CMuted),
            Margin = new Thickness(0, 0, 0, 5),
        };
        leftStack.Children.Add(inputLabel);

        var input = new TextBox
        {
            Background = new SolidColorBrush(CBg),
            Foreground = new SolidColorBrush(CText),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            FontSize = 13,
            FontFamily = new FontFamily("Consolas"),
            Padding = new Thickness(12, 9, 12, 9),
            CaretBrush = new SolidColorBrush(CAccent),
        };
        input.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("TicketInput")
        {
            Source = _vm,
            Mode = System.Windows.Data.BindingMode.TwoWay,
            UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
        });
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) _vm.StartCommand.Execute(null); };
        leftStack.Children.Add(input);

        // Recents chips
        var recentRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
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
                    Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x29)),
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
                chip.MouseLeave += (_, _) => chip.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x29));
                recentRow.Children.Add(chip);
            }
        }
        _vm.RecentTickets.CollectionChanged += (_, _) => Dispatcher.Invoke(RebuildRecents);
        RebuildRecents();
        leftStack.Children.Add(recentRow);

        // Right: action buttons (vertical stack)
        var btnStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(btnStack, 2);
        outer.Children.Add(btnStack);

        var btnRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var btnRow2 = new StackPanel { Orientation = Orientation.Horizontal };

        Button ActionBtn(string label, Color fg, Color bg, System.Windows.Input.ICommand cmd, bool hasBorder = false)
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
                Padding = new Thickness(16, 9, 16, 9),
                Margin = new Thickness(0, 0, 6, 0),
                Command = cmd,
                MinWidth = 100,
            };
            b.MouseEnter += (_, _) => b.Background = new SolidColorBrush(Color.FromArgb(0x40, bg.R, bg.G, bg.B));
            b.MouseLeave += (_, _) => b.Background = new SolidColorBrush(Color.FromArgb(0x22, bg.R, bg.G, bg.B));
            return b;
        }

        btnRow1.Children.Add(ActionBtn("▶  Iniciar", CGreen, CGreen, _vm.StartCommand));
        btnRow1.Children.Add(ActionBtn("↺  Retomar", CText, CMuted, _vm.PauseCommand));
        btnRow2.Children.Add(ActionBtn("⏹  Finalizar", CRed, CRed, _vm.StopCommand));
        btnRow2.Children.Add(ActionBtn("⇄  Trocar", CAccent, CAccent, _vm.SwitchCommand));

        btnStack.Children.Add(btnRow1);
        btnStack.Children.Add(btnRow2);

        return card;
    }

    // ── History Card ──────────────────────────────────────────────────────────

    private UIElement BuildHistoryCard()
    {
        var card = Card();
        var dock = new DockPanel();
        card.Child = dock;

        // Header: tabs + export buttons
        var header = new Border
        {
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0x1A, 0x20, 0x29)),
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

        // Section label + total (center)
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

        // Export buttons (right)
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

        // Entries scroll
        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
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
            default: // Hoje
                entries = _vm.TodayEntries.ToList();
                label = DateTime.Today.ToString("dd/MM/yyyy");
                break;
        }

        // Update header label + grand total
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

        // ── Group by day (descending), then by ticket ─────────────────────────
        var byDay = entries
            .GroupBy(e => e.Start.Date)
            .OrderByDescending(g => g.Key);

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

            // ── Day card ──────────────────────────────────────────────────────
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

            // Day header row
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

            dayHdrGrid.Children.Add(new TextBlock
            {
                Text = dayLabel,
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

            // Divider
            dayStack.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(CBorder),
            });

            // ── Ticket rows inside day ────────────────────────────────────────
            var ticketGroups = dayGroup
                .GroupBy(e => e.TicketId)
                .Select(g => (
                    Ticket: g.Key,
                    Title: g.FirstOrDefault()?.Title ?? "",
                    Secs: g.Sum(e => e.Seconds),
                    Count: g.Count(),
                    First: g.Min(e => e.Start),
                    Last: g.Max(e => e.End ?? e.Start)
                ))
                .OrderByDescending(g => g.Secs)
                .ToList();

            var maxTicketSecs = ticketGroups.Max(g => g.Secs);

            for (int i = 0; i < ticketGroups.Count; i++)
            {
                var (ticket, title, secs, count, first, last) = ticketGroups[i];

                var ticketRow = new Border
                {
                    Padding = new Thickness(14, 10, 14, 10),
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                ticketRow.Child = grid;

                // Left side
                var leftCol = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
                Grid.SetColumn(leftCol, 0);
                grid.Children.Add(leftCol);

                // Badge + title row
                var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
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

                // Time range + session count
                var metaRow = new StackPanel { Orientation = Orientation.Horizontal };
                metaRow.Children.Add(new TextBlock
                {
                    Text = $"{first:HH:mm} – {last:HH:mm}",
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

                // Progress bar
                var barTrack = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x25, 0x30)),
                    CornerRadius = new CornerRadius(3),
                    Height = 3,
                    Margin = new Thickness(0, 6, 0, 0),
                };
                var barFill = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x99, 0xA7, 0x8B, 0xFA)),
                    CornerRadius = new CornerRadius(3),
                    Height = 3,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                var ratio = maxTicketSecs > 0 ? (double)secs / maxTicketSecs : 0;
                barTrack.SizeChanged += (_, e) => barFill.Width = e.NewSize.Width * ratio;
                barFill.Loaded += (_, _) => barFill.Width = barTrack.ActualWidth * ratio;
                var barGrid = new Grid();
                barGrid.Children.Add(barTrack);
                barGrid.Children.Add(barFill);
                leftCol.Children.Add(barGrid);

                // Right: duration
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

                // Divider between tickets (not after last)
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
        sb.AppendLine("Ticket,Título,Início,Fim,Duração (hh:mm:ss)");
        foreach (var e in entries)
        {
            var ts = TimeSpan.FromSeconds(e.Seconds);
            sb.AppendLine(
                $"\"{e.TicketId}\",\"{e.Title.Replace("\"", "\"\"")}\",\"{e.Start:HH:mm:ss}\"," +
                $"\"{e.End?.ToString("HH:mm:ss") ?? ""}\",\"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}\"");
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