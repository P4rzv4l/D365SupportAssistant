// =============================================================================
//  TodoRowBuilder.cs — Constrói uma linha da tabela de tarefas
// =============================================================================
// Responsabilidade única: receber um TodoItem e devolver um Border.
// Não conhece paginação, filtros, abas nem estado global.
// =============================================================================

using D365Assistant.Core.Models.Todo;
using D365Assistant.Views.Dashboard.Theme;
using D365Assistant.Views.Todo.Components;
using D365Assistant.Views.Todo.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Todo.Sections;

public sealed class TodoRowBuilder
{
    private readonly Action<TodoItem> _onToggle;
    private readonly Action<TodoItem, WpfBorder> _onSelect;

    public TodoRowBuilder(
        Action<TodoItem> onToggle,
        Action<TodoItem, WpfBorder> onSelect)
    {
        _onToggle = onToggle;
        _onSelect = onSelect;
    }

    public WpfBorder Build(TodoItem item)
    {
        var row = CreateRowContainer(item);
        var grid = TodoColumnFactory.Create();
        grid.Margin = new Thickness(0);
        row.Child = grid;

        AddTaskCell(grid, item);
        AddRelatedCell(grid, item);
        AddCategoryCell(grid, item);
        AddPriorityCell(grid, item);
        AddDueDateCell(grid, item);
        AddStatusCell(grid, item);

        return row;
    }

    // ── Container ─────────────────────────────────────────────────────────────

    private WpfBorder CreateRowContainer(TodoItem item)
    {
        var row = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 2, 0, 2),
            Cursor = Cursors.Hand,
        };

        row.MouseEnter += (_, _) => OnMouseEnter(row, item);
        row.MouseLeave += (_, _) => OnMouseLeave(row, item);
        row.MouseLeftButtonUp += (_, _) => _onSelect(item, row);

        return row;
    }

    private static void OnMouseEnter(WpfBorder row, TodoItem item)
    {
        if (row.Tag is not "selected")
            row.Background = DashboardTheme.Brush(DashboardTheme.RowHover);
    }

    private static void OnMouseLeave(WpfBorder row, TodoItem item)
    {
        if (row.Tag is not "selected")
            row.Background = DashboardTheme.Brush(DashboardTheme.Surface);
    }

    // ── Cells ─────────────────────────────────────────────────────────────────

    private void AddTaskCell(Grid grid, TodoItem item)
    {
        var col = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 10, 8, 10),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var chk = TodoUiFactory.Checkbox(item.Done);
        chk.MouseLeftButtonUp += (_, e) =>
        {
            _onToggle(item);
            e.Handled = true;
        };
        col.Children.Add(chk);
        col.Children.Add(BuildTitleStack(item));

        Set(grid, col, TodoColumnFactory.ColTask);
    }

    private static StackPanel BuildTitleStack(TodoItem item)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        stack.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(item.Done ? DashboardTheme.TextSub : DashboardTheme.Text),
            TextDecorations = item.Done ? TextDecorations.Strikethrough : null,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 260,
        });

        if (!string.IsNullOrWhiteSpace(item.Description))
            stack.Children.Add(new TextBlock
            {
                Text = item.Description.Length > 55
                                ? item.Description[..55] + "…"
                                : item.Description,
                FontSize = 10.5,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 260,
            });

        return stack;
    }

    private static void AddRelatedCell(Grid grid, TodoItem item)
    {
        var hasTicket = !string.IsNullOrEmpty(item.TicketId);
        var tb = new TextBlock
        {
            Text = item.TicketId ?? "—",
            FontSize = 11,
            Foreground = hasTicket
                ? DashboardTheme.Brush(DashboardTheme.Accent)
                : new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Set(grid, tb, TodoColumnFactory.ColRelated);
    }

    private static void AddCategoryCell(Grid grid, TodoItem item)
    {
        var tb = new TextBlock
        {
            Text = item.Category,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Set(grid, tb, TodoColumnFactory.ColCategory);
    }

    private static void AddPriorityCell(Grid grid, TodoItem item)
    {
        var info = TodoDisplayMappers.Priority(item.Priority);
        var badge = TodoUiFactory.Badge(info);
        Set(grid, badge, TodoColumnFactory.ColPriority);
    }

    private static void AddDueDateCell(Grid grid, TodoItem item)
    {
        var tb = new TextBlock
        {
            Text = item.DueDate.HasValue
                                ? item.DueDate.Value.ToString("dd/MM/yyyy HH:mm")
                                : "—",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(
                                    TodoDisplayMappers.DueColor(item.IsOverdue, item.IsDueToday)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Set(grid, tb, TodoColumnFactory.ColDueDate);
    }

    private static void AddStatusCell(Grid grid, TodoItem item)
    {
        var info = TodoDisplayMappers.Status(item.Done, item.IsOverdue);
        var badge = TodoUiFactory.Badge(info);
        Set(grid, badge, TodoColumnFactory.ColStatus);
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static void Set(Grid g, UIElement el, int col)
    {
        Grid.SetColumn(el, col);
        g.Children.Add(el);
    }
}