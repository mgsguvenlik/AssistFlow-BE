# Canlıya özel Tahsilat dosya adaptörü

`Collections.Files`, finansal `Collections.Production` aracından ayrıdır.
Ortak `IFileStorage`/`R2FileStorage`, API, frontend ve test-only aktarıcılar
değişmez. Migration/seed, MGS yazması, Job değişikliği ve CDN silme içermez.
Program uygulama hostunu/worker'ları başlatmaz.

## Hedefler ve güvenlik

- `plan-rehearsal`, sabit `sql2022/AssistFlow_Performance_Restore` üzerinde
  yalnız okur; namespace yalnız `isolated.invalid/rehearsal-files` olur.
  Gerçek R2 ayarları prova yolunda kullanılmaz.
- `rehearse` yalnız aynı hedefte dosya ilişkisi ve `CdnFileReceipt` yazar.
  Gerçek `R2FileStorage`, HTTP çıkışını reddeden yerel `IsolatedS3` ile çalışır.
  Bu, gerçek public CDN erişimi veya uygulama HTTP yetkisi kabulü değildir.
- `plan-live` yalnız okuma ve yerel plan üretimidir; canlı kurulumu/finansal
  ilk batch ve onayları tamamlandıktan sonra kullanılabilir. Bu hazırlıkta
  çalıştırılmadı. Config'teki AssistFlow bağlantısı kapsam tarafından sabit
  hedefe yönlendirilir; serbest hedef DB parametresi yoktur.
- `apply-live`, açık ayrı dosya onayını ve aynı kod/kaynak için dosya provasını
  **bağlantıdan önce** doğrular. Config, plan ve namespace uyuşmalı; aktif
  operatör, yedek/bakım/tarama/public maruziyet/finansal replay kanıtları,
  taze plan ve kapalı dört Job zorunludur. Şablonlar NOT_APPROVED kalır.

Prova/çıktılar yalnız yeni `.tools` dizinine; eski dosyanın üzerine yazılmaz.
Link/reparse yolları reddedilir. Kaynak fiziksel kök salt-okunur, traversal
kontrollüdür. Arşiv yalnız boyut sınırlı `7z e -so` ile belleğe okunur.
Dosya: PDF/PNG/JPEG, içerik imzası, SHA-256, 1–20 MB, en fazla 260 karakter ad.
Bu kontroller zararlı yazılım taramasının yerine geçmez.

## Kullanım

BE repository kökünden; credential argüman olarak verilmez ve loglanmaz:

```powershell
dotnet build tools/Collections.Files/Collections.Files.csproj -m:1

# Yalnız prova DB SELECT; kaynak dosyalar tekrar byte/hash doğrulanır.
dotnet tools/Collections.Files/bin/Debug/net9.0/Collections.Files.dll plan-rehearsal `
  WebAPI/appsettings.Production.json .tools/INCELEME-PAKETI BUNDLE_SHA `
  YEREL_CONTRACT_KOKU AKTIVITE_RAR BATCH_ID `
  .tools/FINANSAL-KABUL/acceptance.json .tools/YENI-DOSYA-PLANI

# Yalnız izole hedef + yerel sahte S3; gerçek CDN çağrısı yok.
dotnet tools/Collections.Files/bin/Debug/net9.0/Collections.Files.dll rehearse `
  WebAPI/appsettings.Production.json .tools/YENI-DOSYA-PLANI/plan.json PLAN_SHA `
  .tools/YENI-DOSYA-PROVASI REHEARSAL_ONLY
