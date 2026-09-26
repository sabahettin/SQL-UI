# SqlUI'yi tek dosyalik bir exe olarak yayinlar.
#
# Varsayilan: framework-bagimli (framework-dependent) yayin - .NET 10 Desktop Runtime'in
# hedef makinede kurulu olmasini gerektirir ama exe boyutu ~20MB'dir (SSMS'in 600MB'ina
# kiyasla). Bu makinede .NET 10 SDK zaten kurulu oldugu icin varsayilan budur.
#
# Kendi kendine yetebilen (self-contained, .NET kurulumu gerektirmeyen) bir yayin icin
# -SelfContained anahtarini kullanin; boyut ~190MB'a cikar (WPF'in native bilesenleri
# nedeniyle - trimleme WPF'te tam desteklenmedigi icin kapali birakildi).
#
# Kullanim:
#   .\publish.ps1                  # framework-dependent, publish-fd\ klasorune
#   .\publish.ps1 -SelfContained    # self-contained, publish\ klasorune

param(
    [switch]$SelfContained
)

if ($SelfContained) { $outDir = "publish"; $selfContainedArg = "true" } else { $outDir = "publish-fd"; $selfContainedArg = "false" }
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

dotnet publish src/SqlUI.App/SqlUI.App.csproj `
    -c Release -r win-x64 `
    --self-contained $selfContainedArg `
    -p:PublishSingleFile=true `
    -o $outDir

Write-Host ""
Write-Host "Yayinlandi: $outDir\SqlUI.App.exe"
$size = (Get-ChildItem $outDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host ("Toplam boyut: {0:N1} MB" -f $size)
