# Tahsilat canlı geçişi için güvenli commit kapsamı

17 Ekim 2026 yerel incelemesi. Kullanıcı devamında bu üç grup için yalnız yerel commit oluşturulmasını onayladı; push, main merge, Jenkins veya canlı uygulama onayı vermedi. İki repository de `tahsilat-module` branchindedir. FE çalışma alanı temizdir; bu hazırlık için yeni FE commit'i gerekmez. BE başlangıç commit'i `e75a761`, FE başlangıç commit'i `e13d42f` olarak kaydedildi; uzak referanslar fetch edilmedi.

## Önerilen commit grupları

### 1 Backend açılış güvenliği

Önerilen mesaj: `Tahsilat geçişi için backend açılış kontrollerini ekle`.

- `Core/Settings/Concrete/StartupExecutionOptions.cs`
- `Business/DependencyResolvers/Autofac/AutofacBusinessModule.cs`
- `Business/Services/ManitouStagingSyncBackgroundService.cs`
- `Business/Services/RehearsalManagedWorker.cs`
- `Data/Seeding/Infrastructure/SeedingExtensions.cs`
- `WebAPI/Middleware/ApplyMigrations.cs`
- `tools/StartupIsolation.Checks/` içindeki üç kaynak/README/proje dosyası.

İlk altı dosya backend yayınını etkiler; kontrol projesi manuel çalışır. Ayar verilmezse migration, seed ve Manitou eski davranışıyla açık kalır. Sadece kodu deploy etmek güvenli bakım başlangıcı sağlamaz. İki migration yolu birlikte yönetilir. Diğer dört worker yalnız özel izole prova modunda engellenir; Production ortamında bu mod reddedilir ve genel worker durdurma anahtarı olarak kullanılamaz. Production açılış override'ları ayrı hosting kontrolüdür.

### 2 Manuel aktarım ve doğrulama araçları

Önerilen mesaj: `Tahsilat canlı aktarım ve prova araçlarını ekle`.

- `tools/Collections.Release/`, `Collections.Transfer/`, `Collections.Rehearsal/`, `Collections.Production/`, `Collections.Files/`, `Collections.CdnReadOnly/` kaynakları ve README/proje dosyaları.
- `tools/Collections.Import/CollectionPaymentCurrencyComparison.cs`: aynı legacy çözümleyicisini offline Release aracının kullanabilmesi için çıkarılan ortak metot; bağlı kaynak olduğu için tek başına atlanmaz.
- `tools/` altındaki değişiklik kapsamına giren 11 PowerShell scripti.

Dosya listesi aşağıdaki manifestte tek tek belirtilir; dizinlerin gelecekteki tüm içeriği için genel ekleme izni değildir. Bu araçlar ana solution'a eklenmemiş ayrı console projeleridir. GitHub'a alınmaları SQL, restore, veri aktarımı veya CDN upload işlemini otomatik başlatmaz. Bazı scriptler onayla yazma/restore yapabildiğinden bu incelemede çalıştırılmaz. Finansal aktarımın ilk replay kontrolü dosya aktarımından önce tamamlanır.

### 3 Geçiş dokümanları ve Git korumaları

Önerilen mesaj: `Tahsilat canlı geçiş planı ve kontrollü işlem belgelerini ekle`.

- `.gitignore`
- `docs/collection-production-transition.md`, `docs/module-development-plan.md`
- `docs/releases/collections-2026-10-15/` içindeki SQL taslakları, şema ve prova raporları.
- `docs/releases/collections-2026-10-17/` içindeki runbook, kapalı onay şablonları, örnek açılış ayarları, raporlar ve bu commit kapsamı.

Eski tarihli raporlar tarihsel kanıttır; güncel durum için 17 Ekim devam kayıtları esas alınır. SQL'in Git'te bulunması çalıştırma yetkisi değildir. Ortak veri/prosedür/rol ve menü seed adımları kendi onay ve önkoşullarıyla uygulanır. Prova sonucunu veya kapalı onay JSON'unu canlı yazma onayına çevirmeyin.

## Kesinlikle kapsam dışında

