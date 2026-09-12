# Goal

AssistFlow CRM altında legacy Tahsilat Takibi ve sözleşmeleri, mevcut müşteri alışkanlıklarını koruyarak güvenli, hızlı ve geliştirilebilir biçimde taşımak. İlk faz tahsilat takibi/işlemleri ve veri aktarımıdır; zam ve kapsamlı raporlama sonraki fazdır. Production hazır sonucu ancak veri mutabakatı, kullanıcı kabulü, yetki ve yük testleri tamamlanınca verilir.

Bu dosya ana geliştirme görev kaydıdır. Ana agent işleri bağımlılık sırasıyla yürütür; parça sonlarında tekrar devam onayı istemez. Güncelleme: 10 Eylül 2026. Güncel müşteri kararları: [10 Eylül muhasebe cevapları](accounting-decisions-2026-09-10.md). Çelişen eski teknik varsayımlardan önceliklidir.

# Requirements

- FE React/TypeScript/Vite, BE mevcut Model/Data/Business/WebAPI katmanları korunur. Mevcut ortak API, repository/UnitOfWork, audit, CDN, UI, hata ve store yapıları kullanılır. DI yalnız AutofacBusinessModule içinde.
- Yalnız module ait yeni tablolar/tanımlar `collection` şemasında. Mevcut dbo.Customers, ServiceType, CustomerType, CurrencyType ve diğer ortak tanımlar çoğaltılmaz. Ortak tablolarda değişiklik için kullanıcı onayı gerekir.
- Tenant yetkisi/ilişkisi/filtresi yok; kimlik doğrulama ve menü/işlem yetkileri zorunlu. Contract.ServiceTypeId mevcut ServiceType'a gider; Product bağı yok.
- SubscriberNo → SubscriberCode iş anahtarıdır. Çift/eksik eşleşme otomatik seçilmez; müşteri otomatik yaratılmaz. Eski sözleşmeler ve ilişkili geçmiş kayıpsız, tekrar çalıştırılabilir aktarılır.
- Bireysel/grup davranışları korunur. CustomerType N/GM/A/G eşlemesi; BANKA mevcut kaydı bozulmaz. ServiceType BAKIM→mevcut Bakım; diğer isimlerde onaylı map, fuzzy otomatik birleştirme yok.
- Donuk ve ücretsiz dönemde yeni borç yok; aktifleşmeden sonra tam dönem ücret, donuk döneme geriye borç yok. Önceki borçlar/ödemeler silinmez. Yeni başlangıç gerçek gün, aynı-gün yenileme, kıst yok; legacy bilinmeyen gün uydurulmaz.
- Zam geçmiş borç/ödemeyi değiştirmez. Çalışma kararı: dönem içi zam ek borç yaratmaz, sonraki yenilemede uygulanır. Fiyat yürürlük tarihi ile yenileme ankrajı ayrıdır.
- Kısa ayda son gün, sonraki ay asıl güne dönüş. Yeniden aktifleşme yeni ankrajdır. 1/2/3/4/6/12/24/36 ay desteklenir; üç aylık tutar üç ayda bir tam tutardır.
- VAR/YOK/boş tahsilata dahil olma durumuyla ilgilidir; değer bazlı matris müşteri cevabında açık olmadığı için netleşmeden filtre kodlanmaz. Kart bilgileri ve online POS entegrasyonu mevcut kapsam dışıdır. Tarihsel ödeme yöntemi eşlemesi veri kaybını önlemek için korunur. Ücretsiz yöntem/tarihçe otomatik eşdeğer sayılmaz.
- Manuel finansal düzeltmede fiziksel silme; ters kayıt yok. Yetki, transaction, audit, concurrency ve retry kontrolü zorunlu. Grup operasyonel etiketi finansal borçtan ayrıdır.
- Kullanıcı metinlerinin tamamı Türkçe. Teknik exception/SQL/credential kullanıcıya sızmaz.
- Server pagination/sort/filter, sınırlı projection, stabil sıra, cancellation. Sorgula öncesi liste isteği yok; detay/sekme/özet ihtiyaç anında. Büyük veri performansı ölçülür, varsayılmaz.
- Her iki repo `tahsilat-module`; başka branch'e merge yok. Mevcut değişiklikler korunur; production/legacy DB'ye yazma, seed/migration çalıştırma, dosya silme gibi riskli operasyonlar ayrıca onaya tabidir.

# Completed

