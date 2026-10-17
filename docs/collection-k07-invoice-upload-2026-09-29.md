# K07 — Kesilen fatura dosyası yükleme

## Güncel sonuç — uçtan uca geliştirme tamamlandı

Bu bölüm aşağıdaki ilk parça/gelecek sıra notlarının önündedir. K07 ekranları ve servisleri tamamlandı; kullanıcı tarayıcı kabulü ile gerçek müşteri dosyası provası henüz tamamlanmış sayılmaz.

### Veri ve cari eşleme

Migrationlar `20260929100739_AddCollectionInvoiceLoads` ve `20260929101445_AddCollectionInvoiceLoadRowAudit` yalnız AssistFlowTest'te uygulandı. InvoiceAccount cari kodu, tekil hedef CustomerId veya hata, kaynak müşteri kimlikleri/hash ve doğrulama tarihi tutar. InvoiceLoad tür/dosya hash/CDN anahtarı/oluşturan/rowversion; InvoiceLoadRow özgün satır JSON, Excel satır numarası, eşleşme, hata/durum, fatura ID, tekrar anahtarı ve satır uygulayan/tarih bilgilerini korur. Finansal silme bu satırları silmez.

MGS.Core.Customer.AccountNo salt-okunur tarandı. Hedef yalnız K06'da kullanılan korunan sözleşme stage/map ve onaylı GroupParent kaynak kimliklerinden bulunur. Aynı cari kod legacy'de birden fazla müşteriye aitse tek hedef adayı olsa bile otomatik seçim yapılmaz. Tüm adaylar güncel tahsilat kapsamıyla kontrol edilir. Runtime API, MGS'ye bağımlı değildir; collection.InvoiceAccount kullanır.

13.766 kaynak cari kod: 2.910 eşleşmiş, 699 çoklu legacy müşteri, 10.156 korunan tekil hedef yok, 1 kapsam dışı. Hesap kodları büyük harf/kenar boşluğu normalizasyonuyla saklanır, baştaki sıfırlar korunur. Plan hash'i `A0A7CDDEF00EED6DB0CAD0D6C1CF6CA7FC7DC1009ED4FB3E8AD263764A8CF507`. Kanıtlar `.tools/k07-accounts.json` ve `.tools/k07-accounts-applied.json`; kişisel veri içerebileceğinden Git dışında. Belirsiz kodlar hata mesajıyla saklanır; müşteriler oluşturulmaz/değiştirilmez. Bu sayılar fatura aktarım adedi değildir.

### API ve işleyiş

- GET `/api/collections/invoice-loads`, `/{id}`, `/{id}/rows?page=1&pageSize=25&status=Error`.
- POST multipart `/api/collections/invoice-loads` (file, type); POST `/{id}/refresh` ve `/{id}/apply` (rowVersion).
- CollectionFollowUp View/Edit ve mevcut Enabled/ContractCreateEnabled anahtarları. Aktif kullanıcı kontrolü, yalnız GET/POST. Tenant ilişkisi yok.
- Parse başarılı dosya mevcut `IFileStorage.UploadAsync` ile CDN'e gider. Değişmez içerik anahtarı tür+SHA-256'dır. DB/CDN belirsiz yanıtında dosya silinmez; aynı dosyayla tekrar deneme mevcut anahtar/önizlemeyi kullanır. Aynı tür+dosya hash DB'de unique.
- Önizleme hatalı satırları tutar, fatura üretmez. Hazır satırları aktar butonu açık onay ister. Serializable transaction içinde cari/kapsam/para birimi/mükerrerlik yeniden kontrol edilir. Önizleme değiştiyse yeni hali kaydedilip 409 ile tekrar onay istenir; o çağrı faturaya yazmaz.
- Yalnız Ready satırları bir transaction'da faturaya çevrilir. Imported satır tekrar işlenmez. İçe alma finansal iş anahtarı müşteri+tür+normalize fatura numarasıdır; unique ImportedKey farklı dosya/eşzamanlı yüklemede tekrar oluşmasını engeller. Mevcut K06 faturaları ve InvoiceDelete audit snapshotları da kontrol edilir. Önceden silinmiş faturayı dosyayla yeniden yaratma yok.
- Kaynak Ödendi mi değeri korunur, ödeme üretilmez. Döviz kuru dönüşümü yok. Hatalı dosya satırı düzeltilip yeni dosya yüklenebilir; aynı faturaya ait doğru aktarılmış satırlar mükerrer ayrılır. Cari eşleme düzeltildikten sonra refresh ile hata satırı yeniden değerlendirilir. Ortak müşteri editörü/otomatik yeni müşteri süreci eklenmedi.

