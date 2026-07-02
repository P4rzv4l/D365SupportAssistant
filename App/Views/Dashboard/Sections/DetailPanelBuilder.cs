// =============================================================================
//  DetailPanelBuilder.cs — Painel lateral de detalhes de um chamado
// =============================================================================
// Responsabilidade única: dado um IncidentSnapshot, preenche o StackPanel
// de detalhes. Não conhece tabela, paginação nem estado global.
// =============================================================================

using D365Assistant.Core.Models.Incident;
using D365Assistant.Core.Services;
using D365Assistant.Views.Dashboard.Components;
using D365Assistant.Views.Dashboard.Helpers;
using D365Assistant.Views.Dashboard.Theme;
using FontAwesome.Sharp;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;

namespace D365Assistant.Views.Dashboard.Sections;

public sealed class DetailPanelBuilder
{
    private readonly Action<string, string> _onStartTimer;  // ticketNumber, title
    private readonly Action<string> _onClose;       // clears selection
    private readonly StorageService _storage;

    public DetailPanelBuilder(
        Action<string, string> onStartTimer,
        Action<string> onClose,
        StorageService storage)
    {
        _onStartTimer = onStartTimer;
        _onClose = onClose;
        _storage = storage;
    }

    /// <summary>
    /// Popula <paramref name="container"/> com as seções do chamado selecionado.
    /// Chame <c>container.Children.Clear()</c> antes de invocar.
    /// </summary>
    public void Populate(StackPanel container, IncidentSnapshot snap)
    {
        container.Children.Add(BuildHeader(snap));
        container.Children.Add(BuildInfoGrid(snap));
        container.Children.Add(BuildTimeline(snap));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HEADER
    // ══════════════════════════════════════════════════════════════════════════

    private UIElement BuildHeader(IncidentSnapshot snap)
    {
        var section = SectionBorder(DashboardTheme.Surface2, bottomBorder: true);
        var stack = new StackPanel();
        section.Child = stack;

        stack.Children.Add(BuildHeaderTitleRow(snap));
        stack.Children.Add(BuildSubjectText(snap));
        stack.Children.Add(BuildActionRow(snap));

        var trackedSeconds = TryGetTrackedSeconds(snap.TicketNumber);
        if (trackedSeconds > 0)
            stack.Children.Add(BuildTimerDisplay(trackedSeconds));

        return section;
    }

    private UIElement BuildHeaderTitleRow(IncidentSnapshot snap)
    {
        var g = TwoColumnGrid(margin: new Thickness(0, 0, 0, 8));

        // Left: star + ticket number + priority + status
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(left, 0);
        g.Children.Add(left);

        left.Children.Add(TicketNumberText(snap.TicketNumber));

        var priInfo = IncidentDisplayMappers.Priority(snap.PriorityCode);
        left.Children.Add(UiFactory.Badge(priInfo.Icon ,priInfo.Label, priInfo.FgHex, priInfo.BgHex));

        var stInfo = IncidentDisplayMappers.Status(snap.StatusCode);
        left.Children.Add(UiFactory.Badge(priInfo.Icon, stInfo.Label, stInfo.FgHex, stInfo.BgHex,
                                          margin: new Thickness(6, 0, 0, 0)));

        // Right: close button
        var btnClose = BuildCloseButton(snap.TicketNumber);
        Grid.SetColumn(btnClose, 1);
        g.Children.Add(btnClose);

        return g;
    }

    private Button BuildCloseButton(string ticketNumber)
    {
        var btn = new Button
        {
            Content = "✕",
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6, 4, 6, 4),
        };
        btn.MouseEnter += (_, _) => btn.Foreground = DashboardTheme.Brush(DashboardTheme.Text);
        btn.MouseLeave += (_, _) => btn.Foreground = DashboardTheme.Brush(DashboardTheme.TextSub);
        btn.Click += (_, _) => _onClose(ticketNumber);
        return btn;
    }

