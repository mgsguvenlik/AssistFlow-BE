# Ödeme para birimi ve finansal davranış karşılaştırması

## Sonuç

**27 Eylül devam sonucu:** Bu rapordaki 139.678 ön aday güncel doğrulama sonrası testte aktarıldı; tekrar önizleme 0 yeni kayıt verdi. Aşağıdaki bulgular karşılaştırma aşamasına aittir. [Aktarım sonucu](collection-payment-transfer-2026-09-26.md).

Sabit ödeme kesiti ve önceki salt-okunur rapor üzerinden **141.478** hedef sözleşmeye bağlı ödeme karşılaştırıldı. Yeni `compare-payment-currency` komutu yalnız yerel dosya okur/yazar; veritabanı bağlantısı, aktarım veya uygulama modu yoktur. API, frontend, şema ve çalışan finansal hesap değiştirilmedi.

| Karşılaştırma | Ödeme |
| --- | ---: |
| Legacy ve mevcut takvimde tek para birimi eşleşti | 140.911 |
| Legacy takvimde aday yok | 566 |
| Legacy aday var, hedef takvimde tek aday yok | 1 |
| Toplam | 141.478 |

Kaynak/hedef değişikliği ve diğer veri kontrolleri de hesaba katıldığında **139.678 ön aday**, **1.800 inceleme kaydı** vardır. Bütün çıktılar `ReadyToApply=false`; bunlar henüz aktarılmış veya kesin aktarılabilir kayıt değildir.

Önceki 136.445 adaydan 136.438'i eşleşti, 7'si takvim farkıyla incelemeye ayrıldı. Önceden yalnız ücretsiz/eksi/sıfır semantiği nedeniyle bekleyen **3.240 ek kayıt** diğer kontrolleri de geçerek ön adaya katıldı. Böylece 136.445 − 7 + 3.240 = 139.678. Bu bir toplu dışlama/silme kararı değildir.

| Onaylı hedef para birimi | Ön aday adedi | İşaretli tutar toplamı |
| --- | ---: | ---: |
| CurrencyTypeId 1 (legacy TL/14) | 137.961 | 26.229.413,21 |
| CurrencyTypeId 2 (legacy USD/13) | 1.717 | 116.045,96 |

Para birimleri birleştirilmedi; kur çevrimi, tutar yuvarlama, sıfırlama veya yeni ödeme yaratma yapılmadı. `IsFree=true` kayıtları bu legacy toplamda yer aldığı için tutarlar yalnız nakit tahsilat toplamı olarak adlandırılamaz.

## Ücretsiz, negatif ve sıfır ödeme kararı

- ZIP içindeki `wwwroot/Pages/Core/Customer.aspx.cs:612–655,1217–1245` ödeme tutarını ve Free alanını ayrı ayrı yazar. Ücretsiz işareti tutarı otomatik sıfırlamaz. Güncelleme aynı alanları korur, silme fizikseldir. Bireysel/grup müşteri ekranlarında aynı desen bulunur.
- MGS `dbo.fnTahsilatTakibi:87–107` ve `dbo.fnTahsilatTakibiGrup:89–109`, sözleşme/ay/yıl üzerinden `sum(isnull(Amount,0))` hesaplar. Free, pozitif veya sıfır tutar filtresi yoktur. `Core.vPayment` tutarı/Free alanını değiştirmeden taşır.
- Dolayısıyla aktarımda **ham tutar işareti ve Free değeri korunur**. Negatif tutarın her satırda iade olduğuna dair ek anlam atanmaz; sıfır tutar satırı silinmez. Ücretsiz ödeme, ücretsiz sözleşme/tarife demek değildir ve geçmiş borcu silme gerekçesi değildir.
- Mevcut AssistFlow Payment modeli decimal(18,2) işaretli tutarı destekler; komut doğrulayıcısı da negatif/sıfırı engellemez. Tracking servisi Amount toplamını IsFree ile sıfırlamaz. İşleyişi değiştirmeye gerek yoktur.
- Yeni karşılaştırma yalnız `FREE_PAYMENT_REVIEW`, `NEGATIVE_AMOUNT_REVIEW`, `ZERO_AMOUNT_REVIEW` gerekçelerini bilgi notuna dönüştürür; diğer kimlik, tarih, tutar, kapsam, para birimi ve değişiklik sorunları korunur. Ön adaylarda 3.208 ücretsiz, 25 negatif, 13 sıfır işareti vardır; kümeler kesişebilir.

## Legacy farkları ve korunan sınırlar

İncelenen dokuz SQL nesnesinin tanım hash'leri kodda sabittir. Tanım değişirse karşılaştırma durur; SQL metni hiçbir zaman çalıştırılmaz. Kaynak collation `Turkish_CI_AS`, sekiz bilinen dönem adı, 2016 alt sınırı, bitişin dahil olması ve açık bitişte kaynak yılın sonraki yıl sonu korunarak **yalnız karşılaştırma amaçlı** legacy takvim hesaplanır. Günlük/saatlik/bilinmeyen dönem fallback'i otomatik kabul edilmez; böyle bir tarihçe varsa kayıt incelemede kalır. Canlı `TOP 1` seçimi taklit edilmez; birden fazla tarife aynı para biriminde olsa da otomatik kabul edilmez.

