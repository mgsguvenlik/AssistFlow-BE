# Tahsilat — canlı geçiş teslimi ve kalan uygulama sırası

## Güncel sonuç

**Yeni sürüm güncellemesi:** [gerçek izole host ve yenilenmiş yazmalı kabul](hosting-and-refresh.md)
tamamlandı. Yeni `-03` planları / `-02` finansal kanıtlar ve `-05` host kanıtı
aynı backend hash'lerine bağlıdır. Aşağıda anlatılan ilk `-01` teslim tarihsel;
yeni teslim adı `collection-cutover-handoff-2026-10-17-02` olacaktır. Canlı GO yoktur.

**Sonraki kod hazırlığı:** [seed/migration/Manitou açılış kontrolleri](startup-isolation.md)
eklendi ve 13 bağlantısız kontrol geçti. Aşağıdaki paket eski runtime'a aittir;
yeni backend bağımlılık hash'leriyle aynı kabul kullanılamaz. Gerçek proses
açılış kabulü ve yeni paket/prova tamamlanmadan canlı GO yoktur.

17 Ekim production adaptörü provası kabul edildi. 4.439 sözleşme, 17.269
tarife, 139.747 ödeme/makbuz; TRY 26.684.052,43 ve USD 115.320,76 doğrulandı.
Tam transaction rollback, sıfır-kayıtlı tekrar ve dört koruma kontrolü başarılı;
son CHECKDB temiz. [Kanıt/hash tablosu](production-adapter-rehearsal.md).

**Teslim hazırlığı tamam; canlı yürütme izni yok.** Güncel paket, bakımda
alınmış son kesit değildir. Canlıya uygulanmadı. Test/prova DB verileri veya
üretilen kimlikler canlıya kopyalanmayacak; kaynak MGS'dir.

## Paket yapısı ve güvenli kullanım

`Prepare-CollectionCutoverHandoff.ps1` yalnız yerel dosya okur/kopyalar/hash'ler;
SQL/config yüklemez, bağlantı açmaz, servis veya gönderim başlatmaz. Yeni
`.tools` dizinine, hiçbir mevcut kanıtı değiştirmeden teslim üretir:

- `review-bundle/`: 106 dosyalı önceki kaynak/karar/eşleme/şema/seed/veri/Job
  inceleme paketi ve onun orijinal hash listesi, byte'ları değişmeden.
- `runtime/`: provada çalışan adaptör ve bağımlılıkları; `.NET 9` kurulumu
  ayrıca gerekir. WebAPI/uygulama config'i, credential, PDB veya EXE yoktur.
- `source-code/`: adaptör, ortak çekirdek ve doğrudan compile-link yardımcıları;
  tüm FE/BE kaynak kodunun yerine geçmez. Referans proje kaynakları Git'tedir.
- `live-plan/`, `rehearsal-plan/`: tarihsel hash bağlı planlar ve ayrı onay
  şablonları. Canlı şablon **NOT_APPROVED** olarak kalır.
- `adapter-proof/`: gerçek teknik kabul, beş alt kanıt ve son profil/CHECKDB.
- `instructions/`: uygulama sırası, prova raporu ve bu teslim belgesi.
- `handoff-manifest.json`: bütün kopyaların SHA-256'sı, asıl bundle/plan/kabul
  bağlamaları ve `ProductionApplyAllowed=false` durumu.

**Bu bir tek-tık deploy veya taşınabilir çalıştırma paketi değildir.** Plan ve
kabul dosyaları orijinal BE `.tools` girdilerine mutlak yollarla bağlıdır;
kopyalama bu yolları sessizce değiştirmez. Yeni host/yol, kod veya bağımlılık
derlemesinde yeniden plan/prova gerekir. Komutlar doğrulanmış BE kökünden,
o planın hash'lediği runtime ile yürütülür; başka yerden alınan DLL ile değil.
Gerçek dosya arşivleri/CDN nesneleri ayrıca güvenli korunur. Kişisel veriler
içerdiğinden paket Git/GitHub/Jenkins konsol loguna veya herkese açık linke konmaz.

## Kalan kapılar — tamamlanmış sayılmayacaklar

