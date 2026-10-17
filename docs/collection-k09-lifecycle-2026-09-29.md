# K09 — Sözleşme yaşam döngüsü ve manuel düzeltmeler

## Tahsilat ekibi geri bildirimi — 30 Eylül

**Son oturum kontrolü:** Kullanıcı son kodla API'yi yeniden başlattı. Takip GET toplamları Haziran 2026 için TRY 734.205,17 dönem borcu / 602.547,17 ödeme / 131.658,00 kalan döndürdü ve ekranda görüldü; konsol hatası yok. Toplu zam yılı/ayı ve sözleşme ödeme yöntemi alanı görüldü. Ödeme yöntemleri legacy seed'de tümü pasifti: müşteri isteğine istinaden sadece GTS/BANK_TRANSFER için `20260930230000_EnableCollectionRequestedPaymentMethods` veri migrationı testte uygulandı; diğer legacy yöntemleri pasif kalır. Bu migration canlı paketine eklenmelidir. Aşağıdaki “yeni migration yok / yeniden başlatma bekleniyor” notları bu son tespitten öncesine aittir. Kısıtlı yetkili kullanıcıyla 403/negatif HTTP kabulü ve finansal kayıt değiştiren UI kabulü hâlâ açık; gerçek veride zam uygulanmadı.

- Takip altında para birimi bazında dönem borcu (tahsil edilecek), tahsil edilen ve net kalan eklendi. `GET tracking/totals` aynı SQL BuildRows/filtre/kapsam üzerinde çalışır; yalnız görünen sayfa toplanmaz, kur çevrimi yapılmaz. Sayfa/sıralama değişimi aynı toplam cache anahtarını kullanır; tekrar sorgu ve toplu tahsilat sonrası yenilenir.
- Excel dışa aktarma butonu üst sağa taşındı. Mevcut tüm filtre sonuçlarını akışla indiren UTF-8 CSV korunur; dosya XLSX diye sunulmaz.
- Yeni sözleşmede mevcut ödeme yöntemi tanımlarından GTS/havale vb. seçim eklendi; sözleşme özetinde görünür. Ödeme dönemi (aylık vb.) farklı alandır. Mevcut sözleşmede Manuel Düzeltmeler ödeme yöntemi alanı korunur. Aktif tanım doğrulaması ve tekrar isteği hash'i dahil; eski metotsuz istek hash'leri değişmez. Yeni tanım/table/provider oluşturulmadı.
- Kullanıcı yıllık zam oranını ekibin girip onaylamasını seçti. Mevcut toplu zam ekranına yıl, yıl dönümü ayı filtresi ve sözleşme tarihi eklendi. API yıl dönümünü StartDate'ten hesaplar; ekip her yıl yeniden onaylar, otomatik oran/job yok. 29 Şubat normal yılda 28 Şubat. Yalnız gelecekteki yıl dönümü planlanır; geçmiş/bugün, sona ermiş/donuk/ücretsiz, mevcut ileri tarife ve ilgili dönemden itibaren ödeme bulunan sözleşmeler değiştirilmez. Genel tekil tarife değişikliği davranışı korunur. Eski borç/ödeme ve yenileme günü değişmez; mevcut hesaplayıcı gelecekteki tarife dilimini uygular.
- SQL/CDN K09 kontrolüne bireysel/grup filtre toplamı mutabakatı, yıllık zam tarihi, eski tarifenin korunması ve tekrar reddi eklendi; tamamı testte geçti, geçici kayıt/dosyalar temizlendi. Yeni migration yok. BE Release/Import ve FE Vite build başarılı. Değişen frontend lint temiz; genel TS önceden mevcut modül dışı hataları sürüyor.
- Kullanıcının ilk API restartı sonrasında müşteri detay GET ekranı açıldı. Bu geri bildirimle yeni BE kodu geldiğinden tekrar restart istendi. Son toplam/yıllık zam HTTP ekran kabulü ve role göre HTTP reddi henüz tamamlanmadı; nihai K09 kabulü açık tutulur. Arşivde kalan 12 eşleme istisnası değişmedi.

## 30 Eylül — son teknik doğrulama

