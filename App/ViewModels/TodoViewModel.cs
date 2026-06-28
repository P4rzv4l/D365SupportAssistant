// =============================================================================
//  TodoViewModel.cs — ViewModel completo de tarefas
// =============================================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Todo;
using D365Assistant.Core.Services;
using System.Collections.ObjectModel;

namespace D365Assistant.ViewModels;

public partial class TodoViewModel : ObservableObject
{
    private readonly StorageService _storage;
    private List<TodoItem> _all = [];

    // ── Filtros / search ──────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _filterGroup = "Pendentes"; // Pendentes | Hoje | Atrasadas | Concluídas | Todas
    [ObservableProperty] private string _filterCat = "Todas";

    // ── Form de edição ────────────────────────────────────────────────────────
    [ObservableProperty] private string _formTitle = "";
    [ObservableProperty] private string _formDescription = "";
    [ObservableProperty] private string _formCategory = "Geral";
    [ObservableProperty] private int _formPriority = 2;
    [ObservableProperty] private DateTime? _formDueDate = null;
    [ObservableProperty] private string? _formTicketId = null;
    [ObservableProperty] private bool _formVisible = false;
    [ObservableProperty] private bool _isEditing = false;
    [ObservableProperty] private string _formError = "";
    private int _editingId = 0;

    // ── Contadores ────────────────────────────────────────────────────────────
    [ObservableProperty] private int _countPending = 0;
    [ObservableProperty] private int _countToday = 0;
    [ObservableProperty] private int _countOverdue = 0;
    [ObservableProperty] private int _countDone = 0;

    // ── Categorias ────────────────────────────────────────────────────────────
    public ObservableCollection<string> Categories { get; } = ["Todas", "Geral", "Chamado", "Reunião", "Follow-up", "Documentação", "Outro"];

    // ── Lista visível ─────────────────────────────────────────────────────────
    public ObservableCollection<TodoItem> Items { get; } = [];

    public static readonly string[] GroupOptions = ["Pendentes", "Hoje", "Atrasadas", "Concluídas", "Todas"];
    public static readonly int[] Priorities = [1, 2, 3];

    public TodoViewModel(StorageService storage)
    {
        _storage = storage;
        Load();
    }

    // ── Load / Filter ─────────────────────────────────────────────────────────

    public void Load()
    {
        _all = _storage.GetAllTodos();
        ApplyFilter();
        UpdateCounters();
    }

    partial void OnSearchTextChanged(string _) => ApplyFilter();
    partial void OnFilterGroupChanged(string _) => ApplyFilter();
    partial void OnFilterCatChanged(string _) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = _all.AsEnumerable();

        q = FilterGroup switch
        {
            "Hoje" => q.Where(t => !t.Done && t.IsDueToday),
            "Atrasadas" => q.Where(t => t.IsOverdue),
            "Concluídas" => q.Where(t => t.Done),
            "Todas" => q,
            _ => q.Where(t => !t.Done), // Pendentes
        };

        if (FilterCat != "Todas")
            q = q.Where(t => t.Category == FilterCat);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim().ToLower();
            q = q.Where(t =>
                t.Title.ToLower().Contains(s) ||
                t.Description.ToLower().Contains(s) ||
                (t.TicketId?.ToLower().Contains(s) ?? false) ||
                t.Category.ToLower().Contains(s));
        }

        // Sort: overdue first, then by priority, then by due date
        q = FilterGroup == "Concluídas"
            ? q.OrderByDescending(t => t.DoneAt)
            : q.OrderByDescending(t => t.IsOverdue)
               .ThenBy(t => t.Priority)
               .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
               .ThenByDescending(t => t.CreatedAt);

        Items.Clear();
        foreach (var item in q)
            Items.Add(item);
    }

    private void UpdateCounters()
    {
        CountPending = _all.Count(t => !t.Done);
        CountToday = _all.Count(t => t.IsDueToday);
        CountOverdue = _all.Count(t => t.IsOverdue);
        CountDone = _all.Count(t => t.Done);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public void OpenNew()
    {
        _editingId = 0;
        IsEditing = false;
        FormTitle = "";
        FormDescription = "";
        FormCategory = "Geral";
        FormPriority = 2;
        FormDueDate = null;
        FormTicketId = null;
        FormError = "";
        FormVisible = true;
    }

    [RelayCommand]
    public void OpenEdit(TodoItem item)
    {
        _editingId = item.Id;
        IsEditing = true;
        FormTitle = item.Title;
        FormDescription = item.Description;
        FormCategory = item.Category;
        FormPriority = item.Priority;
        FormDueDate = item.DueDate;
        FormTicketId = item.TicketId;
        FormError = "";
        FormVisible = true;
    }

    [RelayCommand]
    public void CloseForm() => FormVisible = false;

    [RelayCommand]
    public void Save()
    {
        if (string.IsNullOrWhiteSpace(FormTitle))
        {
            FormError = "O título é obrigatório.";
            return;
        }

        var item = _editingId > 0
            ? _all.FirstOrDefault(t => t.Id == _editingId) ?? new TodoItem()
            : new TodoItem { CreatedAt = DateTime.Now };

        item.Title = FormTitle.Trim();
        item.Description = FormDescription.Trim();
        item.Category = FormCategory;
        item.Priority = FormPriority;
        item.DueDate = FormDueDate;
        item.TicketId = string.IsNullOrWhiteSpace(FormTicketId) ? null : FormTicketId.Trim();

        _storage.SaveTodo(item);

        if (_editingId == 0) _all.Insert(0, item);
        FormVisible = false;
        ApplyFilter();
        UpdateCounters();
    }

    [RelayCommand]
    public void Toggle(TodoItem item)
    {
        item.Done = !item.Done;
        item.DoneAt = item.Done ? DateTime.Now : null;
        _storage.ToggleTodo(item.Id, item.Done);
        ApplyFilter();
        UpdateCounters();
    }

    [RelayCommand]
    public void Delete(TodoItem item)
    {
        _storage.DeleteTodo(item.Id);
        _all.Remove(item);
        ApplyFilter();
        UpdateCounters();
    }

    [RelayCommand]
    public void ClearDone()
    {
        var done = _all.Where(t => t.Done).ToList();
        foreach (var t in done)
        {
            _storage.DeleteTodo(t.Id);
            _all.Remove(t);
        }
        ApplyFilter();
        UpdateCounters();
    }

    public void NewFromTicket(string ticketId, string title)
    {
        OpenNew();
        FormTitle = $"Follow-up: {ticketId}";
        FormCategory = "Chamado";
        FormTicketId = ticketId;
    }

    [RelayCommand]
    public void UpdateKanbanStatus(TodoItem item)
    {
        // Sincroniza Done se necessário
        if (item.Done != (_all.FirstOrDefault(t => t.Id == item.Id)?.Done ?? item.Done))
        {
            _storage.ToggleTodo(item.Id, item.Done);
        }

        // Persiste KanbanStatus + todos os campos via SaveTodo
        _storage.SaveTodo(item);

        // Atualiza o item na lista _all
        var idx = _all.FindIndex(t => t.Id == item.Id);
        if (idx >= 0) _all[idx] = item;

        ApplyFilter();
        UpdateCounters();
    }
}