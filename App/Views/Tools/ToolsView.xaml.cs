// =============================================================================
//  ToolsView.xaml.cs — Orquestrador do ToolsView (refatorado)
// =============================================================================

using D365Assistant.ViewModels;
using D365Assistant.Views.Tools.Components;
using D365Assistant.Views.Tools.Sections;
using D365Assistant.Views.Tools.Theme;
using D365Assistant.Views.Flows.Sections;
using D365Assistant.Core.Services;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace D365Assistant.Views;

public partial class ToolsView : Page
{
    private readonly WebResourcesViewModel _vm;
    private readonly WebResourcesPanelBuilder _webResourcesBuilder;
    private readonly FlowsPanelBuilder _flowsBuilder;

    private Button _tabFlows = null!;
    private Button _tabWebRes = null!;
    private Border _panelFlows = null!;
    private Border _panelWebRes = null!;

    private static class Tabs
    {
        public const string Flows = "⚡  Fluxos";
        public const string WebResources = "🌐  Recursos da Web";
    }

    public ToolsView(
        WebResourcesViewModel vm,
        HttpClient http,
        IExternalAuthService auth,
        VaultViewModel vault,
        VaultService vaultService,
        FlowsViewModel flowsVm)
    {
        _vm = vm;
        _webResourcesBuilder = new WebResourcesPanelBuilder(vm, http, auth, vault, vaultService);
        _flowsBuilder = new FlowsPanelBuilder(flowsVm);
        DataContext = vm;
        Title = "Ferramentas";
        Background = ToolsTheme.Brush(ToolsTheme.Bg);

        var root = new DockPanel { Margin = new Thickness(24, 20, 24, 20) };

        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var tabBar = BuildTabBar();
        DockPanel.SetDock(tabBar, Dock.Top);
        root.Children.Add(tabBar);

        _panelFlows = BuildPanel(_flowsBuilder.Build(), visible: false);
        DockPanel.SetDock(_panelFlows, Dock.Top);
        root.Children.Add(_panelFlows);

        _panelWebRes = BuildPanel(_webResourcesBuilder.Build(), visible: true);
        root.Children.Add(_panelWebRes);

        Content = root;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LAYOUT
    // ══════════════════════════════════════════════════════════════════════════

    private static UIElement BuildHeader()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

        stack.Children.Add(new TextBlock
        {
            Text = "🛠️  Ferramentas",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Utilitários para interagir diretamente com o ambiente Dynamics 365.",
            FontSize = 12,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextMuted),
            Margin = new Thickness(0, 4, 0, 0),
        });

        return stack;
    }

    private UIElement BuildTabBar()
    {
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };

        _tabFlows = ToolsUiFactory.TabButton(Tabs.Flows, active: false);
        _tabWebRes = ToolsUiFactory.TabButton(Tabs.WebResources, active: true);

        _tabFlows.Click += (_, _) => ActivateTab(showFlows: true);
        _tabWebRes.Click += (_, _) => ActivateTab(showFlows: false);

        tabs.Children.Add(_tabFlows);
        tabs.Children.Add(_tabWebRes);

        return new Border
        {
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Surface2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 0, 0, 20),
            Child = tabs,
        };
    }

    private static Border BuildPanel(UIElement content, bool visible) => new()
    {
        Visibility = visible ? Visibility.Visible : Visibility.Collapsed,
        Child = content,
    };

    // ══════════════════════════════════════════════════════════════════════════
    //  TAB SWITCHING
    // ══════════════════════════════════════════════════════════════════════════

    private void ActivateTab(bool showFlows)
    {
        _panelFlows.Visibility = showFlows ? Visibility.Visible : Visibility.Collapsed;
        _panelWebRes.Visibility = !showFlows ? Visibility.Visible : Visibility.Collapsed;

        ToolsUiFactory.SetTabActive(_tabFlows, showFlows);
        ToolsUiFactory.SetTabActive(_tabWebRes, !showFlows);
    }
}