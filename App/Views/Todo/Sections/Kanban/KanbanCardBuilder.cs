// =============================================================================
//  KanbanCardBuilder.cs — Constrói um card individual do Kanban
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

namespace D365Assistant.Views.Todo.Sections.Kanban;

public static class KanbanCardBuilder
{
    public static WpfBorder Build(
        TodoItem item,
        Action<TodoItem> onClick,
        MouseButtonEventHandler onMouseDown)
    {
        var card = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand,
            Tag = item,
        };

        card.MouseEnter += (_, _) =>
            card.Background = DashboardTheme.Brush(DashboardTheme.Surface3);
        card.MouseLeave += (_, _) =>
            card.Background = DashboardTheme.Brush(DashboardTheme.Surface2);
        card.MouseLeftButtonDown += onMouseDown;
        card.MouseLeftButtonUp += (_, _) => onClick(item);

        var stack = new StackPanel();
        card.Child = stack;

        // ── Priority strip (top accent line) ──────────────────────────────────
        var priInfo = TodoDisplayMappers.Priority(item.Priority);
        stack.Children.Add(new WpfBorder
        {
            Height = 3,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(
                                  (Color)ColorConverter.ConvertFromString(priInfo.FgHex)),
            Margin = new Thickness(-12, -10, -12, 10),
        });

        // ── Title ─────────────────────────────────────────────────────────────
        stack.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(
                                  item.Done ? DashboardTheme.TextSub : DashboardTheme.Text),
            TextWrapping = TextWrapping.Wrap,
            TextDecorations = item.Done ? TextDecorations.Strikethrough : null,
            Margin = new Thickness(0, 0, 0, 6),
        });

        // ── Description ───────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(item.Description))
            stack.Children.Add(new TextBlock
            {
                Text = item.Description.Length > 80
                                   ? item.Description[..80] + "…"
                                   : item.Description,
                FontSize = 10.5,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
            });

        // ── Footer: due date + priority badge + status badge ──────────────────
        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        stack.Children.Add(footer);

        // Due date
        if (item.DueDate.HasValue)
        {
            var dueTb = new TextBlock
            {
                Text = $"📅 {item.DueDate.Value:dd/MM/yy}",
                FontSize = 10,
                Foreground = DashboardTheme.Brush(
                                        TodoDisplayMappers.DueColor(item.IsOverdue, item.IsDueToday)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(dueTb, 0);
            footer.Children.Add(dueTb);
        }

        // Priority badge
        var priBadge = TodoUiFactory.Badge(priInfo);
        priBadge.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(priBadge, 1);
        footer.Children.Add(priBadge);

        return card;
    }
}