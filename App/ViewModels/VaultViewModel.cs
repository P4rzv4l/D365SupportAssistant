using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Vault;
using D365Assistant.Core.Services;
using System.Collections.ObjectModel;

namespace D365Assistant.ViewModels;

public partial class VaultViewModel : ObservableObject
{
    private readonly VaultService _vault;

    // ── Estado geral ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isUnlocked = false;
    [ObservableProperty] private string _lockLabel = "🔒  Bloqueado";
    [ObservableProperty] private string _lockColor = "#C0392B";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _hasClients = false;

    // ── Cliente selecionado ───────────────────────────────────────────────────
    [ObservableProperty] private VaultClient? _selectedClient;
    [ObservableProperty] private string _activeTab = "Credentials";

    // ── Coleções ──────────────────────────────────────────────────────────────
    public ObservableCollection<VaultClient> Clients { get; } = [];
    public ObservableCollection<VaultCredential> Credentials { get; } = [];
    public ObservableCollection<VaultLink> Links { get; } = [];

    // ── Eventos para a View abrir diálogos ───────────────────────────────────
    public event Action<bool>? RequestUnlockDialog;
    public event Action<VaultClient?>? RequestClientDialog;
    public event Action<VaultCredential?, int>? RequestCredentialDialog;
    public event Action<VaultLink?, int>? RequestLinkDialog;
    public event Action<string, Action>? RequestConfirmDialog;

    // ── Presets ───────────────────────────────────────────────────────────────
    public static readonly string[] ClientColors =
        ["#1A6CF5", "#7C3AED", "#0D9488", "#D97706", "#DC2626", "#0891B2", "#65A30D"];

    public static readonly string[] EnvPresets = ["PRD", "HML", "DEV", "UAT", "Suporte", "Outro"];
    public static readonly string[] LabelPresets = ["Usuário de Acesso", "Usuário de Serviço", "Administrador", "API Key", "Banco de Dados", "Outro"];

    public VaultViewModel(VaultService vault)
    {
        _vault = vault;
        UpdateLockState();
    }

    // ── Lock / Unlock ─────────────────────────────────────────────────────────

    [RelayCommand]
    public void ToggleLock()
    {
        if (_vault.IsUnlocked)
        {
            _vault.Lock();
            UpdateLockState();
            SelectedClient = null;
            Clients.Clear();
            Credentials.Clear();
            Links.Clear();
        }
        else
        {
            RequestUnlockDialog?.Invoke(!_vault.HasMaster);
        }
    }

    public void PerformUnlock(string password, bool isSetup)
    {
        try
        {
            if (isSetup)
                _vault.SetupMaster(password);
            else
                _vault.Unlock(password);

            UpdateLockState();
            RefreshClients();
            StatusText = "";
        }
        catch (WrongPasswordException)
        {
            StatusText = "⚠ Senha incorreta.";
        }
        catch (Exception ex)
        {
            StatusText = $"⚠ Erro: {ex.Message}";
        }
    }

    // ── Clientes ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public void AddClient() => RequestClientDialog?.Invoke(null);

    [RelayCommand]
    public void EditClient() => RequestClientDialog?.Invoke(SelectedClient);

    [RelayCommand]
    public void DeleteClient()
    {
        if (SelectedClient is null) return;
        var name = SelectedClient.Name;
        var id = SelectedClient.Id;

        RequestConfirmDialog?.Invoke(
            $"Excluir '{name}' e TODAS as suas credenciais e links?\n\nEssa ação não pode ser desfeita.",
            () =>
            {
                _vault.DeleteClient(id);
                SelectedClient = null;
                Credentials.Clear();
                Links.Clear();
                RefreshClients();
            });
    }

