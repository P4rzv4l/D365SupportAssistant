// =============================================================================
//  DiffEngine.cs — Diff linha a linha entre dois conteúdos
// =============================================================================
// Algoritmo LCS (Longest Common Subsequence).
// Arquivos grandes (> MaxLcsLines) usam fallback de comparação posicional
// simples para evitar O(n²) de memória/tempo, mas continuam corretos
// (sem perder sincronia como acontecia ao truncar o LCS no meio).
// =============================================================================

namespace D365Assistant.Views.Tools.Sections.Comparator;

public enum DiffStatus { Equal, Added, Removed }

public record DiffLine(string Text, DiffStatus Status, int? LineNumberLeft, int? LineNumberRight);

public static class DiffEngine
{
    // Acima disso, a matriz O(n*m) do LCS fica grande demais (~64MB em 8000x8000).
    // Usamos fallback nesse caso — ainda correto, só não acha o menor diff possível.
    private const int MaxLcsLines = 6000;

    public static List<DiffLine> Compute(string left, string right)
    {
        var lLines = Split(left);
        var rLines = Split(right);

        return (lLines.Count > MaxLcsLines || rLines.Count > MaxLcsLines)
            ? ComputePositional(lLines, rLines)
            : ComputeLcs(lLines, rLines);
    }

    // ── LCS completo (arquivos pequenos/médios) ──────────────────────────────

    private static List<DiffLine> ComputeLcs(List<string> lLines, List<string> rLines)
    {
        var lcs = BuildLcs(lLines, rLines);
        var result = new List<DiffLine>();

        int li = 0, ri = 0, lNum = 1, rNum = 1;

        foreach (var common in lcs)
        {
            while (li < lLines.Count && lLines[li] != common)
            {
                result.Add(new DiffLine(lLines[li], DiffStatus.Removed, lNum++, null));
                li++;
            }
            while (ri < rLines.Count && rLines[ri] != common)
            {
                result.Add(new DiffLine(rLines[ri], DiffStatus.Added, null, rNum++));
                ri++;
            }
            result.Add(new DiffLine(common, DiffStatus.Equal, lNum++, rNum++));
            li++;
            ri++;
        }

        while (li < lLines.Count)
        {
            result.Add(new DiffLine(lLines[li], DiffStatus.Removed, lNum++, null));
            li++;
        }
        while (ri < rLines.Count)
        {
            result.Add(new DiffLine(rLines[ri], DiffStatus.Added, null, rNum++));
            ri++;
        }

        return result;
    }

    private static List<string> BuildLcs(List<string> a, List<string> b)
    {
        var la = a.Count;
        var lb = b.Count;

        var dp = new int[la + 1, lb + 1];

        for (int i = 1; i <= la; i++)
            for (int j = 1; j <= lb; j++)
                dp[i, j] = a[i - 1] == b[j - 1]
                    ? dp[i - 1, j - 1] + 1
                    : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        var lcs = new List<string>();
        int x = la, y = lb;
        while (x > 0 && y > 0)
        {
            if (a[x - 1] == b[y - 1]) { lcs.Add(a[x - 1]); x--; y--; }
            else if (dp[x - 1, y] > dp[x, y - 1]) x--;
            else y--;
        }
        lcs.Reverse();
        return lcs;
    }

    // ── Fallback posicional (arquivos muito grandes) ─────────────────────────
    // Compara linha N de A com linha N de B. Não detecta linhas inseridas/removidas
    // no meio do arquivo (causaria desalinhamento), mas é O(n) e nunca trava.
    // Suficiente para o caso comum: mesmo arquivo com pequenas edições pontuais.

    private static List<DiffLine> ComputePositional(List<string> lLines, List<string> rLines)
    {
        var result = new List<DiffLine>();
        var max = Math.Max(lLines.Count, rLines.Count);

        for (int i = 0; i < max; i++)
        {
            var hasLeft = i < lLines.Count;
            var hasRight = i < rLines.Count;

            if (hasLeft && hasRight)
            {
                var status = lLines[i] == rLines[i] ? DiffStatus.Equal : DiffStatus.Removed;
                result.Add(new DiffLine(lLines[i], status, i + 1, status == DiffStatus.Equal ? i + 1 : null));

                if (status != DiffStatus.Equal)
                    result.Add(new DiffLine(rLines[i], DiffStatus.Added, null, i + 1));
            }
            else if (hasLeft)
            {
                result.Add(new DiffLine(lLines[i], DiffStatus.Removed, i + 1, null));
            }
            else
            {
                result.Add(new DiffLine(rLines[i], DiffStatus.Added, null, i + 1));
            }
        }

        return result;
    }

    private static List<string> Split(string content) =>
        content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').ToList();
}