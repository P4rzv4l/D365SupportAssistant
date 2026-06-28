// =============================================================================
//  WebResourceDetailPanel.cs — Painel lateral de detalhes do Web Resource
// =============================================================================

using D365Assistant.Core.Models.WebResource;
using D365Assistant.ViewModels;
using D365Assistant.Views.Tools.Components;
using D365Assistant.Views.Tools.Theme;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Tools.Sections;

public sealed class WebResourceDetailPanel
{
    private readonly WebResourcesViewModel _vm;
    private readonly WpfBorder _panel;
    private readonly StackPanel _content;

    public WpfBorder Root => _panel;

    public WebResourceDetailPanel(WebResourcesViewModel vm)
    {
        _vm = vm;

        _content = new StackPanel();

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _content,
        };

        _panel = new WpfBorder
        {
            Width = 340,
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Visibility = Visibility.Collapsed,
            Child = scroll,
        };
    }

    public void Show(WebResource resource)
    {
        _content.Children.Clear();
        _content.Children.Add(BuildHeader(resource));
        _content.Children.Add(BuildActions(resource));
        _content.Children.Add(BuildInfoSection(resource));
        _panel.Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        _panel.Visibility = Visibility.Collapsed;
        _content.Children.Clear();
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private UIElement BuildHeader(WebResource r)
    {
        var border = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 14, 16, 14),
        };

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        border.Child = g;

        // Type icon + name
        var left = new StackPanel();
        Grid.SetColumn(left, 0);
        g.Children.Add(left);

        // Type badge
        var (typeFg, typeBg) = TypeColors(r.TypeCode);
        left.Children.Add(new WpfBorder
        {
            Background = new SolidColorBrush(typeBg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, typeFg.R, typeFg.G, typeFg.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = r.TypeLabel,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(typeFg),
            },
        });

        left.Children.Add(new TextBlock
        {
            Text = r.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
            TextWrapping = TextWrapping.Wrap,
        });

        if (!string.IsNullOrWhiteSpace(r.DisplayName))
            left.Children.Add(new TextBlock
            {
                Text = r.DisplayName,
                FontSize = 11,
                Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
                Margin = new Thickness(0, 4, 0, 0),
            });

        // Close button
        var btnClose = new Button
        {
            Content = "✕",
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Top,
        };
        btnClose.MouseEnter += (_, _) => btnClose.Foreground = ToolsTheme.Brush(ToolsTheme.Text);
        btnClose.MouseLeave += (_, _) => btnClose.Foreground = ToolsTheme.Brush(ToolsTheme.TextSub);
        btnClose.Click += (_, _) => Hide();
        Grid.SetColumn(btnClose, 1);
        g.Children.Add(btnClose);

        return border;
    }

    // ── Action buttons ────────────────────────────────────────────────────────

    private UIElement BuildActions(WebResource r)
    {
        var border = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10),
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        border.Child = row;

        // Copy name
        var btnCopyName = SmallButton("📋 Copiar nome", ToolsTheme.Accent);
        btnCopyName.Click += (_, _) => _vm.CopyNameCommand.Execute(r);
        row.Children.Add(btnCopyName);

        // Copy ID
        var btnCopyId = SmallButton("🔑 Copiar ID", ToolsTheme.Surface2);
        btnCopyId.Margin = new Thickness(6, 0, 0, 0);
        btnCopyId.Click += (_, _) => _vm.CopyIdCommand.Execute(r);
        row.Children.Add(btnCopyId);

        // Open in Dynamics
        var envUrl = _vm.EnvironmentUrl.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            var url = $"{envUrl}/main.aspx?etn=webresource&id={r.WebResourceId}&pagetype=entityrecord";
            var btnOpen = SmallButton("🔗 Abrir", ToolsTheme.Surface2);
            btnOpen.Margin = new Thickness(6, 0, 0, 0);
            btnOpen.ToolTip = "Abrir no Dynamics 365";
            btnOpen.Click += (_, _) =>
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            row.Children.Add(btnOpen);
        }

        return border;
    }

    // ── Info section ──────────────────────────────────────────────────────────

    private static UIElement BuildInfoSection(WebResource r)
    {
        var body = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };

        InfoRow(body, "ID", r.WebResourceId, mono: true);
        InfoRow(body, "Nome lógico", r.Name, mono: true);
        InfoRow(body, "Tipo", r.TypeLabel);
        InfoRow(body, "Solução", r.ManagedLabel);
        InfoRow(body, "Modificado em", r.ModifiedOnFormatted);

        return body;
    }

    private static void InfoRow(StackPanel parent, string label, string value,
                                bool mono = false)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "—") return;

        var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        g.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
        });

        var valueTb = new TextBlock
        {
            Text = value,
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
            TextWrapping = TextWrapping.Wrap,
        };
        if (mono) valueTb.FontFamily = new FontFamily("Consolas");

        Grid.SetColumn(valueTb, 1);
        g.Children.Add(valueTb);

        parent.Children.Add(g);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Button SmallButton(string label, Color bg) => new()
    {
        Content = label,
        FontSize = 11,
        Background = new SolidColorBrush(bg),
        Foreground = ToolsTheme.Brush(ToolsTheme.Text),
        BorderThickness = new Thickness(1),
        BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
        Cursor = Cursors.Hand,
        Padding = new Thickness(10, 5, 10, 5),
    };

    private static (Color fg, Color bg) TypeColors(int typeCode) => typeCode switch
    {
        1 => (ToolsTheme.Blue, Color.FromRgb(0x0C, 0x1F, 0x3A)), // HTML
        2 => (ToolsTheme.Purple, Color.FromRgb(0x1E, 0x12, 0x45)), // CSS
        3 => (ToolsTheme.Yellow, Color.FromRgb(0x3B, 0x2A, 0x00)), // JS
        11 => (Color.FromRgb(0x6E, 0xE7, 0xB7), Color.FromRgb(0x0A, 0x1F, 0x14)), // SVG
        _ => (ToolsTheme.TextSub, ToolsTheme.Surface2),
    };
}