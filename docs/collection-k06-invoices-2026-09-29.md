# K06 — Kesilen fatura ve bağlı ödemeler

## Sonuç — 29 Eylül, K06 tamamlama parçası

Önceki salt-okunur önizleme aşamasından sonra net kayıtlar **AssistFlowTest**'e aktarıldı: 1.847 fatura / 1.939 ödeme. `invoice-transfer <settings> <rapor> <önizleme SHA-256>` aynı planı transaction içinde yeniden hesaplar; hash değişmişse yazmaz. Kaynak/hedef kontrolleri ve satır bazlı tutar/sayı mutabakatı başarısızsa tüm aktarım geri alınır. Yeni/eski hedefe eklenen ödemeler aynı işlemde ele alınır; mevcut faturaya ödeme eklenmesi invoice rowversion'ı da değiştirir. Kaynak audit alanları ve hash korunur; CreatedUser=0 aktarım kaydını, CreatedDate aktarım zamanını gösterir; özgün tarihler LegacyCreatedOn/ModifiedOn alanlarındadır.

| Para birimi | Fatura | Fatura toplamı | Ödeme toplamı | Kalan |
| --- | ---: | ---: | ---: | ---: |
| TL | 1.846 | 7.152.481,01 | 7.145.597,04 | 6.883,97 |
| USD | 1 | 0,00 | 0,00 | 0,00 |

1.847 faturanın tamamında tek tek fatura tutarı/bağlı ödeme sayısı/toplamı doğrulandı. Son önizleme hash'i `30A4142DFE7BB345264DCDB7F10B93E5EEB7D1560F696BBB0A6FB66564DB9E8C`: 1.847 fatura ve 1.939 ödeme AlreadyApplied; Ready yok. 3.631 fatura / 3.883 ödeme inceleme listesinde, K12'de çözülmek üzere korunuyor. Bu sayıların çoğu korunan müşteri eşlemesi olmayan kaynak kayıtlardır; yeni müşteriye tahminle bağlanmadı. Yerel kanıtlar `.tools/k06-invoice-transfer-20260929.json`, `.applied.json`, `.tools/k06-invoice-posttransfer-20260929.json`.

### Komutlar ve ekran

- POST `/{id}/payments`, `/{id}/payments/{paymentId}/update`, `/{id}/payments/{paymentId}/delete`, `/{id}/comment`, `/{id}/delete`.
- Mevcut CollectionFollowUp View/Edit, Enabled/ContractCreateEnabled kontrolleri ve geçerli kullanıcı doğrulaması kullanılır; ayrı yeni yetki sistemi yoktur.
- Yeni ödeme pozitif ve iki ondalıklıdır. Tarihsel sıfır/negatif ödeme tutarı değiştirilmeden korunabilir; yeni negatif işlem icat edilmez. Faturanın para birimi/müşterisi değiştirilmez; ödeme başka faturaya taşınmaz.
- Ödemesi olan fatura silinemez; ödemesiz fatura veya ödeme fiziksel silinir. `collection.InvoiceOperation` FK olmadan öncesi/sonrası, kullanıcı, tarih, legacy kimlik ve istek hash'ini korur. Silinmiş legacy kimlik önizlemede işaretlenir, yeniden canlandırılmaz.
- Serializable transaction, fatura ve ödeme rowversion, unique RequestId ve actor/payload karşılaştırması kullanılır. Ödeme-only değişiklikte bile fatura sürümü ilerler. Aynı isteğin tekrarı yeni ödeme üretmez.
- Detaydaki ödeme listesinden ekle/düzenle/sil ve açıklama sekmesinden açıklama düzenle/fatura sil İşlem sekmesini açar. Form düzenlenmeye başlandığı andaki fatura sürümünü sabit tutar. Belirsiz ağ yanıtında form/sekme kilitlenir, aynı payload ve işlem anahtarıyla yeniden denenir. Silmede açık onay metni vardır. UI mesajları ortak Türkçe toast yapısındadır.

### Doğrulama ve kabul sınırı

`invoice-setup <Development JSON> --verify-commands` yalnız AssistFlowTest üzerinde geçici bir faturada finansal komut zincirini sınar. 29 Eylül koşusu başarılı; create/replay, farklı payload aynı key reddi, kısmi bakiye, stale rowversion, ödemeli fatura silme engeli, update/delete/comment, silinen kaydın audit'i doğrulandı. Yalnız geçici fatura ve ona ait test audit'i temizlendi. Gerçek müşteri/sözleşme veya aktarılan faturalara yazılmadı.

