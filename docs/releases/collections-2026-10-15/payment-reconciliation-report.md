# Ödeme ve banka kimliği mutabakatı

Devam güncellemesi: [ilişki/dosya mutabakatı](relation-reconciliation-report.md) sırasında canlı müşteri referansları yenilendi; ödeme kontrolleri aynı sonuçla tekrarlandı. Aktif birleşik paket `-03` oldu. Aşağıdaki `-01/-02` hash ve yolları önceki ödeme incelemesinin tarihsel kanıtıdır.

15 Ekim 2026. **Canlıya yazılmadı.** MGS'den yalnız para birimi/dönem tanımları, collation/kaynak yılı ve dokuz SQL nesnesinin kaynak metni SELECT ile iki kez okundu. Sonuçlar aynı; dokuz tanım daha önce incelenen sabit hash'lerle eşleşti. Legacy view/fonksiyonları çalıştırılmadı; bütün ödeme karşılaştırması mevcut hash'li 15 Ekim kesiti üzerinde çevrimdışı yapıldı. Bu çalışma final bakım kesiti veya tüm canlı view sonuçlarının SQL ile yeniden taranması değildir.

## Ödeme sonucu

| Sonuç | Kayıt |
| --- | ---: |
| Kaynak ödeme | 243.480 |
| Teknik aday | **139.717** |
| İnceleme/dışlama | 103.763 |
| Önceki önizlemeden eklenen tarihsel ödeme | 3.219 |
| Önceki adaylardan legacy takvim nedeniyle ayrılan | 6 |

Önceki 136.504 − 6 + 3.219 = **139.717**. Sözleşme aday kümesi aynı: **4.438**. Bu sayılar kesin aktarım onayı veya bütün sözleşmelerin tamamlandığı anlamına gelmez.

| Para birimi | Aday | Ham işaretli tutar |
| --- | ---: | ---: |
| TRY | 138.001 | 26.665.704,43 |
| USD | 1.716 | 115.320,76 |

Tutarlar para birimi bazında ayrı; kur çevrimi/yuvarlama yapılmadı. **Nakit tahsilat toplamı değildir:** adaylarda 3.186 ücretsiz işaretli, 18 sıfır ve 24 negatif ödeme var; kümeler kesişebilir. Önceki [legacy inceleme kararı](../../collection-payment-currency-comparison-2026-09-26.md) gereği Amount işareti ve Free alanı korunur. Ücretsiz ödeme ücretsiz sözleşme anlamına gelmez; geçmiş borcu sıfırlamaz. Son hazırlık önizlemesindeki ihtiyatlı Free/pozitif tutar bekletmesi yalnız geçerli kaynak değerleri için kaldırıldı; diğer sözleşme/kimlik/karar/takvim kontrolleri korundu.

Önceki adaylardan ayrılan altı PaymentID: `138906`, `138944`, `139083`, `139129`, `139519`, `441482`. İlk dördü legacy 2016 alt sınırı; `139519` eski üç yıllık ankraj farkı; `441482` açık bitiş takviminin 2027 üst sınırı dışında. Bunlar önceki incelemede de tanımlanmış teknik farklardır; dönem/para birimi tahminle değiştirilmedi.

## Banka tekrar koruması

- **83.543** kimlik: GTS **65.970**, IVR **17.573**.
- Tip, boşluk, kaynak kimliği ve `Trim().ToUpperInvariant()` sonrası tip+numara tekilliği kontrollerinde sorun **0**.
- Plan, elenen eski abonelikler dahil **tüm** kaynak kimliklerini kapsar. Bu, eski abonelik ödemelerini aktarmak değildir; aynı banka dosyasının yeniden ödeme üretmesini engellemek içindir.
- Kaynak BulkPayment kimliğinden ödeme satırına ilişki uydurulmadı. Uygulama aşamasında anahtarlar mevcut `CollectionBankLoadService.Key` ile üretilecek ve BankBaseline işlem makbuzu saklanacak. Henüz hedefe hiçbir banka kimliği yazılmadı.

## Güncel yerel paketler

BE `.tools` içinde; kişisel/finansal içerik Git dışında tutulur:

- `collection-payment-reconciliation-2026-10-15-01`: SQL tanım kanıtı, tüm ödeme sonuçları, aday ödeme ve banka kimliği planı.
- **Aktif veri inceleme paketi:** `collection-production-data-package-2026-10-15-02`. Önceki `-01` değiştirilmedi; tarihsel ilk önizleme olarak kaldı. Yeni paket 4.438 sözleşme / 139.717 ödeme, aynı 18 servis ve 2.601 müşteri tipi önerisini içerir. Ödeme planı biçimi manifestte `legacy-reconciled-source-plan-v1` olarak belirtilir.

| Paket hash listesi | SHA-256 |
| --- | --- |
| Ödeme mutabakatı | `69452EE23BDC60FDD97AEF2893DB2C301A9D96DBB8004EC6202E435FB2AC62AC` |
| Aktif veri paketi | `275B85E43720C7B06F6025326DAF64A8636E42DB85544A6B512DD83BBA6A077C` |

İki pakette 15 dosya hash'i doğrulandı. Paketleme tüm aday ödemelerde kaynak/RequestId tekilliğini, mevcut RequestId algoritmasını, aday sözleşme bağını ve kapalı uygulama durumunu tekrar kontrol etti. Önceden incelenmiş legacy resolver gövdesi değiştirilmeden ortak çevrimdışı metoda ayrıldı; gövde önceki sürümle karşılaştırıldı. Release aracı build'i 0 hata/0 uyarı; mevcut Import aracı 0 hata/691 mevcut bağımlılık ve nullable uyarısıyla derlendi. Çalışan backend/frontend veya finansal servisler değiştirilmedi. SQL restore/idempotency/rollback provası yapılmadı.

## Sonraki bağımlılık

Üst kart/cari, not, grup geçmişi, fatura ve fiziksel dosya/CDN ilişkilerini güncel kaynakla mutabık hale getir. Ardından aktör/audit eşlemesi, izole prova ve bakım kesitinde kaynak/hedef yeniden doğrulaması. Ortak dbo/prosedür taslakları kapalı; paket **BLOCKED_REVIEW_PACKAGE_NOT_APPROVED_FOR_EXECUTION**, tüm ödeme satırları **ReadyToApply=false**. [Yayın planı](README.md).