- Legacy Dosyalar kontrolündeki yükleme/kaldırma karşılığı müşteri kartına tamamlandı. Mevcut CDN servisiyle PDF/PNG/JPG, en fazla 20 MB; içerik imzası kontrolü, server-side liste, ortak toast ve Edit/View yetkisi kullanılır. API yalnız GET/POST.
- Yükleme işlem anahtarı + içerik hash'iyle tekrar güvenli; kaldırılan dosya aynı istek veya arşiv aktarımıyla yeniden etkinleşmez. Kaldırma kullanıcı/tarih kaydını korur; CDN nesnesini silmez veya URL erişimini iptal etmez. Değiştirme yeni dosyayı yükleyip eskisini listeden kaldırarak yapılır.
- `20260930200005_AddCollectionCustomerFileActions` yalnız AssistFlowTest'e uygulandı. Manuel dosyada legacy kimlik/arşiv hash'i boş olabilir; aktarılmış dosya kanıtları değişmez. Ortak müşteri tabloları/MGS/canlı değişmedi.
- Gerçek test SQL/CDN kontrolü: yükleme, tekil tekrar, yanlış müşteri reddi, ilk kaldırma audit'inin korunması, CDN arşivi ve yeniden etkinleşmeme başarılı. Finansal K09 kontrolleri de tekrar geçti; yalnız koşunun geçici kayıtları/dosyaları temizlendi.
- Oturumlu tarayıcıda müşteri16838 ve **üç arşiv dosyası** görüldü; yükleme/kaldırma kontrolleri mevcut yetkili oturumda görünür. Gerçek müşteri dosyası kaldırılmadı veya finansal kayıt değiştirilmedi.
- BE Release/aktarım aracı build ve FE test-mode Vite build başarılı; değişen FE lint temiz. Genel TypeScript kontrolünde modül dışı mevcut hatalar devam ediyor; değişen müşteri dosyası dosyalarında hata yok.
- **Kalan kabul:** çalışan backend yeni kodla yeniden başlatıldıktan sonra yeni POST uçlarının tarayıcı/HTTP yetki kabulü; gerçek finansal UI kabulü. 12 arşiv dosyasının doğrulanmamış eşlemeleri ayrı veri istisnası olarak açık. K09 bütünü henüz nihai kabul edilmiş değildir.

## 30 Eylül — onaylı eski abonelik dosyası devri

Kullanıcı, elenen aboneliklerin 6 dosyasının **doğru güncel müşteri eşlemesi doğrulandığında** devrini onayladı. Ödemeler MGS'de kalır.

- Tekilleştirme karar kayıtları 924 → 291401 ve 2206 → 87400 olarak okundu. Eski abone numarasının hedefini doğrudan kullanmak doğru değildir.
- 924 → korunmuş legacy 291401 → AssistFlowTest müşteri **16838** tekil abone numarası ve tahsilat kapsamıyla doğrulandı. Bu korunmuş müşterinin aktarılmış sözleşmesi yok; dosyalar sözleşmeye değil mevcut müşteri kartına bağlandı. Ortak müşteri bilgileri değiştirilmedi.
- **3 dosya aktarıldı.** Her dosya için arşiv hash'i, içerik hash'i, kaynak yolu, eski/korunmuş müşteri kimlikleri ve devir kararı `collection.CustomerAttachment` içinde korunur. Mevcut R2FileStorage/test CDN kullanıldı; sağlayıcıya değişiklik yok. CDN'den geri okunan üç içerik kaynak SHA256 ile aynı.
- Tekrar çalıştırma: **0 yeni / 3 mevcut**, üç içerik yeniden doğrulandı. Belirsiz hata durumunda CDN nesnesi silinmez; deterministik içerik anahtarı ve tekil SourcePath ile devam edilir.
- Plan hash: `BBBADB22D4F735A19D5C6D8BDE7C6C49E5D44C32BACBA2FC560D94F3F545A7FB`.
- `20260930194336_AddCollectionCustomerAttachments` sadece AssistFlowTest'te uygulandı. Şema collection; mevcut dbo.Customer/Customers tabloları değiştirilmedi. MGS ve canlı AssistFlow salt-okunur kaldı.
- `/crm/collections/customers/16838` → **Müşteri Dosyaları** sekmesi eklendi. İlk dilim yalnız GET listeydi; yukarıdaki son teknik doğrulama ile manuel yükleme/listeden kaldırma da tamamlandı.
- **Diğer 3 dosya bekliyor:** eski 2206'nın korunmuş kaydı 87400'dür. Korunmuş kayıt için uygun test müşteri eşlemesi doğrulanamadı. Eski aboneliğin 16598 kartı kullanılmadı. Yeni ortak müşteri oluşturma/sınıflandırma yapılmadı; doğru hedefin netleşmesi gerekir.
- Diğer 9 asıl dosyanın önceki eşleme/kapsam sorunları devam ediyor; Thumb dosyaları aktarılmadı. Toplam arşiv sonucu: **3/15 asıl dosya aktarıldı, 12 bekliyor**.
- BE solution/FE Vite build ve değişen FE lint başarılı; genel TypeScript mevcut hatalarla başarısız, değişen müşteri dosyası ekranlarında hata yok. Gerçek SQL/CDN ve liste servisi doğrulandı; oturumlu browser/HTTP kabulü açık.

