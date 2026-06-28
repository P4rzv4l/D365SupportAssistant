// =============================================================================
//  FlowsPanelBuilder.cs — Painel placeholder de Fluxos
// =============================================================================

using D365Assistant.Views.Tools.Components;
using D365Assistant.Views.Tools.Theme;
using System.Windows;
using System.Windows.Controls;

namespace D365Assistant.Views.Tools.Sections;

public static class FlowsPanelBuilder
{
    public static UIElement Build()
    {
        var card = ToolsUiFactory.Card();

        var inner = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 60, 0, 60),
        };

        inner.Children.Add(new TextBlock
        {
            Text = "⚡",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
        });

        inner.Children.Add(new TextBlock
        {
            Text = "Ferramenta de Fluxos",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        inner.Children.Add(new TextBlock
        {
            Text = "Em breve — esta aba receberá as ferramentas de análise de fluxos.",
            FontSize = 13,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextMuted),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            TextAlignment = TextAlignment.Center,
        });

        card.Child = inner;
        return card;
    }
}