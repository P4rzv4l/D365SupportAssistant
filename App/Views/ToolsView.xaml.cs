using D365Assistant.Core.Models.WebResource;
using D365Assistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

public partial class ToolsView : Page
{
    private readonly WebResourcesViewModel _vm;

    private Button _tabFlows = null!;
    private Button _tabWebRes = null!;
    private Border _panelFlows = null!;
    private Border _panelWebRes = null!;

    public ToolsView(WebResourcesViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        Title = "Ferramentas";
        Background = Brush("#0D1117");

        var root = new DockPanel { Margin = new Thickness(24, 20, 24, 20) };

        // ── Cabeçalho ────────────────────────────────────────────────────────
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        header.Children.Add(new TextBlock
        {
            Text = "🛠️  Ferramentas",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#E6EDF3"),
        });
        header.Children.Add(new TextBlock
        {
            Text = "Utilitários para interagir diretamente com o ambiente Dynamics 365.",
            FontSize = 12,
            Foreground = Brush("#484F58"),
            Margin = new Thickness(0, 4, 0, 0),
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Tab bar ───────────────────────────────────────────────────────────
        var tabBar = new StackPanel { Orientation = Orientation.Horizontal };

        _tabFlows = MakeTabBtn("⚡  Fluxos", active: false);
        _tabWebRes = MakeTabBtn("🌐  Recursos da Web", active: true);

        _tabFlows.Click += (_, _) => SwitchTab(showFlows: true);
        _tabWebRes.Click += (_, _) => SwitchTab(showFlows: false);

        tabBar.Children.Add(_tabFlows);
        tabBar.Children.Add(_tabWebRes);

        var tabDivider = new Border
        {
            BorderBrush = Brush("#21262D"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 0, 0, 20),
        };
        tabDivider.Child = tabBar;
        DockPanel.SetDock(tabDivider, Dock.Top);
        root.Children.Add(tabDivider);

        // ── Painel: Fluxos (placeholder) ──────────────────────────────────────
        _panelFlows = new Border { Visibility = Visibility.Collapsed };
        _panelFlows.Child = BuildFlowsPlaceholder();
        DockPanel.SetDock(_panelFlows, Dock.Top);
        root.Children.Add(_panelFlows);

        // ── Painel: Recursos da Web ───────────────────────────────────────────
        _panelWebRes = new Border { Visibility = Visibility.Visible };
        _panelWebRes.Child = BuildWebResourcesPanel();
        root.Children.Add(_panelWebRes);

        Content = root;
    }

    // =========================================================================
    //  PAINEL: Fluxos (placeholder)
    // =========================================================================

    private static UIElement BuildFlowsPlaceholder()
    {
        var card = Card();
        var inner = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 60, 0, 60),
        };
        inner.Children.Add(new TextBlock
        {
            Text = "⚡",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
        });
        inner.Children.Add(new TextBlock
        {
            Text = "Ferramenta de Fluxos",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#E6EDF3"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        inner.Children.Add(new TextBlock
        {
            Text = "Em breve — esta aba receberá as ferramentas de análise de fluxos.",
            FontSize = 13,
            Foreground = Brush("#484F58"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            TextAlignment = TextAlignment.Center,
        });
        card.Child = inner;
        return card;
    }

    // =========================================================================
    //  PAINEL: Recursos da Web
    // =========================================================================

    private UIElement BuildWebResourcesPanel()
    {
        var root = new DockPanel();

        // ── 1. Card de configuração ────────────────────────────────────────────
        var configCard = Card();
        configCard.Margin = new Thickness(0, 0, 0, 14);
        configCard.Child = BuildConfigSection();
        DockPanel.SetDock(configCard, Dock.Top);
        root.Children.Add(configCard);

        // ── 2. Barra de stats ──────────────────────────────────────────────────
        var statsBar = BuildStatsBar();
        DockPanel.SetDock(statsBar, Dock.Top);
        root.Children.Add(statsBar);

        // ── 3. Status + contador ───────────────────────────────────────────────
        var searchRow = BuildSearchRow();
        DockPanel.SetDock(searchRow, Dock.Top);
        root.Children.Add(searchRow);

        // ── 4. Spinner ─────────────────────────────────────────────────────────
        var spinner = new TextBlock
        {
            Text = "⏳  Buscando...",
            FontSize = 13,
            Foreground = Brush("#58A6FF"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
        };
        spinner.SetBinding(UIElement.VisibilityProperty,
            Bind("IsBusy", converter: new BoolToVisibilityConverter()));
        DockPanel.SetDock(spinner, Dock.Top);
        root.Children.Add(spinner);

        // ── 5. Lista ───────────────────────────────────────────────────────────
        root.Children.Add(BuildListView());

        return root;
    }

    // ── Seção de configuração ─────────────────────────────────────────────────

    private UIElement BuildConfigSection()
    {
        var panel = new StackPanel { Margin = new Thickness(0) };

        panel.Children.Add(new TextBlock
        {
            Text = "1.  Configurar conexão",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#E6EDF3"),
            Margin = new Thickness(0, 0, 0, 12),
        });

        // ── Linha 1: URL do ambiente ───────────────────────────────────────────
        var urlRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        urlRow.Children.Add(LabelFor("URL do ambiente *"));

        var urlBox = InputBox();
        urlBox.SetBinding(TextBox.TextProperty, Bind("EnvironmentUrl", twoWay: true));
        urlBox.ToolTip = "Ex: https://org.crm.dynamics.com";
        urlBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = _vm.SearchAsync(); };
        Grid.SetColumn(urlBox, 1);
        urlRow.Children.Add(urlBox);

        panel.Children.Add(urlRow);

        // ── Linha 2: Tipo + Filtro + Botões ───────────────────────────────────
        var actionRow = new Grid();
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        actionRow.Children.Add(LabelFor("Tipo de recurso"));

        var typeCombo = new ComboBox
        {
            Background = Brush("#161B22"),
            Foreground = Brush("#E6EDF3"),
            BorderBrush = Brush("#30363D"),
            BorderThickness = new Thickness(1),
            FontSize = 13,
            Padding = new Thickness(10, 7, 10, 7),
            ItemsSource = WebResourcesViewModel.TypeOptions,
            IsEditable = false,
        };
        typeCombo.SetBinding(ComboBox.SelectedItemProperty, Bind("SelectedType", twoWay: true));
        Grid.SetColumn(typeCombo, 1);
        actionRow.Children.Add(typeCombo);

        var filterLbl = LabelFor("Filtro de nome *");
        Grid.SetColumn(filterLbl, 3);
        actionRow.Children.Add(filterLbl);

        var filterBox = InputBox();
        filterBox.ToolTip = "Texto que o nome lógico deve conter (ex: bz, contoso)";
        filterBox.SetBinding(TextBox.TextProperty, Bind("FilterText", twoWay: true,
            trigger: System.Windows.Data.UpdateSourceTrigger.PropertyChanged));
        filterBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = _vm.SearchAsync(); };
        Grid.SetColumn(filterBox, 4);
        actionRow.Children.Add(filterBox);

        var btnSearch = ActionButton("🔍  Buscar", "#7C3AED");
        btnSearch.Margin = new Thickness(10, 0, 6, 0);
        btnSearch.MinWidth = 110;
        btnSearch.Command = _vm.SearchCommand;
        btnSearch.SetBinding(Button.IsEnabledProperty, Bind("IsBusy", converter: new InverseBoolConverter()));
        Grid.SetColumn(btnSearch, 5);
        actionRow.Children.Add(btnSearch);

        var btnClear = ActionButton("✕  Limpar", "#21262D");
        btnClear.MinWidth = 80;
        btnClear.ToolTip = "Limpar resultados";
        btnClear.Command = _vm.ClearResultsCommand;
        Grid.SetColumn(btnClear, 6);
        actionRow.Children.Add(btnClear);

        panel.Children.Add(actionRow);

        panel.Children.Add(new TextBlock
        {
            Text = "ℹ  A URL pode ser diferente do appsettings.json — útil para consultar outros ambientes (sandbox, produção…).",
            FontSize = 11,
            Foreground = Brush("#484F58"),
            Margin = new Thickness(0, 10, 0, 0),
        });

        return panel;
    }

    // ── Barra de estatísticas ─────────────────────────────────────────────────

    private UIElement BuildStatsBar()
    {
        var border = new Border
        {
            Background = Brush("#161B22"),
            BorderBrush = Brush("#30363D"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 0, 12),
        };
        border.SetBinding(UIElement.VisibilityProperty,
            Bind("HasResults", converter: new BoolToVisibilityConverter()));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(StatChip("Total", "TotalCount", "#E6EDF3"));
        row.Children.Add(StatSep());
        row.Children.Add(StatChip("JavaScript", "CountJs", "#F0DB4F"));
        row.Children.Add(StatSep());
        row.Children.Add(StatChip("HTML", "CountHtml", "#58A6FF"));
        row.Children.Add(StatSep());
        row.Children.Add(StatChip("CSS", "CountCss", "#B392F0"));
        row.Children.Add(StatSep());
        row.Children.Add(StatChip("Outros", "CountOther", "#8B949E"));

        border.Child = row;
        return border;
    }

    private UIElement StatChip(string label, string binding, string color)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = Brush("#484F58") });
        var num = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brush(color) };
        num.SetBinding(TextBlock.TextProperty, Bind(binding));
        sp.Children.Add(num);
        return sp;
    }

    private static UIElement StatSep() =>
        new Border { Width = 1, Background = Brush("#21262D"), Margin = new Thickness(0, 2, 18, 2) };

    // ── Status + contador ─────────────────────────────────────────────────────

    private UIElement BuildSearchRow()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusTxt = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        statusTxt.SetBinding(TextBlock.TextProperty, Bind("StatusText"));
        statusTxt.SetBinding(TextBlock.ForegroundProperty, Bind("StatusColor",
            converter: new ColorStringToBrushConverter()));
        Grid.SetColumn(statusTxt, 0);
        grid.Children.Add(statusTxt);

        var countTxt = new TextBlock
        {
            FontSize = 11,
            Foreground = Brush("#484F58"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        countTxt.SetBinding(TextBlock.TextProperty, Bind("TotalCount", stringFormat: "{0} item(s)"));
        Grid.SetColumn(countTxt, 1);
        grid.Children.Add(countTxt);

        return grid;
    }

    // ── Lista de resultados ───────────────────────────────────────────────────

    private ListView BuildListView()
    {
        var lv = new ListView
        {
            Background = Brush("#161B22"),
            BorderBrush = Brush("#30363D"),
            BorderThickness = new Thickness(1),
            Foreground = Brush("#E6EDF3"),
            FontSize = 12,
        };

        var gv = new GridView();
        gv.Columns.Add(Col("Nome lógico", "Name", 300));
        gv.Columns.Add(Col("Display Name", "DisplayName", 200));
        gv.Columns.Add(Col("Tipo", "TypeLabel", 90));
        gv.Columns.Add(Col("Solução", "ManagedLabel", 120));
        gv.Columns.Add(Col("Modificado", "ModifiedOnFormatted", 140));

        lv.View = gv;
        lv.ItemsSource = _vm.Items;

        lv.MouseDoubleClick += (_, _) =>
        {
            if (lv.SelectedItem is WebResource item)
                _vm.CopyNameCommand.Execute(item);
        };

        var menu = new ContextMenu();

        var miCopyName = new MenuItem { Header = "📋  Copiar nome lógico" };
        miCopyName.Click += (_, _) =>
        {
            if (lv.SelectedItem is WebResource item) _vm.CopyNameCommand.Execute(item);
        };

        var miCopyId = new MenuItem { Header = "🔑  Copiar ID" };
        miCopyId.Click += (_, _) =>
        {
            if (lv.SelectedItem is WebResource item) _vm.CopyIdCommand.Execute(item);
        };

        menu.Items.Add(miCopyName);
        menu.Items.Add(miCopyId);
        lv.ContextMenu = menu;

        return lv;
    }

    // =========================================================================
    //  Tab switching
    // =========================================================================

    private void SwitchTab(bool showFlows)
    {
        _panelFlows.Visibility = showFlows ? Visibility.Visible : Visibility.Collapsed;
        _panelWebRes.Visibility = !showFlows ? Visibility.Visible : Visibility.Collapsed;
        SetTabActive(_tabFlows, showFlows);
        SetTabActive(_tabWebRes, !showFlows);
    }

    private static void SetTabActive(Button btn, bool active)
    {
        btn.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        btn.Foreground = active ? Brush("#E6EDF3") : Brush("#8B949E");
        if (btn.Content is Border bd)
            bd.BorderBrush = active ? Brush("#7C3AED") : Brushes.Transparent;
    }

    // =========================================================================
    //  Helpers de UI
    // =========================================================================

    private static Button MakeTabBtn(string text, bool active)
    {
        var indicator = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 2),
            BorderBrush = active ? Brush("#7C3AED") : Brushes.Transparent,
            Padding = new Thickness(16, 10, 16, 10),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = active ? Brush("#E6EDF3") : Brush("#8B949E"),
            },
        };
        return new Button
        {
            Content = indicator,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(0),
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = active ? Brush("#E6EDF3") : Brush("#8B949E"),
        };
    }

    private static Button ActionButton(string text, string bgHex) => new()
    {
        Content = text,
        Background = Brush(bgHex),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Cursor = Cursors.Hand,
        Padding = new Thickness(16, 9, 16, 9),
    };

    private static Border Card() => new()
    {
        Background = Brush("#161B22"),
        BorderBrush = Brush("#30363D"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(20, 16, 20, 16),
    };

    private static TextBox InputBox() => new()
    {
        Background = Brush("#0D1117"),
        Foreground = Brush("#E6EDF3"),
        CaretBrush = Brush("#E6EDF3"),
        BorderBrush = Brush("#30363D"),
        BorderThickness = new Thickness(1),
        FontSize = 13,
        FontFamily = new FontFamily("Consolas"),
        Padding = new Thickness(10, 7, 10, 7),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private static TextBlock LabelFor(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = Brush("#8B949E"),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 10, 0),
    };

    private static GridViewColumn Col(string header, string binding, double width) => new()
    {
        Header = header,
        Width = width,
        DisplayMemberBinding = new System.Windows.Data.Binding(binding),
    };

    private static System.Windows.Data.Binding Bind(
        string path,
        bool twoWay = false,
        System.Windows.Data.IValueConverter? converter = null,
        string? stringFormat = null,
        System.Windows.Data.UpdateSourceTrigger trigger =
            System.Windows.Data.UpdateSourceTrigger.Default)
    {
        var b = new System.Windows.Data.Binding(path)
        {
            Mode = twoWay
                ? System.Windows.Data.BindingMode.TwoWay
                : System.Windows.Data.BindingMode.OneWay,
            UpdateSourceTrigger = trigger,
        };
        if (converter is not null) b.Converter = converter;
        if (stringFormat is not null) b.StringFormat = stringFormat;
        return b;
    }

    private static SolidColorBrush Brush(string hex)
    {
        var c = (System.Windows.Media.Color)
            System.Windows.Media.ColorConverter.ConvertFromString(hex);
        return new SolidColorBrush(c);
    }
}

// ── Converters ────────────────────────────────────────────────────────────────

public class InverseBoolConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
        => v is bool b && !b;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
        => v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotImplementedException();
}