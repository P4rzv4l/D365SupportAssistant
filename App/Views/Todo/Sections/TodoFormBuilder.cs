// =============================================================================
//  TodoFormBuilder.cs — Overlay de criação/edição de tarefa
// =============================================================================
// Responsabilidade única: construir e controlar o formulário modal.
// Não conhece tabela, detalhe nem estado de seleção.
// =============================================================================

using D365Assistant.ViewModels;
using D365Assistant.Views.Dashboard.Theme;
using D365Assistant.Views.Todo.Components;
using D365Assistant.Views.Todo.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views.Todo.Sections;

public sealed class TodoFormBuilder
{
    // ── Form field refs (preenchidos em Build) ─────────────────────────────────
    public TextBox? TitleBox { get; private set; }
    public TextBox? DescBox { get; private set; }
    public TextBox? TicketBox { get; private set; }
    public DatePicker? DuePicker { get; private set; }
    public TextBlock? ErrorLabel { get; private set; }
    public Func<string?> GetCategory { get; private set; } = () => "Geral";
    public Func<string?> GetPriority { get; private set; } = () => "Média";
    public Action<string> SetCategory { get; private set; } = _ => { };
    public Action<string> SetPriority { get; private set; } = _ => { };

    private readonly TodoViewModel _vm;
    private readonly Action _onClose;

    public TodoFormBuilder(TodoViewModel vm, Action onClose)
    {
        _vm = vm;
        _onClose = onClose;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    public Border Build()
    {
        var overlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x99, 0x00, 0x00, 0x00)),
            Visibility = Visibility.Collapsed,
        };
        overlay.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource == overlay) _onClose();
        };

        var panel = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
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

        var header = BuildFormHeader();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);

        var footer = BuildFormFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(footer);

        var bodyScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        bodyScroll.Content = BuildFormBody();
        dock.Children.Add(bodyScroll);

        return overlay;
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private UIElement BuildFormHeader()
    {
        var border = new Border
        {
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(22, 16, 22, 16),
        };

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        border.Child = g;

        var titleTb = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        };
        titleTb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("IsEditing")
        {
            Source = _vm,
            Converter = new BoolToStringConverter("Editar Tarefa", "Nova Tarefa"),
        });
        g.Children.Add(titleTb);

        var btnClose = new Button
        {
            Content = "✕",
            FontSize = 14,
            Background = Brushes.Transparent,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6),
        };
        btnClose.Click += (_, _) => _onClose();
        Grid.SetColumn(btnClose, 1);
        g.Children.Add(btnClose);

        return border;
    }

    // ── Footer ────────────────────────────────────────────────────────────────

    private UIElement BuildFormFooter()
    {
        var border = new Border
        {
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(22, 14, 22, 14),
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        border.Child = row;

        ErrorLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.Red),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Visibility = Visibility.Collapsed,
        };
        row.Children.Add(ErrorLabel);

        var btnCancel = TodoUiFactory.OutlineButton("Cancelar");
        btnCancel.Click += (_, _) => _onClose();
        row.Children.Add(btnCancel);

        var btnSave = TodoUiFactory.PrimaryButton("Salvar Tarefa");
        btnSave.Margin = new Thickness(8, 0, 0, 0);
        btnSave.Click += (_, _) => Submit();
        row.Children.Add(btnSave);

        return border;
    }

    // ── Body ──────────────────────────────────────────────────────────────────

    private UIElement BuildFormBody()
    {
        var body = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

        // Título
        body.Children.Add(TodoUiFactory.FormLabel("Título *"));
        TitleBox = TodoUiFactory.FormInput();
        body.Children.Add(TitleBox);

        // Descrição
        body.Children.Add(TodoUiFactory.FormLabel("Descrição"));
        DescBox = new TextBox
        {
            Background = DashboardTheme.Brush(DashboardTheme.Bg),
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 16),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Height = 72,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        body.Children.Add(DescBox);

        // Row 1: Categoria + Prioridade
        var r1 = TodoUiFactory.TwoColumnForm();
        body.Children.Add(r1);

        var catCol = new StackPanel();
        catCol.Children.Add(TodoUiFactory.FormLabel("Categoria"));
        var (catHost, getCat, setCat, setCatItems) = TodoUiFactory.Dropdown();
        setCatItems(["Geral", "Chamado", "Reunião", "Follow-up", "Documentação", "Outro"]);
        GetCategory = getCat;
        SetCategory = setCat;
        catCol.Children.Add(catHost);
        Grid.SetColumn(catCol, 0);
        r1.Children.Add(catCol);

        var priCol = new StackPanel();
        priCol.Children.Add(TodoUiFactory.FormLabel("Prioridade"));
        var (priHost, getPri, setPri, setPriItems) = TodoUiFactory.Dropdown();
        setPriItems(["Alta", "Média", "Baixa"]);
        setPri("Média");
        GetPriority = getPri;
        SetPriority = setPri;
        priCol.Children.Add(priHost);
        Grid.SetColumn(priCol, 2);
        r1.Children.Add(priCol);

        // Row 2: Vencimento + Chamado
        var r2 = TodoUiFactory.TwoColumnForm();
        r2.Margin = new Thickness(0, 0, 0, 4);
        body.Children.Add(r2);

        var dueCol = new StackPanel();
        dueCol.Children.Add(TodoUiFactory.FormLabel("Vencimento"));
        DuePicker = new DatePicker
        {
            Background = DashboardTheme.Brush(DashboardTheme.Bg),
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            FontSize = 12,
            SelectedDateFormat = DatePickerFormat.Short,
        };
        dueCol.Children.Add(DuePicker);
        Grid.SetColumn(dueCol, 0);
        r2.Children.Add(dueCol);

        var tickCol = new StackPanel();
        tickCol.Children.Add(TodoUiFactory.FormLabel("Chamado (opcional)"));
        TicketBox = TodoUiFactory.FormInput("Ex: CAS-12345");
        TicketBox.Margin = new Thickness(0);
        tickCol.Children.Add(TicketBox);
        Grid.SetColumn(tickCol, 2);
        r2.Children.Add(tickCol);

        return body;
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    private void Submit()
    {
        _vm.FormTitle = TitleBox?.Text ?? "";
        _vm.FormDescription = DescBox?.Text ?? "";
        _vm.FormCategory = GetCategory() ?? "Geral";
        _vm.FormPriority = TodoDisplayMappers.PriorityCode(GetPriority() ?? "Média");
        _vm.FormDueDate = DuePicker?.SelectedDate;
        _vm.FormTicketId = TicketBox?.Text;
        _vm.SaveCommand.Execute(null);

        if (!_vm.FormVisible)
        {
            _onClose();
        }
        else if (!string.IsNullOrEmpty(_vm.FormError) && ErrorLabel != null)
        {
            ErrorLabel.Text = _vm.FormError;
            ErrorLabel.Visibility = Visibility.Visible;
        }
    }
}