[Operatör/rol ve SC01–SC06 karar listesi, salt-okuma ön kontrolü ve doğru tek-yazıcı sırası](operator-and-shared-approvals.md)
hazır. Ortak servis/müşteri/G/üst kart verileri aktarıcıdan önce ayrı SQL'lerle
uygulanmaz; menü seed'inin rol kodu NULL kalır, AU01/AU02 ayrı onaylanır.
Gerçek Code'lar ve imzalı kararlar henüz yoktur; bu belge uygulama izni değildir.

| İş | Sorumlu / çıkış kriteri | Şimdiki durum |
| --- | --- | --- |
| Aktör/yetki | İşletim: aktif tekil User.Code ve görüntüleme/yazma Role.Code listesi; en az yetki kabulü | Boş; Id=1 veya geniş rol tahmini yok |
| Ortak veri/prosedür | Kullanıcı onayı: 18 servis, 2.602 müşteri tipi, G tipi/56 üst kart ve `stg.sp_SyncCustomers` dar farkı; önceki değer/hash kanıtı | Sadece inceleme taslakları |
| Yayın kapsamı | İş birimi: bekleyen istisnalar ve K10/K11 için dahil/hariç/manuel işletim kararı | Teknik prova kapsam kararı yerine geçmez |
| Açılış izolasyonu | Üç StartupExecution bayrağı gerçek prova hostunda doğrulandı; production override/diğer worker işletimi ayrı | 18 offline kontrol ve gerçek izole Kestrel kabulü hazır. Production/IIS kabulü değil; süre 0 durdurmaz |
| Job/bakım | İşletim: gerçek step/hash bağımlılığına göre durdur/devam onayı, yazıcıların bitmiş olduğunun kanıtı | Matris onaysız; hiçbir Job durdurulmadı |
| Geri dönüş | İşletim: canlı full/log/PIT restore kanıtı, eski BE/FE artefaktları ve ayrı restore yetkisi | Transaction provası yeterli değildir |
| Dosyalar | Dosya/CDN sorumlusu: tarama, sahiplik, MIME/boyut/hash, mevcut CDN yükleme/readback ve erişim kabulü | Yalnız yerel ilişki/hash kanıtı |
| Son kaynak/paket | Bakım sonrası güncel MGS ve canlı müşteri referansları, yeniden karar/mutabakat ve mühürlü paket | Mevcut paket tarihsel; canlıya çalıştırılmaz |
| Kullanıcı kabulü | Yetkili/yetkisiz HTTP/ekran, tutar/Excel/dosya kontrolü; finansal yazma ve gerçek SMS ayrı onay | Açık |

Ortak tablo/prosedür onayı olmadan veri adaptörünün ilk aktarımı başlatılmaz.
Rol verme, prosedür değişikliği ve CDN yükleme finansal adaptör tarafından
yapılmaz; ayrı onaylı araç/SQL ve kendi mutabakatları gerekir.

## Bakım günü sırası / durma noktaları

1. Yukarıdaki kapsam, ortak değişiklik, yetki, açılış izolasyonu ve geri dönüş
   kapılarını bakım öncesinde kapat. FE/BE main commitleri, Jenkins job/build
   ve artefakt geri dönüş sürümlerini kaydet. Bu belge merge/deploy yetkisi değildir.
2. Kullanıcı yazmalarını ve BE'yi durdur. Yalnız onaylı kaynak/hedef Job
   adımlarını durdur; çalışan işlemlerin bitmesini doğrula. Başka modül/backup
   Joblarını topluca kapatma. Başka SQL oturumu varsa KILL değil, dur ve incele.
3. Gerçek full/log yedeği ve geri dönüş erişimini doğrula. Bakım başlangıcını
   ve operatör onay kaydını güvenli dış kanıt olarak kaydet.
4. MGS/AssistFlow sabit SELECT kesiti → müşteri kararlarını yeniden bağlama →
   finansal/tarife/ödeme/ilişki mutabakatı → canlı referans/schema/data planları →
   yeni mühürlü bundle üret. Değişen müşteri kararını otomatik genelleme;
   bekleyenleri tahminle alma. Test verisi veya prova ID'si kullanma.
