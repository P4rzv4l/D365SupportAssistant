// =============================================================================
//  App/Views/VaultDialogs.xaml.cs — Diálogos modais do Cofre
//  Alterações:
//    • Layouts com dois campos por linha (Grid 2 colunas) onde faz sentido
//    • Botão "Copiar senha" inline nos diálogos de credencial e ambiente
//    • Estilos carregados do ResourceDictionary VaultDialogs.xaml
// =============================================================================

using D365Assistant.Core.Models.Vault;
using D365Assistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace D365Assistant.Views;

// ── Helpers compartilhados ────────────────────────────────────────────────────
file static class DH
{
    // ResourceDictionary definido inline — sem dependência de URI/arquivo externo
    private static ResourceDictionary? _res;
    public static ResourceDictionary Res => _res ??= BuildRes();

    private static ResourceDictionary BuildRes()
    {
        var bg = B("#0B1018");
        var bgDeep = B("#06090E");
        var border = B("#0D1825");
        var textPri = B("#C8DCF0");
        var textSec = B("#3D4E63");
        var blue = B("#1A6CF5");
        var amber = B("#D4830A");

        Style Window() => new Style(typeof(Window))
        {
            Setters =
            {
                new Setter(System.Windows.Window.BackgroundProperty, bg),
                new Setter(System.Windows.Window.FontFamilyProperty, new FontFamily("Segoe UI")),
                new Setter(System.Windows.Window.ResizeModeProperty, ResizeMode.NoResize),
            }
        };

        Style DlgTitle() => new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.FontSizeProperty,   15.0),
                new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold),
                new Setter(TextBlock.ForegroundProperty, textPri),
                new Setter(TextBlock.MarginProperty,     new Thickness(0, 0, 0, 18)),
            }
        };

        Style FieldLabel() => new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.FontSizeProperty,   11.0),
                new Setter(TextBlock.ForegroundProperty, textSec),
                new Setter(TextBlock.MarginProperty,     new Thickness(0, 0, 0, 3)),
            }
        };

        Style DlgField() => new Style(typeof(TextBox))
        {
            Setters =
            {
                new Setter(TextBox.BackgroundProperty,      bgDeep),
                new Setter(TextBox.ForegroundProperty,      Brushes.White),
                new Setter(TextBox.BorderBrushProperty,     border),
                new Setter(TextBox.BorderThicknessProperty, new Thickness(1)),
                new Setter(TextBox.FontSizeProperty,        12.0),
                new Setter(TextBox.PaddingProperty,         new Thickness(10, 7, 10, 7)),
                new Setter(TextBox.MarginProperty,          new Thickness(0, 0, 0, 12)),
                new Setter(TextBox.CaretBrushProperty,      blue),
            }
        };

        Style DlgPassword() => new Style(typeof(PasswordBox))
        {
            Setters =
            {
                new Setter(PasswordBox.BackgroundProperty,      bgDeep),
                new Setter(PasswordBox.ForegroundProperty,      Brushes.White),
                new Setter(PasswordBox.BorderBrushProperty,     border),
                new Setter(PasswordBox.BorderThicknessProperty, new Thickness(1)),
                new Setter(PasswordBox.FontSizeProperty,        12.0),
                new Setter(PasswordBox.PaddingProperty,         new Thickness(10, 7, 10, 7)),
                new Setter(PasswordBox.MarginProperty,          new Thickness(0, 0, 0, 12)),
            }
        };

        Style DlgCombo() => new Style(typeof(ComboBox))
        {
            Setters =
            {
                new Setter(ComboBox.BackgroundProperty,      bgDeep),
                new Setter(ComboBox.ForegroundProperty,      Brushes.White),
                new Setter(ComboBox.BorderBrushProperty,     border),
                new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)),
                new Setter(ComboBox.FontSizeProperty,        12.0),
                new Setter(ComboBox.PaddingProperty,         new Thickness(8, 6, 8, 6)),
                new Setter(ComboBox.MarginProperty,          new Thickness(0, 0, 0, 12)),
            }
        };

        Style DlgBtn() => new Style(typeof(Button))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty,      B("#0F2A5C")),
                new Setter(Button.ForegroundProperty,      B("#5A9FE0")),
                new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                new Setter(Button.FontSizeProperty,        12.0),
                new Setter(Button.FontFamilyProperty,      new FontFamily("Segoe UI Semibold")),
                new Setter(Button.PaddingProperty,         new Thickness(20, 9, 20, 9)),
                new Setter(Button.CursorProperty,          Cursors.Hand),
            }
        };

        Style DlgBtnCancel() => new Style(typeof(Button), DlgBtn())
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, B("#12171F")),
                new Setter(Button.ForegroundProperty, B("#3D4E63")),
            }
        };

        Style DlgSep() => new Style(typeof(Border))
        {
            Setters =
            {
                new Setter(Border.HeightProperty,     1.0),
                new Setter(Border.BackgroundProperty, border),
                new Setter(Border.MarginProperty,     new Thickness(0, 4, 0, 16)),
            }
        };

        Style DlgWarning() => new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.FontSizeProperty,    10.0),
                new Setter(TextBlock.ForegroundProperty,  amber),
                new Setter(TextBlock.MarginProperty,      new Thickness(0, 4, 0, 12)),
                new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap),
            }
        };

        var rd = new ResourceDictionary();
        rd["DlgWindow"] = Window();
        rd["DlgTitle"] = DlgTitle();
        rd["FieldLabel"] = FieldLabel();
        rd["DlgField"] = DlgField();
        rd["DlgPassword"] = DlgPassword();
        rd["DlgCombo"] = DlgCombo();
        rd["DlgBtn"] = DlgBtn();
        rd["DlgBtnCancel"] = DlgBtnCancel();
        rd["DlgSep"] = DlgSep();
        rd["DlgWarning"] = DlgWarning();
        return rd;
    }

    public static SolidColorBrush B(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    // Aplica o estilo do dicionário a um elemento
    public static T Styled<T>(T elem, string key) where T : FrameworkElement
    {
        if (Res.Contains(key))
            elem.Style = (Style)Res[key];
        return elem;
    }

    public static TextBlock Title(string text)
        => Styled(new TextBlock { Text = text }, "DlgTitle");

    public static TextBlock FieldLabel(string text)
        => Styled(new TextBlock { Text = text }, "FieldLabel");

    public static Border Sep()
        => Styled(new Border(), "DlgSep");

    public static TextBlock Warning(string text)
        => Styled(new TextBlock { Text = text }, "DlgWarning");

    public static TextBox Field(string value = "")
        => Styled(new TextBox { Text = value, CaretBrush = B("#1A6CF5") }, "DlgField");

    public static PasswordBox PwdBox()
        => Styled(new PasswordBox(), "DlgPassword");

    public static ComboBox Combo(IEnumerable<string> items, string selected = "")
    {
        var cb = Styled(new ComboBox
        {
            IsEditable = true,
            Text = selected,
            ItemsSource = items,
        }, "DlgCombo");
        return cb;
    }

    public static Button Btn(string label, bool cancel = false)
        => Styled(new Button { Content = label, Cursor = Cursors.Hand },
                  cancel ? "DlgBtnCancel" : "DlgBtn");

    // Linha de dois campos lado a lado (proporção configurável)
    public static Grid TwoCol(
        string lbl1, UIElement f1,
        string lbl2, UIElement f2,
        double w1 = double.NaN, double w2 = double.NaN)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition
        { Width = double.IsNaN(w1) ? new GridLength(1, GridUnitType.Star) : new GridLength(w1) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        g.ColumnDefinitions.Add(new ColumnDefinition
        { Width = double.IsNaN(w2) ? new GridLength(1, GridUnitType.Star) : new GridLength(w2) });

        var sp1 = new StackPanel();
        sp1.Children.Add(FieldLabel(lbl1));
        sp1.Children.Add(f1);
        Grid.SetColumn(sp1, 0);
        g.Children.Add(sp1);

        var sp2 = new StackPanel();
        sp2.Children.Add(FieldLabel(lbl2));
        sp2.Children.Add(f2);
        Grid.SetColumn(sp2, 2);
        g.Children.Add(sp2);

        return g;
    }

    // Campo + botão de copiar, inline (para senha dentro do diálogo)
    public static (PasswordBox PwdBox, UIElement Row) PwdWithCopy()
    {
        var pb = PwdBox();
        var feedback = new TextBlock
        {
            Text = "",
            FontSize = 10,
            Foreground = B("#22C55E"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };

        var copyBtn = new Button
        {
            Content = "📋",
            Background = Brushes.Transparent,
            Foreground = B("#3D4E63"),
            BorderThickness = new Thickness(0),
            FontSize = 13,
            Padding = new Thickness(4, 0, 4, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Copiar senha",
        };
        copyBtn.Click += (_, _) =>
        {
            if (pb.Password.Length > 0)
            {
                Clipboard.SetText(pb.Password);
                feedback.Text = "✓ Copiado";
                var t = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (_, _) => { feedback.Text = ""; t.Stop(); };
                t.Start();
            }
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        pb.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetColumn(pb, 0);
        row.Children.Add(pb);

        var btnWrap = new Border { Margin = new Thickness(6, 0, 0, 12), VerticalAlignment = VerticalAlignment.Center };
        btnWrap.Child = copyBtn;
        Grid.SetColumn(btnWrap, 1);
        row.Children.Add(btnWrap);

        var fbWrap = new Border { Margin = new Thickness(0, 0, 0, 12), VerticalAlignment = VerticalAlignment.Center };
        fbWrap.Child = feedback;
        Grid.SetColumn(fbWrap, 2);
        row.Children.Add(fbWrap);

        return (pb, row);
    }

    public static StackPanel BtnRow(params Button[] btns)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        foreach (var b in btns)
        {
            b.Margin = new Thickness(6, 0, 0, 0);
            row.Children.Add(b);
        }
        return row;
    }
}

// =============================================================================
//  Unlock / Setup
// =============================================================================

public class VaultUnlockDialog : Window
{
    public string? Password { get; private set; }

    private readonly PasswordBox _pwdBox;
    private readonly PasswordBox? _pwd2Box;

    public VaultUnlockDialog(Window owner, bool isSetup)
    {
        Owner = owner;
        Style = (Style)DH.Res["DlgWindow"];
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Title = isSetup ? "Configurar Vault" : "Desbloquear Vault";
        Width = 420;
        Height = isSetup ? 290 : 215;

        var stack = new StackPanel { Margin = new Thickness(32, 28, 32, 28) };
        stack.Children.Add(DH.Title(isSetup ? "🔐  Criar senha mestre" : "🔒  Desbloquear Vault"));
        stack.Children.Add(DH.Sep());

        stack.Children.Add(DH.FieldLabel("Senha mestre"));
        _pwdBox = DH.PwdBox();
        stack.Children.Add(_pwdBox);

        if (isSetup)
        {
            stack.Children.Add(DH.FieldLabel("Confirmar senha"));
            _pwd2Box = DH.PwdBox();
            stack.Children.Add(_pwd2Box);
            stack.Children.Add(DH.Warning("⚠  Anote bem a senha — não há recuperação possível."));
        }

        var okBtn = DH.Btn(isSetup ? "Criar" : "Desbloquear");
        var cancelBtn = DH.Btn("Cancelar", cancel: true);

        okBtn.Click += (_, _) =>
        {
            if (isSetup && _pwdBox.Password != _pwd2Box!.Password)
            {
                MessageBox.Show("As senhas não coincidem.", "Vault",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_pwdBox.Password.Length < 4)
            {
                MessageBox.Show("Mínimo de 4 caracteres.", "Vault",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Password = _pwdBox.Password;
            DialogResult = true;
        };
        cancelBtn.Click += (_, _) => DialogResult = false;

        _pwdBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                okBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };

        stack.Children.Add(DH.BtnRow(cancelBtn, okBtn));
        Content = stack;
    }
}

// =============================================================================
//  Cliente
// =============================================================================

public class VaultClientDialog : Window
{
    public record ClientResult(string Name, string CrmUrl, string Notes, string Color);
    public ClientResult? Result { get; private set; }

    private readonly TextBox _nameBox, _urlBox, _notesBox;
    private string _selectedColor;

    public VaultClientDialog(Window owner, VaultClient? existing)
    {
        Owner = owner;
        Style = (Style)DH.Res["DlgWindow"];
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Title = existing is null ? "Novo Cliente" : "Editar Cliente";
        Width = 480;
        Height = 340;

        _selectedColor = existing?.Color ?? "#1A6CF5";

        var stack = new StackPanel { Margin = new Thickness(32, 28, 32, 28) };
        stack.Children.Add(DH.Title(existing is null ? "➕  Novo cliente" : "✏  Editar cliente"));
        stack.Children.Add(DH.Sep());

        // Nome (linha única — campo obrigatório em destaque)
        stack.Children.Add(DH.FieldLabel("Nome *"));
        _nameBox = DH.Field(existing?.Name ?? "");
        stack.Children.Add(_nameBox);

        // URL + Seletor de cor lado a lado
        _urlBox = DH.Field(existing?.CrmUrl ?? "");
        _notesBox = DH.Field(existing?.Notes ?? "");

        stack.Children.Add(DH.TwoCol("URL do CRM", _urlBox, "Observações", _notesBox));

        // Seletor de cor
        var colorRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 18),
        };
        colorRow.Children.Add(new TextBlock
        {
            Text = "Cor do cliente:",
            FontSize = 11,
            Foreground = DH.B("#3D4E63"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });

        foreach (var hex in VaultViewModel.ClientColors)
        {
            var h = hex;
            var dot = new Border
            {
                Width = 22,
                Height = 22,
                Background = DH.B(h),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(h == _selectedColor ? 2 : 0),
                BorderBrush = Brushes.White,
            };
            dot.MouseLeftButtonUp += (_, _) =>
            {
                _selectedColor = h;
                foreach (var child in colorRow.Children.OfType<Border>())
                {
                    var bg = ((SolidColorBrush)child.Background).Color.ToString();
                    child.BorderThickness = new Thickness(
                        string.Equals(bg,
                            ((SolidColorBrush)DH.B(h)).Color.ToString(),
                            StringComparison.OrdinalIgnoreCase) ? 2 : 0);
                }
            };
            colorRow.Children.Add(dot);
        }
        stack.Children.Add(colorRow);

        var okBtn = DH.Btn("Salvar");
        var cancelBtn = DH.Btn("Cancelar", cancel: true);

        okBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Nome obrigatório.", "Cliente");
                return;
            }
            Result = new(_nameBox.Text.Trim(), _urlBox.Text.Trim(),
                         _notesBox.Text.Trim(), _selectedColor);
            DialogResult = true;
        };
        cancelBtn.Click += (_, _) => DialogResult = false;

        stack.Children.Add(DH.BtnRow(cancelBtn, okBtn));
        Content = stack;
    }
}

// =============================================================================
//  Credencial
// =============================================================================

public class VaultCredentialDialog : Window
{
    public record CredResult(
        string Label, string Username, string Password,
        string Extra, string Notes);

    public CredResult? Result { get; private set; }

    private readonly ComboBox _labelBox;
    private readonly TextBox _userBox, _extraBox, _notesBox;
    private readonly PasswordBox _pwdBox;

    public VaultCredentialDialog(Window owner, VaultCredential? existing)
    {
        Owner = owner;
        Style = (Style)DH.Res["DlgWindow"];
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Title = existing is null ? "Nova Credencial" : "Editar Credencial";
        Width = 500;
        Height = 420;

        var stack = new StackPanel { Margin = new Thickness(32, 28, 32, 28) };
        stack.Children.Add(DH.Title(existing is null ? "🔑  Nova credencial" : "✏  Editar credencial"));
        stack.Children.Add(DH.Sep());

        // Rótulo
        stack.Children.Add(DH.FieldLabel("Tipo / Rótulo *"));
        _labelBox = DH.Combo(VaultViewModel.LabelPresets, existing?.Label ?? "");
        stack.Children.Add(_labelBox);

        // Usuário (linha inteira)
        stack.Children.Add(DH.FieldLabel("Usuário"));
        _userBox = DH.Field(existing?.Username ?? "");
        stack.Children.Add(_userBox);

        // Senha com botão de copiar
        stack.Children.Add(DH.FieldLabel("Senha"));
        var (pb, pwdRow) = DH.PwdWithCopy();
        _pwdBox = pb;
        if (existing is not null) _pwdBox.Password = existing.Password;
        stack.Children.Add(pwdRow);

        // Extra + Notas lado a lado
        _extraBox = DH.Field(existing?.Extra ?? "");
        _notesBox = DH.Field(existing?.Notes ?? "");
        stack.Children.Add(DH.TwoCol("Extra", _extraBox, "Notas", _notesBox));

        var okBtn = DH.Btn("Salvar");
        var cancelBtn = DH.Btn("Cancelar", cancel: true);

        okBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_labelBox.Text))
            {
                MessageBox.Show("Rótulo obrigatório.");
                return;
            }
            Result = new(
                _labelBox.Text.Trim(), _userBox.Text,
                _pwdBox.Password, _extraBox.Text, _notesBox.Text);
            DialogResult = true;
        };
        cancelBtn.Click += (_, _) => DialogResult = false;

        stack.Children.Add(DH.BtnRow(cancelBtn, okBtn));
        Content = stack;
    }
}

