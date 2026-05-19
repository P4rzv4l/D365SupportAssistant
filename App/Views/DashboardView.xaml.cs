// =============================================================================
//  DashboardView.xaml.cs — Chamados estilo tabela + painel lateral (dark)
// =============================================================================

using D365Assistant.Core.Models.Incident;
using D365Assistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

public partial class DashboardView : Page
{
    private readonly DashboardViewModel _vm;
    private readonly TrackerViewModel _trackerVm;
    private readonly MainWindow _mainWindow;

    // ── Palette ───────────────────────────────────────────────────────────────
    private static readonly Color CBg = Color.FromRgb(0x08, 0x0C, 0x12);
    private static readonly Color CSurface = Color.FromRgb(0x0F, 0x15, 0x20);
    private static readonly Color CSurface2 = Color.FromRgb(0x13, 0x1B, 0x27);
    private static readonly Color CSurface3 = Color.FromRgb(0x18, 0x22, 0x30);
    private static readonly Color CBorder = Color.FromRgb(0x1E, 0x28, 0x38);
    private static readonly Color CBorder2 = Color.FromRgb(0x28, 0x36, 0x48);
    private static readonly Color CText = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private static readonly Color CTextSub = Color.FromRgb(0x64, 0x74, 0x8B);
    private static readonly Color CAccent = Color.FromRgb(0x3B, 0x82, 0xF6);
    private static readonly Color CGreen = Color.FromRgb(0x22, 0xC5, 0x5E);
    private static readonly Color CRed = Color.FromRgb(0xEF, 0x44, 0x44);
    private static readonly Color CYellow = Color.FromRgb(0xF5, 0x9E, 0x0B);
    private static readonly Color CPurple = Color.FromRgb(0xA7, 0x8B, 0xFA);
    private static readonly Color COrange = Color.FromRgb(0xF9, 0x73, 0x16);
    private static readonly Color CRowHov = Color.FromRgb(0x12, 0x1C, 0x2C);
    private static readonly Color CRowSel = Color.FromRgb(0x16, 0x22, 0x36);

    // ── State ─────────────────────────────────────────────────────────────────
    private IncidentSnapshot? _selected;
    private int _page = 1;
    private const int PageSize = 15;
    private string _activeTab = "Todos os Chamados";

    // ── UI refs ───────────────────────────────────────────────────────────────
    private StackPanel? _tableBody;
    private Border? _detailPanel;
    private StackPanel? _detailContent;
    private TextBlock? _totalTb;
    private TextBlock? _pageTb;
    private TextBlock? _lastUpdTb;
    private readonly Dictionary<string, Border> _rowMap = [];
    private readonly Dictionary<string, Button> _tabBtns = [];

    // Filter control refs (for Clear button)
    private TextBox? _searchBox;
    private Action<string>? _setStatusFilter;
    private Action<string>? _setPriFilter;

