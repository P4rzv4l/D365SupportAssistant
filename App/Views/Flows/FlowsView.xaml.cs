// =============================================================================
//  FlowsView.xaml.cs — Analisador de Fluxos do Dynamics
// =============================================================================
// Estrutura:
//   Models/WorkflowItem.cs              — modelo de domínio
//   ViewModels/FlowsViewModel.cs        — fetch, filtros, stats
//   Sections/FlowRowBuilder.cs          — linha da tabela
//   Sections/FlowDetailPanel.cs         — painel lateral de detalhes
//   FlowsView.xaml.cs                   ← você está aqui (orquestrador)
// =============================================================================

using D365Assistant.Core.Models.Flows;
using D365Assistant.ViewModels;
using D365Assistant.Views.Dashboard.Theme;
using D365Assistant.Views.Flows.Sections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views;

public partial class FlowsView : Page
{
    private readonly FlowsViewModel _vm;

    // ── UI refs ───────────────────────────────────────────────────────────────
    private StackPanel? _tableBody;
    private WpfBorder? _detailPanelRoot;
    private FlowDetailPanel? _detail;
    private TextBlock? _totalTb;
    private TextBlock? _statusTb;
    private Grid? _contentGrid;

    // ── Row state ─────────────────────────────────────────────────────────────
    private WorkflowItem? _selected;
    private readonly Dictionary<string, WpfBorder> _rowMap = [];

    // ── Tabs ──────────────────────────────────────────────────────────────────
    private readonly Dictionary<string, Button> _tabBtns = [];

    // ── Dropdown refs ─────────────────────────────────────────────────────────
    private Action<string>? _setTypeFilter;
    private Action<string>? _setStatusFilter;
    private Action<string>? _setSortOrder;

    public FlowsView(FlowsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        ((Grid)Content).Children.Add(BuildRoot());

        _vm.Items.CollectionChanged += (_, _) => Dispatcher.Invoke(RenderTable);
        _vm.PropertyChanged += OnVmPropertyChanged;

        this.Unloaded += (_, _) =>
        {
            _vm.Items.CollectionChanged -= (_, _) => Dispatcher.Invoke(RenderTable);
            _vm.PropertyChanged -= OnVmPropertyChanged;
        };
    }

