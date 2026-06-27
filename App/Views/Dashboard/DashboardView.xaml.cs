// =============================================================================
//  DashboardView.xaml.cs — Orquestrador do Dashboard (refatorado)
// =============================================================================
// Responsabilidade deste arquivo: montar o layout raiz e coordenar os
// sub-builders. NENHUMA lógica de cor, badge ou mapeamento de domínio aqui.
//
// Estrutura de arquivos do namespace:
//   Theme/
//     DashboardTheme.cs          — paleta de cores
//   Helpers/
//     IncidentDisplayMappers.cs  — mapeamento domínio → exibição (puro)
//   Components/
//     UiFactory.cs               — primitivos de UI reutilizáveis
//     TableColumnFactory.cs      — definição de colunas da tabela
//   Sections/
//     IncidentRowBuilder.cs      — construção de linha da tabela
//     DetailPanelBuilder.cs      — painel lateral de detalhes
//   DashboardView.xaml.cs        — ← você está aqui (orquestrador)
// =============================================================================

using D365Assistant.Core.Models.Incident;
using D365Assistant.Core.Services;
using D365Assistant.ViewModels;
using D365Assistant.Views.Dashboard.Components;
using D365Assistant.Views.Dashboard.Helpers;
using D365Assistant.Views.Dashboard.Sections;
using D365Assistant.Views.Dashboard.Theme;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

