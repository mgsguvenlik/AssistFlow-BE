# Tahsilat yayın hazırlık aracı

Bu araç DB'ye yazmaz. WebAPI başlatmaz, migration/seed/Job çalıştırmaz; apply komutu içermez. Bağlantı/parola çıktıya yazılmaz. .NET 9; repo kökünden çalıştırılır.

`reconcile-relations` hedef referanslarını capture manifestinden seçer: canlı
`AssistFlow` veya açıkça işaretli `AssistFlow_Performance_Restore`. Her iki
durumda da yalnız SELECT çalışır. Gerçek prova uygulayıcısı ayrı
[Collections.Rehearsal](../Collections.Rehearsal/README.md) aracıdır; bu hazırlık
aracına production apply yetkisi eklenmemiştir.

## Komutlar

Prova sonrası güncel teslim ve kalan kapılar:
[17 Ekim canlı inceleme paketi](../../docs/releases/collections-2026-10-17/README.md).
`seal-cutover` aşağıdaki girdileri **çevrimdışı** birleştirir; SQL/deploy/apply
komutu değildir. Gerçek prova sonuçlarını hash ile doğrular, şema/seed içeriklerini
prova türevlerine dönüştürerek aynı SQL olduğunu denetler, canlı/prova referanslarını
ayırır. İnsan onayları false kalır; production importer içermez.

```powershell
dotnet build tools/Collections.Release/Collections.Release.csproj
dotnet tools/Collections.Release/bin/Debug/net9.0/Collections.Release.dll seal-cutover .tools/LIVE-CAPTURE .tools/LIVE-REVIEW .tools/LIVE-DATA .tools/LIVE-PREFLIGHT.json .tools/MGS-PREFLIGHT.json .tools/LIVE-SCHEMA .tools/NEW-CUTOVER-REVIEW
```

Bu sürümün prova kanıtları 17 Ekim başarılı sonuç hash'lerine sabittir. Farklı
prova/sürüm için kanıtlar ayrıca incelenip paketleyici güncellenir; `Approved`
bayraklarını değiştirerek production apply açma seçeneği yoktur.

