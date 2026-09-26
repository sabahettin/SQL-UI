using CommunityToolkit.Mvvm.ComponentModel;

namespace SqlUI.App.Services;

// AvalonEdit TextEditor'un FontFamily/FontSize'ini XAML'de {Binding Source={x:Static
// services:EditorSettings.Instance}} ile bagliyoruz; degistiginde tum acik sekmeler aninda
// guncellenir (her sekme kendi TextEditor'unu Editor_Loaded'da olusturuyor olsa da, binding
// canli oldugu icin ayrica kod yazmaya gerek yok).
public partial class EditorSettings : ObservableObject
{
    public static EditorSettings Instance { get; } = new();

    [ObservableProperty] private string fontFamily = "Consolas";
    [ObservableProperty] private double fontSize = 13;
}
