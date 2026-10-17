# Tahsilat — canlı CDN aktarımı ve erişim kabul hazırlığı

Güncel devam: [ayrı dosya adaptörü ve izole yazmalı prova](cdn-adapter-rehearsal.md).
Aşağıdaki metin ilk offline hazırlık kaydıdır; gerçek CDN/HTTP kabulü hâlâ
ayrıdır. Yeni adapter eski test araçlarının hedef kilidini kaldırmaz.

Durum: **hazırlık tamam; canlı aktarım ve erişim kabulü yapılmadı.** Bu adımda
AssistFlow/MGS bağlantısı, CDN isteği/yüklemesi/silmesi, backend başlangıcı veya
Jenkins işlemi yok. Ortak CDN, API, frontend ve mevcut işleyiş değiştirilmedi.

Bağlantılar: [geçiş sırası](cutover-runbook.md),
[bakım/yedek/geri dönüş](job-maintenance-and-recovery.md),
[kapalı CDN onay şablonu](cdn-approval.template.json),
[offline kanıt kontrolü](../../../tools/Test-CollectionCdnPreparation.ps1).

## 1. Doğrulanan mevcut yapı ve kapsam

- Ortak altyapı `IFileStorage` → `R2FileStorage`; Endpoint, BucketName,
  KeyPrefix, PublicBaseUrl ve credential mevcut R2Storage ayarlarından gelir.
  Yeni CDN servisi/prefix veya erişim politikası kurulmaz. Production değerleri
  test ayarlarından tahmin edilmez; güvenli config referansıyla doğrulanır.
- `BuildObjectKey` gelen değerin yalnız dosya adını alıp mevcut KeyPrefix'i
  ekler; `GetPublicUrl` bu anahtarı PublicBaseUrl ile birleştirir. DB'ye test
  URL'si veya `prefix/prefix/file` yazılmaz. Planın anahtarı düz, güvenli dosya
  adı olmalı; sessiz basename dönüşümüyle hatalı yol kabul edilmemeli.
- `ExistsAsync` yalnız metadata varlığıdır; içerik doğrulaması değildir.
  `UploadAsync` PUT işlemi aynı anahtarı ezebilir. Bu yüzden canlı aktarıcının
  ayrı namespace/anahtar çakışma kontrolü, tek-yazıcı kilidi ve hash/readback
  kabulü gereklidir. SHA-256 yerine ETag/Content-Length tek başına kullanılmaz.
- Dosya listeleri CollectionFollowUp View; yükleme/kaldırma Edit ve ilgili
  kullanım bayraklarıyla korunur. `api/collections/contracts/{id}/attachments`
  ve `api/collections/customers/{id}/attachments` mevcut yollarıdır. Dosya
  kaldırma POST'tur; mevcut davranış metadata'yı listeden kaldırır, CDN
  arşivini veya paylaşılmış bağlantının erişimini silmez.
- **CDN doğrudan bağlantıları mevcut yapıda public'tir.** Yetkisiz API liste
  isteğinin reddedilmesi, URL'yi bilen kişinin dosyayı açamayacağı anlamına
  gelmez. `/uploads/{fileName}` yolu da AllowAnonymous'tır; R2'ye 302 veya
  ayara göre eski yerel fallback dönebilir. Public bağlantı modeli önceki
  kullanıcı kararıdır; burada signed URL/özel proxy geliştirilmedi. Yayın
  kabulünde veri sahibi bu maruziyeti teyit eder. Gizlilik şartı değişirse
  yükleme durur ve ayrıca kapsam/altyapı kararı alınır.

### Aktarıcı sınırı

Mevcut `CollectionAttachmentTransfer` ve `CollectionActivityTransfer` yalnız
AssistFlowTest/uploads-test hedeflidir; canlı için kilitleri kaldırılmaz.
`Collections.Production` finansal aktarıcısı dosyaları yalnız
ContractFileEvidence/CustomerFileEvidence olarak audit'e alır; gerçek attachment
oluşturmaz ve CDN çağırmaz. Bu durum eksik URL üretmekten bilinçli olarak ayrıdır.

**Sonraki teknik uygulama:** mevcut storage'ı kullanan, canlıya özel ayrı
onaylı dosya adaptörü ve izole yazmalı kesinti/tekrar provası hazırlanmalıdır.
Bu belgeler o adaptörün tasarım/kabul sözleşmesidir; çalışır canlı uploader veya
tamamlanmış CDN provası olarak sunulmaz. Ortak IFileStorage değişmez.

