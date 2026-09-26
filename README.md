# SqlUI

Hafif, çok-motorlu bir SQL istemcisi — SQL Server Management Studio'nun (SSMS) ~600MB'lık
Visual Studio kabuğu olmadan, kişisel kullanım için WPF/.NET 10 ile sıfırdan yazıldı.

## Özellikler

- **Çok-motorlu bağlantı**: SQL Server, SQLite, PostgreSQL, MySQL (`IDbProvider` soyutlaması
  üzerinden) — SQL Server ve SQLite gerçek sunucularda uçtan uca doğrulandı.
- **Object Explorer**: sunucu → veritabanı → tablo/görünüm/saklı yordam ağacı, sağ tık menüleri
  (Select Top N, script üretme, yeniden adlandırma, silme, yedekleme/geri yükleme).
- **Bağlam-farkındalıklı IntelliSense**: `FROM` sonrası sadece tablo, `SELECT`/`WHERE` sonrası
  sadece kolon önerileri; nokta operatöründen sonra ilgili tablonun kolonları.
- **Çoklu sonuç kümesi**, sonuç ızgarasında hücre düzenleme, Excel'e aktarma.
- **Script üretimi**: CREATE / SELECT / INSERT / DROP; tahmini yürütme planı görüntüleme.
- **Veritabanı yönetimi**: oluşturma, yeniden adlandırma, silme, yedekleme/geri yükleme
  (SQL Server için tam destek).
- **Koyu/açık tema**, **Türkçe/İngilizce** dil desteği, yeniden boyutlandırılabilir/responsive
  arayüz.
- **Tek-dosya, framework-dependent publish** (~22MB) ve tamamen bağımsız self-contained publish
  (~187MB, hiçbir kurulum gerektirmez) seçenekleri.

## Teknoloji Yığını

- **.NET 10 / WPF** — masaüstü arayüzü
- **CommunityToolkit.Mvvm** — MVVM (ObservableObject, RelayCommand)
- **Microsoft.Extensions.Hosting** — DI container bootstrap
- **AvalonEdit** — SQL söz dizimi vurgulamalı editör
- **ClosedXML** — Excel'e aktarma
- **Serilog** — loglama
- Sağlayıcılar: `Microsoft.Data.SqlClient`, `Microsoft.Data.Sqlite`, `Npgsql`, `MySqlConnector`

## Proje Yapısı

```
src/
  SqlUI.App/              WPF uygulaması (MVVM, Views, Services)
  SqlUI.Core/              Sağlayıcı-bağımsız soyutlamalar (IDbProvider, IntelliSense, Scripting)
  SqlUI.Data.SqlServer/    SQL Server sağlayıcısı
  SqlUI.Data.Sqlite/       SQLite sağlayıcısı
  SqlUI.Data.Postgres/     PostgreSQL sağlayıcısı
  SqlUI.Data.MySql/        MySQL/MariaDB sağlayıcısı
tests/
  SqlUI.Core.Tests/            Saf mantık testleri (script şablonları, IntelliSense, showplan ayrıştırma)
  SqlUI.Data.Sqlite.Tests/     Gerçek geçici .db dosyaları üzerinde entegrasyon testleri
publish.ps1                Tek-dosya yayınlama scripti (framework-dependent / self-contained)
```

## Derleme ve Çalıştırma

```bash
dotnet build
dotnet test
dotnet run --project src/SqlUI.App
```

Dağıtılabilir bir çıktı için:

```powershell
.\publish.ps1                  # framework-dependent, ~22MB, hedefte .NET 10 Desktop Runtime gerekir
.\publish.ps1 -SelfContained   # ~187MB, hiçbir kurulum/önkoşul gerektirmez
```

## Bilinen Sınırlamalar

- PostgreSQL ve MySQL sağlayıcıları kod olarak tamamlandı ancak gerçek bir sunucuda
  doğrulanmadı — sorun bildirilirse ayrıca giderilecektir.
- Sonuç ızgarasında kolon bazlı filtreleme henüz yok.
- Kurulum paketi (installer) yok; self-contained publish klasörü doğrudan taşınıp çalıştırılabilir.

---

# SqlUI (English)

A lightweight, multi-engine SQL client — built from scratch with WPF/.NET 10 as a personal
alternative to SQL Server Management Studio's ~600MB Visual Studio shell.

## Features

- **Multi-engine connections**: SQL Server, SQLite, PostgreSQL, MySQL (via an `IDbProvider`
  abstraction) — SQL Server and SQLite verified end-to-end against real servers.
- **Object Explorer**: server → database → table/view/stored procedure tree, context menus
  (Select Top N, script generation, rename, delete, backup/restore).
- **Context-aware IntelliSense**: table suggestions after `FROM`, column suggestions after
  `SELECT`/`WHERE`, and table-scoped columns after the dot operator.
- **Multiple result sets**, in-grid cell editing, Excel export.
- **Script generation**: CREATE / SELECT / INSERT / DROP; estimated execution plan view.
- **Database management**: create, rename, delete, backup/restore (full support for SQL Server).
- **Dark/light theme**, **Turkish/English** localization, resizable/responsive UI.
- **Single-file publish** options: framework-dependent (~22MB) or fully self-contained
  (~187MB, no installation required).

## Tech Stack

- **.NET 10 / WPF** for the desktop UI
- **CommunityToolkit.Mvvm** for MVVM (ObservableObject, RelayCommand)
- **Microsoft.Extensions.Hosting** for DI bootstrap
- **AvalonEdit** for the syntax-highlighted SQL editor
- **ClosedXML** for Excel export
- **Serilog** for logging
- Providers: `Microsoft.Data.SqlClient`, `Microsoft.Data.Sqlite`, `Npgsql`, `MySqlConnector`

## Build & Run

```bash
dotnet build
dotnet test
dotnet run --project src/SqlUI.App
```

To produce a distributable build:

```powershell
.\publish.ps1                  # framework-dependent, ~22MB, requires .NET 10 Desktop Runtime on target
.\publish.ps1 -SelfContained   # ~187MB, no prerequisites at all
```

## Known Limitations

- The PostgreSQL and MySQL providers are implemented but have not been verified against a real
  server — issues will be addressed as reported.
- No column-level filtering in the results grid yet.
- No installer package; the self-contained publish folder can be copied and run directly.
