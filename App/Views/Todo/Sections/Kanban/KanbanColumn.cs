// =============================================================================
//  KanbanColumn.cs — Definição das colunas do Kanban
// =============================================================================

namespace D365Assistant.Views.Todo.Sections.Kanban;

public enum KanbanColumn
{
    Novo = 0,
    Pendente = 1,
    EmAndamento = 2,
    Validacao = 3,
    Atrasado = 4,
    Concluido = 5,
}

public static class KanbanColumnMeta
{
    public record ColumnInfo(string Label, string FgHex, string BgHex, string BorderHex);

    public static readonly IReadOnlyDictionary<KanbanColumn, ColumnInfo> All =
        new Dictionary<KanbanColumn, ColumnInfo>
        {
            [KanbanColumn.Novo] = new("Novo", "#93C5FD", "#0C1F3A", "#1E3A5F"),
            [KanbanColumn.Pendente] = new("Pendente", "#94A3B8", "#1E2A38", "#2D3F52"),
            [KanbanColumn.EmAndamento] = new("Em Andamento", "#86EFAC", "#0A2010", "#14401E"),
            [KanbanColumn.Validacao] = new("Validação", "#A78BFA", "#1E1245", "#32206A"),
            [KanbanColumn.Atrasado] = new("Atrasado", "#FCA5A5", "#3B0C0C", "#5A1010"),
            [KanbanColumn.Concluido] = new("Concluído", "#6EE7B7", "#0A1F14", "#0F3020"),
        };

    /// <summary>Mapeia o status atual do TodoItem para uma coluna inicial do Kanban.</summary>
    public static KanbanColumn FromTodoItem(D365Assistant.Core.Models.Todo.TodoItem item)
    {
        if (item.Done) return KanbanColumn.Concluido;
        if (item.IsOverdue) return KanbanColumn.Atrasado;
        return KanbanColumn.Pendente;
    }
}