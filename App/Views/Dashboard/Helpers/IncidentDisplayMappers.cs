// =============================================================================
//  IncidentDisplayMappers.cs — Mapeamento de domínio → apresentação
// =============================================================================
// Regra: funções puras, sem side-effects, sem dependência de UI.
// Testáveis em xUnit sem abrir janela alguma.
// =============================================================================

using D365Assistant.Views.Dashboard.Theme;
using System.Windows.Media;

namespace D365Assistant.Views.Dashboard.Helpers;

/// <summary>Dados visuais para um badge (cor + label).</summary>
public readonly record struct BadgeInfo(string FgHex, string BgHex, string Label);

/// <summary>Dados visuais para SLA ou satisfação (label + cor WPF).</summary>
public readonly record struct ColoredLabel(string Text, Color Color);

/// <summary>
/// Converte códigos de domínio do Dynamics em informações de exibição.
/// Nenhum método aqui conhece WPF além de <see cref="Color"/>.
/// </summary>
public static class IncidentDisplayMappers
{
    // ── Priority ──────────────────────────────────────────────────────────────

    public static BadgeInfo Priority(int? code) => code switch
    {
        419500000 => new("#FCA5A5", "#3B0C0C", "Urgente"),
        1 => new("#FCD34D", "#3B2A00", "Alto"),
        2 => new("#93C5FD", "#0C1F3A", "Normal"),
        3 => new("#86EFAC", "#0A2010", "Baixa"),
        _ => new("#64748B", "#0F1520", "—"),
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
        100000000 => new("#93C5FD", "#0C1F3A", "Novo"),
        4 => new("#64748B", "#0F1520", "Aguard. Fila"),
        1 => new("#86EFAC", "#0A2010", "Em Atendimento"),
        419500000 => new("#FCD34D", "#3B2A00", "Aguard. Cliente"),
        3 => new("#A78BFA", "#1E1245", "Em Aprovação"),
        2 => new("#FCA5A5", "#3B0C0C", "Impeditivo"),
        5 => new("#86EFAC", "#0A2010", "Resolvido"),
        6 => new("#374151", "#0D1117", "Cancelado"),
        419500001 => new("#374151", "#0F1520", "Despriorizado"),
        121360001 => new("#FCD34D", "#3B2A00", "Aguard. Microsoft"),
        _ => new("#374151", "#0F1520", $"St.{code}"),
    };

    // ── SLA ───────────────────────────────────────────────────────────────────

    /// <summary>Badge para coluna da tabela.</summary>
    public static (string text, string fgHex, string bgHex, string tooltip) SlaTableBadge(
        int? bzStatusKpiFirst)
    {
        if (bzStatusKpiFirst == 419500000)
            return ("✓ 1º Atend.", "#86EFAC", "#0A2010",
                    "SLA de primeiro atendimento cumprido");

        return ("⚡ Ag. 1ª Com.", "#FB923C", "#2D1500",
                "Aguardando envio da 1ª comunicação ao cliente");
    }

    /// <summary>Label para painel de detalhes.</summary>
    public static ColoredLabel SlaDetail(int? bzStatusKpiFirst, bool firstResponseSent)
    {
        if (bzStatusKpiFirst == 419500002)
            return new("✓ Cumprido", DashboardTheme.Green);

        if (!firstResponseSent)
            return new("⚡ Ag. 1ª Comunicação", DashboardTheme.Orange);

        if (bzStatusKpiFirst == 419500000)
            return new("⏱ Pendente", DashboardTheme.Yellow);

        return new("— N/D", DashboardTheme.TextSub);
    }

    // ── Customer Satisfaction ─────────────────────────────────────────────────

    public static BadgeInfo Satisfaction(int? code) => code switch
    {
        5 => new("#86EFAC", "#0A2010", "😊 Muito Satisfeito"),
        4 => new("#6EE7B7", "#0A1F14", "🙂 Satisfeito"),
        3 => new("#FCD34D", "#3B2A00", "😐 Neutro"),
        2 => new("#FCA5A5", "#2D0A0A", "😕 Insatisfeito"),
        1 => new("#F87171", "#3B0A0A", "😞 Muito Insatisfeito"),
        _ => new("#4B5663", "#1A2029", "— Sem resposta"),
    };

    public static ColoredLabel SatisfactionDetail(int? code) => code switch
    {
        5 => new("😊 Muito Satisfeito", DashboardTheme.Green),
        4 => new("🙂 Satisfeito", Color.FromRgb(0x6E, 0xE7, 0xB7)),
        3 => new("😐 Neutro", DashboardTheme.Yellow),
        2 => new("😕 Insatisfeito", Color.FromRgb(0xFC, 0xA5, 0xA5)),
        1 => new("😞 Muito Insatisfeito", DashboardTheme.Red),
        _ => new("Sem resposta de satisfação", DashboardTheme.TextSub),
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