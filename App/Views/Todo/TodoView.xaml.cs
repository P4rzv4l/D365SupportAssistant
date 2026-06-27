// =============================================================================
//  TodoView.xaml.cs — Orquestrador do TodoView (refatorado)
// =============================================================================
// Responsabilidade: montar layout raiz e coordenar sub-builders.
// Nenhuma cor hardcoded, nenhum mapeamento de domínio, nenhum badge aqui.
//
// Estrutura:
//   Theme/         → DashboardTheme.cs         (compartilhado com Dashboard)
//   Helpers/
//     TodoDisplayMappers.cs                     (mapeamento domínio → exibição)
//   Components/
//     TodoUiFactory.cs                          (primitivos de UI)
//     TodoColumnFactory.cs                      (definição de colunas)
//   Sections/
//     TodoRowBuilder.cs                         (linha da tabela)
//     TodoDetailBuilder.cs                      (painel lateral)
//     TodoFormBuilder.cs                        (overlay de formulário)
//   TodoView.xaml.cs                            ← você está aqui
// =============================================================================

using D365Assistant.Core.Models.Todo;
using D365Assistant.ViewModels;
using D365Assistant.Views.Dashboard.Theme;
using D365Assistant.Views.Todo.Components;
using D365Assistant.Views.Todo.Helpers;
using D365Assistant.Views.Todo.Sections;
using D365Assistant.Views.Todo.Sections.Kanban;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views;

