# Tahsilat — prova sonrası kontrollü canlı geçiş

Durum: **inceleme paketi / NO-GO**. Bu belge veya paket oluşturulması canlı
uygulama onayı değildir. Bu adımda AssistFlow/MGS'ye yazılmaz; Job, Jenkins,
WebAPI, SMS, mail veya CDN çalıştırılmaz. Kaynak güncel MGS'dir; test/prova
finansal kayıtları veya üretilen kimlikleri canlıya kopyalanmaz.

## Paket ve kanıt

Dosya adaptörü eki: [ayrı araç, gerçek izole DB ve yerel sağlayıcı provası](cdn-adapter-rehearsal.md).
Dosya fazı mevcut finansal aktarıcıya upload eklemez. Tamamlanmış kabul ve
ayrı kod/plan/hash paketi olmadan dosya fazı canlıya açılmaz; gerçek public
CDN ve kullanıcı/API kabulü izole simülasyonla tamamlanmış sayılmaz.

Dosya operasyon eki: [Canlı CDN aktarımı ve erişim kabulü](cdn-transfer-and-access.md).
Paket/aday mutabakatı offline tamamlandı; upload/HTTP kabulü yapılmadı.
Finansal ilk replay attachment açılmadan tamamlanır; dosya aktarımı ayrı
adaptör/prova/onay gerektirir. Public bağlantı API yetkisiyle özel hale gelmez.

Güncel operasyon eki: [Job durdurma/devam, bakım ve yedek/geri dönüş](job-maintenance-and-recovery.md).
Hazırlık tamamdır; imzalı bakım/Job kararları, yeni yedek/restore kanıtı ve
son kesit henüz tamamlanmadı. Ekteki kapalı JSON uygulama yetkisi değildir.
Tüm Job'ları kapatma/açma veya canlı restore komutu hazırlanmadı.

Güncel ek kanıt: [production adaptörü yazmalı provası](production-adapter-rehearsal.md)
tamamlandı. Aynı `-02` kod/paket için teknik kabul vardır; aşağıdaki mühürlü
inceleme paketine sonradan eklenmemiştir. Bu kanıt canlı onay, bakım sonrası
taze kesit, başlangıç izolasyonu veya CDN/HTTP kabulünün yerine geçmez.

`seal-cutover` çevrimdışı komutu güncel canlı kesiti, karar/finansal inceleme,
veri planları, şema/seed ve gerçekleşmiş prova kanıtlarını tek özel `.tools`
klasöründe toplar. `bundle-hashes.json` bütün dosyaları, `manifest.json` ortamı
ve bağımlılıkları tanımlar. Kişisel veriler ve prosedür metinleri Git'e konmaz.
Config/credential paketlenmez. Fiziksel arşiv ve CDN nesneleri bu pakette yoktur;
yalnız dosya ilişkisi/hash envanteri vardır. Asıl arşivler güvenli saklanmalıdır.

Prova: 4.439 sözleşme, 17.269 tarife, 139.747 ödeme/makbuz; TRY 26.684.052,43
ve USD 115.320,76. İkinci çalışma 0 yeni kayıt; kesinti rollback'i ve dört
makbuz/audit koruma senaryosu geçti; CHECKDB temiz. Bunlar **prova sonucu**,
canlı hedefte uygulanmış kayıt veya son kesit sayısı değildir.

`rehearsal-delta.json`, kaynak ve canlı referans hash farklarını gösterir.
Hash farkı varsa yalnız sayılar aynı diye onay verilmez. Güncel eşleme yeniden
hesaplanır; değişen satırlarda eski müşteri kararı otomatik genişletilmez.
İki tur aynı hash **tablolar arası transactional kesit garantisi vermez**.

## Bağımlılık sırası ve kabul