## 2. Yerel paket mutabakatı

Aktif teslim `collection-cutover-handoff-2026-10-17-02`, manifest SHA-256
`6BC34709D364D02DCD72F05F682A7C1D805690A88D8971375A98B313F2C55ABC`.
Dosya kanıtları değişmemiş `collection-cutover-review-2026-10-17-02/data`
içindedir. İnceleme bundle hash'i ve dört dosyanın SHA-256 değeri kapalı onay
şablonuna bağlıdır. Mühürlü paketler yeniden yazılmadı.

| Kanıt/kayıt | Güncel paket sonucu |
| --- | --- |
| Sözleşme dosya planı | 9.517 sözleşme satırı |
| Yerel kanıtı uygun, CDN bekleyen | 1.818 ilişki; 1.100 farklı kaynak yolu; 1.079 farklı içerik SHA-256 |
| Arşivde bulunmayan / boş | 69 / 19 sözleşme dosyası |
| Dosya metadata'sı olmayan | 2.533 sözleşme |
| Sözleşmesi bekleyen veya dışlanan | 5.078 satır; dosyası sırf mevcut diye kapsama alınmaz |
| Fiziksel envanter | 1.769 dosya; tamamı otomatik yükleme adayı değildir |
| Aktivite müşteri dosyaları | 15 toplam; 3 aday / 12 sahipliği doğrulanmamış |

1.818 ilişkinin boyut toplamı **4.219.679.529 byte**; bu tekrar eden dosya
ilişkilerini içerir, benzersiz depolama/tarife maliyeti değildir. Üç aktivite
dosyası **4.313.667 byte**. Aynı hash farklı müşterinin dosyasını otomatik
birleştirme yetkisi vermez; hedef ilişki ve sahiplik ayrı korunur. Final object
anahtarı tasarımı yapılmadan upload sayısı/maliyet sözü verilmez.

Önceki altı eski-abonelik dosyası devir onayı korunur. Güncel kanıtta
924 → 291401 için üç dosya aday; 2206 → 87400 için üç dosyanın hedef sahipliği
doğrulanamadığından bekliyor. Diğer dokuz bekleyen aktivite dosyası da dahil,
12 satır tahminle ilişkilendirilmez. Test CustomerId/ContractId veya benzer
unvan seçilmez. Dosya devir onayı eski ödemeleri/notları taşıma izni değildir.

69 eksik/19 boş dosya sözleşmenin aktarılmasını engellemez veya sözleşmeyi
sildirmez; istisna raporunda kalır. Yeni bulunmuş dosyalar ayrı envanter/hash
ve sahiplik kontrolünden geçmeden mevcut pakete sessizce eklenmez.

## 3. Aktarım öncesi kapılar

1. Finansal aktarım ve aynı batch'in ilk replay mutabakatı **attachment/CDN
   açılmadan önce** bitmelidir. Mevcut ilk finansal doğrulama attachment
   tablolarının boşluğunu bekler; dosyalar açıldıktan sonra bu ilk-kurulum
   doğrulamasını kör tekrar çalıştırmak doğru değildir.
2. Gerçek production R2 endpoint/bucket/prefix/public host güvenli config'ten
   doğrulanır; test prefix'i reddedilir. Namespace fingerprint ve secret
   içermeyen config referansı operatör onayına bağlanır. Credential/URL/token
   Git'e veya genel loga yazılmaz. IAM/bucket/CORS/policy değişikliği yapılmaz.
3. Final kaynak metadata ve canlı batch/map/owner bilgileri tekrar okunur;
   silinmiş/kapsam dışı müşteri/sözleşme, çelişen mapping, değişmiş rowversion
   veya dosya listesi varsa satır bekler. Yeni canlı ID'ler kullanılır.
4. Yerel Contract arşivi/çıkarılmış kök ve Aktivite.rar tekrar hash'lenir;
   güvenli kök, traversal/reparse/link, boyut, isim ve gerçek içerik imzası
   kontrol edilir. Yalnız geçerli PDF/PNG/JPEG, 1–20 MB, isim en fazla 260
   karakter. Arşivden okuma limitli olmalı; Thumb girdileri alınmaz.
5. Güncel zararlı yazılım taraması yapılıp araç/sürüm/zaman/sonuç hash'i
   güvenli kanıta alınır. Önceki MIME/hash kontrolü malware taraması değildir.
   Şüpheli/şifreli/okunamayan içerik karantinada kalır, otomatik dönüştürülmez.
