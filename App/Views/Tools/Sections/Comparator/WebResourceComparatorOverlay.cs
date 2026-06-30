// =============================================================================
//  WebResourceComparatorOverlay.cs — Comparador lado a lado entre ambientes
// =============================================================================
// Fluxo:
//   1. Usuário escolhe cliente do Vault → dois ambientes (VaultLink)
//   2. App busca o recurso pelo nome nos dois ambientes via API
//   3. Exibe diff lado a lado: linhas iguais, removidas (vermelho) e adicionadas (verde)
// =============================================================================

using D365Assistant.Core.Models.Vault;
using D365Assistant.Core.Models.WebResource;
using D365Assistant.Core.Services;
using D365Assistant.ViewModels;
using D365Assistant.Views.Tools.Theme;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Tools.Sections.Comparator;

public sealed class WebResourceComparatorOverlay
{
    private readonly HttpClient _http;
    private readonly IExternalAuthService _auth;
    private readonly VaultViewModel _vault;
    private readonly VaultService _vaultService;

    // ── UI refs ───────────────────────────────────────────────────────────────
    private readonly WpfBorder _overlay;
    private StackPanel? _envLeftPanel;
    private StackPanel? _envRightPanel;
    private TextBlock? _resourceNameTb;
    private TextBlock? _statusTb;
    private Grid? _diffArea;
    private TextBlock? _statsLeft;
    private TextBlock? _statsRight;
    private TextBlock? _statsDiff;

    // ── State ─────────────────────────────────────────────────────────────────
    private VaultLink? _envLeft;
    private VaultLink? _envRight;
    private string _resourceName = "";

    public WpfBorder Root => _overlay;

