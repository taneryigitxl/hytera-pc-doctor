# Hytera Pc Doctor

Windows tanı ve sorun giderme uygulaması. IT teknisyenleri ve son kullanıcılar için canlı sistem verisini okur; uydurma sağlık puanı üretmez.

**Türkçe / English** arayüz · WPF · .NET 10 · MVVM

## İndir

Kurulum dosyası GitHub Releases üzerinden yayınlanır. Kaynak kod bu depodadır; `setup.exe` git geçmişine gömülmez.

1. [Latest release](https://github.com/taneryigitxl/hytera-pc-doctor/releases/latest) sayfasını aç
2. **[HyteraPcDoctor-Setup.exe](https://github.com/taneryigitxl/hytera-pc-doctor/releases/download/v1.1.0/HyteraPcDoctor-Setup.exe)** dosyasını indir
3. Kur’a bas — uygulama masaüstüne `Hytera Pc Doctor.exe` olarak kopyalanır

Windows SmartScreen imzasız exe’lerde uyarı gösterebilir. Kaynak bu depo ise ve hash eşleşiyorsa dosya bizim paketimizdir.

SHA256 (v1.1.0): `B42C17118C9E31004D986FE1B8D4B22770676919BFA2161579B8F21737E41341`

## Features

- **Dashboard** — cihaz adı, Windows sürüm/build, CPU, RAM, disk, GPU, IPv4, uptime
- **Sistem Tarama** — CPU, RAM, depolama, Windows, sürücü, ağ, olay günlüğü, başlangıç, güvenlik
- **Sorunları uyar ve çöz** — onay sonrası temp temizlik, DNS flush, fazla başlangıç kapatma, güvenlik duvarı / Defender onarımı
- **Donanım** — CPU, bellek modülleri, GPU, anakart, BIOS, disk, birim, bağdaştırıcı
- **Ağ** — aktif adaptör, IP, gateway, DNS, MAC, hız, ping, DNS, bağlantı testi
- **Olay günlükleri** — System / Application Kritik ve Hata kayıtları (TR/EN düzey adları)
- **Başlangıç** — Run anahtarları ve başlangıç klasörü; etkinleştir / devre dışı bırak
- **Güvenlik** — Windows Defender ve Güvenlik Duvarı profilleri
- **Gösterge** — Afterburner tarzı köşe overlay; aç/kapa ve CPU/GPU/RAM/disk ölçümlerini tek tek seç
- **Ayarlar** — koyu/açık tema, Türkçe/İngilizce, overlay kısayolu

Bulgular **Tamam / Uyarı / Kritik** olarak ölçülen değerlerden sınıflanır.

## Architecture

```
PCDoctor.slnx
└── src
    ├── PCDoctor.Core          Models, enums, contracts
    ├── PCDoctor.Diagnostics   Collectors, scan, repair, logging
    ├── PCDoctor.App           WPF shell, MVVM, themes
    └── PCDoctor.Setup         Desktop installer
```

- **Core** has no Windows or UI dependencies.
- **Diagnostics** uses WMI/CIM, Event Log, registry, `DriveInfo`, `NetworkInterface`, `GetSystemTimes`, `GlobalMemoryStatusEx`.
- **App** is WPF + MVVM. Collectors run asynchronously.

## Technologies

- C# / .NET 10 (`net10.0-windows`)
- WPF + MVVM
- System.Management (WMI)
- Windows Event Log API
- Settings/logs under `%LocalAppData%\PCDoctor`

## Build

Requirements: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download)

```powershell
dotnet build PCDoctor.slnx
dotnet run --project src/PCDoctor.App/PCDoctor.App.csproj
```

Setup paketi:

```powershell
.\build-setup.ps1
```

Çıktı: `dist/HyteraPcDoctor-Setup.exe`

## Notes

- Manifest `asInvoker`. Bazı Defender / firewall / HKLM işlemleri yönetici ister.
- Troubleshooter senaryoları katalog aşamasındadır.
- HTML/PDF rapor dışa aktarma henüz yok.
- Güvenlik olay günlüğü yükseltilmiş izin istediği için sorgulanmaz.
