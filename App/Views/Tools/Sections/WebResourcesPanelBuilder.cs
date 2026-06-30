// =============================================================================
//  WebResourcesPanelBuilder.cs — Painel de Recursos da Web (reescrito)
// =============================================================================

using D365Assistant.Core.Models.WebResource;
using D365Assistant.ViewModels;
using D365Assistant.Views.Tools.Components;
using D365Assistant.Views.Tools.Theme;
using D365Assistant.Views.Tools.Sections.Viewer;
using D365Assistant.Views.Tools.Sections.Comparator;
using D365Assistant.Core.Services;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Tools.Sections;

public sealed class WebResourcesPanelBuilder
{
    private readonly WebResourcesViewModel _vm;
    private readonly HttpClient _http;
    private readonly IExternalAuthService _auth;
    private readonly VaultViewModel _vault;
    private readonly VaultService _vaultService;
    private WebResourceTableBuilder? _table;
    private WebResourceDetailPanel? _detail;
    private WebResourceViewerOverlay? _viewer;
    private WebResourceComparatorOverlay? _comparator;

    private string _localSearch = "";
    private int? _typeFilter = null;
    private bool? _managedFilter = null;

    private readonly Dictionary<string, Button> _filterBtns = [];
    private TextBox? _localSearchBox;

    public WebResourcesPanelBuilder(
        WebResourcesViewModel vm,
        HttpClient http,
        IExternalAuthService auth,
        VaultViewModel vault,
        VaultService vaultService)
    {
        _vm = vm;
        _http = http;
        _auth = auth;
        _vault = vault;
        _vaultService = vaultService;
        _vm.Items.CollectionChanged += (_, _) => ApplyLocalFilters();
    }

    public UIElement Build()
    {
        _viewer = new WebResourceViewerOverlay(_http, _auth);
        _comparator = new WebResourceComparatorOverlay(_http, _auth, _vault, _vaultService);

        _detail = new WebResourceDetailPanel(
            _vm,
            onViewContent: async r => await _viewer.ShowAsync(r, _vm.EnvironmentUrl),
            onCompare: r => _comparator.Show(r));

        _table = new WebResourceTableBuilder(item => _detail.Show(item));

        // Root grid: [left panel] [detail] [viewer overlay] [comparator overlay]
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = BuildLeftPanel();
        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        Grid.SetColumn(_detail.Root, 1);
        root.Children.Add(_detail.Root);

        // Overlays span full grid (col 0 + col 1)
        Grid.SetColumn(_viewer.Root, 0);
        Grid.SetColumnSpan(_viewer.Root, 2);
        root.Children.Add(_viewer.Root);

        Grid.SetColumn(_comparator.Root, 0);
        Grid.SetColumnSpan(_comparator.Root, 2);
        root.Children.Add(_comparator.Root);

        return root;
    }

    private UIElement BuildLeftPanel()
    {
        var dock = new DockPanel();

        var configCard = ToolsUiFactory.Card(margin: new Thickness(0, 0, 0, 12));
        configCard.Child = BuildConfigSection();
        DockPanel.SetDock(configCard, Dock.Top);
        dock.Children.Add(configCard);

        var stats = BuildStatsBar();
        DockPanel.SetDock(stats, Dock.Top);
        dock.Children.Add(stats);

        var filterBar = BuildLocalFiltersBar();
        DockPanel.SetDock(filterBar, Dock.Top);
        dock.Children.Add(filterBar);

        var spinner = BuildSpinner();
        DockPanel.SetDock(spinner, Dock.Top);
        dock.Children.Add(spinner);

        var statusRow = BuildStatusRow();
        DockPanel.SetDock(statusRow, Dock.Top);
        dock.Children.Add(statusRow);

        dock.Children.Add(_table!.Build());
        return dock;
    }

