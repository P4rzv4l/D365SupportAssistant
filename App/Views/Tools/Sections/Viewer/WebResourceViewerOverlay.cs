// =============================================================================
//  WebResourceViewerOverlay.cs — Visualizador de conteúdo em tela cheia
// =============================================================================

using D365Assistant.Core.Models.WebResource;
using D365Assistant.Core.Services;
using D365Assistant.Views.Tools.Theme;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Tools.Sections.Viewer;

public sealed class WebResourceViewerOverlay
{
    private readonly HttpClient _http;
    private readonly IExternalAuthService _auth;

    private readonly WpfBorder _overlay;
    private TextBlock? _titleTb;
    private TextBlock? _statusTb;
    private Grid? _contentArea;

    public WpfBorder Root => _overlay;

    public WebResourceViewerOverlay(HttpClient http, IExternalAuthService auth)
    {
        _http = http;
        _auth = auth;
        _overlay = Build();
    }

    // ── Show ──────────────────────────────────────────────────────────────────

    public async Task ShowAsync(WebResource resource, string environmentUrl)
    {
        _overlay.Visibility = Visibility.Visible;

        if (_titleTb != null) _titleTb.Text = resource.Name;
        if (_statusTb != null) _statusTb.Text = "⏳ Carregando conteúdo...";
        if (_contentArea != null) _contentArea.Children.Clear();

        try
        {
            var content = await FetchContentAsync(resource, environmentUrl);
            var lang = SyntaxHighlighter.DetectLanguage(resource.TypeCode);
            var rtb = SyntaxHighlighter.Build(content, lang);

            if (_contentArea != null)
            {
                _contentArea.Children.Clear();
                _contentArea.Children.Add(rtb);
            }

            if (_statusTb != null)
                _statusTb.Text = $"{resource.TypeLabel}  •  {CountLines(content)} linhas  •  {FormatSize(content)}";
        }
        catch (Exception ex)
        {
            if (_statusTb != null)
                _statusTb.Text = $"⚠ Erro ao carregar: {ex.Message}";
        }
    }

    public void Hide() => _overlay.Visibility = Visibility.Collapsed;

    // ── Build UI ──────────────────────────────────────────────────────────────

    private WpfBorder Build()
    {
        var overlay = new WpfBorder
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x0D, 0x11, 0x17)),
            Visibility = Visibility.Collapsed,
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        overlay.Child = root;

        // ── Header ────────────────────────────────────────────────────────────
        var header = new WpfBorder
        {
            Background = ToolsTheme.Brush(ToolsTheme.Surface),
            BorderBrush = ToolsTheme.Brush(ToolsTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 12, 20, 12),
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var hg = new Grid();
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Child = hg;

        var left = new StackPanel();
        Grid.SetColumn(left, 0);
        hg.Children.Add(left);

        _titleTb = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = ToolsTheme.Brush(ToolsTheme.Text),
            FontFamily = new FontFamily("Consolas"),
        };
        left.Children.Add(_titleTb);

        _statusTb = new TextBlock
        {
            FontSize = 11,
            Foreground = ToolsTheme.Brush(ToolsTheme.TextSub),
            Margin = new Thickness(0, 4, 0, 0),
        };
        left.Children.Add(_statusTb);

        // Right: copy + close buttons
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(right, 1);
        hg.Children.Add(right);

        var btnCopy = HeaderButton("📋 Copiar tudo");
        btnCopy.Click += async (_, _) =>
        {
            if (_contentArea?.Children.Count > 0
                && _contentArea.Children[0] is RichTextBox rtb)
            {
                var text = new TextRange(
                    rtb.Document.ContentStart,
                    rtb.Document.ContentEnd).Text;
                System.Windows.Clipboard.SetText(text);
            }
        };
        right.Children.Add(btnCopy);

        var btnClose = HeaderButton("✕ Fechar");
        btnClose.Margin = new Thickness(8, 0, 0, 0);
        btnClose.Click += (_, _) => Hide();
        right.Children.Add(btnClose);

        // ── Content area ──────────────────────────────────────────────────────
        _contentArea = new Grid();
        Grid.SetRow(_contentArea, 1);
        root.Children.Add(_contentArea);

        return overlay;
    }

    private static Button HeaderButton(string label) => new()
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

    // ── Fetch content ─────────────────────────────────────────────────────────

    private async Task<string> FetchContentAsync(WebResource resource, string environmentUrl)
    {
        var baseUrl = environmentUrl.TrimEnd('/') + "/api/data/v9.2/";
        var url = $"{baseUrl}webresourceset({resource.WebResourceId})?$select=content,name";

        var headers = await _auth.GetHeadersAsync(environmentUrl);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        foreach (var (k, v) in headers)
            req.Headers.TryAddWithoutValidation(k, v);

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("content", out var contentEl))
        {
            var base64 = contentEl.GetString() ?? "";
            if (!string.IsNullOrEmpty(base64))
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }

        return "// Conteúdo não disponível ou recurso binário.";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountLines(string content) =>
        content.Split('\n').Length;

    private static string FormatSize(string content)
    {
        var bytes = Encoding.UTF8.GetByteCount(content);
        return bytes < 1024 ? $"{bytes} B"
             : bytes < 1024 * 1024 ? $"{bytes / 1024.0:F1} KB"
             : $"{bytes / (1024.0 * 1024):F1} MB";
    }
}