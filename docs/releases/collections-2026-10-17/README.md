# Tahsilat — prova sonrası canlı inceleme paketi

**Canlı CDN salt-okuma:** [Mevcut dosya erişimleri ve runtime yönlendirme sonucu](cdn-live-readonly.md).
PDF/PNG/JPEG örneklerinin üçünde R2/public 200 ve SHA-256/MIME/boyut eşit;
production API örneklerde beklenen uploads adresine 302 veriyor. Test öneki
uploads-test. Yeni Tahsilat dosyaları henüz canlıda yok; genel credential/
hosting eşitliği, PUT izni, pilot ve migration readback kabul edilmedi.
Canlı DB/CDN yazma, upload/delete/ayar değişikliği yok; onaylar kapalı.

**Dosya adaptörü devamı:** [Ayrı canlı dosya adaptörü ve izole prova kaydı](cdn-adapter-rehearsal.md).
Yeni `Collections.Files` mevcut R2FileStorage'ı kullanır; izole sağlayıcı HTTP
çıkışını reddeder. **Yazmalı kabul tamam:** 1.818 sözleşme ilişkisi +3 müşteri
dosyası /1.821 makbuz; tam replay 0 INSERT/0 PUT, 10 kontrol ve 15 finansal/
orijinal audit parmak izi eşit. CHECKDB temiz; 95 migration/24 Job adımı aynı.
Gerçek CDN/HTTP ve canlı yazma yoktur.

Ayrı teslim `.tools/collection-cdn-handoff-2026-10-17-01`: 153 dosya,
72.683.587 byte; manifest SHA-256
`B1A037D9A4515BFB5415DB01C99120BB46D5680B78E049D6FEE802035F1D2559`.
Kaynak/runtime/kapalı plan/prova/son profil içerir; gerçek config ve fiziksel
dosyalar dahil değildir. Asıl finansal teslim ve kaynak paketler değişmedi.

**CDN hazırlığı:** [Canlı dosya aktarımı ve erişim kabul planı](cdn-transfer-and-access.md),
kapalı onay/journal şablonu ve config/SQL/HTTP kullanmayan offline kontrol hazır.
Paket kanıtı: 1.818 sözleşme ilişkisi (1.100 yol) +3 müşteri dosyası aday;
69 eksik/19 boş dosya ve 12 sahipliği doğrulanmamış aktivite dosyası ayrı kalır.
Public URL modeli ve API yetkisi ayrıldı. Mevcut test aktarıcıları canlıya
açılmadı; ayrı adaptör/prova yukarıda tamamlandı, gerçek CDN kabulü açık kapıdır.
Bu adımda DB/CDN bağlantısı veya yükleme yok; mühürlü paket değişmedi.

**Job/bakım/geri dönüş hazırlığı:** [Durdurma/devam matrisi ve aşamalı kurtarma planı](job-maintenance-and-recovery.md)
hazır. 19 Job/24 adım üç sınıfta ele alındı; onay şablonu kapalıdır.
Salt-okuma ön kontrol SQL'i yalnız offline doğrulandı, çalıştırılmadı.
Gerçek bakım penceresi, Job başına onay ve güncel yedek/kurtarma kabulü beklenir.
Canlı DB/Job/yedek/restore/Jenkins işlemi yapılmadı; mühürlü paket değişmedi.

**Onay hazırlığı:** [Canlı operatör/rol ve ortak tablo/prosedür onay listesi](operator-and-shared-approvals.md),
kapalı karar JSON'u ve salt-okuma SQL ön kontrolü hazır. Gerçek operatör/rol
kodları tahmin edilmedi; hak veya ortak veri uygulanmadı. Servis/müşteri/G/üst
kart farklarında tek yazıcı mevcut aktarıcıdır; inceleme SQL'leri önceden
çalıştırılmaz. İmzalı karar ve son kesit kapıları hâlâ açıktır.

**Güncel devam:** [Gerçek izole WebAPI hostu ve backend paket yenilemesi](hosting-and-refresh.md).
Host kabulü özel prova ortamında geçti; eski kanıtlar korundu. Güncel finansal
prova/teslim durumu bu belgede izlenir. Canlı AssistFlow/MGS'ye yazılmaz.

**Aktif yeni sürüm teslimi:** `.tools/collection-cutover-handoff-2026-10-17-02`,
275 dosya; manifest SHA
`6BC34709D364D02DCD72F05F682A7C1D805690A88D8971375A98B313F2C55ABC`.
Gerçek host, yeni backend DLL'leri ve tamamlanmış yeniden yazmalı kabul birlikte
doğrulandı. Aşağıdaki `handoff-01` ve eski runtime'a ilişkin notlar tarihsel
teslimdir. Canlı son kesit ve insan/operasyon onayları hâlâ kapalıdır.

