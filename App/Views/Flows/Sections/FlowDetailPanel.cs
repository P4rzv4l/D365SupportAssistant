// =============================================================================
//  FlowDetailPanel.cs — Painel lateral de detalhes de um fluxo
// =============================================================================

using D365Assistant.Core.Models.Flows;
using D365Assistant.Views.Dashboard.Theme;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Flows.Sections;

public sealed class FlowDetailPanel
{
    private readonly WpfBorder _panel;
    private readonly StackPanel _content;
    private readonly Action _onClose;
    private readonly string _connectedUrl;

    public WpfBorder Root => _panel;

    public FlowDetailPanel(Action onClose, string connectedUrl = "")
    {
        _onClose = onClose;
        _connectedUrl = connectedUrl;

        _content = new StackPanel();
        _panel = new WpfBorder
        {
            Width = 340,
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Visibility = Visibility.Collapsed,
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _content,
            },
        };
    }

    public void Show(WorkflowItem item)
    {
        _content.Children.Clear();
        _content.Children.Add(BuildHeader(item));
        _content.Children.Add(BuildInfo(item));
        _panel.Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        _panel.Visibility = Visibility.Collapsed;
        _content.Children.Clear();
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private UIElement BuildHeader(WorkflowItem item)
    {
        var border = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 14, 16, 14),
        };

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        border.Child = g;

        var left = new StackPanel();
        Grid.SetColumn(left, 0);
        g.Children.Add(left);

        // Category badge
        var (catFg, catBg) = item.Category switch
        {
            5 => ("#93C5FD", "#0C1F3A"),
            0 => ("#FCD34D", "#3B2A00"),
            2 => ("#A78BFA", "#1E1245"),
            _ => ("#94A3B8", "#1E2A38"),
        };
        left.Children.Add(SmallBadge(item.CategoryLabel, catFg, catBg));

        left.Children.Add(new TextBlock
        {
            Text = item.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        });

        // Close button
        var btnClose = new Button
        {
            Content = "✕",
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Top,
        };
        btnClose.MouseEnter += (_, _) => btnClose.Foreground = DashboardTheme.Brush(DashboardTheme.Text);
        btnClose.MouseLeave += (_, _) => btnClose.Foreground = DashboardTheme.Brush(DashboardTheme.TextSub);
        btnClose.Click += (_, _) => _onClose();
        Grid.SetColumn(btnClose, 1);
        g.Children.Add(btnClose);

        return border;
    }

    // ── Info ──────────────────────────────────────────────────────────────────

    private UIElement BuildInfo(WorkflowItem item)
    {
        var body = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };

        // Action buttons
        var btnRow = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16),
        };

        var btnCopyId = ActionBtn("📋 Copiar ID");
        btnCopyId.Click += (_, _) => System.Windows.Clipboard.SetText(item.WorkflowId);
        btnRow.Children.Add(btnCopyId);

        var btnCopyName = ActionBtn("📋 Copiar Nome");
        btnCopyName.Margin = new Thickness(6, 0, 0, 0);
        btnCopyName.Click += (_, _) => System.Windows.Clipboard.SetText(item.Name);
        btnRow.Children.Add(btnCopyName);

        if (!string.IsNullOrWhiteSpace(_connectedUrl) && item.Category is 0 or 2)
        {
            var url = $"{_connectedUrl}/sfa/workflow/edit.aspx?id={item.WorkflowId}";
            var btnOpen = ActionBtn("🔗 Abrir no Dynamics");
            btnOpen.Margin = new Thickness(6, 4, 0, 0);
            btnOpen.Click += (_, _) =>
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            btnRow.Children.Add(btnOpen);
        }

        body.Children.Add(btnRow);
        body.Children.Add(new WpfBorder
        {
            Height = 1,
            Background = DashboardTheme.Brush(DashboardTheme.Border),
            Margin = new Thickness(0, 0, 0, 14),
        });

        // Fields
        InfoRow(body, "ID", item.WorkflowId, mono: true);
        InfoRow(body, "Status", item.StatusLabel);
        InfoRow(body, "Categoria", item.CategoryLabel);
        InfoRow(body, "Proprietário", item.OwnerName);

        if (item.HasHttpsTrigger)
            InfoRow(body, "Gatilho", "⚡ HTTP (Instant)", valueColor: DashboardTheme.Yellow);

        return body;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void InfoRow(StackPanel parent, string label, string value,
                                bool mono = false, Color? valueColor = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        g.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
        });

        var valTb = new TextBlock
        {
            Text = value,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(valueColor ?? DashboardTheme.Text),
            TextWrapping = TextWrapping.Wrap,
        };
        if (mono) valTb.FontFamily = new FontFamily("Consolas");
        Grid.SetColumn(valTb, 1);
        g.Children.Add(valTb);

        parent.Children.Add(g);
    }

    private static Button ActionBtn(string label) => new()
    {
        Content = label,
        FontSize = 11,
        Background = DashboardTheme.Brush(DashboardTheme.Surface2),
        Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(10, 5, 10, 5),
    };

    private static WpfBorder SmallBadge(string text, string fgHex, string bgHex)
    {
        var fg = (Color)ColorConverter.ConvertFromString(fgHex);
        return new WpfBorder
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, fg.R, fg.G, fg.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fg),
            },
        };
    }
}