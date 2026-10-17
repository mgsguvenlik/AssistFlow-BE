# Tahsilat — Job, bakım ve geri dönüş planı

Durum: **hazırlık tamamlandı; canlı işletim onayı verilmedi.** Bu adımda DB
bağlantısı, Job durdurma/başlatma, BACKUP/RESTORE, Jenkins veya host başlangıcı
yapılmadı. Bu belge çalıştırılabilir operasyon komutu değildir.

İlgili belgeler: [ana geçiş sırası](cutover-runbook.md),
[operatör/rol ve ortak fark onayları](operator-and-shared-approvals.md),
[kapalı bakım onay şablonu](maintenance-approval.template.json),
[çalıştırılmamış salt-okuma ön kontrolü](job-backup-preflight.sql).
Onay JSON'u aktarıcının authorization dosyası değildir; otomatik uygulanmaz.

## 1. Kanıt ve kapsam

Aktif teslim `collection-cutover-handoff-2026-10-17-02`, manifest SHA-256:
`6BC34709D364D02DCD72F05F682A7C1D805690A88D8971375A98B313F2C55ABC`.
Job profil kanıtı `.tools/collection-production-adapter-final-2026-10-17-02.json`,
SHA-256 `D267C6E446B307645AA20E92B5EC043299A4252615713143A2AA564FCA2FBD30`.
Mühürlü paketler değiştirilmedi; bu belge ayrı operasyon hazırlığıdır.

Profilde **19 tekil Job, 24 tekil adım** var. Eski `job-review-matrix.json`
içindeki 15 satır tam Job sayısı değildir: adım/zamanlama ilişkilerini içerir,
DB filtresiyle bazı çapraz adımlar dışarıda kalır. Örneğin
`GrupAboneSayilariMail` dört adımlıdır; yalnız MGS adımlarını incelemek
Mgs_Corporate/master etkisini gizler. Bir Job birden fazla zamanlamayla tekrar
görünebilir. Son kesitte tüm adımlar ve bağımlılıklar tekrar değerlendirilir.

Zamanlama adı gerçek çalışma saati kabul edilmez; sayısal frekans, başlangıç/
bitiş ve zamanlama kimlikleri kullanılır. Tarihsel envanter güncel durdurma
kanıtı değildir. SHA kaydı komutun semantiğinin incelendiği anlamına gelmez.

## 2. Job durdurma/devam matrisi

| Sınıf | Job'lar | Bakım ve devam koşulu |
| --- | --- | --- |
| Aktarıcının kapalı olmasını zorunlu tuttuğu dört Job | Platform Tahsilat Bakım; Platform SIMStoMGS-MANITOUTOMGS; Manitou2Flowassist; Platform GrupTopluMail | Ayrı sahip/onayla yeni tetiklemeler kapatılır, mevcut çalışma biter. Dördü de bulunmalı, disabled olmalı ve güncel Agent oturumunda çalışmıyor olmalı. Aktarıcı kendisi durdurmaz. |
| Bağımlılık/iş sahibi incelemesi gereken sekiz Job | GrupAboneSayilariMail; SIMS REPORT DATA; Platform Mgsk Abone Adedi; Platform NetsisToPlatfromJob; Platform SIMSServiceToMGS; Platform Takvim Hatırlatma; RefreshManitouLogSynonym; spServiceReminder | Tam adımlar, çapraz DB/harici servis etkileri incelenir. Kaynak/hedef yazıcısı veya bildirim etkisi varsa onaylı kesme matrisine alınır; topluca durdurulmaz. |
| Varsayılan mevcut işletimi korunacak yedi Job | _DBA - Backup - Full; _DBA - Backup - Log; Database Mirroring Monitor Job; LSAlert_SRV-SQL; Index Plan.Subplan_1; Tungana_IndexRebuilds; syspolicy_purge_history | Yedek/log/izleme kapatılmaz. İndeks bakımının kaynak tüketimi DBA ile ayrıca planlanır. Profilde LSAlert_SRV-SQL zaten kapalıdır; otomatik açılmaz. |

