using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SqlUI.App.ViewModels;

public enum NodeKind { Server, Database, Folder, Table, View, StoredProcedure, Column }

public partial class ObjectExplorerNodeViewModel : ObservableObject
{
    public string Text { get; }
    public NodeKind Kind { get; }
    public string? Database { get; }   // bu node'un ait oldugu veritabani (tablo/kolon sorgulari icin)
    public string? Owner { get; }      // tablo node'lari icin: parent tablo adi (kolon node'lari kullanir)

    public ObservableCollection<ObjectExplorerNodeViewModel> Children { get; } = new();

    private readonly Func<Task<List<ObjectExplorerNodeViewModel>>>? _loader;
    private bool _loaded;

    [ObservableProperty] private bool isExpanded;

    // Segoe Fluent Icons / Segoe MDL2 Assets glyph kodlari (bkz. MainWindow.xaml'de bu font
    // aileleriyle render edilen Icon TextBlock'u). Emoji yerine bunlar kullaniliyor cunku:
    // 1) Emoji renkli/bitmap glyphlerdir, TextBlock.Foreground'u (dolayisiyla koyu/acik temayi)
    //    yok sayar - koyu temada garip/tutarsiz gorunmesinin sebebi buydu.
    // 2) Bu font ailelerindeki her kod noktasinin gercekten var oldugu, bir .NET GlyphTypeface
    //    testiyle (CharacterToGlyphMap) dogrulandi - "tofu" kutucuk riski yok.
    public string Icon => Kind switch
    {
        NodeKind.Server => "",
        NodeKind.Database => "",
        NodeKind.Folder => "",
        NodeKind.Table => "",
        NodeKind.View => "",
        NodeKind.StoredProcedure => "",
        NodeKind.Column => "",
        _ => "",
    };

    // SSMS'teki gibi ikon turune gore renklendirme - "canli"/modern bir gorunum icin. Emoji
    // olmadigindan (monokrom Segoe glyph) renk tamamen bizim kontrolumuzde secili.
    public Brush IconColor => Kind switch
    {
        NodeKind.Server => Brushes.SlateGray,
        NodeKind.Database => new SolidColorBrush(Color.FromRgb(0xCC, 0x9A, 0x1E)),
        NodeKind.Folder => new SolidColorBrush(Color.FromRgb(0xDC, 0xB6, 0x2B)),
        NodeKind.Table => new SolidColorBrush(Color.FromRgb(0x3E, 0x8E, 0xDE)),
        NodeKind.View => new SolidColorBrush(Color.FromRgb(0x3C, 0xB3, 0x8C)),
        NodeKind.StoredProcedure => new SolidColorBrush(Color.FromRgb(0xB0, 0x7A, 0xE0)),
        NodeKind.Column => Brushes.Gray,
        _ => Brushes.Gray,
    };

    public ObjectExplorerNodeViewModel(string text, NodeKind kind, string? database = null, string? owner = null,
        Func<Task<List<ObjectExplorerNodeViewModel>>>? loader = null)
    {
        Text = text; Kind = kind; Database = database; Owner = owner; _loader = loader;
        if (loader != null) Children.Add(new ObjectExplorerNodeViewModel("...", NodeKind.Folder));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value) _ = EnsureLoadedAsync();
    }

    [RelayCommand]
    public async Task EnsureLoadedAsync()
    {
        if (_loaded || _loader == null) return;
        _loaded = true;
        try
        {
            var kids = await _loader();
            Children.Clear();
            foreach (var k in kids) Children.Add(k);
        }
        catch (Exception ex)
        {
            Children.Clear();
            Children.Add(new ObjectExplorerNodeViewModel("Hata: " + ex.Message, NodeKind.Folder));
        }
    }

    // Sag-tik menusundeki "Yenile" icin: verimizi tekrar sunucudan ceker (Children direkt
    // guncellenir, ObservableCollection oldugu icin genisletilmis durumdaysa aninda yansir).
    [RelayCommand]
    public async Task RefreshAsync()
    {
        _loaded = false;
        await EnsureLoadedAsync();
    }
}