- Son transaction dilimi doğrulaması: **88 model/sorgu/API/DI/işlem kontrolü geçti**, BE build **0 hata / 725 uyarı**. Beş yeni test requestId, actor, payload, inactive model ve cancellation sınırlarıdır; gerçek transaction gövdesi SQL'de çalıştırılmadı. Veritabanı/seed/host açılmadı. Aktif yazma endpoint'i yok.

- P07 transaction persistence ilk dilimi: CollectionPaymentTransaction, mevcut Repository aynı AppDataContext ile kullanılarak serializable transaction içinde payment save + audit/receipt save + commit yapar. EF execution strategy delegate içinde transaction oluşturulur; rowversion conflict ve unique RequestId yarışı Türkçe conflict döndürür. Erken dönüş/hata transaction dispose ile rollback olur; kendi takip ettiği payment/receipt detach edilir. Replay işlem makbuzudur, güncel ödeme varlığı/bakiyesi iddiası değildir. DI/endpoint kaydı yok; finansal uygunluk/yetki command servisi tamamlanmadan çağrılmayacak.

- Son doğrulama (10 Eylül): **83 model/sorgu/API/DI/işlem içeriği kontrolü geçti**, backend build **0 hata / 47 uyarı**. Yeni snapshot/hash testleri gerçek transaction veya eşzamanlı request testi değildir. Önceki 76 offline kural paketi bu dilimde değiştirilmedi. DB/host/seed çalıştırılmadı.

- 10 Eylül bağımsız güvenlik dilimi: typed CollectionPaymentCommand, schemaVersion=1 canonical SHA-256 hash ve açık audit snapshot projection eklendi. Create/update/delete alanları, decimal(18,2) sınırı, muhasebe ayı, 8-byte rowversion doğrulanır. Tutar ölçeği ve kültür farkı hash'i değiştirmez; diğer alan farkları korunur, açıklama trim edilmez. RequestId anahtarı ve actor hash dışında ayrı doğrulanır. Hash istemciden alınmaz. Model kuralları DB yetkisi/finansal uygunluk anlamına gelmez; negatif değerler teknik olarak temsil edilebilir, yeni manuel işlem politikası komut servisinde belirlenecek.

- P03 audit/receipt model dilimi: `collection.PaymentOperation`, global unique RequestId, actor, işlem türü, SHA-256 için 32 byte payload hash, ödeme kimliği, önce/sonra JSON ve tamamlanma zamanı. Ortak genel idempotency/audit karşılığı bulunmadı; Helpdesk TicketHistory kendi modülüne bağlı ve ödeme silinmesi sonrası kalıcı receipt ihtiyacına uygun değil. Mevcut BaseEntity/EF konvansiyonu kullanıldı. Ödemeye FK yok, fiziksel silme receipt'i cascade silmez. Create/update/delete snapshot şekilleri ve JSON/hash yapısı için check taslağı eklendi. Bu tablo henüz aktif modelde değil.
- P03 saf replay kontrolü: aynı kullanıcı+aynı 32-byte hash tekrar yanıtına aday; farklı kullanıcı/içerik reddedilir. Yetki ayrıca her istekte kontrol edilecek. Replay eski ödeme snapshot'ını otomatik güncel sonuç gibi döndürmez; command servisi kayıtlı işlem sonucu ve mevcut silinmişlik durumunu açıkça ele alacak.

- P02 sabit kaynak tanım eşlemeleri eklendi: PaymentMethod, SubscriptionStatus, ContractStatus, GroupStatus kaynak kimliği → kod. Hedef DB kimliği uydurulmaz; bilinmeyen kimlik null, boş sözleşme durumu kaynak Id14→UNKNOWN. LEGACY_FREE yalnız kaynak ödeme yöntemi kodudur, tarihli ücretsiz davranışa dönüşüm değildir. Banka canonical map ve seed uygulaması hâlâ ayrı işler.
- P03 ilk model dilimi: collection.Payment, GroupStatus ve ContractPeriodFollowUp. Payment mevcut BaseEntity/ITimestamped/IAuditedByUser kullanır, ISoftDeletable kullanmaz; açık CurrencyType FK, decimal(18,2), nullable 1000 karakter açıklama, rowversion, nonunique dönem indeksi. Negatif eski iadeler temsil edilebilir; yeni manuel giriş yetkisi/validasyonu ayrıca yapılacak. Müşteri Contract üzerinden çözülür, Payment üzerinde ikinci müşteri kaydı yok.
- Grup takipte opsiyonel durum, kaynak kapasitesi 500 karakter açıklama, rowversion ve aktif contract/month unique indeksi; ödeme FK veya otomatik finansal aksiyon yok. Silme/grup etiket davranışı mevcut kararlar doğrultusunda ayrı komut olacaktır.
- Güncel doğrulama: **72 offline kontrol + 64 model/sorgu/API/DI kontrolü**, BE build **0 hata / 47 uyarı** (artımlı). Testte nullable kimlik method-group eşlemesi derleme hatası verdi, açık lambda ile düzeltilip tekrar geçti. Aktif context, migration ve DB değişmedi.

