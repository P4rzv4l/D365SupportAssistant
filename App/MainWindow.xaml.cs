// =============================================================================
//  MainWindow.xaml.cs — Code-behind da janela principal
// =============================================================================

using D365Assistant.Core.Services;
using D365Assistant.ViewModels;
using D365Assistant.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace D365Assistant;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly MainViewModel _vm;
    private readonly DashboardViewModel _dashVm;
    private readonly AlertsViewModel _alertsVm;
    private readonly TrackerViewModel _trackerVm;
    private readonly TrackerHistoryViewModel _trackerHistoryVm;
    private readonly AIViewModel _aiVm;
    private readonly IncidentsViewModel _incidentsVm;
    private readonly WebResourcesViewModel _webResourcesVm;
    private readonly TodoViewModel _todoVm;
    private readonly IDataverseService _dataverse;
    private string _userName = "Carregando...";
    public string UserName
    {
        get => _userName;
        set
        {
            _userName = value;
            OnPropertyChanged();
        }
    }

    private Button? _activeNavBtn;
    private readonly Dictionary<string, Page> _pages = [];

    public MainWindow(
        MainViewModel vm,
        DashboardViewModel dashVm,
        AlertsViewModel alertsVm,
        TrackerViewModel trackerVm,
        TrackerHistoryViewModel trackerHistoryVm,
        AIViewModel aiVm,
        IncidentsViewModel incidentsVm,
        WebResourcesViewModel webResourcesVm,
        TodoViewModel todoVm,
        IDataverseService dataverse)
    {
        InitializeComponent();

        // Define ícone via caminho absoluto (evita case-sensitivity do pack URI)
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "App", "Assets", "logo-roxo.ico");
            if (System.IO.File.Exists(iconPath))
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
        }
        catch { }

        DataContext = this;
        _vm = vm;
        _dashVm = dashVm;
        _alertsVm = alertsVm;
        _trackerVm = trackerVm;
        _trackerHistoryVm = trackerHistoryVm;
        _aiVm = aiVm;
        _incidentsVm = incidentsVm;
        _webResourcesVm = webResourcesVm;
        _todoVm = todoVm;

        _vm.DataRefreshed += OnDataRefreshed;
        _vm.MonitorError += OnMonitorError;

        var auth = App.Services.GetRequiredService<IAuthService>();
        auth.DeviceCodeRequired += OnDeviceCodeRequired;

        NavigateTo("Dashboard", BtnDashboard);

        // Inicia monitoramento após a janela aparecer
        Loaded += async (_, _) =>
        {
            await _vm.StartMonitoringAsync();
            await LoadUserAsync();
        };

        var uiTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        uiTimer.Tick += (_, _) => UpdateTopBar();
        uiTimer.Start();

        _dataverse = dataverse;
    }

    // ── Navegação ─────────────────────────────────────────────────────────────

    private void NavBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            NavigateTo(tag, btn);
    }

    private void NavigateTo(string pageName, Button? btn)
    {
        if (!_pages.TryGetValue(pageName, out var page))
        {
            page = pageName switch
            {
                "Dashboard" => new DashboardView(_dashVm, _trackerVm, this),
                "Incidents" => new IncidentsView(_incidentsVm),
                "Tracker" => new TrackerView(_trackerVm, _trackerHistoryVm),
                "TrackerHistory" => new TrackerHistoryView(_trackerHistoryVm),
                "Alerts" => new AlertsView(_alertsVm),
                "AI" => new AIView(_aiVm),
                "Tools" => new ToolsView(_webResourcesVm),
                "Todo" => new TodoView(_todoVm),
                "Vault" => new VaultView(
                    App.Services.GetRequiredService<VaultViewModel>()),
                "Settings" => new SettingsView(
                    App.Services.GetRequiredService<SettingsViewModel>()),
                _ => new DashboardView(_dashVm, _trackerVm, this),
            };
            _pages[pageName] = page;
        }

        MainFrame.Navigate(page);

        if (_activeNavBtn != null)
            _activeNavBtn.Style = (Style)FindResource("SidebarBtn");

        if (btn != null)
        {
            btn.Style = (Style)FindResource("SidebarBtnActive");
            _activeNavBtn = btn;
        }
    }

    // ── TopBar ────────────────────────────────────────────────────────────────

    private void UpdateTopBar()
    {
        ClockText.Text = _vm.ClockText;
        NextCycleText.Text = _vm.NextCycleText;
        StatusText.Text = _vm.StatusText;
        StatusBarText.Text = _vm.StatusBarText;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(_vm.StatusDotColor);
            StatusDot.Fill = new SolidColorBrush(color);
        }
        catch { }

        if (_vm.AlertBadgeCount > 0)
        {
            AlertBadge.Visibility = Visibility.Visible;
            AlertBadgeCount.Text = _vm.AlertBadgeCount > 99 ? "99+" : _vm.AlertBadgeCount.ToString();
        }
        else
        {
            AlertBadge.Visibility = Visibility.Collapsed;
        }

        if (_trackerVm.IsRunning && !string.IsNullOrEmpty(_trackerVm.ActiveTicket))
        {
            TimerStatusPanel.Visibility = Visibility.Visible;
            TimerTicketText.Text = _trackerVm.ActiveTicket;
            TimerElapsedText.Text = _trackerVm.TimerDisplay;
        }
        else
        {
            TimerStatusPanel.Visibility = Visibility.Collapsed;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Device Flow ───────────────────────────────────────────────────────────

    private void OnDeviceCodeRequired(object? sender, DeviceCodeEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(e.VerificationUrl)
                { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning("Não foi possível abrir o browser: {Error}", ex.Message);
        }

        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                $"Login necessário com sua conta Microsoft.\n\n" +
                $"O browser foi aberto automaticamente.\n\n" +
                $"Digite este código na página:\n\n" +
                $"          {e.UserCode}\n\n" +
                $"(URL: {e.VerificationUrl})",
                "Autenticação — D365 Support Assistant",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    // ── Eventos do Orchestrator ───────────────────────────────────────────────

    private void OnDataRefreshed(object? sender, CycleCompletedEventArgs e)
    {
        _dashVm.UpdateData(e.Snapshots, e.Snapshots.Count(s =>
            (DateTime.UtcNow - s.FirstSeenAt.ToUniversalTime()).TotalHours < 24));
        _incidentsVm.UpdateData(e.Snapshots);
        if (e.Alerts.Count > 0)
            _alertsVm.AddAlerts(e.Alerts);
    }

    private void OnMonitorError(object? sender, string error)
    {
        if (error.Contains("401") || error.Contains("AADSTS") ||
            error.Contains("Unauthorized") || error.Contains("token"))
        {
            MessageBox.Show(
                $"Erro de autenticação com o Dynamics 365:\n\n{error}\n\n" +
                "Verifique:\n" +
                "• Configurações no appsettings.json\n" +
                "• Delete o cache de token em Configurações",
                "Erro de Autenticação",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    // ── Acesso público para as Views ──────────────────────────────────────────

    public void QuickStartTimer(string ticketId, string title)
    {
        _trackerVm.TicketInput = ticketId;
        _trackerVm.StartCommand.Execute(null);
        NavigateTo("Tracker", BtnTracker);
    }

    public void OpenAIForTicket(string ticketId)
    {
        _aiVm.TicketInput = ticketId;
        NavigateTo("AI", BtnAI);
        _aiVm.AnalyzeCommand.Execute(null);
    }

    // ── Fechar ────────────────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _vm.StopMonitoring();
        Log.Information("D365 Assistant encerrado.");
    }

    private async Task LoadUserAsync()
    {
        try
        {
            var who = await _dataverse.WhoAmIAsync();
            var fullName = await _dataverse.GetUserFullNameAsync(who.UserId);

            UserName = string.IsNullOrEmpty(fullName)
                ? "Usuário"
                : fullName;
        }
        catch (Exception ex)
        {
            Log.Warning("Erro ao carregar usuário: {Error}", ex.Message);
            UserName = "Usuário";
        }
    }

    private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
    {

    }
}