public partial class TodoView : Page
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly TodoViewModel _vm;

    // ── Sub-builders ──────────────────────────────────────────────────────────
    private TodoFormBuilder? _formBuilder;
    private TodoDetailBuilder? _detailBuilder;

    // ── UI refs ───────────────────────────────────────────────────────────────
    private StackPanel? _tableBody;
    private WpfBorder? _detailPanel;
    private StackPanel? _detailContent;
    private WpfBorder? _formOverlay;
    private TextBlock? _totalTb;
    private TextBlock? _pageTb;

    // ── State ─────────────────────────────────────────────────────────────────
    private TodoItem? _selected;
    private int _page = 1;
    private string _activeTab = TabLabels.Mine;
    private string _statusFilter = "Todos";

    // ── View mode ─────────────────────────────────────────────────────────────
    private enum ViewMode { List, Kanban }
    private ViewMode _viewMode = ViewMode.List;
    private TodoKanbanView? _kanbanView;
    private UIElement? _kanbanRoot;
    private UIElement? _listRoot;
    private Grid? _contentGrid;   // grid col0=table col1=detail
    private Button? _btnToggleView;
    private Func<string?>? _getPeriod;
    private Func<string?>? _getSort;

    private readonly Dictionary<string, Button> _tabBtns = [];
    private readonly Dictionary<string, WpfBorder> _rowMap = [];

    // ── Tab labels ────────────────────────────────────────────────────────────
    private static class TabLabels
    {
        public const string Mine = "Minhas Tarefas";
        public const string Done = "Tarefas Concluídas";
    }

    // ── Notification timer ────────────────────────────────────────────────────
    private readonly System.Windows.Threading.DispatcherTimer _notifTimer;

    // ══════════════════════════════════════════════════════════════════════════
    //  CONSTRUCTOR
    // ══════════════════════════════════════════════════════════════════════════

    public TodoView(TodoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        // Form builder — created first so overlay can be added to root grid
        _formBuilder = new TodoFormBuilder(vm, onClose: CloseForm);

        _detailBuilder = new TodoDetailBuilder(
            onEdit: item => { _vm.OpenEditCommand.Execute(item); ShowForm(item); },
            onToggle: item => { _vm.ToggleCommand.Execute(item); Refresh(); },
            onDelete: item =>
            {
                _vm.DeleteCommand.Execute(item);
                _selected = null;
                if (_detailPanel != null) _detailPanel.Visibility = Visibility.Collapsed;
                Refresh();
            },
            onClose: CloseDetailPanel);

        var rootGrid = (Grid)Content;
        rootGrid.Children.Add(BuildRoot());

        _formOverlay = _formBuilder.Build();
        rootGrid.Children.Add(_formOverlay);

        _notifTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5),
        };
        _notifTimer.Tick += (_, _) => CheckNotifications();
        _notifTimer.Start();

        // Sempre recarrega ao entrar na página para garantir dados frescos,
        // mas o guard de Refresh() evita renderizações duplicadas
        _vm.Load();

        SubscribeToViewModel();
        Refresh();
    }

    private void SubscribeToViewModel()
    {
        void OnCollectionChanged(object? s, NotifyCollectionChangedEventArgs e)
            => Dispatcher.Invoke(RequestRefresh);

        void OnPropertyChanged(object? s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(_vm.CountPending) or nameof(_vm.CountToday)
                               or nameof(_vm.CountOverdue) or nameof(_vm.CountDone))
                Dispatcher.Invoke(RequestRefresh);
        }

        _vm.Items.CollectionChanged += OnCollectionChanged;
        _vm.PropertyChanged += OnPropertyChanged;

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
        var root = new Grid { Background = DashboardTheme.Brush(DashboardTheme.Bg) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        AddRow(root, BuildTopBar(), 0);
        AddRow(root, BuildTabs(), 1);
        AddRow(root, BuildToolbar(), 2);
        AddRow(root, BuildContent(), 3);

        return root;
    }

    // ── Top bar ───────────────────────────────────────────────────────────────

    private UIElement BuildTopBar()
    {
        var bar = SurfaceBar(bottomBorder: true, padding: new Thickness(24, 14, 24, 14));
        var g = TwoColumnGrid();
        bar.Child = g;

        var left = BuildTopBarLeft();
        var right = BuildTopBarRight();

        Grid.SetColumn(left, 0); g.Children.Add(left);
        Grid.SetColumn(right, 1); g.Children.Add(right);

        return bar;
    }

    private static UIElement BuildTopBarLeft()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "TODO",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Gerencie suas tarefas e atividades pendentes",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        });
        return stack;
    }

    private UIElement BuildTopBarRight()
    {
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        right.Children.Add(new TextBlock
        {
            Text = "Período:",
            FontSize = 12,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var (periodHost, getPeriod, _, setPeriodItems) = TodoUiFactory.Dropdown(_ =>
        {
            _page = 1;
            RequestRefresh();
        });
        setPeriodItems(["Todas as tarefas", "Esta semana", "Este mês", "Hoje"]);
        _getPeriod = getPeriod;
        periodHost.Width = 160;
        periodHost.Margin = new Thickness(0, 0, 10, 0);
        right.Children.Add(periodHost);

        var btnRefresh = TodoUiFactory.OutlineButton("↻  Atualizar");
        btnRefresh.Click += (_, _) => { _vm.Load(); Refresh(); };
        right.Children.Add(btnRefresh);

        var btnNew = TodoUiFactory.PrimaryButton("+ Nova Tarefa");
        btnNew.Margin = new Thickness(8, 0, 0, 0);
        btnNew.Click += (_, _) => { _vm.OpenNewCommand.Execute(null); ShowForm(null); };
        right.Children.Add(btnNew);

        return right;
    }

    // ── Tabs ──────────────────────────────────────────────────────────────────

    private UIElement BuildTabs()
    {
        var bar = SurfaceBar(bottomBorder: true, padding: new Thickness(20, 0, 20, 0));
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Child = tabs;

        foreach (var (label, count) in new[]
        {
            (TabLabels.Mine, _vm.CountPending),
            (TabLabels.Done, _vm.CountDone),
        })
        {
            var btn = BuildTabButton(label, count > 0 ? count.ToString() : null);
            var l = label;
            btn.Click += (_, _) => ActivateTab(l);
            _tabBtns[label] = btn;
            tabs.Children.Add(btn);
        }

        HighlightTab(TabLabels.Mine);
        return bar;
    }

    private static Button BuildTabButton(string label, string? badge)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (badge != null)
            stack.Children.Add(new Border
            {
                Background = DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x33),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 1, 7, 1),
                Margin = new Thickness(6, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = badge,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DashboardTheme.Brush(DashboardTheme.Purple),
                },
            });

        return new Button
        {
            Content = stack,
            Background = Brushes.Transparent,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            BorderThickness = new Thickness(0, 0, 0, 2),
            BorderBrush = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Padding = new Thickness(4, 12, 4, 12),
            Margin = new Thickness(0, 0, 20, 0),
        };
    }

    private void ActivateTab(string label)
    {
        _activeTab = label;
        _page = 1;
        HighlightTab(label);
        Refresh();
    }

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

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private UIElement BuildToolbar()
    {
        var bar = SurfaceBar(bottomBorder: true, padding: new Thickness(16, 8, 16, 8));
        var g = TwoColumnGrid();
        bar.Child = g;

        Grid.SetColumn(BuildSearchBox(), 0); g.Children.Add(BuildSearchBox());

        var right = BuildToolbarRight();
        Grid.SetColumn(right, 1); g.Children.Add(right);

        return bar;
    }

    private UIElement BuildSearchBox()
    {
        var wrap = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Bg),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            Height = 32,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(new TextBlock
        {
            Text = "⌕",
            FontSize = 13,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            Margin = new Thickness(0, 0, 6, 0),
        });

        var box = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            FontSize = 12,
            Width = 160,
        };
        box.TextChanged += (_, _) => { _vm.SearchText = box.Text; _page = 1; Refresh(); };
        row.Children.Add(box);

        wrap.Child = row;
        return wrap;
    }

    private UIElement BuildToolbarRight()
    {
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        right.Children.Add(new TextBlock
        {
            Text = "Status:",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });

        var (statusHost, getStatus, _, setStatusItems) = TodoUiFactory.Dropdown(val =>
        {
            _statusFilter = val;
            _page = 1;
            Refresh();
        });
        setStatusItems(["Todos", "Pendentes", "Atrasadas", "Hoje", "Concluídas"]);
        statusHost.Width = 120;
        statusHost.Margin = new Thickness(0, 0, 16, 0);
        right.Children.Add(statusHost);

        right.Children.Add(new TextBlock
        {
            Text = "Ordenar por:",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });

        var (sortHost, getSort, _, setSortItems) = TodoUiFactory.Dropdown(_ =>
        {
            _page = 1;
            RequestRefresh();
        });
        setSortItems(["Data de Vencimento", "Prioridade", "Título", "Criação"]);
        _getSort = getSort;
        sortHost.Width = 150;
        sortHost.Margin = new Thickness(0, 0, 12, 0);
        right.Children.Add(sortHost);

        _btnToggleView = new Button
        {
            Content = "⊞",
            FontSize = 14,
            Background = DashboardTheme.Brush(DashboardTheme.Bg),
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(8, 4, 8, 4),
            ToolTip = "Alternar entre lista e Kanban",
        };
        _btnToggleView.Click += (_, _) => ToggleViewMode();
        right.Children.Add(_btnToggleView);

        return right;
    }

    // ── View mode toggle ──────────────────────────────────────────────────────

    private void ToggleViewMode()
    {
        _viewMode = _viewMode == ViewMode.List ? ViewMode.Kanban : ViewMode.List;

        if (_btnToggleView != null)
        {
            _btnToggleView.Content = _viewMode == ViewMode.Kanban ? "☰" : "⊞";
            _btnToggleView.Foreground = _viewMode == ViewMode.Kanban
                ? DashboardTheme.Brush(DashboardTheme.Accent)
                : DashboardTheme.Brush(DashboardTheme.TextSub);
        }

        if (_viewMode == ViewMode.Kanban) ShowKanban();
        else ShowList();
    }

    private void ShowKanban()
    {
        if (_listRoot != null) _listRoot.Visibility = Visibility.Collapsed;

        if (_kanbanView == null)
        {
            _kanbanView = new TodoKanbanView(
                _vm,
                onCardClick: item =>
                {
                    if (_detailPanel == null || _detailContent == null) return;
                    _detailPanel.Visibility = Visibility.Visible;
                    _detailContent.Children.Clear();
                    _detailBuilder?.Populate(_detailContent, item);
                },
                onItemChanged: RequestRefresh);

            _kanbanRoot = _kanbanView.Build(GetFilteredItems());

            if (_contentGrid != null)
            {
                Grid.SetColumn(_kanbanRoot, 0);
                _contentGrid.Children.Insert(0, _kanbanRoot);
            }
        }
        else
        {
            _kanbanRoot!.Visibility = Visibility.Visible;
            _kanbanView.Populate(GetFilteredItems());
        }
    }

    private void ShowList()
    {
        if (_kanbanRoot != null) _kanbanRoot.Visibility = Visibility.Collapsed;
        if (_listRoot != null) _listRoot.Visibility = Visibility.Visible;
        RequestRefresh();
    }

    // ── Content ───────────────────────────────────────────────────────────────

    private UIElement BuildContent()
    {
        _contentGrid = new Grid();
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tableArea = BuildTableArea();
        _listRoot = tableArea;
        Grid.SetColumn(tableArea, 0);
        _contentGrid.Children.Add(tableArea);

        _detailPanel = BuildDetailPanel();
        Grid.SetColumn(_detailPanel, 1);
        _contentGrid.Children.Add(_detailPanel);

        return _contentGrid;
    }

    private UIElement BuildTableArea()
    {
        var area = new Grid();
        area.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        area.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var tableWrap = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
        };
        var tableStack = new DockPanel();
        tableWrap.Child = tableStack;

        var header = BuildTableHeader();
        DockPanel.SetDock(header, Dock.Top);
        tableStack.Children.Add(header);

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
        _tableBody = new StackPanel();
        sv.Content = _tableBody;
        tableStack.Children.Add(sv);

        Grid.SetRow(tableWrap, 0);
        area.Children.Add(tableWrap);

        var footer = BuildPaginationFooter();
        Grid.SetRow(footer, 1);
        area.Children.Add(footer);

        return area;
    }

    private UIElement BuildTableHeader()
    {
        var hdr = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var g = TodoColumnFactory.Create();
        hdr.Child = g;

        AddHeaderCell(g, TodoColumnFactory.ColTask, "Tarefa", leftMargin: true);
        AddHeaderCell(g, TodoColumnFactory.ColRelated, "Relacionado a");
        AddHeaderCell(g, TodoColumnFactory.ColCategory, "Tipo");
        AddHeaderCell(g, TodoColumnFactory.ColPriority, "Prioridade");
        AddHeaderCell(g, TodoColumnFactory.ColDueDate, "Vencimento");
        AddHeaderCell(g, TodoColumnFactory.ColStatus, "Status");

        return hdr;
    }

    private static void AddHeaderCell(Grid g, int col, string text, bool leftMargin = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(leftMargin ? 16 : 0, 10, 8, 10),
        };
        Grid.SetColumn(tb, col);
        g.Children.Add(tb);
    }

    private WpfBorder BuildDetailPanel()
    {
        var panel = new Border
        {
            Width = 320,
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        _detailContent = new StackPanel();
        panel.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _detailContent,
        };

        return panel;
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

        var g = TwoColumnGrid();
        bar.Child = g;

        _totalTb = new TextBlock
        {
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_totalTb, 0);
        g.Children.Add(_totalTb);

        var pagRow = BuildPageButtons();
        Grid.SetColumn(pagRow, 1);
        g.Children.Add(pagRow);

        return bar;
    }

    private UIElement BuildPageButtons()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Button PB(string label) => new()
        {
            Content = label,
            FontSize = 12,
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(2, 0, 2, 0),
            MinWidth = 30,
        };

        var btnFirst = PB("«"); btnFirst.Click += (_, _) => { _page = 1; Refresh(); };
        var btnPrev = PB("‹"); btnPrev.Click += (_, _) => { if (_page > 1) { _page--; Refresh(); } };
        var btnNext = PB("›"); btnNext.Click += (_, _) => { _page++; Refresh(); };
        var btnLast = PB("»"); btnLast.Click += (_, _) => { _page = 999; Refresh(); };

        _pageTb = new TextBlock
        {
            FontSize = 12,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0),
        };

        row.Children.Add(btnFirst);
        row.Children.Add(btnPrev);
        row.Children.Add(_pageTb);
        row.Children.Add(btnNext);
        row.Children.Add(btnLast);

        return row;
    }

    // ── Refresh guard ─────────────────────────────────────────────────────────
    private bool _refreshPending = false;

    private void RequestRefresh()
    {
        if (_refreshPending) return;
        _refreshPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            _refreshPending = false;
            Refresh();
        });
    }

    private void Refresh()
    {
        if (_tableBody == null) return;

        _tableBody.Children.Clear();
        _rowMap.Clear();

        _vm.FilterGroup = _activeTab == TabLabels.Done ? "Concluídas" : "Todas";

        var items = GetFilteredItems();

        var total = items.Count;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)12));
        _page = Math.Clamp(_page, 1, pages);
        var paged = items.Skip((_page - 1) * 12).Take(12).ToList();

        if (_totalTb != null) _totalTb.Text = $"Total: {total} tarefa{(total != 1 ? "s" : "")}";
        if (_pageTb != null) _pageTb.Text = $"{_page}";

        if (total == 0)
        {
            _tableBody.Children.Add(new TextBlock
            {
                Text = "Nenhuma tarefa encontrada.",
                FontSize = 13,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
            return;
        }

        var rowBuilder = new TodoRowBuilder(
            onToggle: item => { _vm.ToggleCommand.Execute(item); Refresh(); },
            onSelect: SelectRow);

        foreach (var item in paged)
        {
            var row = rowBuilder.Build(item);
            _rowMap[item.Id.ToString()] = row;
            _tableBody.Children.Add(row);
        }

        // Restore selection highlight
        if (_selected != null && _rowMap.TryGetValue(_selected.Id.ToString(), out var sel))
        {
            sel.Background = DashboardTheme.Brush(DashboardTheme.RowSelected);
            sel.Tag = "selected";
        }
    }

    private List<TodoItem> GetFilteredItems()
    {
        var items = _vm.Items.ToList();

        // Período
        var today = DateTime.Today;
        items = _getPeriod?.Invoke() switch
        {
            "Hoje" => items.Where(t => t.DueDate?.Date == today).ToList(),
            "Esta semana" => items.Where(t => t.DueDate?.Date >= today
                                            && t.DueDate?.Date <= today.AddDays(7)).ToList(),
            "Este mês" => items.Where(t => t.DueDate?.Date >= today
                                            && t.DueDate?.Date <= today.AddDays(30)).ToList(),
            _ => items, // "Todas as tarefas"
        };

        // Status
        items = _statusFilter switch
        {
            "Pendentes" => items.Where(t => !t.Done && !t.IsOverdue).ToList(),
            "Atrasadas" => items.Where(t => t.IsOverdue).ToList(),
            "Hoje" => items.Where(t => t.IsDueToday).ToList(),
            _ => items,
        };

        // Aba
        items = _activeTab switch
        {
            TabLabels.Mine => items.Where(t => !t.Done).ToList(),
            TabLabels.Done => items.Where(t => t.Done).ToList(),
            _ => items,
        };

        // Ordenação
        items = _getSort?.Invoke() switch
        {
            "Prioridade" => items.OrderBy(t => t.Priority).ToList(),
            "Título" => items.OrderBy(t => t.Title).ToList(),
            "Criação" => items.OrderByDescending(t => t.CreatedAt).ToList(),
            _ => items.OrderBy(t => t.DueDate ?? DateTime.MaxValue).ToList(), // Data de Vencimento
        };

        return items;
    }

    // ── Row selection ─────────────────────────────────────────────────────────

    private void SelectRow(TodoItem item, WpfBorder row)
    {
        foreach (var (_, r) in _rowMap)
        {
            r.Background = DashboardTheme.Brush(DashboardTheme.Surface);
            r.Tag = null;
        }

        _selected = item;
        row.Background = DashboardTheme.Brush(DashboardTheme.RowSelected);
        row.Tag = "selected";

        if (_detailPanel == null || _detailContent == null || _detailBuilder == null) return;

        _detailPanel.Visibility = Visibility.Visible;
        _detailContent.Children.Clear();
        _detailBuilder.Populate(_detailContent, item);
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

    // ── Form ──────────────────────────────────────────────────────────────────

    private void ShowForm(TodoItem? item)
    {
        if (_formOverlay == null || _formBuilder == null) return;
        _formOverlay.Visibility = Visibility.Visible;

        if (_formBuilder.TitleBox != null) _formBuilder.TitleBox.Text = _vm.FormTitle;
        if (_formBuilder.DescBox != null) _formBuilder.DescBox.Text = _vm.FormDescription;
        if (_formBuilder.TicketBox != null) _formBuilder.TicketBox.Text = _vm.FormTicketId ?? "";
        if (_formBuilder.DuePicker != null) _formBuilder.DuePicker.SelectedDate = _vm.FormDueDate;

        _formBuilder.SetCategory(_vm.FormCategory);
        _formBuilder.SetPriority(TodoDisplayMappers.PriorityLabel(_vm.FormPriority));

        if (_formBuilder.ErrorLabel != null)
        {
            _formBuilder.ErrorLabel.Text = "";
            _formBuilder.ErrorLabel.Visibility = Visibility.Collapsed;
        }

        _formBuilder.TitleBox?.Focus();
    }

    private void CloseForm()
    {
        if (_formOverlay != null) _formOverlay.Visibility = Visibility.Collapsed;
        _vm.CloseFormCommand.Execute(null);
        Refresh();
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
                new ToastContentBuilder()
                    .AddText(overdues.Count == 1
                        ? $"⚠ Tarefa atrasada: {overdues[0].Title}"
                        : $"⚠ {overdues.Count} tarefas atrasadas")
                    .AddText(string.Join("\n", overdues.Take(3).Select(t => $"• {t.Title}")))
                    .Show();
            }
            else if (dueTodays.Any())
            {
                new ToastContentBuilder()
                    .AddText(dueTodays.Count == 1
                        ? $"📅 Vence hoje: {dueTodays[0].Title}"
                        : $"📅 {dueTodays.Count} tarefas vencem hoje")
                    .AddText(string.Join("\n", dueTodays.Take(3).Select(t => $"• {t.Title}")))
                    .Show();
            }
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LAYOUT UTILITIES
    // ══════════════════════════════════════════════════════════════════════════

    private static Border SurfaceBar(bool bottomBorder, Thickness? padding = null) => new()
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
}

// ── Converter (mantido no mesmo arquivo por ser pequeno e sem dependências) ────

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