Kod varlığı ile entegrasyon/kabul tamamlanması farklıdır:

- Legacy kaynak, DB profilleri, ekran/işlem analizi: workspace `../docs/tahsilat/faz-0` altında 01–18 belgeler. Bu yol workspace köküne göredir; belgeler şu an BE git kapsamı dışında.
- Ayrı branch'ler, sözleşme ve PaymentFrequency taslak model/configuration, aktif model dışında metadata testleri.
- Takvim, çakışma, aday para birimi, ödeme sıklığı eşleme, izole tahakkuk hesabı; önceki son çalışmada 67 offline kontrol.
- Sözleşme read query/service/controller; ortak repository/envelope/DI, default-disabled flag ve missing-model 503. Query SQL çevirisi ve DI kontrolleri mevcut; önceki paket 42 kontrol.
- FE `/crm/collections/contracts` rotası, yetki sınırı, Sorgula/list/detail, ortak bileşenler/ApiService/parseApiError/Zustand; menü kaydı yapılmadı.
- Türkçe annotation/başarı mesajları ve collection kapsamlı model-binding filtresi eklendi; son dilimin regresyon testi güncellenecek.
- Önceki FE build/lint ve BE build başarılı. Bu, HTTP entegrasyonu, TypeScript projesinin tamamı veya yük testi başarısı değildir.
- P01 model/configuration dilimi: collection.ContractRatePeriod, BillingAnchor, aynı Model enum'u, NoAction referanslar, decimal(18,2), koşullu ücretli zorunluluk ve aktif dönem indeksleri. 67 offline + 48 model/sorgu/API/DI kontrolü geçti; build 0 hata / 47 uyarı (artımlı). Migration uygulaması yok. Kaynak açıklama profili ve aktif model entegrasyonu ayrı işlerdir.
- P02 model dilimi: Bank/PaymentMethod/SubscriptionStatus/ContractStatus collection tanımları, opsiyonel Contract FK'leri ve kart son kullanma date alanı eklendi. Seed/map/UI henüz yok; P02 tam bitmiş değildir. Aynı BaseEntity/EF configuration konvansiyonu kullanıldı, yeni ortak generic model tabanı kurulmadı. Mapping/ad normalizasyonu ayrıca doğrulanacak.
- P15 Türkçe doğrulama: framework İngilizce binding hatasının Türkçe ResponseModel'e çevrilmesi, geçerli isteğin geçmesi, annotation ve filter order kontrolleri eklendi. Test kurulumundaki ValidationContext instance hatası düzeltilip yeniden çalıştırıldı.
- Son doğrulama: **67 offline kontrol; 58 model/sorgu/API/DI kontrolü; BE build 0 hata / 47 uyarı**. Uyarılar artımlı derleme sayısıdır, baseline iyileşmesi iddiası değildir. DB bağlantısı/host/migration yok.
- P08 yenileme dilimi: FE Sorgula/table göstergesi isValidating kullanıyor; yenileme sırasında tekrar submit engelleniyor. Dosyaya özel ESLint başarılı; tarayıcı kabulü henüz yok.

# In Progress

