// =============================================================================
//  TodoKanbanView.cs — Board Kanban com drag & drop por mouse events
// =============================================================================
// Drag sem DoDragDrop: threshold de 5px separa click de arrasto.
// Canvas transparente sobre o board recebe os eventos de mouse durante o drag.
// =============================================================================

using D365Assistant.Core.Models.Todo;
using D365Assistant.ViewModels;
using D365Assistant.Views.Dashboard.Theme;
using D365Assistant.Views.Todo.Components;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Todo.Sections.Kanban;

public sealed class TodoKanbanView
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly TodoViewModel _vm;
    private readonly Action<TodoItem> _onCardClick;
    private readonly Action _onItemChanged;

    // ── Drag state ────────────────────────────────────────────────────────────
    private TodoItem? _dragItem;
    private WpfBorder? _dragGhost;
    private Point _mouseDownPos;
    private Point _dragOffset;
    private bool _isDragging;
    private WpfBorder? _pressedCard;
    private KanbanColumn? _hoveredColumn;

    // ── Column refs ───────────────────────────────────────────────────────────
    private readonly Dictionary<KanbanColumn, StackPanel> _columnPanels = [];
    private readonly Dictionary<KanbanColumn, WpfBorder> _columnBorders = [];

    // ── Item → column map (runtime moves) ────────────────────────────────────
    private readonly Dictionary<int, KanbanColumn> _itemColumns = [];

    // ── Root refs ─────────────────────────────────────────────────────────────
    private Grid? _rootGrid;
    private Canvas? _dragCanvas;
    private ScrollViewer? _boardScroll;

    private const double DragThreshold = 6.0;

    public TodoKanbanView(
        TodoViewModel vm,
        Action<TodoItem> onCardClick,
        Action onItemChanged)
    {
        _vm = vm;
        _onCardClick = onCardClick;
        _onItemChanged = onItemChanged;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BUILD
    // ══════════════════════════════════════════════════════════════════════════

    public UIElement Build(IEnumerable<TodoItem> items)
    {
        var root = new Grid();
        _rootGrid = root;

        // Scrollable board row
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = DashboardTheme.Brush(DashboardTheme.Bg),
        };
        _boardScroll = scroll;

        var board = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(16),
        };
        scroll.Content = board;
        root.Children.Add(scroll);

        // Drag canvas — sits on top, only active during drag
        _dragCanvas = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
        };
        root.Children.Add(_dragCanvas);

        // Build columns
        foreach (var col in Enum.GetValues<KanbanColumn>())
            board.Children.Add(BuildColumn(col));

        // Wire root-level mouse events for drag
        root.MouseMove += OnRootMouseMove;
        root.MouseUp += OnRootMouseUp;

        Populate(items);
        return root;
    }

    public void Populate(IEnumerable<TodoItem> items)
    {
        foreach (var panel in _columnPanels.Values)
            panel.Children.Clear();

        foreach (var item in items)
        {
            var col = _itemColumns.TryGetValue(item.Id, out var saved)
                ? saved
                : KanbanColumnMeta.FromTodoItem(item);

            _itemColumns[item.Id] = col;

            if (_columnPanels.TryGetValue(col, out var panel))
                panel.Children.Add(BuildCard(item));
        }

        UpdateColumnCounts();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  COLUMN
    // ══════════════════════════════════════════════════════════════════════════

    private UIElement BuildColumn(KanbanColumn col)
    {
        var meta = KanbanColumnMeta.All[col];

        var outer = new WpfBorder
        {
            Width = 220,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(
                                  (Color)ColorConverter.ConvertFromString(meta.BgHex)),
            BorderBrush = new SolidColorBrush(
                                  (Color)ColorConverter.ConvertFromString(meta.BorderHex)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
        };
        _columnBorders[col] = outer;

        var colStack = new StackPanel();
        outer.Child = colStack;

        colStack.Children.Add(BuildColumnHeader(col, meta));

        var cardsScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 560,
            Padding = new Thickness(10, 0, 10, 10),
        };

        var cardsPanel = new StackPanel();
        cardsScroll.Content = cardsPanel;
        _columnPanels[col] = cardsPanel;
        colStack.Children.Add(cardsScroll);

        return outer;
    }

    private static UIElement BuildColumnHeader(
        KanbanColumn col, KanbanColumnMeta.ColumnInfo meta)
    {
        var hdr = new WpfBorder { Padding = new Thickness(12, 10, 12, 10) };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdr.Child = g;

        var labelRow = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(labelRow, 0);
        g.Children.Add(labelRow);

        labelRow.Children.Add(new WpfBorder
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(
                                    (Color)ColorConverter.ConvertFromString(meta.FgHex)),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        labelRow.Children.Add(new TextBlock
        {
            Text = meta.Label,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(
                                    (Color)ColorConverter.ConvertFromString(meta.FgHex)),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var countTb = new TextBlock
        {
            Text = "0",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(
                             (Color)ColorConverter.ConvertFromString(meta.FgHex)),
            Tag = $"count_{col}",
        };
        var countBadge = new WpfBorder
        {
            Background = DashboardTheme.AlphaBrush(
                               (Color)ColorConverter.ConvertFromString(meta.FgHex), 0x22),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 1, 7, 1),
            Child = countTb,
        };
        Grid.SetColumn(countBadge, 1);
        g.Children.Add(countBadge);

        return hdr;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CARD
    // ══════════════════════════════════════════════════════════════════════════

    private WpfBorder BuildCard(TodoItem item)
    {
        var card = KanbanCardBuilder.Build(
            item,
            onClick: _ => { },   // click handled via mouse events below
            onMouseDown: (_, _) => { });

        card.Tag = item;
        card.Cursor = Cursors.Hand;

        card.MouseLeftButtonDown += (sender, e) =>
        {
            _pressedCard = card;
            _dragItem = item;
            _mouseDownPos = e.GetPosition(_rootGrid);
            _dragOffset = e.GetPosition(card);
            _isDragging = false;
            card.CaptureMouse();
            e.Handled = true;
        };

        card.MouseLeftButtonUp += (sender, e) =>
        {
            var posInRoot = e.GetPosition(_rootGrid);
            card.ReleaseMouseCapture();

            if (_isDragging)
            {
                FinalizeDrag(posInRoot);
                ResetDragState();
            }
            else if (_dragItem?.Id == item.Id)
            {
                // Was a simple click
                ResetDragState();
                _onCardClick(item);
            }

            e.Handled = true;
        };

        card.MouseMove += (sender, e) =>
        {
            if (_pressedCard != card || e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(_rootGrid);
            var diff = pos - _mouseDownPos;

            if (!_isDragging)
            {
                if (Math.Abs(diff.X) < DragThreshold && Math.Abs(diff.Y) < DragThreshold)
                    return;

                // Threshold exceeded — start drag
                StartDragVisual(card, item, pos);
            }
            else
            {
                MoveDragGhost(pos);
                HighlightDropTarget(pos);
            }

            e.Handled = true;
        };

        return card;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DRAG VISUAL
    // ══════════════════════════════════════════════════════════════════════════

    private void StartDragVisual(WpfBorder card, TodoItem item, Point pos)
    {
        _isDragging = true;
        card.Opacity = 0.35;

        _dragGhost = KanbanCardBuilder.Build(item, _ => { }, (_, _) => { });
        _dragGhost.Width = 220;
        _dragGhost.Opacity = 0.88;
        _dragGhost.IsHitTestVisible = false;
        _dragGhost.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 16,
            Opacity = 0.4,
            ShadowDepth = 4,
        };

        Canvas.SetLeft(_dragGhost, pos.X - _dragOffset.X);
        Canvas.SetTop(_dragGhost, pos.Y - _dragOffset.Y);

        if (_dragCanvas != null)
        {
            _dragCanvas.Children.Add(_dragGhost);
            _dragCanvas.IsHitTestVisible = false; // canvas never intercepts — card has capture
        }
    }

    private void MoveDragGhost(Point pos)
    {
        if (_dragGhost == null) return;
        Canvas.SetLeft(_dragGhost, pos.X - _dragOffset.X);
        Canvas.SetTop(_dragGhost, pos.Y - _dragOffset.Y);
    }

    private void HighlightDropTarget(Point pos)
    {
        var hit = FindColumnAt(pos);

        foreach (var (c, b) in _columnBorders)
        {
            var meta = KanbanColumnMeta.All[c];
            if (c == hit)
            {
                b.BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(meta.FgHex));
                b.BorderThickness = new Thickness(2);
            }
            else
            {
                b.BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(meta.BorderHex));
                b.BorderThickness = new Thickness(1);
            }
        }

        _hoveredColumn = hit;
    }

    private void FinalizeDrag(Point releasePos)
    {
        var targetCol = FindColumnAt(releasePos);
        if (targetCol == null || _dragItem == null) return;

        var item = _dragItem;

        // Mesma coluna — não faz nada
        if (_itemColumns.TryGetValue(item.Id, out var current) && current == targetCol)
            return;

        if (targetCol == KanbanColumn.Concluido && !item.Done)
        {
            var confirmed = false;
            if (_rootGrid != null)
            {
                var overlay = BuildConfirmOverlay(item,
                    onConfirm: () => confirmed = true,
                    onCancel: () => confirmed = false);

                _rootGrid.Children.Add(overlay);
                var frame = new System.Windows.Threading.DispatcherFrame();
                ((FrameworkElement)overlay).Tag = frame;
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }

            if (!confirmed) return;
        }

        // Aplica coluna + sincroniza Done/DoneAt no item
        KanbanColumnMeta.ApplyToItem(item, targetCol.Value);

        // Persiste via ViewModel
        _vm.UpdateKanbanStatusCommand.Execute(item);

        // Atualiza mapa visual e repopula
        _itemColumns[item.Id] = targetCol.Value;
        Populate(_vm.Items);
        _onItemChanged();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ROOT MOUSE EVENTS (fallback for when card loses capture)
    // ══════════════════════════════════════════════════════════════════════════

    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(_rootGrid);
        MoveDragGhost(pos);
        HighlightDropTarget(pos);
    }

    private void OnRootMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        FinalizeDrag(e.GetPosition(_rootGrid));
        ResetDragState();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Encontra qual coluna está sob o ponto dado nas coordenadas do _rootGrid.
    /// Não usa HitTest pois o card tem CaptureMouse — usa bounds via TransformToAncestor.
    /// </summary>
    private KanbanColumn? FindColumnAt(Point posInRoot)
    {
        if (_rootGrid == null) return null;

        foreach (var (col, border) in _columnBorders)
        {
            try
            {
                // Converte a posição do root para coordenadas locais do border
                var localPos = _rootGrid.TransformToDescendant(border)
                                        .Transform(posInRoot);

                if (localPos.X >= 0 && localPos.X <= border.ActualWidth &&
                    localPos.Y >= 0 && localPos.Y <= border.ActualHeight)
                    return col;
            }
            catch { }
        }

        return null;
    }

    private void ResetDragState()
    {
        // Restore card opacity
        if (_dragItem != null && _itemColumns.TryGetValue(_dragItem.Id, out var col)
            && _columnPanels.TryGetValue(col, out var panel))
        {
            foreach (WpfBorder c in panel.Children)
                if (c.Tag is TodoItem t && t.Id == _dragItem.Id)
                    c.Opacity = 1.0;
        }

        // Clear ghost
        if (_dragCanvas != null) _dragCanvas.Children.Clear();

        // Reset column highlights
        foreach (var (c, b) in _columnBorders)
        {
            var meta = KanbanColumnMeta.All[c];
            b.BorderBrush = new SolidColorBrush(
                                    (Color)ColorConverter.ConvertFromString(meta.BorderHex));
            b.BorderThickness = new Thickness(1);
        }

        _dragItem = null;
        _dragGhost = null;
        _pressedCard = null;
        _hoveredColumn = null;
        _isDragging = false;
    }

    private void UpdateColumnCounts()
    {
        foreach (var (col, border) in _columnBorders)
        {
            if (!_columnPanels.TryGetValue(col, out var panel)) continue;
            var tag = $"count_{col}";

            foreach (var child in GetAllChildren(border))
            {
                if (child is TextBlock tb && tb.Tag?.ToString() == tag)
                {
                    tb.Text = panel.Children.Count.ToString();
                    break;
                }
            }
        }
    }

    private static IEnumerable<DependencyObject> GetAllChildren(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is DependencyObject d)
            {
                yield return d;
                foreach (var sub in GetAllChildren(d))
                    yield return sub;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CONFIRM OVERLAY
    // ══════════════════════════════════════════════════════════════════════════

    private UIElement BuildConfirmOverlay(TodoItem item, Action onConfirm, Action onCancel)
    {
        var overlay = new WpfBorder
        {
            Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x00, 0x00, 0x00)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var centerGrid = new Grid();
        overlay.Child = centerGrid;

        var dialog = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24, 20, 24, 20),
            Width = 380,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        centerGrid.Children.Add(dialog);

        var stack = new StackPanel();
        dialog.Child = stack;

        stack.Children.Add(new TextBlock
        {
            Text = "✓  Concluir Tarefa",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Green),
            Margin = new Thickness(0, 0, 0, 10),
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Deseja marcar a tarefa como concluída?",
            FontSize = 12,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6),
        });

        stack.Children.Add(new WpfBorder
        {
            Background = DashboardTheme.AlphaBrush(DashboardTheme.Green, 0x15),
            BorderBrush = DashboardTheme.AlphaBrush(DashboardTheme.Green, 0x30),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 20),
            Child = new TextBlock
            {
                Text = item.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = DashboardTheme.Brush(DashboardTheme.Text),
                TextWrapping = TextWrapping.Wrap,
            },
        });

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        stack.Children.Add(btnRow);

        void Close()
        {
            _rootGrid?.Children.Remove(overlay);
            if (overlay is FrameworkElement fe
                && fe.Tag is System.Windows.Threading.DispatcherFrame f)
                f.Continue = false;
        }

        var btnCancel = TodoUiFactory.OutlineButton("Cancelar");
        btnCancel.Click += (_, _) => { onCancel(); Close(); };
        btnRow.Children.Add(btnCancel);

        var btnConfirm = TodoUiFactory.PrimaryButton("✓  Confirmar");
        btnConfirm.Margin = new Thickness(10, 0, 0, 0);
        btnConfirm.Click += (_, _) => { onConfirm(); Close(); };
        btnRow.Children.Add(btnConfirm);

        return overlay;
    }
}