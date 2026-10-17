# Canlıya özel veri aktarıcısı — 17 Ekim

**Güncel durum:** bağlantılar kapatıldıktan sonra aynı adaptörün yazmalı provası
tamamlandı; rollback, ilk aktarım, tekrar, dört koruma ve CHECKDB başarılı.
[Yeni yazmalı kabul ve hash'ler](production-adapter-rehearsal.md).
Canlı **NO-GO** korunur. Aşağıdaki kayıt, bağlantılar kapatılamadan önceki
kod/salt-okunur hazırlığın tarihsel kanıtıdır; güncel yazmalı kabulü geçersiz
kılmaz ve yeni sonuç yerine kullanılmaz.

Kullanıcı prova DB'sindeki uygulama bağlantılarını şu an kapatamıyor. Restore
ön kontrolü açık kullanıcı oturumları nedeniyle durdu; zorla oturum kapatma,
restore veya veri temizleme yapılmadı. Bu çalışma boyunca **AssistFlow, MGS ve
prova DB'sinde DDL/DML/ayar yazması yapılmadı**. Mevcut prova verisi korunuyor.

## Tamamlananlar

- Ayrı [Collections.Production](../../../tools/Collections.Production/README.md)
  adaptörü eklendi. Eski prova aracının hedef/capture/REHEARSAL_ONLY kilitleri
  korunur. Finansal işlemler ortak compile-link çekirdeğe ayrıldı; ikinci bir
  RequestId/komut hash/banka kimliği veya iş kuralı motoru yazılmadı.
- Plan bundle/source/schema/bağlama ve adaptör/Business/Data/Model/Core hash'lerine
  bağlı. Canlı ve prova bağlamaları ayrıdır; aynı MGS kaynağı ve aday kapsamı
  zorunlu, prova hedef kimlikleri canlıya kopyalanmaz.
- Canlıda ayrı açık onay, operatör ve onay kanıtları, bakım sonrası taze kesit,
  aynı kod/paket için tamamlanmış **yazmalı adaptör provası** zorunlu. Onaysız
  dosya veya prova teyidi canlıyı açmaz. Prova yazmalı kabulü henüz yok;
  `acceptance.json` üretilmedi ve uydurulmadı.
- İlk aktarımda boş financial/source/map/makbuz/banka baseline'ı, tam migration
  seti, kaynak/referans hash'i, teknik aktör/trigger/başka oturum kontrolleri.
  Canlıda dört ilgili Job'ın kapalı/çalışmıyor olması denetlenir; Job değiştirilmez.
- Veri+ham kaynak+makbuz+çalıştırma/onay audit'i aynı transaction'da. Tekrar
  makbuz/audit doğrular, silineni yaratmaz/düzenleneni ezmez. Commit sonucu
  belirsizliğinde rollback başarısı iddia edilmez; önce mutabakat gerekir.
- `certify-rehearsal` ilk yazma, tekrar, kesinti rollback ve dört koruma sonucunu
  aynı hedef/plan/kod/pakete bağlamadan yazmalı teknik kabul oluşturmaz.
  Bu teknik kabul insan/live onayı değildir. Prova hata deneyleri canlıya kapalı.
- WebAPI/general seed/worker/SMS/mail/CDN başlatılmaz. İlk finansal cutover
  kapsamıdır; CDN ve canlı kullanım sonrası genel delta importer ayrı konudur.

## Yapılan doğrulama ve sınırı

- Yeni adaptör build: **0 hata /9 uyarı**. Refaktör edilmiş eski prova adaptörü
  build: **0 hata /3 uyarı**. İlk bağımlılık derlemesinin mevcut genel uyarıları
  ayrıca sürer; ilgisiz refactoring yapılmadı. FE değişmedi.
- Kapalı canlı onayı, prova planının canlı teyidiyle çalıştırılması ve yanlış
  plan hash'i olumsuz kontrolleri geçti: özellikle bulunmayan config yolu verildi,
  her biri config/SQL/çıktı açılmadan beklenen kapıda reddedildi.
- `verify-existing` ile eski batch 3 **sahiplenmeden/değiştirmeden** salt-okunur
  doğrulandı: **4.439 sözleşme, 17.269 tarife, 139.747 ödeme/makbuz**;
  TRY **26.684.052,43**, USD **115.320,76**. Not/grup/fatura/cari/banka ve ham
  audit kontrolü de geçti. Önce/sonra 19 veri/referans parmak izi aynı.
  `DatabaseWrites=false`, **`WritePathVerified=false`**.
- Salt-okunur kanıt `-01` adaptör derlemesine aittir. Sonraki `-02` derlemede
  yalnız canlı kaynak tekrar-okumasından kalan **aracın kendi** boş bağlantı
  havuzlarını kapatma düzeltmesi yapıldı; finansal çekirdek/bağımlılık hash'leri
  aynı. `-02` kapı kontrolleri tekrarlandı. Bu farktan da yazmalı kabul çıkarılmaz.
- Başarılı eski tam prova, yeni adaptörün ilk yazma veya rollback/replay
  provasının tamamlandığı anlamına gelmez. Aktif DB üzerinde böyle bir deneme
  yapılmadı. Fiziksel ağ kesintisi/backup-PIT/HTTP/CDN kabulü ayrıca açıktır.

## Özel plan ve kanıtlar

Önceki hash mühürlü canlı **inceleme paketi değiştirilmedi**:
`collection-cutover-review-2026-10-17-02`, SHA
`8253BCA52AED33271A445FE6B46EBEF7A86E3F5220C48239B54E7B6F12AFD673`.
Bu paket içindeki `ContainsProductionImporter=false` hâlâ doğrudur; yeni ayrı
adaptör kaynakları ve planları bu tarihsel pakete sessizce eklenmedi.

BE `.tools` altında:

- `collection-production-adapter-live-plan-2026-10-17-02/plan.json`:
  `20FC5BAE18844C91DF0FC5E7E3DBE6CEBD7B686042A35ECA05404E71B9C24D2D`.
- `collection-production-adapter-rehearsal-plan-2026-10-17-02/plan.json`:
  `114B375A0CDC6D366FBB88CCE4AAC6E80CA1063BB8827EF7AB31175D97E9D15E`.
  İkisinin de onay şablonu ayrı; canlı authorization `NOT_APPROVED`.
- `collection-production-adapter-readonly-2026-10-17-01/result.json`:
  `A59344ECACC484B8B3115DBF97002692D7ACC9F0490041A5ED26A7B322E6A285`.
- `collection-adapter-before-2026-10-17-01.json`: işlem öncesi salt-okunur profil.
- `collection-production-adapter-guards-2026-10-17-02.json`: son sürüm kapı kontrol kaydı.

## Bağlantılar kapatılabildiğinde sıra

1. Prova kullanıcı oturumlarının gerçekten kapandığını tekrar oku; zorla KILL yok.
2. Önceki açık disposable restore onayı kapsamı ve hedef yeniden teyit edilerek
   yalnız AssistFlow_Performance_Restore baseline restore/şema hazırlanır.
   Canlı AssistFlow/MGS'ye restore, backup veya DML yapılmaz.
3. Aktarıcı/kaynak/bağımlılık değiştiyse planlar yeniden üretilir. Aynı canlı
   inceleme paketi ile prova hedef bağlaması kaynak byte/kapsam düzeyinde eşleşir.
4. Yeni adaptörde **rollback-check → apply → apply tekrar → resilience-check**;
   sayım/tutar/hash ve makbuz mutabakatı, son CHECKDB, sonra `certify-rehearsal`.
5. Teknik kanıt yeni yayın paketine açıkça bağlanır; canlı operatör/ortak fark/
   bakım/Job/başlangıç/CDN/yedek ve kullanıcı onay kapıları ayrıca kapanır.

Aktif uygulama varken sıfırlama veya yazmalı deneme yapılmayacak. Kalan
bağımlılık kod yazma değil, **uygun prova bakım aralığı ve gerçek yazma kabulüdür**.
