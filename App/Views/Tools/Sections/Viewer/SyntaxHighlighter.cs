// =============================================================================
//  SyntaxHighlighter.cs — Colorização básica de JS / CSS / HTML
// =============================================================================
// Produz um RichTextBox (somente leitura) com tokens coloridos.
// Não depende de libs externas — tokenização por regex simples.
// =============================================================================

using D365Assistant.Views.Tools.Theme;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace D365Assistant.Views.Tools.Sections.Viewer;

public enum ContentLanguage { JavaScript, Css, Html, Unknown }

public static class SyntaxHighlighter
{
    // ── Token colors ──────────────────────────────────────────────────────────
    private static readonly Color CKeyword = Color.FromRgb(0xC5, 0x78, 0xFF); // purple
    private static readonly Color CString = Color.FromRgb(0xCE, 0x91, 0x78); // orange
    private static readonly Color CComment = Color.FromRgb(0x60, 0x71, 0x6A); // gray-green
    private static readonly Color CNumber = Color.FromRgb(0x4E, 0xC9, 0xB0); // teal
    private static readonly Color CTag = Color.FromRgb(0x56, 0x9C, 0xD6); // blue
    private static readonly Color CAttr = Color.FromRgb(0x9C, 0xDC, 0xFE); // light blue
    private static readonly Color CSelector = Color.FromRgb(0xD7, 0xBA, 0x7D); // yellow
    private static readonly Color CProp = Color.FromRgb(0x9C, 0xDC, 0xFE); // light blue
    private static readonly Color CDefault = ToolsTheme.Text;

    // ── JS keywords ───────────────────────────────────────────────────────────
    private static readonly HashSet<string> JsKeywords =
    [
        "var","let","const","function","return","if","else","for","while","do",
        "switch","case","break","continue","new","this","typeof","instanceof",
        "true","false","null","undefined","class","extends","import","export",
        "default","async","await","try","catch","finally","throw","of","in",
        "delete","void","yield","static","super","from",
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    public static RichTextBox Build(string content, ContentLanguage lang,
                                    double fontSize = 12)
    {
        var rtb = new RichTextBox
        {
            IsReadOnly = true,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Foreground = ToolsTheme.Brush(CDefault),
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = fontSize,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsDocumentEnabled = true,
        };

        var doc = new FlowDocument();
        var para = new Paragraph { LineHeight = fontSize * 1.6, Margin = new Thickness(0) };
        doc.Blocks.Add(para);
        rtb.Document = doc;

        var runs = lang switch
        {
            ContentLanguage.JavaScript => TokenizeJs(content),
            ContentLanguage.Css => TokenizeCss(content),
            ContentLanguage.Html => TokenizeHtml(content),
            _ => new[] { new Run(content) { Foreground = ToolsTheme.Brush(CDefault) } },
        };

        foreach (var run in runs)
            para.Inlines.Add(run);

        return rtb;
    }

    public static ContentLanguage DetectLanguage(int typeCode) => typeCode switch
    {
        3 => ContentLanguage.JavaScript,
        2 => ContentLanguage.Css,
        1 => ContentLanguage.Html,
        _ => ContentLanguage.Unknown,
    };

    // ── JavaScript tokenizer ──────────────────────────────────────────────────

    private static IEnumerable<Run> TokenizeJs(string src)
    {
        // Pattern order matters — more specific first
        var pattern = new Regex(
            @"(//[^\n]*|/\*[\s\S]*?\*/)" +           // comments
            @"|(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|`(?:[^`\\]|\\.)*`)" + // strings
            @"|(\b(?:0x[\da-fA-F]+|\d+\.?\d*)\b)" +  // numbers
            @"|(\b(?:" + string.Join("|", JsKeywords) + @")\b)" + // keywords
            @"|([a-zA-Z_$][a-zA-Z0-9_$]*)" +          // identifiers
            @"|(\S|\s+)",                               // other / whitespace
            RegexOptions.Compiled);

        foreach (Match m in pattern.Matches(src))
        {
            Color color;
            if (m.Groups[1].Success) color = CComment;
            else if (m.Groups[2].Success) color = CString;
            else if (m.Groups[3].Success) color = CNumber;
            else if (m.Groups[4].Success) color = CKeyword;
            else color = CDefault;

            yield return new Run(m.Value) { Foreground = new SolidColorBrush(color) };
        }
    }

    // ── CSS tokenizer ─────────────────────────────────────────────────────────

    private static IEnumerable<Run> TokenizeCss(string src)
    {
        var pattern = new Regex(
            @"(/\*[\s\S]*?\*/)" +                   // comments
            @"|(""[^""]*""|'[^']*')" +              // strings
            @"|(\b\d+\.?\d*(?:px|em|rem|%|vh|vw|s|ms|deg)?\b)" + // numbers+units
            @"|([.#]?[a-zA-Z][a-zA-Z0-9_-]*\s*(?=\{))" + // selectors
            @"|([a-zA-Z-]+\s*(?=:))" +              // properties
            @"|(\S|\s+)",
            RegexOptions.Compiled);

        foreach (Match m in pattern.Matches(src))
        {
            Color color;
            if (m.Groups[1].Success) color = CComment;
            else if (m.Groups[2].Success) color = CString;
            else if (m.Groups[3].Success) color = CNumber;
            else if (m.Groups[4].Success) color = CSelector;
            else if (m.Groups[5].Success) color = CProp;
            else color = CDefault;

            yield return new Run(m.Value) { Foreground = new SolidColorBrush(color) };
        }
    }

    // ── HTML tokenizer ────────────────────────────────────────────────────────

    private static IEnumerable<Run> TokenizeHtml(string src)
    {
        var pattern = new Regex(
            @"(<!--[\s\S]*?-->)" +                  // comments
            @"|(</?[a-zA-Z][a-zA-Z0-9]*)" +        // opening tags
            @"|(>)" +                               // closing >
            @"|([a-zA-Z-]+=)" +                    // attributes
            @"|(""[^""]*""|'[^']*')" +             // attr values
            @"|(\S|\s+)",
            RegexOptions.Compiled);

        foreach (Match m in pattern.Matches(src))
        {
            Color color;
            if (m.Groups[1].Success) color = CComment;
            else if (m.Groups[2].Success) color = CTag;
            else if (m.Groups[3].Success) color = CTag;
            else if (m.Groups[4].Success) color = CAttr;
            else if (m.Groups[5].Success) color = CString;
            else color = CDefault;

            yield return new Run(m.Value) { Foreground = new SolidColorBrush(color) };
        }
    }
}