- `WebAPI/appsettings.Development.json`: mevcut kullanıcı değişikliği aynen korunur; stage edilmez. `.gitignore` takip edilen bu dosyayı korumaz, `git add .` kullanılmaz.
- Diğer gerçek `appsettings*.json`, `.env.*`, connection string, API/SMS/CDN anahtarı ve hosting secret'ları. Yalnız yeni, gizli bilgi içermeyen `startup-isolation.example.json` ve kapalı onay şablonları kapsamdadır.
- `.tools/`, kesit/NDJSON verileri, gerçek müşteri ve dosya envanterleri, doldurulmuş operatör onayları, audit/makbuz çıktıları, aktarım paketleri ve fiziksel arşivler.
- `bin/`, `obj/`, DLL, uygulama logları ve yerel upload dizinleri.

Raporlardaki toplu sayılar, teknik nesne adları, karar kimlikleri ve hash referansları ham müşteri listesi değildir. Dokümanlar iç operasyon bilgisi içerir; repository'nin dışa açılması bu kapsamın parçası değildir. Şifre kalıpları, e-posta ve telefon taramasındaki adaylar incelendi; izole `unused` değerleri, onay durumları ve bir SHA içindeki rakam eşleşmesi gerçek secret/telefon sayılmadı. Bu dar tarama repository geçmişinin bütünü için secret temizliği iddiası değildir.

## Doğrulama ve sonraki adım

- BE solution `--no-restore -m:1 -nr:false` ile başarılı derlendi, 0 hata. Varsayılan paralel deneme 0 derleyici hatasına rağmen başarısız exit verdi; tek düğümlü tekrar başarılıdır. Mevcut NU1701 eski Identity ve MSB3277 EF Relational sürüm uyarıları devam eder; paket sürümleri bu hazırlıkta değiştirilmedi.
- Yeni JSON şablonlarının beşi ve PowerShell scriptlerinin on biri syntax kontrolünden geçti. Scriptler çalıştırılmadı.
- Mevcut bağlantısız açılış kontrolü 18/18 başarılıdır; DB/API/HTTP çağrısı yapmaz.
- `Collections.Import`, `Collections.Release`, `Collections.Rehearsal`, `Collections.Production`, `Collections.Files`, `Collections.CdnReadOnly`, `StartupIsolation.Checks` projeleri ayrı ayrı `--no-restore -m:1 -nr:false` ile 0 hata derlendi. İşletim komutları çalıştırılmadı.
- FE değişmediği için FE build/lint tekrarlanmadı. Hiçbir API hostu, finansal aktarıcı, restore veya CDN aracı çalıştırılmadı.
- İlk incelemede takip edilen diff temizdi. Yeni dosyalar stage edildiğinde `CollectionTransferCore.cs` ve üretilmiş 21 şema SQL'inin sonundaki boş satırlar için uyarı görüldü. Mühürlü prova kaynak/SQL hash'lerini değiştirmemek için içerikler korunup yalnız bu uyarı yerel `core.whitespace=-blank-at-eof` ile kontrol dışında bırakıldı; repository ayarı değiştirilmedi. Diğer staged whitespace kontrolleri geçmelidir.

Tam dosya listesi ve inceleme anındaki SHA-256 değerleri `git-commit-scope.files.json` içindedir. Manifest kendi hash'ini içermez; kendisi 3. gruba dahildir. Dosya değişirse kapsam/hash kontrolü yenilenir. Gelecekte yalnız bu listedeki dosyalar açık adlarıyla stage edilir, staged diff ayrıca incelenir ve gerçek config bulunmadığı yeniden kontrol edilir.

Yerel mühürlü prova/teslim paketleri değiştirilmez. Git kaynaklarının yayınlanması, eski paketleri son bakım kesiti veya yeni Release/hosting çıktısı için otomatik kabul kanıtı yapmaz. Gerçek Jenkins tanımı repository'de bulunmadığından job'un harici script/hook davranışı ayrıca kullanıcı tarafından teyit edilmelidir. Main merge ve Jenkins kullanıcı tarafından yapılır.

## Yerel commit durumu

- Grup 1: `25ca545` — açılış kontrolleri, 9 dosya.
- Grup 2: `a53a25e` — aktarım/prova araçları, 41 dosya.
- Grup 3: bu kayıt ve hash manifesti dahil geçiş dokümanları/Git korumaları, 63 dosya; üçüncü ayrı commit'in kapsamıdır.

Kaynaklar önceki incelemeyle aynıdır; yalnız bu durum belgesi, ana plan ve bunların manifest hash'leri güncellendi. `WebAPI/appsettings.Development.json` stage/commit kapsamı dışındadır. Push, deploy, DB/CDN veya job işlemi yapılmadı.
