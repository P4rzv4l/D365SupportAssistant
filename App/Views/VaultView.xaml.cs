// =============================================================================
//  App/Views/VaultView.xaml.cs — Code-behind do Cofre de Credenciais
//  Alterações:
//    • BuildCredentialCard: exibe URL e Validade (com badge de alerta se expirada)
//    • CopyField: label fixo em 100 px para alinhamento consistente
//    • OnRequestCredential: assinatura alinhada com os campos reais de VaultCredential
// =============================================================================

using D365Assistant.Core.Models.Vault;
using D365Assistant.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

public partial class VaultView : Page
{
    private readonly VaultViewModel _vm;
    private string _activeTab = "Credentials";

    // ── Paleta de cores ───────────────────────────────────────────────────────
    private static readonly SolidColorBrush BrushSurface = B("#0B1018");
    private static readonly SolidColorBrush BrushBorder = B("#0D1825");
    private static readonly SolidColorBrush BrushTextPri = B("#C8DCF0");
    private static readonly SolidColorBrush BrushTextSec = B("#3D4E63");
    private static readonly SolidColorBrush BrushTextMuted = B("#1E2E40");
    private static readonly SolidColorBrush BrushBlue = B("#1A6CF5");
    private static readonly SolidColorBrush BrushGreen = B("#22C55E");
    private static readonly SolidColorBrush BrushRed = B("#C0392B");
    private static readonly SolidColorBrush BrushAmber = B("#D4830A");
    private static readonly SolidColorBrush BrushPurple = B("#7C3AED");