### Ekranlar

`/crm/collections/invoice-loads`: B/K dosya seçimi ve server-side yükleme geçmişi.

`/crm/collections/invoice-loads/:id`: üstte dosya/tür/sayı özetleri, geri/yenile; altta Önizleme ve Hatalar / Kaynak Dosya sekmeleri. Tablo 25 satırlık server-side sayfalıdır; hazır/hatalı/mükerrer/aktarılmış filtreleri, Türkçe nedenler, kaynak ve eşleşen müşteri, fatura linki vardır. Mevcut DataTable/Select/ConfirmDialog/detail tabs/toast yapıları kullanıldı. Yalnız Tahsilat ekranlarına ekleme yapıldı.

### Doğrulama

Mevcut import aracındaki `invoice-load-check <Development JSON>` yalnız AssistFlowTest'e izin verir. Gerçek R2FileStorage (mock değil) ile rastgele işaretli geçici .xlsx yükledi; iyi/kötü cari satırı ayrımı, dosya adı değişse de aynı batch, liste/detay/hata filtresi, 1 net fatura aktarımı, eski rowversion reddi, başka dosyada aynı fatura engeli, fiziksel silinen faturayı tekrar yükleme engeli geçti. Ödendi mi=Evet ödeme oluşturmadı. Yalnız koşuya özgü geçici cari/fatura/batch/audit/CDN kayıtları finally bloğunda temizlendi; kullanıcı dosyaları, legacy ve önceden aktarılmış faturalar değiştirilmedi. Son koşu başarılıdır; öncesinde yakalanan LINQ projeksiyon ve doğrulama fixture normalizasyon hataları giderildi.

BE solution/import build ve FE build geçti; değişen FE lint temiz, K07 dosyalarında TS hatası yok. Proje genelindeki TS hataları bu iş kapsamında giderilmedi. Oturumlu browser kullanıcı kabulü, gerçek müşteri dosyasıyla prova ve canlıya geçiş ayrıca gereklidir. Kaynak cari istisnaları K12 takibindedir; K07 teknik akışı tamamlanması bu istisnaların onaylandığı anlamına gelmez.

---

## İlk parça geçmişi (aşağıdaki gelecek sıra maddeleri yukarıdaki uygulamayla tamamlandı)

## Referans ve kapsam

Legacy `InvoiceFollowLoad.aspx.cs` dosyayı Excel → Core.InvoiceFollowLoad kuyruğu → Core.InvoiceFollow olarak işler. Kolonlar: **Cari Kod, Müşteri Adı, No, Tarih, Tutar, Ödendi mi, Açıklama, Proje Kodu, Para Birimi**. B/K sayfa seçimi Bireysel/Kurumsal fatura türüne dönüşür. Dosyadaki Ödendi mi alanı ayrı bir InvoiceFollowPayment yaratmaz; yeni sistem de bu alandan ödeme uydurmayacak. Finansal bakiye K06'daki bağlı ödeme toplamına dayanır.

Legacy'nin TOP 1 cari/para birimi seçmesi, cari bulunamazsa otomatik ortak müşteri oluşturması ve önceki tüm kuyrukları tarihçeye taşıması aynen uygulanmayacak. Tahsilat kapsamı dışındaki müşteriler geri alınmayacak. Dosya adı değiştirerek aynı faturayı yeniden yüklemek yeni fatura oluşturmamalı.

## İlk parça — tamamlanan kod

