# Backend açılışı — seed, migration ve Manitou izolasyonu

## Durum

**Sonraki kabul:** [gerçek WebAPI izole hosting ve yeni sürüm provası](hosting-and-refresh.md).
Gerçek loopback host kontrolü geçti. İzole prova modu yalnız özel ortam/prova
kataloğunda diğer worker constructor'larını durdurur; production için genel bir
worker kapatma bayrağı değildir. Aşağıdaki 13 kontrol/host çalıştırılmadı
ifadeleri ilk hazırlığın tarihsel sınırıdır; güncel bağlantısız kontrol sayısı
18'dir. Gerçek canlı hosting ve dış servis/kullanıcı kabulü hâlâ ayrı kapıdır.

Kod hazırlığı ve bağlantısız kontroller tamamlandı. **Canlı AssistFlow/MGS'ye
bağlanılmadı veya değişiklik yapılmadı.** WebAPI gerçek host'u, başka worker,
Jenkins, SQL Job veya Manitou API başlatılmadı. Gerçek appsettings dosyaları ve
kullanıcının Development config değişikliği korunur; örnek gizli bilgi içermez.

DI mevcut `AutofacBusinessModule` içindedir. Program.cs'ye kayıt/ayrı bootstrap
eklenmedi; mevcut başlangıç sırası değiştirilmedi. Müşteri tipi/tenant/staging
eşleme SQL'i ve Manitou iş mantığı aynı kaldı. Worker yalnız kendi ayarını
mevcut DI'dan alır; global ServiceTool üzerinden ikinci provider'dan ayar
çözmez. Eski çalışma sıklığı/ilk tur davranışı, etkin olduğunda korunur.

## Bulunan iki migration yolu

1. `UseDataSeedingAsync`: scope/DbContext → MigrateAsync → SeedRunner.
2. `MigrationApplier.ApplyMigrations`: tekrar scope/DbContext → pending kontrolü
   → gerekirse Migrate. Yalnız ikinci yolu kapatmak yeterli değildir.

`StartupExecution.MigrationsEnabled=false` ikisindeki migrationı durdurur.
`SeedsEnabled=false` tüm kayıtlı `IDataSeed` çalıştırmasını durdurur. İkisi de
false ise ilk yol **scope/DbContext bile oluşturmaz**; ikinci yol da DbContext
oluşturmaz. Sadece seed açıkken migration kapalıysa önce pending migration
salt-okuma kontrolü yapılır; eksik şemada seed'e geçilmez. Her iki ayar true
olduğunda eski akış aynıdır. Bu hazırlık mevcut iki yolu refactor ederek
tekleştirmez veya seed sırasını değiştirmez.

Genel seed kapsamı yalnız Tahsilat değildir: şehir/konfigürasyon/parola
politikası/tenant dashboard/iş akışları/menüler/Helpdesk/Sheets ve kayıtlı
Tahsilat SMS şablonu etkilenir. SeedRunner her seed'i ayrı transaction'da
çalıştırır; son seed hatası önceki seed commit'lerini geri almaz. Bu nedenle
ilk açılışta genel seed'leri çalıştırmak yerine gerekli yayın şema/seed'leri
önce ayrı onaylı paket üzerinden uygulanacaktır. Eksik ortak modül seed'leri
varsa ilgili modül sorumlusu ayrı inceler; kapalı açılış bunları oluşturmaz.

## Ayarlar / varsayılan davranış

Yeni üst bölüm: `StartupExecution`.

| Ayar | Yazılmazsa | Geçişte | Etki |
| --- | --- | --- | --- |
| MigrationsEnabled | true | false | Her iki otomatik migration yolunu kapatır |
| SeedsEnabled | true | false | Kayıtlı genel seed'leri çalıştırmaz |
| ManitouSyncEnabled | true | false | Worker ilk API/DB çağrısından önce çıkar |

Geriye uyumluluk için eksik ayarlar otomatik olarak true'dur. **Geçişte bunlar
açıkça false verilmelidir.** Yazım hatası/boolean olmayan değer, strict options
binding ile hata verir; sessizce izinli varsayılmasına güvenilmez. Etkili üç
değer açılış logunda görünür; bağlantı/token/credential loglanmaz.

Örnek: [startup-isolation.example.json](startup-isolation.example.json).
Bu bir **birleştirilecek override örneğidir**, tam appsettings değildir;
mevcut AppSettings bağlantı/diğer parametreleri bununla değiştirilmez. Gerçek
appsettings'e bu çalışmada yazılmadı. Sunucu için alternatif ortam değişkenleri:

```text
StartupExecution__MigrationsEnabled=false
StartupExecution__SeedsEnabled=false
StartupExecution__ManitouSyncEnabled=false
AppSettings__CollectionSmsEnabled=false
AppSettings__SmsSimulationEnabled=true
CollectionRead__Enabled=false
CollectionRead__ContractCreateEnabled=false
```

