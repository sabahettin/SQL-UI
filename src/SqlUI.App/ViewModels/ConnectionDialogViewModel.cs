using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SqlUI.App.Services;
using SqlUI.Core;
using SqlUI.Core.Models;

namespace SqlUI.App.ViewModels;

public partial class ConnectionDialogViewModel : ObservableObject
{
    private readonly ConnectionProfileStore _store;
    private readonly ActiveConnection _activeConnection;

    public ObservableCollection<IDbProvider> Providers { get; }

    [ObservableProperty] private IDbProvider selectedProvider;
    [ObservableProperty] private string server = @".\SQLEXPRESS";
    [ObservableProperty] private string? database;
    [ObservableProperty] private bool integratedSecurity = true;
    [ObservableProperty] private string? userName;
    [ObservableProperty] private string? password;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isConnecting;

    public ObservableCollection<ConnectionProfile> RecentConnections { get; } = new();
    [ObservableProperty] private ConnectionProfile? selectedRecent;

    public bool ShowServerAuthFields => SelectedProvider?.Capabilities.HasFlag(DbProviderCapabilities.ServerAuthentication) ?? false;
    public bool IsFileBased => SelectedProvider?.Key == "sqlite";

    public event Action<bool?>? RequestClose;

    public ConnectionDialogViewModel(IEnumerable<IDbProvider> providers, ConnectionProfileStore store, ActiveConnection activeConnection)
    {
        _store = store;
        _activeConnection = activeConnection;
        Providers = new ObservableCollection<IDbProvider>(providers);
        selectedProvider = Providers.First();

        foreach (var p in _store.Load()) RecentConnections.Add(p);
    }

    partial void OnSelectedProviderChanged(IDbProvider value)
    {
        OnPropertyChanged(nameof(ShowServerAuthFields));
        OnPropertyChanged(nameof(IsFileBased));
    }

    partial void OnSelectedRecentChanged(ConnectionProfile? value)
    {
        if (value is null) return;
        var provider = Providers.FirstOrDefault(p => p.Key == value.ProviderKey);
        if (provider != null) SelectedProvider = provider;
        Server = value.Server;
        Database = value.Database;
        IntegratedSecurity = value.IntegratedSecurity;
        UserName = value.UserName;
        Password = value.EncryptedSecret != null ? ConnectionProfileStore.DecryptSecret(value) : null;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        IsConnecting = true;
        StatusMessage = "Bağlanılıyor...";
        try
        {
            var profile = new ConnectionProfile
            {
                ProviderKey = SelectedProvider.Key,
                Server = Server,
                Database = string.IsNullOrWhiteSpace(Database) ? null : Database,
                IntegratedSecurity = IntegratedSecurity,
                UserName = IntegratedSecurity ? null : UserName,
            };
            string cs = SelectedProvider.BuildConnectionString(profile, IntegratedSecurity ? null : Password);
            await SelectedProvider.TestConnectionAsync(cs);

            _activeConnection.Provider = SelectedProvider;
            _activeConnection.ConnectionString = cs;
            _activeConnection.Database = profile.Database;
            _activeConnection.ServerLabel = $"{SelectedProvider.DisplayName} - {Server}";

            _store.SaveRecent(profile, IntegratedSecurity ? null : Password);
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Bağlantı hatası: " + ex.Message;
        }
        finally { IsConnecting = false; }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    [RelayCommand]
    private void BrowseFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "SQLite veritabanı|*.db;*.sqlite;*.sqlite3|Tüm dosyalar|*.*" };
        if (dlg.ShowDialog() == true) Server = dlg.FileName;
    }

    [RelayCommand]
    private void DeleteRecent(ConnectionProfile? profile)
    {
        profile ??= SelectedRecent;
        if (profile == null) return;
        _store.Delete(profile.Id);
        RecentConnections.Remove(profile);
        if (SelectedRecent == profile) SelectedRecent = null;
    }
}
