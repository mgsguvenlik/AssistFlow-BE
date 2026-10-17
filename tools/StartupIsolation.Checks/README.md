# Açılış izolasyonu — dar kapsamlı bağlantısız kontrol

BE kökünde:

```powershell
dotnet run --project tools/StartupIsolation.Checks/StartupIsolation.Checks.csproj
```

Mevcut console-check yaklaşımını kullanır; yeni test framework'ü yoktur. Gerçek
AutofacBusinessModule options kaydı, seed/migration girişleri ve Manitou worker
izin denetimini sınar. DbContext/scope factory'leri gerçek SQL/API'ye ulaşmadan
hata verir. WebApplication yalnız Build edilir, host/HTTP/diğer worker'lar
başlatılmaz. WebAPI config'i veya gerçek credential kullanılmaz. Etkin yol
kontrolü gerçek seed/migration kabulü değildir; canlı çalıştırılmaz.

[Ayarlar ve geçiş sınırları](../../docs/releases/collections-2026-10-17/startup-isolation.md).