Jenkins job ortamında yazılması tek başına yeterli değildir: IIS/servis/container
**uygulama prosesine** bu değerlerin ulaştığı doğrulanır. Mevcut config sırası
JSON'dan sonra environment variable okur; etkili ayarlar logla teyit edilir.
Bu ayarlar yeniden başlatmayla alınır. Dosyayı runtime'da değiştirmek worker'ı
güvenle durdurma/başlatma işlemi değildir. ManitouRunEveryMinutes=0 hâlâ
60 dakikadır, durdurma parametresi değildir.

## Bakım ve kontrollü geri açılış

1. Henüz canlıya deploy yapma. Yeni BE sürümü ve yeni ayarları izole ortamda
   onayla; diğer worker'ların kendi ayarlarını/bekleyen işlerini ayrıca incele.
2. Bakımda BE'yi durdur; ilgili SQL Job ve kullanıcı yazıcılarını ayrı onaylı
   plana göre durdur. Bu üç bayrak SQL Agent Joblarını veya çalışan eski BE
   prosesini durdurmaz.
3. Yukarıdaki override'ı gerçek hosting ortamına hazırla. Onaylı schema/seed,
   veri+makbuz mutabakatı ve dosya işleri ayrı tamamlanır. Yanlış DB/config veya
   eksik migrationı gizlemek için bu bayraklar kullanılmaz.
4. Yeni kodla BE açıldığında üç false değeri ve atlanan migration/seed/Manitou
   uyarıları logda görülmeli. Manitou sync başlangıç/API çağrısı olmamalı.
   Dış çağrıları kapalı izole ortamda süreç seviyesinde kontrol edilmeden bu
   belgenin kendisi `StartupIsolation` production kanıtı sayılmaz.
5. Önce kapalı Tahsilat, ardından ayrı izinle salt-okunur kullanıcı kabulü;
   finansal yazma ve SMS açılması ayrı karardır. Bu bayraklar genel API read-only
   modu değildir. Mail/SLA/periyodik rapor/Helpdesk/SMS worker'ları global olarak
   kapatılmadı; kendi guard'ları/işleri ayrıca değerlendirilir. Manuel Manitou
   API tüketicilerini veya dış kaynak Joblarını bu worker bayrağı durdurmaz.
6. Genel migration/seed'i sırf Manitou açılacak diye açma. Canlı schema/seed
   paketle yönetildiği sürece ikisi false kalabilir. Yeniden açılmaları ayrı
   onaylı şema/seed incelemesi ve restart gerektirir.
7. Manitou yalnız Job/prosedür/customer-type uyumluluğu ve K10 kapsamı kabul
   edilince `ManitouSyncEnabled=true` + kontrollü restart ile açılır. **İlk tur
   hemen çalışır.** Durdurmak için false + servis stop/restart ve mevcut
   işlemlerin bitişi doğrulanır; credential bozma veya süreyi sıfırlama yoktur.

## Yapılan doğrulama / sınırlar

- `dotnet build AssistFlow-BE.sln --no-restore`: **0 hata /811 uyarı**.
  Son artımlı tekrar **0 hata /58 uyarı**. Mevcut çözüm uyarıları topluca
  refactor edilmedi; sayılar ilgili derlemelere aittir, baseline iyileşmesi değildir.
- `dotnet run --project tools/StartupIsolation.Checks/StartupIsolation.Checks.csproj`:
  **13 bağlantısız kontrol geçti.** Varsayılanlar, geçersiz ayar/value, dört
  migration/seed kombinasyonu, iki gerçek migration girişinin izin denetimi ve
  Manitou kapalı/etkin ilk tur davranışı denetlendi.
- Kontrolde DbContext factory ve Manitou scope factory hata veren sınırlarla
  engellidir; gerçek SQL/API'ye ulaşamaz. Yalnız test WebApplication nesnesi
  Build edilir; Start/Run/HTTP listener ve diğer hosted service'ler çalışmaz.
  Etkin yol denemesi gerçek seed/migration/API çalıştırıldığı anlamına gelmez.
- İlk kontrol derlemesindeki using ve araç EF sürüm eşleşmesi sorunları
  düzeltildi; son çalışma başarılı. Yeni test framework'ü eklenmedi.
- **Yapılmadı:** gerçek hosting prosesinde kontrollü açılış, gerçek seed yazma
  testi, Manitou gerçek çağrısı, Job durdurma veya canlı config/deploy.

## Paket ve prova kabulüne etkisi

Core/Business/Data kaynakları değişti. Önceki
`collection-cutover-handoff-2026-10-17-01` ve yazmalı kabul **eski runtime'ın
tarihsel kanıtıdır**; değiştirilmedi.
Bu yeni backend sürümüyle aynı finansal kurallar kullanılsa da katı kod/hash
kapıları atlanamaz: yeni adaptör/bağımlılık derlemesi → yeni plan/paket →
aynı sürümle gerekli izole yazmalı kabul ve son-kesit mutabakatı → yeni teslim.
Bu adımda eski kabul JSON'ları veya mühürlü paket hash'leri değiştirilmedi;
canlı onay dosyaları kapalı kalır. Runtime'ı eski onayın klasörüne kopyalamak
veya CodeHashes alanını elle güncellemek kabul değildir.