**Sonraki hazırlık:** [Backend açılış izolasyonu](startup-isolation.md) ve gizli
bilgi içermeyen override örneği hazır; iki migration yolu, seed ve Manitou
kontrolleri 13 bağlantısız doğrulamadan geçti. Gerçek hosting/izole süreç
kabulü yapılmadı. Aşağıdaki teslim eski runtime'a bağlıdır; bu yeni backend
sürümüyle yeni hash/plan/paket ve gerekli prova hazırlanmalıdır. Eski kanıtlar
ve kapalı canlı onaylar değiştirilmedi.

**Güncel devam:** [Canlı adaptörün yazmalı provası](production-adapter-rehearsal.md)
yalnız AssistFlow_Performance_Restore üzerinde tamamlandı: ilk aktarım,
rollback, sıfır-kayıtlı tekrar, dört koruma ve son CHECKDB başarılı.
Teknik kabul aynı kod/pakete bağlıdır; canlı authorization hâlâ kapalıdır.
Aşağıdaki mühürlü `-02` inceleme paketine sonradan dosya eklenmedi.

**Teslim devamı:** [Paket içeriği, kalan kapılar ve bakım günü sırası](execution-handoff.md).
Yeni offline teslim, orijinal inceleme paketini, provada çalışan runtime'ı,
ayrı kapalı canlı/prova planlarını ve yazmalı kabulü birlikte hash'ler.
Son bakım kesiti ve canlı uygulama onayı yerine geçmez.

Üretilen teslim: `.tools/collection-cutover-handoff-2026-10-17-01`, 258 dosya;
manifest SHA `AAC3F982035C99167F9AEF8446F4470B374442D09F8FAB4959D3CB084B5111D5`.
[Teslim/doğrulama kaydı](handoff-delivery.md). Bu son hazırlıkta DB bağlantısı yoktur.

**Durum: hazırlanmış / mühürlenmiş inceleme paketi; canlı uygulama onayı değil.**
AssistFlow ve MGS üzerinde yalnız SELECT çalıştı. DDL/DML, seed, Job,
restore/yedek, CDN/SMS/mail veya Jenkins/uygulama başlangıcı yapılmadı.
Frontend/runtime backend davranışı değiştirilmedi. Kullanıcının mevcut
`appsettings.Development.json` değişikliği korunmuştur, paketlenmemiştir.

Aktif özel paket: BE `.tools/collection-cutover-review-2026-10-17-02`.
`bundle-hashes.json` SHA-256:
`8253BCA52AED33271A445FE6B46EBEF7A86E3F5220C48239B54E7B6F12AFD673`.
106 dosya, yaklaşık 412 MB; tamamı yeniden hash'lendi, hata 0. Önceki `-01`
inceleme çıktısı tarihsel kanıttır; aktif teslim `-02`'dir. Hiçbiri uygulanmadı.
Veriler/prosedür metni kişisel ve hassastır; Git dışındadır. Arşiv dosyalarının
kendileri pakette yoktur, yalnız içerik/ilişki/hash envanteri vardır.

## Güncel salt-okunur sonuç

| Kontrol | Sonuç |
| --- | --- |
| MGS kaynak | 19.948 müşteri, 9.517 sözleşme, 34.588 tarihçe, 243.518 ödeme |
| Canlı hedef müşteriler | 18.888 toplam, 18.887 aktif; boş abone no 0 |
| Prova baseline'ına fark | 18.886 yerine 18.888 müşteri; canlı referans eşlemeleri yeniden hesaplandı |
| MGS kaynak hash'leri | Prova kesitiyle aynı; iki güncel okuma da sabit. Bu son bakım kesiti değildir |
| Canlı şema | 74 migration; collection yok, 21 Collection migrationı eksik; bekleyen ortak migration yok |
| Veri adayları | 4.439 sözleşme / 17.269 tarife / 139.747 ödeme |
| Ödeme toplamları | TRY 26.684.052,43 / USD 115.320,76; para birimleri birleştirilmez |
| Ayrı ortak veri onayı | 18 ServiceType ekleme, 2.602 mevcut müşteri tipi değişikliği, 56 grup üst kartı oluşturma önerisi |
| İlişkiler | 8.281 not, 26.228 grup geçmişi (+2 aynı tekrar audit), 1.813 fatura / 1.904 fatura ödemesi |
| Cari/banka | 13.790 cari kod: yalnız 2.891 net sahiplik; 83.543 banka kimliği/sorun 0 |
| Dosya | 1.818 sözleşme dosya ilişkisi +3 müşteri dosyası yerel hash kanıtı; gerçek CDN aktarımı yapılmadı |
| Bekleyen/dışlanan | 5.078 sözleşme / 103.771 ödeme; tamamı kesin ret sayılmaz, nedenleri pakette |

