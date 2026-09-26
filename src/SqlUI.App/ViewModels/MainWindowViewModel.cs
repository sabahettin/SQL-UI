using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SqlUI.App.Services;
using SqlUI.App.Views;
using SqlUI.Core;
using SqlUI.Core.IntelliSense;

namespace SqlUI.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ActiveConnection _connection;
    private readonly AppSettingsStore _settingsStore;
    private readonly SqlIntelliSenseEngine _intelliSense = new();
    private List<TableColumnSet> _intelliSenseMetadata = new();
    private int _queryCounter = 1;

    [ObservableProperty] private bool isDarkTheme;

    public ObservableCollection<QueryTabViewModel> Tabs { get; } = new();
    [ObservableProperty] private QueryTabViewModel? selectedTab;

    public ObservableCollection<ObjectExplorerNodeViewModel> RootNodes { get; } = new();

    public ObservableCollection<string> Databases { get; } = new();
    [ObservableProperty] private string? selectedDatabase;

    [ObservableProperty] private string statusText = Strings.Get("tr", "Ready");
    [ObservableProperty] private string rowCountText = string.Format(Strings.Get("tr", "RowCountFormat"), 0);
    [ObservableProperty] private string elapsedText = string.Format(Strings.Get("tr", "ElapsedFormat"), "00:00:00");

    private static string Loc(string key) => LocalizationService.Instance[key];
    [ObservableProperty] private bool isBusy;

    public string WindowTitle => $"SqlUI - {_connection.ServerLabel}";
    public bool SupportsMultipleDatabases => _connection.Provider.Capabilities.HasFlag(DbProviderCapabilities.MultipleDatabases);

    public MainWindowViewModel(ActiveConnection connection, AppSettingsStore settingsStore)
    {
        _connection = connection;
        _settingsStore = settingsStore;
        isDarkTheme = _settingsStore.Load().Theme == ThemeService.Dark;

        CreateNewTab();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (SupportsMultipleDatabases)
            {
                var dbs = await _connection.Provider.GetDatabasesAsync(_connection.ConnectionString);
                foreach (var db in dbs) Databases.Add(db);
                // SelectedDatabase atamasi OnSelectedDatabaseChanged -> LoadIntelliSenseMetadataAsync'i
                // zaten tetikliyor; burada ayrica cagirirsak coklu-db saglayicilarda ayni anda iki
                // istek yarisir (biri digerini gecebilir).
                SelectedDatabase = _connection.Database ?? Databases.FirstOrDefault(d => d.Equals("master", StringComparison.OrdinalIgnoreCase)) ?? Databases.FirstOrDefault();
            }
            else
            {
                _ = LoadIntelliSenseMetadataAsync();
            }
            await LoadObjectExplorerAsync();
        }
        catch (Exception ex)
        {
            StatusText = "Object Explorer yüklenemedi: " + ex.Message;
        }

        await RestoreSessionFilesAsync();
    }

    private async Task RestoreSessionFilesAsync()
    {
        foreach (var path in _settingsStore.Load().LastOpenFiles)
        {
            try
            {
                if (!File.Exists(path)) continue;
                string content = await File.ReadAllTextAsync(path);
                Tabs.Add(new QueryTabViewModel { Title = Path.GetFileName(path), FilePath = path, QueryText = content });
            }
            catch { /* dosya silinmis/erisilemez olabilir - o sekmeyi atla */ }
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ThemeService.Apply(IsDarkTheme ? ThemeService.Dark : ThemeService.Light);
    }

    public void SaveSession(double windowWidth, double windowHeight)
    {
        // Once var olan ayarlari yukleyip sadece bu metodun sorumlu oldugu alanlari guncelliyoruz;
        // aksi halde Ayarlar penceresinden degistirilen Dil/Font gibi degerler burada sifirlanirdi.
        var settings = _settingsStore.Load();
        settings.Theme = IsDarkTheme ? ThemeService.Dark : ThemeService.Light;
        settings.WindowWidth = windowWidth;
        settings.WindowHeight = windowHeight;
        settings.LastOpenFiles = Tabs.Where(t => t.FilePath != null).Select(t => t.FilePath!).ToList();
        _settingsStore.Save(settings);
    }

    partial void OnSelectedDatabaseChanged(string? value)
    {
        if (value == null) return;
        _connection.Database = value;
        _primaryKeyCache.Clear();
        _ = LoadIntelliSenseMetadataAsync();
    }

    private int _intelliSenseLoadToken;

    private async Task LoadIntelliSenseMetadataAsync()
    {
        int token = ++_intelliSenseLoadToken;
        try
        {
            var columns = await _connection.Provider.GetAllColumnsAsync(_connection.ConnectionString, _connection.Database);
            if (token != _intelliSenseLoadToken) return; // bu arada baska bir yukleme (db degisimi) basladi, eski sonucu at

            var grouped = columns
                .GroupBy(c => c.TableName ?? "")
                .Select(g =>
                {
                    var set = new TableColumnSet { TableName = g.Key };
                    set.Columns.AddRange(g.Select(c => c.Name));
                    return set;
                })
                .ToList();
            _intelliSenseMetadata = grouped;
        }
        catch
        {
            // IntelliSense metadata yuklenemezse sessizce vazgec - sorgu calistirmayi engellemez.
            if (token == _intelliSenseLoadToken) _intelliSenseMetadata = new List<TableColumnSet>();
        }
    }

    public List<string> GetIntelliSenseSuggestions(string wordBeforeCursor, string textBeforeWord)
        => _intelliSense.GetSuggestions(wordBeforeCursor, textBeforeWord, _intelliSenseMetadata);

    // SQL Server'a ozel sistem veritabanlari; ayri bir klasorde gruplanarak kullanici
    // veritabanlarinin listede kaybolmasi engellenir (SSMS'in de yaptigi gibi).
    private static readonly HashSet<string> SystemDatabaseNames =
        new(StringComparer.OrdinalIgnoreCase) { "master", "model", "msdb", "tempdb" };

    private async Task LoadObjectExplorerAsync()
    {
        RootNodes.Clear();
        var provider = _connection.Provider;

        if (SupportsMultipleDatabases)
        {
            var dbs = await provider.GetDatabasesAsync(_connection.ConnectionString);
            var userDbs = dbs.Where(d => !SystemDatabaseNames.Contains(d)).ToList();
            var systemDbs = dbs.Where(d => SystemDatabaseNames.Contains(d)).ToList();

            foreach (var db in userDbs)
                RootNodes.Add(new ObjectExplorerNodeViewModel(db, NodeKind.Database, db, loader: () => LoadDatabaseFoldersAsync(db)));

            if (systemDbs.Count > 0)
            {
                RootNodes.Add(new ObjectExplorerNodeViewModel("System Databases", NodeKind.Folder, loader: () =>
                    Task.FromResult(systemDbs
                        .Select(db => new ObjectExplorerNodeViewModel(db, NodeKind.Database, db, loader: () => LoadDatabaseFoldersAsync(db)))
                        .ToList())));
            }
        }
        else
        {
            foreach (var node in await LoadDatabaseFoldersAsync(null)) RootNodes.Add(node);
        }
    }

    private async Task<List<ObjectExplorerNodeViewModel>> LoadDatabaseFoldersAsync(string? db)
    {
        var provider = _connection.Provider;
        var folders = new List<ObjectExplorerNodeViewModel>
        {
            new("Tablolar", NodeKind.Folder, db, loader: () => LoadTablesAsync(db, isView: false)),
            new("Görünümler", NodeKind.Folder, db, loader: () => LoadTablesAsync(db, isView: true)),
        };
        if (provider.Capabilities.HasFlag(DbProviderCapabilities.StoredProcedures))
            folders.Add(new ObjectExplorerNodeViewModel("Saklanan Yordamlar", NodeKind.Folder, db, loader: () => LoadProceduresAsync(db)));
        return await Task.FromResult(folders);
    }

    private async Task<List<ObjectExplorerNodeViewModel>> LoadTablesAsync(string? db, bool isView)
    {
        var provider = _connection.Provider;
        var items = isView ? await provider.GetViewsAsync(_connection.ConnectionString, db)
                            : await provider.GetTablesAsync(_connection.ConnectionString, db);
        return items.Select(t => new ObjectExplorerNodeViewModel(t.QualifiedName, isView ? NodeKind.View : NodeKind.Table, db, owner: t.Name,
            loader: () => LoadColumnsAsync(db, t.Name))).ToList();
    }

    private async Task<List<ObjectExplorerNodeViewModel>> LoadColumnsAsync(string? db, string table)
    {
        var cols = await _connection.Provider.GetColumnsAsync(_connection.ConnectionString, db, table);
        return cols.Select(c => new ObjectExplorerNodeViewModel(
            $"{c.Name} ({c.DataType}{(c.MaxLength is > 0 ? $"({c.MaxLength})" : "")}, {(c.IsNullable ? "null" : "not null")}{(c.IsPrimaryKey ? ", PK" : "")}{(c.IsIdentity ? ", IDENTITY" : "")})",
            NodeKind.Column)).ToList();
    }

    private async Task<List<ObjectExplorerNodeViewModel>> LoadProceduresAsync(string? db)
    {
        var procs = await _connection.Provider.GetStoredProceduresAsync(_connection.ConnectionString, db);
        return procs.Select(p => new ObjectExplorerNodeViewModel(p, NodeKind.StoredProcedure, db)).ToList();
    }

    [RelayCommand]
    private void NewQuery() => CreateNewTab();

    private QueryTabViewModel CreateNewTab()
    {
        var tab = new QueryTabViewModel { Title = $"SQLQuery{_queryCounter++}.sql" };
        Tabs.Add(tab);
        SelectedTab = tab;
        return tab;
    }

    [RelayCommand]
    private void CloseTab(QueryTabViewModel? tab)
    {
        tab ??= SelectedTab;
        if (tab == null) return;
        if (tab.IsDirty)
        {
            var res = MessageBox.Show($"'{tab.Title}' için kaydedilmemiş değişiklikler var. Kaydedilsin mi?",
                "Kaydedilmemiş Değişiklikler", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Cancel) return;
            if (res == MessageBoxResult.Yes) SaveQuery(tab);
        }
        Tabs.Remove(tab);
    }

    [RelayCommand]
    private void CloseAllTabs()
    {
        foreach (var t in Tabs.ToList()) CloseTab(t);
    }

    [RelayCommand]
    private void CloseOtherTabs(QueryTabViewModel? keep)
    {
        keep ??= SelectedTab;
        if (keep == null) return;
        foreach (var t in Tabs.Where(t => t != keep).ToList()) CloseTab(t);
    }

    public void OpenSelectTop1000(ObjectExplorerNodeViewModel node)
    {
        if (node.Kind != NodeKind.Table && node.Kind != NodeKind.View || node.Owner == null) return;
        if (SupportsMultipleDatabases && node.Database != null) SelectedDatabase = node.Database;

        var tab = CreateNewTab();
        tab.QueryText = _connection.Provider.BuildSelectTopQuery(node.Owner, 1000);
        tab.PendingEditTable = node.Owner;
        _ = ExecuteAsync();
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        var tab = SelectedTab;
        if (tab == null || string.IsNullOrWhiteSpace(tab.QueryText)) return;

        StatusText = Loc("Executing");
        tab.IsExecuting = true;
        tab.Cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _connection.Provider.ExecuteQueryAsync(_connection.ConnectionString, _connection.Database, tab.QueryText, tab.Cts.Token);
            sw.Stop();

            tab.ResultSets.Clear();
            foreach (var dt in result.ResultSets)
            {
                var rs = new ResultSetViewModel(dt);
                if (result.ResultSets.Count == 1 && tab.PendingEditTable != null)
                {
                    rs.EditableTable = tab.PendingEditTable;
                    string tableName = tab.PendingEditTable;
                    rs.EnableEditing((row, col, val) => CommitCellEditAsync(tableName, row, col, val));
                }
                tab.ResultSets.Add(rs);
            }
            tab.MessagesText = string.Join(Environment.NewLine, result.Messages);
            tab.SelectedResultsTabIndex = result.ResultSets.Count == 0 ? 1 : 0;

            int totalRows = result.ResultSets.Sum(t => t.Rows.Count);
            StatusText = Loc("QueryCompleted");
            RowCountText = string.Format(Loc("RowCountFormat"), totalRows);
            ElapsedText = string.Format(Loc("ElapsedFormat"), sw.Elapsed.ToString(@"hh\:mm\:ss"));
        }
        catch (OperationCanceledException)
        {
            StatusText = Loc("QueryCancelled");
            tab.MessagesText = Loc("QueryCancelledByUser");
            tab.SelectedResultsTabIndex = 1;
        }
        catch (Exception ex)
        {
            tab.MessagesText = ex.Message;
            tab.SelectedResultsTabIndex = 1;
            StatusText = Loc("ErrorOccurred");
        }
        finally
        {
            tab.IsExecuting = false;
            tab.Cts?.Dispose(); tab.Cts = null;
            tab.PendingEditTable = null;
        }
    }

    [RelayCommand]
    private void CancelQuery() => SelectedTab?.Cts?.Cancel();

    public bool SupportsExecutionPlan => _connection.Provider.Capabilities.HasFlag(DbProviderCapabilities.ExecutionPlan);

    [RelayCommand]
    private async Task ShowEstimatedPlanAsync()
    {
        var tab = SelectedTab;
        if (tab == null || !SupportsExecutionPlan || string.IsNullOrWhiteSpace(tab.QueryText)) return;

        StatusText = "Yürütme planı alınıyor...";
        try
        {
            string? xml = await _connection.Provider.GetEstimatedPlanXmlAsync(_connection.ConnectionString, _connection.Database, tab.QueryText);
            tab.ExecutionPlan.Clear();
            foreach (var node in Core.ExecutionPlan.ShowPlanXmlParser.Parse(xml)) tab.ExecutionPlan.Add(node);
            tab.SelectedResultsTabIndex = 2;
            StatusText = "Yürütme planı hazır.";
        }
        catch (Exception ex)
        {
            StatusText = "Yürütme planı alınamadı: " + ex.Message;
        }
    }

    private readonly Dictionary<string, string?> _primaryKeyCache = new(StringComparer.OrdinalIgnoreCase);

    private async Task CommitCellEditAsync(string table, DataRow row, string columnName, object? newValue)
    {
        if (columnName == "#") return; // sira numarasi kolonu, gercek bir DB kolonu degil

        try
        {
            if (!_primaryKeyCache.TryGetValue(table, out string? pkColumn))
            {
                var columns = await _connection.Provider.GetColumnsAsync(_connection.ConnectionString, _connection.Database, table);
                pkColumn = columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name;
                _primaryKeyCache[table] = pkColumn;
            }
            if (pkColumn == null) { StatusText = $"'{table}' için birincil anahtar bulunamadı, düzenleme kaydedilemedi."; return; }
            if (columnName == pkColumn) return; // birincil anahtarin kendisi duzenlenemez

            object? pkValue = row[pkColumn];
            await _connection.Provider.UpdateCellAsync(_connection.ConnectionString, _connection.Database, table, columnName, newValue, pkColumn, pkValue);
            StatusText = Loc("DataUpdated");
        }
        catch (Exception ex)
        {
            StatusText = "Güncelleme hatası: " + ex.Message;
        }
    }

    public async Task ScriptCreateTableAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Owner == null) return;
        var columns = await _connection.Provider.GetColumnsAsync(_connection.ConnectionString, node.Database, node.Owner);
        OpenScriptTab(Core.Scripting.SqlScriptGenerator.BuildCreateTable(_connection.Provider, node.Owner, columns));
    }

    public void ScriptSelectTable(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Owner == null) return;
        OpenScriptTab(Core.Scripting.SqlScriptGenerator.BuildSelect(_connection.Provider, node.Owner));
    }

    public async Task ScriptInsertTableAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Owner == null) return;
        var columns = await _connection.Provider.GetColumnsAsync(_connection.ConnectionString, node.Database, node.Owner);
        OpenScriptTab(Core.Scripting.SqlScriptGenerator.BuildInsert(_connection.Provider, node.Owner, columns));
    }

    public void ScriptDropTable(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Owner == null) return;
        OpenScriptTab(Core.Scripting.SqlScriptGenerator.BuildDrop(_connection.Provider, node.Owner));
    }

    private void OpenScriptTab(string script)
    {
        var tab = CreateNewTab();
        tab.QueryText = script;
    }

    [RelayCommand]
    private void ExportToExcel(ResultSetViewModel? rs)
    {
        rs ??= SelectedTab?.ResultSets.FirstOrDefault();
        if (rs == null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook();
            wb.Worksheets.Add(rs.Table, "Results").Columns().AdjustToContents();
            wb.SaveAs(dlg.FileName);
            StatusText = "Excel'e aktarıldı: " + dlg.FileName;
        }
        catch (Exception ex)
        {
            StatusText = "Dışa aktarma hatası: " + ex.Message;
        }
    }

    [RelayCommand]
    private void SaveQuery(QueryTabViewModel? tab = null)
    {
        tab ??= SelectedTab;
        if (tab == null) return;
        if (tab.FilePath == null) { SaveQueryAs(tab); return; }
        File.WriteAllText(tab.FilePath, tab.QueryText);
        tab.IsDirty = false;
    }

    [RelayCommand]
    private void SaveQueryAs(QueryTabViewModel? tab = null)
    {
        tab ??= SelectedTab;
        if (tab == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SQL dosyaları|*.sql|Tüm dosyalar|*.*",
            FileName = tab.FilePath != null ? Path.GetFileName(tab.FilePath) : tab.Title
        };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, tab.QueryText);
            tab.FilePath = dlg.FileName;
            tab.Title = Path.GetFileName(dlg.FileName);
            tab.IsDirty = false;
        }
    }

    [RelayCommand]
    private async Task OpenQueryFileAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "SQL dosyaları|*.sql|Tüm dosyalar|*.*" };
        if (dlg.ShowDialog() == true)
        {
            string content = await File.ReadAllTextAsync(dlg.FileName);
            var tab = new QueryTabViewModel { Title = Path.GetFileName(dlg.FileName), FilePath = dlg.FileName, QueryText = content };
            Tabs.Add(tab);
            SelectedTab = tab;
        }
    }

    public bool HasUnsavedChanges => Tabs.Any(t => t.IsDirty);

    // --- Object Explorer sag-tik islemleri: veritabani/tablo/saklanan yordam yonetimi ---
    // (yalnizca ilgili Capabilities bayragi isaretliyken anlamli; UI da buna gore gosterir/gizler)

    public bool SupportsDatabaseManagement => _connection.Provider.Capabilities.HasFlag(DbProviderCapabilities.DatabaseManagement);

    public async Task NewDatabaseAsync()
    {
        if (!SupportsDatabaseManagement) return;
        string? name = PromptDialog.Show(Application.Current.MainWindow, "Yeni Veritabanı", "Veritabanı adı:");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            await _connection.Provider.CreateDatabaseAsync(_connection.ConnectionString, name);
            await LoadObjectExplorerAsync();
            StatusText = $"'{name}' veritabanı oluşturuldu.";
        }
        catch (Exception ex) { StatusText = "Hata: " + ex.Message; }
    }

    public async Task RenameDatabaseAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Kind != NodeKind.Database || !SupportsDatabaseManagement) return;
        string? newName = PromptDialog.Show(Application.Current.MainWindow, "Veritabanını Yeniden Adlandır", "Yeni ad:", node.Text);
        if (string.IsNullOrWhiteSpace(newName) || newName == node.Text) return;
        try
        {
            await _connection.Provider.RenameDatabaseAsync(_connection.ConnectionString, node.Text, newName);
            await LoadObjectExplorerAsync();
            StatusText = "Veritabanı yeniden adlandırıldı.";
        }
        catch (Exception ex) { StatusText = "Hata: " + ex.Message; }
    }

    public async Task DeleteDatabaseAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Kind != NodeKind.Database || !SupportsDatabaseManagement) return;
        var res = MessageBox.Show($"'{node.Text}' veritabanı KALICI OLARAK silinecek. Emin misiniz?",
            "Veritabanını Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;
        try
        {
            await _connection.Provider.DeleteDatabaseAsync(_connection.ConnectionString, node.Text);
            await LoadObjectExplorerAsync();
            StatusText = "Veritabanı silindi.";
        }
        catch (Exception ex) { StatusText = "Hata: " + ex.Message; }
    }

    // "Cannot open backup device" / "Erisim engellendi" hatasi neredeyse her zaman ayni sebepten
    // olur: BACKUP/RESTORE dosya yolunu SQL SERVER SURECI acar, bu makineyi kullanan kisi degil.
    // Kullanici kendi Temp/Belgelerim klasorunu secerse SQL Server servis hesabinin o klasore
    // yazma izni olmayabilir (yerel gelistirme kurulumlarinda bile sikca yasanir). Gercek
    // sunucuda dogrulanan bir durum - kullaniciya nedenini aciklayan bir ipucu ekliyoruz.
    private static string FormatBackupRestoreError(Exception ex)
    {
        string msg = ex.Message;
        if (msg.Contains("Cannot open backup device") || msg.Contains("Erişim engellendi") || msg.Contains("Access is denied"))
        {
            return msg + "\n\nİpucu: Yedekleme/geri yükleme işlemini SQL Server SERVİSİ kendi izinleriyle " +
                   "yapar (bu programı çalıştıran kullanıcı değil). Genellikle SQL Server'ın kendi Backup " +
                   "klasörüne (örn. C:\\Program Files\\Microsoft SQL Server\\MSSQLxx.MSSQLSERVER\\MSSQL\\Backup\\) " +
                   "yazma izni vardır; kendi Belgelerim/Masaüstü klasörünüze yazma izni olmayabilir.";
        }
        return msg;
    }

    public async Task BackupDatabaseAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Kind != NodeKind.Database) return;
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Yedek dosyası|*.bak", FileName = $"{node.Text}.bak" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            StatusText = "Yedekleniyor...";
            await _connection.Provider.BackupDatabaseAsync(_connection.ConnectionString, node.Text, dlg.FileName);
            StatusText = "Yedekleme tamamlandı: " + dlg.FileName;
        }
        catch (Exception ex)
        {
            StatusText = "Yedekleme hatası.";
            MessageBox.Show(FormatBackupRestoreError(ex), "Yedekleme Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task RestoreDatabaseAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Kind != NodeKind.Database) return;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Yedek dosyası|*.bak" };
        if (dlg.ShowDialog() != true) return;
        var res = MessageBox.Show($"'{node.Text}' üzerine geri yükleme yapılacak, mevcut veriler kaybolacak. Emin misiniz?",
            "Geri Yükle", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;
        try
        {
            StatusText = "Geri yükleniyor...";
            await _connection.Provider.RestoreDatabaseAsync(_connection.ConnectionString, node.Text, dlg.FileName);
            StatusText = "Geri yükleme tamamlandı.";
        }
        catch (Exception ex)
        {
            StatusText = "Geri yükleme hatası.";
            MessageBox.Show(FormatBackupRestoreError(ex), "Geri Yükleme Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task DeleteTableAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Owner == null || (node.Kind != NodeKind.Table && node.Kind != NodeKind.View)) return;
        var res = MessageBox.Show($"'{node.Owner}' KALICI OLARAK silinecek. Emin misiniz?",
            "Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;
        try
        {
            await _connection.Provider.DeleteTableAsync(_connection.ConnectionString, node.Database, node.Owner);
            await LoadObjectExplorerAsync();
            StatusText = "Silindi.";
        }
        catch (Exception ex) { StatusText = "Hata: " + ex.Message; }
    }

    public async Task ModifyStoredProcedureAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Kind != NodeKind.StoredProcedure) return;
        try
        {
            string? def = await _connection.Provider.GetStoredProcedureDefinitionAsync(_connection.ConnectionString, node.Database, node.Text);
            if (string.IsNullOrEmpty(def)) { StatusText = "Tanım bulunamadı."; return; }
            CreateNewTab().QueryText = def;
        }
        catch (Exception ex) { StatusText = "Hata: " + ex.Message; }
    }

    public async Task ExecuteStoredProcedureAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Kind != NodeKind.StoredProcedure) return;
        try
        {
            var parameters = await _connection.Provider.GetStoredProcedureParametersAsync(_connection.ConnectionString, node.Database, node.Text);
            string quoted = _connection.Provider.QuoteIdentifier(node.Text);
            string script;
            var inputParams = parameters.Where(p => !p.IsOutput).ToList();
            if (inputParams.Count == 0)
                script = $"EXEC {quoted}";
            else
                script = $"EXEC {quoted}\n    " + string.Join(",\n    ", inputParams.Select(p => $"{p.Name} = <deger> -- {p.DataType}"));
            CreateNewTab().QueryText = script;
        }
        catch (Exception ex) { StatusText = "Hata: " + ex.Message; }
    }

    public async Task DeleteStoredProcedureAsync(ObjectExplorerNodeViewModel? node)
    {
        if (node?.Kind != NodeKind.StoredProcedure) return;
        var res = MessageBox.Show($"'{node.Text}' KALICI OLARAK silinecek. Emin misiniz?",
            "Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;
        try
        {
            await _connection.Provider.DeleteStoredProcedureAsync(_connection.ConnectionString, node.Database, node.Text);
            await LoadObjectExplorerAsync();
            StatusText = "Silindi.";
        }
        catch (Exception ex) { StatusText = "Hata: " + ex.Message; }
    }

    public async Task RefreshObjectExplorerAsync() => await LoadObjectExplorerAsync();
}
