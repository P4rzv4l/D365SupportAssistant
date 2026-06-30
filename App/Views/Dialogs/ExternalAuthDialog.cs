// =============================================================================
//  ExternalAuthDialog.cs — Dialog do Device Code Flow para ambientes externos
// =============================================================================
// Exibido quando o ExternalAuthService precisa que o usuário autentique
// num ambiente de cliente. Mostra o código, URL e abre o browser.
// =============================================================================

using D365Assistant.Views.Dashboard.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBorder = System.Windows.Controls.Border;

namespace D365Assistant.Views.Dialogs;

public sealed class ExternalAuthDialog : Window
{
    public ExternalAuthDialog(string userCode, string verificationUrl, string message)
    {
        Title = "Autenticação Necessária";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = DashboardTheme.Brush(DashboardTheme.Surface);

        Content = BuildContent(userCode, verificationUrl, message);
    }

    private UIElement BuildContent(string code, string url, string message)
    {
        var stack = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };

        // Icon + title
        stack.Children.Add(new TextBlock
        {
            Text = "🔐  Login necessário",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = DashboardTheme.Brush(DashboardTheme.Text),
            Margin = new Thickness(0, 0, 0, 8),
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Para acessar este ambiente Dynamics 365, autentique com sua conta Microsoft:",
            FontSize = 12,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20),
        });

        // Step 1
        stack.Children.Add(StepLabel("1. Acesse a URL no browser:"));
        stack.Children.Add(UrlBox(url));

        // Step 2
        stack.Children.Add(StepLabel("2. Digite este código:"));
        stack.Children.Add(BuildCodeBox(code));

        // Browser note
        stack.Children.Add(new TextBlock
        {
            Text = "ℹ  O browser foi aberto automaticamente. Se não abriu, copie a URL acima.",
            FontSize = 11,
            Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 20),
        });

        // Close button
        var btnClose = new Button
        {
            Content = "OK — Aguardando autenticação",
            FontSize = 12,
            Background = DashboardTheme.Brush(DashboardTheme.Accent),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(0, 10, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        btnClose.Click += (_, _) => Close();
        stack.Children.Add(btnClose);

        return stack;
    }

    private static TextBlock StepLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = DashboardTheme.Brush(DashboardTheme.TextSub),
        Margin = new Thickness(0, 0, 0, 6),
    };

    private static UIElement UrlBox(string url)
    {
        var border = new WpfBorder
        {
            Background = DashboardTheme.Brush(DashboardTheme.Bg),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 16),
            Cursor = Cursors.Hand,
        };

        border.Child = new TextBlock
        {
            Text = url,
            FontSize = 11,
            FontFamily = new FontFamily("Consolas"),
            Foreground = DashboardTheme.Brush(DashboardTheme.Accent),
            TextDecorations = TextDecorations.Underline,
            TextWrapping = TextWrapping.Wrap,
        };

        border.MouseLeftButtonUp += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        };

        return border;
    }

    private static UIElement BuildCodeBox(string code)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var codeBorder = new WpfBorder
        {
            Background = DashboardTheme.AlphaBrush(DashboardTheme.Accent, 0x15),
            BorderBrush = DashboardTheme.Brush(DashboardTheme.Accent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6, 0, 0, 6),
            Padding = new Thickness(16, 12, 16, 12),
            Child = new TextBlock
            {
                Text = code,
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = DashboardTheme.Brush(DashboardTheme.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
            },
        };
        Grid.SetColumn(codeBorder, 0);
        g.Children.Add(codeBorder);

        var btnCopy = new Button
        {
            Content = "📋",
            FontSize = 16,
            Background = DashboardTheme.Brush(DashboardTheme.Accent),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(14, 0, 14, 0),
            ToolTip = "Copiar código",
        };
        btnCopy.Click += (_, _) =>
        {
            System.Windows.Clipboard.SetText(code);
            btnCopy.Content = "✓";
        };
        Grid.SetColumn(btnCopy, 1);
        g.Children.Add(btnCopy);

        return g;
    }
}