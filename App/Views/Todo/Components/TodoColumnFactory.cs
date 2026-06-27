// =============================================================================
//  TodoColumnFactory.cs — Definição de colunas da tabela de tarefas
// =============================================================================

using System.Windows;
using System.Windows.Controls;

namespace D365Assistant.Views.Todo.Components;

public static class TodoColumnFactory
{
    public const int ColTask = 0;
    public const int ColRelated = 1;
    public const int ColCategory = 2;
    public const int ColPriority = 3;
    public const int ColDueDate = 4;
    public const int ColStatus = 5;

    public static Grid Create()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.8, GridUnitType.Star) }); // Tarefa
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }); // Relacionado
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) }); // Tipo
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) }); // Prioridade
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) }); // Vencimento
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) }); // Status
        return g;
    }
}