5. Yeni bundle için yeni canlı/prova planlarını aynı doğrulanmış kodla üret.
   **Hash değiştiğinde eski `acceptance.json` yeni paketi kabul etmez.** Yeni
   kesit/paketin izole provası ve aynı pakete bağlı kabulü gereklidir. Yeni
   prova restore'u ayrıca uygun aralık/oturum kontrolü ister; canlı restore yok.
6. **30 dakika kapısı:** adaptör `capture-manifest.StartedAtUtc` üzerinden,
   bakım sonrası alınmış ve çalıştırmada 30 dakikadan eski olmayan kesit ister.
   Bu süre yalnız ödeme tarihi veya onay dosyası süresi değildir. Kesit + paket
   üretimi + gerektiğinde prova + şema hazırlığı bu sınırı aşarsa mevcut kod
   yürütmeyi reddeder. Eski timestamp/hash/onayı elle değiştirerek aşılmaz;
   durulur, son-kesit/prova zamanlaması yeniden planlanır. Zaman penceresi
   sağlanmadan canlı GO verilmez; bu teslim bu operasyonel koşulu çözülmüş saymaz.
7. Yeni plan/hash'e ve gerçek dış onaylara bağlı **ayrı** canlı onay dosyası
   hazırlanır. Şablonun adını değiştirmek onay değildir. Altı kanıt türü gerekir:
   `HumanApproval`, `MaintenanceFreeze`, `BackupRestore`, `StartupIsolation`,
   `SharedChanges`, `AdapterRehearsalAcceptance`. Sonuncusu gerçek kabulün ve
   alt kanıtların hash'leriyle aynı bundle/kod olmalıdır.
8. Onaylı hash sırasıyla canlı şema/seed, operatör/rol ve ayrı prosedür farkı
   uygulanır; ilk hatada durulur. Migration geçmişi elle düzeltilmez. Finansal
   adaptör öncesi tüm beklenen migrationlar tam, financial baseline boş,
   teknik aktör çakışmasız ve ilgili dört Job kapalı/çalışmıyor olmalıdır.
9. Onaylı production adaptöründe yalnız `apply` kullanılır; canlıya
   `rollback-check` veya `resilience-check` çalıştırılmaz. Her deneme yeni çıktı
   dizinidir. Canlı kaynağın son hash kontrolü ve hedef referans kontrolü geçmeden
   DML başlamaz. Commit belirsizse `commit-uncertain.json` üzerinden mutabakat
   yapılır; kör tekrar veya makbuz/audit silme yoktur.
10. Kaynak/aday/hedef sayım, para birimi tutarları, ilişkiler ve aynı batch
    tekrarını doğrula. Tekrar için de geçerli onay/süre ve değişmemiş kaynak
    gerekir. İlk aktarım/replay kabulü **CDN ve kullanıcı yazmaları açılmadan**
    tamamlanır; genel delta importer olarak kullanılmaz.
11. Mevcut CDN ile ayrı onaylı dosya aktarımı/readback yapılır; eksikler
    raporlanır. Sahte URL veya audit silme yoktur. Sonra kullanıcı Jenkins **BE**
    jobunu kontrollü açar; genel seed/worker izolasyonu önceden doğrulanmış olmalı.
12. Kullanıcı Jenkins **FE** jobunu çıkarır; önce salt-okunur kabul yapılır.
    `CollectionRead.Enabled=true`, `ContractCreateEnabled=false`; SMS
    `CollectionSmsEnabled=false`, `SmsSimulationEnabled=true`. Bu bayraklar
    tüm modülün read-only güvenliği değildir; mevcut yazma yetkileri, genel seed,
    worker'lar ve dış çağrılar ayrıca kontrol edilir. Ardından ayrı iş birimi
    onayıyla finansal yazma ve son olarak gerçek SMS açılır.
13. İlk gün/ertesi gün tutar, mükerrer işlem, 5xx/SQL bekleme, dosya ve bildirim
    hatalarını takip et. Job'ları yalnız imzalı devam planıyla aç; legacy ve yeni
    otomasyonları aynı işi iki kez yapacak şekilde birlikte açma.

Detaylar: [ana runbook](cutover-runbook.md). Canlı onay şablonu ve bu teslimdeki
bekleyen kapılar **false/boş** kalır; teknik kabul insan onayı değildir.