- **11 Eylül iş kararı güncellemesi:** kullanıcı VAR=dahil / YOK=hariç ilişkisini açıkladı ve kalan sınırlar için teknik karar yetkisi verdi. Tam 15 mevcut ay; boş/UNKNOWN yeni borç üretiminden hariç; 16+ başlangıçta ilk borç sonraki ay aynı gün, orijinal gün ankrajı korunur. Ayrıntı ve müşteriye iletilecek metin `accounting-decisions-2026-09-11.md`. Önceki “muhasebe cevabı bekleniyor” notları bu konularda geçersizdir; kod/test entegrasyonu P05/P06/P15 görevlerine dahildir. Bu karar kaydı DB veya geçmiş borçları değiştirmedi.
- **11 Eylül son doğrulama:** çift/sahipsiz ödeme sayımı dahil **20 gerçek SQL kontrolünün tamamı geçti**; son test çalışmasının kayıtları temizlendi. Aşağıdaki 19 kontrol ve tekrar bekleme notu ara çalışma kaydıdır; tekrar tamamlandı. `git diff --check` başarılı. Bu dilimde üretim kodu değiştirilmedi, test projesi derlenip gerçek DB'de çalıştırıldı.
- **11 Eylül — P07 SQL kabul dilimi:** yeni `tools/Collections.Sql.Tests` gerçek AssistFlowTest'te çalıştırıldı. İlk 19 kontrol başarılı: rollback, rowversion, aynı/farklı payload ve kullanıcı, bariyerle eşzamanlı yazma, commit sonrası yanıt kaybı simülasyonunda retry, silme ve eski create replay. Teste ait sözleşme/ödemeler/makbuzlar finally ile temizlendi; ortak tablolara veya legacy'ye yazılmadı. Identity sayaçları normal olarak ilerledi. Son ek ödeme sayımı kontrolüyle toplam 20 kontrolün tekrar çalıştırılması izleniyor. Bu sonuç HTTP yetki/finansal eligibility veya gerçek ağ failover kabulü değildir. Sıradaki bağımsız iş: tanım seed/map ve command authorization entegrasyonu; muhasebe belirsizlikleri etkinleştirilmez.
- **Güncel durum — P04 temel kurulum tamamlandı:** `20260910150310_AddCollectionFoundation` AssistFlowTest üzerinde tek transaction ile uygulandı. 10 collection tablosu, 21 ikincil indeks, 12 NoAction FK, 14 etkin/güvenilir CHECK doğrulandı. Bağımsız bağlantıda tabloların tamamı boş ve migration kaydı mevcut. Aktif AppDataContext artık collection modellerini içeriyor; önceki “model kaydı yok/DDL yok” notları tarihsel durumdur. Ortak tablolar ve önceki migration kayıtları değişmedi. Seed, legacy aktarımı ve finansal endpoint aktivasyonu yapılmadı.
- Doğrulama: **94 model/sorgu/API/migration kontrolü + 76 offline iş kuralı kontrolü başarılı; solution build 0 hata / 47 uyarı** (artımlı). Migration Up SQL'i modül-only model farkıyla birebir eşit; target model aktif modelle uyumlu. Sonraki bağımlılık: gerçek SQL transaction/rowversion/idempotency kabul testleri ve tanım seed/map hazırlığı. Açık muhasebe kuralları hâlâ aktive edilmez.
- VPN sonrası salt-okuma preflight başarılı: hedef `AssistFlowTest`, `collection` tabloları henüz yok; CREATE TABLE/CREATE SCHEMA yetkileri mevcut. `dbo.Customers`, `dbo.ServiceType`, `dbo.CurrencyType` Id alanları bigint NOT NULL. Ağ engeli kalktı; önceki erişim hatası tarihsel kayıttır. Henüz DDL/veri yazılmadı.
- Migration geçmişi farkı: DB'de olup yerel migration dosyalarında olmayan kayıtlar `20251124200930_FixCustomerDuplicateColumn`, `20260619194553_addPriorityDefionationTable`, `20260705204551_addWorkOrderToArchive`, `20260905071247_AddServiceRequestServiceTypes`. Yerelde olup DB'de eksik kayıt saptanmadı. Bu kayıtlar silinmeyecek/değiştirilmeyecek; körlemesine database update veya ortak şema düzeltmesi uygulanmayacak. Sıradaki P04: collection kapsamlı migration/script üretimi, gerçek hedef FK/şema önkoşulu denetimi ve kontrollü kurulum.
- Son kullanıcı yetkisi: AssistFlow hedefinde tahsilata özel gerekli tablolar `collection` altında oluşturulabilir. Legacy yazma, ortak dbo tablo/veri değişiklikleri ve production veri aktarımı bu yetkinin dışında kalır. Development yapılandırmasının hedefi `192.168.1.8 / AssistFlowTest`; ilk salt-okuma bağlantı kontrolü erişim hatası verdi. Fiziksel tablo ve migration geçmişi henüz doğrulanamadı; DDL uygulanmadı.
- P04 güvenlik kontrolü eklendi: EF relational model farkının tamamı yalnız `collection` EnsureSchema/CreateTable/CreateIndex işlemlerinden oluşmalı; tam 10 tablo, cascade olmayan FK ve transaction dışı komut bulunmaması test edildi. **92 model/sorgu/API kontrolü geçti**. Bu sonuç gerçek hedef şema uyumluluğu veya uygulanmış migration anlamına gelmez.
- Transaction kodunun gerçek SQL rollback/commit-belirsizliği/retry/yarış/rowversion testleri izole DB kurulumu onayına bağlı. Offline guard testleri bunların yerine geçmez. Sonraki kod işi aynı transaction içinde müşteri/sözleşme/para birimi/dönem uygunluğu ve command authorization entegrasyonu; belirsiz başlangıç/VAR-YOK finansal eligibility kuralları aktive edilmez.