```powershell
# SQL migration dosyalarını yerel ön kontrol raporundan üretir; DB bağlantısı kurmaz.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- build .tools/live-profile.json .tools/schema-review-NEW

# Yalnız sabit SELECT sorgularıyla MGS + canlı AssistFlow referanslarını okur.
# Dosyaları iki kez hash'ler; bu bir cross-database snapshot garantisi değildir.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- preview-live WebAPI/appsettings.Production.json .tools/live-capture-NEW

# Kullanıcının onayladığı aynı-instance prova hedefinin referanslarını okur.
# MGS yalnız SELECT kaynağıdır; canlı AssistFlow hedef referansı olarak kullanılmaz.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- preview-rehearsal WebAPI/appsettings.Production.json .tools/rehearsal-capture-NEW

# Kaynak dosya hash'lerini doğrular, yalnız yerel dosyalarda finansal/kimlik önizlemesi üretir.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- review-live .tools/live-capture-NEW .tools/live-review-NEW

# Arşivdeki karar ve MGS kesitini hash/alan kanıtlarıyla güncel okumaya bağlar; tamamen çevrimdışı.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- replay-decisions .tools/live-capture-NEW ../local-archives/collection-cleanup-2026-10-04 .tools/decision-replay-NEW

# Onay/istisna kapılarını finansal incelemeye uygular; tamamen çevrimdışı.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- review-live .tools/live-capture-NEW .tools/live-review-WITH-DECISIONS .tools/decision-replay-NEW

# Yerel aday paketini ve varsayılanı kapalı SQL taslaklarını üretir.
# Canlıda yalnız mevcut stg.sp_SyncCustomers tanımını SELECT ile okur; SQL çalıştırmaz.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- prepare-data .tools/live-capture-NEW .tools/live-review-WITH-DECISIONS WebAPI/appsettings.Production.json .tools/data-package-NEW

# MGS'de yalnız tanım/collation/yıl ve dokuz SQL nesnesinin metnini iki kez SELECT ile okur.
# Ödemeler ve banka kimlikleri mevcut hash'li yerel kesitten karşılaştırılır; view/fonksiyon çalıştırılmaz.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- reconcile-payments .tools/live-capture-NEW .tools/live-review-WITH-DECISIONS WebAPI/appsettings.Production.json .tools/payment-reconciliation-NEW

# İsteğe bağlı son argüman, kaynak/karar/inceleme hash'leri aynı mutabakatı pakete bağlar.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- prepare-data .tools/live-capture-NEW .tools/live-review-WITH-DECISIONS WebAPI/appsettings.Production.json .tools/data-package-WITH-PAYMENTS .tools/payment-reconciliation-NEW

# MGS müşteri/cari + grup detayları ve canlı müşteri/grup/tip/para birimi referanslarını yalnız SELECT ile okur.
# Arşiv manifesti ve Aktivite arşivi bilinen hash'lere sabitlenmiştir; yerel dosyalar yeniden hash'lenir.
# 7z yalnız l ve e -so (belleğe okuma) için kullanılır; diske çıkarma veya CDN isteği yoktur.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- reconcile-relations .tools/live-capture-NEW .tools/live-review-WITH-DECISIONS WebAPI/appsettings.Production.json ../local-archives/collection-cleanup-2026-10-04/.tools/item-eight-source-20260926 ../local-archives/collection-cleanup-2026-10-04/.tools/item-eight-verified.json '<Aktivite.rar tam yolu>' .tools/relation-reconciliation-NEW

# Son iki argümanla ödeme ve ilişki mutabakatı aynı kaynak/karar/inceleme hash'ine bağlanır.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- prepare-data .tools/live-capture-NEW .tools/live-review-WITH-DECISIONS WebAPI/appsettings.Production.json .tools/data-package-FULL .tools/payment-reconciliation-NEW .tools/relation-reconciliation-NEW

# Aktif veri paketine bağlı audit ve izole prova hazırlığı. Yalnız canlı metadata SELECT'leri;
# gerçek restore, backup, kullanıcı oluşturma, uygulama/worker başlatma veya apply yoktur.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- prepare-rehearsal .tools/live-capture-NEW .tools/data-package-FULL WebAPI/appsettings.Production.json .tools/audit-rehearsal-NEW
```

Canlı bağlantı, `COLLECTION_RELEASE_CONNECTIONSTRING` ortam değişkeninden veya verilen config'in `AppSettings.MSSQLConnectionString` alanından alınır. Hedef adı AssistFlow olmalıdır; MGS aynı sunucudan salt okunur açılır. Credential komuta/README'ye konmaz.

Önizleme dosyaları kişisel/finansal veri içerdiğinden yalnız yeni `.tools` alt klasörüne yazılır; mevcut çıktı üzerine yazılmaz. SQL lock timeout 5 saniye, sorgu timeout 120 saniyedir; kaynakta snapshot ayarı değişmez, NOLOCK/uzun Serializable okuma yapılmaz. Hata varsa eksik klasör final paket sayılmaz; capture manifesti ve bütün hash listesi yoksa review yapılamaz.

Ödeme dönem/vade/para birimi kuralları mevcut Business kaynak dosyalarına link edilerek kullanılır; ikinci bir iş kuralı motoru kurulmaz. Önizleme sayıları aktarım onayı değildir. Eski kararların hash/alan kanıtları, grup/cari, fatura, fiziksel dosya ve legacy ödeme görünümü kontrolleri ayrıca tamamlanır. Test müşteri/işlem kimlikleri veya test işlem verisi canlıya kopyalanmaz.

