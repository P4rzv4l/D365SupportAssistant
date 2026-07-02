// =============================================================================
//  IncidentDisplayMappers.cs — Mapeamento de domínio → apresentação
// =============================================================================
// Regra: funções puras, sem side-effects, sem dependência de UI.
// Testáveis em xUnit sem abrir janela alguma.
// =============================================================================

using D365Assistant.Views.Dashboard.Theme;
using FontAwesome.Sharp;
using System.Windows.Media;

namespace D365Assistant.Views.Dashboard.Helpers;

/// <summary>Dados visuais para um badge (cor + label).</summary>
public readonly record struct BadgeInfo(string FgHex, string BgHex, string Label, IconChar Icon);

/// <summary>Dados visuais para SLA ou satisfação (label + cor WPF).</summary>
public readonly record struct ColoredLabel(string Text, Color Color, IconChar Icon);

/// <summary>
/// Converte códigos de domínio do Dynamics em informações de exibição.
/// Nenhum método aqui conhece WPF além de <see cref="Color"/>.
/// </summary>
public static class IncidentDisplayMappers
{
    // ── Priority ──────────────────────────────────────────────────────────────

    public static BadgeInfo Priority(int? code) => code switch
    {
        419500000 => new("#FCA5A5", "#3B0C0C", "Urgente", IconChar.Fire),
        1 => new("#FCD34D", "#3B2A00", "Alto", IconChar.TriangleExclamation),
        2 => new("#93C5FD", "#0C1F3A", "Normal", IconChar.Circle),
        3 => new("#86EFAC", "#0A2010", "Baixa", IconChar.ArrowAltCircleDown),
        _ => new("#64748B", "#0F1520", "—", IconChar.Question),
    };

    /// <summary>Ordem numérica para ordenação (menor = mais urgente).</summary>
    public static int PrioritySortOrder(int? code) => code switch
    {
        419500000 => 0,
        1 => 1,
        2 => 2,
        3 => 3,
        _ => 4,
    };

    /// <summary>Ordem numérica para ordenação (menor = mais urgente/ativo).</summary>
    public static int StatusSortOrder(int code) => code switch
    {
        2 => 0,   // Impeditivo
        1 => 1,   // Em Atendimento
        419500000 => 2,   // Aguard. Cliente
        121360001 => 3,   // Aguard. Microsoft
        3 => 4,   // Em Aprovação
        100000000 => 5,   // Novo
        4 => 6,   // Aguard. Fila
        419500001 => 7,   // Despriorizado
        5 => 8,   // Resolvido
        6 => 9,   // Cancelado
        _ => 10,
    };

// ── Status ────────────────────────────────────────────────────────────────

public static BadgeInfo Status(int code) => code switch
    {
        100000000 => new("#93C5FD", "#0C1F3A", "Novo", IconChar.CirclePlus),
        4 => new("#64748B", "#0F1520", "Aguard. Fila", IconChar.User),
        1 => new("#86EFAC", "#0A2010", "Em Atendimento", IconChar.Headset),
        419500000 => new("#FCD34D", "#3B2A00", "Aguard. Cliente", IconChar.UserLock),
        3 => new("#A78BFA", "#1E1245", "Em Aprovação", IconChar.UserCheck),
        2 => new("#FCA5A5", "#3B0C0C", "Impeditivo", IconChar.TriangleExclamation),
        5 => new("#86EFAC", "#0A2010", "Resolvido", IconChar.CircleCheck),
        6 => new("#374151", "#0D1117", "Cancelado", IconChar.CircleXmark),
        419500001 => new("#374151", "#0F1520", "Despriorizado", IconChar.Pause),
        121360001 => new("#FCD34D", "#3B2A00", "Aguard. Microsoft", IconChar.Microsoft),
        _ => new("#374151", "#0F1520", $"St.{code}", IconChar.Question),
    };

    // ── SLA ───────────────────────────────────────────────────────────────────

    /// <summary>Badge para coluna da tabela.</summary>
    public static (IconChar icon, string text, string fgHex, string bgHex, string tooltip) SlaTableBadge(
        int? bzStatusKpiFirst, bool bzFirstResponseDate)
    {
        if (bzStatusKpiFirst == 419500000 && bzFirstResponseDate)
            return (IconChar.Check ,"1º Atend.", "#86EFAC", "#0A2010",
                    "SLA de primeiro atendimento cumprido");
        if(!bzFirstResponseDate) return (
                IconChar.Clock, "Ag. 1ª Com.", "#FB923C", "#2D1500",
                "Aguardando envio da 1ª comunicação ao cliente");
        if (bzStatusKpiFirst == 419500002)
            return (IconChar.HourglassHalf, "Expirado", "#F87171", "#3B0A0A",
                "SLA de primeiro atendimento violado");

        return (IconChar.Clock, "Pendente", "#FCD34D", "#3B2A00",
                "SLA de primeiro atendimento pendente");
    }

    /// <summary>Label para painel de detalhes.</summary>
    public static ColoredLabel SlaDetail(int? bzStatusKpiFirst, bool bzFirstResponseDate)
    {
        if (bzStatusKpiFirst == 419500000 && bzFirstResponseDate)
            return new("Cumprido", DashboardTheme.Green, IconChar.Check);

        if (!bzFirstResponseDate)
            return new("Ag. 1ª Comunicação", DashboardTheme.Orange, IconChar.Clock);

        if (bzStatusKpiFirst == 419500002 && bzFirstResponseDate)
            return new("Expirado", DashboardTheme.Red, IconChar.HourglassHalf);

        return new("— N/D", DashboardTheme.TextSub, IconChar.Question);
    }

    // ── Customer Satisfaction ─────────────────────────────────────────────────

    public static BadgeInfo Satisfaction(int? code) => code switch
    {
        5 => new("#86EFAC", "#0A2010", "Muito Satisfeito", IconChar.FaceLaugh),
        4 => new("#6EE7B7", "#0A1F14", "Satisfeito", IconChar.FaceSmile),
        3 => new("#FCD34D", "#3B2A00", "Neutro", IconChar.FaceMeh),
        2 => new("#FCA5A5", "#2D0A0A", "Insatisfeito", IconChar.FaceFrown),
        1 => new("#F87171", "#3B0A0A", "Muito Insatisfeito", IconChar.FaceAngry),
        _ => new("#4B5663", "#1A2029", "Sem resposta", IconChar.Question),
    };

    public static ColoredLabel SatisfactionDetail(int? code) => code switch
    {
        5 => new("Muito Satisfeito", DashboardTheme.Green, IconChar.FaceLaugh),
        4 => new("Satisfeito", Color.FromRgb(0x6E, 0xE7, 0xB7), IconChar.FaceSmile),
        3 => new("Neutro", DashboardTheme.Yellow, IconChar.FaceMeh),
        2 => new("Insatisfeito", Color.FromRgb(0xFC, 0xA5, 0xA5), IconChar.FaceFrown),
        1 => new("Muito Insatisfeito", DashboardTheme.Red, IconChar.FaceAngry),
        _ => new("Sem resposta de satisfação", DashboardTheme.TextSub, IconChar.Question),
    };

    // ── Idle time ─────────────────────────────────────────────────────────────

    public static string IdleLabel(double hours) =>
        hours < 1 ? $"{(int)(hours * 60)}m atrás"
        : hours < 24 ? $"{hours:F0}h atrás"
                      : $"{hours / 24:F0}d atrás";

    public static Color IdleColor(double hours) =>
        hours > 48 ? DashboardTheme.Red
        : hours > 8 ? DashboardTheme.Yellow
        : DashboardTheme.TextSub;
}