| Sıra | İş | Geçiş koşulu / durma nedeni |
| --- | --- | --- |
| 1 | Yayın kapsamı ve onay | Açık istisnalar ile K10 dış sistem/K11 grup bilgilendirme kapsamı ve varsa manuel işletim iş birimince kabul edilir. Bekleyen kayıtlar zorla alınmaz. |
| 2 | Canlıya özel uygulayıcı | Prova aracının DB kilidi kaldırılmadan ayrı Collections.Production adapter hazırlandı; aynı kod/paketle yazma, rollback, tekrar ve dört koruma provası geçti. Teknik acceptance.json canlı onay kanıtına ayrıca bağlanır. **Mühürlü inceleme paketine importer sonradan eklenmedi; canlı uygulama yetkisi verilmedi.** |
| 3 | Ortak kayıt/prosedür | ServiceType ekleme, CustomerType sınıflandırma, G üst kart oluşturma ve stg.sp_SyncCustomers dar farkı ayrı imzalı onay alır. Kurumsal/BANKA kayıtları ezilmez, CustomerGroupId/TenantId değiştirilmez. Önceki değer/SQL hash'leri saklanır. |
| 4 | Operatör/rol | Onaylayan aktif User.Code ve yetki verilecek Role.Code listesi belirlenir. Teknik aktör 0 insan onayı değildir; Id=1 seçilmez. Menünün görüntüleme/yazma yetkileri ayrı doğrulanır. |
| 5 | Bakım ve Job matrisi | Kullanıcı yazmaları durur, BE kapatılır. `job-review-matrix.json` içindeki gerçek step/command hash bağımlılıkları incelenerek yalnız onaylı kaynak/hedef yazıcıları ve bildirimler durdurulur. Önceki etkinlik/zamanlama kaydı alınır; tüm instance/backup/ilgisiz modül Jobları kapatılmaz. Matris **onaysızdır**, durdurma komutu içermez. |
| 6 | Geri dönüş tabanı | Güncel canlı tam/log yedeği, erişim/restore kanıtı, eski BE/FE artefaktları ve config saklanır. Diğer modüllerin verisini de etkileyen restore/PIT kararı ayrıca onaylanır. Transaction rollback, tam DB geri dönüş provası yerine geçmez. |
| 7 | Son kesit ve yeniden paket | Yazıcılar gerçekten durduktan sonra MGS ve canlı referanslar tekrar okunur, tüm hazırlık zinciri yeni klasörlerde üretilir. Karar/para birimi/tutar/hash drift'i varsa yeniden inceleme; eski paket hash'iyle devam edilmez. |
| 8 | Şema/seed | Onaylı production manifestindeki sırayla SQL; her dosyada hash ve predecessor kontrolü, ilk SQL hatasında dur. Migration geçmişi elle düzenlenmez. Seed'ler onaylı parametreyle, rol kapsamı açıkça seçilerek uygulanır. Bu adım henüz yapılmadı. |
| 9 | Veri ve makbuz | Referans/üst kart → sözleşme/tarife → ödeme+makbuz → not/grup/fatura/cari/banka → dosya ilişkileri. Yeni canlı ID'leri üretilir. Paket dışı silme/üstüne yazma yok; eski abonelik ödemeleri MGS'de kalır. |
| 10 | DB mutabakatı | Kaynak→aday plan→hedef adet/hash; para birimi ayrı tutar; donuk/ücretsiz/boş/YOK; tarihçe, fatura tam ödeme zinciri ve bakiye, not/sahiplik, banka baseline/tekrar koruması ve dışlama nedenleri tutarlı olmalı. Tekrar 0 yeni kayıt. |
| 11 | Dosya/CDN | Mevcut CDN altyapısı, güncel sahiplik ve nesne/hash/MIME/boyut/güvenlik doğrulamasıyla ayrı onaylı aktarım. Eksik dosya sözleşmeyi silmez. Sahte URL üretilmez. Belirsiz upload nesnesi kör silinmez; SQL/CDN atomik kabul edilmez. |
| 12 | Jenkins BE | Kullanıcı main merge/deploy yapar. BE/FE jobları ayrıdır. BE öncesi aşağıdaki başlangıç yan etkileri kapısı kapanmalı. Doğru live DB, pending migration=0, genel seed sonrası farklar, servis logları ve kontrollü ilk sorgular doğrulanır. |
| 13 | Salt-okunur açılış / FE | CollectionRead.Enabled=true, ContractCreateEnabled=false; kullanıcı FE Jenkins jobunu çıkarır. Yetkili/yetkisiz, takip/detay/geri/filtre, Excel ve toplamlar, dosya erişimi ve API kontrolleri iş birimiyle yapılır. |
| 14 | Yazma/SMS ve izleme | Ayrı kabul sonrası finansal yazma açılır; sonra ayrıca SMS şablon/alıcı/grup sorumlusu onayıyla gönderim açılır. Simülasyon kayıtları gerçek gönderime dönüştürülmez. İlk gün ve ertesi gün mutabakat/5xx/SQL bekleme/mükerrer ödeme/bildirim hataları izlenir. |