Migration `20260929084242_AddCollectionInvoiceOperations` testte uygulanmıştır. Model-snapshot kontrolü başarılı; BE solution/import ve FE build, değişen FE lint geçti. Global TypeScript denetiminde proje geneli mevcut hatalar var, K06 dosyalarında hata görülmedi. Browser `http://localhost:5173/crm/collections/invoices` denemesi bağlantı reddiyle sonuçlandı; **tarayıcı ve oturumlu HTTP yetki kabulü yapılmış sayılmaz**. Yerel uygulama yeni sürümle başlatılınca kullanıcı kabulü yapılacaktır. K06 işlev geliştirmesi/net aktarımı bitti, eksik veri istisnaları K12 ve ekran kabulü açık; production hazır değildir.

---

Aşağıdaki önizleme/kalan iş bölümleri önceki aşamanın tarihsel kaydıdır; güncel durum yukarıdadır.

## Legacy kanıtı ve karar

`Pages/Core/InvoiceFollow.aspx.cs` listesi `Core.vInvoiceFollow` kullanır. B/K fatura türüdür; CustomerType ile otomatik eşitlenmez. View ödeme toplamını yalnız `Core.InvoiceFollowPayment.InvoiceFollowID` üzerinden alır; kalan = fatura tutarı − bağlı ödeme toplamı. Ödenen filtresi kalan <= 0; açık filtresi kalan > 0'dır. Kısmi ve fazla ödeme gösterimi korunur.

Kaynakta no/tarih/tutar/CurrencyID/proje/açıklama vardır; kur alanı yoktur. K06 eski kabul metnindeki “kur” yeni döviz dönüşümü gerektirmez. Ödeme para birimi faturadan gelir. Fatura ekranı elle yeni fatura üretmez; legacy dosya yükleme K07 işidir. `Core.Invoice` paneliyle veya sözleşme `collection.Payment` ile birleştirme yapılmaz.

## Bu parçada uygulanan

- collection.Invoice: müşteri ve mevcut CurrencyType FK, B/K kontrolü, kaynak kimliği/hash/audit bilgileri, rowversion; müşteri+tarih ve tür+tarih indeksleri.
- collection.InvoicePayment: fatura FK, tarih/tutar/açıklama, kaynak kimliği/hash/audit bilgileri, rowversion; fatura+tarih indeksi. Otomatik cascade silme ve soft-delete yok.
- Kaynak kimlikleri filtreli unique indeksle tekildir. Eksik tarih/tutar/para birimi tahminle doldurulmayacak; import önizlemesinde ayrılacak.
- GET `/api/collections/invoices`, `/{id}`, `/{id}/payments`; mevcut CollectionFollowUp görüntüleme izni ve Enabled anahtarı. Müşteri erişimi onaylı tahsilat kapsamına bağlıdır.
- Liste server-side filtre/sayfalama (maksimum 100); müşteri kartından filtreli kullanım. Para birimleri kendi koduyla gösterilir, karma para birimi toplamı hesaplanmaz.
- FE ortak DataTable, Select, detail layout/tabs ve toast.push köprüsü kullanır. Liste bağlantısı Tahsilat Takip içindedir. Genel müşteri/diğer modül ekranlarına dokunulmadı.

## Test ve kurulum

Migration: `20260929080736_AddCollectionInvoices`. EF aracıyla oluşturuldu; Up yalnız iki yeni tablo ve indekslerini içerir. Snapshot'taki eski el yazımı bölümlerin biçim/sıra farkları EF üretimidir; mevcut tablolar için migration işlemi yoktur.

Test-only komut:

```text
dotnet tools/Collections.Import/bin/Debug/net9.0/Collections.Import.dll invoice-setup WebAPI/appsettings.Development.json --install
dotnet tools/Collections.Import/bin/Debug/net9.0/Collections.Import.dll invoice-setup WebAPI/appsettings.Development.json --verify
```

Araç yalnız 192.168.1.8 / AssistFlowTest'e izin verir; install yalnız tam bu migration bekliyorsa çalışır. Model-snapshot eşliği kontrol edilir. 29 Eylül kurulum başarılı; ardından B/K/tümü × ödenmiş/açık/tümü dokuz SQL sorgusu ve bulunamayan detay sorgusu geçti. Gerçek finansal kayıt eklenmedi.

