// =============================================================================
//  TodoDetailBuilder.cs — Painel lateral de detalhes de uma tarefa
// =============================================================================
// Responsabilidade única: dado um TodoItem, preencher o StackPanel de detalhes.
// =============================================================================

using D365Assistant.Core.Models.Todo;
using D365Assistant.Views.Dashboard.Theme;
using D365Assistant.Views.Todo.Components;
using D365Assistant.Views.Todo.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views.Todo.Sections;

public sealed class TodoDetailBuilder
{
    private readonly Action<TodoItem> _onEdit;
    private readonly Action<TodoItem> _onToggle;
    private readonly Action<TodoItem> _onDelete;
    private readonly Action _onClose;

    public TodoDetailBuilder(
        Action<TodoItem> onEdit,
        Action<TodoItem> onToggle,
        Action<TodoItem> onDelete,
        Action onClose)
    {
        _onEdit = onEdit;
        _onToggle = onToggle;
        _onDelete = onDelete;
        _onClose = onClose;
    }

    public void Populate(StackPanel container, TodoItem item)
    {
        container.Children.Add(BuildHeader(item));
        container.Children.Add(BuildBody(item));
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private UIElement BuildHeader(TodoItem item)
    {
        var border = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12),
        };

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        border.Child = g;

        g.Children.Add(new TextBlock
        {
            Text = "Detalhes da Tarefa",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var actions = BuildHeaderActions(item);
        Grid.SetColumn(actions, 1);
        g.Children.Add(actions);

        return border;
    }

    private UIElement BuildHeaderActions(TodoItem item)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        var btnEdit = TodoUiFactory.LinkButton("✎ Editar");
        btnEdit.Click += (_, _) => _onEdit(item);
        panel.Children.Add(btnEdit);

        var toggleLabel = item.Done ? "↩ Reabrir" : "✓ Concluir";
        var toggleColor = item.Done ? DashboardTheme.TextSub : DashboardTheme.Green;
        var btnToggle = TodoUiFactory.LinkButton(toggleLabel, toggleColor);
        btnToggle.Click += (_, _) => _onToggle(item);
        panel.Children.Add(btnToggle);

        var btnDelete = TodoUiFactory.LinkButton("🗑 Excluir", DashboardTheme.Red);
        btnDelete.Click += (_, _) => _onDelete(item);
        panel.Children.Add(btnDelete);

        var btnClose = BuildCloseButton();
        panel.Children.Add(btnClose);

        return panel;
    }

    private Button BuildCloseButton()
    {
        var btn = new Button
        {
            Content = "✕",
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Fechar detalhes",
        };
        btn.MouseEnter += (_, _) => btn.Foreground = DashboardTheme.Brush(DashboardTheme.Text);
        btn.MouseLeave += (_, _) => btn.Foreground = DashboardTheme.Brush(DashboardTheme.TextSub);
        btn.Click += (_, _) => _onClose();
        return btn;
    }

    // ── Body ──────────────────────────────────────────────────────────────────

    private static UIElement BuildBody(TodoItem item)
    {
        var body = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };

        DetailRow(body, "Título", new TextBlock
        {
            Text = item.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            TextWrapping = TextWrapping.Wrap,
        });

        if (!string.IsNullOrWhiteSpace(item.Description))
            DetailRow(body, "Descrição", new TextBlock
            {
                Text = item.Description,
                FontSize = 11,
                Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrEmpty(item.TicketId))
            DetailRow(body, "Relacionado a", new TextBlock
            {
                Text = item.TicketId,
                FontSize = 11,
                Foreground = DashboardTheme.Brush(DashboardTheme.Accent),
                TextDecorations = TextDecorations.Underline,
                Cursor = Cursors.Hand,
            });

        DetailRow(body, "Tipo", new TextBlock
        {
            Text = item.Category,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        });

        DetailRow(body, "Prioridade", TodoUiFactory.Badge(TodoDisplayMappers.Priority(item.Priority)));
        DetailRow(body, "Status", TodoUiFactory.Badge(TodoDisplayMappers.Status(item.Done, item.IsOverdue)));

        if (item.DueDate.HasValue)
            DetailRow(body, "Vencimento", new TextBlock
            {
                Text = $"📅 {item.DueDate.Value:dd/MM/yyyy HH:mm}",
                FontSize = 11,
                Foreground = DashboardTheme.Brush(
                                 TodoDisplayMappers.DueColor(item.IsOverdue, item.IsDueToday)),
            });

        DetailRow(body, "Criado em", new TextBlock
        {
            Text = $"🕐 {item.CreatedAt:dd/MM/yyyy HH:mm}",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        });

        if (item.Done && item.DoneAt.HasValue)
            DetailRow(body, "Concluído em", new TextBlock
            {
                Text = $"✓ {item.DoneAt.Value:dd/MM/yyyy HH:mm}",
                FontSize = 11,
                Foreground = DashboardTheme.Brush(DashboardTheme.Green),
            });

        return body;
    }

    private static void DetailRow(StackPanel parent, string label, UIElement value)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        g.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
        });

        Grid.SetColumn(value, 1);
        g.Children.Add(value);

        parent.Children.Add(g);
    }
}