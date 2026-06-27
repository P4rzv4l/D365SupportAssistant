// =============================================================================
//  TodoDisplayMappers.cs — Mapeamento de domínio → apresentação
// =============================================================================
// Funções puras, sem side-effects, testáveis sem UI.
// =============================================================================

using D365Assistant.Views.Dashboard.Theme;
using System.Windows.Media;

namespace D365Assistant.Views.Todo.Helpers;

public readonly record struct BadgeInfo(string FgHex, string BgHex, string Label);
public readonly record struct ColoredLabel(string Text, Color Color);

public static class TodoDisplayMappers
{
    // ── Priority ──────────────────────────────────────────────────────────────

    public static BadgeInfo Priority(int code) => code switch
    {
        1 => new("#FCA5A5", "#3B0C0C", "Alta"),
        3 => new("#86EFAC", "#0A2010", "Baixa"),
        _ => new("#FCD34D", "#3B2A00", "Média"),
    };

    public static int PriorityCode(string label) => label switch
    {
        "Alta" => 1,
        "Baixa" => 3,
        _ => 2,
    };

    public static string PriorityLabel(int code) => code switch
    {
        1 => "Alta",
        3 => "Baixa",
        _ => "Média",
    };

    // ── Status ────────────────────────────────────────────────────────────────

    public static BadgeInfo Status(bool done, bool overdue) =>
        done ? new("#86EFAC", "#0A2010", "Concluída")
        : overdue ? new("#FCA5A5", "#3B0C0C", "Atrasada")
                  : new("#94A3B8", "#1E2A38", "Pendente");

    public static ColoredLabel StatusDetail(bool done, bool overdue)
    {
        if (done) return new("Concluída", DashboardTheme.Green);
        if (overdue) return new("Atrasada", DashboardTheme.Red);
        return new("Pendente", DashboardTheme.TextSub);
    }

    // ── Due date color ────────────────────────────────────────────────────────

    public static Color DueColor(bool overdue, bool dueToday) =>
        overdue ? DashboardTheme.Red
        : dueToday ? DashboardTheme.Yellow
                   : DashboardTheme.TextSub;
}