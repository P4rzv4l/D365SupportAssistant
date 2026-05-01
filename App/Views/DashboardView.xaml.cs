// =============================================================================
//  DashboardView.xaml.cs — Code-behind do Dashboard (redesenhado)
// =============================================================================

using D365Assistant.Core.Models;
using D365Assistant.Core.Models.Incident;
using D365Assistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

public partial class DashboardView : Page
{
    private readonly DashboardViewModel _vm;
    private readonly TrackerViewModel _trackerVm;
    private readonly MainWindow _mainWindow;

    private readonly Dictionary<string, Button> _priBtns = [];
    private readonly Dictionary<string, Button> _statusBtns = [];

    // Cores base do tema
    private static readonly Color BgRow = Color.FromRgb(0x0F, 0x15, 0x20);
    private static readonly Color BgRowHover = Color.FromRgb(0x14, 0x1B, 0x27);
    private static readonly Color BorderRow = Color.FromRgb(0x12, 0x19, 0x2A);
    private static readonly Color BorderHov = Color.FromRgb(0x1E, 0x26, 0x33);

    public DashboardView(DashboardViewModel vm, TrackerViewModel trackerVm, MainWindow mainWindow)
    {
        InitializeComponent();
        _vm = vm;
        _trackerVm = trackerVm;
        _mainWindow = mainWindow;

        DataContext = _vm;
        BuildFilterButtons();
        _vm.Incidents.CollectionChanged += (_, _) => RenderIncidents();
        RenderKpis();
    }

    // ── KPIs ──────────────────────────────────────────────────────────────────

    private void RenderKpis()
    {
        KpiTotal.Text = _vm.TotalAtivo.ToString();
        KpiUrgente.Text = _vm.Urgentes.ToString();
        KpiHigh.Text = _vm.AltaPrioridade.ToString();
        KpiSla.Text = _vm.RiscoSla.ToString();
        KpiEsgotado.Text = _vm.HorasEsgotadas.ToString();
        KpiNew.Text = _vm.NovosHoje.ToString();
        LastUpdatedText.Text = _vm.LastUpdated;

        KpiUrgente.Foreground = _vm.Urgentes > 0 ? Brush("#FCA5A5") : Brush("#6B7280");
        KpiHigh.Foreground = _vm.AltaPrioridade > 0 ? Brush("#FCD34D") : Brush("#6B7280");
        KpiSla.Foreground = _vm.RiscoSla > 0 ? Brush("#FCD34D") : Brush("#6B7280");
        KpiEsgotado.Foreground = _vm.HorasEsgotadas > 0 ? Brush("#FCA5A5") : Brush("#6B7280");
    }

    // ── Filtros ───────────────────────────────────────────────────────────────

    private void BuildFilterButtons()
    {
        foreach (var lbl in _vm.PriorityOptions)
        {
            var btn = MakePill(lbl);
            btn.Click += (_, _) => { _vm.PriFilter = lbl; HighlightFilter(_priBtns, lbl); };
            _priBtns[lbl] = btn;
            PriFilterPanel.Children.Add(btn);
        }
        HighlightFilter(_priBtns, "Todos");

        foreach (var lbl in _vm.StatusOptions)
        {
            var btn = MakePill(lbl);
            btn.Click += (_, _) => { _vm.StatusFilter = lbl; HighlightFilter(_statusBtns, lbl); };
            _statusBtns[lbl] = btn;
            StatusFilterPanel.Children.Add(btn);
        }
        HighlightFilter(_statusBtns, "Todos");
    }