    private UIElement BuildConfigSection()
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = "Configurar conexão",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
            Margin = new Thickness(0, 0, 0, 12),
        });

        var urlRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        urlRow.Children.Add(ToolsUiFactory.Label("URL do ambiente *"));
        var urlBox = ToolsUiFactory.InputBox();
        urlBox.SetBinding(TextBox.TextProperty, ToolsUiFactory.Bind("EnvironmentUrl", twoWay: true));
        urlBox.ToolTip = "Ex: https://org.crm.dynamics.com";
        urlBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = _vm.SearchAsync(); };
        Grid.SetColumn(urlBox, 1);
        urlRow.Children.Add(urlBox);
        panel.Children.Add(urlRow);

        var actionRow = new Grid();
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        actionRow.Children.Add(ToolsUiFactory.Label("Tipo"));

        var (typeComboVisual, typeCombo) = BuildDarkComboBox(WebResourcesViewModel.TypeOptions);
        typeCombo.SetBinding(ComboBox.SelectedItemProperty,
            ToolsUiFactory.Bind("SelectedType", twoWay: true));
        Grid.SetColumn(typeComboVisual, 1);
        actionRow.Children.Add(typeComboVisual);

        var filterLbl = ToolsUiFactory.Label("Filtro de nome *");
        Grid.SetColumn(filterLbl, 3);
        actionRow.Children.Add(filterLbl);

        var filterBox = ToolsUiFactory.InputBox();
        filterBox.ToolTip = "Texto que o nome lógico deve conter";
        filterBox.SetBinding(TextBox.TextProperty, ToolsUiFactory.Bind("FilterText", twoWay: true, trigger: UpdateSourceTrigger.PropertyChanged));
        filterBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = _vm.SearchAsync(); };
        Grid.SetColumn(filterBox, 4);
        actionRow.Children.Add(filterBox);

        var btnSearch = ToolsUiFactory.ActionButton("🔍  Buscar", ToolsTheme.Accent);
        btnSearch.Margin = new Thickness(10, 0, 6, 0);
        btnSearch.MinWidth = 100;
        btnSearch.Command = _vm.SearchCommand;
        btnSearch.SetBinding(UIElement.IsEnabledProperty, ToolsUiFactory.Bind("IsBusy", converter: new InverseBoolConverter()));
        Grid.SetColumn(btnSearch, 5);
        actionRow.Children.Add(btnSearch);

        var btnClear = ToolsUiFactory.ActionButton("✕  Limpar", ToolsTheme.Surface2);
        btnClear.MinWidth = 76;
        btnClear.Command = _vm.ClearResultsCommand;
        Grid.SetColumn(btnClear, 6);
        actionRow.Children.Add(btnClear);

        panel.Children.Add(actionRow);
        panel.Children.Add(new TextBlock
        {
            Text = "ℹ  A URL pode ser diferente do appsettings.json — útil para outros ambientes.",
            FontSize = 10,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextMuted),
            Margin = new Thickness(0, 8, 0, 0),
        });

        return panel;
    }

    private UIElement BuildStatsBar()
    {
        var border = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 10),
        };
        border.SetBinding(UIElement.VisibilityProperty,
            ToolsUiFactory.Bind("HasResults", converter: new BoolToVisibilityConverter()));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(StatChip("Total", "TotalCount", ToolsTheme.Text, null));
        row.Children.Add(ToolsUiFactory.VerticalSeparator());
        row.Children.Add(StatChip("JavaScript", "CountJs", ToolsTheme.Yellow, 3));
        row.Children.Add(ToolsUiFactory.VerticalSeparator());
        row.Children.Add(StatChip("HTML", "CountHtml", ToolsTheme.Blue, 1));
        row.Children.Add(ToolsUiFactory.VerticalSeparator());
        row.Children.Add(StatChip("CSS", "CountCss", ToolsTheme.Purple, 2));
        row.Children.Add(ToolsUiFactory.VerticalSeparator());
        row.Children.Add(StatChip("Outros", "CountOther", ToolsTheme.Gray, -1));
        border.Child = row;
        return border;
    }

    private UIElement StatChip(string label, string bindingPath, Color color, int? typeCode)
    {
        var sp = new StackPanel
        {
            Margin = new Thickness(0, 0, 14, 0),
            Cursor = typeCode != null ? Cursors.Hand : Cursors.Arrow,
        };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = ToolsTheme.Brush(ToolsTheme.TextMuted) });
        var num = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(color) };
        num.SetBinding(TextBlock.TextProperty, ToolsUiFactory.Bind(bindingPath));
        sp.Children.Add(num);

        if (typeCode != null)
        {
            sp.MouseLeftButtonUp += (_, _) =>
            {
                _typeFilter = _typeFilter == typeCode ? null
                              : typeCode == -1 ? -999
                              : typeCode;
                ApplyLocalFilters();
            };
        }
        return sp;
    }

    private UIElement BuildLocalFiltersBar()
    {
        var border = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 8, 12, 8),
        };
        border.SetBinding(UIElement.VisibilityProperty,
            ToolsUiFactory.Bind("HasResults", converter: new BoolToVisibilityConverter()));

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        border.Child = g;

        // Search box local
        var searchWrap = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Bg),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 0, 8, 0),
            Height = 30,
            Width = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var sRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sRow.Children.Add(new TextBlock { Text = "⌕", FontSize = 12, Foreground = ToolsTheme.Brush(ToolsTheme.TextSub), Margin = new Thickness(0, 0, 6, 0) });
        _localSearchBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
            FontSize = 11,
            Width = 180,
        };
        _localSearchBox.TextChanged += (_, _) => { _localSearch = _localSearchBox.Text; ApplyLocalFilters(); };
        sRow.Children.Add(_localSearchBox);
        searchWrap.Child = sRow;
        Grid.SetColumn(searchWrap, 0);
        g.Children.Add(searchWrap);

        // Managed filter pills
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(right, 1);
        g.Children.Add(right);

        right.Children.Add(new TextBlock
        {
            Text = "Solução:",
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        foreach (var (lbl, val) in new (string, bool?)[]
        {
            ("Todos", null), ("Gerenciado", true), ("Não gerenciado", false),
        })
        {
            var v = val;
            var btn = FilterPill(lbl);
            btn.Click += (_, _) => { _managedFilter = v; UpdatePillHighlights(); ApplyLocalFilters(); };
            _filterBtns[lbl] = btn;
            right.Children.Add(btn);
        }

        UpdatePillHighlights();
        return border;
    }

    private static Button FilterPill(string label) => new()
    {
        Content = label,
        FontSize = 11,
        Background = ToolsTheme.Brush(ToolsTheme.Surface2),
        Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
        BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(10, 4, 10, 4),
        Margin = new Thickness(4, 0, 0, 0),
    };

    private void UpdatePillHighlights()
    {
        foreach (var (lbl, btn) in _filterBtns)
        {
            var active = lbl switch
            {
                "Todos" => _managedFilter == null,
                "Gerenciado" => _managedFilter == true,
                "Não gerenciado" => _managedFilter == false,
                _ => false,
            };
            btn.Background = active ? new SolidColorBrush(Color.FromArgb(0x22, ToolsTheme.Accent.R, ToolsTheme.Accent.G, ToolsTheme.Accent.B)) : ToolsTheme.Brush(ToolsTheme.Surface2);
            btn.Foreground = active ? ToolsTheme.Brush(ToolsTheme.Accent) : ToolsTheme.Brush(ToolsTheme.TextSub);
            btn.BorderBrush = active ? ToolsTheme.Brush(ToolsTheme.Accent) : ToolsTheme.Brush(ToolsTheme.Border);
        }
    }

    private UIElement BuildStatusRow()
    {
        var g = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusTb = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        statusTb.SetBinding(TextBlock.TextProperty, ToolsUiFactory.Bind("StatusText"));
        statusTb.SetBinding(TextBlock.ForegroundProperty, ToolsUiFactory.Bind("StatusColor", converter: new ColorStringToBrushConverter()));
        g.Children.Add(statusTb);

        var countTb = new TextBlock
        {
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextMuted),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        countTb.SetBinding(TextBlock.TextProperty, ToolsUiFactory.Bind("TotalCount", stringFormat: "{0} item(s)"));
        Grid.SetColumn(countTb, 1);
        g.Children.Add(countTb);
        return g;
    }

    private UIElement BuildSpinner()
    {
        var tb = new TextBlock
        {
            Text = "⏳  Buscando...",
            FontSize = 13,
            Foreground = ToolsTheme.Brush(ToolsTheme.Blue),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
        };
        tb.SetBinding(UIElement.VisibilityProperty, ToolsUiFactory.Bind("IsBusy", converter: new BoolToVisibilityConverter()));
        return tb;
    }

    private void ApplyLocalFilters()
    {
        if (_table == null) return;

        var filtered = _vm.Items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_localSearch))
        {
            var q = _localSearch.Trim().ToLower();
            filtered = filtered.Where(r =>
                r.Name.ToLower().Contains(q) ||
                r.DisplayName.ToLower().Contains(q));
        }

        if (_typeFilter != null)
            filtered = _typeFilter == -999
                ? filtered.Where(r => r.TypeCode is not (1 or 2 or 3))
                : filtered.Where(r => r.TypeCode == _typeFilter);

        if (_managedFilter != null)
            filtered = filtered.Where(r => r.IsManaged == _managedFilter);

        _table.Populate(filtered);
    }

    // ── Dark ComboBox simulado com Border + Popup ─────────────────────────────
    // Abordagem: não usa ComboBox nativo (template impossível de customizar por código).
    // Um Border abre um Popup manualmente, e um ComboBox oculto mantém o binding MVVM.

    private static (UIElement visual, ComboBox hiddenCombo) BuildDarkComboBox(
        IEnumerable<object> items)
    {
        var itemList = items.ToList();

        // ComboBox oculto — só existe para o binding MVVM funcionar
        var hiddenCombo = new ComboBox
        {
            ItemsSource = itemList,
            SelectedIndex = 0,
            Visibility = Visibility.Collapsed,
        };

        // Texto do item selecionado
        var selectedTb = new TextBlock
        {
            FontSize = 12,
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = itemList.FirstOrDefault()?.ToString() ?? "",
        };

        var arrow = new TextBlock
        {
            Text = "⌄",
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        var innerGrid = new Grid();
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        innerGrid.Children.Add(selectedTb); Grid.SetColumn(selectedTb, 0);
        innerGrid.Children.Add(arrow); Grid.SetColumn(arrow, 1);

        var host = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 34,
            Cursor = Cursors.Hand,
            Child = innerGrid,
        };

        // Popup com lista de itens
        var itemsStack = new StackPanel
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
        };

        var popup = new Popup
        {
            AllowsTransparency = true,
            StaysOpen = false,
            PlacementTarget = host,
            Placement = PlacementMode.Bottom,
            MinWidth = 180,
            Child = new WpfBorder
            {
                Background = ToolsTheme.Brush(ToolsTheme.Surface),
                BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0, 0, 6, 6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    Opacity = 0.4,
                    ShadowDepth = 4,
                },
                Child = new ScrollViewer
                {
                    MaxHeight = 220,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = itemsStack,
                },
            },
        };

        popup.Opened += (_, _) =>
        {
            popup.MinWidth = host.ActualWidth > 0 ? host.ActualWidth : 180;
            arrow.Text = "⌃";
            host.BorderBrush = ToolsTheme.Brush(ToolsTheme.Accent);
        };
        popup.Closed += (_, _) =>
        {
            arrow.Text = "⌄";
            host.BorderBrush = ToolsTheme.Brush(ToolsTheme.Border);
        };

        // Preenche itens no popup
        foreach (var item in itemList)
        {
            var i = item;
            var row = new WpfBorder
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(12, 8, 12, 8),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = item.ToString(),
                    FontSize = 12,
                    Foreground = ToolsTheme.Brush(ToolsTheme.Text),
                },
            };
            row.MouseEnter += (_, _) =>
                row.Background = ToolsTheme.Brush(ToolsTheme.Surface2);
            row.MouseLeave += (_, _) =>
                row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (_, _) =>
            {
                selectedTb.Text = i.ToString();
                hiddenCombo.SelectedItem = i;
                popup.IsOpen = false;
            };
            itemsStack.Children.Add(row);
        }

        host.MouseLeftButtonUp += (_, _) =>
            popup.IsOpen = !popup.IsOpen;

        // Wrapper que contém visual + hiddenCombo
        var wrapper = new StackPanel { Orientation = Orientation.Vertical };
        wrapper.Children.Add(host);
        wrapper.Children.Add(hiddenCombo);

        return (wrapper, hiddenCombo);
    }
}