`CollectionInvoiceFileParser` projedeki mevcut ClosedXML bağımlılığını kullanır; yeni paket/CDN altyapısı eklenmedi. Saf doğrulama katmanıdır; DB veya CDN yazmaz, müşteri eşlemesini yapmış saymaz.

- .xlsx; 10 MB sıkıştırılmış/100 MB açılmış içerik ve 5.000 veri satırı sınırı. Eski .xls için anlaşılır dönüştürme mesajı.
- Tek dolu sheet; zorunlu başlıklar ve tekrar eden başlık kontrolü. Veri satırlarının özgün Excel satır numarası korunur.
- Cari kod ve fatura no metin olmalıdır; sayısal hücrelerin kayıp sıfırları tahmin edilmez.
- Gerçek Excel tarihi ve gg.AA.yyyy/ISO metin tarih desteklenir. Biçimsiz seri tarihte 1900/1904 başlangıcı tahmin edilmez.
- Sayısal tutar, Türkçe `1.234,56` veya açık noktalı `1234.56`; belirsiz `1.234` ve ikiden fazla ondalık kabul edilmez. Tarihsel fatura işleyişi gereği sıfır/negatif değer parser tarafından pozitif yapılmaz.
- Formüller değerlendirilmez; ilgili satır hata alır. Makrolu içerik reddedilir. Tutar/para birimi eksikleri tahmin edilmez. TL/YTL/TRL → TRY kod eşdeğerliği, döviz dönüşümü değildir.
- Dosya içindeki aynı cari kod/fatura no tekrarlarının tümü incelemeye ayrılır; ilk satır keyfî seçilmez.
- SHA-256 dosya hash'i, ham bilgilendirici alanlar ve Türkçe satır hataları üretilir. Bu sonuç henüz aktarım onayı değildir.

## Bağımlılık ve sonraki sıra

Doğrulama: Business ve mevcut import aracı derlemeleri başarılı. `invoice-file-check` ile bellekte hazırlanan dosyada başlık, baştaki sıfırların korunması, tarih/Türkçe tutar, belirsiz tutar reddi, formül reddi ve tekrar satır kontrolleri geçti. Test dosyası/CDN kaydı oluşturulmadı; DB erişimi yok. Yeni test altyapısı kurulmadı.

1. **Cari eşleme:** dbo.Customers içinde finansal AccountNo alanı yok. GroupParent yalnız grup üst kartlarının cari kodunu kapsıyor. K06'daki korunan legacy müşteri haritasından finansal cari eşlemeyi collection altında hedefe özel saklamak gerekir; SubscriberCode/CustomerShortCode cari kod yerine kullanılamaz. Çoklu kaynak veya hedef eşleşme ayrı hata olmalıdır. Ortak Customer şemasına kolon eklenmeyecek.
2. **Kalıcı yükleme/kuyruk:** collection altında batch ve row modeli, özgün dosya için mevcut IFileStorage/CDN, aktör/hash/sürüm/audit; tür+dosya ve finansal iş anahtarı bazlı tekrar güvenliği. CDN ile DB atomik değildir; başarısız/şüpheli cevapta kayıtlı dosya metadata'sı güvenli yeniden denemeyi desteklemeli.
3. **API ve ekran:** mevcut GET/POST, CollectionFollowUp View/Edit, Türkçe toast, server-side sayfalı önizleme/hatalı satırlar; `/invoice-loads/:id` özet + sekmeler. Uygula öncesi eşleme/bakiye/tekrarlar sunucuda yeniden doğrulanır. Belirsiz eşleşmeden müşteri yaratılmaz.
4. **Kabul:** net satırlar K06 faturalarına bir kez yazılır; hata satırları ayrı kalır. Aynı veya adı değiştirilmiş dosyada mükerrer fatura yok; mevcut faturalara çakışma uyarısı. Ödeme yaratılmaz. Test DB prova ve kullanıcı ekran kabulü; canlı migration/eşleme paketi ayrıca hazırlanır.

K07 bitmedi: bu parçada dosya yükleme endpoint'i, CDN kaydı, DB migrationı veya frontend ekranı henüz eklenmedi. Geliştirme sıra kaydı, tamamlanmış özellik iddiası değildir.
