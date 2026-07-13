// =============================================================================
//  FlowsPanelBuilder.cs — Painel do Analisador de Fluxos (aba dentro de Tools)
// =============================================================================

using D365Assistant.Core.Models.Flows;
using D365Assistant.ViewModels;
using D365Assistant.Views.Dashboard.Theme;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Flows.Sections;

public sealed class FlowsPanelBuilder
{
    private readonly FlowsViewModel _vm;

    private StackPanel? _tableBody;
    private TextBlock? _totalTb;
    private TextBlock? _statusTb;
    private FlowDetailPanel? _detail;

    private WorkflowItem? _selected;
    private readonly Dictionary<string, WpfBorder> _rowMap = [];

    public FlowsPanelBuilder(FlowsViewModel vm)
    {
        _vm = vm;
        _vm.Items.CollectionChanged += (_, _) =>
            Application.Current.Dispatcher.Invoke(RenderTable);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_vm.StatusText) or nameof(_vm.IsBusy)
                               or nameof(_vm.TotalCount) or nameof(_vm.StatusColor))
                Application.Current.Dispatcher.Invoke(UpdateStatusBar);
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BUILD
    // ══════════════════════════════════════════════════════════════════════════

    public UIElement Build()
    {
        _detail = new FlowDetailPanel(
            onClose: CloseDetail,
            connectedUrl: _vm.ConnectedUrl);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = BuildLeftPanel();
        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        Grid.SetColumn(_detail.Root, 1);
        root.Children.Add(_detail.Root);

        return root;
    }

    // ── Left panel ────────────────────────────────────────────────────────────

    private UIElement BuildLeftPanel()
    {
        var dock = new DockPanel();

        var connBar = BuildConnectionBar();
        DockPanel.SetDock(connBar, Dock.Top);
        dock.Children.Add(connBar);

        var filterBar = BuildFilterBar();
        DockPanel.SetDock(filterBar, Dock.Top);
        dock.Children.Add(filterBar);

        var statusBar = BuildStatusBar();
        DockPanel.SetDock(statusBar, Dock.Top);
        dock.Children.Add(statusBar);

        var hdr = BuildTableHeader();
        DockPanel.SetDock(hdr, Dock.Top);
        dock.Children.Add(hdr);

        dock.Children.Add(BuildTableBody());
        return dock;
    }

    // ── Connection bar ────────────────────────────────────────────────────────

    private UIElement BuildConnectionBar()
    {
        var bar = SurfaceBar(true, new Thickness(16, 10, 16, 10));
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = g;

        var urlBox = DarkTextBox("https://org.crm.dynamics.com", height: 34);
        urlBox.TextChanged += (_, _) => _vm.EnvironmentUrl = urlBox.Text;
        urlBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = _vm.FetchAsync(); };
        Grid.SetColumn(urlBox, 0);
        g.Children.Add(urlBox);

        var (typeHost, _, _, setTypeItems) = Dropdown(val =>
        {
            var opt = FlowsViewModel.TypeOptions.FirstOrDefault(o => o.Label == val);
            if (opt != null) _vm.SelectedType = opt;
        });
        setTypeItems(FlowsViewModel.TypeOptions.Select(o => o.Label).ToList());
        typeHost.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(typeHost, 1);
        g.Children.Add(typeHost);

        var btnFetch = PrimaryButton("⚡ Buscar");
        btnFetch.Click += async (_, _) => await _vm.FetchAsync();
        Grid.SetColumn(btnFetch, 2);
        g.Children.Add(btnFetch);

        var btnClear = OutlineButton("✕ Limpar");
        btnClear.Click += (_, _) =>
        {
            _vm.ClearResultsCommand.Execute(null);
            _tableBody?.Children.Clear();
            _rowMap.Clear();
            _detail?.Hide();
            UpdateStatusBar();
        };
        Grid.SetColumn(btnClear, 3);
        g.Children.Add(btnClear);

        return bar;
    }

    // ── Filter bar ────────────────────────────────────────────────────────────

    private UIElement BuildFilterBar()
    {
        var bar = SurfaceBar(true, new Thickness(16, 8, 16, 8));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Child = row;

        row.Children.Add(new TextBlock
        {
            Text = "Pesquisar:",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var box1 = DarkTextBox("Termo 1...", width: 150, height: 28);
        box1.TextChanged += (_, _) => _vm.SearchTerm1 = box1.Text;
        row.Children.Add(box1);

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
            ToolTip = "Alternar AND / OR",
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
        row.Children.Add(toggleAnd);

        var box2 = DarkTextBox("Termo 2...", width: 150, height: 28);
        box2.TextChanged += (_, _) => _vm.SearchTerm2 = box2.Text;
        row.Children.Add(box2);

        var (stHost, _, _, setStItems) = Dropdown(val => _vm.StatusFilter = val);
        setStItems(["Todos", "Ativo", "Inativo"]);
        stHost.Width = 100;
        stHost.Margin = new Thickness(16, 0, 0, 0);
        row.Children.Add(stHost);

        var (sortHost, _, _, setSortItems) = Dropdown(val => _vm.SortOrder = val);
        setSortItems(["A-Z", "Z-A"]);
        sortHost.Width = 72;
        sortHost.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(sortHost);

        var httpsCheck = new CheckBox
        {
            Content = "Só HTTPS",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
            Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed,
        };
        httpsCheck.Checked += (_, _) => _vm.OnlyHttps = true;
        httpsCheck.Unchecked += (_, _) => _vm.OnlyHttps = false;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.HasCloudFlows))
                Application.Current.Dispatcher.Invoke(() =>
                    httpsCheck.Visibility = _vm.HasCloudFlows
                        ? Visibility.Visible : Visibility.Collapsed);
        };
        row.Children.Add(httpsCheck);

        var btnExport = OutlineButton("⬇ Exportar");
        btnExport.Click += (_, _) => _vm.ExportJsonCommand.Execute(null);
        btnExport.Margin = new Thickness(16, 0, 0, 0);
        row.Children.Add(btnExport);

        return bar;
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private UIElement BuildStatusBar()
    {
        var bar = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 6, 16, 6),
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Child = row;

        _totalTb = new TextBlock
        {
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };
        row.Children.Add(_totalTb);

        _statusTb = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(_statusTb);

        row.Children.Add(new WpfBorder
        {
            Width = 1,
            Background = DashboardTheme.Brush(DashboardTheme.Border),
            Margin = new Thickness(16, 2, 16, 2),
        });

        void StatChip(string label, Func<int> get, Color color)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            var num = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(_vm.CountActive) or nameof(_vm.CountInactive)
                                   or nameof(_vm.CountCloud) or nameof(_vm.CountClassic)
                                   or nameof(_vm.CountRules))
                    Application.Current.Dispatcher.Invoke(() => num.Text = get().ToString());
            };
            sp.Children.Add(num);
            row.Children.Add(sp);
        }

        StatChip("Ativos:", () => _vm.CountActive, DashboardTheme.Green);
        StatChip("Inativos:", () => _vm.CountInactive, DashboardTheme.TextSub);
        StatChip("Cloud:", () => _vm.CountCloud, DashboardTheme.Accent);
        StatChip("Clássicos:", () => _vm.CountClassic, DashboardTheme.Yellow);
        StatChip("Regras:", () => _vm.CountRules, DashboardTheme.Purple);

        return bar;
    }

    // ── Table header ──────────────────────────────────────────────────────────

    private static UIElement BuildTableHeader()
    {
        var hdr = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var g = ColGrid();
        hdr.Child = g;

        void H(int col, string text, bool left = false)
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

        H(0, "Nome", left: true);
        H(1, "Categoria");
        H(2, "Proprietário");
        H(3, "Status");
        H(4, "Gatilho");
        return hdr;
    }

    // ── Table body ────────────────────────────────────────────────────────────

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
    //  RENDER
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderTable()
    {
        if (_tableBody == null) return;
        _tableBody.Children.Clear();
        _rowMap.Clear();

        if (_vm.Items.Count == 0)
        {
            _tableBody.Children.Add(new TextBlock
            {
                Text = _vm.IsBusy ? "⏳ Buscando..." : "Nenhum fluxo encontrado.",
                FontSize = 13,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0),
            });
            UpdateStatusBar();
            return;
        }

        var rowBuilder = new FlowRowBuilder(
            onSelect: SelectRow,
            onCopyId: item => Clipboard.SetText(item.WorkflowId),
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
            _totalTb.Text = _vm.HasResults ? $"{_vm.TotalCount} fluxo(s)" : "";

        if (_statusTb == null) return;
        _statusTb.Text = _vm.IsBusy ? "⏳ Buscando..." : _vm.StatusText;
        try
        {
            _statusTb.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_vm.StatusColor));
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static Grid ColGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        return g;
    }

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

    private static Button PrimaryButton(string label) => new()
    {
        Content = label,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Background = DashboardTheme.Brush(DashboardTheme.Accent),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Padding = new Thickness(16, 0, 16, 0),
        Height = 34,
        Margin = new Thickness(8, 0, 0, 0),
    };

    private static Button OutlineButton(string label) => new()
    {
        Content = label,
        FontSize = 11,
        Background = DashboardTheme.Brush(DashboardTheme.Surface2),
        Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(10, 0, 10, 0),
        Height = 34,
        Margin = new Thickness(6, 0, 0, 0),
    };

    private static WpfBorder SurfaceBar(bool bottomBorder, Thickness? padding = null) => new()
    {
        Background = DashboardTheme.Brush(DashboardTheme.Surface),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = bottomBorder ? new Thickness(0, 0, 0, 1) : new Thickness(0),
        Padding = padding ?? new Thickness(0),
    };

    private static (WpfBorder host, Func<string?> getValue,
                    Action<string> setValue, Action<List<string>> setItems)
        Dropdown(Action<string>? onSelected = null)
    {
        var items = new List<string>(); var selectedIdx = 0; Popup? popup = null;

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

        host.MouseEnter += (_, _) => host.Background = DashboardTheme.Brush(DashboardTheme.Surface3);
        host.MouseLeave += (_, _) => host.Background = DashboardTheme.Brush(DashboardTheme.Surface2);
        host.MouseLeftButtonUp += (_, _) =>
        {
            popup?.Let(p => p.IsOpen = false);
            var list = new StackPanel { Background = DashboardTheme.Brush(DashboardTheme.Surface) };
            for (int i = 0; i < items.Count; i++)
            {
                var idx = i; var item = items[i];
                var r = new WpfBorder
                {
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = idx == selectedIdx ? DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x30) : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = item,
                        FontSize = 11,
                        Foreground = idx == selectedIdx ? DashboardTheme.Brush(DashboardTheme.Purple) : DashboardTheme.Brush(DashboardTheme.Text),
                    },
                };
                r.MouseEnter += (_, _) => { if (idx != selectedIdx) r.Background = DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x18); };
                r.MouseLeave += (_, _) => { r.Background = idx == selectedIdx ? DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x30) : Brushes.Transparent; };
                r.MouseLeftButtonUp += (_, _) => { selectedIdx = idx; selectedTb.Text = item; popup?.Let(p => p.IsOpen = false); onSelected?.Invoke(item); };
                list.Children.Add(r);
            }
            popup = new Popup
            {
                Child = new WpfBorder
                {
                    Child = list,
                    Background = DashboardTheme.Brush(DashboardTheme.Surface),
                    BorderBrush = DashboardTheme.Brush(DashboardTheme.Border2),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 12, Opacity = 0.4, ShadowDepth = 4 },
                },
                PlacementTarget = host,
                Placement = PlacementMode.Bottom,
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

file static class FlowsPopupExt
{
    public static void Let<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj is not null) action(obj);
    }
}