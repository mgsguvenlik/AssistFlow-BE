# İzole aktarım provası — hazırlık ve kabul sırası

**17 Ekim veri uygulaması:** Hedef-kilitli makbuz/audit korumalı aktarım ve
kontrollü kesinti provası gerçekleştirildi. Güncel sonuç ve açık kapılar
[veri aktarım kanıtında](data-transfer-2026-10-17.md) takip edilir. Aşağıdaki
ilk hazırlık hükümlerindeki “veri provası yapılmadı” ifadeleri tarihsel kayıttır;
ayrı-instance ve tam restore/uygulama kabulü hâlâ ayrıca değerlendirilir.

**17 Ekim uygulama güncellemesi:** Kullanıcı hedefin üzerine restore'u onayladı; en güncel mevcut AssistFlow tam yedeği yalnız `AssistFlow_Performance_Restore` üzerine restore edildi. CHECKDB temiz. 21 migration + 2 seed iki kez çalıştı; şema/tanım tekrar kontrolü geçti. [Gerçek çalışma kanıtları ve kalan veri provası](execution-2026-10-17.md). Aşağıdaki eski “onay/restore bekleniyor” ifadeleri tarihsel ilk hazırlıktır; uçtan uca finansal veri provası hâlâ açık.

**17 Ekim hedef tercihi:** Kullanıcı aynı instance üzerindeki `AssistFlow_Performance_Restore` DB'sini seçti. [Salt-okunur bulgular ve kontrollü ortak-instance istisnası](target-selection-2026-10-17.md). DB eski AssistFlowTest restore'u; güncel canlı kopyası değil. Üzerine restore için açık onay bekleniyor. Aşağıdaki ayrı-instance profili tarihsel öneridir; ortak-instance için uygulanabilir kabul edilmiş sayılmaz, canlı Joblar kapatılmaz.

Durum: **hazırlık paketi hazır; SQL hedefi/onaylayıcı belirtilmedi, gerçek prova yapılmadı.** Bu klasörde restore veya apply çalıştıran araç yoktur. Mevcut test-only araçların canlı kilidi kaldırılmaz. [Audit hazırlık raporu](../audit-rehearsal-report.md).

## Başlatmak için gerekenler

1. Canlı SQL instanceından **ayrı**, sadece bu prova için ayrılmış SQL instance/host. Aynı canlı instanceında farklı DB adı yeterli izolasyon değildir. Bağlantı güvenli ortam değişkeni/secret üzerinden sağlanır; parola Git'e/komuta/rapora yazılmaz.
2. SQL sunucusundan okunabilecek AssistFlow ve MGS tam yedekleri, varsa gereken log zinciri ve her mantıksal dosya için açık DATA/LOG `MOVE` hedefi. Mevcut msdb kayıtları özel hazırlık paketinde bulunur; dosyaya erişim veya restore başarısı değildir. Yeni backup, dosya kopyalama veya restore henüz yetkilendirilmedi/çalıştırılmadı.
3. Yayın/aktarımı onaylayacak gerçek AssistFlow kullanıcısının **Code** değeri. Bu kodun tekil/aktif olduğu ayrıca okunur. Legacy ad benzerliğinden kişi seçilmez; sistem aktörü 0, insan onayı yerine geçmez.

## İzolasyon

- İsim önerileri: `AssistFlowCollectionRehearsal` ve `MGSCollectionRehearsal`; canlı/test DB adı kullanma. Yeni DB hedefleri boş/yok olmalı; `WITH REPLACE`, mevcut DB düşürme veya SINGLE_USER ile kullanıcı atma yoktur.
- Prova makinesinden canlı SQL, SMTP, Telsam/JetSMS, Manitou ve production R2/CDN uçlarına ağ çıkışı kapalı olmalı. Secret bulunmaması tek başına güvenlik önlemi değildir.
- SQL Agent Jobları/msdb/loginler canlıdan klonlanmaz. Restore veritabanlarında TRUSTWORTHY ve cross-DB ownership chaining kapalı, linked server yok. Restore edilen uygulama içindeki bağlantı/config tabloları da incelenir.
- WebAPI/Jenkins başlatılmaz. SMS simülasyon ayarı tek başına mail/Manitou/background servisleri durdurmaz. `Program.cs` başlangıcı migration/genel seed ve worker başlatır.
- Paylaşılan yedek ve çıktı müşteri/finansal veridir; erişim sınırlı, Git dışı, kanıtlar tamamlanınca kullanıcı onaylı saklama/temizleme planı gerekir. Hazırlık hiçbir dosyayı silmez.