- 10 Eylül: başlangıç/VAR-YOK açık maddeleri korunarak audit/idempotency geliştirmesi sürdürüldü. Ödeme ve makbuzun aynı transaction'da uygulanması sonraki dilim; doğrudan write endpoint veya DI kaydı eklenmedi. Snapshot serializer yalnız ödeme alanlarını içerir, navigation veya ham request serialize etmez. Güncel 15'i kesme kuralı belirsizken borç üretimi aktive edilmeyecek.

- 10 Eylül cevaplarıyla kart/banka taslağı çıkarıldı. 15'i kesme kuralında tam 15 ve ilk borç/yenileme günü ayrımı, VAR/YOK/boş dahil olma matrisi netleşmeli. P05/P06 başlangıç/dahil olma hesabı bu açıklamaya bağımlıdır; bağımsız audit/idempotency işleri devam edebilir.

- P00 repository/karar denetimi ve FE bağımsız incelemesi tamamlandı; görev kaydı oluşturuldu.
- Sıradaki parça P03 typed komut içeriğinin kanonik hash üretimi ve P07 ödeme/receipt/audit tek transaction servis taslağı. Unique indeks yarışı, rollback ve fiziksel silme retry testi gerçek izole DB kabulüne dahil. Audit modeli tek başına immutability veya exactly-once garantisi sağlamaz. P02 banka canonical map, seed taslağı ve kaynak uzunluk kontrolleri açık.

# Todo

Her satırın bitişi kanıt/test ile kaydedilir. Bağımlılık yoksa güvenli bağımsız iş paralel yürütülebilir.