27 Eylül kod incelemesinin tarihsel bulguları: Tahsilat Bakım RunFollow/
RunFollowGroup ve rapor önbelleklerini; SIMStoMGS isimli Job kaynak müşteri/
sözleşme/tarihçe yazımını; Manitou2Flowassist staging entegrasyon tüketimini
çalıştırıyordu. GrupTopluMail finansal hatırlatmaların yanında diğer CRM mail
akışlarını içeriyordu. Netsis ürün/fiyat entegrasyonuydu, ödeme aktarımı
değildi. GrupAboneSayilariMail operasyonel sayım/mail, SIMS REPORT DATA aylık
snapshot işiydi. Bunlar güncel komut hash'i ve sahip incelemesi olmadan yeni
çalıştırma kararı değildir; özellikle karma Job'ın tamamını bekletmenin diğer
modüllere etkisi onaylanmalıdır.

Her Job için GUID + ad, tüm adım hash'leri/başarı-hata dalları/proxy,
zamanlama kimlikleri ve tanım hash'leri, önceki enabled değeri, mevcut çalışma,
sahip, durdurma onayı, **ayrı devam onayı ve sırası** kaydedilir. Ortak
zamanlamayı değiştirmek diğer Job'ları etkiler; varsayılan yöntem değildir.
Adım atlatmak için Job akışı yeniden yazılmaz.

Disabled yapmak mevcut çalışmayı durdurmaz; manuel/başka Job/harici başlatmayı
da tek başına engellemez. Varsayılan mevcut çalışmanın bitmesini beklemektir.
Pencere aşılırsa geçiş ertelenir. Çalışan Job'ı zorla durdurma, oturum KILL,
SQL Agent servisini veya tüm Job'ları kapatma yetkisi bu planda yoktur.

## 3. Bakım günü sırası ve kabul kapıları

| Sıra | İşlem | Devam için kanıt / durma sebebi |
| --- | --- | --- |
| 1 | İş sahibi, aktif operatör User.Code, DBA, Jenkins sorumlusu, bakım başlangıç/bitiş, kabul edilen RPO/RTO ve Job kararları belirlenir. | Gerçek isim/kod ve onay referansı olmadan NO-GO. Rol/ortak tablo izinleri ayrı onay belgesine bağlıdır. |
| 2 | Eski/yeni BE-FE artefaktı, güvenli config ve production Data Protection anahtarlarına kurtarma erişimi doğrulanır; yedek tabanı hazır edilir. | Şifre/anahtar Git'e veya genel pakete konmaz. Yedek kullanılabilirliği son kesitten önce doğrulanır. |
| 3 | Kullanıcı yazmaları engellenir; tüm BE instance'ları ve ilgili harici yazıcı/zamanlayıcılar onaylı sırayla durur. Onaylı Job'larda yeni tetikleme kapatılır, çalışanların bitmesi beklenir. | Tüm adımlar, son Agent oturumu, açık istekler, master üzerinden çapraz yazıcılar ve harici başlatıcılar kontrol edilir. Eksik görünürlük/izin boş sonuç sayılmaz; NO-GO. |
| 4 | Freeze zamanı UTC kaydedilir; kaynak ve hedef referans son kesiti alınır. | Kararlar/eşlemeler/drift yeniden kontrol edilir. Yeni klasörlerde paket/plan/hash ve gerekiyorsa aynı sürümle prova yenilenir. Eski hash'e yeni kesit etiketi yapıştırılmaz. |
| 5 | Onaylı şema/seed/prosedür ve ardından makbuz/audit korumalı veri aktarıcısı uygulanır. | SC01–04 için tek yazıcı aktarıcıdır; eski 10/20 inceleme SQL'leri önce çalıştırılmaz. SC05 prosedür ve SC06 yetki/seed ayrı onaylıdır. İlk hata veya drift'te durulur. |
| 6 | Adet, para birimi ayrı tutar, kaynak-hedef hash, makbuz/audit ve tekrar güvenliği mutabakatı yapılır. | Belirsiz commit'te yeniden uygulamak yerine batch/RequestId/makbuzlar incelenir. Kabul yoksa kullanıcı açılmaz. |
| 7 | Ayrı onaylı mevcut CDN aktarımı/erişim kabulü ve kontrollü Jenkins BE, ardından FE dağıtımı yapılır. | Kullanıcı main merge/deploy eder. Başlangıç seed/migration/Manitou ayarları gözden geçirilir; pending migration ve beklenmedik yan etki kontrol edilir. |
| 8 | Salt-okunur kullanıcı/API kabulü; ayrı finansal yazma, SMS ve Job devam kararları uygulanır. | Teknik prova insan kabulü değildir. Devam matrisi tamamlanmadan tüm Job'lar eski enabled değerine topluca döndürülmez. |

