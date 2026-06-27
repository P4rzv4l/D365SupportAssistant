// =============================================================================
//  TableColumnFactory.cs — Definição de colunas da tabela
// =============================================================================
// Regra: mudar largura de coluna = mudar UMA linha aqui, em nenhum outro lugar.
// =============================================================================

using System.Windows;
using System.Windows.Controls;

namespace D365Assistant.Views.Dashboard.Components;

public static class TableColumnFactory
{
    // Índices públicos: evitam magic numbers nos callers
    public const int ColId = 0;
    public const int ColSubject = 1;
    public const int ColCustomer = 2;
    public const int ColStatus = 3;
    public const int ColPriority = 4;
    public const int ColDate = 5;
    public const int ColSla = 6;
    public const int ColSatisf = 7;

    /// <summary>
    /// Cria um Grid com as colunas padrão da tabela de chamados.
    /// A coluna de Satisfação é exibida apenas na aba "Resolvidos".
    /// </summary>
    public static Grid Create(bool showSatisfaction)
    {
        var g = new Grid();

        Add(g, 148);                                           // 0: ID          (CAS-XXXXX-XXXXXX)
        Add(g, 1, GridUnitType.Star);                         // 1: Assunto      (flexível)
        Add(g, 160);                                          // 2: Cliente
        Add(g, 148);                                          // 3: Status
        Add(g, 90);                                           // 4: Prioridade
        Add(g, 138);                                          // 5: Abertura + idle
        Add(g, 120);                                          // 6: SLA
        Add(g, showSatisfaction ? 150 : 0);                   // 7: Satisfação

        return g;
    }

    private static void Add(Grid g, double width,
                            GridUnitType unit = GridUnitType.Pixel)
        => g.ColumnDefinitions.Add(
               new ColumnDefinition { Width = new GridLength(width, unit) });
}