// =============================================================================
//  FlowRowBuilder.cs — Constrói uma linha da tabela de fluxos
// =============================================================================

using D365Assistant.Core.Models.Flows;
using D365Assistant.Views.Dashboard.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Flows.Sections;

public sealed class FlowRowBuilder
{
    private readonly Action<WorkflowItem, WpfBorder> _onSelect;
    private readonly Action<WorkflowItem> _onCopyId;
    private readonly Action<WorkflowItem> _onOpenDynamics;
    private readonly string _connectedUrl;

    public FlowRowBuilder(
        Action<WorkflowItem, WpfBorder> onSelect,
        Action<WorkflowItem> onCopyId,
        Action<WorkflowItem> onOpenDynamics,
        string connectedUrl)
    {
        _onSelect = onSelect;
        _onCopyId = onCopyId;
        _onOpenDynamics = onOpenDynamics;
        _connectedUrl = connectedUrl;
    }

    public WpfBorder Build(WorkflowItem item)
    {
        var row = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 2, 0, 2),
            Cursor = Cursors.Hand,
            ContextMenu = BuildContextMenu(item),
        };

        row.MouseEnter += (_, _) => { if (row.Tag is not "selected") row.Background = DashboardTheme.Brush(DashboardTheme.RowHover); };
        row.MouseLeave += (_, _) => { if (row.Tag is not "selected") row.Background = DashboardTheme.Brush(DashboardTheme.Surface); };
        row.MouseLeftButtonUp += (_, _) => _onSelect(item, row);

        var g = ColGrid();
        row.Child = g;

        // Col 0: status dot + name
        AddNameCell(g, item);

        // Col 1: category badge
        AddCategoryCell(g, item);

        // Col 2: owner
        AddOwnerCell(g, item);

        // Col 3: status badge
        AddStatusCell(g, item);

        // Col 4: trigger badge (HTTPS) — só Cloud Flows
        AddTriggerCell(g, item);

        return row;
    }

    // ── Cells ─────────────────────────────────────────────────────────────────

    private static void AddNameCell(Grid g, WorkflowItem item)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 10, 8, 10),
        };

        // Active dot
        stack.Children.Add(new WpfBorder
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            Background = item.IsActive
                ? DashboardTheme.Brush(DashboardTheme.Green)
                : DashboardTheme.Brush(DashboardTheme.TextSub),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });

        stack.Children.Add(new TextBlock
        {
            Text = item.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(
                               item.IsActive ? DashboardTheme.Text : DashboardTheme.TextSub),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        Grid.SetColumn(stack, 0);
        g.Children.Add(stack);
    }

    private static void AddCategoryCell(Grid g, WorkflowItem item)
    {
        var (fg, bg) = item.Category switch
        {
            5 => ("#93C5FD", "#0C1F3A"),
            0 => ("#FCD34D", "#3B2A00"),
            2 => ("#A78BFA", "#1E1245"),
            _ => ("#94A3B8", "#1E2A38"),
        };
        var badge = Badge(item.CategoryLabel, fg, bg);
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(badge, 1);
        g.Children.Add(badge);
    }

    private static void AddOwnerCell(Grid g, WorkflowItem item)
    {
        var tb = new TextBlock
        {
            Text = item.OwnerName,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(tb, 2);
        g.Children.Add(tb);
    }

    private static void AddStatusCell(Grid g, WorkflowItem item)
    {
        var badge = Badge(
            item.IsActive ? "Ativo" : "Inativo",
            item.IsActive ? "#86EFAC" : "#94A3B8",
            item.IsActive ? "#0A2010" : "#1E2A38");
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(badge, 3);
        g.Children.Add(badge);
    }

    private static void AddTriggerCell(Grid g, WorkflowItem item)
    {
        if (!item.HasHttpsTrigger)
        {
            Grid.SetColumn(new UIElement(), 4);
            return;
        }

        var badge = Badge("⚡ HTTPS", "#FCD34D", "#3B2A00");
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        badge.ToolTip = "Gatilho HTTP (Instant Cloud Flow)";
        Grid.SetColumn(badge, 4);
        g.Children.Add(badge);
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private ContextMenu BuildContextMenu(WorkflowItem item)
    {
        var menu = new ContextMenu();

        var mCopyId = new MenuItem { Header = "📋 Copiar ID" };
        mCopyId.Click += (_, _) => _onCopyId(item);
        menu.Items.Add(mCopyId);

        var mCopyName = new MenuItem { Header = "📋 Copiar Nome" };
        mCopyName.Click += (_, _) => System.Windows.Clipboard.SetText(item.Name);
        menu.Items.Add(mCopyName);

        if (!string.IsNullOrWhiteSpace(_connectedUrl) && item.Category is 0 or 2)
        {
            menu.Items.Add(new Separator());
            var mOpen = new MenuItem { Header = "🔗 Abrir no Dynamics" };
            mOpen.Click += (_, _) => _onOpenDynamics(item);
            menu.Items.Add(mOpen);
        }

        return menu;
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private static Grid ColGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) }); // nome
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });                    // categoria
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });   // owner
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });                     // status
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });                     // trigger
        return g;
    }

    private static WpfBorder Badge(string text, string fgHex, string bgHex)
    {
        var fg = (Color)ColorConverter.ConvertFromString(fgHex);
        return new WpfBorder
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, fg.R, fg.G, fg.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fg),
            },
        };
    }
}