| ID | İş / bağımlılık | Kabul kriteri |
| --- | --- | --- |
| P00 | Denetim ve plan | Gerçek git durumu, eksikler, test sınırları, eski karar çelişkileri kayıtlı |
| P01 | Tarife modeli / onaylı kurallar | collection şeması, doğru FK/decimal/date, ankraj ayrımı, overlap guard, model testleri |
| P02 | Sözleşme tanımları / P01 | PaymentMethod/Status/Bank mevcut karşılıkları tekrar kontrol edilmiş; ortak tablo değişmeden kayıpsız alanlar, bank canonical map |
| P03 | Finansal ödeme/grup takip/audit/idempotency modelleri / P01 | Fiziksel silme güvenliği, currency ve dönem bağı, concurrency, cascade yok, unique işlem anahtarı, Türkçe hata |
| P04 | Tamamlandı: aktif EF temel modeli ve kontrollü AssistFlowTest kurulumu / P01–03 | Yalnız collection DDL; migration SQL/model eşitliği, 10 tablo/21 indeks/12 FK/14 CHECK ve boş veri doğrulandı; runbook docs/collection-foundation-installation.md |
| P05 | Sözleşme CRUD/tarife/dondurma/aktifleşme / P01–04 | Açık komutlar, geleceğe etkili değişiklik, tarihçe korunur, tek transaction/concurrency, kayıtlı borç yeniden yazılmaz |
| P06 | Tahsilat takip sorgu API / P03–05 | N/grup görünümü, tüm legacy gerekli filtreler, currency bazlı toplam, aynı filtre count/list/export, Türkçe hatalar |
| P07 | Tek/toplu ödeme ve düzeltme / P03–06 | Kısmi ödeme, retry çift kayıt yok, stale tutar kontrolü, fiziksel silme audit, yetkisiz komut reddi, etiket bağımsız |
| P08 | FE sözleşme ekranını tamamla / P02,05 | Ortak lookup/çeviri/form/state, cancellation, erişilebilirlik, listeye dönüş, eşzamanlı değişiklik mesajı, görsel kabul |
| P09 | FE tahsilat/grup ekranı / P06–07 | Legacy Sorgula, lazy detay/sekmeler, gereksiz istek yok, bakiye/etiket ayrımı, yüklenme/boş/hata ayrımı |
| P10 | Dosya/CDN / P03,05 | Mevcut IFileStorage; sözleşme/dosya erişimi, metadata pagination, boyut/tür kontrolü, eski dosya doğrulaması |
| P11 | Export/isteğe bağlı özet / P06 | Ortak export/job varsa kullan; sayfa değil tüm filtre sonucu, sınırlı bellek, kullanıcı erişimi ve iptal |
| P12 | Migration staging/map/istisna kodu / P01–04 | Kaynak audit ve ham değer kayıpsız; hash/idempotency, duplicate müşteri/kur/overlap karantina; silineni retry yaratmaz |
| P13 | Salt-okuma eksik profil / P12 | Currency aday/view farkı, tekil issue sayısı, collation, müşteri silinmişlik, açıklama uzunluğu, dosya/bağlı kayıt profili |
| P14 | İzole aktarım provası / P12–13 + ortam onayı | Sabit veri kesiti, contract/history/payment/grup sıra; currency bazlı tutar ve sayım mutabakatı, rollback provası |
| P15 | HTTP yetki/güvenlik/Türkçe regresyon / P04–11 | Anonim/izinsiz/okuma/yazma matrisi; bağlama/validation/exception Türkçe; veri sızıntısı yok; startup seed çalıştırmadan test host |
| P16 | Performans / P06,09,14 | Temsilî büyük veri, query plan/index, p95 gecikme/bellek/DB çağrı sayısı, derin sayfa ve eşzamanlılık; hedefler ölçümle somutlaştırılır |
| P17 | Production geçiş / P14–16 + onay | İş birimi kabulü, erişim/backup/rollback/izleme/runbook, modül açık ayarı ve veri yükleme ayrıca onaylı |
| P18 | Sonraki faz zam/rapor | İlk faz kabulünden sonra mevcut legacy iş kapsamı; geçmiş ödeme değişmez |

# Decisions

- Mevcut Program IRepository kaydı ActivatorUtilities ile ayrı AppDataContext üretir. Inject edilen UnitOfWork ve başka inject context'i aynı transaction sanmak hatalı olur. Ortak global DI değiştirilmeden transaction primitive'i mevcut Repository sınıfını transaction'ın context'iyle oluşturur; aynı-context yazılar korunur. Bu desen mevcut Helpdesk doğrudan context transaction kullanımına uygundur. Yeni genel repository/transaction framework kurulmadı. Temiz/dedicated context zorunludur, ambient veya tracked kullanıcı değişiklikleriyle çalışmaz.

- 10 Eylül revizyonu: aşağıdaki ve Completed bölümündeki banka/kart korunması kayıtları tarihsel çalışma kaydıdır, artık aktif gereksinim değildir. P02 banka canonical map/seed işi mevcut kart kapsamıyla birlikte iptal edildi. “Yeni başlangıç gerçek gün” yalnız imza tarihinin korunmasını ifade eder; imza günü ilk borç tarihi olarak kullanılamaz. İlk borç kuralı 15'i kesme açıklamasına göre ayrıca netleştirilecek. Eski 18 numaralı karar belgesi yeni müşteri yanıtının önüne geçmez.

- PaymentOperation yalnız başarıyla commit edilen işlem makbuzudur; ödeme mutation ile aynı transaction'da yazılmalıdır. Paralel aynı anahtar yarışında unique ihlali sonrası transaction geri alınır ve receipt yeniden okunur; henüz uygulanmadı. PayloadHash gelecekte operation kind, bütün finansal alanlar, expected rowversion ve canonical şema sürümünü kapsamalı; istemciden hazır hash kabul edilmez. Typed before/after snapshot yalnız ödeme alanlarını içerecek; credential/kart verisi/log ham request tutulmaz. Audit retention ve silinmiş kaynak mapping tombstone ile birlikte davranış P12'de tamamlanacak.