    private static UIElement BuildSubjectText(IncidentSnapshot snap) => new TextBlock
    {
        Text = snap.Title,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 10),
    };

    private UIElement BuildActionRow(IncidentSnapshot snap)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        var btnTimer = UiFactory.ActionButton(IconChar.Play, "Iniciar Tempo", DashboardTheme.Green);
        btnTimer.Click += (_, _) => _onStartTimer(snap.TicketNumber, snap.Title);
        row.Children.Add(btnTimer);

        var btnPause = UiFactory.ActionButton(IconChar.Pause, "Pausar", DashboardTheme.TextSub);
        btnPause.Margin = new Thickness(6, 0, 0, 0);
        row.Children.Add(btnPause);

        var btnFinish = UiFactory.ActionButton(IconChar.Stop, "Finalizar", DashboardTheme.Red);
        btnFinish.Margin = new Thickness(6, 0, 0, 0);
        row.Children.Add(btnFinish);

        return row;
    }

    private static UIElement BuildTimerDisplay(long seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return new TextBlock
        {
            Text = $"Tempo Total  {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}",
            FontSize = 11,
            FontFamily = new FontFamily("Consolas"),
            Foreground = DashboardTheme.Brush(DashboardTheme.Purple),
            Margin = new Thickness(0, 8, 0, 0),
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  INFO GRID
    // ══════════════════════════════════════════════════════════════════════════

    private static UIElement BuildInfoGrid(IncidentSnapshot snap)
    {
        var section = SectionBorder(DashboardTheme.Surface, bottomBorder: true);

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        section.Child = g;

        var left = new StackPanel();
        var right = new StackPanel();
        Grid.SetColumn(left, 0); g.Children.Add(left);
        Grid.SetColumn(right, 1); g.Children.Add(right);

        // Left column
        InfoItem(left, "Cliente", OrDash(snap.CustomerDisplayName));
        InfoItem(left, "Aberto em", snap.CreatedOn.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));

        var sla = IncidentDisplayMappers.SlaDetail(snap.BzStatusKpiFirst, snap.BzFirstResponseDate);
        InfoItem(left, "SLA 1º Atendimento", sla.Text, sla.Color);

        if (!string.IsNullOrEmpty(snap.Description))
            InfoItem(left, "Descrição", snap.Description, DashboardTheme.TextSub);

        // Right column
        InfoItem(right, "Última Atualização",
                        snap.ModifiedOn.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));

        var sat = IncidentDisplayMappers.SatisfactionDetail(snap.CustomerSatisfactionCode);
        InfoItem(right, "Satisfação do Cliente", sat.Text, sat.Color);

        if (!string.IsNullOrEmpty(snap.BzMotivoStatus))
            InfoItem(right, "Observações", snap.BzMotivoStatus, DashboardTheme.TextSub);

        return section;
    }

    private static void InfoItem(StackPanel parent, string label, string value,
                                 Color? valueColor = null)
    {
        parent.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            Margin = new Thickness(0, 0, 0, 2),
        });
        parent.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(valueColor ?? DashboardTheme.Text),
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TIMELINE
    // ══════════════════════════════════════════════════════════════════════════

    private static UIElement BuildTimeline(IncidentSnapshot snap)
    {
        var section = SectionBorder(DashboardTheme.Surface, bottomBorder: false,
                                    padding: new Thickness(16, 12, 16, 16));
        var stack = new StackPanel();
        section.Child = stack;

        stack.Children.Add(BuildTimelineHeader());
        stack.Children.Add(BuildCommentBox());

        foreach (var evt in BuildTimelineEvents(snap).OrderByDescending(e => e.dt))
            stack.Children.Add(TimelineEventRow(evt.actor, evt.type, evt.msg, evt.dt, evt.color));

        return section;
    }

    private static UIElement BuildTimelineHeader()
    {
        var g = TwoColumnGrid(margin: new Thickness(0, 0, 0, 12));

        g.Children.Add(new TextBlock
        {
            Text = "Linha do Tempo",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
        });

        var btnFilter = UiFactory.GhostButton("Filtrar");
        Grid.SetColumn(btnFilter, 1);
        g.Children.Add(btnFilter);

        return g;
    }

    private static UIElement BuildCommentBox()
    {
        var border = new Border
        {
            Background = DashboardTheme.Brush(DashboardTheme.Surface2),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 12),
        };

        var g = TwoColumnGrid();
        border.Child = g;

        g.Children.Add(new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            FontSize = 11,
            Text = "Adicionar comentário...",
        });

        var btnSend = new Button
        {
            Content = "▶",
            FontSize = 11,
            Background = DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x22),
            Foreground = DashboardTheme.Brush(DashboardTheme.Purple),
            BorderBrush = DashboardTheme.AlphaBrush(DashboardTheme.Purple, 0x44),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(8, 4, 8, 4),
        };
        Grid.SetColumn(btnSend, 1);
        g.Children.Add(btnSend);

        return border;
    }

    private static IEnumerable<(DateTime dt, string actor, string type, string msg, Color color)>
        BuildTimelineEvents(IncidentSnapshot snap)
    {
        yield return (snap.CreatedOn.ToLocalTime(), "Sistema",
                      "Criação", "Chamado criado", DashboardTheme.TextSub);

        yield return (snap.ModifiedOn.ToLocalTime(), snap.OwnerName ?? "Sistema",
                      "Comentário", "Última atualização", DashboardTheme.Accent);

        if (!snap.FirstResponseSent)
            yield return (snap.CreatedOn.ToLocalTime().AddMinutes(5), "Sistema",
                          "Status", "Status alterado para Em Atendimento", DashboardTheme.Yellow);
    }

    private static UIElement TimelineEventRow(
        string actor, string type, string msg, DateTime dt, Color color)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(BuildAvatar(actor, color), 0); row.Children.Add(BuildAvatar(actor, color));

        var content = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetColumn(content, 1); row.Children.Add(content);

        var topLine = TwoColumnGrid();
        topLine.Children.Add(new TextBlock
        {
            Text = actor,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var dateTb = new TextBlock
        {
            Text = dt.ToString("dd/MM/yyyy HH:mm"),
            FontSize = 10,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(dateTb, 1);
        topLine.Children.Add(dateTb);
        content.Children.Add(topLine);

        content.Children.Add(new TextBlock
        {
            Text = msg,
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
        });

        return row;
    }

    private static Border BuildAvatar(string actor, Color color)
    {
        var initials = string.Concat(
            actor.Split(' ').Take(2).Select(p => p.Length > 0 ? p[0].ToString() : ""));

        return new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = DashboardTheme.AlphaBrush(color, 0x25),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            Child = new TextBlock
            {
                Text = initials.ToUpper(),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = DashboardTheme.Brush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  UTILITIES
    // ══════════════════════════════════════════════════════════════════════════

    private long TryGetTrackedSeconds(string ticketNumber)
    {
        try { return _storage.GetTrackedSecondsForTicket(ticketNumber); }
        catch { return 0; }
    }

    private static Border SectionBorder(Color bg, bool bottomBorder, Thickness? padding = null) => new()
    {
        Background = DashboardTheme.Brush(bg),
        BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
        BorderThickness = bottomBorder ? new Thickness(0, 0, 0, 1) : new Thickness(0),
        Padding = padding ?? new Thickness(16, 12, 16, 12),
    };

    private static Grid TwoColumnGrid(Thickness? margin = null)
    {
        var g = new Grid();
        if (margin.HasValue) g.Margin = margin.Value;
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        return g;
    }

    private static TextBlock TicketNumberText(string number) => new()
    {
        Text = number,
        FontSize = 13,
        FontWeight = FontWeights.Bold,
        FontFamily = new FontFamily("Consolas"),
        Foreground = DashboardTheme.Brush(DashboardTheme.Accent),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
    };

    private static string OrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;
}