    public WebResourceComparatorOverlay(
        HttpClient http,
        IExternalAuthService auth,
        VaultViewModel vault,
        VaultService vaultService)
    {
        _http = http;
        _auth = auth;
        _vault = vault;
        _vaultService = vaultService;
        _overlay = Build();

        // Repopula os seletores automaticamente quando o Vault carrega/recarrega
        // clientes (ex: depois de desbloquear) — corrige o "Nenhum ambiente"
        // que aparecia antes dos dados chegarem
        _vault.Clients.CollectionChanged += (_, _) =>
        {
            if (_overlay.Visibility == Visibility.Visible)
                RefreshEnvSelectors();
        };

        _vault.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VaultViewModel.IsUnlocked)
                && _overlay.Visibility == Visibility.Visible)
                RefreshEnvSelectors();
        };
    }

    // ── Show ──────────────────────────────────────────────────────────────────

    public void Show(WebResource resource)
    {
        _resourceName = resource.Name;
        if (_resourceNameTb != null) _resourceNameTb.Text = resource.Name;
        if (_statusTb != null) _statusTb.Text = "Selecione os ambientes e clique em Comparar.";
        if (_diffArea != null) _diffArea.Children.Clear();

        RefreshEnvSelectors();
        _overlay.Visibility = Visibility.Visible;
    }

    public void Hide() => _overlay.Visibility = Visibility.Collapsed;

    // ══════════════════════════════════════════════════════════════════════════
    //  BUILD UI
    // ══════════════════════════════════════════════════════════════════════════

    private WpfBorder Build()
    {
        var overlay = new WpfBorder
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF8, 0x0D, 0x11, 0x17)),
            Visibility = Visibility.Collapsed,
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // env selector
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // stats bar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // diff
        overlay.Child = root;

        AddRow(root, BuildHeader(), 0);
        AddRow(root, BuildEnvSelector(), 1);
        AddRow(root, BuildStatsBar(), 2);
        AddRow(root, BuildDiffArea(), 3);

        return overlay;
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private UIElement BuildHeader()
    {
        var bar = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 12, 20, 12),
        };

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = g;

        var left = new StackPanel();
        Grid.SetColumn(left, 0);
        g.Children.Add(left);

        left.Children.Add(new TextBlock
        {
            Text = "🔍 Comparador de Ambientes",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
        });

        _resourceNameTb = new TextBlock
        {
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(0, 4, 0, 0),
        };
        left.Children.Add(_resourceNameTb);

        var btnClose = SmallButton("✕ Fechar");
        btnClose.Click += (_, _) => Hide();
        Grid.SetColumn(btnClose, 1);
        g.Children.Add(btnClose);

        return bar;
    }

    // ── Environment selector ──────────────────────────────────────────────────

    private UIElement BuildEnvSelector()
    {
        var bar = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 12, 20, 12),
        };

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Child = g;

        // Left env panel
        _envLeftPanel = new StackPanel();
        Grid.SetColumn(_envLeftPanel, 0);
        g.Children.Add(_envLeftPanel);

        // Separator
        var sep = new WpfBorder
        {
            Width = 1,
            Background = ToolsTheme.Brush(ToolsTheme.Border),
            Margin = new Thickness(16, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetColumn(sep, 1);
        g.Children.Add(sep);

        // Right env panel
        _envRightPanel = new StackPanel();
        Grid.SetColumn(_envRightPanel, 2);
        g.Children.Add(_envRightPanel);

        // Compare button
        var btnCompare = new Button
        {
            Content = "⚖ Comparar",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = ToolsTheme.Brush(ToolsTheme.Accent),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(20, 10, 20, 10),
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        btnCompare.Click += async (_, _) => await RunComparisonAsync();
        Grid.SetColumn(btnCompare, 3);
        g.Children.Add(btnCompare);

        return bar;
    }

    private void RefreshEnvSelectors()
    {
        if (_envLeftPanel == null || _envRightPanel == null) return;

        _envLeftPanel.Children.Clear();
        _envRightPanel.Children.Clear();

        // Check vault state
        if (!_vault.IsUnlocked)
        {
            _envLeftPanel.Children.Add(BuildVaultLockedPrompt());
            return;
        }

        var clients = _vault.Clients.ToList();
        if (clients.Count == 0)
        {
            _envLeftPanel.Children.Add(NoClientsMessage());
            return;
        }

        BuildEnvPanel(_envLeftPanel, "Ambiente A (base)", side: "left");
        BuildEnvPanel(_envRightPanel, "Ambiente B (comparar)", side: "right");
    }

    private void BuildEnvPanel(StackPanel panel, string title, string side)
    {
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            Margin = new Thickness(0, 0, 0, 6),
        });

        // Client selector
        var clientLabel = new TextBlock
        {
            Text = "Cliente:",
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            Margin = new Thickness(0, 0, 0, 4),
        };
        panel.Children.Add(clientLabel);

        // Links dropdown (populated when client changes)
        var linksPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var linkLabel = new TextBlock
        {
            Text = "Ambiente:",
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            Margin = new Thickness(0, 6, 0, 4),
        };

        // Current selection display
        var selectionTb = new TextBlock
        {
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextMuted),
            FontStyle = FontStyles.Italic,
            Text = "Nenhum ambiente selecionado",
            Margin = new Thickness(0, 4, 0, 0),
        };
        panel.Children.Add(selectionTb);

        // Build client buttons
        var clientsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        panel.Children.Add(clientsWrap);

        StackPanel? activeLinksContainer = null;

        foreach (var client in _vault.Clients)
        {
            var c = client;
            var btnC = new Button
            {
                Content = c.Name,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(0x22, ParseColor(c.Color).R, ParseColor(c.Color).G, ParseColor(c.Color).B)),
                Foreground = new SolidColorBrush(ParseColor(c.Color)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, ParseColor(c.Color).R, ParseColor(c.Color).G, ParseColor(c.Color).B)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 4),
            };

            btnC.Click += (_, _) =>
            {
                // Clear previous links panel
                if (activeLinksContainer != null)
                    panel.Children.Remove(activeLinksContainer);

                activeLinksContainer = new StackPanel();
                panel.Children.Add(activeLinksContainer);

                activeLinksContainer.Children.Add(new TextBlock
                {
                    Text = $"Ambientes de {c.Name}:",
                    FontSize = 10,
                    Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
                    Margin = new Thickness(0, 4, 0, 4),
                });

                // Busca direto do VaultService pelo clientId — não depende do
                // cliente "selecionado" na tela Vault, que é uma coleção
                // separada (_vault.Links) só preenchida via SelectClient()
                var links = _vaultService.GetLinks(c.Id);

                if (links.Count == 0)
                {
                    activeLinksContainer.Children.Add(new TextBlock
                    {
                        Text = "Nenhum ambiente cadastrado para este cliente.",
                        FontSize = 10,
                        Foreground = ToolsTheme.Brush(ToolsTheme.TextMuted),
                    });
                    return;
                }

                var linksWrap = new WrapPanel();
                activeLinksContainer.Children.Add(linksWrap);

                foreach (var link in links)
                {
                    var lnk = link;
                    var btnL = new Button
                    {
                        Content = lnk.EnvName,
                        FontSize = 11,
                        Background = ToolsTheme.Brush(ToolsTheme.Surface2),
                        Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
                        BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(10, 4, 10, 4),
                        Margin = new Thickness(0, 0, 6, 4),
                        ToolTip = lnk.Url,
                    };
                    btnL.Click += (_, _) =>
                    {
                        // Deselect siblings
                        foreach (Button b in linksWrap.Children)
                        {
                            b.Background = ToolsTheme.Brush(ToolsTheme.Surface2);
                            b.Foreground = ToolsTheme.Brush(ToolsTheme.TextSub);
                            b.BorderBrush = ToolsTheme.Brush(ToolsTheme.Border);
                        }
                        btnL.Background = new SolidColorBrush(Color.FromArgb(0x33,
                                               ToolsTheme.Accent.R,
                                               ToolsTheme.Accent.G,
                                               ToolsTheme.Accent.B));
                        btnL.Foreground = ToolsTheme.Brush(ToolsTheme.Accent);
                        btnL.BorderBrush = ToolsTheme.Brush(ToolsTheme.Accent);

                        if (side == "left") { _envLeft = lnk; }
                        else { _envRight = lnk; }

                        selectionTb.Text = $"{c.Name} / {lnk.EnvName}";
                        selectionTb.Foreground = ToolsTheme.Brush(ToolsTheme.Text);
                        selectionTb.FontStyle = FontStyles.Normal;
                    };
                    linksWrap.Children.Add(btnL);
                }
            };

            clientsWrap.Children.Add(btnC);
        }
    }

    // ── Stats bar ─────────────────────────────────────────────────────────────

    private UIElement BuildStatsBar()
    {
        var bar = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 8, 20, 8),
            Visibility = Visibility.Collapsed,
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Child = row;

        _statsLeft = StatLabel("", ToolsTheme.TextSub);
        _statsRight = StatLabel("", ToolsTheme.TextSub);
        _statsDiff = StatLabel("", ToolsTheme.Yellow);

        row.Children.Add(_statsLeft);
        row.Children.Add(Separator());
        row.Children.Add(_statsRight);
        row.Children.Add(Separator());
        row.Children.Add(_statsDiff);

        _statusTb = StatLabel("", ToolsTheme.TextMuted);
        row.Children.Add(Separator());
        row.Children.Add(_statusTb);

        return bar;
    }

    // ── Diff area ─────────────────────────────────────────────────────────────

    private UIElement BuildDiffArea()
    {
        _diffArea = new Grid();
        return _diffArea;
    }

    // ── Run comparison ────────────────────────────────────────────────────────

    private async Task RunComparisonAsync()
    {
        if (_envLeft == null || _envRight == null)
        {
            if (_statusTb != null) _statusTb.Text = "⚠ Selecione os dois ambientes antes de comparar.";
            return;
        }

        if (_diffArea != null) _diffArea.Children.Clear();
        if (_statusTb != null) _statusTb.Text = "⏳ Buscando recurso nos dois ambientes...";

        try
        {
            var results = await Task.WhenAll(
                FetchContentAsync(_resourceName, _envLeft.Url),
                FetchContentAsync(_resourceName, _envRight.Url));
            var contentLeft = results[0];
            var contentRight = results[1];

            var diff = DiffEngine.Compute(contentLeft, contentRight);

            UpdateStats(contentLeft, contentRight, diff);
            RenderDiff(diff);

            if (_statusTb != null)
                _statusTb.Text = diff.Any(d => d.Status != DiffStatus.Equal)
                    ? $"⚠ Diferenças encontradas"
                    : "✓ Arquivos idênticos";
        }
        catch (Exception ex)
        {
            if (_statusTb != null)
                _statusTb.Text = $"⚠ Erro: {ex.Message}";
        }
    }

    private void UpdateStats(string left, string right, List<DiffLine> diff)
    {
        var added = diff.Count(d => d.Status == DiffStatus.Added);
        var removed = diff.Count(d => d.Status == DiffStatus.Removed);

        if (_statsLeft != null) _statsLeft.Text = $"A: {CountLines(left)} linhas";
        if (_statsRight != null) _statsRight.Text = $"B: {CountLines(right)} linhas";
        if (_statsDiff != null) _statsDiff.Text = $"+{added} / -{removed} linhas diferentes";
    }

    // ── Render diff ───────────────────────────────────────────────────────────

    private void RenderDiff(List<DiffLine> diff)
    {
        if (_diffArea == null) return;
        _diffArea.Children.Clear();
        _diffArea.ColumnDefinitions.Clear();
        _diffArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _diffArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftPanel = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)) };
        var rightPanel = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)) };

        var leftScroll = new ScrollViewer
        {
            Content = leftPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var rightScroll = new ScrollViewer
        {
            Content = rightPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        // Sync vertical scroll
        leftScroll.ScrollChanged += (_, e) =>
        {
            if (e.VerticalChange != 0)
                rightScroll.ScrollToVerticalOffset(leftScroll.VerticalOffset);
        };
        rightScroll.ScrollChanged += (_, e) =>
        {
            if (e.VerticalChange != 0)
                leftScroll.ScrollToVerticalOffset(rightScroll.VerticalOffset);
        };

        Grid.SetColumn(leftScroll, 0);
        Grid.SetColumn(rightScroll, 1);
        _diffArea.Children.Add(leftScroll);
        _diffArea.Children.Add(rightScroll);

        // Column divider
        var divider = new WpfBorder
        {
            Width = 1,
            Background = ToolsTheme.Brush(ToolsTheme.Border),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(divider, 0);
        _diffArea.Children.Add(divider);

        foreach (var line in diff)
        {
            var (leftRow, rightRow) = BuildDiffRow(line);
            leftPanel.Children.Add(leftRow);
            rightPanel.Children.Add(rightRow);
        }
    }

    private static (UIElement left, UIElement right) BuildDiffRow(DiffLine line)
    {
        var (leftBg, rightBg) = line.Status switch
        {
            DiffStatus.Removed => (Color.FromArgb(0x33, 0xEF, 0x44, 0x44), Color.FromRgb(0x0D, 0x11, 0x17)),
            DiffStatus.Added => (Color.FromRgb(0x0D, 0x11, 0x17), Color.FromArgb(0x33, 0x22, 0xC5, 0x5E)),
            _ => (Color.FromRgb(0x0D, 0x11, 0x17), Color.FromRgb(0x0D, 0x11, 0x17)),
        };

        var (leftFg, rightFg) = line.Status switch
        {
            DiffStatus.Removed => (Color.FromRgb(0xFF, 0x99, 0x99), ToolsTheme.TextSub),
            DiffStatus.Added => (ToolsTheme.TextSub, Color.FromRgb(0x86, 0xEF, 0xAC)),
            _ => (ToolsTheme.TextSub, ToolsTheme.TextSub),
        };

        var lineNumColor = Color.FromRgb(0x37, 0x41, 0x51);

        UIElement MakeRow(string? lineNum, string text, Color bg, Color fg) => new Grid
        {
            Background = new SolidColorBrush(bg),
            Children =
            {
                NewGrid(lineNum, text, fg, lineNumColor),
            },
        };

        Grid NewGrid(string? num, string text, Color fg, Color numFg)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var numTb = new TextBlock
            {
                Text = num ?? "",
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(numFg),
                Padding = new Thickness(8, 3, 8, 3),
                TextAlignment = TextAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0E, 0x14)),
            };
            Grid.SetColumn(numTb, 0);
            g.Children.Add(numTb);

            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(fg),
                Padding = new Thickness(12, 3, 12, 3),
            };
            Grid.SetColumn(tb, 1);
            g.Children.Add(tb);

            return g;
        }

        var leftText = line.Status == DiffStatus.Added ? "" : line.Text;
        var rightText = line.Status == DiffStatus.Removed ? "" : line.Text;
        var leftNum = line.LineNumberLeft?.ToString() ?? "";
        var rightNum = line.LineNumberRight?.ToString() ?? "";

        return (
            MakeRow(leftNum, leftText, leftBg, line.Status == DiffStatus.Removed ? leftFg : ToolsTheme.Text),
            MakeRow(rightNum, rightText, rightBg, line.Status == DiffStatus.Added ? rightFg : ToolsTheme.Text)
        );
    }

    // ── Fetch content ─────────────────────────────────────────────────────────

    private async Task<string> FetchContentAsync(string resourceName, string envUrl)
    {
        var baseUrl = envUrl.TrimEnd('/');

        // Ensure it has the /api/data path
        if (!baseUrl.Contains("/api/data"))
            baseUrl += "/api/data/v9.2";

        var filter = Uri.EscapeDataString($"name eq '{resourceName}'");
        var url = $"{baseUrl}/webresourceset?$select=content,name&$filter={filter}";

        var headers = await _auth.GetHeadersAsync(envUrl);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        foreach (var (k, v) in headers)
            req.Headers.TryAddWithoutValidation(k, v);

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("value", out var arr) && arr.GetArrayLength() > 0)
        {
            var first = arr[0];
            if (first.TryGetProperty("content", out var contentEl))
            {
                var base64 = contentEl.GetString() ?? "";
                if (!string.IsNullOrEmpty(base64))
                    return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            }
        }

        return $"// Recurso '{resourceName}' não encontrado neste ambiente.";
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private static TextBlock StatLabel(string text, Color color) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = new SolidColorBrush(color),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 12, 0),
    };

    private static WpfBorder Separator() => new()
    {
        Width = 1,
        Background = ToolsTheme.Brush(ToolsTheme.Border),
        Margin = new Thickness(0, 2, 12, 2),
    };

    private static Button SmallButton(string label) => new()
    {
        Content = label,
        FontSize = 11,
        Background = ToolsTheme.Brush(ToolsTheme.Surface2),
        Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
        BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(12, 6, 12, 6),
    };

    private UIElement BuildVaultLockedPrompt()
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };

        stack.Children.Add(new TextBlock
        {
            Text = "🔒 Vault bloqueado.",
            FontSize = 12,
            Foreground = ToolsTheme.Brush(ToolsTheme.Yellow),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });

        var btnUnlock = new Button
        {
            Content = "🔓 Desbloquear Vault",
            FontSize = 11,
            Background = ToolsTheme.Brush(ToolsTheme.Accent),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(12, 6, 12, 6),
        };
        // ToggleLock dispara RequestUnlockDialog no VaultViewModel; quem escuta
        // esse evento (a VaultView) deve abrir o dialog de senha. Como esse
        // overlay não tem acesso direto ao dialog, repassamos o pedido.
        btnUnlock.Click += (_, _) => _vault.ToggleLockCommand.Execute(null);

        stack.Children.Add(btnUnlock);
        return stack;
    }

    private static UIElement NoClientsMessage() => new TextBlock
    {
        Text = "Nenhum cliente cadastrado no Vault.",
        FontSize = 12,
        Foreground = ToolsTheme.Brush(ToolsTheme.TextMuted),
    };

    private static void AddRow(Grid g, UIElement el, int row)
    {
        Grid.SetRow(el, row);
        g.Children.Add(el);
    }

    private static int CountLines(string s) => s.Split('\n').Length;

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return ToolsTheme.Accent; }
    }
}