Aktarıcının **captureStartedAt üzerinden 30 dakika** son kesit sınırı korunur.
Yedek hazırlığı bu süreyi tüketmeyecek şekilde önceden yapılır. Güncel paket/
prova bu pencereye sığmıyorsa geçiş ertelenip yöntem tekrar planlanır;
timestamp değiştirilmez, güvenlik kapısı gevşetilmez.

## 4. Backend ve Job'ların kontrollü devamı

- `ContractCreateEnabled=false` tüm sistemi salt-okunur yapmaz. Diğer
  finansal/dosya/SMS yazma yolları ve etkin rollerin HasEdit hakları da
  engellenir veya tam bakım sürdürülür. SMS ayrı kapalı tutulur; yalnız
  simülasyon kullanmak kuyruğu Simulated olarak tüketebilir.
- Production'da özel IsolatedRehearsalMode kullanılmaz. StartupExecution
  bayrakları yalnız kendi migration/seed/Manitou yollarını kapsar; diğer dört
  worker normal production hostunda çalışır. İlk açılıştan önce bekleyen
  bildirim/işler ve diğer modül etkileri ayrıca kabul edilir.
- Manitou API worker'ı üretici, Manitou2Flowassist staging tüketicisidir.
  İlk worker turu hemen çalışabilir. İkisi ancak güncel eşleme/prosedür ve
  finansal yetki kabulünden sonra kontrollü üretici → staging inceleme →
  tüketici sırasıyla açılabilir; otomatik izin değildir.
- Legacy tahsilat bakım/mail/source entegrasyonları K10/K11 kapsamı ve sahiplik
  kararı olmadan geri açılmaz. Eski ve yeni eşdeğer finansal/bildirim
  otomasyonları birlikte çalıştırılmaz. Kuyruk/gecikmiş bildirimler körlemesine
  telafi edilmez. Önceden kapalı Job ayrı onay olmadan açılmaz.
- İlk açılışta ve sonraki iş gününde Job sonuçları, blocking/log alanı,
  entegrasyon farkları, ödeme/makbuz tekrarı, borç ve para birimi toplamları,
  SMS/mail kuyruğu sorumlularca kontrol edilir. Yeni otomasyon oluşturulmadı.

## 5. Yedek ve kurtarma kabul listesi

Mevcut kanıt: 17 Ekim 00:00:02 tarihli AssistFlow tam yedeği prova hedefinde
VERIFYONLY, gerçek restore ve ayrı son CHECKDB ile doğrulandı. Eski yedekte
`has_backup_checksums=false`, `is_damaged=false`; **checksum doğrulanmış**
denemez. Son CHECKDB kanıtı final profilindedir; ilk restore kaydındaki
CheckDbComplete alanı tek başına son sonucu temsil etmez. Bu prova yeni canlı
yedeğin kullanılabilirliğini veya noktasal kurtarmayı kanıtlamaz.

Canlı bakım öncesinde DBA aşağıdakileri doldurup onaylar:

- Güncel yedek seti UUID/ID, medya aileleri ve sıra/position, başlangıç/bitiş
  saat dilimi, erişim, yeterli hedef disk/log alanı ve büyüme eşikleri.
- Mevcut stratejiyi bozmayan full/copy-only tercihi; desteklenen checksum ile
  yedek/verify. Checksum yoksa gerekçe ve ayrı istisna onayı; sessiz kabul yok.
- Seçilen yedeğin **izole gerçek restore + CHECKDB** kanıtı. msdb satırı veya
  VERIFYONLY tek başına restore provası değildir.
- Gerçek recovery model, LSN/log zinciri, recovery fork, gerekiyorsa diff/log
  setleri. FULL için geçerli zincir; BULK_LOGGED için noktasal kurtarma
  kısıtları; SIMPLE için log/PIT sözü verilmez. Model otomatik değiştirilmez.
- Gerekirse tail-log prosedürü ve ayrıca uygulama yetkisi; şifreleme/TDE
  sertifika/anahtar kurtarması, production key ring ve güvenli config erişimi.