BE solution/import build ve FE build başarılı. Değişen FE dosyaları lint kontrolünden geçti. Tüm projenin TS kontrolü mevcut hatalar nedeniyle başarılı sayılmaz; değişen dosyalarda TS hatası saptanmadı. Tarayıcı ve gerçek dolu veri kabulü açık.

## K06 kalan işler / kabul

### İkinci parça — salt-okunur aktarım önizlemesi

`invoice-preview <Development JSON> <yerel rapor yolu>` komutu eklendi. Hiçbir uygulama/migration/yazma modu yoktur. MGS Core.InvoiceFollow/InvoiceFollowPayment okunur; hedefte yalnız korunan sözleşmelerin kaynak müşteri kimliği ve onaylı GroupParent kayıtları eşleme sağlar. Mevcut tahsilat kapsam filtresi korunur. Kaynak müşteri/fatura sahipliği, tarih, decimal(18,2) uyumu, alan uzunlukları, para birimi ve tekrar aktarım hash'i kontrol edilir. Eksik ödeme geçmişiyle yanlış bakiye üretmemek için sorunlu ödemesi olan fatura bütün olarak bekletilir.

29 Eylül sonucu:

| Kayıt | Kaynak | Hazır | İnceleme |
| --- | ---: | ---: | ---: |
| Kesilen fatura | 5.478 | 1.847 | 3.631 |
| Fatura ödemesi | 5.822 | 1.939 | 3.883 |

Fatura nedenleri (bir kayıt birden fazla neden taşıyabilir): 3.620 korunan müşteri eşlemesi yok; 42 para birimi eşleşmiyor/boş; 3 tarih eksik; 1 tutar eksik/geçersiz. İnceleme ödemelerinin tamamı hazır olmayan faturalarına bağlı olduğundan bekler. Eşleşmeyenleri yeni müşteriye veya benzer isme bağlama yapılmadı; bu kategori kalıcı dışlama kararı değildir.

Kaynak Type değerleri **Bireysel (4.674)** ve **Kurumsal (804)**; B/K URL seçeneğinin DB'de de tek harf olduğu varsayımı kullanılmadı. Önizleme özgün alanı tutar, TargetType ayrı olarak B/K üretir. TL/YTL/TRL yalnız TRY para birimi kod eşdeğerliğiyle çözülür; kur dönüşümü yoktur. Eksik para birimi tahmin edilmez.

Rapor `.tools/k06-invoice-preview-20260929.json` (kişisel kaynak verisi nedeniyle sürüm kontrolü dışında). Plan SHA-256: `7CA68A910EA2F095ECE5B5B4B865ED9BA43040BB63CBFAC1C793810A9E5BF818`. Rapor satır kimliklerini, kaynak alanlarını, hedef kimlik adaylarını, nedenleri ve kaynak hash'lerini içerir. Bu hash yalnız bu önizleme kesitini tanımlar; uygulama aracı bu parçada yoktur. Import projesi build başarılı; gerçek SQL Server salt-okunur sorguları başarılı. Veri aktarılmadı, frontend değişmedi.

### Sıradaki işler

1. Kaynak müşteri eşlemesi: korunan abonelik ve G üst kart haritaları; eski/elenen abonelerin ödemeleri MGS'de kalır. İsme veya TOP 1 cari koduna göre otomatik bağlama yok.
2. Kaynak fatura–ödeme müşteri uyumsuzluğu, tarih/tutar/para birimi eksikleri önizleme/istisna raporunda ayrılır. Hash onaylı, tekrar güvenli aktarım; kaynak okunur, değişmez.
3. Ödeme ekleme/düzeltme/fiziksel silme: mevcut ödeme transaction/idempotency/audit standartlarıyla, fatura ledger'ına özel komutlar. Fatura açıklama güncellemesi ve bağımlı ödeme varken fatura silme güvenliği.
4. Para birimi ve fatura bazında kaynak/hedef tutar–ödeme–kalan mutabakatı; gerçek yetki ve tarayıcı kabulü.
5. Bunlar tamamlandıktan sonra K07 dosya yükleme; mevcut CDN kullanımı korunur.
