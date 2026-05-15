// =============================================================================
//  TodoView.xaml.cs — Todo estilo tabela + painel detalhes lateral
// =============================================================================

using D365Assistant.Core.Models.Todo;
using D365Assistant.ViewModels;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

public partial class TodoView : Page
{
    private readonly TodoViewModel _vm;

    // ── Palette (Dark) ────────────────────────────────────────────────────────
    private static readonly Color CBg = Color.FromRgb(0x08, 0x0C, 0x12);
    private static readonly Color CSurface = Color.FromRgb(0x0F, 0x15, 0x20);
    private static readonly Color CSurface2 = Color.FromRgb(0x13, 0x1B, 0x27);
    private static readonly Color CSurface3 = Color.FromRgb(0x18, 0x22, 0x30);
    private static readonly Color CBorder = Color.FromRgb(0x1E, 0x28, 0x38);
    private static readonly Color CBorder2 = Color.FromRgb(0x28, 0x34, 0x44);
    private static readonly Color CText = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private static readonly Color CTextSub = Color.FromRgb(0x64, 0x74, 0x8B);
    private static readonly Color CAccent = Color.FromRgb(0x3B, 0x82, 0xF6);
    private static readonly Color CGreen = Color.FromRgb(0x22, 0xC5, 0x5E);
    private static readonly Color CRed = Color.FromRgb(0xEF, 0x44, 0x44);
    private static readonly Color CYellow = Color.FromRgb(0xF5, 0x9E, 0x0B);
    private static readonly Color CPurple = Color.FromRgb(0xA7, 0x8B, 0xFA);
    private static readonly Color CRowHov = Color.FromRgb(0x12, 0x1C, 0x2C);
    private static readonly Color CRowSel = Color.FromRgb(0x16, 0x22, 0x36);

    // ── State ─────────────────────────────────────────────────────────────────
    private TodoItem? _selected;
    private int _page = 1;
    private const int PageSize = 12;
    private string _activeTab = "Minhas Tarefas";
    private string _sortBy = "Vencimento";
    private string _statusFilter = "Todos";

    // ── UI Refs ───────────────────────────────────────────────────────────────
    private StackPanel? _tableBody;
    private Border? _detailPanel;
    private StackPanel? _detailContent;
    private TextBlock? _totalTb;
    private TextBlock? _pageTb;
    private Border? _formOverlay;
    private TextBox? _formTitleBox;
    private TextBox? _formDescBox;
    private TextBox? _formTicketBox;
    private TextBox? _formNotesBox;
    private DatePicker? _formDuePicker;
    private TextBlock? _formErrorTb;
    private Func<string?>? _getCat;
    private Action<string>? _setCat;
    private Func<string?>? _getPri;
    private Action<string>? _setPri;
    private readonly Dictionary<string, Button> _tabBtns = [];
    private readonly Dictionary<string, Border> _rowMap = [];

    // ── Notification timer ────────────────────────────────────────────────────
    private readonly System.Windows.Threading.DispatcherTimer _notifTimer;