## 30 Eylül güncellemesi — müşteri kararı ve Aktivite arşivi

- Kullanıcı VAR/boş → YOK geçişinde geçmiş borçların korunmasını, yalnız ileriye dönük borç oluşumunun durmasını onayladı. Bu iş kararı artık açık değildir.
- Manuel Düzeltmeler sekmesine **YOK yap / borç oluşumunu durdur** eklendi. Günlük hesapta bugün doğmuş borcu da korumak için Türkiye saatine göre ertesi gün başlangıcında Suspended tarife dilimi açılır. Eski dilim bu sınırda kapanır; tutar, yenileme günü, eski tahakkuklar ve tüm ödemeler korunur. Sözleşme durumu NONE olur; borç davranışı geçmişe uygulanmaz.
- Gerekçe/onay, sürüm kontrolü ve kalıcı audit/makbuz aynı transaction içindedir. Aynı istek ikinci dilim açmaz. Eksik/ileri tarihli plan, bitmiş/başlamamış sözleşme ve zaten YOK durumu reddedilir. Yeni migration yok; mevcut ContractCorrection kullanılır. Gerçek müşteri sözleşmesi YOK yapılmadı.
- `k09-check` geçici test kayıtlarında geçmiş borçların tutar/tarih/para birimi eşitliğini, ödemenin korunmasını, üç aylık gelecekte ek borç oluşmamasını ve tekrar isteğinin tek işlemi korumasını doğruladı. Test kayıtları/CDN dosyası temizlendi.
- Tarife tarih düzeltmesinde komşu dilimler arasında boşluk bırakma da engellendi: hesaplayıcı kesintisiz tarihçe bekliyor. Önceki “boşluk oluşturulabilir” açıklaması geçersizdir; UI düzeltildi.
- `Aktivite.rar` geldi; kaynak bekleme konusu kapandı. Arşiv bütünlüğü başarılı, 51.574 klasör, 15 asıl dosya / 7 müşteri, 6 Thumb dosyası. Eşleme incelemesi salt-okunur `activity-preview` komutuyla yapıldı; ayrıntılar aşağıda. Hiçbir asıl dosya için korunmuş kesin tahsilat müşteri eşlemesi bulunmadığından CDN/DB aktarımı yapılmadı.

| Legacy müşteri | Asıl dosya | Tespit / gereken karar |
| --- | ---: | --- |
| 924 | 3 | Önceki tekilleştirmede elenen abonelik. Aynı abone numarasında 16838/MGSK adayı var; bu, eski dosyaları o müşteriye bağlama yetkisi değildir. Dosya devri kararı gerekir. |
| 2206 | 3 | Aktarılan eski abonelik daha sonra elenmiş. 16598/MGSK adayı var; eski dosyaların devri ayrıca onaylanmalı. |
| 1922 | 4 | Sözleşme stage kayıtları Excluded/Blocked; hedef müşteri yok. 3. madde müşteri eşlemesi/oluşturma kararıyla birlikte çözülmeli. |
| 53768 | 1 | MGS tipi A; tahsilat sözleşme stage/korunmuş eşleme yok. Doğru hedef/kapsam gerekir. |
| 205263 | 1 | MGS tipi A; tahsilat sözleşme stage/korunmuş eşleme yok. Doğru hedef/kapsam gerekir. |
| 53762 | 1 | Mevcut MGS.Core.Customer içinde kimlik bulunamadı; sahiplik teyidi gerekir. |
| 207084 | 2 | Mevcut MGS.Core.Customer içinde kimlik bulunamadı; sahiplik teyidi gerekir. |

Arşiv SHA256: `398B4120D946D7969A3569771DE8E03F2E4FDE59A9CCA045CA7A5D611F96C480`. Dosyalar açılmadı/çalıştırılmadı veya silinmedi. Thumb öğeleri asıl müşteri dosyası olarak aktarılmayacak. Eski abonelik ödemelerini aktarmama kararı değişmedi; dosyaların devri ayrı konudur.