    private static Button MakePill(string label) => new()
    {
        Content = label,
        FontFamily = new FontFamily("Segoe UI Semibold"),
        FontSize = 11,
        Padding = new Thickness(10, 3, 10, 3),
        Margin = new Thickness(0, 0, 5, 0),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20)),
        Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x19, 0x2A)),
    };

    private static void HighlightFilter(Dictionary<string, Button> btns, string active)
    {
        foreach (var (lbl, btn) in btns)
        {
            if (lbl == active)
            {
                // roxo ativo
                btn.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA));
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xA7, 0x8B, 0xFA));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x19, 0x2A));
            }
        }
    }

    // ── Renderização ──────────────────────────────────────────────────────────

    private void RenderIncidents()
    {
        IncidentList.ItemsSource = null;
        IncidentList.Items.Clear();

        var count = _vm.Incidents.Count;
        IncidentCountText.Text = $"{count} chamado{(count != 1 ? "s" : "")}";

        RenderKpis();

        var stack = new StackPanel();
        foreach (var snap in _vm.Incidents)
            stack.Children.Add(BuildRow(snap));

        if (count == 0)
            stack.Children.Add(new TextBlock
            {
                Text = "Nenhum chamado encontrado.",
                FontSize = 13,
                Foreground = Brush("#1F2937"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 40)
            });

        IncidentList.Items.Add(stack);
    }

    // ── Card de chamado ───────────────────────────────────────────────────────

    private Border BuildRow(IncidentSnapshot snap)
    {
        // Card container
        var card = new Border
        {
            Background = new SolidColorBrush(BgRow),
            BorderBrush = new SolidColorBrush(BorderRow),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 6),
        };
        card.MouseEnter += (_, _) =>
        {
            card.Background = new SolidColorBrush(BgRowHover);
            card.BorderBrush = new SolidColorBrush(BorderHov);
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = new SolidColorBrush(BgRow);
            card.BorderBrush = new SolidColorBrush(BorderRow);
        };
        card.ContextMenu = BuildContextMenu(snap);

        // Layout interno: barra pri | corpo | ações
        var grid = new Grid { ClipToBounds = true };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        card.Child = grid;

        // Barra de prioridade (lado esquerdo, bordas arredondadas à esquerda)
        var priBar = new Border
        {
            Background = PriorityBarColor(snap.PriorityCode),
            CornerRadius = new CornerRadius(9, 0, 0, 9),
        };
        Grid.SetColumn(priBar, 0);
        grid.Children.Add(priBar);

        // ── Corpo ──────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(13, 9, 10, 9) };
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);

        // Linha 1: ticket + badges + meta direita
        var row1 = new Grid();
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.Children.Add(row1);

        // Esquerda: ticket + badges
        var left = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(left, 0);
        row1.Children.Add(left);

        // Número do chamado (clicável se tiver URL)
        var ticketLbl = new TextBlock
        {
            Text = snap.TicketNumber,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            Foreground = !string.IsNullOrEmpty(snap.BzpUrl)
                            ? Brush("#60A5FA")
                            : Brush("#6B7280"),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = !string.IsNullOrEmpty(snap.BzpUrl) ? Cursors.Hand : Cursors.Arrow,
        };
        if (!string.IsNullOrEmpty(snap.BzpUrl))
        {
            var url = snap.BzpUrl;
            ticketLbl.MouseLeftButtonUp += (_, _) =>
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        left.Children.Add(ticketLbl);

        // Badge prioridade
        var (priLabel, priFg, priBg) = snap.PriorityCode switch
        {
            419500000 => ("Urgente", "#FCA5A5", "#3B0C0C"),
            1 => ("Alto", "#FCD34D", "#3B2A00"),
            2 => ("Normal", "#D1A827", "#1E1A00"),
            3 => ("Baixa", "#86EFAC", "#0A2010"),
            _ => ("—", "#6B7280", "#0F1520"),
        };
        left.Children.Add(MakeBadge(priLabel, priFg, priBg));

        // Badge status
        var (stLabel, stFg, stBg) = StatusInfo(snap.StatusCode);
        left.Children.Add(MakeBadge(stLabel, stFg, stBg));

        // Tipo de caso
        var caseType = snap.CaseTypeCode switch
        {
            1 => "Dúvida",
            275500001 => "Garantia",
            3 => "Melhoria",
            2 => "Problema",
            275500000 => "Projeto",
            100000000 => "Solicitação",
            4 => "Sol. Indispon.",
            419500000 => "Sugestão Bot",
            _ => ""
        };
        if (!string.IsNullOrEmpty(caseType))
            left.Children.Add(new TextBlock
            {
                Text = $"· {caseType}",
                FontSize = 10.5,
                Foreground = Brush("#1F2937"),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });

        // Direita: chips de idle / timer / esgotado
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(right, 1);
        row1.Children.Add(right);

        // Horas esgotadas
        if (snap.BzHorasEsgotadas)
            right.Children.Add(new TextBlock
            {
                Text = "⏰ esgotado",
                FontSize = 10.5,
                Foreground = Brush("#F59E0B"),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });

        // Timer rastreado
        try
        {
            var secs = App.Services.GetRequiredService<Core.Services.StorageService>()
                          .GetTrackedSecondsForTicket(snap.TicketNumber);
            if (secs > 0)
            {
                var t = TimeSpan.FromSeconds(secs);
                var fmt = t.Hours > 0 ? $"{t.Hours}h {t.Minutes:D2}m" : $"{t.Minutes}m {t.Seconds:D2}s";
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = $"⏱ {fmt}",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10.5,
                        Foreground = Brush("#A78BFA"),
                    }
                };
                right.Children.Add(chip);
            }
        }
        catch { }

        // Tempo parado
        var idleH = snap.HoursSinceModified;
        var idleTxt = idleH < 1 ? $"{(int)(idleH * 60)}m parado"
                    : idleH < 24 ? $"{idleH:F1}h parado"
                                   : $"{idleH / 24:F1}d parado";
        var idleFg = idleH > 48 ? "#EF4444"
                    : idleH > 8 ? "#F59E0B"
                                   : "#1F2937";
        right.Children.Add(new TextBlock
        {
            Text = idleTxt,
            FontSize = 10.5,
            Foreground = Brush(idleFg),
            VerticalAlignment = VerticalAlignment.Center,
        });

        // Linha 2: título + cliente
        var titleText = snap.Title.Length > 80 ? snap.Title[..80] + "…" : snap.Title;
        var cliente = snap.CustomerDisplayName;
        var row2 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        row2.Children.Add(new TextBlock
        {
            Text = titleText,
            FontSize = 12.5,
            Foreground = Brush("#E2E8F0"),
        });
        if (!string.IsNullOrEmpty(cliente))
            row2.Children.Add(new TextBlock
            {
                Text = $"  ·  {(cliente.Length > 35 ? cliente[..35] + "…" : cliente)}",
                FontSize = 11,
                Foreground = Brush("#4B5563"),
                VerticalAlignment = VerticalAlignment.Center,
            });
        body.Children.Add(row2);

        // ── Botões de ação ────────────────────────────────────────────────
        var actPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        Grid.SetColumn(actPanel, 2);
        grid.Children.Add(actPanel);

        var btnTimer = MakeActionBtn("▶  Timer",
            fg: "#86EFAC", bg: Color.FromArgb(0x1E, 0x22, 0xC5, 0x5E),
            border: Color.FromArgb(0x44, 0x22, 0xC5, 0x5E));
        btnTimer.Click += (_, _) => _mainWindow.QuickStartTimer(snap.TicketNumber, snap.Title);

        var btnAI = MakeActionBtn("✦  IA",
            fg: "#A78BFA", bg: Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA),
            border: Color.FromArgb(0x44, 0xA7, 0x8B, 0xFA));
        btnAI.Margin = new Thickness(0, 5, 0, 0);
        btnAI.Click += (_, _) => _mainWindow.OpenAIForTicket(snap.TicketNumber);

        actPanel.Children.Add(btnTimer);
        actPanel.Children.Add(btnAI);

        return card;
    }

    // ── Helpers visuais ───────────────────────────────────────────────────────

    private static Button MakeActionBtn(string label, string fg, Color bg, Color border) => new()
    {
        Content = label,
        FontFamily = new FontFamily("Segoe UI Semibold"),
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Background = new SolidColorBrush(bg),
        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg)),
        BorderBrush = new SolidColorBrush(border),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Padding = new Thickness(10, 4, 10, 4),
    };

    private static Border MakeBadge(string text, string fg, string bg)
    {
        var fgColor = (Color)ColorConverter.ConvertFromString(fg);
        var bgColor = (Color)ColorConverter.ConvertFromString(bg);
        return new Border
        {
            Background = new SolidColorBrush(bgColor),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fgColor),
            }
        };
    }

    private static SolidColorBrush PriorityBarColor(int? code) => code switch
    {
        419500000 or 1 => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
        2 => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        3 => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        _ => new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)),
    };

    private static (string label, string fg, string bg) StatusInfo(int code) => code switch
    {
        100000000 => ("Novo", "#93C5FD", "#0C1F3A"),
        4 => ("Aguardando Fila", "#6B7280", "#0F1520"),
        1 => ("Em Atendimento", "#86EFAC", "#0A2010"),
        419500000 => ("Aguard. Cliente", "#FCD34D", "#3B2A00"),
        3 => ("Em Aprovação", "#A78BFA", "#1E1245"),
        2 => ("Impeditivo", "#FCA5A5", "#3B0C0C"),
        5 => ("Resolvido", "#86EFAC", "#0A2010"),
        1000 => ("Info Fornecida", "#6B7280", "#0F1520"),
        6 => ("Cancelado", "#374151", "#0D1117"),
        419500001 => ("Despriorizado", "#374151", "#0F1520"),
        121360001 => ("Aguard. Microsoft", "#FCD34D", "#3B2A00"),
        _ => ($"St.{code}", "#374151", "#0F1520"),
    };

    private static SolidColorBrush Brush(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    private ContextMenu BuildContextMenu(IncidentSnapshot snap)
    {
        var menu = new ContextMenu();

        var itemTimer = new MenuItem { Header = $"▶ Timer — {snap.TicketNumber}" };
        itemTimer.Click += (_, _) => _mainWindow.QuickStartTimer(snap.TicketNumber, snap.Title);
        menu.Items.Add(itemTimer);

        var itemAI = new MenuItem { Header = "✦ Analisar com IA" };
        itemAI.Click += (_, _) => _mainWindow.OpenAIForTicket(snap.TicketNumber);
        menu.Items.Add(itemAI);

        menu.Items.Add(new Separator());

        var itemCopy = new MenuItem { Header = "📋 Copiar número" };
        itemCopy.Click += (_, _) => System.Windows.Clipboard.SetText(snap.TicketNumber);
        menu.Items.Add(itemCopy);

        if (!string.IsNullOrEmpty(snap.BzpUrl))
        {
            var itemCRM = new MenuItem { Header = "🔗 Abrir no Dynamics" };
            var url = snap.BzpUrl;
            itemCRM.Click += (_, _) =>
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            menu.Items.Add(itemCRM);
        }

        if (!string.IsNullOrEmpty(snap.BzMotivoStatus))
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem
            {
                Header = $"📝 {snap.BzMotivoStatus[..Math.Min(snap.BzMotivoStatus.Length, 60)]}",
                IsEnabled = false,
            });
        }

        return menu;
    }

    // ── Eventos da UI ─────────────────────────────────────────────────────────

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        => App.Services.GetRequiredService<MainViewModel>().RefreshCommand.Execute(null);

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => _vm.SearchText = ((TextBox)sender).Text;

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }
}