    public void ConfirmClientDialog(string name, string crmUrl, string notes, string color, int? existingId)
    {
        if (existingId.HasValue)
            _vault.UpdateClient(existingId.Value, name, crmUrl, notes, color);
        else
            _vault.AddClient(name, crmUrl, notes, color);

        RefreshClients();

        if (existingId.HasValue)
        {
            var updated = _vault.GetClient(existingId.Value);
            if (updated is not null) SelectClient(updated);
        }
    }

    public void SelectClient(VaultClient client)
    {
        SelectedClient = client;
        RefreshCredentials();
        RefreshLinks();
        ActiveTab = "Credentials";
    }

    // ── Credenciais ───────────────────────────────────────────────────────────

    [RelayCommand]
    public void AddCredential()
    {
        if (SelectedClient is null) return;
        RequestCredentialDialog?.Invoke(null, SelectedClient.Id);
    }

    [RelayCommand]
    public void EditCredential(VaultCredential? cred)
    {
        if (cred is null || SelectedClient is null) return;
        RequestCredentialDialog?.Invoke(cred, SelectedClient.Id);
    }

    [RelayCommand]
    public void DeleteCredential(VaultCredential? cred)
    {
        if (cred is null) return;
        RequestConfirmDialog?.Invoke("Excluir esta credencial?", () =>
        {
            _vault.DeleteCredential(cred.Id);
            RefreshCredentials();
        });
    }

    public void ConfirmCredentialDialog(string label, string username, string password,
                                        string extra, string notes, int clientId, int? existingId)
    {
        if (existingId.HasValue)
            _vault.UpdateCredential(existingId.Value, label, username, password, extra, notes);
        else
            _vault.AddCredential(clientId, label, username, password, extra, notes);

        RefreshCredentials();
    }

    // ── Links ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    public void AddLink()
    {
        if (SelectedClient is null) return;
        RequestLinkDialog?.Invoke(null, SelectedClient.Id);
    }

    [RelayCommand]
    public void EditLink(VaultLink? link)
    {
        if (link is null || SelectedClient is null) return;
        RequestLinkDialog?.Invoke(link, SelectedClient.Id);
    }

    [RelayCommand]
    public void DeleteLink(VaultLink? link)
    {
        if (link is null) return;
        RequestConfirmDialog?.Invoke("Excluir este link?", () =>
        {
            _vault.DeleteLink(link.Id);
            RefreshLinks();
        });
    }

    public void ConfirmLinkDialog(string envName, string url, string username,
                                  string password, string notes, int clientId, int? existingId)
    {
        if (existingId.HasValue)
            _vault.UpdateLink(existingId.Value, envName, url, username, password, notes);
        else
            _vault.AddLink(clientId, envName, url, username, password, notes);

        RefreshLinks();
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    public void RefreshClients()
    {
        Clients.Clear();
        if (!_vault.IsUnlocked) return;

        var q = SearchText.Trim().ToLower();
        var all = _vault.ListClients();
        var filtered = string.IsNullOrEmpty(q)
            ? all
            : all.Where(c => c.Name.ToLower().Contains(q)).ToList();

        foreach (var c in filtered) Clients.Add(c);
        HasClients = Clients.Count > 0;
    }

    public void RefreshCredentials()
    {
        Credentials.Clear();
        if (SelectedClient is null || !_vault.IsUnlocked) return;
        foreach (var c in _vault.GetCredentials(SelectedClient.Id))
            Credentials.Add(c);
    }

    public void RefreshLinks()
    {
        Links.Clear();
        if (SelectedClient is null || !_vault.IsUnlocked) return;
        foreach (var l in _vault.GetLinks(SelectedClient.Id))
            Links.Add(l);
    }

    partial void OnSearchTextChanged(string _) => RefreshClients();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateLockState()
    {
        IsUnlocked = _vault.IsUnlocked;
        LockLabel = _vault.IsUnlocked ? "🔓  Desbloqueado" : "🔒  Bloqueado";
        LockColor = _vault.IsUnlocked ? "#1A8A3E" : "#C0392B";
    }
}