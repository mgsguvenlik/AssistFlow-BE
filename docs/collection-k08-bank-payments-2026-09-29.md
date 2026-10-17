# K08 — GTS/IVR dosyasından ödeme

## Güncel sonuç — geliştirme ve test entegrasyonu tamamlandı

- `/crm/collections/bank-loads` yükleme/geçmiş, `/:id` özet+sekme ekranları; Tahsilat Takip ekranından erişim. GTS/IVR ve muhasebe dönemi seçimi, 25 satırlı server-side sayfalama, durum filtresi, sözleşme/ödeme bağlantısı, önizleme yenileme ve onay vardır. Yalnız GET/POST, mevcut View/Edit ve Türkçe toast kullanılır.
- `collection.BankLoad`, `BankLoadRow`, `BankTransaction`, `BankBaseline` ile sözleşme GTS/IVR arama indeksleri eklendi. Migrationlar yalnız AssistFlowTest'e uygulandı: `20260929111421_AddCollectionBankLoads`, `20260929112405_AddCollectionBankReferenceIndexes`. Ortak Customer ve CDN sağlayıcısı değiştirilmedi.
- MGS salt okunur profilinde GTS Mail Order ve IVR Satış doğrulandı. GTS TL/USD, IVR TL görüldü. İptal/iade normal tahsilat sayılmaz. Kaynaktaki **83.387** banka kaydının **83.387 tekil, dolu işlem kimliği** hedefte geçmiş korumasına alındı. Hash: `27B3A18F5A1448A89CECAEB49A9D87A6D045498A11E83EE55BD8B1C06D4E77CE`.
- Bu sayı yeni ödeme aktarımı değildir. Tüm legacy banka kimlikleri korunur; kaynak ödemesi hedefe aktarılmamış/eski abonelikte bırakılmış olsa da dosyayla yeniden oluşturulmaz. Başarısız tarihsel kimlikler de otomatik yeniden işlenmez; istisna olarak incelenir. Bu koruma, yalnız hedef ödeme haritasına bakmaktan daha kısıtlayıcıdır.
- Sözleşme referansı tekil ve onaylı kapsamda olmalı; seçilen dönem/para biriminde ücretli tarife bulunmalı. Kart/son kullanma/iletişim kolonları çıkarılır; **orijinal Excel saklanmaz**, yalnız izinli alanlardan üretilmiş JSON mevcut CDN'ye gider.
- Onay başına en fazla 100 hazır satır işlenir; kalanlar tekrar komutla alınır. Mevcut `CollectionPaymentTransaction` kullanılır. Ödeme+kalıcı ödeme audit'i+banka kimliği+satır sonucu tek serializable transaction'dadır. Aynı kimlik farklı dosya/dönemde, eşzamanlı istekle veya ödeme silindikten sonra yeni ödeme üretemez. Önizleme değişmişse 409 ve yeniden onay gerekir. İşlem bir kısmında kesilirse tamamlanan satırlar korunur.
- Gerçek SQL/R2 kontrolü geçti: GTS ve IVR başarılı ödeme, başarısız/iade/eşleşmeyen ayrımı, aynı dosya, farklı dosyada tekrar, eski önizleme, silinmiş ödeme koruması ve **aynı batch'e eşzamanlı iki aktarım**. Test için oluşturulan sözleşme, ödeme, audit, kuyruk ve CDN dosyaları koşu sonunda temizlendi; gerçek kullanıcı ödemesi oluşturulmadı.
- BE solution/import ve FE Vite build başarılı; değişen FE dosyalarında ESLint temiz. Genel TypeScript kontrolünde mevcut proje hataları devam ediyor; K08 dosyalarında hata yok. Saf parser kontrolü başarılı.
- **Nihai kullanıcı kabulü açık:** yerel FE/API dinlemiyordu; oturumlu tarayıcı/gerçek banka dosyası ve HTTP yetki kabulü bu koşuda doğrulanmadı. Bu sonuç canlı yayın onayı değildir. İade/iptaller için otomatik negatif ödeme eklenmedi; manuel inceleme önerisi kullanıcıya soruldu, henüz yanıt yok.

## Kaynak ve kapsam

Legacy `wwwroot/Pages/Core/Payment.aspx.cs`, `DataTableToDatabase` (3402+), `DatabaseToPayment` (3650+) ve `SQL_Payment_GTS/IVR` incelendi. Dosya → Core.BulkPayment kuyruğu → kullanıcı komutuyla Core.Payment akışı vardır; otomatik Job değildir. Yeni akış da kullanıcı onaylı olacaktır.