    public DashboardView(DashboardViewModel vm, TrackerViewModel trackerVm, MainWindow mainWindow)
    {
        InitializeComponent();
        _vm = vm;
        _trackerVm = trackerVm;
        _mainWindow = mainWindow;

        var root = BuildRoot();
        ((Grid)Content).Children.Add(root);

        void OnCollChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => Dispatcher.Invoke(RenderTable);
        void OnPropChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
            => Dispatcher.Invoke(SyncLastUpdated);

        _vm.Incidents.CollectionChanged += OnCollChanged;
        _vm.PropertyChanged += OnPropChanged;

        this.Unloaded += (_, _) =>
        {
            _vm.Incidents.CollectionChanged -= OnCollChanged;
            _vm.PropertyChanged -= OnPropChanged;
        };

        RenderTable();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ROOT LAYOUT
    // ══════════════════════════════════════════════════════════════════════════

    private UIElement BuildRoot()
    {
        var root = new Grid { Background = new SolidColorBrush(CBg) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // topbar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // tabs
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content

        root.Children.Add(BuildTopBar()); Grid.SetRow(root.Children[root.Children.Count - 1], 0);
        root.Children.Add(BuildTabs()); Grid.SetRow(root.Children[root.Children.Count - 1], 1);
        root.Children.Add(BuildContent()); Grid.SetRow(root.Children[root.Children.Count - 1], 2);

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
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = g;

        // Left
        var left = new StackPanel();
        Grid.SetColumn(left, 0);
        g.Children.Add(left);
        left.Children.Add(new TextBlock
        {
            Text = "Chamados",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(CText),
        });
        left.Children.Add(new TextBlock
        {
            Text = "Gerencie e acompanhe os chamados de suporte",
            FontSize = 11,
            Foreground = new SolidColorBrush(CTextSub),
        });

        // Right
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(right, 1);
        g.Children.Add(right);

        _lastUpdTb = new TextBlock
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(CTextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            Text = _vm.LastUpdated,
        };
        right.Children.Add(_lastUpdTb);

        right.Children.Add(new TextBlock
        {
            Text = "Período:",
            FontSize = 12,
            Foreground = new SolidColorBrush(CTextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var (perHost, _, _, setPerItems) = Dropdown();
        setPerItems(["Últimos 30 dias", "Hoje", "Esta semana", "Este mês", "Todos"]);
        perHost.Width = 150;
        perHost.Margin = new Thickness(0, 0, 10, 0);
        right.Children.Add(perHost);

        var btnRef = OutlineBtn("↻  Atualizar");
        btnRef.Click += (_, _) =>
            App.Services.GetRequiredService<MainViewModel>().RefreshCommand.Execute(null);
        right.Children.Add(btnRef);

        return bar;
    }

    private void SyncLastUpdated()
    {
        if (_lastUpdTb != null) _lastUpdTb.Text = _vm.LastUpdated;
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

        foreach (var label in new[] { "Todos os Chamados", "Meus Chamados", "Em Atendimento", "Aguardando Cliente", "Aguardando Terceiros", "Resolvidos", "Cancelados" })
        {
            var btn = TabBtn(label);
            var l = label;
            btn.Click += (_, _) =>
            {
                _activeTab = l;
                _vm.ActiveTab = l;
                _page = 1;
                HighlightTab(l);
                RenderTable();
            };
            _tabBtns[label] = btn;
            tabs.Children.Add(btn);
        }
        HighlightTab("Todos os Chamados");
        return bar;
    }

    private Button TabBtn(string label)
    {
        var btn = new Button
        {
            Content = label,
            FontSize = 12,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(CTextSub),
            BorderThickness = new Thickness(0, 0, 0, 2),
            BorderBrush = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Padding = new Thickness(4, 12, 4, 12),
            Margin = new Thickness(0, 0, 20, 0),
        };
        return btn;
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

    // ── Content: filters + table + detail ─────────────────────────────────────

    private UIElement BuildContent()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // filters
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // table
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // detail

        grid.Children.Add(BuildFilters());
        Grid.SetColumn(grid.Children[grid.Children.Count - 1], 0);

        grid.Children.Add(BuildTableArea());
        Grid.SetColumn(grid.Children[grid.Children.Count - 1], 1);

        // Detail panel
        _detailPanel = new Border
        {
            Width = 380,
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        var detailSv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _detailContent = new StackPanel();
        detailSv.Content = _detailContent;
        _detailPanel.Child = detailSv;
        Grid.SetColumn(_detailPanel, 2);
        grid.Children.Add(_detailPanel);

        return grid;
    }

    // ── Filters sidebar ───────────────────────────────────────────────────────

    private UIElement BuildFilters()
    {
        var sidebar = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 1, 0),
        };
        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(14, 16, 14, 16),
        };
        sidebar.Child = sv;

        var stack = new StackPanel();
        sv.Content = stack;

        // Header
        var hdrRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        stack.Children.Add(hdrRow);

        hdrRow.Children.Add(new TextBlock
        {
            Text = "Filtros",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
        });

        var btnClear = new Button
        {
            Content = "Limpar",
            FontSize = 10,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(CAccent),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(0),
        };
        btnClear.Click += (_, _) =>
        {
            _vm.SearchText = "";
            _vm.PriFilter = "Todos";
            _vm.StatusFilter = "Todos";
            if (_searchBox != null) _searchBox.Text = "";
            _setStatusFilter?.Invoke("Todos");
            _setPriFilter?.Invoke("Todas");
            _page = 1;
            RenderTable();
        };
        Grid.SetColumn(btnClear, 1);
        hdrRow.Children.Add(btnClear);

        // Search
        var searchBorder = new Border
        {
            Background = new SolidColorBrush(CBg),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 0, 8, 0),
            Height = 32,
            Margin = new Thickness(0, 0, 0, 14),
        };
        var sRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sRow.Children.Add(new TextBlock { Text = "⌕", FontSize = 12, Foreground = new SolidColorBrush(CTextSub), Margin = new Thickness(0, 0, 5, 0) });
        var searchBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(CText),
            FontSize = 11,
            Width = 130,
        };
        _searchBox = searchBox;
        searchBox.TextChanged += (_, _) => { _vm.SearchText = searchBox.Text; _page = 1; };
        sRow.Children.Add(searchBox);
        searchBorder.Child = sRow;
        stack.Children.Add(searchBorder);

        // Status filter
        stack.Children.Add(FilterLabel("Status"));
        var (stHost, getSt, setStVal, setStItems) = Dropdown(val =>
        {
            _vm.StatusFilter = val == "Todos" ? "Todos" : val;
            _page = 1;
            RenderTable();
        });
        setStItems(["Todos", "Em Atendimento", "Aguardando Fila", "Aguard. Cliente", "Impeditivo", "Resolvido", "Cancelado"]);
        _setStatusFilter = setStVal;
        stHost.Margin = new Thickness(0, 0, 0, 14);
        stack.Children.Add(stHost);

        stack.Children.Add(FilterLabel("Prioridade"));
        var (priHost, getPri, setPriVal, setPriItems) = Dropdown(val =>
        {
            _vm.PriFilter = val == "Todas" ? "Todos" : val;
            _page = 1;
            RenderTable();
        });
        setPriItems(["Todas", "Urgente", "Alto", "Normal", "Baixa"]);
        _setPriFilter = setPriVal;
        priHost.Margin = new Thickness(0, 0, 0, 14);
        stack.Children.Add(priHost);

        stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(CBorder), Margin = new Thickness(0, 4, 0, 14) });