    private void OnVmPropertyChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(_vm.StatusText) or nameof(_vm.TotalCount)
                           or nameof(_vm.HasCloudFlows) or nameof(_vm.IsBusy))
            Dispatcher.Invoke(UpdateStatusBar);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ROOT LAYOUT
    // ══════════════════════════════════════════════════════════════════════════

    private UIElement BuildRoot()
    {
        var root = new Grid { Background = DashboardTheme.Brush(DashboardTheme.Bg) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // topbar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // filters
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // stats
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content

        AddRow(root, BuildTopBar(), 0);
        AddRow(root, BuildFilters(), 1);
        AddRow(root, BuildStatsBar(), 2);
        AddRow(root, BuildContent(), 3);

        return root;
    }

    // ── Top bar ───────────────────────────────────────────────────────────────

    private UIElement BuildTopBar()
    {
        var bar = SurfaceBar(bottomBorder: true, padding: new Thickness(24, 14, 24, 14));
        var g = TwoColumnGrid();
        bar.Child = g;

        var left = new StackPanel();
        Grid.SetColumn(left, 0);
        g.Children.Add(left);
        left.Children.Add(new TextBlock
        {
            Text = "⚡  Analisador de Fluxos",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        });
        left.Children.Add(new TextBlock
        {
            Text = "Busque e analise workflows e Cloud Flows do Dynamics 365",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        });

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(right, 1);
        g.Children.Add(right);

        var btnExport = OutlineButton("⬇ Exportar JSON");
        btnExport.Click += (_, _) => _vm.ExportJsonCommand.Execute(null);
        right.Children.Add(btnExport);

        var btnClear = OutlineButton("✕ Limpar");
        btnClear.Margin = new Thickness(8, 0, 0, 0);
        btnClear.Click += (_, _) =>
        {
            _vm.ClearResultsCommand.Execute(null);
            _tableBody?.Children.Clear();
            _rowMap.Clear();
            _detail?.Hide();
            UpdateStatusBar();
        };
        right.Children.Add(btnClear);

        return bar;
    }

    // ── Filters bar ───────────────────────────────────────────────────────────

    private UIElement BuildFilters()
    {
        var bar = SurfaceBar(bottomBorder: true, padding: new Thickness(16, 10, 16, 10));
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });  // URL
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });  // Type
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // Buscar
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // spacer
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // status filter
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // sort
        bar.Child = g;

        // URL input
        var urlBox = DarkTextBox("https://org.crm.dynamics.com");
        urlBox.TextChanged += (_, _) => _vm.EnvironmentUrl = urlBox.Text;
        Grid.SetColumn(urlBox, 0);
        g.Children.Add(urlBox);

        // Type dropdown
        var (typeHost, _, setTypeVal, setTypeItems) = Dropdown(val =>
        {
            var opt = FlowsViewModel.TypeOptions.FirstOrDefault(o => o.Label == val);
            if (opt != null) _vm.SelectedType = opt;
        });
        setTypeItems(FlowsViewModel.TypeOptions.Select(o => o.Label).ToList());
        _setTypeFilter = setTypeVal;
        typeHost.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(typeHost, 1);
        g.Children.Add(typeHost);

        // Buscar button
        var btnFetch = new Button
        {
            Content = "⚡  Buscar",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = DashboardTheme.Brush(DashboardTheme.Accent),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Height = 34,
        };
        btnFetch.Click += async (_, _) => await _vm.FetchAsync();
        Grid.SetColumn(btnFetch, 2);
        g.Children.Add(btnFetch);

        // Status filter
        var (stHost, _, setStVal, setStItems) = Dropdown(val =>
        {
            _vm.StatusFilter = val;
        });
        setStItems(["Todos", "Ativo", "Inativo"]);
        _setStatusFilter = setStVal;
        stHost.Width = 110;
        stHost.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(stHost, 4);
        g.Children.Add(stHost);

        // Sort
        var (sortHost, _, setSortVal, setSortItems) = Dropdown(val =>
        {
            _vm.SortOrder = val;
        });
        setSortItems(["A-Z", "Z-A"]);
        _setSortOrder = setSortVal;
        sortHost.Width = 80;
        sortHost.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(sortHost, 5);
        g.Children.Add(sortHost);

        return bar;
    }

    // ── Stats bar ─────────────────────────────────────────────────────────────

    private UIElement BuildStatsBar()
    {
        var bar = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 8, 20, 8),
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Child = row;

        _totalTb = new TextBlock
        {
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0),
        };
        row.Children.Add(_totalTb);

        _statusTb = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(_statusTb);

        // Search boxes inline
        row.Children.Add(new WpfBorder
        {
            Width = 1,
            Background = DashboardTheme.Brush(DashboardTheme.Border),
            Margin = new Thickness(20, 2, 20, 2),
        });

        row.Children.Add(BuildSearchSection());

        return bar;
    }

    private UIElement BuildSearchSection()
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };

        stack.Children.Add(new TextBlock
        {
            Text = "Pesquisar:",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var box1 = DarkTextBox("Termo 1...", width: 160, height: 28);
        box1.TextChanged += (_, _) => _vm.SearchTerm1 = box1.Text;
        stack.Children.Add(box1);

        // AND/OR toggle
        var toggleAnd = new Button
        {
            Content = "E",
            FontSize = 11,
            Width = 36,
            Height = 28,
            Background = DashboardTheme.AlphaBrush(DashboardTheme.Accent, 0x22),
            Foreground = DashboardTheme.Brush(DashboardTheme.Accent),
            BorderBrush = DashboardTheme.AlphaBrush(DashboardTheme.Accent, 0x44),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Margin = new Thickness(6, 0, 6, 0),
            ToolTip = "Alternar entre AND / OR",
        };
        toggleAnd.Click += (_, _) =>
        {
            _vm.SearchAnd = !_vm.SearchAnd;
            toggleAnd.Content = _vm.SearchAnd ? "E" : "OU";
            toggleAnd.Background = _vm.SearchAnd
                ? DashboardTheme.AlphaBrush(DashboardTheme.Accent, 0x22)
                : DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x22);
            toggleAnd.Foreground = _vm.SearchAnd
                ? DashboardTheme.Brush(DashboardTheme.Accent)
                : DashboardTheme.Brush(DashboardTheme.Purple);
        };
        stack.Children.Add(toggleAnd);

        var box2 = DarkTextBox("Termo 2...", width: 160, height: 28);
        box2.TextChanged += (_, _) => _vm.SearchTerm2 = box2.Text;
        stack.Children.Add(box2);

        // HTTPS filter (only visible when cloud flows loaded)
        var httpsCheck = new CheckBox
        {
            Content = "Só gatilhos HTTPS",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
            Cursor = Cursors.Hand,
        };
        httpsCheck.Checked += (_, _) => _vm.OnlyHttps = true;
        httpsCheck.Unchecked += (_, _) => _vm.OnlyHttps = false;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.HasCloudFlows))
                Dispatcher.Invoke(() =>
                    httpsCheck.Visibility = _vm.HasCloudFlows
                        ? Visibility.Visible : Visibility.Collapsed);
        };
        httpsCheck.Visibility = Visibility.Collapsed;
        stack.Children.Add(httpsCheck);

        return stack;
    }

    // ── Content (table + detail) ──────────────────────────────────────────────

    private UIElement BuildContent()
    {
        _contentGrid = new Grid();
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Table area
        var tableArea = new Grid();
        tableArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // col headers
        tableArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // body
        Grid.SetColumn(tableArea, 0);
        _contentGrid.Children.Add(tableArea);

        AddRow(tableArea, BuildTableHeader(), 0);
        AddRow(tableArea, BuildTableBody(), 1);

        // Detail panel
        _detail = new FlowDetailPanel(
            onClose: CloseDetail,
            connectedUrl: _vm.ConnectedUrl);

        _detailPanelRoot = _detail.Root;
        Grid.SetColumn(_detailPanelRoot, 1);
        _contentGrid.Children.Add(_detailPanelRoot);

        return _contentGrid;
    }

    private UIElement BuildTableHeader()
    {
        var hdr = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        hdr.Child = g;

        void HdrCell(int col, string text, bool left = false)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(left ? 16 : 0, 9, 8, 9),
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        HdrCell(0, "Nome", left: true);
        HdrCell(1, "Categoria");
        HdrCell(2, "Proprietário");
        HdrCell(3, "Status");
        HdrCell(4, "Gatilho");

        return hdr;
    }

    private UIElement BuildTableBody()
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

    // ══════════════════════════════════════════════════════════════════════════
    //  RENDER TABLE
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderTable()
    {
        if (_tableBody == null) return;
        _tableBody.Children.Clear();
        _rowMap.Clear();

        if (_vm.Items.Count == 0)
        {
            if (_vm.IsBusy) return;
            _tableBody.Children.Add(new TextBlock
            {
                Text = "Nenhum fluxo encontrado.",
                FontSize = 13,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0),
            });
            return;
        }

        var rowBuilder = new FlowRowBuilder(
            onSelect: SelectRow,
            onCopyId: item => System.Windows.Clipboard.SetText(item.WorkflowId),
            onOpenDynamics: item =>
            {
                var url = $"{_vm.ConnectedUrl}/sfa/workflow/edit.aspx?id={item.WorkflowId}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            },
            connectedUrl: _vm.ConnectedUrl);

        foreach (var item in _vm.Items)
        {
            var row = rowBuilder.Build(item);
            _rowMap[item.WorkflowId] = row;
            _tableBody.Children.Add(row);
        }

        // Restore selection
        if (_selected != null && _rowMap.TryGetValue(_selected.WorkflowId, out var sel))
        {
            sel.Background = DashboardTheme.Brush(DashboardTheme.RowSelected);
            sel.Tag = "selected";
        }

        UpdateStatusBar();
    }

    private void SelectRow(WorkflowItem item, WpfBorder row)
    {
        foreach (var (_, r) in _rowMap)
        {
            r.Background = DashboardTheme.Brush(DashboardTheme.Surface);
            r.Tag = null;
        }

        _selected = item;
        row.Background = DashboardTheme.Brush(DashboardTheme.RowSelected);
        row.Tag = "selected";

        _detail?.Show(item);
    }

    private void CloseDetail()
    {
        _selected = null;
        foreach (var (_, r) in _rowMap)
        {
            r.Background = DashboardTheme.Brush(DashboardTheme.Surface);
            r.Tag = null;
        }
        _detail?.Hide();
    }

    private void UpdateStatusBar()
    {
        if (_totalTb != null)
            _totalTb.Text = _vm.HasResults
                ? $"{_vm.TotalCount} fluxo(s) exibido(s)"
                : "";

        if (_statusTb != null)
        {
            _statusTb.Text = _vm.IsBusy ? "⏳ Buscando..." : _vm.StatusText;
            try
            {
                _statusTb.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(_vm.StatusColor));
            }
            catch { }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  UI HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static TextBox DarkTextBox(string placeholder, double width = 260, double height = 34) => new()
    {
        Background = DashboardTheme.Brush(DashboardTheme.Bg),
        Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        CaretBrush = DashboardTheme.Brush(DashboardTheme.Accent),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = new Thickness(1),
        FontSize = 12,
        Padding = new Thickness(10, 0, 10, 0),
        Width = width,
        Height = height,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private static Button OutlineButton(string label) => new()
    {
        Content = label,
        FontSize = 11,
        Background = DashboardTheme.Brush(DashboardTheme.Surface2),
        Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(12, 6, 12, 6),
    };

    private static WpfBorder SurfaceBar(bool bottomBorder, Thickness? padding = null) => new()
    {
        Background = DashboardTheme.Brush(DashboardTheme.Surface),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = bottomBorder ? new Thickness(0, 0, 0, 1) : new Thickness(0),
        Padding = padding ?? new Thickness(0),
    };

    private static Grid TwoColumnGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        return g;
    }

    private static void AddRow(Grid g, UIElement el, int row)
    {
        Grid.SetRow(el, row);
        g.Children.Add(el);
    }

    private static (WpfBorder host,
                    Func<string?> getValue,
                    Action<string> setValue,
                    Action<List<string>> setItems)
        Dropdown(Action<string>? onSelected = null)
    {
        var items = new List<string>();
        var selectedIdx = 0;

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

        var ig = new Grid();
        ig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ig.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ig.Children.Add(selectedTb); Grid.SetColumn(selectedTb, 0);
        ig.Children.Add(arrow); Grid.SetColumn(arrow, 1);

        var host = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 34,
            Cursor = Cursors.Hand,
            Child = ig,
        };

        System.Windows.Controls.Primitives.Popup? popup = null;

        host.MouseEnter += (_, _) => host.Background = DashboardTheme.Brush(DashboardTheme.Surface3);
        host.MouseLeave += (_, _) => host.Background = DashboardTheme.Brush(DashboardTheme.Surface2);
        host.MouseLeftButtonUp += (_, _) =>
        {
            // rebuild popup
            popup?.Let(p => p.IsOpen = false);
            var list = new StackPanel { Background = DashboardTheme.Brush(DashboardTheme.Surface) };

            for (int i = 0; i < items.Count; i++)
            {
                var idx = i; var item = items[i];
                var row = new WpfBorder
                {
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = idx == selectedIdx
                        ? DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x30)
                        : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = item,
                        FontSize = 11,
                        Foreground = idx == selectedIdx
                            ? DashboardTheme.Brush(DashboardTheme.Purple)
                            : DashboardTheme.Brush(DashboardTheme.Text),
                    },
                };
                row.MouseEnter += (_, _) => { if (idx != selectedIdx) row.Background = DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x18); };
                row.MouseLeave += (_, _) => { row.Background = idx == selectedIdx ? DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x30) : Brushes.Transparent; };
                row.MouseLeftButtonUp += (_, _) =>
                {
                    selectedIdx = idx;
                    selectedTb.Text = item;
                    popup?.Let(p => p.IsOpen = false);
                    onSelected?.Invoke(item);
                };
                list.Children.Add(row);
            }

            popup = new System.Windows.Controls.Primitives.Popup
            {
                Child = new WpfBorder
                {
                    Child = list,
                    Background = DashboardTheme.Brush(DashboardTheme.Surface),
                    BorderBrush = DashboardTheme.Brush(DashboardTheme.Border2),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 12,
                        Opacity = 0.4,
                        ShadowDepth = 4,
                    },
                },
                PlacementTarget = host,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                MinWidth = host.ActualWidth > 0 ? host.ActualWidth : 140,
            };
            popup.Opened += (_, _) => { host.BorderBrush = DashboardTheme.Brush(DashboardTheme.Accent); arrow.Text = "⌃"; };
            popup.Closed += (_, _) => { host.BorderBrush = DashboardTheme.Brush(DashboardTheme.Border); arrow.Text = "⌄"; };
            popup.IsOpen = true;
        };

        string? getValue() => selectedIdx < items.Count ? items[selectedIdx] : null;
        void setValue(string v) { var i = items.IndexOf(v); if (i >= 0) { selectedIdx = i; selectedTb.Text = items[i]; } }
        void setItems(List<string> n) { items.Clear(); items.AddRange(n); if (n.Count > 0) { selectedIdx = 0; selectedTb.Text = n[0]; } }

        return (host, getValue, setValue, setItems);
    }
}

file static class PopupExt
{
    public static void Let<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj is not null) action(obj);
    }
}