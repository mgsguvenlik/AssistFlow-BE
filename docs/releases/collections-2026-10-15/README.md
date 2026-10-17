# Tahsilat — Jenkins ile canlı geçiş planı

**Güncel devam — 17 Ekim canlı paket hazırlığı:** Prova kanıtlarına bağlı yeni
canlı müşteri eşlemeleri, 21 migration/2 seed eşdeğerlik kontrolü, veri planları,
Job inceleme matrisi ve onay kapıları tek hash'li inceleme paketinde toplandı.
[Aktif paket ve kalan işler](../collections-2026-10-17/README.md),
[kontrollü geçiş runbook'u](../collections-2026-10-17/cutover-runbook.md).
Paket uygulama onayı veya production importer değildir; canlıya yazılmadı.

**17 Ekim veri provası:** Yalnız onaylı prova DB'sine 4.439 sözleşme,
17.269 tarife ve 139.747 ödeme/makbuz aktarıldı; kaynak/tutar/audit mutabakatı
geçti. Kontrollü kesinti rollback'i, ikinci çalışmada 0 yeni kayıt, dört
makbuz/audit koruma senaryosu ve son CHECKDB başarılı. Ayrıntı ve ayrı kalan
kapılar [veri provası raporunda](rehearsal/data-transfer-2026-10-17.md).
Canlı AssistFlow/MGS'ye yazılmadı; production NO-GO sürer.

**17 Ekim gerçekleşen prova:** Kullanıcının açık onayıyla yalnız `AssistFlow_Performance_Restore` üzerine güncel mevcut canlı yedek restore edildi. CHECKDB temiz; 21 migration + 2 seed ilk/ikinci çalışma geçti, 95 migration / 34 collection tablosu. Rol, Job ve bildirim servisi açılmadı; canlı AssistFlow/MGS değişmedi. [Kanıt ve veri provası açıkları](rehearsal/execution-2026-10-17.md). Bu sonuç production yayını veya finansal veri aktarımının tamamlandığı anlamına gelmez.

**17 Ekim:** Kullanıcı prova için aynı instance üzerinde `AssistFlow_Performance_Restore` hedefini belirledi. Salt-okuma bağlantısı başarılı; hedef 5 Ağustos AssistFlowTest restore'u, güncel canlı kopyası değil. [Hedef incelemesi ve restore onay kapısı](rehearsal/target-selection-2026-10-17.md). Henüz hedefe veya canlıya yazılmadı; eski ayrı-instance preflight bu profile doğrudan uygulanmaz.

Durum: **hazırlık / NO-GO**. SQL dosyaları üretildi; canlıda DDL, INSERT, UPDATE, seed, aktarım veya Job değişikliği yapılmadı. Şema paketi, veri aktarımının hazır olduğu anlamına gelmez. Aşağıdaki durdurma koşulları kapanmadan main merge/deploy yapılmaz.

Güncel devam: [not/grup/fatura/cari/dosya mutabakatı](relation-reconciliation-report.md). Yeni canlı referans kesitiyle karar ve ödeme kontrolleri tekrarlandı; 4.438 sözleşme / **139.717 ödeme** aynı kaldı. İlişki ve dosya planları aktif **`collection-production-data-package-2026-10-15-03`** paketine bağlandı. Ortak referans/prosedür taslakları kapalıdır. Bunlar uygulama onayı veya tamamlanmış veri aktarımı değildir; aşağıdaki ilk önizleme sayıları tarihsel kanıttır.

Son hazırlık: [aktör/audit ve izole prova paketi](audit-rehearsal-report.md). 173.417 aday kaynak kaydının audit planı, sistem aktörü kontrolü ve mevcut yedek metadata envanteri hazır. [Prova sırası/kabul kapıları](rehearsal/README.md) tanımlandı; gerçek izole host, yedek/MOVE bilgileri ve insan onaylayıcı kodu bekleniyor. Restore/gerçek apply provası henüz yapılmadı; bu kapılar kapatılmış sayılmaz.

## Kesinleşen yayın yöntemi

- FE ve BE için ayrı Jenkins job'ları var; kodu main'e kullanıcı merge edecek ve deploy edecek.
- Bakım aralığı ayrılabilir. Önce hazır paket/prova, sonra üzerinde anlaşılan saatte geçiş.
- Güncel veri kaynağı **legacy MGS**. AssistFlowTest'teki sonradan girilmiş ödeme, zam ve notlar kopyalanmaz.
- AssistFlow canlı müşteri, grup ve tanım kimlikleri yeniden çözülür. Test identity değerleri, plan hash'leri ve müşteri tabloları canlıya kopyalanmaz.
- Mevcut uygulama/CDN mimarisi korunur. Ortak dbo müşteri/prosedür değişiklikleri ayrı fark ve onay paketidir; eski test yardımcılarının kilidi kaldırılmaz.

## 15 Ekim salt-okunur tespitler

| Kontrol | Sonuç |
| --- | --- |
| AssistFlow migration geçmişi | 74 kayıt; 21 Tahsilat migration'ı eksik |
| Yeni ortak migrationlar | MGS Sheets ve Password Policy zaten uygulanmış; tekrar çalıştırılmayacak |
| Repository dışı geçmiş | `20251124200930_FixCustomerDuplicateColumn` mevcut; silinmez veya uygulanmış gibi yeni kayıt üretilmez |
| collection şeması tabloları | Yok |
| Aktif AssistFlow müşterileri | 18.883; boş abone no 0, aynı normalize abone noyu taşıyan aktif müşteri grubu 0 |
| Canlı müşteri tipleri | BRYSL01, KRMSL01, BNK01, GRP01; GRP01 yeniden eklenmez; eski araçtaki Id=5 türünü G'ye çevirme davranışı kullanılmaz |
| Tahsilat menüsü / SMS şablonu | Yok |
| Canlı sp_SyncCustomers | 23 Eylül sürümü; testten prosedür bütünüyle kopyalanmaz, canlı tanımına dar fark gerekir |
| Kaynak Joblar | Platform Tahsilat Bakım, Platform SIMStoMGS-MANITOUTOMGS, Platform GrupTopluMail ve Manitou2Flowassist etkin |
| Snapshot isolation | AssistFlow'da kapalı; ayar değiştirilmedi. Canlıda uzun serializable okuma yerine onaylı restore/kesme stratejisi gerekir |

Bu sayılar yalnız 15 Ekim ön kontrol anına aittir. Geçiş günü profil yeniden alınır; sayı/şema değişirse paket yeniden değerlendirilir. Metadata satır sayıları yaklaşık, abone kalite sayımları gerçek sorgu sonucudur.

## Hazırlanan dosyalar

- `data-preparation-report.md`: güncel MGS/canlı referans eşlemeleri, ön aday/istisna sayıları ve Git dışındaki yerel veri paketlerinin hash referansları. Bu hazırlık okuması final bakım kesiti veya apply onayı değildir.
- `00-preflight.sql`: hedef migration, tablo/kolon/indeks/FK, ortak tanım, abone kalite, menü, SMS ve prosedür hash incelemesi; yalnız okur.
- `01-legacy-preflight.sql`: MGS kaynak tablo ve SQL Agent adım/zamanlama metadata'sı; yalnız okur, komut içeriği/credential raporlamaz.
- `schema/01...21-*.sql`: EF migration Up'larından üretilmiş, hedef DB ve predecessor kontrollü idempotent scriptler. Her migration kendi transaction'ına sahiptir; 21 script tek atomik işlem değildir. Aynı migration tekrarında veri silinmez. Drift/yarım manuel kurulum varsa devam edilmez.
- `schema/manifest.json`: uygulama sırası, migration kimlikleri, her SQL'in SHA-256'sı ve kullanılan ön kontrol raporunun hash'i. Durum `REVIEW_REQUIRED_NOT_APPROVED_FOR_EXECUTION`.
- `30-definition-seed.sql`: ödeme dönemleri, sözleşme/abonelik/grup durumları ve ödeme yöntemleri. **Varsayılan uygulama kapalıdır.** Mevcut tanımları yeniden adlandırmaz; çakışmada durur.
- `40-menu-sms-seed.sql`: Tahsilat menüsü ve parametrik SMS şablonu. **Varsayılan uygulama kapalıdır.** Mevcut şablonu ezmez; rol kodu NULL iken yetki vermez. Onaylı rol kodu ayrı seçilir; Id=1 varsayımı yapılmaz.
- `41-shared-reference-plan.sql`: mevcut canlı ServiceType tanımlarına karşı ad/kod eşleme ve eksik tanım adayları; yalnız SELECT. Bulanık isim eşlemesiyle otomatik yeni ortak kayıt oluşturmaz.
- `50-postflight.sql`: zorunlu migration, yapı ve tutar toplamlarını salt-okunur kontrol eder. Başarı tek başına veri mutabakatı/kullanıcı kabulü değildir.
- `tools/Collections.Release`: çevrimdışı paket/karar araçları ve ayrıca sabit SELECT kullanan kaynak/kanıt/audit hazırlık komutları. Hiçbir komut WebAPI/worker, backup/restore, migration/seed veya veri yazma başlatmaz.
- `tools/Read-CollectionProductionPreflight.ps1`: bağlantıyı güvenli config/`COLLECTION_RELEASE_CONNECTIONSTRING` üzerinden alır, yalnız ön kontrol sorgularını çalıştırır. Hassas raporlar `.tools` altında Git dışında kalır.

## Kritik: backend başlangıcı DB'ye yazar

`WebAPI/Program.cs` önce `UseDataSeedingAsync<AppDataContext>()` çağırır. Bu çağrı önce `Database.MigrateAsync`, ardından kayıtlı **tüm genel seed'leri** çalıştırır. Sonrasında `MigrationApplier` yeniden pending migration kontrol eder. `CollectionRead.Enabled=false` bunu durdurmaz.

Bu nedenle mevcut kodla güvenli sıra: **BE durmuşken onaylı SQL paketi → veri aktarımı ve DB mutabakatı → Jenkins BE → genel seed/log doğrulama → salt-okunur modül açılışı → Jenkins FE → ekran kabulü ve yazma açılışı.** Şemayı ilk kez oluşturma işi deploy başlangıcına bırakılmaz. Genel seed etkisi de restore provasında gözlenir. BE ile başlayan Manitou/mail gibi background servisler ayrıca değerlendirilir; finansal veri uygulaması sırasında backend kapalı kalır. Bu hazırlıkta başlangıç davranışı değiştirilmedi.

## Uygulama sırası ve kabul kapıları

| Sıra | İş | Kabul / devam koşulu |
| --- | --- | --- |
| 1 | Yayın kapsamı ve sürüm kilidi | FE/BE commit SHA, kullanıcı main merge commitleri, Jenkins build numarası; K10/K11 ve açık K12 istisnaları için beklenen işletim/manüel karşılık açıkça kabul edilmiş |
| 2 | Canlı restore/prova ortamı | Taze AssistFlow restore'u; kaynak MGS tutarlı kopyası/kesiti; restore, tekrar çalıştırma ve geri dönüş doğrulanmış; prova SMS/mail gerçek müşteriye gitmez |
| 3 | Ortak referans ve müşteri fark planı | Canlı abone/kod eşlemesi; CustomerType/ServiceType eksikleri insert listesi; sınıflandırma eski/yeni değerleri ve sp_SyncCustomers dar farkı hash'lenmiş; ortak kayıt değişiklikleri ayrıca onaylı |
| 4 | Kaynak aktarım paketi | MGS güncel sözleşme/tarife/zam, ödeme, not, grup dönem geçmişi, fatura/cari ve dosya ilişkileri; kaynak kimlik/hash + canlı hedef kimliği ile önizleme; belirsiz kayıtlar nedenleriyle dışarıda |
| 5 | Son bakım başlangıcı | Kullanıcı yazmaları kapalı, BE durmuş; ilgili kaynak/hedef Job adımlarının önceki etkinlik durumu kaydedilmiş ve yalnız onaylıları durdurulmuş. Ortak stok/servis/backup Jobları körlemesine kapatılmaz |
| 6 | Yedek ve son kesit | AssistFlow tam yedek + gerekiyorsa log zinciri, MGS tutarlı son kesit; geri yükleme denenmiş. CDN envanteri/hash'leri, önceki BE/FE artefaktları ve config yedekli |
| 7 | SQL şema | `schema` manifest sırasıyla; SHA kontrolü; ilk hatada durma; migration başına kayıt/nesne doğrulama; ardından 30 ve 40 onaylı parametrelerle. Mevcut tarihçe silinmez |
| 8 | Canlı veri uygulaması — BE kapalı | Onaylı canlıya özel apply yolu; test-only fixture/kurucu çalıştırılmaz. Yalnız korunmuş sözleşmeler ve onların ödemeleri; yeni hedef ID/map'ler. Her parça transaction/receipt/idempotency ile uygulanır |
| 9 | DB mutabakatı | Kaynak→plan→hedef sayıları; para birimi bazlı ödeme; tarihçe tutar/tarih/dönem; donuk/ücretsiz/YOK; mükerrer ödeme=0; banka baseline/identity koruması; not/fatura/dosya hash/linkleri ve dışlamalar raporlu |
| 10 | Jenkins BE — modül kapalı | CollectionRead.Enabled=false, ContractCreateEnabled=false, CollectionSmsEnabled=false, SmsSimulationEnabled=true. BE doğru live DB'ye bağlı; startup migration pending=0 ve genel seed kontrolü başarılı. API borç/ödeme/kalan toplamı açılış kontrolünde karşılaştırılır. Background servis/joblar yalnız onaylı kapsamla açılır |
| 11 | Açılış — önce salt okunur | Enabled=true, ContractCreateEnabled=false; gerekiyorsa config değişikliğiyle BE Jenkins yeniden yayını/restartı. Sonra FE Jenkins yayını. Yetkili/yetkisiz oturum, detay/geri dönüş, filtre, Excel/toplamlar, dosya açma ve ekran/API kontrolleri |
| 12 | Finansal yazma açılışı | İş birimi ve kullanıcı onayı sonrası ContractCreateEnabled=true; ilk gerçek işlem ve tekrar deneme kontrolü; otomatik dış sistemler/joblar yalnız onaylı kapsamla devrede |
| 13 | SMS devreye alma | Şablon/alıcı/grup sorumlusu/servis yetkisi onaylı; kuyrukta test simülasyonu gerçek gönderime çevrilmez. Gerçek credential Jenkins secret/ortam override; ayrı onayla SMS açılır. Mail/outbox da kontrolsüz tetiklenmez |
| 14 | İzleme | İlk çalışma günü API 5xx/SQL kilit-zaman aşımı, ödeme tekrarları, eksik dosyalar, SMS Pending/Unknown, mail ve senkronizasyon hata kayıtları izlenir; ertesi gün para birimi mutabakatı tekrar edilir |

### SQL uygulama disiplini

Şema scriptleri hazırlanmış olsa da henüz prova edilmemiştir. SSMS tüm klasörü seçip hatadan sonra devam ederek çalıştırmak yasaktır. Uygulama aracının ilk SQL hatasında durması gerekir (`sqlcmd -b -V 16` gibi); credential komut satırına yazılmaz. Her dosyanın manifest SHA'sı doğrulanır. Başarısız dosyada önce transaction/nesne/migration durumu yeni bağlantıyla okunur; kör tekrar/elle migration kaydı ekleme yoktur. Yedek/restore onayı olmadan `@Apply` açılmaz.

### Veri aktarımı — korunacak kararlar

- Donuk sözleşmeler donuk olarak gelir; ileri borç hesaplanmaz. Ücretsiz hizmette borç oluşmaz.
- YOK kayıtları eski karar kapsamına göre aktarım dışıdır; yeni sistemde sonradan YOK yapılan sözleşmenin geçmiş borcu korunur. Boş durum için onaylı kararlar ayrı taşınır; topluca VAR'a dönüştürülmez.
- Aynı isim/abone tekrarlarında müşteri kararı ve tarih kanıtıyla yalnız en yeni abonelik korunur. Elenen aboneliklerin ödemeleri MGS'de kalır; güncel karta devredilmez. Dosyalar için ayrı onaylı eşleme uygulanır.
- Excel madde 5/6/7 kararları kaynak kimlikleriyle yeniden uygulanır; sadece testte onaylanmış ID'leri kopyalamak yeterli değildir. Açık kayıtlar yeniden yorumlanmaz.
- MGS'nin cache Follow tablolarını finansal asıl veri diye kopyalamak yoktur. Yeni hesaplayıcı sözleşme/tarife/ödeme üzerinden çalışır.
- Referanslar CustomerGroup.Code, ServiceType adı/onaylı haritası, para birimi kodu ve tekil abone numarasıyla çözülür. G/A üst kartları ve cari ilişkileri mevcut Kurumsal/BANKA kayıtları ezilmeden planlanır.
- ÖdemeOperation, BankTransaction/Baseline, migration map ve silme makbuzları tekrar korumasıdır. Test baseline'ı güncel MGS yerine kullanılamaz; yeniden çıkarılır.
- Dosyalar mevcut CDN'de korunur; ortam/nesne sahipliği ve geçerli URL doğrulanır. SQL+CDN atomik değildir: belirsiz upload sonucu nesne silinmez, hash+receipt ile mutabakat yapılır.

## Geri dönüş

1. Sorunda modül/finansal yazma/SMS kapatılır; kullanıcı trafiği durdurulur. Kuyruk/DB/CDN kanıtları korunur.
2. Additive şema değişikliklerinde öncelik önceki BE/FE artefaktıyla uygulama geri dönüşüdür; eski sürümün yeni şemayla uyumu restore provasında doğrulanır. Başlangıç migration/seed etkisi hesaba katılır.
3. EF Down, DROP/TRUNCATE, makbuz silme veya test DB restore'u geri dönüş yöntemi değildir.
4. Finansal yazma başladıysa canlı DB'yi eski yedeğe kör restore etmek diğer modüllerin yeni verisini de kaybettirir. Önce zaman aralığı/etki listesi, point-in-time seçeneği ve kullanıcı onayı gerekir.
5. Legacy Joblar ancak yeni/legacy çift ödeme veya çift bildirim yaratmayacağı doğrulanarak önceki etkinlik durumuna döndürülür. CDN dosyaları topluca silinmez.

## Henüz hazır olmayan zorunlu parçalar

- [ ] Yeni canlı kaynak kesiti + karar/batch/field hash mutabakatı; eksik ServiceType ve G/A üst kart önerilerinin canlıya özel insert listesi.
- [ ] Canlı müşteri sınıflandırma önizlemesi + dar sp_SyncCustomers farkı; önceki değer/prosedür yedeği.
- [ ] Test-only araçlardan ayrı, canlı hedef/baseline/hash ve tekrar koruması doğrulanmış veri apply yolu (sözleşme/tarife/ödeme/not/fatura/grup/dosya/banka baseline).
- [ ] Onaylı menü/rol listesi ve aktarım audit kullanıcısı; test kullanıcı ID'si taşınmaz.
- [ ] K10 dış sistem sözleşme yaşam döngüsü / yeni GM sözleşmesi ve K11 grup bilgilendirmesi için yayın kapsamı/manuel geçici işletim kararı. Eski analizde açık; mevcut ekranlar bunların tamamlandığı kanıtı değildir.
- [ ] Job bazlı kesme/devam matrisi; güncel komut tanımları hassas ortamda incelenmeli. Mevcut salt-okunur Job raporu yalnız adım hash/zamanlama gösterir.
- [ ] İzole restore provası, şema/seed iki kez çalışma kontrolü, finansal ve CDN mutabakatı, geri dönüş provası, kullanıcı kabulü.

Bu işler kapanana kadar paket **çalıştırılacak final yayın paketi** olarak değerlendirilmez. Ana geliştirme kaydı: [module-development-plan.md](../../module-development-plan.md). Önceki hazırlık: [collection-production-transition.md](../../collection-production-transition.md).

## Hazırlık araçlarını tekrar çalıştırma

BE repository kökünde PowerShell 7 ile; bağlantı bilgileri komuta yazılmaz. Varsayılan Production JSON yerine Jenkins'in gerçekten kullandığı config/ortam override doğrulanır.

```powershell
# Her rapor için yeni dosya adı kullanılır; eski kanıt üzerine yazılmaz.
./tools/Read-CollectionProductionPreflight.ps1 -OutputPath .tools/live-preflight-YENI.json
./tools/Read-CollectionProductionPreflight.ps1 -SourceLegacy -OutputPath .tools/mgs-preflight-YENI.json

# Yeni, boş bir klasöre yalnız dosya üretir; DB bağlantısı/yazması yoktur.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- build .tools/live-preflight-YENI.json .tools/release-review-YENI

# Sabit SELECT sorguları; yalnız yerel özel klasöre veri/eşleme önizlemesi yazar.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- preview-live WebAPI/appsettings.Production.json .tools/live-capture-YENI
# Tamamen çevrimdışı tarife/ödeme/ilişki incelemesi; apply komutu yoktur.
dotnet run --project tools/Collections.Release/Collections.Release.csproj -- review-live .tools/live-capture-YENI .tools/live-financial-review-YENI
```

Anahtar, parola, müşteri listesi, snapshot veya yedek Git'e konmaz. Release SQL değişirse manifest hash'i de yeniden üretilir; eski hash ile uygulama yapılmaz. Ortak tanım ve seed taslakları restore üzerinde ayrıca denenmeden onaylı sayılmaz.