- 2016 öncesi aylık başlangıç Ocak 2016'ya kayar; bazı çok aylık/yıllık başlangıçlar aynı ay 2016'ya kayar. 2 yıllık dönem bu özel dalda yer almaz.
- SQL NULL işlem türü elenir; literal `null` metni SQL NULL değildir. Ücretsiz/Hizmet Dondurma tarifeleri elenir.
- Tarihçenin müşterisi bulunmuyorsa legacy iç birleştirme nedeniyle aday oluşmaz.
- Kaynak yıl 2026 olduğundan açık bitiş takviminde 2027 sonrasına aday yoktur. Gelecek yıllardaki sonuç değişebilir; bu eski davranış yeni uygulamanın hesap kuralı haline getirilmedi.
- Eski tahsilat toplam fonksiyonları farklı para birimlerini sözleşme/ay/yıl bazında birleştirebiliyor; bu hatalı davranış taşınmaz. Yeni toplamlar para birimi bazında ayrıdır.

Önceki adaylardan ayrılan yedi PaymentID:

| PaymentID | Kaynak sözleşme | Dönem | Neden |
| --- | ---: | --- | --- |
| 138906 | 1270 | 2015-12 | Legacy 2016 alt sınırı |
| 138944 | 1477 | 2015-12 | Legacy 2016 alt sınırı |
| 139083 | 2131 | 2015-12 | Legacy 2016 alt sınırı |
| 139129 | 2429 | 2015-12 | Legacy 2016 alt sınırı |
| 145255 | 1898 | 2015-11 | Legacy 2016 alt sınırı |
| 139519 | 2450 | 2017-09 | Üç yıllık ankraj legacy'de 2011→2016 kaymış |
| 441482 | 48883 | 2029-02 | Legacy açık bitiş üst sınırının dışında |

Ters yöndeki fark PaymentID **139518**, sözleşme **2450**, **2016-09**: legacy aday veriyor, hedefte yenileme yok. Hiçbirinin dönemi/para birimi tahminle değiştirilmedi; güvenli alt kümenin aktarımı bu istisnaları çözülmüş saymayacak.

## Kanıt, tekrar çalıştırma ve doğrulama

- Kanıt sorgusu: `docs/sql/20260926_CollectionPaymentLegacyEvidence.sql`; sonucu Git dışı `.tools/payment-legacy-reference.json`. Beş sonuç kümesi sırasıyla para birimleri, ödeme dönemleri, collation/kaynak yılı, SQL nesne tanımları ve batch 1 onaylı para birimi eşlemeleridir.
- Kanıt SHA-256: `BC99DEDB20513700B3929CC0D8750DDBE4A4D06E8C92650071E18E38F238EACD`.
- Girdi rapor SHA-256: `DCDB3301BF3C2C4AAE17344D8698C99463AB043345656D1A80FE1B1AC6E4A111`.
- Çıktı, önceki inceleme klasörünün altındaki `currency-20260926-203826-4df82dd48e0b45f5a9108942544ef5e6` klasöründedir. `comparison.ndjson` SHA-256: `7A27C2A14F0BB15ECE170EE5C0984A5BE5929695620DEB364E11D23CB179DD32`.
- Çıktı satır/aday sayımı, disk hash'i ve tüm satırların kapalı uygulama durumu doğrulandı. Veriler kişisel/finansal veri kapsamındadır, Git dışında tutulur. Tamamlanmamış klasör/özetsiz rapor başarı sayılmaz.
- Canlı MGS `vPaymentCurrency` yalnız üç sözleşme/dört dönem için okundu: 15/2016-01 ve 2450/2016-09 TL verdi; 2450/2017-09 ve 48883/2029-02 sonuç vermedi. Yerel karşılaştırmayla eşleşti. Tüm canlı ödeme view'u taranmadı; tam canlı finansal mutabakat iddiası değildir. Sonuç `.tools/payment-legacy-currency-sample.json`.
- Importer build: 0 hata, mevcut 3 bağımlılık uyarısı. Yeni test altyapısı eklenmedi.
- Yanlış kanıt hash'i, ön inceleme dosyaları açılmadan Türkçe hata ve başarısız çıkışla reddedildi. Git diff boşluk kontrolü başarılı.

```powershell
dotnet tools/Collections.Import/bin/Debug/net9.0/Collections.Import.dll compare-payment-currency tools/Collections.Snapshot/snapshots/payment-review-20260926-225543248/review-20260926-202710-4b94ca2e210845b089914c73fa1a3a23 .tools/payment-legacy-reference.json BC99DEDB20513700B3929CC0D8750DDBE4A4D06E8C92650071E18E38F238EACD
```

## Sıradaki bağımlılık

Ödeme fiziksel silinirken MigrationMap FK'sını güvenle kaldırıp kaynak kimliğini kalıcı olarak korumak; retry ile silinen ödemenin geri gelmesini engellemek. Ardından güncel kaynak/tanım/eşleme/hedef doğrulamasıyla kabul edilen alt kümenin önizleme ve para birimi bazlı mutabakatı, kontrollü test aktarımı. Bu rapor hedefi dondurmaz; uygulama öncesi yeniden doğrulama şarttır. Diğer 1.800 satır incelemede, dışlanmış eski aboneliklerin ödemeleri MGS'de kalır.