| Bilgi | GTS | IVR |
| --- | --- | --- |
| Banka işlem kimliği | RRN | İşlem Numarası |
| Sözleşme eşleştirmesi | Kayıt No → Contract.GtsNo | Sipariş Numarası → Contract.IvrNo |
| Banka başarısı | Dönüş Kodu = 00 | İşlem Durumu = Başarılı |
| Ödeme tarihi | İşlem Tarihi | İşlem Tarihi |
| Tutar | Tutar | Sipariş Tutarı |
| Para birimi | Kur | Döviz cinsi |
| Dönem | Kullanıcının seçtiği ay | Kullanıcının seçtiği ay |

Legacy GTS `Serbest İade` ve IVR `İade` tutarını negatif yapar. Ancak GTS kuyruğa alma koşulu yalnız dönüş koduna bakar; işlem tipi açıklamasına rağmen başka işlem tipleri de ödemeye dönüşebilir. TOP 1 sözleşme seçimi ve ödeme/kuyruk güncellemesinin ayrı olması da korunmayacak hatalardır.

## Tarihsel ilk parça kaydı

- Mevcut ClosedXML kullanılarak GTS/IVR saf dosya okuyucusu ve DTO eklendi. Henüz API/UI veya kalıcı kuyruk yok; ödeme oluşturmaz.
- Tür ve dönem açıkça seçilir, başlıktaki ilk kolona bakarak dosya türü tahmin edilmez.
- XLSX, 10 MB, açılmış içerik 100 MB, 5.000 satır sınırı; başlık, tarih, hassas kodların metin oluşu, ondalık tutar, formül ve aynı işlem kimliği tekrar kontrolleri.
- Sonuçlar `Candidate` (yalnız biçim uygun), `Review`, `BankFailed`. Candidate eşleşmiş/onaylanmış ödeme değildir.
- Kart numarası, son kullanma, adres ve iletişim kolonları çıktı DTO'suna alınmaz. Kaynak dosya şu aşamada hiçbir yere kaydedilmez.
- GTS Mail Order dışındaki türler ve henüz kaynak değerleri doğrulanmamış IVR türleri incelemede tutulur. İade otomatik pozitif/negatif ödeme yapılmaz.
- Doğrulama: BE solution ve import aracı derlemeleri sıfır hatayla geçti (mevcut uyarılar sürüyor). `bank-file-check` bellek içi örneklerle GTS/IVR, başarısız banka sonucu, iade, belirsiz tutar, formül, tekrar ve kart alanı dışlama kontrollerini geçti. DB/CDN yazılmadı. Gerçek banka dosyası, API ve tarayıcı kabulü yapılmadı.

## Başlangıçtaki bağımlılık sırası / kabul ölçütleri

1. MGS BulkPayment üzerinde yalnız toplulaştırılmış, salt okunur işlem tipi/kur/dönüş kodu profili: gerçek IVR tahsilat türlerini ve kur anlamını doğrula. Yeni banka formatı kaynakla uyuşmazsa güncel örnek iste; tahmin etme.
2. Onaylı kapsam içindeki GtsNo/IvrNo tekil sözleşme, dönem/tarife/para birimi kontrolü. Sıfır veya çoklu eşleşme karantina; TOP 1 ve otomatik kur dönüşümü yok.
3. `collection` altında kalıcı batch/satır + banka işlem kimliği benzersizliği ve audit. Daha önce legacy Payment'a çevrilmiş BulkPayment kanıtı mevcut ödeme aktarım haritasıyla karşılaştırılmalı; tarihsel ödeme tekrar üretilmemeli. Dosya adı/hash kontrolü tek başına yeterli değildir.
4. Mevcut ödeme komut altyapısıyla tekrar çalıştırılabilir, eşzamanlılığa dayanıklı uygulama. Ödeme yazılıp kuyruk güncellenmeden bağlantı kesilse de tekrar ödeme oluşmamalı. Silinmiş ödeme audit'i tekrar oluşturmayı engellemeli.
5. Tahsilat altında yükleme/geçmiş ve /:id özet+sekme; server-side sayfalı önizleme, Türkçe toast, GET/POST ve mevcut View/Edit. Kart içeren orijinal dosyayı CDN'ye yükleme; gerekiyorsa yalnız izinli alanlardan yeniden üretilmiş temiz dosyayı mevcut CDN ile sakla. Sağlayıcıya değişiklik yok.
6. Banka başarısız, belirsiz, iade, geçmişte aktarılmış ve yinelenen kayıtların ödeme üretmediğini; geçerli kaydın tam bir kez oluştuğunu test ortamında doğrula. Gerçek banka dosyasıyla kullanıcı kabulü ayrıca gerekli.

## Açık konu

Legacy iade kayıtları mevcut. Kullanıcının fiziksel finansal düzeltme kararıyla otomatik negatif banka iadesi aynı işlem değildir. İadeler listelenir ancak otomatik uygulama, mevcut iş kuralı ve kullanıcı kararı netleştirilmeden açılmaz. Diğer geliştirmeler buna bağlı olmadan sürer.