6. Bakım, yedek/kurtarma, gerçek operatör ve public-link kabulü tamamlanır.
   Küçük pilot sınırı, eşzamanlılık, timeout ve retry limiti sorumlu tarafından
   belirlenir. Pilot ve tüm aktarım **ayrı onaylardır**. Onay şablonu tümüyle
   kapalıdır; alanları doldurmak tek başına uygulama yetkisi vermez.

## 4. Ayrı canlı dosya adaptörünün işlem sözleşmesi

Yeni plan; final finansal batch/kaynak, arşiv/içerik hash'leri, mevcut
production namespace, kod sürümü, gerçek canlı sahipler ve kaynak satır
hash'lerine bağlanır. Kararlı dosya anahtarı ortam + kaynak kimliği + sahiplik
bağlamı + içerik hash'ini içermeli; test anahtarı tekrar kullanılmamalıdır.
Rastgele yeni isimle tekrar yükleme yapılmaz. İsim/tür/size/hash değişirse
aynı onayın devamı değil yeni plan gerekir.

Her ilişki için güvenli journal; onay şablonundaki JournalRequiredFields
alanlarını ve şu aşamaları tutar:

`Planned → LocalVerified → ObjectVerified → Attached → PublicVerified → Accepted`

`Held`, `UploadOutcomeUnknown`, `AttachOutcomeUnknown`, `ReadbackFailed`
ayrı sonuçlardır. Journal append-only, yetkisi kısıtlı ve Git dışındadır;
aktarımı tek başına kanıtlamaz, DB ve nesneyle tekrar karşılaştırılır.

- Önce yerel dosya tekrar doğrulanır. Nesne varsa tam authenticated GET ile
  hash/MIME/size kontrol edilir; fark varsa üstüne yazılmaz veya silinmez.
  Yoksa mevcut `R2FileStorage.UploadAsync` kullanılır; sadece beklenen yokluk
  sonucu yok kabul edilir. 403/timeout, yokluk sayılmaz.
- Timeout/belirsiz PUT'ta yeni anahtar veya kör PUT yok; aynı anahtarı tam
  readback ile uzlaştır. Eşzamanlı başka yazıcı ihtimali kapanmıyorsa dur.
  Mevcut PUT koşullu create garantisi vermediğinden tek-yazıcı ve çakışma
  kontrolleri gerçek kabulde kanıtlanmalıdır.
- Uzak byte SHA-256/MIME/size doğrulanmadan SQL ilişkisi açılmaz. Ağ transferi
  boyunca geniş finansal transaction açık tutulmaz. Kısa SQL transaction'da
  owner/map/rowversion ve mevcut ilişki tekrar kontrol edilip attachment
  bağlanır. Sözleşmede StoredFileName tekil; müşteri dosyasında SourcePath
  tekildir. İlgili hash/karar/journal ve audit mutabakatı korunur.
- Kullanıcı tarafından kaldırılmış ilişki geri etkinleştirilmez; farklı
  sözleşme/müşteriye bağlı aynı key, aynı adlı farklı içerik veya eşzamanlı
  değişiklik bekletilir. Sahiplik dosya adına veya yalnız content hash'e göre
  türetilmez. Teknik audit aktörü 0 mevcut göç geleneğidir; gerçek operatör
  onayı ayrıca kaydedilir, insan kimliği taklit edilmez.
- SQL commit belirsizse yeni bağlantıyla aynı key/owner/metadata/audit
  uzlaştırılır. SQL rollback oldu diye CDN nesnesi silinmez. Mevcut ilişki
  eksiksizse tekrar 0 yeni ilişki/0 gereksiz PUT üretmelidir.

## 5. Erişim ve pilot kabul matrisi