    public VaultView(VaultViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        _vm.RequestUnlockDialog += OnRequestUnlock;
        _vm.RequestClientDialog += OnRequestClient;
        _vm.RequestCredentialDialog += OnRequestCredential;
        _vm.RequestLinkDialog += OnRequestLink;
        _vm.RequestConfirmDialog += OnRequestConfirm;

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(_vm.IsUnlocked):
                    UpdateLockUI();
                    RefreshClientList();
                    break;
                case nameof(_vm.SelectedClient):
                    UpdateDetailPanel();
                    break;
                case nameof(_vm.StatusText):
                    UpdateStatusMsg();
                    break;
            }
        };

        _vm.Clients.CollectionChanged += (_, _) => RefreshClientList();
        _vm.Credentials.CollectionChanged += (_, _) => RefreshCredentialList();
        _vm.Links.CollectionChanged += (_, _) => RefreshLinkList();

        UpdateLockUI();
        RefreshClientList();
    }

    // ── Estado do lock ────────────────────────────────────────────────────────

    private void UpdateLockUI()
    {
        if (_vm.IsUnlocked)
        {
            LockBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, 0x22, 0xC5, 0x5E));
            LockBadgeText.Text = "🔓  Desbloqueado";
            LockBadgeText.Foreground = BrushGreen;
            BtnToggleLock.Content = "🔒  Bloquear";
            BtnAddClient.Visibility = Visibility.Visible;
        }
        else
        {
            LockBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xC0, 0x39, 0x2B));
            LockBadgeText.Text = "🔒  Bloqueado";
            LockBadgeText.Foreground = BrushRed;
            BtnToggleLock.Content = "🔓  Desbloquear";
            BtnAddClient.Visibility = Visibility.Collapsed;
            EmptyMsg.Text = "🔒  Vault bloqueado\nClique em Desbloquear para acessar.";
            EmptyPanel.Visibility = Visibility.Visible;
            DetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateStatusMsg()
    {
        if (string.IsNullOrEmpty(_vm.StatusText))
        {
            StatusMsg.Visibility = Visibility.Collapsed;
        }
        else
        {
            StatusMsg.Text = _vm.StatusText;
            StatusMsg.Visibility = Visibility.Visible;
        }
    }

    // ── Painel de detalhe ─────────────────────────────────────────────────────

    private void UpdateDetailPanel()
    {
        if (_vm.SelectedClient is null)
        {
            EmptyMsg.Text = "← Selecione um cliente";
            EmptyPanel.Visibility = Visibility.Visible;
            DetailPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var client = _vm.SelectedClient;
        var color = ParseColor(client.Color);

        ClientColorBar.Background = new SolidColorBrush(color);
        ClientNameText.Text = client.Name;

        if (!string.IsNullOrEmpty(client.CrmUrl))
        {
            ClientUrlText.Text = client.CrmUrl;
            ClientUrlText.Visibility = Visibility.Visible;
        }
        else
        {
            ClientUrlText.Visibility = Visibility.Collapsed;
        }

        NotesBox.Text = client.Notes;

        EmptyPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;

        SwitchTab(_activeTab);
        RefreshCredentialList();
        RefreshLinkList();
    }

    // ── Lista de clientes ─────────────────────────────────────────────────────

    private void RefreshClientList()
    {
        ClientListPanel.Children.Clear();

        if (!_vm.IsUnlocked)
            return;

        if (_vm.Clients.Count == 0)
        {
            ClientListPanel.Children.Add(new TextBlock
            {
                Text = "Nenhum cliente.\nClique em ➕ Cliente\npara adicionar.",
                FontSize = 11,
                Foreground = BrushTextMuted,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        foreach (var client in _vm.Clients)
            ClientListPanel.Children.Add(BuildClientRow(client));
    }

    private Border BuildClientRow(VaultClient client)
    {
        var isSelected = _vm.SelectedClient?.Id == client.Id;
        var clientColor = ParseColor(client.Color);
        var bgColor = isSelected
            ? Color.FromRgb(0x0E, 0x16, 0x21)
            : Color.FromRgb(0x0B, 0x10, 0x18);

        var row = new Border
        {
            Background = new SolidColorBrush(bgColor),
            BorderBrush = BrushBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.Hand,
        };

        var grid = new Grid { Margin = new Thickness(0, 8, 10, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Child = grid;

        // Barra de cor
        var bar = new Border { Background = new SolidColorBrush(clientColor) };
        Grid.SetColumn(bar, 0);
        grid.Children.Add(bar);

        // Inicial
        var initial = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x30, clientColor.R, clientColor.G, clientColor.B)),
            CornerRadius = new CornerRadius(4),
            Width = 26,
            Height = 26,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = client.Name.Length > 0 ? client.Name[0].ToString().ToUpper() : "?",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(clientColor),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
        };
        Grid.SetColumn(initial, 1);
        grid.Children.Add(initial);

        // Nome
        var name = new TextBlock
        {
            Text = client.Name.Length > 18 ? client.Name[..18] + "…" : client.Name,
            FontSize = 12,
            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = isSelected ? BrushTextPri : BrushTextSec,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 2);
        grid.Children.Add(name);

        // Hover
        row.MouseEnter += (_, _) => { if (!isSelected) row.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x16, 0x21)); };
        row.MouseLeave += (_, _) => { if (!isSelected) row.Background = new SolidColorBrush(bgColor); };
        row.MouseLeftButtonUp += (_, _) => { _vm.SelectClient(client); RefreshClientList(); };

        return row;
    }

    // ── Lista de credenciais ──────────────────────────────────────────────────

    private void RefreshCredentialList()
    {
        CredentialListPanel.Children.Clear();

        if (_vm.Credentials.Count == 0)
        {
            CredentialListPanel.Children.Add(EmptyHint("Nenhuma credencial. Clique em ➕ Adicionar."));
            return;
        }

        foreach (var cred in _vm.Credentials)
            CredentialListPanel.Children.Add(BuildCredentialCard(cred));
    }

    private UIElement BuildCredentialCard(VaultCredential cred)
    {
        var card = new Border { Style = (Style)FindResource("ItemCard") };
        var stack = new StackPanel();
        card.Child = stack;

        // ── Header: rótulo + ações ────────────────────────────────────────
        var hdr = new Grid();
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(MakeBadge("🔑", BrushPurple));
        left.Children.Add(new TextBlock
        {
            Text = cred.Label,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushTextPri,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 10, 0),
        });

        Grid.SetColumn(left, 0);
        hdr.Children.Add(left);

        var acts = new StackPanel { Orientation = Orientation.Horizontal };
        acts.Children.Add(IconBtn("✏", BrushTextSec, () => _vm.EditCredentialCommand.Execute(cred)));
        acts.Children.Add(IconBtn("🗑", BrushRed, () => _vm.DeleteCredentialCommand.Execute(cred)));
        Grid.SetColumn(acts, 1);
        hdr.Children.Add(acts);

        stack.Children.Add(hdr);
        stack.Children.Add(new Border { Height = 10 });

        // ── Campos copiáveis ──────────────────────────────────────────────
        if (!string.IsNullOrEmpty(cred.Username))
            stack.Children.Add(CopyField("Usuário", cred.Username));
        if (!string.IsNullOrEmpty(cred.Password))
            stack.Children.Add(CopyField("Senha", cred.Password, secret: true));
        if (!string.IsNullOrEmpty(cred.Extra))
            stack.Children.Add(CopyField("Extra", cred.Extra));
        if (!string.IsNullOrEmpty(cred.Notes))
            stack.Children.Add(new TextBlock
            {
                Text = cred.Notes,
                FontSize = 11,
                Foreground = BrushTextMuted,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });

        return card;
    }

    // ── Lista de links ────────────────────────────────────────────────────────

    private void RefreshLinkList()
    {
        LinkListPanel.Children.Clear();

        if (_vm.Links.Count == 0)
        {
            LinkListPanel.Children.Add(EmptyHint("Nenhum link. Clique em ➕ Adicionar."));
            return;
        }

        foreach (var link in _vm.Links)
            LinkListPanel.Children.Add(BuildLinkCard(link));
    }

    private UIElement BuildLinkCard(VaultLink link)
    {
        var envColor = link.EnvName switch
        {
            "PRD" => BrushGreen,
            "HML" => BrushAmber,
            "DEV" => BrushBlue,
            "UAT" => BrushPurple,
            _ => BrushTextSec,
        };

        var card = new Border
        {
            Background = BrushSurface,
            BorderBrush = BrushBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 10),
            ClipToBounds = true,
        };

        var outer = new Grid();
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        card.Child = outer;

        outer.Children.Add(new Border
        {
            Background = envColor,
            CornerRadius = new CornerRadius(8, 0, 0, 8),
        });

        var inner = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
        Grid.SetColumn(inner, 1);
        outer.Children.Add(inner);

        // Header
        var hdr = new Grid();
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        hdr.Children.Add(MakeBadge(link.EnvName, envColor));

        var acts = new StackPanel { Orientation = Orientation.Horizontal };
        acts.Children.Add(IconBtn("✏", BrushTextSec, () => _vm.EditLinkCommand.Execute(link)));
        acts.Children.Add(IconBtn("🗑", BrushRed, () => _vm.DeleteLinkCommand.Execute(link)));
        Grid.SetColumn(acts, 1);
        hdr.Children.Add(acts);

        inner.Children.Add(hdr);
        inner.Children.Add(new Border { Height = 10 });

        if (!string.IsNullOrEmpty(link.Url))
            inner.Children.Add(CopyField("URL", link.Url, isLink: true));
        if (!string.IsNullOrEmpty(link.Username))
            inner.Children.Add(CopyField("Usuário", link.Username));
        if (!string.IsNullOrEmpty(link.Password))
            inner.Children.Add(CopyField("Senha", link.Password, secret: true));
        if (!string.IsNullOrEmpty(link.Notes))
            inner.Children.Add(new TextBlock
            {
                Text = link.Notes,
                FontSize = 11,
                Foreground = BrushTextMuted,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });

        return card;
    }

    // ── Campo copiável ────────────────────────────────────────────────────────

    private UIElement CopyField(string label, string value,
                                bool secret = false, bool isLink = false)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        // Label fixo em 100 px para alinhamento consistente entre todos os campos
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = BrushTextMuted,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var masked = secret ? new string('•', Math.Min(value.Length, 14)) : value;
        var isRevealed = false;

        var valueLbl = new TextBlock
        {
            Text = masked,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            Foreground = isLink ? BrushBlue : BrushTextPri,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Cursor = isLink ? Cursors.Hand : Cursors.Arrow,
        };
        if (isLink)
            valueLbl.MouseLeftButtonUp += (_, _) => TryOpenUrl(value);
        Grid.SetColumn(valueLbl, 1);
        row.Children.Add(valueLbl);

        // Botões de ação
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(btnPanel, 2);
        row.Children.Add(btnPanel);

        if (secret)
        {
            btnPanel.Children.Add(IconBtn("👁", BrushTextSec, () =>
            {
                isRevealed = !isRevealed;
                valueLbl.Text = isRevealed ? value : new string('•', Math.Min(value.Length, 14));
            }));
        }

        // Feedback "copiado"
        var feedback = new TextBlock
        {
            Text = "",
            FontSize = 10,
            Foreground = BrushGreen,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };

        btnPanel.Children.Add(IconBtn("📋", BrushTextSec, () =>
        {
            Clipboard.SetText(value);
            feedback.Text = "✓";
            var t = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += (_, _) => { feedback.Text = ""; t.Stop(); };
            t.Start();
        }));
        btnPanel.Children.Add(feedback);

        return row;
    }

    // ── Tabs ──────────────────────────────────────────────────────────────────

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            SwitchTab(tag);
    }

    private void SwitchTab(string tab)
    {
        _activeTab = tab;

        TabCredentials.Visibility = tab == "Credentials" ? Visibility.Visible : Visibility.Collapsed;
        TabLinks.Visibility = tab == "Links" ? Visibility.Visible : Visibility.Collapsed;
        TabNotes.Visibility = tab == "Notes" ? Visibility.Visible : Visibility.Collapsed;

        TabBtnCreds.Style = tab == "Credentials" ? (Style)FindResource("TabBtnActive") : (Style)FindResource("TabBtn");
        TabBtnLinks.Style = tab == "Links" ? (Style)FindResource("TabBtnActive") : (Style)FindResource("TabBtn");
        TabBtnNotes.Style = tab == "Notes" ? (Style)FindResource("TabBtnActive") : (Style)FindResource("TabBtn");
    }

    // ── Eventos de UI ─────────────────────────────────────────────────────────

    private void BtnToggleLock_Click(object sender, RoutedEventArgs e) => _vm.ToggleLockCommand.Execute(null);
    private void BtnAddClient_Click(object sender, RoutedEventArgs e) => _vm.AddClientCommand.Execute(null);
    private void BtnEditClient_Click(object sender, RoutedEventArgs e) => _vm.EditClientCommand.Execute(null);
    private void BtnDeleteClient_Click(object sender, RoutedEventArgs e) => _vm.DeleteClientCommand.Execute(null);
    private void BtnAddCredential_Click(object sender, RoutedEventArgs e) => _vm.AddCredentialCommand.Execute(null);
    private void BtnAddLink_Click(object sender, RoutedEventArgs e) => _vm.AddLinkCommand.Execute(null);

    private void BtnSaveNotes_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedClient is null) return;
        var c = _vm.SelectedClient;
        _vm.ConfirmClientDialog(c.Name, c.CrmUrl, NotesBox.Text, c.Color, c.Id);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => _vm.SearchText = ((TextBox)sender).Text;

    private void ClientUrl_Click(object sender, MouseButtonEventArgs e)
    {
        if (_vm.SelectedClient is not null)
            TryOpenUrl(_vm.SelectedClient.CrmUrl);
    }

    // ── Diálogos ──────────────────────────────────────────────────────────────

    private void OnRequestUnlock(bool isSetup)
    {
        var dlg = new VaultUnlockDialog(Window.GetWindow(this)!, isSetup);
        if (dlg.ShowDialog() == true && dlg.Password is not null)
            _vm.PerformUnlock(dlg.Password, isSetup);
    }

    private void OnRequestClient(VaultClient? existing)
    {
        var dlg = new VaultClientDialog(Window.GetWindow(this)!, existing);
        if (dlg.ShowDialog() == true && dlg.Result is not null)
            _vm.ConfirmClientDialog(
                dlg.Result.Name, dlg.Result.CrmUrl,
                dlg.Result.Notes, dlg.Result.Color, existing?.Id);
    }

    private void OnRequestCredential(VaultCredential? existing, int clientId)
    {
        var dlg = new VaultCredentialDialog(Window.GetWindow(this)!, existing);
        if (dlg.ShowDialog() == true && dlg.Result is not null)
            _vm.ConfirmCredentialDialog(
                dlg.Result.Label, dlg.Result.Username, dlg.Result.Password,
                dlg.Result.Extra, dlg.Result.Notes, clientId, existing?.Id);
    }

    private void OnRequestLink(VaultLink? existing, int clientId)
    {
        var dlg = new VaultLinkDialog(Window.GetWindow(this)!, existing);
        if (dlg.ShowDialog() == true && dlg.Result is not null)
            _vm.ConfirmLinkDialog(
                dlg.Result.EnvName, dlg.Result.Url, dlg.Result.Username,
                dlg.Result.Password, dlg.Result.Notes, clientId, existing?.Id);
    }

    private void OnRequestConfirm(string message, Action onConfirm)
    {
        if (MessageBox.Show(message, "Confirmar", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            onConfirm();
    }

    // ── Helpers visuais ───────────────────────────────────────────────────────

    private static Border MakeBadge(string text, SolidColorBrush fg) => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(0x25, fg.Color.R, fg.Color.G, fg.Color.B)),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(6, 2, 6, 2),
        Margin = new Thickness(0, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = fg,
        },
    };

    private static Button IconBtn(string icon, SolidColorBrush fg, Action onClick) => new()
    {
        Content = icon,
        Background = Brushes.Transparent,
        Foreground = fg,
        BorderThickness = new Thickness(0),
        FontSize = 13,
        Padding = new Thickness(5, 3, 5, 3),
        Cursor = Cursors.Hand,
        Command = new RelayCmd(onClick),
    };

    private static TextBlock EmptyHint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x2E, 0x40)),
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 40, 0, 0),
        TextWrapping = TextWrapping.Wrap,
    };

    private static SolidColorBrush B(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    private static Color ParseColor(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);

    private static void TryOpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private class RelayCmd(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? _) => true;
        public void Execute(object? _) => action();
    }
}