using System.ComponentModel;

namespace SqlUI.App.Services;

// XAML'de indexer binding'i ile kullanilir: Text="{Binding Source={x:Static services:LocalizationService.Instance}, Path=[SomeKey]}".
// PropertyChanged(null) (indexer adi bos) WPF'e "Item[]" ile baslayan TUM binding'leri yeniden
// degerlendirmesini soyler - bu, standart bir WPF calisma-zamani dil degistirme desenidir,
// ozel bir MarkupExtension yazmaya gerek birakmaz.
public class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _language = "tr";
    public string Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    public string this[string key] => Strings.Get(_language, key);
}