| Kontrol | Beklenen sonuç / kapsam |
| --- | --- |
| Authenticated R2 readback | Her final nesnenin gerçek SHA-256, MIME ve byte boyutu yerelle aynı. HEAD/ETag yeterli değil. |
| Public CDN URL | Her final anahtar mevcut GetPublicUrl ile üretilir; HTTPS ve onaylı host/prefix, tam GET byte hash'i/MIME/size doğru. 200 HTML/login/hata sayfası başarı sayılmaz. |
| Redirect/fallback | API uploads 302 tek başına kabul değil; nihai onaylı CDN içeriği doğrulanır. Başka hosta redirect izlenmez; local fallback CDN başarısı sayılmaz. |
| Liste API yetkisi | Oturumsuz 401/403 veya mevcut auth reddi; oturumlu ama View'suz 403; View sahibi yalnız ilgili aktif dosyaları sayfalı görür. Hassas URL listesi yetkisize dönmez. |
| Yazma yetkisi | View-only kullanıcı Edit işlemi yapamaz. Başka owner'ın attachment ID'siyle kaldırma reddedilir; bu yazmalı negatif kontroller yalnız izole provada yapılır. Canlı smoke test veri silmez. |
| Direct URL maruziyeti | Public URL oturumsuz açılabilir; bu mevcut modelin beklenen davranışıdır, API yetki atlaması testiyle karıştırılmaz. Linki bilen için gizlilik/iptal garantisi verilmez. |
| UI sözleşme/müşteri | Gerçek canlı ID'li detayın dosya tabında doğru isim/tür/boyut ve bağlantı; PDF/görsel yeni sekmede açılır. Pilot farklı türleri ve varsa çoklu ilişkiyi kapsar. |
| Eksik/kaldırılmış dosya | Sahte URL/boş attachment yok; sözleşme korunur. Kaldırılmış satır tekrar etkinleşmez; eski CDN linki kaldırmayla iptal olmaz. |
| Kesinti/tekrar | Upload sonrası SQL öncesi, SQL sonrası journal öncesi, timeout ve aynı/çelişen anahtar durumları izole provada doğrulanır. Aynı plan replay'inde çift ilişki/yeniden upload yok. |

Public GET doğrulaması sınırlı boyut/timeout ile yapılır; yalnız onaylı
nesnelere istek atılır. TLS kapatılmaz, bucket listelemesi veya tüm CDN
taraması yapılmaz. CSP/CORS/cache hatası varsa ayrıca raporlanır; mevcut ortak
altyapı bu hazırlık kapsamında değiştirilmez. Resim/PDF yeni sekme açılışı
çalışıyor diye authenticated içerik/hash kabulü atlanmaz.

## 6. Kısmi hata, geri dönüş ve teslim raporu

Pilot başarısızsa tüm aktarım başlamaz. İşlem sahibi final raporda aday,
uploaded/önceden mevcut, readback doğrulanmış, yeni/mevcut ilişki, public kabul,
bekleyen/eksik/kaldırılmış ve hata adetlerini **ilişki/nesne ayrı** gösterir.
Kaynak toplamları ile her durumun toplamı tutarlı olmalı; kayıp satır olmaz.
Tam kabul için istisnalar açıkça iş birimine sunulur; sıfır hata iddiası
yapılarak eksik dosyalar gizlenmez.

Yetim yükleme, belirsiz commit veya kısmi public kabulde sonraki satırlar
durdurulur ve journal/DB/R2 mutabakatı yapılır. Otomatik DeleteMany/DELETE,
prefix temizliği veya shared bucket purge yok. Temizlik gerekirse yalnız bu
run'da yeni yaratıldığı kanıtlanan, referanssız tam object-key listesi için
ayrı açık onay alınır. Önceden var olan/paylaşılan nesneler silinmez.
Metadata kaldırma public erişimi geri almaz; ifşa durumunda ayrı incident ve
onaylı erişim müdahalesi gerekir. SQL restore CDN'yi geri almaz; dosya yüklemek
için tüm finansal yazma/SMS açılmaz.

## 7. Bu hazırlığın doğrulaması ve kalan uygulama

BE kökünden `./tools/Test-CollectionCdnPreparation.ps1` yalnız yerel paket ve
kapalı şablonu okur; config, SDK, SQL, HTTP, arşiv açma veya çıktı yazma yok.
Bundle/kanıt hash'leri, 9.517 plan satırı ve 1.818+3 aday, 69/19 ve 12 bekleyen
adetleri, aynı yol hash tutarlılığı ve kapalı bayraklar doğrulandı. Bu kontrol
fiziksel byte'ları yeniden taramaz, malware veya canlı HTTP testi değildir.
PowerShell parser kontrolü ve repository dışı yol/yanlış şablon reddi geçti;
`git diff --check` başarılı. Runtime değişmediğinden uygulama build'i yapılmadı.

Sıradaki bağımlılıklar: ayrı dosya adaptörü/izole prova → final owner ve
production config kabulü → onaylı pilot → tam aktarım/readback → API/UI kabulü.
Gerçek production erişim ve upload henüz doğrulanmadı. Hazırlık, yeni canlı
yazma yetkisi veya production-ready kararı değildir.
