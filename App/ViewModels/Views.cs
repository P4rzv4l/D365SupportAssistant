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

    public TrackerView(TrackerViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        Title = "Time Tracker";
        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17));

        var grid = new Grid { Margin = new Thickness(20) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        // ── Coluna esquerda: cronômetro ────────────────────────────────────────
        var leftCard = MakeCard();
        Grid.SetColumn(leftCard, 0);

        var left = new StackPanel { Margin = new Thickness(20) };
        leftCard.Child = left;

        // Status pill
        var pill = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 4, 16, 4),
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var pillText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold,
        };
        pill.Child = pillText;
        left.Children.Add(pill);

        // Bind pill to VM
        void UpdatePill()
        {
            pill.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(
                    _vm.IsRunning ? "#0F2A1A" : "#2D0A0A"));
            pillText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_vm.StatusPillColor));
            pillText.Text = _vm.StatusPill;
        }
        _vm.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(_vm.StatusPill) or nameof(_vm.IsRunning))
                Dispatcher.Invoke(UpdatePill);
        };
        UpdatePill();

        // Cronômetro
        var timerText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 44,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var timerBinding = new System.Windows.Data.Binding("TimerDisplay") { Source = _vm };
        timerText.SetBinding(TextBlock.TextProperty, timerBinding);
        var timerColorBinding = new System.Windows.Data.Binding("TimerColor")
        {
            Source = _vm,
            Converter = new ColorStringToBrushConverter(),
        };
        timerText.SetBinding(TextBlock.ForegroundProperty, timerColorBinding);
        left.Children.Add(timerText);

        // Ticket ativo
        var ticketLbl = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 4),
        };
        var ticketBinding = new System.Windows.Data.Binding("ActiveTicket") { Source = _vm };
        ticketLbl.SetBinding(TextBlock.TextProperty, ticketBinding);
        left.Children.Add(ticketLbl);

        left.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            Margin = new Thickness(0, 12, 0, 12)
        });

        // Input ticket
        var inputLbl = new TextBlock
        {
            Text = "CHAMADO",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x4F, 0x58)),
            Margin = new Thickness(0, 0, 0, 4),
        };
        left.Children.Add(inputLbl);

        var input = new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            FontSize = 13,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 12),
        };
        var inputBinding = new System.Windows.Data.Binding("TicketInput")
        {
            Source = _vm,
            Mode = System.Windows.Data.BindingMode.TwoWay,
            UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
        };
        input.SetBinding(TextBox.TextProperty, inputBinding);
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) _vm.StartCommand.Execute(null); };
        left.Children.Add(input);

        // Botões
        var btnRow = new UniformGrid { Rows = 1, Columns = 4 };

        Button MakeBtn(string label, string bg, System.Windows.Input.ICommand cmd)
        {
            var b = new Button
            {
                Content = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0, 8, 0, 8),
                Margin = new Thickness(0, 0, 4, 0),
                Command = cmd,
            };
            return b;
        }

        btnRow.Children.Add(MakeBtn("▶ Iniciar", "#238636", _vm.StartCommand));
        btnRow.Children.Add(MakeBtn("⏸ Pausar", "#30363D", _vm.PauseCommand));
        btnRow.Children.Add(MakeBtn("⏹ Parar", "#30363D", _vm.StopCommand));
        btnRow.Children.Add(MakeBtn("🔁 Trocar", "#7C3AED", _vm.SwitchCommand));
        left.Children.Add(btnRow);

        grid.Children.Add(leftCard);

        // ── Coluna direita: resumo do dia ─────────────────────────────────────
        var rightCard = MakeCard();
        Grid.SetColumn(rightCard, 2);

        var right = new DockPanel { Margin = new Thickness(16) };
        rightCard.Child = right;

        var rightHdr = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        rightHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rightHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rightHdr.Children.Add(new TextBlock
        {
            Text = "Hoje",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        });
        var dayTotalLbl = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var dayTotalBinding = new System.Windows.Data.Binding("DayTotal") { Source = _vm };
        dayTotalLbl.SetBinding(TextBlock.TextProperty, dayTotalBinding);
        Grid.SetColumn(dayTotalLbl, 1);
        rightHdr.Children.Add(dayTotalLbl);

        DockPanel.SetDock(rightHdr, Dock.Top);
        right.Children.Add(rightHdr);

        var sep = new System.Windows.Shapes.Rectangle
        { Height = 1, Fill = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)) };
        DockPanel.SetDock(sep, Dock.Top);
        right.Children.Add(sep);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var dayList = new ItemsControl { ItemsSource = _vm.TodayEntries };
        dayList.ItemTemplate = BuildTimeEntryTemplate();
        scroll.Content = dayList;
        right.Children.Add(scroll);

        grid.Children.Add(rightCard);
        Content = grid;
    }

    private static Border MakeCard()
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
        };
    }

    private static DataTemplate BuildTimeEntryTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Grid));

        var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        factory.AppendChild(col1);
        factory.AppendChild(col2);

        var lbl = new FrameworkElementFactory(typeof(TextBlock));
        lbl.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("TicketId"));
        lbl.SetValue(TextBlock.ForegroundProperty,
            new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)));
        lbl.SetValue(TextBlock.FontSizeProperty, 12.0);
        lbl.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 4));
        factory.AppendChild(lbl);

        var dur = new FrameworkElementFactory(typeof(TextBlock));
        dur.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Formatted"));
        dur.SetValue(TextBlock.ForegroundProperty,
            new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)));
        dur.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
        dur.SetValue(TextBlock.FontSizeProperty, 12.0);
        dur.SetValue(Grid.ColumnProperty, 1);
        factory.AppendChild(dur);

        return new DataTemplate { VisualTree = factory };
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