## Prova sırası ve ölçülebilir kabul

| Sıra | İş | Kabul |
| --- | --- | --- |
| 1 | Yedek doğrulama | İzole instance üzerinde HEADERONLY/FILELISTONLY/VERIFYONLY; doğru DB, yedek zamanı, tüm stripe dosyaları, mantıksal dosyalar, LSN zinciri. Kaynakta checksum yoksa VERIFYONLY checksum kanıtı diye sunulmaz; restore sonrası CHECKDB ayrıca gerekir. |
| 2 | Yeni adlara restore | Açık ve gözden geçirilmiş MOVE listesi, `REPLACE` yok. Yalnız onaylı boş hedeflere. ONLINE, collation, dosya yolları ve CHECKDB sonucu kaydedilir. İki bağımsız DB yedeği aynı anda alınmış kesit diye kabul edilmez. |
| 3 | Hedef ön kontrol | `00-isolation-preflight.sql` açık host/onay parametreleriyle yalnız okunur çalışır. Restored müşteri/tanım fingerprint'i planla uyuşmalı; eski yedek farkı varsa paket yeniden üretilir, sessizce devam edilmez. |
| 4 | Geri dönüş tabanı | Şema/aktarım öncesi izole baseline yedeği ve entity/fingerprint raporu. Yeni yedeğin restore edilebildiği ayrıca sınanır; canlıda yedek/restore çalıştırılmaz. |
| 5 | Şema/seed | Manifestteki 21 migration, sonra tanım/menü seed'leri. Canlı DB guard'ları körlemesine kaldırılmaz: onaylı izole host/DB guard'larıyla ayrı türev paket üretilir; her SQL batch aynı korumaya sahip olur. İlk hatada durma, pending=0, FK/check indeks doğrulaması; ikinci çalışma 0 ek değişiklik. |
| 6 | Veri apply hazırlığı | Tam ve izole-hedef kilitli uygulayıcı ayrıca tamamlanmalı. Referanslar ve onaylı üst kartlar → sözleşme/tarife → ödeme + makbuz → not/grup/fatura/cari → dosya/banka. Sistem aktörü/audit planı, ham kaynak, hash, map ve transaction birlikte korunur. |
| 7 | Birinci aktarım | Plan→hedef tam sayım ve kimlik/hash mutabakatı; para birimi ayrı, Free/eksi/sıfır değişmez; donuk/ücretsiz/YOK ve kaynak dışlamaları korunur. Fatura–ödeme tam zinciri ve bakiye eşitliği. Belirsiz kayıt içeri sızmaz. |
| 8 | Tekrar/yarım işlem | Aynı paket ikinci kez 0 yeni ödeme/sözleşme/map/makbuz/banka kimliği/dosya. Kaynak değişimi ve eski payload reddi; kontrollü kesintide transaction rollback ve güvenli tekrar. Bu kontroller yalnız atılabilir prova kayıtlarında yapılır. |
| 9 | Düzenleme/silme tekrarı | Prova ödemesi elle değiştirildikten/silindikten sonra eski import ödemeyi ezmez/geri yaratmaz; kalıcı RequestId makbuzu korunur. Fatura/dosya kaldırma kanıtları da korunur. |
| 10 | Dosya | Canlı CDN'e çıkmadan yerel no-op/izole transport ile sahiplik, hash, MIME, boyut ve yarım upload senaryosu. Gerçek production CDN aktarımı/erişimi daha sonra ayrı onaydır; provada üretim nesnesi yazılmaz/silinmez. |
| 11 | Geri dönüş | İzole baseline yedeğini **başka yeni DB adına** restore et; ilk fingerprint/sayımlarla eşitliğini göster. Canlıya EF Down/restore veya receipt silme uygulanmaz. |
| 12 | Kanıt/onay | Paket SHA, SQL/araç commitleri, önce/sonra/tekrar/geri dönüş raporları, hatalar ve açık istisnalar. Gerçek operatör kodu ve zamanıyla onay; başarısız/eksik adımda NO-GO. |

Şema scriptleri production DB guard'ına bağlıdır; adlarını değiştirip doğrudan çalıştırma. Data paketindeki JSON'lar SQL veya çalıştırılabilir apply komutu değildir. Hazırlığın tamamlanması restore provasının tamamlandığı anlamına gelmez.