Önceki 794 müşteri kararı yeniden karşılaştırıldı; değişmemiş üç açık
literal-null tarihçe onayı korundu. Elenen aboneliklerin ödemeleri MGS'de
kalır. Tarihçe/donma/para birimi/kimlik belirsizliği tahminle çözülmedi.
Güncel canlı `stg.sp_SyncCustomers` tanımı değişmemiş:
`7E6F301164802E3DCA96C6A8291D31CF61B160A4DB41047AD6505A545D81315C`.
Dar fark `.txt` kanıtı ve kapalı müşteri/servis SQL taslakları pakette;
prosedür veya ortak veri uygulanmadı.

## Paket içeriği ve güvenlik

- `source/`, `review/`, `data/`: yeni **canlı** eşleme/karar ve ham kaynak
  planları. Prova hedefi capture'ı bu araca kabul edilmez.
- `schema/`: güncel canlı migration geçmişine göre yeniden üretilmiş 21 SQL;
  eski repository dışı migration silinmez. 21 migration +2 seed'in hedef guard
  dönüşümleri tekrar oluşturulup gerçekleşmiş prova SQL hash'leriyle birebir
  karşılaştırıldı. Eski prova, farklı SQL için başarı kanıtı sayılmaz.
- Üst düzey preflight/seed/postflight SQL'leri: salt-okuma kontrolleri ve
  varsayılanı `@Apply=0` kapalı tanım/menü/SMS seed'leri. Rol kodu NULL.
- `rehearsal-proof/`: hash'e sabitlenmiş gerçek apply/replay/rollback/koruma ve
  son CHECKDB kanıtları. Yeni prova verisi canlıya taşınmaz.
- `rehearsal-delta.json`: kaynak/canlı referans farkları; bu kesitte yalnız
  `live-customers.ndjson` değişti. Bu fark taze canlı referanslarıyla ele alındı.
- `job-review-matrix.json`: güncel job/step/hash/zamanlama envanteri; durdurma
  ve tekrar açma onayları false. İlgisiz Joblar otomatik kapatılmaz.
- `approvals.required.json`: operatör/rol/son kesit/ortak değişiklik/işletim
  kapıları boş veya false; `ProductionApplyAllowed=false`.
- `RUNBOOK.md`: [güncel geçiş sırası, BE başlangıç riski ve geri dönüş](cutover-runbook.md).

İç veri manifestindeki eski hazırlık `Gates` alanları muhafaza edilir; güncel
prova kabulü `rehearsal-proof/` ve üst manifesttedir. İnceleme dosyalarındaki
`ReadyToApply=false` kapıları otomatik kaldırılmaz. Paket **production importer
içermez**: JSON planı veya prova aracı canlıya çalıştırılacak komut değildir.

## Sıradaki bağımlılıklar

1. **Tamamlandı:** prova aracının kilidi kaldırılmadan ayrı canlı adaptör ve
   aynı kod/pakete bağlı yazmalı prova kabulü hazır. Yeni son kesit/paket/kod
   bu kabule otomatik dahil değildir; hash değişiminde yeniden prova gerekir.
2. Ortak müşteri/tanım/üst kart/prosedür farkları, aktif operatör User.Code ve
   menü Role.Code listesi için ayrı onay kayıtlarını tamamlamak.
3. BE başlangıcındaki genel seed ve hemen çalışan Manitou worker'ını kontrollü
   açma yöntemini netleştirmek/doğrulamak. Süreyi 0 yapmak worker'ı kapatmaz.
   Bu ortak davranış bu hazırlıkta değiştirilmedi.
4. Güncel Job adımları için kesme/devam matrisi, K10/K11 yayın kapsamı,
   production CDN güvenlik/erişim kabulü ve operasyonel backup/PIT geri dönüş
   kapılarını tamamlamak.
5. Onaylı bakımda yazıcılar durduktan sonra **yeniden son kesit ve yeni paket**;
   sonra şema → veri → DB mutabakatı → kontrollü Jenkins BE → FE/salt-okuma
   kabulü → ayrı onayla finansal yazma/SMS. Henüz bu adımlar uygulanmaz.

## Yeniden üretim ve doğrulama

BE kökünde güncel `Collections.Release` derlenmelidir; eski DLL'ye güvenilmez.
Hazırlık komutları [araç README'sinde](../../../tools/Collections.Release/README.md).
Her denemede yeni klasör/dosya adı kullanılır. Bu teslim için zincir:

`live-preflight-01 + mgs-preflight-01 → live-capture-02 → live-decisions-02 →
live-review-02 → live-payments-02 + live-relations-02 → live-data-01 +
live-schema-01 → cutover-review-02` (tam özel isimler `.tools/collection-...-2026-10-17-...`).

`seal-cutover` çevrimdışıdır. Önce tüm kaynak/paket/prova hash'lerini ve canlı
hedefini kontrol eder, sonra dosya üretir. Prova capture'ıyla olumsuz kontrolde
reddetti ve çıktı klasörü oluşturmadı. Build **0 hata /0 uyarı** (son artımlı
derleme); ilk bağımlılık derlemesindeki mevcut uyarılar ayrıca devam eder.
FE değişmediği için FE build/lint çalıştırılmadı. Production uygulaması yok.