- Payment ve grup takip Period alanı muhasebe ayının ilk günüdür; gerçek yenileme günü değildir. Aynı ay içinde reaktivasyon nedeniyle birden fazla geçerli tahakkuk olursa aylık görünüm bunları toplar; bu durum ayrı döneme otomatik ödeme dağıtımı getirmez. Tahakkuk DueDate bilgisi ayrı tutulacak, migration ham ay/yılını kaybetmeyecek. Para birimi uyuşmayan aylık kayıtlar birleştirilmeyecek.
- Fiziksel silmeye uygun entity, güvenli silme iş akışı tamamlandı demek değildir. Audit, idempotency tombstone ve transaction P03/P07 tamamlanmadan Payment generic CRUD'a veya aktif EF modele açılmaz.

- FE denetimi: parseApiError içindeki data.message dalı network/timeout çevirisinden önce; Türkçe gereksinimi için ortak helper'da dar kapsamlı ve regresyon testli düzeltme P15'e eklendi. DataTable local sort ile store sorgusu remount sonrası ayrışabilir; geriye uyumlu controlled-sort desteği P08 kabulüne dahil. Aynı sorgu mutate sırasında isValidating kullanılmalı. AuthProvider zaten SWR cache temizliyor, ikinci temizleyici eklenmeyecek.
- P01 BillingBehavior enum'u Model katmanına taşındı; hesap ve persistence aynı enum'u kullanıyor. Aktif dönemlerde unique ContractId/EffectiveFrom ve tek açık dönem indeksleri var; bunlar kapalı aralık çakışmalarını tek başına engellemez. Aralık kontrolü ve transaction P05 kapsamındadır.

- Güncel muhasebe kararı 18 belgesidir; eski ayın ilk günü zorunluluğu yeni kayıtlar için geçersiz. Geçmiş verinin gün hassasiyeti ayrı korunur.
- ORM metadata/ToQueryString testleri DB constraint veya SQL performans kanıtı değildir. 503 model guard fiziksel tablo varlığını kanıtlamaz.
- Generic CRUD yazma action'ları nedeniyle read controller'da devralınmaz; ortak ResponseModel/MenuAuthorize/DI korunur.
- Güncel denetimde iki repo origin/tahsilat-module izliyor, eski dosyaların çoğu commit edilmiş; mevcut yerel değişiklikler korunacak. Önceki “hiç commit yok” notları tarihsel kayıttır. Bu agent bu tur commit/push/merge yapmadı.
- Eksik alanların şimdilik modelde olmaması kaynak verisini atma kararı değildir. Kullanılabilir olmayan endpoint default kapalı kalır; mock veri gerçek veri gibi gösterilmez.

# Risks / Open Questions

- Yeni müşteri yanıtında ciddi hesap farkı doğuran belirsizlikler: tam ayın 15'i; 28'inde imzada sonraki ayın 1'i/28'i veya sadece rapor ayı ayrımı; “ikisini de” ifadesi; VAR/YOK/boş dahil olma matrisi. Bunlar tahminle kapatılmayacak. Ayrıntı 10 Eylül karar belgesinde.

- AssistFlow tahsilat tablolarının kurulumu artık yetkili; hedef bağlantı ve mevcut şema doğrulaması uygulama önkoşuludur. Production geçişi, gerçek veri aktarımı ve ortak tablo değişiklikleri ayrıca onaylı olmalıdır.
- GBP veya ortak Customer/ServiceType/CustomerType alan/veri değişiklikleri kullanıcı onayına tabidir; mevcut genel özerklik bunu kaldırmaz.
- AsOfDate işlem tarihi ile kayıt zamanı ayrımı ve geçmiş rapor anlamı net değil. Canlı finansal rapor açılmadan gerçek senaryoyla karar gerekir.
- Legacy gerçek başlangıç günü ve dondurma geçmişi eksik olabilir; bugünkü durumdan geçmiş uydurulmaz. Currency adaylarında belirsizlik var; tek aday aktarım onayı değildir.
- Banka alanlarının korunması ve dönem içi zam/aktifleşme yorumları çalışma kararıdır; muhasebeye iletilecek not 18'de, değiştirilirse etkili tarih/göç değerlendirilir.
- FE tam type-check, görsel kabul ve otomatik kullanıcı akışı henüz tamamlanmadı. Mevcut bağımlılık/build uyarıları ayrı sınıflandırılmalı; ilgisiz toplu refactoring yapılmaz.
- Tüm P maddeleri bitmeden “production hazır” denmeyecek. Riskli işlemi engelleyen sınır, bağımsız güvenli işlerin durması anlamına gelmez.
