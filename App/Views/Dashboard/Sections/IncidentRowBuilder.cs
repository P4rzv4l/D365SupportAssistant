// =============================================================================
//  IncidentRowBuilder.cs — Constrói uma linha da tabela de chamados
// =============================================================================
// Responsabilidade única: receber um IncidentSnapshot e devolver um Border.
// Não conhece paginação, filtros, tab ativa (exceto para visibilidade de
// satisfação) nem estado global do dashboard.
// =============================================================================

using D365Assistant.Core.Models.Incident;
using D365Assistant.Views.Dashboard.Components;
using D365Assistant.Views.Dashboard.Helpers;
using D365Assistant.Views.Dashboard.Theme;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views.Dashboard.Sections;

public sealed class IncidentRowBuilder
{
    private readonly bool _showSatisfaction;
    private readonly Action<IncidentSnapshot, Border> _onSelect;
    private readonly Func<IncidentSnapshot, ContextMenu> _contextMenuFactory;

    public IncidentRowBuilder(
        bool showSatisfaction,
        Action<IncidentSnapshot, Border> onSelect,
        Func<IncidentSnapshot, ContextMenu> contextMenuFactory)
    {
        _showSatisfaction = showSatisfaction;
        _onSelect = onSelect;
        _contextMenuFactory = contextMenuFactory;
    }

    public Border Build(IncidentSnapshot snap)
    {
        var row = CreateRowContainer(snap);
        var grid = TableColumnFactory.Create(_showSatisfaction);
        row.Child = grid;

        AddIdCell(grid, snap);
        AddSubjectCell(grid, snap);
        AddCustomerCell(grid, snap);
        AddStatusCell(grid, snap);
        AddPriorityCell(grid, snap);
        AddDateCell(grid, snap);
        AddSlaCell(grid, snap);
        AddSatisfactionCell(grid, snap);

        return row;
    }

    // ── Container ─────────────────────────────────────────────────────────────

    private Border CreateRowContainer(IncidentSnapshot snap)
    {
        var row = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 10, 0, 10),   // padding vertical uniforme para todas as células
            Cursor = Cursors.Hand,
            ContextMenu = _contextMenuFactory(snap),
        };

        row.MouseEnter += (_, _) => OnRowMouseEnter(row, snap);
        row.MouseLeave += (_, _) => OnRowMouseLeave(row, snap);
        row.MouseLeftButtonUp += (_, _) => _onSelect(snap, row);

        return row;
    }

    // Hover/selection state is managed externally via tag
    private static void OnRowMouseEnter(Border row, IncidentSnapshot snap)
    {
        if (row.Tag is not "selected")
            row.Background = DashboardTheme.Brush(DashboardTheme.RowHover);
    }

    private static void OnRowMouseLeave(Border row, IncidentSnapshot snap)
    {
        if (row.Tag is not "selected")
            row.Background = DashboardTheme.Brush(DashboardTheme.Surface);
    }

    // ── Cells ─────────────────────────────────────────────────────────────────

    private static void AddIdCell(Grid grid, IncidentSnapshot snap)
    {
        var hasUrl = !string.IsNullOrEmpty(snap.BzpUrl);
        var idColor = hasUrl ? DashboardTheme.Accent : DashboardTheme.TextSub;

        var tb = new TextBlock
        {
            Text = snap.TicketNumber,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = DashboardTheme.Brush(idColor),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 12, 0),  // só horizontal; row tem padding vertical
            Cursor = hasUrl ? Cursors.Hand : Cursors.Arrow,
        };

        if (hasUrl)
            tb.MouseLeftButtonUp += (_, e) =>
            {
                Process.Start(new ProcessStartInfo(snap.BzpUrl!) { UseShellExecute = true });
                e.Handled = true;
            };

        Set(grid, tb, TableColumnFactory.ColId);
    }

    private static void AddSubjectCell(Grid grid, IncidentSnapshot snap)
    {
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };

        stack.Children.Add(new TextBlock
        {
            Text = snap.Title,
            FontSize = 12,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        Set(grid, stack, TableColumnFactory.ColSubject);
    }

    private static void AddCustomerCell(Grid grid, IncidentSnapshot snap)
    {
        var tb = new TextBlock
        {
            Text = snap.CustomerDisplayName,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        Set(grid, tb, TableColumnFactory.ColCustomer);
    }

    private static void AddStatusCell(Grid grid, IncidentSnapshot snap)
    {
        var info = IncidentDisplayMappers.Status(snap.StatusCode);
        var badge = UiFactory.Badge(info.Icon, info.Label, info.FgHex, info.BgHex,
                                   margin: new Thickness(0, 0, 12, 0));
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        Set(grid, badge, TableColumnFactory.ColStatus);
    }

    private static void AddPriorityCell(Grid grid, IncidentSnapshot snap)
    {
        var info = IncidentDisplayMappers.Priority(snap.PriorityCode);
        var badge = UiFactory.Badge(info.Icon, info.Label, info.FgHex, info.BgHex,
                                   margin: new Thickness(0, 0, 12, 0));
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        Set(grid, badge, TableColumnFactory.ColPriority);
    }

    private static void AddDateCell(Grid grid, IncidentSnapshot snap)
    {
        var idleH = snap.HoursSinceModified;
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        stack.Children.Add(new TextBlock
        {
            Text = snap.CreatedOn.ToLocalTime().ToString("dd/MM/yy HH:mm"),
            FontSize = 10.5,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        });

        stack.Children.Add(new TextBlock
        {
            Text = IncidentDisplayMappers.IdleLabel(idleH),
            FontSize = 9.5,
            Foreground = DashboardTheme.Brush(IncidentDisplayMappers.IdleColor(idleH)),
        });

        Set(grid, stack, TableColumnFactory.ColDate);
    }

    private static void AddSlaCell(Grid grid, IncidentSnapshot snap)
    {
        var (icon, text, fgHex, bgHex, tooltip) =
            IncidentDisplayMappers.SlaTableBadge(snap.BzStatusKpiFirst, snap.BzFirstResponseDate);

        var badge = UiFactory.Badge(icon, text, fgHex, bgHex, margin: new Thickness(0, 0, 8, 0));
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        badge.ToolTip = tooltip;
        Set(grid, badge, TableColumnFactory.ColSla);
    }

    private void AddSatisfactionCell(Grid grid, IncidentSnapshot snap)
    {
        var info = IncidentDisplayMappers.Satisfaction(snap.CustomerSatisfactionCode);
        var badge = UiFactory.Badge(info.Icon, info.Label, info.FgHex, info.BgHex,
                                   margin: new Thickness(8, 0, 8, 0));
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.Visibility = _showSatisfaction
                                    ? Visibility.Visible
                                    : Visibility.Collapsed;
        Set(grid, badge, TableColumnFactory.ColSatisf);
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static void Set(Grid g, UIElement el, int col)
    {
        Grid.SetColumn(el, col);
        g.Children.Add(el);
    }
}