```

Bu komutlarda Production config yalnız mevcut güvenli bağlantı kaynağıdır;
prova hedefi kodda sabittir. `COLLECTION_RELEASE_CONNECTIONSTRING` override'ı
desteklenir, ancak kaynak katalog ve sunucu kısıtı korunur. Gerçek bağlantı
metni çıktı/pakete yazılmaz. Prova başlarken başka kullanıcı oturumu,
etkin trigger veya uygunsuz TRUSTWORTHY/DB chaining/broker ayarı varsa durur;
KILL/restore/otomatik düzeltme yapmaz.

Canlı komut ayrı `apply-live` arayüzüdür; **bu belgede çalıştırılacak dolu
canlı komutu veya onay yoktur.** `plan-live` aynı plan argümanlarını kabul eder;
prova kapsamı canlıya çevrilmez. Üretilen onay şablonunda:

- Authorization açık dosya uygulama kararı, Scope LIVE, aynı PlanHash,
  gerçek OperatorCode/ApprovalReference ve ExpiresAtUtc gerekir.
- MaintenanceApproved, BackupApproved, ScanApproved, PublicExposureAccepted,
  NamespaceApproved, FinancialReplayApproved ayrı gerçek kararlardır.
- Evidence kayıtlarında HumanApproval/Maintenance/Backup/MalwareScan/Namespace/
  FinancialReplay için özel Path/Sha256 kanıtı gerekir.
- RehearsalAcceptancePath/Hash aynı BundleHash, CodeHash ve kaynak dosya
  projeksiyonuna bağlı başarılı izole kabul olmalıdır. Plan 30 dakikadan eski
  olamaz; önce finansal ilk replay bitmiş olmalıdır.
- Mode PILOT/FULL ve MaxFiles onaylanır. FULL tüm adayları kapsar. Başlangıç
  şablonunda MaxFiles=0, tüm kapılar false, kimlikler boştur.

## İşlem ve tekrar sözleşmesi

1. Batch/hash, orijinal dosya audit'i, tekil SubscriberCode, güncel müşteri,
   sözleşme map/owner/rowversion doğrulanır. Önceden kaldırılmış veya receipt'i
   varken fiziksel silinmiş ilişki tekrar oluşturulmaz.
2. Yerel byte'lar tekrar doğrulanır. Nesne yoksa mevcut storage ile PUT;
   varsa **üstüne yazmadan** GET SHA-256/MIME/size kontrol edilir. Metadata
   403/timeout yokluk değildir; hata halinde durulur. ETag tek başına kanıt
   değildir. Anahtar kapsam/namespace/kaynak/sahip/içerik hash'inden kararlıdır.
3. Kısa Serializable SQL transaction içinde sahiplik tekrar kontrol edilir;
   attachment ve immutable `collection.MigrationSourceRow/CdnFileReceipt`
   aynı transaction'da yazılır. Yeni şema gerektirmez. Teknik aktör 0 ve
   gerçek operatör onayının ayrımı korunur.
4. Journal append-only ilerler. Commit sonrası belirsizlikte nesne silinmez;
   aynı key/owner/receipt karşılaştırılarak devam edilir. Tam replay sıfır
   INSERT ve sıfır PUT üretir; orijinal ödemeler/notlar değişmez.
5. Canlıda mevcut public URL tam GET ile hash/MIME/size kontrolünden geçer.
   Redirect ve HTML hata sayfası kabul edilmez. Prova bu kontrolü yerel depo
   readback'i olarak **SimulatedPublicReadback** etiketler.

İşlem sıralıdır; örtük paralel upload veya sınırsız yeniden deneme yoktur.
CDN/SQL atomik değildir: upload sonrası SQL hatasında nesne journal ile
korunur. Aynı key'de yanlış içerik asla ezilmez. Bakım/tek-yazıcı zorunludur;
ortak R2 PUT arayüzü koşullu create garantisi sağlamaz.

## Prova çıktıları ve işletim sınırı

`result.json`, `journal.ndjson`, finansal önce/sonra parmak izleri ve bunlara
hash ile bağlı `acceptance.json` üretilir. Izole gerçek DB transaction'larında
upload sonrası, commit öncesi, commit sonrası kesintiler; tam tekrar; yanlış
owner/audit; bozuk readback; metadata 403; kaldırılan ilişki ve mevcut liste
servisleri kontrol edilir. Kaldırma deneyi rollback edilir. Başarılı dosya
ilişkileri/makbuzları prova DB'sinde, nesneler yalnız özel yerel dizinde kalır.
DB yalnız StoredFileName tutar; isolated.invalid URL'si prova servisinin
izole ayarlarla ürettiği çıktıdır. Bu DB normal CDN ayarlı uygulamaya bağlanırsa
gerçek dosya varmış gibi gösterilmemelidir; yeni anahtarlar gerçek CDN'de yoktur.
Eski finansal prova artık dosya tablolarını boş bekleyen ilk-kurulum kontrolüyle
kör yeniden çalıştırılmaz; eski mühürlü kanıtlar değiştirilmez.

Gerçek CDN TLS/izin/cache/malware/public-link ve tarayıcı/API yetki kabulü
ayrı canlı kapılarıdır. Prova acceptance'ı ProductionAuthorizationGranted=false
ve RealCdnReadbackVerified=false kalır. Kod/bağımlılık/kaynak değişince yeni
plan/kabul gerekir. Canlı UI açılması, SMS veya diğer finansal yazmalar bu
aracın kapsamı değildir. Ayrıntı: [CDN geçiş planı](../../docs/releases/collections-2026-10-17/cdn-transfer-and-access.md).

Başarılı kabulü ayrı offline teslim yapmak için `tools/Prepare-CollectionCdnHandoff.ps1`
PlanPath, AcceptancePath, onarımsız başarılı CHECKDB içeren FinalProfilePath ve
yeni OutputPath ister. Aynı kod/kaynak/prova ve kapalı onayı doğrular; fiziksel
arşiv/nesne ve config kopyalamaz. Canlı işletim yetkisi vermez.