// =============================================================================
//  Link / Ambiente
// =============================================================================

public class VaultLinkDialog : Window
{
    public record LinkResult(string EnvName, string Url, string Username, string Password, string Notes);
    public LinkResult? Result { get; private set; }

    private readonly ComboBox _envBox;
    private readonly TextBox _urlBox, _userBox, _notesBox;
    private readonly PasswordBox _pwdBox;

    public VaultLinkDialog(Window owner, VaultLink? existing)
    {
        Owner = owner;
        Style = (Style)DH.Res["DlgWindow"];
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Title = existing is null ? "Novo Ambiente" : "Editar Ambiente";
        Width = 480;
        Height = 380;

        var stack = new StackPanel { Margin = new Thickness(32, 28, 32, 28) };
        stack.Children.Add(DH.Title(existing is null ? "🌐  Novo ambiente" : "✏  Editar ambiente"));
        stack.Children.Add(DH.Sep());

        // Ambiente + URL lado a lado
        _envBox = DH.Combo(VaultViewModel.EnvPresets, existing?.EnvName ?? "PRD");
        _urlBox = DH.Field(existing?.Url ?? "https://");
        stack.Children.Add(DH.TwoCol("Ambiente *", _envBox, "URL *", _urlBox, 130));

        // Usuário + Senha lado a lado
        _userBox = DH.Field(existing?.Username ?? "");
        var (pb, pwdRow) = DH.PwdWithCopy();
        _pwdBox = pb;
        if (existing is not null) _pwdBox.Password = existing.Password;

        // Usuário
        stack.Children.Add(DH.FieldLabel("Usuário"));
        stack.Children.Add(_userBox);

        // Senha com copiar
        stack.Children.Add(DH.FieldLabel("Senha"));
        stack.Children.Add(pwdRow);

        // Notas
        stack.Children.Add(DH.FieldLabel("Notas"));
        _notesBox = DH.Field(existing?.Notes ?? "");
        stack.Children.Add(_notesBox);

        var okBtn = DH.Btn("Salvar");
        var cancelBtn = DH.Btn("Cancelar", cancel: true);

        okBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_envBox.Text) || string.IsNullOrWhiteSpace(_urlBox.Text))
            {
                MessageBox.Show("Ambiente e URL são obrigatórios.");
                return;
            }
            Result = new(_envBox.Text.Trim(), _urlBox.Text.Trim(),
                         _userBox.Text, _pwdBox.Password, _notesBox.Text);
            DialogResult = true;
        };
        cancelBtn.Click += (_, _) => DialogResult = false;

        stack.Children.Add(DH.BtnRow(cancelBtn, okBtn));
        Content = stack;
    }
}