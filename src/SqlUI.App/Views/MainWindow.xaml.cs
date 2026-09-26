using System.Windows;
using System.Windows.Input;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using SqlUI.App.Services;
using SqlUI.App.ViewModels;
using SqlUI.Core.IntelliSense;

namespace SqlUI.App.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly AppSettingsStore _settingsStore;
    private static IHighlightingDefinition? _sqlHighlighting;

    public MainWindow(MainWindowViewModel viewModel, AppSettingsStore settingsStore)
    {
        _viewModel = viewModel;
        _settingsStore = settingsStore;
        InitializeComponent();
        DataContext = _viewModel;

        var settings = settingsStore.Load();
        if (settings.WindowWidth > 200 && settings.WindowHeight > 200)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        Closing += MainWindow_Closing;
    }

    private static IHighlightingDefinition LoadSqlHighlighting()
    {
        if (_sqlHighlighting != null) return _sqlHighlighting;
        var uri = new Uri("pack://application:,,,/Resources/SQL.xshd");
        using var stream = Application.GetResourceStream(uri)!.Stream;
        using var reader = new XmlTextReader(stream);
        _sqlHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        return _sqlHighlighting;
    }

    // Her sorgu sekmesi "kendi" AvalonEdit TextEditor'unu tasiyor GIBI gorunur, ama WPF'in
    // TabControl+ItemsSource+ContentTemplate ikilisi SADECE SECILI sekmenin icerigini render eder
    // ve TEK BIR gorsel agaci (ve dolayisiyla TEK BIR TextEditor ornegini) sekmeler arasinda
    // YENIDEN KULLANIR - sadece DataContext'i degistirir. Bu yuzden Editor_Loaded (bir Loaded
    // event'i) sadece ILK sekme icin bir kez tetiklenir; sonraki her "Yeni Sorgu" veya sekme
    // degistirme, ayni editor'un DataContext'ini sessizce degistirir ama editor.Text ESKI
    // sekmenin metnini gostermeye devam eder - "yeni sorgu, once acik olan sekmenin kopyasi
    // gibi goruntuleniyor" seklinde bildirilen hatanin kok nedeni buydu. Cozum: DataContext
    // her degistiginde editor.Text'i YENIDEN senkronize ediyoruz (sadece ilk yuklemede degil).
    private void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        var editor = (TextEditor)sender;
        editor.SyntaxHighlighting = LoadSqlHighlighting();

        bool suppressTextChanged = false;

        void SyncEditorFromTab()
        {
            if (editor.DataContext is not QueryTabViewModel t) return;
            suppressTextChanged = true;
            if (editor.Text != t.QueryText) editor.Text = t.QueryText;
            editor.CaretOffset = Math.Min(editor.CaretOffset, editor.Text.Length);
            suppressTextChanged = false;
        }

        SyncEditorFromTab();
        editor.DataContextChanged += (_, _) => SyncEditorFromTab();

        editor.TextChanged += (_, _) =>
        {
            if (suppressTextChanged) return;
            if (editor.DataContext is not QueryTabViewModel tab) return;
            tab.QueryText = editor.Text;
            tab.IsDirty = true;
        };

        CompletionWindow? completionWindow = null;
        editor.TextArea.TextEntered += (_, args) =>
        {
            // Her tus vurusunda onceki pencereyi kapatip taze veriyle yeniden aciyoruz. AvalonEdit'in
            // kendi ic filtrelemesine guvenmek yerine bunu yapmamizin sebebi: CompletionWindow'un
            // StartOffset'ten caret'e kadar olan metni kendi filtresi icin kullanmasi, bizim kelime
            // siniri tanimimizla (bosluk/virgul/parantez) tam ortusmuyor - ozellikle SQL anahtar
            // kelimesi yazilirken (orn. "from") bir tablo/kolon adiyla harf eslesirse pencere acilip
            // sonraki bosluk/karakterlerde duzgun kapanmayabiliyor, bos gorunen "asili" bir pencere
            // biraklabiliyordu. Her seferinde kapat + yeniden ac bu sinif hatayi tamamen ortadan kaldirir.
            completionWindow?.Close();
            completionWindow = null;

            string word = SqlIntelliSenseEngine.GetWordBeforeCursor(editor.Text, editor.CaretOffset);
            if (word.Length < 2) return;

            int plainWordStart = editor.CaretOffset - word.Length;
            string textBeforeWord = editor.Text[..plainWordStart];
            var suggestions = _viewModel.GetIntelliSenseSuggestions(word, textBeforeWord);
            if (suggestions.Count == 0) return;

            // Onerinin kendisiyle birebir ayniysa (kullanici zaten tam adi yazdi) pencereyi acma.
            if (suggestions.Count == 1 && suggestions[0].Equals(word, StringComparison.OrdinalIgnoreCase)) return;

            int dotIndex = word.LastIndexOf('.');
            int wordStart = editor.CaretOffset - (dotIndex >= 0 ? word.Length - dotIndex - 1 : word.Length);

            var window = new CompletionWindow(editor.TextArea) { StartOffset = wordStart };
            foreach (var s in suggestions) window.CompletionList.CompletionData.Add(new SqlCompletionData(s));
            // Pencereyi her tusta taze actigimiz icin AvalonEdit hicbir ogeyi otomatik secmiyor;
            // secim olmadan Enter/Tab hicbir sey eklemiyordu.
            window.CompletionList.SelectedItem = window.CompletionList.CompletionData[0];
            window.Closed += (_, _) => { if (completionWindow == window) completionWindow = null; };
            window.Show();
            completionWindow = window;
        };
    }

    // Sira numarasi ("#") kolonu gercek bir DB kolonu degil, grid duzenlenebilir olsa bile
    // bu kolon her zaman salt-okunur kalmali.
    private void ResultsGrid_AutoGeneratingColumn(object? sender, System.Windows.Controls.DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyName == "#") e.Column.IsReadOnly = true;
    }

    // Object Explorer'da bir veritabani (ya da altindaki bir nesne) secilince aktif veritabani
    // o olur - aksi halde sorgular toolbar'daki varsayilan "master"da calisip "Invalid object
    // name" hatasi veriyordu.
    private void ObjectExplorerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ObjectExplorerNodeViewModel { Database: { } db } && _viewModel.SupportsMultipleDatabases)
            _viewModel.SelectedDatabase = db;
    }

    private void ObjectExplorerTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node)
            _viewModel.OpenSelectTop1000(node);
    }

    // WPF TreeView sag tikta secili ogeyi otomatik degistirmez; bu yuzden tiklanan node'u
    // once elle seciyoruz ki context menu dogru node uzerinde acilsin (SSMS'teki gibi).
    private void ObjectExplorerTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d)
        {
            var item = FindAncestor<System.Windows.Controls.TreeViewItem>(d);
            if (item != null) item.IsSelected = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void MenuSelectTop1000_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) _viewModel.OpenSelectTop1000(node);
    }

    private async void MenuScriptCreate_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.ScriptCreateTableAsync(node);
    }

    private void MenuScriptSelect_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) _viewModel.ScriptSelectTable(node);
    }

    private async void MenuScriptInsert_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.ScriptInsertTableAsync(node);
    }

    private void MenuScriptDrop_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) _viewModel.ScriptDropTable(node);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settingsStore) { Owner = this };
        dlg.ShowDialog();
    }

    private void MenuNewQuery_Click(object sender, RoutedEventArgs e) => _viewModel.NewQueryCommand.Execute(null);

    private async void MenuBackupDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.BackupDatabaseAsync(node);
    }

    private async void MenuRestoreDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.RestoreDatabaseAsync(node);
    }

    private async void MenuRenameDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.RenameDatabaseAsync(node);
    }

    private async void MenuDeleteDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.DeleteDatabaseAsync(node);
    }

    private async void MenuDeleteTable_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.DeleteTableAsync(node);
    }

    private async void MenuModifySp_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.ModifyStoredProcedureAsync(node);
    }

    private async void MenuExecuteSp_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.ExecuteStoredProcedureAsync(node);
    }

    private async void MenuDeleteSp_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await _viewModel.DeleteStoredProcedureAsync(node);
    }

    private async void MenuRefreshNode_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectExplorerTree.SelectedItem is ObjectExplorerNodeViewModel node) await node.RefreshAsync();
    }

    private async void NewDatabase_Click(object sender, RoutedEventArgs e) => await _viewModel.NewDatabaseAsync();

    private void TabMenuSave_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is QueryTabViewModel tab) _viewModel.SaveQueryCommand.Execute(tab);
    }

    private void TabMenuSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is QueryTabViewModel tab) _viewModel.SaveQueryAsCommand.Execute(tab);
    }

    private void TabMenuClose_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is QueryTabViewModel tab) _viewModel.CloseTabCommand.Execute(tab);
    }

    private void TabMenuCloseOthers_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is QueryTabViewModel tab) _viewModel.CloseOtherTabsCommand.Execute(tab);
    }

    private void TabMenuCloseAll_Click(object sender, RoutedEventArgs e) => _viewModel.CloseAllTabsCommand.Execute(null);

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel.HasUnsavedChanges)
        {
            var res = MessageBox.Show("Kaydedilmemiş değişiklikler var. Yine de kapatılsın mı?",
                "Kaydedilmemiş Değişiklikler", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.No) { e.Cancel = true; return; }
        }
        _viewModel.SaveSession(ActualWidth, ActualHeight);
    }
}