`prepare-data` ödeme kimliği için mevcut `CollectionPaymentImportIdentity` kaynağını kullanır. Üretilen SQL dosyaları `@Apply=0`, hedef DB ve eski değer/hash kontrolleriyle kapalıdır. Prosedürün önce/sonra metinleri çalıştırılabilir SQL değil `.txt` kanıtıdır. Paket `BLOCKED_REVIEW_PACKAGE_NOT_APPROVED_FOR_EXECUTION` durumundadır; sözleşme/ödeme JSON planları canlı import komutu değildir. Ortak dbo/prosedür onayı, final bakım kesiti, aktör eşlemesi ve izole restore/tekrar/geri dönüş provası tamamlanmadan uygulanmaz.

`reconcile-payments`, mevcut `CollectionPaymentCurrencyComparison` çevrimdışı resolver'ını ve dokuz sabit SQL tanım hash'ini paylaşır. Güncel canlı para birimi ID'leri yalnız eşleme önerisidir, onaylı migration map yerine geçmez. Tarihsel ücretsiz/negatif/sıfır ödeme değerleri korunur; diğer kimlik/takvim/karar kapıları kaldırılmaz. Banka planı tüm kaynak kimliklerini kapsar; elenen aboneliklerin ödemesi aktarılmaz. Banka kimliğini ödeme satırına tahminle bağlamaz; uygulamada mevcut `CollectionBankLoadService.Key` kullanılmalıdır. Mutabakat bağlı plan biçimi `legacy-reconciled-source-plan-v1`, ilk önizleme biçimi `preliminary-review-v1` olarak manifestte ayrılır. Kayıtlar hâlâ `ReadyToApply=false` durumundadır.

`reconcile-relations` not/faturada yalnız korunacak sözleşme sahibi veya ayrı doğrulanmış üst kart önerisi kullanır. Üst kartta ad/sekiz harf benzerliği yalnız çakışma uyarısıdır, sahiplik eşlemesi değildir; eksik G tipi/ortak müşteri oluşturma otomatik uygulanmaz. Grup döneminde çelişkili tekrarlar bekler; birebir aynı tekrar tek temsil edilir. Sorunlu ödeme geçmişi bulunan fatura bütünüyle bekler; kısmi geçmişle bakiye yaratılmaz. Cari kodlar kaynak genelinde tekil olmalıdır. Dosyalarda manifest kökü yerine açıkça verilen yerel kök kullanılır; kök dışına çıkma ve link/reparse yolları reddedilir. Önceki altı dosya devir onayı yalnız doğrulanmış güncel müşteriye ve yalnız dosyalara uygulanır; eski ödemelere/notlara genişletilmez. Lokal hash doğrulaması production CDN sahiplik/erişim kabulü veya güncel zararlı yazılım taraması değildir.

`prepare-rehearsal` teknik sistem aktörü 0 için kullanıcı/FK çakışmasını kontrol eder; eski kişi adlarını gerçek kullanıcıya eşlemez. İnsan onayı için ayrı canlı User.Code gerekir; boş bırakılır. Ham kaynak audit'i ve Europe/Istanbul tarihsel offset'iyle UTC önerileri yerel planda korunur. Source isimleri, yedek path/LSN bilgileri Git dışında kalır. Mevcut msdb backup kaydı okunabilir/restore edilebilir yedek kanıtı değildir. Model salt metadata için kurulur; Migrate/EnsureCreated veya WebAPI başlangıcı çağrılmaz. İzole SQL hedefi/yedek yolları ve tam apply yolu tamamlanmadan plan çalıştırılabilir yayın sayılmaz. Bkz. `docs/releases/collections-2026-10-15/rehearsal/README.md`.

`preview-rehearsal` yalnız `AssistFlow_Performance_Restore` için sabittir; manifestte `TargetDatabase` ve `RehearsalOnly=true` kaydedilir. Bu referanslarla `prepare-data` production paketi üretmeyi reddeder. Prova restore/şema komutları release aracı dışında, açık hedef/hash kilitli PowerShell araçlarıdır; production scriptleri değiştirilmez. Bkz. `rehearsal/execution-2026-10-17.md`.