    public TodoView(TodoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        _notifTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _notifTimer.Tick += (_, _) => CheckNotifications();
        _notifTimer.Start();

        var root = BuildRoot();
        ((Grid)Content).Children.Add(root);

        _formOverlay = BuildFormOverlay();
        ((Grid)Content).Children.Add(_formOverlay);

        // Só carrega do banco se ainda não tem dados (evita duplicação ao renavegar)
        if (_vm.Items.Count == 0)
            _vm.Load();

        Refresh();

        void OnCollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => Dispatcher.Invoke(Refresh);

        void OnPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(_vm.CountPending) or nameof(_vm.CountToday)
                               or nameof(_vm.CountOverdue) or nameof(_vm.CountDone))
                Dispatcher.Invoke(Refresh);
        }

        _vm.Items.CollectionChanged += OnCollectionChanged;
        _vm.PropertyChanged += OnPropertyChanged;

        // Desinscreve ao sair da página — evita acumulação de handlers
        this.Unloaded += (_, _) =>
        {
            _vm.Items.CollectionChanged -= OnCollectionChanged;
            _vm.PropertyChanged -= OnPropertyChanged;
            _notifTimer.Stop();
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ROOT LAYOUT
    // ══════════════════════════════════════════════════════════════════════════

    private UIElement BuildRoot()
    {
        var root = new Grid { Background = new SolidColorBrush(CBg) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // top bar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // tabs
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // toolbar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content

        root.Children.Add(BuildTopBar()); Grid.SetRow(root.Children[root.Children.Count - 1], 0);
        root.Children.Add(BuildTabs()); Grid.SetRow(root.Children[root.Children.Count - 1], 1);
        root.Children.Add(BuildToolbar()); Grid.SetRow(root.Children[root.Children.Count - 1], 2);
        root.Children.Add(BuildContent()); Grid.SetRow(root.Children[root.Children.Count - 1], 3);

        return root;
    }

    // ── Top bar ───────────────────────────────────────────────────────────────

    private UIElement BuildTopBar()
    {
        var bar = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 14, 24, 14),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = grid;

        // Left: title
        var left = new StackPanel();
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);
        left.Children.Add(new TextBlock
        {
            Text = "TODO",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(CText),
        });
        left.Children.Add(new TextBlock
        {
            Text = "Gerencie suas tarefas e atividades pendentes",
            FontSize = 11,
            Foreground = new SolidColorBrush(CTextSub),
        });

        // Right: period dropdown + refresh + new
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        right.Children.Add(new TextBlock
        {
            Text = "Período:",
            FontSize = 12,
            Foreground = new SolidColorBrush(CTextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var (periodHost, getPeriod, _, setPeriodItems) = DarkDropdown();
        setPeriodItems(["Todas as tarefas", "Esta semana", "Este mês", "Hoje"]);
        periodHost.Width = 160;
        periodHost.Margin = new Thickness(0, 0, 10, 0);
        right.Children.Add(periodHost);

        var btnRefresh = OutlineBtn("↻  Atualizar");
        btnRefresh.Click += (_, _) => { _vm.Load(); Refresh(); };
        right.Children.Add(btnRefresh);

        var btnNew = PrimaryBtn("+ Nova Tarefa");
        btnNew.Margin = new Thickness(8, 0, 0, 0);
        btnNew.Click += (_, _) => { _vm.OpenNewCommand.Execute(null); ShowForm(); };
        right.Children.Add(btnNew);

        return bar;
    }

    // ── Tabs ──────────────────────────────────────────────────────────────────

    private UIElement BuildTabs()
    {
        var bar = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 0, 20, 0),
        };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Child = tabs;

        foreach (var (label, count) in new[]
        {
            ("Minhas Tarefas",    _vm.CountPending),
            ("Tarefas Concluídas",_vm.CountDone),
        })
        {
            var btn = TabBtn(label, count > 0 ? count.ToString() : null);
            var l = label;
            btn.Click += (_, _) => { _activeTab = l; HighlightTab(l); Refresh(); };
            _tabBtns[label] = btn;
            tabs.Children.Add(btn);
        }
        HighlightTab("Minhas Tarefas");
        return bar;
    }

    private Button TabBtn(string label, string? badge)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        if (badge != null)
            stack.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0xA7, 0x8B, 0xFA)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 1, 7, 1),
                Margin = new Thickness(6, 0, 0, 0),
                Child = new TextBlock { Text = badge, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(CPurple) },
            });

        return new Button
        {
            Content = stack,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(CTextSub),
            BorderThickness = new Thickness(0, 0, 0, 2),
            BorderBrush = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Padding = new Thickness(4, 12, 4, 12),
            Margin = new Thickness(0, 0, 20, 0),
        };
    }

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
                btn.Foreground = new SolidColorBrush(CTextSub);
                btn.BorderBrush = Brushes.Transparent;
            }
        }
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private UIElement BuildToolbar()
    {
        var bar = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 8, 16, 8),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = grid;

        // Search
        var searchWrap = new Border
        {
            Background = new SolidColorBrush(CBg),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            Height = 32,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var sr = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sr.Children.Add(new TextBlock { Text = "⌕", FontSize = 13, Foreground = new SolidColorBrush(CTextSub), Margin = new Thickness(0, 0, 6, 0) });
        var searchBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(CText),
            FontSize = 12,
            Width = 160,
        };
        searchBox.TextChanged += (_, _) => { _vm.SearchText = searchBox.Text; _page = 1; Refresh(); };
        sr.Children.Add(searchBox);
        searchWrap.Child = sr;
        Grid.SetColumn(searchWrap, 0);
        grid.Children.Add(searchWrap);

        // Right: status filter + sort
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        right.Children.Add(new TextBlock { Text = "Status:", FontSize = 11, Foreground = new SolidColorBrush(CTextSub), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });

        var (statusHost, getStatus, _, setStatusItems) = DarkDropdown();
        setStatusItems(["Todos", "Pendentes", "Atrasadas", "Hoje", "Concluídas"]);
        statusHost.Width = 120;
        statusHost.Margin = new Thickness(0, 0, 16, 0);
        statusHost.Tag = getStatus;
        right.Children.Add(statusHost);

        right.Children.Add(new TextBlock { Text = "Ordenar por:", FontSize = 11, Foreground = new SolidColorBrush(CTextSub), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });

        var (sortHost, getSort, _, setSortItems) = DarkDropdown();
        setSortItems(["Data de Vencimento", "Prioridade", "Título", "Criação"]);
        sortHost.Width = 150;
        sortHost.Margin = new Thickness(0, 0, 12, 0);
        right.Children.Add(sortHost);

        // Grid/list toggle (cosmetic)
        var btnGrid = new Button
        {
            Content = "⊞",
            FontSize = 14,
            Background = new SolidColorBrush(CBg),
            Foreground = new SolidColorBrush(CTextSub),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(8, 4, 8, 4),
        };
        right.Children.Add(btnGrid);

        return bar;
    }

    // ── Content: table + detail panel ────────────────────────────────────────

    private UIElement BuildContent()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Table area
        var tableArea = new Grid();
        tableArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        tableArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(tableArea, 0);
        grid.Children.Add(tableArea);

        // Table
        var tableWrap = new Border
        {
            Background = new SolidColorBrush(CSurface),
            Margin = new Thickness(0),
        };
        var tableStack = new DockPanel();
        tableWrap.Child = tableStack;

        // Table header
        tableStack.Children.Add(BuildTableHeader());
        DockPanel.SetDock(tableStack.Children[0], Dock.Top);

        // Table body scroll
        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        sv.PreviewMouseWheel += (_, e) => { sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0); e.Handled = true; };
        _tableBody = new StackPanel();
        sv.Content = _tableBody;
        tableStack.Children.Add(sv);
        Grid.SetRow(tableWrap, 0);
        tableArea.Children.Add(tableWrap);

        // Bottom bar
        var bottom = BuildBottomBar();
        Grid.SetRow(bottom, 1);
        tableArea.Children.Add(bottom);

        // Detail panel (right)
        _detailPanel = new Border
        {
            Width = 320,
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        Grid.SetColumn(_detailPanel, 1);
        grid.Children.Add(_detailPanel);

        _detailContent = new StackPanel();
        var detailScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _detailContent,
        };
        _detailPanel.Child = detailScroll;

        return grid;
    }

    // ── Table header row ──────────────────────────────────────────────────────

    private UIElement BuildTableHeader()
    {
        var hdr = new Border
        {
            Background = new SolidColorBrush(CSurface2),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 0),
        };
        var g = ColGrid();
        hdr.Child = g;

        void HdrCell(int col, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CTextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(col == 0 ? 16 : 0, 10, 8, 10),
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        HdrCell(0, "Tarefa");
        HdrCell(1, "Relacionado a");
        HdrCell(2, "Tipo");
        HdrCell(3, "Prioridade");
        HdrCell(4, "Vencimento");
        HdrCell(5, "Status");
        return hdr;
    }

    // ── Bottom bar ────────────────────────────────────────────────────────────

    private UIElement BuildBottomBar()
    {
        var bar = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 8, 16, 8),
        };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = g;

        _totalTb = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(CTextSub), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_totalTb, 0);
        g.Children.Add(_totalTb);

        var pagRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(pagRow, 1);
        g.Children.Add(pagRow);

        Button PagBtn(string label)
        {
            var b = new Button
            {
                Content = label,
                FontSize = 12,
                Background = new SolidColorBrush(CSurface),
                Foreground = new SolidColorBrush(CText),
                BorderBrush = new SolidColorBrush(CBorder),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(2, 0, 2, 0),
                MinWidth = 30,
            };
            return b;
        }

        var btnFirst = PagBtn("«"); btnFirst.Click += (_, _) => { _page = 1; Refresh(); };
        var btnPrev = PagBtn("‹"); btnPrev.Click += (_, _) => { if (_page > 1) { _page--; Refresh(); } };
        _pageTb = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(CText), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
        var btnNext = PagBtn("›"); btnNext.Click += (_, _) => { _page++; Refresh(); };
        var btnLast = PagBtn("»"); btnLast.Click += (_, _) => { _page = 999; Refresh(); };

        pagRow.Children.Add(btnFirst);
        pagRow.Children.Add(btnPrev);
        pagRow.Children.Add(_pageTb);
        pagRow.Children.Add(btnNext);
        pagRow.Children.Add(btnLast);

        return bar;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  REFRESH / RENDER
    // ══════════════════════════════════════════════════════════════════════════

    private void Refresh()
    {
        if (_tableBody == null) return;
        _tableBody.Children.Clear();
        _rowMap.Clear();

        // Tab filter
        _vm.FilterGroup = _activeTab switch
        {
            "Tarefas Concluídas" => "Concluídas",
            _ => "Todas",
        };

        var items = _vm.Items.ToList();

        // Status filter
        items = _statusFilter switch
        {
            "Pendentes" => items.Where(t => !t.Done && !t.IsOverdue).ToList(),
            "Atrasadas" => items.Where(t => t.IsOverdue).ToList(),
            "Hoje" => items.Where(t => t.IsDueToday).ToList(),
            "Concluídas" => items.Where(t => t.Done).ToList(),
            _ => items,
        };

        // For "Minhas Tarefas" tab — exclude done
        if (_activeTab == "Minhas Tarefas")
            items = items.Where(t => !t.Done).ToList();
        else if (_activeTab == "Tarefas Concluídas")
            items = items.Where(t => t.Done).ToList();

        var total = items.Count;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        _page = Math.Clamp(_page, 1, pages);

        var paged = items.Skip((_page - 1) * PageSize).Take(PageSize).ToList();

        if (_totalTb != null)
            _totalTb.Text = $"Total: {total} tarefa{(total != 1 ? "s" : "")}";
        if (_pageTb != null)
            _pageTb.Text = $"{_page}";

        if (total == 0)
        {
            _tableBody.Children.Add(new TextBlock
            {
                Text = "Nenhuma tarefa encontrada.",
                FontSize = 13,
                Foreground = new SolidColorBrush(CTextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
            return;
        }

        foreach (var item in paged)
        {
            var row = BuildRow(item);
            _rowMap[item.Id.ToString()] = row;
            _tableBody.Children.Add(row);
        }

        // Restore selection highlight
        if (_selected != null && _rowMap.TryGetValue(_selected.Id.ToString(), out var selRow))
            selRow.Background = new SolidColorBrush(CRowSel);
    }

    // ── Table row ─────────────────────────────────────────────────────────────

    private Border BuildRow(TodoItem item)
    {
        var row = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.Hand,
        };
        row.MouseEnter += (_, _) => { if (_selected?.Id != item.Id) row.Background = new SolidColorBrush(CRowHov); };
        row.MouseLeave += (_, _) => { if (_selected?.Id != item.Id) row.Background = new SolidColorBrush(CSurface); };
        row.MouseLeftButtonUp += (_, _) => SelectRow(item, row);

        var g = ColGrid();
        g.Margin = new Thickness(0, 0, 0, 0);
        row.Child = g;

        // Col 0: checkbox + title + subtitle
        var col0 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 10, 8, 10), VerticalAlignment = VerticalAlignment.Center };

        var chk = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1.5),
            BorderBrush = item.Done
                ? new SolidColorBrush(CGreen)
                : new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)),
            Background = item.Done
                ? new SolidColorBrush(Color.FromArgb(0x20, 0x16, 0xA3, 0x4A))
                : Brushes.Transparent,
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand,
            Child = item.Done ? new TextBlock
            {
                Text = "✓",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(CGreen),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            } : null,
        };
        var itm = item;
        chk.MouseLeftButtonUp += (_, e) =>
        {
            _vm.ToggleCommand.Execute(itm);
            e.Handled = true;
            Refresh();
        };
        col0.Children.Add(chk);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleTb = new TextBlock
        {
            Text = item.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(item.Done ? CTextSub : CText),
            TextDecorations = item.Done ? TextDecorations.Strikethrough : null,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 260,
        };
        titleStack.Children.Add(titleTb);
        if (!string.IsNullOrWhiteSpace(item.Description))
            titleStack.Children.Add(new TextBlock
            {
                Text = item.Description.Length > 55 ? item.Description[..55] + "…" : item.Description,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(CTextSub),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 260,
            });
        col0.Children.Add(titleStack);

        Grid.SetColumn(col0, 0);
        g.Children.Add(col0);

        // Col 1: ticket link
        var ticketTb = new TextBlock
        {
            Text = item.TicketId ?? "—",
            FontSize = 11,
            Foreground = string.IsNullOrEmpty(item.TicketId)
                ? new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB))
                : new SolidColorBrush(CAccent),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(ticketTb, 1);
        g.Children.Add(ticketTb);

        // Col 2: category
        var catTb = new TextBlock
        {
            Text = item.Category,
            FontSize = 11,
            Foreground = new SolidColorBrush(CTextSub),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(catTb, 2);
        g.Children.Add(catTb);

        // Col 3: priority badge
        var (priFg, priBg, priText) = item.Priority switch
        {
            1 => ("#FCA5A5", "#3B0C0C", "Alta"),
            3 => ("#86EFAC", "#0A2010", "Baixa"),
            _ => ("#FCD34D", "#3B2A00", "Média"),
        };
        var priBadge = LightBadge(priText, priFg, priBg);
        Grid.SetColumn(priBadge, 3);
        g.Children.Add(priBadge);

        // Col 4: due date
        var dueFg = item.IsOverdue ? CRed : item.IsDueToday ? CYellow : CTextSub;
        var dueTb = new TextBlock
        {
            Text = item.DueDate.HasValue ? item.DueDate.Value.ToString("dd/MM/yyyy HH:mm") : "—",
            FontSize = 11,
            Foreground = new SolidColorBrush(dueFg),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(dueTb, 4);
        g.Children.Add(dueTb);

        // Col 5: status badge
        var (stFg, stBg, stText) = item.Done
            ? ("#86EFAC", "#0A2010", "Concluída")
            : item.IsOverdue
            ? ("#FCA5A5", "#3B0C0C", "Atrasada")
            : ("#94A3B8", "#1E2A38", "Pendente");
        var stBadge = LightBadge(stText, stFg, stBg);
        Grid.SetColumn(stBadge, 5);
        g.Children.Add(stBadge);

        return row;
    }

    private Grid ColGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.8, GridUnitType.Star) }); // tarefa
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }); // relacionado
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });   // tipo
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) }); // prioridade
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) }); // vencimento
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) }); // status
        return g;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DETAIL PANEL
    // ══════════════════════════════════════════════════════════════════════════

    private void SelectRow(TodoItem item, Border row)
    {
        // Reset previous
        foreach (var (_, r) in _rowMap)
            r.Background = new SolidColorBrush(CSurface);

        _selected = item;
        row.Background = new SolidColorBrush(CRowSel);

        if (_detailPanel == null || _detailContent == null) return;
        _detailPanel.Visibility = Visibility.Visible;
        _detailContent.Children.Clear();

        // Detail header
        var hdr = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12),
        };
        var hdrGrid = new Grid();
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdr.Child = hdrGrid;

        hdrGrid.Children.Add(new TextBlock
        {
            Text = "Detalhes da Tarefa",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(actions, 1);
        hdrGrid.Children.Add(actions);

        var btnEdit = LinkBtn("✎ Editar");
        btnEdit.Click += (_, _) => { _vm.OpenEditCommand.Execute(item); ShowForm(); };
        actions.Children.Add(btnEdit);

        var btnDone = LinkBtn(item.Done ? "↩ Reabrir" : "✓ Concluir", item.Done ? CTextSub : CGreen);
        btnDone.Click += (_, _) => { _vm.ToggleCommand.Execute(item); Refresh(); };
        actions.Children.Add(btnDone);

        var btnDel = LinkBtn("🗑 Excluir", CRed);
        btnDel.Click += (_, _) =>
        {
            _vm.DeleteCommand.Execute(item);
            _selected = null;
            if (_detailPanel != null) _detailPanel.Visibility = Visibility.Collapsed;
            Refresh();
        };
        actions.Children.Add(btnDel);

        // Fechar painel
        var btnClose = new Button
        {
            Content = "✕",
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(CTextSub),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Fechar detalhes",
        };
        btnClose.MouseEnter += (_, _) => btnClose.Foreground = new SolidColorBrush(CText);
        btnClose.MouseLeave += (_, _) => btnClose.Foreground = new SolidColorBrush(CTextSub);
        btnClose.Click += (_, _) =>
        {
            _selected = null;
            foreach (var (_, r) in _rowMap)
                r.Background = new SolidColorBrush(CSurface);
            if (_detailPanel != null) _detailPanel.Visibility = Visibility.Collapsed;
        };
        actions.Children.Add(btnClose);

        _detailContent.Children.Add(hdr);

        // Fields
        var body = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        _detailContent.Children.Add(body);

        void Row(string label, UIElement value)
        {
            var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new SolidColorBrush(CTextSub),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0),
            });
            Grid.SetColumn(value, 1);
            rowGrid.Children.Add(value);
            body.Children.Add(rowGrid);
        }

        Row("Título", new TextBlock
        {
            Text = item.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
            TextWrapping = TextWrapping.Wrap,
        });

        if (!string.IsNullOrWhiteSpace(item.Description))
            Row("Descrição", new TextBlock
            {
                Text = item.Description,
                FontSize = 11,
                Foreground = new SolidColorBrush(CTextSub),
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrEmpty(item.TicketId))
            Row("Relacionado a", new TextBlock
            {
                Text = item.TicketId,
                FontSize = 11,
                Foreground = new SolidColorBrush(CAccent),
                TextDecorations = TextDecorations.Underline,
                Cursor = Cursors.Hand,
            });

        Row("Tipo", new TextBlock
        {
            Text = item.Category,
            FontSize = 11,
            Foreground = new SolidColorBrush(CText),
        });

        var (priFg, priBg, priLabel) = item.Priority switch
        {
            1 => ("#FCA5A5", "#3B0C0C", "Alta"),
            3 => ("#86EFAC", "#0A2010", "Baixa"),
            _ => ("#FCD34D", "#3B2A00", "Média"),
        };
        Row("Prioridade", LightBadge(priLabel, priFg, priBg));

        var (stFg, stBg, stLabel) = item.Done
            ? ("#86EFAC", "#0A2010", "Concluída")
            : item.IsOverdue ? ("#FCA5A5", "#3B0C0C", "Atrasada")
            : ("#94A3B8", "#1E2A38", "Pendente");
        Row("Status", LightBadge(stLabel, stFg, stBg));

        if (item.DueDate.HasValue)
        {
            var dueFg = item.IsOverdue ? CRed : item.IsDueToday ? CYellow : CText;
            Row("Vencimento", new TextBlock
            {
                Text = $"📅 {item.DueDate.Value:dd/MM/yyyy HH:mm}",
                FontSize = 11,
                Foreground = new SolidColorBrush(dueFg),
            });
        }

        Row("Criado em", new TextBlock
        {
            Text = $"🕐 {item.CreatedAt:dd/MM/yyyy HH:mm}",
            FontSize = 11,
            Foreground = new SolidColorBrush(CTextSub),
        });

        if (item.Done && item.DoneAt.HasValue)
            Row("Concluído em", new TextBlock
            {
                Text = $"✓ {item.DoneAt.Value:dd/MM/yyyy HH:mm}",
                FontSize = 11,
                Foreground = new SolidColorBrush(CGreen),
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FORM OVERLAY
    // ══════════════════════════════════════════════════════════════════════════

    private Border BuildFormOverlay()
    {
        var overlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x99, 0x00, 0x00, 0x00)),
            Visibility = Visibility.Collapsed,
        };
        overlay.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == overlay) CloseForm(); };

        var panel = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Width = 520,
            MaxHeight = 600,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        overlay.Child = panel;

        var dock = new DockPanel();
        panel.Child = dock;

        // Header
        var fHdr = new Border
        {
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(22, 16, 22, 16),
        };
        var fHdrG = new Grid();
        fHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fHdr.Child = fHdrG;

        var formTitleTb = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(CText),
        };
        formTitleTb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("IsEditing")
        {
            Source = _vm,
            Converter = new BoolToStringConverter("Editar Tarefa", "Nova Tarefa"),
        });
        Grid.SetColumn(formTitleTb, 0);
        fHdrG.Children.Add(formTitleTb);

        var btnX = new Button
        {
            Content = "✕",
            FontSize = 14,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(CTextSub),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6),
        };
        btnX.Click += (_, _) => CloseForm();
        Grid.SetColumn(btnX, 1);
        fHdrG.Children.Add(btnX);
        DockPanel.SetDock(fHdr, Dock.Top);
        dock.Children.Add(fHdr);

        // Footer
        var fFtr = new Border
        {
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(22, 14, 22, 14),
            Background = new SolidColorBrush(CSurface2),
        };
        var fFtrRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        fFtr.Child = fFtrRow;

        _formErrorTb = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(CRed),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Visibility = Visibility.Collapsed,
        };
        fFtrRow.Children.Add(_formErrorTb);

        var btnCancel = OutlineBtn("Cancelar");
        btnCancel.Click += (_, _) => CloseForm();
        fFtrRow.Children.Add(btnCancel);

        var btnSave = PrimaryBtn("Salvar Tarefa");
        btnSave.Margin = new Thickness(8, 0, 0, 0);
        btnSave.Click += (_, _) => SubmitForm();
        fFtrRow.Children.Add(btnSave);

        DockPanel.SetDock(fFtr, Dock.Bottom);
        dock.Children.Add(fFtr);

        // Body
        var bodyScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var body = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        bodyScroll.Content = body;
        dock.Children.Add(bodyScroll);

        // Fields
        body.Children.Add(FLabel("Título *"));
        _formTitleBox = FInput();
        body.Children.Add(_formTitleBox);

        body.Children.Add(FLabel("Descrição"));
        _formDescBox = new TextBox
        {
            Background = new SolidColorBrush(CBg),
            Foreground = new SolidColorBrush(CText),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 16),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Height = 72,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        body.Children.Add(_formDescBox);

        // Row: cat + pri
        var r1 = TwoCol();
        body.Children.Add(r1.grid);

        var catCol = new StackPanel();
        catCol.Children.Add(FLabel("Categoria"));
        var (catHost, getCat, setCat, setCatItems) = DarkDropdown();
        setCatItems(["Geral", "Chamado", "Reunião", "Follow-up", "Documentação", "Outro"]);
        _getCat = getCat; _setCat = setCat;
        catCol.Children.Add(catHost);
        Grid.SetColumn(catCol, 0); r1.grid.Children.Add(catCol);

        var priCol = new StackPanel();
        priCol.Children.Add(FLabel("Prioridade"));
        var (priHost, getPri, setPri, setPriItems) = DarkDropdown();
        setPriItems(["Alta", "Média", "Baixa"]);
        setPri("Média");
        _getPri = getPri; _setPri = setPri;
        priCol.Children.Add(priHost);
        Grid.SetColumn(priCol, 2); r1.grid.Children.Add(priCol);

        // Row: due + ticket
        var r2 = TwoCol();
        r2.grid.Margin = new Thickness(0, 0, 0, 4);
        body.Children.Add(r2.grid);

        var dueCol = new StackPanel();
        dueCol.Children.Add(FLabel("Vencimento"));
        _formDuePicker = new DatePicker
        {
            Background = new SolidColorBrush(CBg),
            Foreground = new SolidColorBrush(CText),
            BorderBrush = new SolidColorBrush(CBorder),
            FontSize = 12,
            SelectedDateFormat = DatePickerFormat.Short,
        };
        dueCol.Children.Add(_formDuePicker);
        Grid.SetColumn(dueCol, 0); r2.grid.Children.Add(dueCol);

        var tickCol = new StackPanel();
        tickCol.Children.Add(FLabel("Chamado (opcional)"));
        _formTicketBox = FInput("Ex: CAS-12345");
        _formTicketBox.Margin = new Thickness(0);
        tickCol.Children.Add(_formTicketBox);
        Grid.SetColumn(tickCol, 2); r2.grid.Children.Add(tickCol);

        return overlay;
    }

    private (Grid grid, ColumnDefinition c1, ColumnDefinition c2) TwoCol()
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        var c1 = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        var gap = new ColumnDefinition { Width = new GridLength(14) };
        var c2 = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        g.ColumnDefinitions.Add(c1);
        g.ColumnDefinitions.Add(gap);
        g.ColumnDefinitions.Add(c2);
        return (g, c1, c2);
    }

    private void ShowForm()
    {
        if (_formOverlay == null) return;
        _formOverlay.Visibility = Visibility.Visible;

        if (_formTitleBox != null) _formTitleBox.Text = _vm.FormTitle;
        if (_formDescBox != null) _formDescBox.Text = _vm.FormDescription;
        if (_formTicketBox != null) _formTicketBox.Text = _vm.FormTicketId ?? "";
        if (_formDuePicker != null) _formDuePicker.SelectedDate = _vm.FormDueDate;

        _setCat?.Invoke(_vm.FormCategory);
        _setPri?.Invoke(_vm.FormPriority switch { 1 => "Alta", 3 => "Baixa", _ => "Média" });

        if (_formErrorTb != null) { _formErrorTb.Text = ""; _formErrorTb.Visibility = Visibility.Collapsed; }
        _formTitleBox?.Focus();
    }

    private void CloseForm()
    {
        if (_formOverlay != null) _formOverlay.Visibility = Visibility.Collapsed;
        _vm.CloseFormCommand.Execute(null);
    }

    private void SubmitForm()
    {
        _vm.FormTitle = _formTitleBox?.Text ?? "";
        _vm.FormDescription = _formDescBox?.Text ?? "";
        _vm.FormCategory = _getCat?.Invoke() ?? "Geral";
        _vm.FormPriority = (_getPri?.Invoke() ?? "Média") switch { "Alta" => 1, "Baixa" => 3, _ => 2 };
        _vm.FormDueDate = _formDuePicker?.SelectedDate;
        _vm.FormTicketId = _formTicketBox?.Text;
        _vm.SaveCommand.Execute(null);

        if (!_vm.FormVisible)
        {
            CloseForm();
            Refresh();
        }
        else if (!string.IsNullOrEmpty(_vm.FormError) && _formErrorTb != null)
        {
            _formErrorTb.Text = _vm.FormError;
            _formErrorTb.Visibility = Visibility.Visible;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NOTIFICATIONS
    // ══════════════════════════════════════════════════════════════════════════

    private void CheckNotifications()
    {
        try
        {
            var overdues = _vm.Items.Where(t => t.IsOverdue).ToList();
            var dueTodays = _vm.Items.Where(t => t.IsDueToday && !t.IsOverdue).ToList();

            if (overdues.Any())
            {
                var title = overdues.Count == 1
                    ? $"⚠ Tarefa atrasada: {overdues[0].Title}"
                    : $"⚠ {overdues.Count} tarefas atrasadas";
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(string.Join("\n", overdues.Take(3).Select(t => $"• {t.Title}")))
                    .Show();
            }
            else if (dueTodays.Any())
            {
                var title = dueTodays.Count == 1
                    ? $"📅 Vence hoje: {dueTodays[0].Title}"
                    : $"📅 {dueTodays.Count} tarefas vencem hoje";
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(string.Join("\n", dueTodays.Take(3).Select(t => $"• {t.Title}")))
                    .Show();
            }
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static (Border host, Func<string?> getValue, Action<string> setValue, Action<List<string>> setItems)
        DarkDropdown(bool light = false)
    {
        var bgColor = light ? CSurface : Color.FromRgb(0x14, 0x1C, 0x28);
        var bgHov = light ? Color.FromRgb(0xF3, 0xF4, 0xF6) : Color.FromRgb(0x1A, 0x22, 0x30);
        var fgColor = light ? CText : Color.FromRgb(0xE2, 0xE8, 0xF0);
        var borderCol = light ? CBorder : Color.FromRgb(0x1E, 0x28, 0x38);
        var dropBg = light ? CSurface : Color.FromRgb(0x0F, 0x15, 0x20);
        var itemHov = light ? Color.FromRgb(0xEF, 0xF0, 0xFF) : Color.FromArgb(0x18, 0xA7, 0x8B, 0xFA);
        var itemSel = light ? Color.FromRgb(0xEB, 0xF0, 0xFF) : Color.FromArgb(0x30, 0xA7, 0x8B, 0xFA);
        var itemSelFg = light ? CAccent : Color.FromRgb(0xA7, 0x8B, 0xFA);

        var items = new List<string>();
        var selectedIdx = 0;
        Popup? popup = null;

        var selectedTb = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(fgColor),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var arrow = new TextBlock
        {
            Text = "⌄",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var hg = new Grid();
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hg.Children.Add(selectedTb); Grid.SetColumn(selectedTb, 0);
        hg.Children.Add(arrow); Grid.SetColumn(arrow, 1);

        var host = new Border
        {
            Background = new SolidColorBrush(bgColor),
            BorderBrush = new SolidColorBrush(borderCol),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 34,
            Cursor = Cursors.Hand,
            Child = hg,
        };

        host.MouseEnter += (_, _) => host.Background = new SolidColorBrush(bgHov);
        host.MouseLeave += (_, _) => host.Background = new SolidColorBrush(bgColor);

        void RebuildPopup()
        {
            popup?.Let(p => p.IsOpen = false);
            var list = new StackPanel { Background = new SolidColorBrush(dropBg) };

            for (int i = 0; i < items.Count; i++)
            {
                var idx = i; var item = items[i];
                var row = new Border
                {
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = idx == selectedIdx ? new SolidColorBrush(itemSel) : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = item,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(idx == selectedIdx ? itemSelFg : fgColor),
                    },
                };
                row.MouseEnter += (_, _) => { if (idx != selectedIdx) row.Background = new SolidColorBrush(itemHov); };
                row.MouseLeave += (_, _) => { row.Background = idx == selectedIdx ? new SolidColorBrush(itemSel) : Brushes.Transparent; };
                row.MouseLeftButtonUp += (_, _) =>
                {
                    selectedIdx = idx; selectedTb.Text = item;
                    popup?.Let(p => p.IsOpen = false);
                };
                list.Children.Add(row);
                if (i < items.Count - 1)
                    list.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(borderCol) });
            }

            popup = new Popup
            {
                Child = new Border
                {
                    Child = list,
                    Background = new SolidColorBrush(dropBg),
                    BorderBrush = new SolidColorBrush(borderCol),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
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
            var p = popup;
            p.Opened += (_, _) => { host.BorderBrush = new SolidColorBrush(CAccent); arrow.Text = "⌃"; };
            p.Closed += (_, _) => { host.BorderBrush = new SolidColorBrush(borderCol); arrow.Text = "⌄"; };
        }

        host.MouseLeftButtonUp += (_, _) => { RebuildPopup(); popup!.IsOpen = !popup.IsOpen; };

        string? getValue() => selectedIdx < items.Count ? items[selectedIdx] : null;
        void setValue(string val) { var i = items.IndexOf(val); if (i >= 0) { selectedIdx = i; selectedTb.Text = items[i]; } }
        void setItems(List<string> n) { items.Clear(); items.AddRange(n); if (n.Count > 0) { selectedIdx = 0; selectedTb.Text = n[0]; } }

        return (host, getValue, setValue, setItems);
    }

    private static Button PrimaryBtn(string label) => new()
    {
        Content = label,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Background = new SolidColorBrush(CPurple),
        Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x08, 0x20)),
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Padding = new Thickness(16, 8, 16, 8),
    };

    private static Button OutlineBtn(string label) => new()
    {
        Content = label,
        FontSize = 12,
        Background = new SolidColorBrush(CSurface2),
        Foreground = new SolidColorBrush(CText),
        BorderBrush = new SolidColorBrush(CBorder2),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(14, 7, 14, 7),
    };

    private static Button LinkBtn(string label, Color? color = null) => new()
    {
        Content = label,
        FontSize = 11,
        Background = Brushes.Transparent,
        Foreground = new SolidColorBrush(color ?? CAccent),
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Padding = new Thickness(6, 4, 6, 4),
        Margin = new Thickness(2, 0, 2, 0),
    };

    private static Border LightBadge(string text, string fgHex, string bgHex) => new()
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
        }
    };

    private static TextBox FInput(string placeholder = "") => new()
    {
        Background = new SolidColorBrush(CBg),
        Foreground = new SolidColorBrush(CText),
        BorderBrush = new SolidColorBrush(CBorder),
        BorderThickness = new Thickness(1),
        FontSize = 12,
        Padding = new Thickness(10, 8, 10, 8),
        Margin = new Thickness(0, 0, 0, 16),
    };

    private static TextBlock FLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(CTextSub),
        Margin = new Thickness(0, 0, 0, 5),
    };
}

// ── Extension helpers ─────────────────────────────────────────────────────────

internal static class PopupExt
{
    public static void Let<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj != null) action(obj);
    }
}

// ── Converter ─────────────────────────────────────────────────────────────────

public class BoolToStringConverter : System.Windows.Data.IValueConverter
{
    private readonly string _true, _false;
    public BoolToStringConverter(string whenTrue, string whenFalse)
        => (_true, _false) = (whenTrue, whenFalse);
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
        => v is true ? _true : _false;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotImplementedException();
}