- Kabul edilen veri kaybı süresi (RPO), toplam geri dönüş hedefi (RTO), ölçülen
  uçtan uca kurtarma süresi. Prova süresinden canlı süre tahmin edilmez.

Normal full/log yedekleri ve izleme korunur; ek ağır yedek DBA ile koordine
edilir. Disk/log baskısında durulur, otomatik shrink/truncate yapılmaz.
MGS kaynak olarak korunur; yeni MGS yedeği almak dahi ayrı operasyon onayıdır.
**MGS restore bu geçişin otomatik geri dönüş adımı değildir.** Prova restore
betiğinin hedef adını değiştirip canlıda çalıştırmak yasaktır.

## 6. Aşamaya göre geri dönüş

| Durum | Güvenli yaklaşım |
| --- | --- |
| Henüz DB değişikliği yok | Geçişi iptal et; kayıtlı eski sürüm/config ve onaylı Job durumlarına kontrollü dön. Gereksiz DB restore yok. |
| Şema/seed/prosedür ayrı commit edildi | Bunlar veri transaction rollback'iyle geri alınmaz. Önceki değer/SQL hash ve sürüm uyumluluğuna göre ayrıca değerlendir. Otomatik EF Down/DROP yok. |
| Veri transaction'ı commit öncesi hata verdi | Tam rollback ve baseline/makbuz/audit mutabakatını doğrula. Önceden commit olmuş metadata'yı geri alınmış sayma. |
| Commit sonucu belirsiz | Yazıcılar kapalı kalır. Batch/RequestId, kaynak ve komut hash'i, makbuz/audit incelenir; kör tekrar, audit silme veya restore yapılmaz. |
| Aktarım başarılı, kullanıcı yazması henüz açılmadı | Modülü kapalı tutma/uyumlu eski artefakt veya ileri düzeltme değerlendirilir. Ortak CustomerType/G/prosedür etkileri nedeniyle yalnız kod geri almak yeterli varsayılmaz. Toplu veri silme yapılmaz. |
| Kullanıcı veya diğer modül yazmaları başladı | Olay yönetimiyle tüm ilgili yazıcılar durdurulur. Son işlemler korunur; ayrı onaylı DBA kurtarması/ileri düzeltme seçilir. Tüm AssistFlow restore/PIT diğer modüllerin yeni verisini kaybettirebilir; kayıp açıkça onaylanmadan uygulanmaz. |

Canlı adaptörde prova aracının `rollback-check` / `verify-existing` komutları
varmış gibi kullanılmaz. Gerçek SMS/mail gönderimi veya CDN nesnesi DB rollback
ile geri alınamaz; ayrı envanter/mutabakat gerekir. Başarılı audit/makbuz veya
paylaşılan CDN nesneleri kör silinmez. Canlı restore/kill/Job komutu bu pakete
eklenmedi; uygulama ayrı açık yetki ve taze hedef kanıtı gerektirir.

## 7. Hazırlığın kapanışı ve açık kapılar

Yerel ScriptDom SQL parser/statement whitelist kontrolü geçti. JSON'daki tüm
boolean kapılar false, JobDecisions boş; 19 tekil Job/24 adım, kapalı LSAlert
baseline'ı ve profil SHA-256 kanıtla eşleşti. `git diff --check` başarılı.
Runtime değişmediği için build/test çalıştırılmadı.
Bu kontroller SQL yetkisi, fiziksel yedek erişimi
veya canlı çalışma durumunu doğrulamaz. Ön kontrol SQL'i çalıştırılmamıştır;
izin/kolon/metadata görünürlüğü operasyon öncesi DBA tarafından değerlendirilir.
Eksik sonuç veya yetki hatası NO-GO'dur, boş/başarılı kabul edilmez.

Hazırlık tamam; **gerçek sorumlular, pencere/RPO/RTO, Job başına kesme ve ayrı
devam kararları, güncel yedek/restore zinciri ve son kaynak kesiti** beklenir.
CDN/HTTP kabulü, K10/K11 kapsamı ve finansal/SMS açılışı ayrı kapılardır.
Bu tur runtime, frontend, mühürlü paket ve kullanıcı config'i değiştirilmedi.