public partial class DashboardView : Page
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly DashboardViewModel _vm;
    private readonly MainWindow _mainWindow;

    // ── UI refs ───────────────────────────────────────────────────────────────
    private StackPanel? _tableBody;
    private Border? _detailPanel;
    private StackPanel? _detailContent;
    private TextBlock? _totalTb;
    private TextBlock? _pageTb;
    private TextBlock? _lastUpdTb;
    private Border? _hdrBorder;

    // ── Filters state ─────────────────────────────────────────────────────────
    private TextBox? _searchBox;
    private Action<string>? _setStatusFilter;
    private Action<string>? _setPriFilter;
    private Func<string?>? _getSort;

    // ── Row selection state ───────────────────────────────────────────────────
    private IncidentSnapshot? _selected;
    private readonly Dictionary<string, Border> _rowMap = [];

    // ── Tab / pagination state ────────────────────────────────────────────────
    private readonly Dictionary<string, Button> _tabBtns = [];
    private string _activeTab = TabLabels.All;
    private int _page = 1;

    private const int PageSize = 25;

    // ── Tab labels ────────────────────────────────────────────────────────────
    private static class TabLabels
    {
        public const string All = "Todos os Chamados";
        public const string InProgress = "Em Atendimento";
        public const string Waiting = "Aguardando Cliente";
        public const string ThirdParty = "Aguardando Terceiros";
        public const string Resolved = "Resolvidos";
        public const string Cancelled = "Cancelados";

        public static readonly string[] All_Tabs =
        [
            All, InProgress, Waiting, ThirdParty, Resolved, Cancelled
        ];
    }

    // ── Sub-builders (created after UI is ready) ──────────────────────────────
    private DetailPanelBuilder? _detailBuilder;

    // ══════════════════════════════════════════════════════════════════════════
    //  CONSTRUCTOR
    // ══════════════════════════════════════════════════════════════════════════

    public DashboardView(DashboardViewModel vm, TrackerViewModel trackerVm, MainWindow mainWindow)
    {
        InitializeComponent();
        _vm = vm;
        _mainWindow = mainWindow;

        ((Grid)Content).Children.Add(BuildRoot());

        _detailBuilder = new DetailPanelBuilder(
            onStartTimer: (ticket, title) => mainWindow.QuickStartTimer(ticket, title),
            onClose: _ => CloseDetailPanel(),
            storage: App.Services.GetRequiredService<StorageService>());

        SubscribeToViewModel();
        RenderTable();
    }

    private void SubscribeToViewModel()
    {
        void OnCollectionChanged(object? s, NotifyCollectionChangedEventArgs e)
            => Dispatcher.Invoke(RenderTable);

        void OnPropertyChanged(object? s, PropertyChangedEventArgs e)
            => Dispatcher.Invoke(SyncLastUpdated);

        _vm.Incidents.CollectionChanged += OnCollectionChanged;
        _vm.PropertyChanged += OnPropertyChanged;

        this.Unloaded += (_, _) =>
        {
            _vm.Incidents.CollectionChanged -= OnCollectionChanged;
            _vm.PropertyChanged -= OnPropertyChanged;
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ROOT LAYOUT
    // ══════════════════════════════════════════════════════════════════════════

    private UIElement BuildRoot()
    {
        var root = new Grid { Background = DashboardTheme.Brush(DashboardTheme.Bg) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        AddRow(root, BuildTopBar(), 0);
        AddRow(root, BuildTabs(), 1);
        AddRow(root, BuildContent(), 2);

        return root;
    }

    // ── Top bar ───────────────────────────────────────────────────────────────

    private UIElement BuildTopBar()
    {
        var bar = SurfaceBar(bottomBorder: true, padding: new Thickness(24, 14, 24, 14));
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = g;

        var left = BuildTopBarLeft();
        var right = BuildTopBarRight();

        AddCol(g, left, 0);
        AddCol(g, right, 1);

        return bar;
    }

    private UIElement BuildTopBarLeft()
    {
        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = "Chamados",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        });
        left.Children.Add(new TextBlock
        {
            Text = "Gerencie e acompanhe os chamados de suporte",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        });
        return left;
    }

    private UIElement BuildTopBarRight()
    {
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _lastUpdTb = new TextBlock
        {
            FontSize = 10,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            Text = _vm.LastUpdated,
        };
        right.Children.Add(_lastUpdTb);

        var btnRefresh = UiFactory.OutlineButton("↻  Atualizar");
        btnRefresh.Click += (_, _) =>
            App.Services.GetRequiredService<MainViewModel>().RefreshCommand.Execute(null);
        right.Children.Add(btnRefresh);

        return right;
    }

    private void SyncLastUpdated()
    {
        if (_lastUpdTb != null) _lastUpdTb.Text = _vm.LastUpdated;
    }

    // ── Tabs ──────────────────────────────────────────────────────────────────

    private UIElement BuildTabs()
    {
        var bar = SurfaceBar(bottomBorder: true, padding: new Thickness(20, 0, 20, 0));
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Child = tabs;

        foreach (var label in TabLabels.All_Tabs)
        {
            var btn = BuildTabButton(label);
            btn.Click += (_, _) => ActivateTab(label);
            _tabBtns[label] = btn;
            tabs.Children.Add(btn);
        }

        HighlightTab(TabLabels.All);
        return bar;
    }

    private void ActivateTab(string label)
    {
        _activeTab = label;
        _vm.ActiveTab = label;
        _page = 1;
        HighlightTab(label);
        RenderTable();
    }

    private static Button BuildTabButton(string label) => new()
    {
        Content = label,
        FontSize = 12,
        Background = Brushes.Transparent,
        Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        BorderThickness = new Thickness(0, 0, 0, 2),
        BorderBrush = Brushes.Transparent,
        Cursor = Cursors.Hand,
        Padding = new Thickness(4, 12, 4, 12),
        Margin = new Thickness(0, 0, 20, 0),
    };

    private void HighlightTab(string active)
    {
        foreach (var (name, btn) in _tabBtns)
        {
            var isActive = name == active;
            btn.Foreground = isActive
                ? DashboardTheme.Brush(DashboardTheme.Accent)
                : DashboardTheme.Brush(DashboardTheme.TextSub);
            btn.BorderBrush = isActive
                ? DashboardTheme.Brush(DashboardTheme.Accent)
                : Brushes.Transparent;
        }
    }

    // ── Content ───────────────────────────────────────────────────────────────

    private UIElement BuildContent()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AddCol(grid, BuildFilters(), 0);
        AddCol(grid, BuildTableArea(), 1);
        AddCol(grid, BuildDetailPanel(), 2);

        return grid;
    }

    // ── Filters ───────────────────────────────────────────────────────────────

    private UIElement BuildFilters()
    {
        var sidebar = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 1, 0),
        };
        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(14, 16, 14, 16),
        };
        sidebar.Child = sv;
        sv.Content = BuildFilterStack();
        return sidebar;
    }

    private StackPanel BuildFilterStack()
    {
        var stack = new StackPanel();

        stack.Children.Add(BuildFilterHeader());
        stack.Children.Add(BuildSearchBox());

        // Status filter
        stack.Children.Add(UiFactory.FilterLabel("Status"));
        var (stHost, _, setStVal, setStItems) = UiFactory.Dropdown(val =>
        {
            _vm.StatusFilter = val == "Todos" ? "Todos" : val;
            _page = 1;
            RenderTable();
        });
        setStItems(["Todos", "Em Atendimento", "Aguardando Fila",
                    "Aguard. Cliente", "Impeditivo", "Resolvido", "Cancelado"]);
        _setStatusFilter = setStVal;
        stHost.Margin = new Thickness(0, 0, 0, 14);
        stack.Children.Add(stHost);

        // Priority filter
        stack.Children.Add(UiFactory.FilterLabel("Prioridade"));
        var (priHost, _, setPriVal, setPriItems) = UiFactory.Dropdown(val =>
        {
            _vm.PriFilter = val == "Todas" ? "Todos" : val;
            _page = 1;
            RenderTable();
        });
        setPriItems(["Todas", "Urgente", "Alto", "Normal", "Baixa"]);
        _setPriFilter = setPriVal;
        priHost.Margin = new Thickness(0, 0, 0, 14);
        stack.Children.Add(priHost);

        stack.Children.Add(UiFactory.HorizontalDivider());
        stack.Children.Add(BuildSavedFilters());

        return stack;
    }

    private UIElement BuildFilterHeader()
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        g.Children.Add(new TextBlock
        {
            Text = "Filtros",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        });

        var btnClear = UiFactory.GhostButton("Limpar");
        btnClear.Click += (_, _) => ClearFilters();
        Grid.SetColumn(btnClear, 1);
        g.Children.Add(btnClear);

        return g;
    }

    private UIElement BuildSearchBox()
    {
        var border = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Bg),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 0, 8, 0),
            Height = 32,
            Margin = new Thickness(0, 0, 0, 14),
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(new TextBlock
        {
            Text = "⌕",
            FontSize = 12,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            Margin = new Thickness(0, 0, 5, 0),
        });

        var box = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            FontSize = 11,
            Width = 130,
        };
        _searchBox = box;
        box.TextChanged += (_, _) => { _vm.SearchText = box.Text; _page = 1; };
        row.Children.Add(box);

        border.Child = row;
        return border;
    }

    private UIElement BuildSavedFilters()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "FILTROS SALVOS",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(Color.FromRgb(0x37, 0x41, 0x51)),
            Margin = new Thickness(0, 0, 0, 8),
        });

        var savedFilters = new (string Label, Action Action)[]
        {
            ("Meus chamados abertos",  () => { _vm.StatusFilter = "Em Atendimento"; RenderTable(); }),
            ("Chamados críticos",      () => { _vm.PriFilter = "Urgente"; RenderTable(); }),
            ("Aguardando retorno",     () => { _vm.StatusFilter = "Aguard. Cliente"; RenderTable(); }),
            ("Em atendimento hoje",    () => { _vm.StatusFilter = "Em Atendimento"; RenderTable(); }),
        };

        foreach (var (label, action) in savedFilters)
        {
            var act = action;
            var btn = new Button
            {
                Content = label,
                FontSize = 11,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0, 6, 0, 6),
            };
            btn.MouseEnter += (_, _) => btn.Foreground = DashboardTheme.Brush(DashboardTheme.Accent);
            btn.MouseLeave += (_, _) => btn.Foreground = DashboardTheme.Brush(DashboardTheme.TextSub);
            btn.Click += (_, _) => act();
            stack.Children.Add(btn);
        }

        return stack;
    }

    private void ClearFilters()
    {
        _vm.SearchText = "";
        _vm.PriFilter = "Todos";
        _vm.StatusFilter = "Todos";
        if (_searchBox != null) _searchBox.Text = "";
        _setStatusFilter?.Invoke("Todos");
        _setPriFilter?.Invoke("Todas");
        _page = 1;
        RenderTable();
    }

    // ── Table area ────────────────────────────────────────────────────────────

    private UIElement BuildTableArea()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddRow(root, BuildTableToolbar(), 0);
        AddRow(root, BuildColumnHeaders(), 1);
        AddRow(root, BuildTableBodyScroll(), 2);
        AddRow(root, BuildPaginationFooter(), 3);

        return root;
    }

    private UIElement BuildTableToolbar()
    {
        var bar = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 8, 16, 8),
        };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = g;

        _totalTb = new TextBlock
        {
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_totalTb, 0);
        g.Children.Add(_totalTb);

        var sortRow = BuildSortRow();
        Grid.SetColumn(sortRow, 1);
        g.Children.Add(sortRow);

        return bar;
    }

    private UIElement BuildSortRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        row.Children.Add(new TextBlock
        {
            Text = "Ordenar por:",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var (sortHost, getSort, _, setSortItems) = UiFactory.Dropdown(_ =>
        {
            _page = 1;
            RenderTable();
        });
        setSortItems([
            "Data de Abertura (Mais recente)",
            "Data de Abertura (Mais antiga)",
            "Status",
            "Prioridade",
            "Cliente",
            "Tempo parado",
        ]);
        sortHost.Width = 200;
        _getSort = getSort;
        row.Children.Add(sortHost);

        return row;
    }

    private UIElement BuildColumnHeaders()
    {
        _hdrBorder = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        RebuildColumnHeaders();
        return _hdrBorder;
    }

    private void RebuildColumnHeaders()
    {
        if (_hdrBorder == null) return;

        var showSatisfaction = _activeTab == TabLabels.Resolved;
        var g = TableColumnFactory.Create(showSatisfaction);

        AddHeaderCell(g, TableColumnFactory.ColId, "ID");
        AddHeaderCell(g, TableColumnFactory.ColSubject, "Assunto");
        AddHeaderCell(g, TableColumnFactory.ColCustomer, "Cliente");
        AddHeaderCell(g, TableColumnFactory.ColStatus, "Status");
        AddHeaderCell(g, TableColumnFactory.ColPriority, "Prioridade");
        AddHeaderCell(g, TableColumnFactory.ColDate, "Aberto em");
        AddHeaderCell(g, TableColumnFactory.ColSla, "SLA 1º Atend.");
        if (showSatisfaction)
            AddHeaderCell(g, TableColumnFactory.ColSatisf, "Satisfação");

        _hdrBorder.Child = g;
    }

    private static void AddHeaderCell(Grid g, int col, string text, bool leftMargin = false)
    {
        // Margens espelham exatamente as das células de dados para alinhamento perfeito
        var margin = col switch
        {
            TableColumnFactory.ColId => new Thickness(16, 9, 12, 9),
            TableColumnFactory.ColSubject => new Thickness(0, 9, 16, 9),
            TableColumnFactory.ColSla => new Thickness(0, 9, 8, 9),
            _ => new Thickness(0, 9, 12, 9),
        };

        var tb = new TextBlock
        {
            Text = text,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin,
        };
        Grid.SetColumn(tb, col);
        g.Children.Add(tb);
    }

    private UIElement BuildTableBodyScroll()
    {
        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = DashboardTheme.Brush(DashboardTheme.Bg),
        };
        sv.PreviewMouseWheel += (_, e) =>
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        };
        _tableBody = new StackPanel();
        sv.Content = _tableBody;
        return sv;
    }

    private UIElement BuildPaginationFooter()
    {
        var bar = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 8, 16, 8),
        };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = g;

        _pageTb = new TextBlock
        {
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_pageTb, 0);
        g.Children.Add(_pageTb);

        var pageButtons = BuildPageButtons();
        Grid.SetColumn(pageButtons, 1);
        g.Children.Add(pageButtons);

        return bar;
    }

    private UIElement BuildPageButtons()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        var bFirst = UiFactory.PageButton("«"); bFirst.Click += (_, _) => { _page = 1; RenderTable(); };
        var bPrev = UiFactory.PageButton("‹"); bPrev.Click += (_, _) => { if (_page > 1) { _page--; RenderTable(); } };
        var bNext = UiFactory.PageButton("›"); bNext.Click += (_, _) => { _page++; RenderTable(); };
        var bLast = UiFactory.PageButton("»"); bLast.Click += (_, _) => { _page = 9999; RenderTable(); };

        row.Children.Add(bFirst);
        row.Children.Add(bPrev);

        for (int i = 1; i <= 5; i++)
        {
            var pg = i;
            var pb = UiFactory.PageButton(i.ToString());
            pb.Click += (_, _) => { _page = pg; RenderTable(); };
            row.Children.Add(pb);
        }

        row.Children.Add(bNext);
        row.Children.Add(bLast);

        return row;
    }

    // ── Detail panel ──────────────────────────────────────────────────────────

    private UIElement BuildDetailPanel()
    {
        _detailPanel = new Border
        {
            Width = 380,
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _detailContent = new StackPanel();
        sv.Content = _detailContent;
        _detailPanel.Child = sv;

        return _detailPanel;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  RENDER TABLE
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderTable()
    {
        if (_tableBody == null) return;

        _tableBody.Children.Clear();
        _rowMap.Clear();
        RebuildColumnHeaders();

        var (paged, total, pages) = GetPagedIncidents();

        UpdateCounterLabels(total, paged);

        if (total == 0)
        {
            _tableBody.Children.Add(EmptyStateText());
            return;
        }

        var showSatisfaction = _activeTab == TabLabels.Resolved;
        var rowBuilder = new IncidentRowBuilder(
            showSatisfaction: showSatisfaction,
            onSelect: SelectRow,
            contextMenuFactory: BuildContextMenu);

        foreach (var snap in paged)
        {
            var row = rowBuilder.Build(snap);
            _rowMap[snap.TicketNumber] = row;
            _tableBody.Children.Add(row);
        }

        // Restore selection highlight
        if (_selected != null && _rowMap.TryGetValue(_selected.TicketNumber, out var sel))
            sel.Background = DashboardTheme.Brush(DashboardTheme.RowSelected);
    }

    /// <summary>Applies sorting, paging and returns the slice.</summary>
    private (List<IncidentSnapshot> paged, int total, int pages) GetPagedIncidents()
    {
        var items = _vm.Incidents.ToList();
        items = ApplySort(items, _getSort?.Invoke());

        var total = items.Count;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        _page = Math.Clamp(_page, 1, pages);

        return (items.Skip((_page - 1) * PageSize).Take(PageSize).ToList(), total, pages);
    }

    private static List<IncidentSnapshot> ApplySort(
        List<IncidentSnapshot> items, string? key) => key switch
        {
            "Prioridade" => items.OrderBy(s => IncidentDisplayMappers.PrioritySortOrder(s.PriorityCode))
                                  .ThenByDescending(s => s.CreatedOn).ToList(),
            "Status" => items.OrderBy(s => IncidentDisplayMappers.StatusSortOrder(s.StatusCode))
                                  .ThenByDescending(s => s.CreatedOn).ToList(),
            "Cliente" => items.OrderBy(s => s.CustomerDisplayName)
                                  .ThenByDescending(s => s.CreatedOn).ToList(),
            "Tempo parado" => items.OrderByDescending(s => s.HoursSinceModified).ToList(),
            "Data de Abertura (Mais antiga)" => items.OrderBy(s => s.CreatedOn).ToList(),
            _ => items.OrderByDescending(s => s.CreatedOn).ToList(),
        };

    private void UpdateCounterLabels(int total, List<IncidentSnapshot> paged)
    {
        var s = total != 1 ? "s" : "";
        if (_totalTb != null)
            _totalTb.Text = $"{total} chamado{s} encontrado{s}";

        if (_pageTb != null)
            _pageTb.Text =
                $"{(_page - 1) * PageSize + 1} - {Math.Min(_page * PageSize, total)} de {total}";
    }

    private static TextBlock EmptyStateText() => new()
    {
        Text = "Nenhum chamado encontrado.",
        FontSize = 13,
        Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 48, 0, 0),
    };

    // ── Row selection ─────────────────────────────────────────────────────────

    private void SelectRow(IncidentSnapshot snap, Border row)
    {
        // Clear previous selection
        foreach (var (_, r) in _rowMap)
        {
            r.Background = DashboardTheme.Brush(DashboardTheme.Surface);
            r.Tag = null;
        }

        _selected = snap;
        row.Background = DashboardTheme.Brush(DashboardTheme.RowSelected);
        row.Tag = "selected";

        if (_detailPanel == null || _detailContent == null || _detailBuilder == null) return;

        _detailPanel.Visibility = Visibility.Visible;
        _detailContent.Children.Clear();
        _detailBuilder.Populate(_detailContent, snap);
    }

    private void CloseDetailPanel()
    {
        _selected = null;
        foreach (var (_, r) in _rowMap)
        {
            r.Background = DashboardTheme.Brush(DashboardTheme.Surface);
            r.Tag = null;
        }
        if (_detailPanel != null) _detailPanel.Visibility = Visibility.Collapsed;
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private ContextMenu BuildContextMenu(IncidentSnapshot snap)
    {
        var menu = new ContextMenu();

        var mTimer = new MenuItem { Header = $"▶ Timer — {snap.TicketNumber}" };
        mTimer.Click += (_, _) => _mainWindow.QuickStartTimer(snap.TicketNumber, snap.Title);
        menu.Items.Add(mTimer);

        var mAI = new MenuItem { Header = "✦ Analisar com IA" };
        mAI.Click += (_, _) => _mainWindow.OpenAIForTicket(snap.TicketNumber);
        menu.Items.Add(mAI);

        menu.Items.Add(new Separator());

        var mCopy = new MenuItem { Header = "📋 Copiar número" };
        mCopy.Click += (_, _) => Clipboard.SetText(snap.TicketNumber);
        menu.Items.Add(mCopy);

        if (!string.IsNullOrEmpty(snap.BzpUrl))
        {
            var mUrl = new MenuItem { Header = "🔗 Abrir no Dynamics" };
            mUrl.Click += (_, _) =>
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(snap.BzpUrl!) { UseShellExecute = true });
            menu.Items.Add(mUrl);
        }

        return menu;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LAYOUT HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static Border SurfaceBar(bool bottomBorder, Thickness? padding = null) => new()
    {
        Background = DashboardTheme.Brush(DashboardTheme.Surface),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = bottomBorder ? new Thickness(0, 0, 0, 1) : new Thickness(0),
        Padding = padding ?? new Thickness(0),
    };

    private static void AddRow(Grid g, UIElement el, int row)
    {
        Grid.SetRow(el, row);
        g.Children.Add(el);
    }

    private static void AddCol(Grid g, UIElement el, int col)
    {
        Grid.SetColumn(el, col);
        g.Children.Add(el);
    }
}