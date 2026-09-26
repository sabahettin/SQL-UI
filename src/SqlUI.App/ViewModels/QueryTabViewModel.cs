using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SqlUI.Core.ExecutionPlan;

namespace SqlUI.App.ViewModels;

public partial class QueryTabViewModel : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string title = "Sorgu";
    [ObservableProperty] private string? filePath;
    [ObservableProperty] private bool isDirty;
    [ObservableProperty] private bool isExecuting;
    [ObservableProperty] private string messagesText = "";
    [ObservableProperty] private int selectedResultsTabIndex;
    [ObservableProperty] private string? pendingEditTable;

    public ObservableCollection<ResultSetViewModel> ResultSets { get; } = new();
    public ObservableCollection<PlanNode> ExecutionPlan { get; } = new();

    // AvalonEdit'in Text'i standart bir bindable DependencyProperty olmadigi icin editor
    // metnini View (code-behind) tutar; ViewModel sadece calistirilacak son metni saklar.
    public string QueryText { get; set; } = "";

    public CancellationTokenSource? Cts { get; set; }

    public string DisplayTitle => Title + (IsDirty ? "*" : "");

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(DisplayTitle));
}