## Kritik: BE başlangıcı salt-okunur değildir

**Güncel hazırlık:** [StartupExecution izolasyon ayarları](startup-isolation.md)
eklendi; iki otomatik migration yolu, genel seed ve Manitou worker ayrı ayrı
kapatılabiliyor. 13 dış bağlantısız kontrol geçti. Aşağıdaki önceki risk analizi
ayarlar verilmediğindeki varsayılan davranışı anlatır. Gerçek hosting süreci
kabulü ve yeni backend hash'leriyle yeni paket/prova hâlâ gereklidir.

- `Program.cs` genel migration/seed akışını başlatır. CollectionRead.Enabled=false
  bunu engellemez. Prova aracı WebAPI'yi başlatmadığından bu başlangıcın uçtan uca
  kabul edildiği iddia edilmez. Genel seed etkisi ayrı kontrollü ortamda doğrulanır.
- `ManitouStagingSyncBackgroundService.ExecuteAsync` ilk turu **beklemeden**
  çalıştırır. `ManitouRunEveryMinutes <= 0` durdurmaz; 60 dakikaya döner.
  CollectionRead veya SMS simülasyonu bu servisi durdurmaz. Mevcut kodda bu
  worker için önceki incelemede devre dışı bırakma bayrağı yoktu; artık
  `StartupExecution.ManitouSyncEnabled=false` ilk turu da engeller. BE açılışından
  önce bu override'ın gerçek proses tarafından alındığı doğrulanmalıdır;
  süreyi 0 yapmak, credential bozmak veya hataya güvenmek çözüm değildir.
- MailOutboxDispatcher, SLA, periodic report ve Helpdesk worker'ları da kayıtlı.
  Tahsilat nedeniyle ilgisiz modülleri global kapatmak yerine kendi mevcut
  koşulları ve bekleyen işler incelenir. SMS için AppSettings.CollectionSmsEnabled=false,
  AppSettings.SmsSimulationEnabled=true; CollectionRead.Enabled=false ve
  ContractCreateEnabled=false başlangıç için yalnız **Tahsilat** korumalarıdır.
- Yeni bayraklar yoksa ortak başlangıç davranışı aynıdır. Kontrollü açılışta
  StartupExecution.MigrationsEnabled/SeedsEnabled/ManitouSyncEnabled açıkça
  false verilmelidir; bu diğer worker'ları veya SQL Jobları kapatmaz.

## Geri dönüş / durdurma

İlk hatada sonraki dosyaya geçme. Açık transaction rollback'i ve yeni bağlantıda
durum incelemesi yapılır; makbuz/audit silinmez. Commit sonucu ağ nedeniyle
belirsizse aynı paketi kör yeniden çalıştırma; batch/RequestId/hash ile doğrula.
Bu senaryo henüz ayrıca fiziksel ağ kesintisiyle prova edilmemiştir.

Şema additive ise öncelikle onaylı eski BE/FE artefaktına dönüş değerlendirilir;
EF Down, DROP/TRUNCATE veya test/prova DB restore'u kullanılmaz. Canlı finansal
yazma başladıysa eski yedeğe dönüş diğer modüllerin yeni kayıtlarını da
kaybettirebilir: etki/PIT/onay olmadan restore yok. CDN'deki gerçek nesneler
topluca silinmez. Legacy/yeni otomasyonlar çift borç/ödeme/bildirim yaratmayacak
şekilde yalnız onaylı sırayla tekrar açılır.

## Teslim / onay kaydı

FE/BE main commitleri, Jenkins build numaraları, son paket SHA'sı, bakım başı/sonu,
operatör Code'u, Role.Code listesi, ortak fark onayları, Job önce/sonra durumları,
yedek/restore kanıtı, finansal/dosya mutabakatı ve iş birimi kabulü güvenli
yayın kaydına yazılır. Bu alanlar boşken `approvals.required.json` kapalı kalır.
Paketin JSON'unda `Approved=true` yazılması tek başına gerçek onay sayılmaz;
onay kaynağı ve yeni imzalı/hash'li paket gerekir. Bu hazırlıkta deploy/DB uygulaması yok.
