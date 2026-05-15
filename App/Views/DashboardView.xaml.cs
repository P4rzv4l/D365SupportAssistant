// =============================================================================
//  DashboardView.xaml.cs — Dashboard redesenhado
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

    // Palette
    private static readonly Color CBg = Color.FromRgb(0x08, 0x0C, 0x12);
    private static readonly Color CSurface = Color.FromRgb(0x0F, 0x15, 0x20);
    private static readonly Color CBorder = Color.FromRgb(0x1E, 0x26, 0x33);
    private static readonly Color CText = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private static readonly Color CMuted = Color.FromRgb(0x4B, 0x55, 0x63);

    public DashboardView(DashboardViewModel vm, TrackerViewModel trackerVm, MainWindow mainWindow)
    {
        InitializeComponent();
        _vm = vm;
        _trackerVm = trackerVm;
        _mainWindow = mainWindow;
        DataContext = _vm;

        BuildFilterButtons();
        _vm.Incidents.CollectionChanged += (_, _) => RenderIncidents();
        _vm.PropertyChanged += (_, _) => RenderKpis();
        RenderKpis();
    }

    // KPIs
    private void RenderKpis()
    {
        KpiTotal.Text = _vm.TotalAtivo.ToString();
        KpiUrgente.Text = _vm.Urgentes.ToString();
        KpiHigh.Text = _vm.AltaPrioridade.ToString();
        KpiSla.Text = _vm.RiscoSla.ToString();
        KpiEsgotado.Text = _vm.HorasEsgotadas.ToString();
        KpiNew.Text = _vm.NovosHoje.ToString();
        KpiSemCom.Text = _vm.SemPrimeiraCom.ToString();
        LastUpdatedText.Text = _vm.LastUpdated;

        KpiUrgente.Foreground = _vm.Urgentes > 0 ? B("#FCA5A5") : B("#374151");
        KpiHigh.Foreground = _vm.AltaPrioridade > 0 ? B("#FCD34D") : B("#374151");
        KpiSla.Foreground = _vm.RiscoSla > 0 ? B("#FCD34D") : B("#374151");
        KpiEsgotado.Foreground = _vm.HorasEsgotadas > 0 ? B("#FCA5A5") : B("#374151");
        KpiSemCom.Foreground = _vm.SemPrimeiraCom > 0 ? B("#FB923C") : B("#374151");
    }

    // Filtros
    private void BuildFilterButtons()
    {
        foreach (var lbl in _vm.PriorityOptions)
        {
            var btn = Pill(lbl);
            btn.Click += (_, _) => { _vm.PriFilter = lbl; Activate(_priBtns, lbl); };
            _priBtns[lbl] = btn;
            PriFilterPanel.Children.Add(btn);
        }
        Activate(_priBtns, "Todos");

        foreach (var lbl in _vm.StatusOptions)
        {
            var btn = Pill(lbl);
            btn.Click += (_, _) => { _vm.StatusFilter = lbl; Activate(_statusBtns, lbl); };
            _statusBtns[lbl] = btn;
            StatusFilterPanel.Children.Add(btn);
        }
        Activate(_statusBtns, "Todos");
    }

    private static Button Pill(string label) => new()
    {
        Content = label,
        FontFamily = new FontFamily("Segoe UI Semibold"),
        FontSize = 11,
        Padding = new Thickness(11, 4, 11, 4),
        Margin = new Thickness(0, 0, 5, 0),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
        Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20)),
        Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x33)),
    };

    private static void Activate(Dictionary<string, Button> btns, string active)
    {
        foreach (var (lbl, btn) in btns)
        {
            if (lbl == active)
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA));
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xA7, 0x8B, 0xFA));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x15, 0x20));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x33));
            }
        }
    }

    // Lista
    private void RenderIncidents()
    {
        IncidentList.Items.Clear();
        var count = _vm.Incidents.Count;
        IncidentCountText.Text = $"{count} chamado{(count != 1 ? "s" : "")}";
        RenderKpis();

        var panel = new StackPanel();

        if (count == 0)
            panel.Children.Add(new TextBlock
            {
                Text = "Nenhum chamado encontrado.",
                FontSize = 13,
                Foreground = new SolidColorBrush(CMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0),
            });
        else
            foreach (var snap in _vm.Incidents)
                panel.Children.Add(BuildCard(snap));

        IncidentList.Items.Add(panel);
    }

    // Card
    private Border BuildCard(IncidentSnapshot snap)
    {
        var (priLabel, priFg, priBg) = PriorityInfo(snap.PriorityCode);
        var (stLabel, stFg, stBg) = StatusInfo(snap.StatusCode);

        var card = new Border
        {
            Background = new SolidColorBrush(CSurface),
            BorderBrush = new SolidColorBrush(CBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 7),
            ClipToBounds = true,
        };
        card.MouseEnter += (_, _) =>
        {
            card.Background = new SolidColorBrush(Color.FromRgb(0x13, 0x1B, 0x27));
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x38, 0x48));
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = new SolidColorBrush(CSurface);
            card.BorderBrush = new SolidColorBrush(CBorder);
        };
        card.ContextMenu = BuildContextMenu(snap);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        card.Child = root;

        // Priority bar
        var priBarEl = new Border
        {
            Background = PriorityBarBrush(snap.PriorityCode),
            CornerRadius = new CornerRadius(9, 0, 0, 9),
        };
        Grid.SetColumn(priBarEl, 0);
        root.Children.Add(priBarEl);

        // Body
        var body = new StackPanel { Margin = new Thickness(14, 10, 10, 10) };
        Grid.SetColumn(body, 1);
        root.Children.Add(body);

        // Row 1: badges + chips
        var row1 = new Grid();
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.Children.Add(row1);

        var badgeRow = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(badgeRow, 0);
        row1.Children.Add(badgeRow);

        // Ticket number
        var ticketTb = new TextBlock
        {
            Text = snap.TicketNumber,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = !string.IsNullOrEmpty(snap.BzpUrl) ? B("#60A5FA") : B("#4B5563"),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = !string.IsNullOrEmpty(snap.BzpUrl) ? Cursors.Hand : Cursors.Arrow,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (!string.IsNullOrEmpty(snap.BzpUrl))
        {
            var url = snap.BzpUrl;
            ticketTb.MouseLeftButtonUp += (_, _) =>
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        badgeRow.Children.Add(ticketTb);
        badgeRow.Children.Add(Badge(priLabel, priFg, priBg));
        badgeRow.Children.Add(Badge(stLabel, stFg, stBg));

        // 1a comunicação flag
        if (!snap.FirstResponseSent)
            badgeRow.Children.Add(Badge("⚡ 1ª Com.", "#F97316", "#2A1200"));
        else
            badgeRow.Children.Add(Badge("✓ Respondido", "#4ADE80", "#0A2010"));

        // Case type
        var ct = snap.CaseTypeCode switch
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
        if (!string.IsNullOrEmpty(ct))
            badgeRow.Children.Add(new TextBlock
            {
                Text = $"· {ct}",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(CMuted),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });

        // Right chips
        var chips = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(chips, 1);
        row1.Children.Add(chips);

        if (snap.BzHorasEsgotadas)
            chips.Children.Add(new TextBlock
            {
                Text = "⏰ esgotado",
                FontSize = 10.5,
                Foreground = B("#F59E0B"),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });

        try
        {
            var secs = App.Services.GetRequiredService<Core.Services.StorageService>()
                          .GetTrackedSecondsForTicket(snap.TicketNumber);
            if (secs > 0)
            {
                var t = TimeSpan.FromSeconds(secs);
                var fmt = t.Hours > 0 ? $"{t.Hours}h {t.Minutes:D2}m" : $"{t.Minutes}m";
                chips.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xA7, 0x8B, 0xFA)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = $"⏱ {fmt}",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10.5,
                        Foreground = B("#A78BFA"),
                    }
                });
            }
        }
        catch { }

        var idleH = snap.HoursSinceModified;
        var idleTxt = idleH < 1 ? $"{(int)(idleH * 60)}m atrás"
                    : idleH < 24 ? $"{idleH:F1}h atrás"
                                 : $"{idleH / 24:F0}d atrás";
        var idleFg = idleH > 48 ? "#EF4444" : idleH > 8 ? "#F59E0B" : "#4B5563";
        chips.Children.Add(new TextBlock
        {
            Text = idleTxt,
            FontSize = 10.5,
            Foreground = B(idleFg),
            VerticalAlignment = VerticalAlignment.Center,
        });

        // Row 2: title + customer
        var row2 = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
        var titleTx = snap.Title.Length > 90 ? snap.Title[..90] + "…" : snap.Title;
        row2.Children.Add(new TextBlock { Text = titleTx, FontSize = 12.5, Foreground = new SolidColorBrush(CText) });
        var cl = snap.CustomerDisplayName;
        if (!string.IsNullOrEmpty(cl))
            row2.Children.Add(new TextBlock
            {
                Text = $"  ·  {(cl.Length > 35 ? cl[..35] + "…" : cl)}",
                FontSize = 11,
                Foreground = new SolidColorBrush(CMuted),
                VerticalAlignment = VerticalAlignment.Center,
            });
        body.Children.Add(row2);

        // Action buttons
        var actions = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        Grid.SetColumn(actions, 2);
        root.Children.Add(actions);

        var btnTimer = ActionBtn("▶ Timer", "#86EFAC",
            Color.FromArgb(0x1E, 0x22, 0xC5, 0x5E), Color.FromArgb(0x44, 0x22, 0xC5, 0x5E));
        btnTimer.Click += (_, _) => _mainWindow.QuickStartTimer(snap.TicketNumber, snap.Title);

        var btnAI = ActionBtn("✦ IA", "#A78BFA",
            Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA), Color.FromArgb(0x44, 0xA7, 0x8B, 0xFA));
        btnAI.Margin = new Thickness(0, 5, 0, 0);
        btnAI.Click += (_, _) => _mainWindow.OpenAIForTicket(snap.TicketNumber);

        actions.Children.Add(btnTimer);
        actions.Children.Add(btnAI);

        return card;
    }

    // Helpers
    private static Button ActionBtn(string label, string fgHex, Color bg, Color border)
    {
        var btn = new Button
        {
            Content = label,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            FontSize = 11,
            Background = new SolidColorBrush(bg),
            Foreground = B(fgHex),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(12, 5, 12, 5),
            MinWidth = 76,
        };
        btn.MouseEnter += (_, _) => btn.Background = new SolidColorBrush(
            Color.FromArgb((byte)Math.Min(bg.A + 0x28, 0xFF), bg.R, bg.G, bg.B));
        btn.MouseLeave += (_, _) => btn.Background = new SolidColorBrush(bg);
        return btn;
    }

    private static Border Badge(string text, string fgHex, string bgHex)
    {
        var fg = (Color)ColorConverter.ConvertFromString(fgHex);
        return new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, fg.R, fg.G, fg.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fg),
            }
        };
    }

    private static SolidColorBrush PriorityBarBrush(int? code) => code switch
    {
        419500000 => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
        1 => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        2 => new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
        3 => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        _ => new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)),
    };

    private static (string label, string fg, string bg) PriorityInfo(int? code) => code switch
    {
        419500000 => ("Urgente", "#FCA5A5", "#3B0C0C"),
        1 => ("Alto", "#FCD34D", "#3B2A00"),
        2 => ("Normal", "#93C5FD", "#0C1F3A"),
        3 => ("Baixa", "#86EFAC", "#0A2010"),
        _ => ("—", "#4B5563", "#0F1520"),
    };

    private static (string label, string fg, string bg) StatusInfo(int code) => code switch
    {
        100000000 => ("Novo", "#93C5FD", "#0C1F3A"),
        4 => ("Aguard. Fila", "#6B7280", "#0F1520"),
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

    private static SolidColorBrush B(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    private ContextMenu BuildContextMenu(IncidentSnapshot snap)
    {
        var menu = new ContextMenu();

        var mTimer = new MenuItem { Header = $"▶ Timer — {snap.TicketNumber}" };
        mTimer.Click += (_, _) => _mainWindow.QuickStartTimer(snap.TicketNumber, snap.Title);
        menu.Items.Add(mTimer);

        var mAI = new MenuItem { Header = "✦ Analisar com IA" };
        mAI.Click += (_, _) => _mainWindow.OpenAIForTicket(snap.TicketNumber);
        menu.Items.Add(mAI);

        menu.Items.Add(new Separator());

        var mCopy = new MenuItem { Header = "📋 Copiar número" };
        mCopy.Click += (_, _) => System.Windows.Clipboard.SetText(snap.TicketNumber);
        menu.Items.Add(mCopy);

        if (!string.IsNullOrEmpty(snap.BzpUrl))
        {
            var mCrm = new MenuItem { Header = "🔗 Abrir no Dynamics" };
            var url = snap.BzpUrl;
            mCrm.Click += (_, _) =>
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            menu.Items.Add(mCrm);
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