K09'un güncel açığı dosya sahipliği kararları, ayrı müşteri dosyası ekranı/aktarımının tamamlanması ve browser/HTTP kabulüdür. Aşağıdaki 29 Eylül durum notlarında geçen VAR/YOK kararı ve arşiv bekleme maddeleri bu güncellemeyle çözülmüştür.

## Mevcut durum

Legacy kapsam audit'i §9/§10 ile mevcut servis/ekranlar tekrar karşılaştırıldı. `CollectionSubscriptionService` aktif/donuk geçişini günlük tarife dilimiyle, `CollectionRateChangeService` ücretli/ücretsiz tarife değişimini mevcut tarihçeyi kapatıp yeni dilimle yürütüyor. Ödeme update/physical delete ve kalıcı PaymentOperation audit'i mevcut. Bunlar yeniden geliştirilmeyecek; sınırları K09 içinde kontrol edilecek.

## Tamamlanan ilk parça: sözleşme dosyasını listeden kaldırma

- Dosyalar sekmesine dosya adıyla onay isteyen **Listeden kaldır** eklendi; ortak ConfirmDialog/toast kullanılır. Finansal detaylar yine /:id ekranındadır.
- `POST /api/collections/contracts/{id}/attachments/{attachmentId}/remove`: mevcut View/Edit yapısındaki Edit, tahsilat feature flag, aktif kullanıcı, onaylı tahsilat kapsamı ve dosyanın belirtilen sözleşmeye ait olması doğrulanır.
- Mevcut Attachment.IsDeleted/UpdatedUser/UpdatedDate kullanılır; kimlik, özgün dosya ve aktarım kanıtları korunur. İşlem serializable; tekrar kaldırma ilk kullanıcı/tarihi değiştirmez. Dosya metadata'sına düzenleme endpoint'i olmadığı için kaldırılacak içerik kimlikle sabittir.
- CDN sağlayıcısı veya fiziksel dosya değiştirilmez/silinmez. **Bu işlem erişim URL'sini iptal etmez; yalnız sözleşmenin aktif dosya listesinden kaldırır.** Finansal kayıtların fiziksel silinmesi kararıyla karıştırılmaz.
- Dosya değiştirme: önce doğru dosyayı yükleme, ardından eskisini listeden kaldırma. İki ayrı işlem; yeni yükleme başarısız olursa eski dosya korunur. Atomik dosya değiştirme iddiası yok.
- Mevcut aktarım aracı kaldırılmış metadata'yı yeniden aktifleştirmiyor: CheckAttachment kaldırılmış kaydı reddeder. Böyle bir durumda operatör mutabakatı gerekir; arşiv dosyası kendiliğinden geri gelmez.
- Dosya kaldırma için yeni tablo/migration yok. Gerçek müşteri dosyası kaldırılmadı. SQL/CDN kontrolü aşağıdaki geçici kayıtlarla tamamlandı; oturumlu ekran kabulü açık.

## Tamamlanan ikinci parça — 29 Eylül