        // Saved filters
        stack.Children.Add(new TextBlock
        {
            Text = "FILTROS SALVOS",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51)),
            Margin = new Thickness(0, 0, 0, 8),
        });

        foreach (var (label, action) in new (string, Action)[]
        {
            ("Meus chamados abertos",    () => { _vm.StatusFilter = "Em Atendimento"; RenderTable(); }),
            ("Chamados críticos",        () => { _vm.PriFilter = "Urgente"; RenderTable(); }),
            ("Aguardando retorno",       () => { _vm.StatusFilter = "Aguard. Cliente"; RenderTable(); }),
            ("Em atendimento hoje",      () => { _vm.StatusFilter = "Em Atendimento"; RenderTable(); }),
        })
        {
            var lbl = label; var act = action;
            var btn = new Button
            {
                Content = lbl,
                FontSize = 11,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(CTextSub),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0, 6, 0, 6),
            };
            btn.MouseEnter += (_, _) => btn.Foreground = new SolidColorBrush(CAccent);
            btn.MouseLeave += (_, _) => btn.Foreground = new SolidColorBrush(CTextSub);
            btn.Click += (_, _) => act();
            stack.Children.Add(btn);
        }

        return sidebar;
    }

    // ── Table area ────────────────────────────────────────────────────────────

    private UIElement BuildTableArea()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // toolbar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // col headers
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // body
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // Toolbar
        var toolbar = new Border
        {
            Background = new SolidColorBrush(CSurface2),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 8, 16, 8),
        };
        var tg = new Grid();
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Child = tg;

        _totalTb = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(CTextSub), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_totalTb, 0);
        tg.Children.Add(_totalTb);

        var sortRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(sortRow, 1);
        tg.Children.Add(sortRow);

        sortRow.Children.Add(new TextBlock { Text = "Ordenar por:", FontSize = 11, Foreground = new SolidColorBrush(CTextSub), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var (sortHost, getSort, _, setSortItems) = Dropdown();
        setSortItems(["Data de Abertura (Mais recente)", "Prioridade", "Cliente", "Tempo parado"]);
        sortHost.Width = 200;
        sortRow.Children.Add(sortHost);

        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        // Col headers
        var hdr = new Border
        {
            Background = new SolidColorBrush(CSurface2),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var hg = ColGrid();
        hdr.Child = hg;

        void HdrCell(int col, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CTextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(col == 0 ? 16 : 0, 9, 8, 9),
            };
            Grid.SetColumn(tb, col);
            hg.Children.Add(tb);
        }

        HdrCell(0, "ID");
        HdrCell(1, "Assunto");
        HdrCell(2, "Cliente");
        HdrCell(3, "Status");
        HdrCell(4, "Prioridade");
        HdrCell(5, "Técnico");
        HdrCell(6, "Aberto em");

        Grid.SetRow(hdr, 1);
        root.Children.Add(hdr);

        // Body scroll
        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = new SolidColorBrush(CBg),
        };
        sv.PreviewMouseWheel += (_, e) => { sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0); e.Handled = true; };
        _tableBody = new StackPanel();
        sv.Content = _tableBody;
        Grid.SetRow(sv, 2);
        root.Children.Add(sv);

        // Footer / pagination
        var footer = BuildFooter();
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        return root;
    }

    private UIElement BuildFooter()
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

        _pageTb = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(CTextSub), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_pageTb, 0);
        g.Children.Add(_pageTb);

        var pagRow = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(pagRow, 1);
        g.Children.Add(pagRow);

        Button PB(string t) => new()
        {
            Content = t,
            FontSize = 12,
            MinWidth = 30,
            Background = new SolidColorBrush(CSurface2),
            Foreground = new SolidColorBrush(CText),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(7, 4, 7, 4),
            Margin = new Thickness(2, 0, 2, 0),
        };

        var bFirst = PB("K"); bFirst.Click += (_, _) => { _page = 1; RenderTable(); };
        var bPrev = PB("‹"); bPrev.Click += (_, _) => { if (_page > 1) { _page--; RenderTable(); } };
        var bNext = PB("›"); bNext.Click += (_, _) => { _page++; RenderTable(); };
        var bLast = PB("»"); bLast.Click += (_, _) => { _page = 9999; RenderTable(); };

        pagRow.Children.Add(bFirst);
        pagRow.Children.Add(bPrev);

        // Page number buttons (up to 5)
        for (int i = 1; i <= 5; i++)
        {
            var pi = i;
            var pb = PB(i.ToString());
            pb.Tag = "pageBtn";
            pb.Click += (_, _) => { _page = pi; RenderTable(); };
            pagRow.Children.Add(pb);
        }

        pagRow.Children.Add(bNext);
        pagRow.Children.Add(bLast);

        return bar;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  RENDER TABLE
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderTable()
    {
        if (_tableBody == null) return;
        _tableBody.Children.Clear();
        _rowMap.Clear();

        var items = _vm.Incidents.ToList();
        var total = items.Count;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        _page = Math.Clamp(_page, 1, pages);
        var paged = items.Skip((_page - 1) * PageSize).Take(PageSize).ToList();

        if (_totalTb != null)
            _totalTb.Text = $"{total} chamado{(total != 1 ? "s" : "")} encontrado{(total != 1 ? "s" : "")}";

        if (_pageTb != null)
            _pageTb.Text = $"{(_page - 1) * PageSize + 1} - {Math.Min(_page * PageSize, total)} de {total}";

        if (total == 0)
        {
            _tableBody.Children.Add(new TextBlock
            {
                Text = "Nenhum chamado encontrado.",
                FontSize = 13,
                Foreground = new SolidColorBrush(CTextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0),
            });
            return;
        }

        foreach (var snap in paged)
        {
            var row = BuildRow(snap);
            _rowMap[snap.TicketNumber] = row;
            _tableBody.Children.Add(row);
        }

        // Restore selection
        if (_selected != null && _rowMap.TryGetValue(_selected.TicketNumber, out var sr))
            sr.Background = new SolidColorBrush(CRowSel);
    }

    // ── Row ───────────────────────────────────────────────────────────────────

    private Border BuildRow(IncidentSnapshot snap)
    {
        var row = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.Hand,
        };
        row.MouseEnter += (_, _) => { if (_selected?.TicketNumber != snap.TicketNumber) row.Background = new SolidColorBrush(CRowHov); };
        row.MouseLeave += (_, _) => { if (_selected?.TicketNumber != snap.TicketNumber) row.Background = new SolidColorBrush(CSurface); };
        row.MouseLeftButtonUp += (_, _) => SelectRow(snap, row);
        row.ContextMenu = BuildContextMenu(snap);

        var g = ColGrid();
        row.Child = g;

        // Col 0: ID
        var idTb = new TextBlock
        {
            Text = snap.TicketNumber,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = string.IsNullOrEmpty(snap.BzpUrl) ? new SolidColorBrush(CTextSub) : new SolidColorBrush(CAccent),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 10, 8, 10),
            Cursor = string.IsNullOrEmpty(snap.BzpUrl) ? Cursors.Arrow : Cursors.Hand,
        };
        if (!string.IsNullOrEmpty(snap.BzpUrl))
        {
            var url = snap.BzpUrl;
            idTb.MouseLeftButtonUp += (_, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                e.Handled = true;
            };
        }
        Grid.SetColumn(idTb, 0); g.Children.Add(idTb);

        // Col 1: Subject
        var subjStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 8, 8) };
        subjStack.Children.Add(new TextBlock
        {
            Text = snap.Title,
            FontSize = 12,
            Foreground = new SolidColorBrush(CText),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 280,
        });
        if (!snap.FirstResponseSent)
            subjStack.Children.Add(new TextBlock
            {
                Text = "⚡ Aguardando 1ª comunicação",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(COrange),
            });
        Grid.SetColumn(subjStack, 1); g.Children.Add(subjStack);

        // Col 2: Customer
        var custTb = new TextBlock
        {
            Text = snap.CustomerDisplayName,
            FontSize = 11,
            Foreground = new SolidColorBrush(CTextSub),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 130,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(custTb, 2); g.Children.Add(custTb);

        // Col 3: Status badge
        var (stFg, stBg, stLabel) = StatusInfo(snap.StatusCode);
        var stBadge = DarkBadge(stLabel, stFg, stBg);
        stBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(stBadge, 3); g.Children.Add(stBadge);

        // Col 4: Priority badge
        var (priFg, priBg, priLabel) = PriorityInfo(snap.PriorityCode);
        var priBadge = DarkBadge(priLabel, priFg, priBg);
        priBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(priBadge, 4); g.Children.Add(priBadge);

        // Col 5: Technician avatar
        var owner = snap.OwnerName ?? "";
        var initials = owner.Length > 0
            ? string.Concat(owner.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0]))
            : "?";
        var avatar = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xA7, 0x8B, 0xFA)),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = owner,
            Child = new TextBlock
            {
                Text = initials.ToUpper(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(CPurple),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
        };
        Grid.SetColumn(avatar, 5); g.Children.Add(avatar);

        // Col 6: Created date + idle
        var idleH = snap.HoursSinceModified;
        var idleFg = idleH > 48 ? CRed : idleH > 8 ? CYellow : CTextSub;
        var idleTxt = idleH < 1 ? $"{(int)(idleH * 60)}m atrás"
                    : idleH < 24 ? $"{idleH:F0}h atrás"
                                 : $"{idleH / 24:F0}d atrás";
        var dateStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        dateStack.Children.Add(new TextBlock
        {
            Text = snap.CreatedOn.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(CTextSub),
        });
        dateStack.Children.Add(new TextBlock
        {
            Text = idleTxt,
            FontSize = 9.5,
            Foreground = new SolidColorBrush(idleFg),
        });
        Grid.SetColumn(dateStack, 6); g.Children.Add(dateStack);

        return row;
    }

    private Grid ColGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // ID
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // subject
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); // customer
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); // status
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // priority
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });  // tech
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // date
        return g;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DETAIL PANEL
    // ══════════════════════════════════════════════════════════════════════════

    private void SelectRow(IncidentSnapshot snap, Border row)
    {
        foreach (var (_, r) in _rowMap) r.Background = new SolidColorBrush(CSurface);
        _selected = snap;
        row.Background = new SolidColorBrush(CRowSel);

        if (_detailPanel == null || _detailContent == null) return;
        _detailPanel.Visibility = Visibility.Visible;
        _detailContent.Children.Clear();

        // ── Header ────────────────────────────────────────────────────────────
        var hdrBorder = new Border
        {
            Background = new SolidColorBrush(CSurface2),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12),
        };
        var hdrStack = new StackPanel();
        hdrBorder.Child = hdrStack;

        // Title row: star + ticket + priority + status + close
        var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrStack.Children.Add(titleRow);

        var titleLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(titleLeft, 0);
        titleRow.Children.Add(titleLeft);

        titleLeft.Children.Add(new TextBlock { Text = "★ ", FontSize = 14, Foreground = new SolidColorBrush(CYellow), VerticalAlignment = VerticalAlignment.Center });
        titleLeft.Children.Add(new TextBlock
        {
            Text = snap.TicketNumber,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(CAccent),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var (priFg2, priBg2, priLbl2) = PriorityInfo(snap.PriorityCode);
        titleLeft.Children.Add(DarkBadge(priLbl2, priFg2, priBg2));

        // Status dropdown-like
        var (stFg2, stBg2, stLbl2) = StatusInfo(snap.StatusCode);
        titleLeft.Children.Add(DarkBadge(stLbl2, stFg2, stBg2, margin: new Thickness(6, 0, 0, 0)));

        // Close button
        var btnClose = new Button
        {
            Content = "✕",
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(CTextSub),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6, 4, 6, 4),
        };
        btnClose.MouseEnter += (_, _) => btnClose.Foreground = new SolidColorBrush(CText);
        btnClose.MouseLeave += (_, _) => btnClose.Foreground = new SolidColorBrush(CTextSub);
        btnClose.Click += (_, _) =>
        {
            _selected = null;
            foreach (var (_, r) in _rowMap) r.Background = new SolidColorBrush(CSurface);
            if (_detailPanel != null) _detailPanel.Visibility = Visibility.Collapsed;
        };
        Grid.SetColumn(btnClose, 1);
        titleRow.Children.Add(btnClose);

        // Subject
        hdrStack.Children.Add(new TextBlock
        {
            Text = snap.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });

        // Action buttons
        var actRow = new StackPanel { Orientation = Orientation.Horizontal };
        hdrStack.Children.Add(actRow);

        var btnTimer = ActionBtn("▶ Iniciar Tempo", CGreen);
        btnTimer.Click += (_, _) => _mainWindow.QuickStartTimer(snap.TicketNumber, snap.Title);
        actRow.Children.Add(btnTimer);

        var btnPause = ActionBtn("⏸ Pausar", CTextSub);
        btnPause.Margin = new Thickness(6, 0, 0, 0);
        actRow.Children.Add(btnPause);

        var btnFinal = ActionBtn("■ Finalizar", CRed);
        btnFinal.Margin = new Thickness(6, 0, 0, 0);
        actRow.Children.Add(btnFinal);

        // Timer display
        try
        {
            var secs = App.Services.GetRequiredService<Core.Services.StorageService>()
                          .GetTrackedSecondsForTicket(snap.TicketNumber);
            if (secs > 0)
            {
                var ts = TimeSpan.FromSeconds(secs);
                hdrStack.Children.Add(new TextBlock
                {
                    Text = $"Tempo Total  {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}",
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(CPurple),
                    Margin = new Thickness(0, 8, 0, 0),
                });
            }
        }
        catch { }

        _detailContent.Children.Add(hdrBorder);

        // ── Info grid ─────────────────────────────────────────────────────────
        var infoGrid = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12),
        };
        var ig = new Grid();
        ig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        infoGrid.Child = ig;

        var leftInfo = new StackPanel();
        Grid.SetColumn(leftInfo, 0);
        ig.Children.Add(leftInfo);

        var rightInfo = new StackPanel();
        Grid.SetColumn(rightInfo, 1);
        ig.Children.Add(rightInfo);

        void InfoItem(StackPanel parent, string label, string value, Color? valueColor = null)
        {
            parent.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(CTextSub),
                Margin = new Thickness(0, 0, 0, 2),
            });
            parent.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 11,
                Foreground = new SolidColorBrush(valueColor ?? CText),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        InfoItem(leftInfo, "Cliente", snap.CustomerDisplayName.Length > 0 ? snap.CustomerDisplayName : "—");
        InfoItem(leftInfo, "Aberto em", snap.CreatedOn.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        InfoItem(rightInfo, "Técnico Responsável", snap.OwnerName ?? "—");
        InfoItem(rightInfo, "Última Atualização", snap.ModifiedOn.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));

        if (!string.IsNullOrEmpty(snap.Description))
            InfoItem(leftInfo, "Descrição", snap.Description, CTextSub);

        if (!string.IsNullOrEmpty(snap.BzMotivoStatus))
            InfoItem(rightInfo, "Observações", snap.BzMotivoStatus, CTextSub);

        _detailContent.Children.Add(infoGrid);

        // ── Timeline ──────────────────────────────────────────────────────────
        var tlBorder = new Border
        {
            Background = new SolidColorBrush(CSurface),
            Padding = new Thickness(16, 12, 16, 16),
        };
        var tlStack = new StackPanel();
        tlBorder.Child = tlStack;

        var tlHdr = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        tlHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tlHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tlStack.Children.Add(tlHdr);

        tlHdr.Children.Add(new TextBlock
        {
            Text = "Linha do Tempo",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
        });

        var btnFilter = new Button
        {
            Content = "Filtrar",
            FontSize = 10,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(CAccent),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(0),
        };
        Grid.SetColumn(btnFilter, 1);
        tlHdr.Children.Add(btnFilter);

        // Add comment box
        var commentRow = new Border
        {
            Background = new SolidColorBrush(CSurface2),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 12),
        };
        var cRow = new Grid();
        cRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commentRow.Child = cRow;

        cRow.Children.Add(new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(CTextSub),
            FontSize = 11,
            Text = "Adicionar comentário...",
        });
        var btnSend = new Button
        {
            Content = "▶",
            FontSize = 11,
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA)),
            Foreground = new SolidColorBrush(CPurple),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xA7, 0x8B, 0xFA)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(8, 4, 8, 4),
        };
        Grid.SetColumn(btnSend, 1);
        cRow.Children.Add(btnSend);

        tlStack.Children.Add(commentRow);

        // Timeline events from BzHistoricoOcorrencia + static events
        var events = new List<(DateTime dt, string actor, string type, string msg, Color color)>
        {
            (snap.CreatedOn.ToLocalTime(),  "Sistema",          "Criação",     $"Chamado criado",           CTextSub),
            (snap.ModifiedOn.ToLocalTime(), snap.OwnerName ?? "Sistema", "Comentário", "Última atualização", CAccent),
        };

        if (!snap.FirstResponseSent)
            events.Insert(1, (snap.CreatedOn.ToLocalTime().AddMinutes(5), "Sistema", "Status",
                "Status alterado para Em Atendimento", CYellow));

        foreach (var (dt, actor, type, msg, color) in events.OrderByDescending(e => e.dt))
        {
            var evt = BuildTimelineEvent(actor, type, msg, dt, color);
            tlStack.Children.Add(evt);
        }

        _detailContent.Children.Add(tlBorder);
    }

    private UIElement BuildTimelineEvent(string actor, string type, string msg, DateTime dt, Color color)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Avatar
        var initials = actor.Length > 0
            ? string.Concat(actor.Split(' ').Take(2).Select(p => p.Length > 0 ? p[0].ToString() : ""))
            : "?";
        var av = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(0x25, color.R, color.G, color.B)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            Child = new TextBlock
            {
                Text = initials.ToUpper(),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
        };
        Grid.SetColumn(av, 0); row.Children.Add(av);

        var content = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetColumn(content, 1); row.Children.Add(content);

        var topLine = new Grid();
        topLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(topLine);

        topLine.Children.Add(new TextBlock
        {
            Text = $"{actor}  ",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(CText),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var typeSpan = new TextBlock
        {
            Text = type,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2),
        };
        Grid.SetColumn(typeSpan, 0);

        topLine.Children.Add(new TextBlock
        {
            Text = dt.ToString("dd/MM/yyyy HH:mm"),
            FontSize = 10,
            Foreground = new SolidColorBrush(CTextSub),
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(topLine.Children[1], 1);

        content.Children.Add(new TextBlock
        {
            Text = msg,
            FontSize = 11,
            Foreground = new SolidColorBrush(CTextSub),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
        });

        return row;
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
        mCopy.Click += (_, _) => System.Windows.Clipboard.SetText(snap.TicketNumber);
        menu.Items.Add(mCopy);

        if (!string.IsNullOrEmpty(snap.BzpUrl))
        {
            var mUrl = new MenuItem { Header = "🔗 Abrir no Dynamics" };
            var url = snap.BzpUrl;
            mUrl.Click += (_, _) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            menu.Items.Add(mUrl);
        }

        return menu;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static (Border host, Func<string?> getValue, Action<string> setValue, Action<List<string>> setItems)
        Dropdown(Action<string>? onSelected = null)
    {
        var items = new List<string>();
        var selectedIdx = 0;
        Popup? popup = null;

        var selectedTb = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(CText),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var arrow = new TextBlock
        {
            Text = "⌄",
            FontSize = 11,
            Foreground = new SolidColorBrush(CTextSub),
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
            Background = new SolidColorBrush(CSurface2),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 32,
            Cursor = Cursors.Hand,
            Child = hg,
        };
        host.MouseEnter += (_, _) => host.Background = new SolidColorBrush(CSurface3);
        host.MouseLeave += (_, _) => host.Background = new SolidColorBrush(CSurface2);

        void RebuildPopup()
        {
            popup?.Let(p => p.IsOpen = false);
            var list = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20)) };
            for (int i = 0; i < items.Count; i++)
            {
                var idx = i; var item = items[i];
                var selFg = Color.FromRgb(0xA7, 0x8B, 0xFA);
                var row = new Border
                {
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = idx == selectedIdx
                        ? new SolidColorBrush(Color.FromArgb(0x30, 0xA7, 0x8B, 0xFA))
                        : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = item,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(idx == selectedIdx ? selFg : CText),
                    },
                };
                row.MouseEnter += (_, _) => { if (idx != selectedIdx) row.Background = new SolidColorBrush(Color.FromArgb(0x18, 0xA7, 0x8B, 0xFA)); };
                row.MouseLeave += (_, _) => { row.Background = idx == selectedIdx ? new SolidColorBrush(Color.FromArgb(0x30, 0xA7, 0x8B, 0xFA)) : Brushes.Transparent; };
                row.MouseLeftButtonUp += (_, _) =>
                {
                    selectedIdx = idx;
                    selectedTb.Text = item;
                    popup?.Let(p => p.IsOpen = false);
                    onSelected?.Invoke(item); // ← dispara callback após seleção
                };
                list.Children.Add(row);
                if (i < items.Count - 1)
                    list.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(CBorder) });
            }

            popup = new Popup
            {
                Child = new Border
                {
                    Child = list,
                    Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20)),
                    BorderBrush = new SolidColorBrush(CBorder2),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 16, Opacity = 0.4, ShadowDepth = 4 },
                },
                PlacementTarget = host,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                MinWidth = host.ActualWidth > 0 ? host.ActualWidth : 160,
            };
            var p2 = popup;
            p2.Opened += (_, _) => { host.BorderBrush = new SolidColorBrush(CAccent); arrow.Text = "⌃"; };
            p2.Closed += (_, _) => { host.BorderBrush = new SolidColorBrush(CBorder); arrow.Text = "⌄"; };
        }

        host.MouseLeftButtonUp += (_, _) => { RebuildPopup(); popup!.IsOpen = !popup.IsOpen; };

        string? getValue() => selectedIdx < items.Count ? items[selectedIdx] : null;
        void setValue(string v) { var i = items.IndexOf(v); if (i >= 0) { selectedIdx = i; selectedTb.Text = items[i]; } }
        void setItems(List<string> n) { items.Clear(); items.AddRange(n); if (n.Count > 0) { selectedIdx = 0; selectedTb.Text = n[0]; } }

        return (host, getValue, setValue, setItems);
    }

    private static Button OutlineBtn(string label) => new()
    {
        Content = label,
        FontSize = 11,
        Background = new SolidColorBrush(Color.FromRgb(0x13, 0x1B, 0x27)),
        Foreground = new SolidColorBrush(CText),
        BorderBrush = new SolidColorBrush(CBorder),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(12, 6, 12, 6),
        Margin = new Thickness(8, 0, 0, 0),
    };

    private static Button ActionBtn(string label, Color accent) => new()
    {
        Content = label,
        FontSize = 11,
        FontFamily = new FontFamily("Segoe UI Semibold"),
        Background = new SolidColorBrush(Color.FromArgb(0x20, accent.R, accent.G, accent.B)),
        Foreground = new SolidColorBrush(accent),
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, accent.R, accent.G, accent.B)),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(12, 6, 12, 6),
    };

    private static TextBlock FilterLabel(string text) => new()
    {
        Text = text,
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(CTextSub),
        Margin = new Thickness(0, 0, 0, 5),
    };

    private static Border DarkBadge(string text, string fgHex, string bgHex, Thickness? margin = null)
    {
        var fg = (Color)ColorConverter.ConvertFromString(fgHex);
        return new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, fg.R, fg.G, fg.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = margin ?? new Thickness(0),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fg),
            }
        };
    }

    private static (string fg, string bg, string label) PriorityInfo(int? code) => code switch
    {
        419500000 => ("#FCA5A5", "#3B0C0C", "Urgente"),
        1 => ("#FCD34D", "#3B2A00", "Alto"),
        2 => ("#93C5FD", "#0C1F3A", "Normal"),
        3 => ("#86EFAC", "#0A2010", "Baixa"),
        _ => ("#64748B", "#0F1520", "—"),
    };

    private static (string fg, string bg, string label) StatusInfo(int code) => code switch
    {
        100000000 => ("#93C5FD", "#0C1F3A", "Novo"),
        4 => ("#64748B", "#0F1520", "Aguard. Fila"),
        1 => ("#86EFAC", "#0A2010", "Em Atendimento"),
        419500000 => ("#FCD34D", "#3B2A00", "Aguard. Cliente"),
        3 => ("#A78BFA", "#1E1245", "Em Aprovação"),
        2 => ("#FCA5A5", "#3B0C0C", "Impeditivo"),
        5 => ("#86EFAC", "#0A2010", "Resolvido"),
        6 => ("#374151", "#0D1117", "Cancelado"),
        419500001 => ("#374151", "#0F1520", "Despriorizado"),
        121360001 => ("#FCD34D", "#3B2A00", "Aguard. Microsoft"),
        _ => ("#374151", "#0F1520", $"St.{code}"),
    };
}