- Sözleşme detayında **Manuel Düzeltmeler** sekmesi: başlangıç/bitiş, ödeme yöntemi ve geçmiş tarifenin tarih aralığı/tutar/para birimi/ödeme dönemi düzeltmesi. Gerekçe, açık etki onayı, sözleşme/tarife sürüm kontrolü, eski/yeni değerler ve kullanıcı/tarih zorunlu. Sayfalı düzeltme geçmişi aynı sekmede.
- Tarife tarih aralığı düzeltmesinde komşu tarifelerle çakışma ve arada boşluk engellenir; eski ve yeni aralıkların birleşimindeki ödemeler kontrol edilir. Yenileme başlangıcı/günü ve borç davranışı korunur; normal aktif/donuk/ücretsiz geçişleriyle karıştırılmaz. Komşu dilimler otomatik taşınmaz.
- Tarihler tarife aralıklarını veya yenileme gününü kendiliğinden taşımaz. Bitiş değişikliğinin etkilediği dönemlerde ödeme varsa işlem engellenir. Geçmiş tarife aralığında ödeme varsa finansal düzeltme engellenir; ödeme yöntemi seçimi ücretsiz hizmete geçiş değildir.
- Ödeme hareketlerinde **Sözleşmeyi düzelt**: mevcut ödeme kimliği, tutar, tarih, dönem ve para birimi korunarak uygun ücretli tarifesi bulunan hedef sözleşmeye taşıma. Kaynak/hedef kapsamı doğrulanır, gerekçe PaymentOperation geçmişinde korunur. Banka satırındaki sözleşme aynı transaction içinde güncellenir; banka işlem tekilliği silinmez. Legacy aktarım makbuzu eski ödemeyi yeniden oluşturmaz/üstüne yazmaz.
- **Sözleşmeyi sil**: ödeme, dosya (listeden kaldırılan dahil), dönem takibi, banka satırı veya migration map bağlantısı olan sözleşmeler engellenir. Yalnız bağımsız sözleşme ve en fazla 1.000 tarifesi fiziksel silinir. Önceki değerler, tarife kopyası ve gerekçe bağımsız audit tablosunda kalır. Silinmiş sözleşmenin özgün oluşturma isteği tekrar gönderilirse yeniden oluşturulmaz. Gerçek müşteri sözleşmesi silinmedi.
- `collection.ContractCorrection` ve `20260929130125_AddCollectionContractCorrections`: yalnız AssistFlowTest'e uygulandı. Audit ve tekrar makbuzu aynı serializable transaction içinde kaydedilir. Endpointler GET/POST; mevcut Edit/View ve feature flag yapısı kullanılır. DI Autofac'tadır.
- `Collections.Import k09-check <Development JSON>` yalnız AssistFlowTest hedefini kabul eder. Gerçek test SQL/CDN üzerinde tarih/tarife düzeltmesi, tekrar isteği, eski sürüm reddi, ödeme taşıma, ödemeli dönem engeli, dosya sahipliği/arşiv korunması, bağımlılıklı silme reddi, bağımsız fiziksel silme ve eski oluşturma isteğinin reddi doğrulandı. Yalnız koşunun geçici kayıtları/CDN dosyası temizlendi. Bu servis entegrasyon kontrolüdür; HTTP yetki veya browser kabulü değildir.

## Kalan geliştirme sırası ve kabul kriterleri

TypeScript genel denetimi mevcut proje hatalarıyla başarısız (çıkış 2); değişen CollectionContractAttachments/CollectionContractService dosyalarında hata bildirilmedi. Genel type-check başarılı sayılmadı.

1. **Sözleşme VAR/boş → YOK düzeltmesi:** 30 Eylül kullanıcı kararıyla geçmiş korunarak ileriye dönük durdurma uygulandı ve doğrulandı; açık iş kararı değil.
2. **Korunan yaşam döngüsü sınırı:** tarihsel tarife tarih/tutar/para birimi/dönem düzeltmesi geliştirildi. Yenileme günü/anchor ve borç davranışı bu formda değiştirilmez; normal aktif/donuk ve ücretli/ücretsiz geçişleri mevcut servislerde korunur. Bu alanları keyfi toplu güncelleyen yeni bir akış eklenmedi.
3. **Ayrı müşteri dosyaları:** Aktivite.rar 30 Eylül geldi ve doğrulandı. 15 asıl dosyada kesin korunmuş tahsilat müşteri eşlemesi bulunamadı; yukarıdaki sahiplik kararları açık. Müşteri dosyası UI/aktarım tamamlandı sayılmaz; kaynak arşiv artık beklenmiyor.
4. **Nihai kabul:** browser/HTTP yetki ve ekran yenileme; gerçek banka kaynaklı taşımanın yükleme ekranıyla birlikte kabulü; yaşam döngüsü donuk/ücretsiz/geçmiş finansal toplam mutabakatı. Genel TypeScript kontrolü mevcut proje hataları nedeniyle başarısız; değişen dosyalarda hata bildirilmedi.

K09 bütünü henüz tamamlanmadı. K08 kullanıcı kabulü ve iade/iptal teyidi ayrı açık maddelerdir.

## Son doğrulama

- BE solution build ve FE Vite build başarılı; değişen FE dosyalarının ESLint kontrolü hatasız.
- Genel TypeScript denetimi mevcut proje hatalarıyla başarısız; yeni/değişen K09 dosyalarında hata bildirilmedi. Bu nedenle tam proje type-check başarılı olarak raporlanmaz.
- Tarife tarih düzeltmesi ve komşu tarife çakışması reddi dahil son `k09-check` SQL/CDN koşusu başarılı. Test fixture kayıtları ve dosyası temizlendi.
- Her iki repo `tahsilat-module`; commit/push/merge yapılmadı. Canlı AssistFlow ve MGS değiştirilmedi.
