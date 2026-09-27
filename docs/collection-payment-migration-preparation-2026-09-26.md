# Ödeme aktarımı — kapsam ve sabit kesit hazırlığı

## Tamamlanan dilim

**27 Eylül uygulama sonucu:** Netleşen **139.678 ödeme AssistFlowTest'e aktarıldı**. Kalıcı makbuz/kaynak audit'i, para birimi/alan/adet mutabakatı ve tekrar önizlemede 0 yeni kayıt doğrulandı. Kalan 1.800 istisna ayrı tutulur. Aşağıdaki yazmasız inceleme/ön aday kayıtları önceki aşamadır. [Güncel aktarım sonucu](collection-payment-transfer-2026-09-26.md).

**Güncel devam adımı:** Legacy para birimi/ücretsiz/eksi/sıfır karşılaştırması tamamlandı. 141.478 kaydın 140.911'inde tek para birimi eşleşti; bütün kontrollerle güncel ön aday **139.678**, incelemede **1.800**. Aşağıdaki 136.445/5.033 sayıları ilk doğrulama aşamasının tarihsel sonucudur. Yazma yapılmadı. [Karşılaştırma, kararlar ve kanıt](collection-payment-currency-comparison-2026-09-26.md).

26 Eylül: salt-okunur profil, yerel kesit ve satır bazlı ödeme doğrulama raporu tamamlandı. MGS, AssistFlowTest ve CDN üzerinde yazma yapılmadı. **141.478 hedef sözleşmesi bulunan kapsam; 136.445 ön kontrol adayıdır. Hiçbiri henüz uygulama/aktarım onayı değildir.**

## Canlı kapsam profili

Sorgu: `docs/sql/20260926_CollectionPaymentScopeProfile.sql`. Sonuç: Git dışındaki `.tools/payment-scope-profile-20260926.json`.

| Sınıf | Ödeme | Kaynak sözleşme kimliği |
| --- | ---: | ---: |
| Aktarılmış, hedefte mevcut sözleşme | 141.478 | 3.986 |
| Daha önce dışlanmış sözleşme | 71.234 | 2.366 |
| Sözleşme kararı/aktarımı bekleyen | 27.607 | 972 |
| Kaynak sözleşmesi bulunamayan | 2.127 | 162 |
| Mevcut sözleşme kesitinin dışında | 13 | 7 |
| **Toplam** | **242.459** | |

- Sınıfların toplamı canlı ödeme sayımıyla eşleşti; fark 0. Batch 1'de 9.498 sözleşme kimliği tekildir.
- 71.234 yalnız madde 4 değildir; YOK/kimliksiz/diğer dışlamalar da dahildir. Önceki 19.963 ödeme envanteriyle aynı kapsam sayılmaz.
- Korunan adaylarda 9 müşteri sahipliği sorunu, 26 negatif, 18 sıfır tutar vardır. Kontroller kesişebilir; toplanarak sorunlu ödeme sayısı hesaplanmaz.
- Korunan adaylarda 3.279 Free=Evet, 138.199 Free=Hayır; null tutar, boş ödeme tarihi ve geçersiz ay/yıl 0. Tüm finansal kontrollerin geçtiği anlamına gelmez.
- Para birimleri birlikte toplanmadı. Eksi/sıfır/ücretsiz kayıtlar dönüştürülmedi veya dışlanmadı. Aynı gün/tutardaki satırlar duplicate sayılmadı.
- Hedef: 4.479 sözleşme, 17.403 tarife, 1.830 aktif dosya ilişkisi, **0 ödeme**.

Profil ayrı SELECT sonuçlarıdır; sabit finansal aktarım kesiti değildir.

## Sabit ödeme inceleme kesiti

Mevcut Snapshot aracına `export-payments` eklendi. Customer, Contract, ContractHistory ve Payment aynı salt-okunur transaction'da NDJSON'a akıtılır. Mevcut `export` komutu korunur. Dosya başına satır sayısı ve SHA-256 manifestte tutulur; hash hesaplanırken SQL transaction kapalıdır.

- Kesit: `mgs-20260926-195543Z`.
- Git dışı klasör: `tools/Collections.Snapshot/snapshots/payment-review-20260926-225543248`.
- Customer **19.917**, Contract **9.510**, ContractHistory **34.487**, Payment **242.459**.
- Manifest SHA-256: `950FD53132F1E8467B42BB02640563EF92EF8C3A46B1A69075450642FE49FDC7`.
- Dört dosyanın diskten SHA-256 ve satır sayımı yeniden doğrulandı; fark 0.
- Kaynağın aktif kullanılmadığına ilişkin kullanıcı kararıyla mevcut `--inactive-source` seçeneği/Serializable okuma kullanıldı; SQL ayarı değiştirilmedi.
- Önceki kesite göre yeni/değişmiş kaynak kayıtları var. Eski batch/eşleme/hedef güncellenmedi. Kesitte yer almak hedefe aktarım onayı değildir; dışlanmış abonelikler MGS'de kalır.
- `exportKind=payment-review`, `ruleVersion=payment-review-v1`. Sözleşme importer'ı bu kesiti bağlantı/staging öncesinde reddeder. Ayrı `review-payments` komutu doğrulamayı yapar; ödeme aktarımı henüz uygulanmadı.

## Satır bazlı doğrulama — tamamlandı

Mevcut Collections.Import aracına `CollectionPaymentReviewer` ve salt-okunur `review-payments` komutu eklendi. API/FE/şema/işleyiş değişmedi. Hash ve sayım doğrulamasından son okumaya kadar dört dosya yazma paylaşımı olmadan açık tutulur. Payment satırları akışla okunur; bütün kaynak kimlikleri tekillik/pozitiflik kontrolünden geçer. Hedef yalnız AssistFlowTest, tutarlı ve kısa Serializable okuma ile alınır; dosya analizi SQL transaction dışında yapılır.

Kontroller: müşteri–sözleşme sahipliği, güncel müşteri sınıflandırması/abone numarası, kaynak kesitleri arasındaki sözleşme/tarihçe hash ve küme farkları, hedef sözleşme/tarife alanları, onaylı para birimi eşlemeleri, dönem ve ödeme tarihi, decimal(18,2) hassasiyeti, açıklama sınırı ve mevcut ödeme eşlemesi. Tarife adayları mevcut `CollectionPeriodRules` ve `PaymentCurrencyCandidateRules` ile çözülür. Aynı gün/tutardaki ayrı PaymentID'ler birleştirilmez; ücretsiz/eksi/sıfır tutar otomatik düzeltilmez.

- Rapor klasörü (Git dışı): `tools/Collections.Snapshot/snapshots/payment-review-20260926-225543248/review-20260926-202710-4b94ca2e210845b089914c73fa1a3a23`.
- `payments.ndjson`: **242.459** satır; kaynak kimlik/hash, hedef sözleşme, kapsam, para birimi adayı ve hata kodları. İsim/iletişim/credential içermez; finansal kayıt olduğundan yine erişimi sınırlı tutulur.
- `summary.json`: kapsam sayıları, kesişebilen neden sayıları, para birimi bazlı ayrı adet/tutarlar ve rapor hash'i.
- Rapor SHA-256: `DCDB3301BF3C2C4AAE17344D8698C99463AB043345656D1A80FE1B1AC6E4A111`.
- **141.478** hedef sözleşme kapsamı = **136.445** ön aday + **5.033** inceleme gerektiren tekil ödeme. Diğer kapsamların sayıları yukarıdaki profille aynı.
- Ön adaylar: hedef CurrencyTypeId **1** için **134.728 / 25.242.378,12**; Id **2** için **1.717 / 116.601,76**. Para birimleri toplanmaz; bunlar aktarılmış veya mutabakatı tamamlanmış tutarlar değildir.
- Hedef ödeme sayısı **0**. Bütün satırlar ve özet `ReadyToApply=false`; uygulama komutu yoktur.

Hedef sözleşmeye bağlı inceleme gerekçeleri (aynı ödeme birden fazla satırda sayılabilir; toplamları 5.033 ile karşılaştırılmaz):

| Gerekçe | Ödeme adedi |
| --- | ---: |
| Ücretsiz işaretinin finansal anlamı incelenecek | 3.279 |
| Kaynak sözleşme önceki kesitten farklı | 1.226 |
| Muhasebe dönemi için tarife/para birimi adayı yok | 560 |
| Kaynak tarihçe içeriği farklı | 145 |
| Kaynak tarihçe kümesi farklı | 134 |
| Negatif tutar | 26 |
| Sıfır tutar | 18 |
| Ödeme müşterisi sözleşme müşterisiyle uyuşmuyor | 9 |
| Güncel hedef müşteri tahsilat sınıfı dışında | 1 |

Kaynak hash farkı yalnız finansal değişiklik demek değildir; audit alanı da değişmiş olabilir. Değişiklikler otomatik olarak hedefe uygulanmadı. Sonuçlar çalıştırma anındaki hedef okumasına aittir; uygulama öncesi yeniden doğrulanmalıdır. Eksik/bozuk dosya veya kimlikte işlem durur; `summary.json` oluşmayan yarım rapor başarılı inceleme sayılmaz.

### Tekrar çalıştırma

AssistFlow-BE dizininden, mevcut build sonrasında:

```powershell
dotnet tools/Collections.Import/bin/Debug/net9.0/Collections.Import.dll review-payments tools/Collections.Snapshot/snapshots/payment-review-20260926-225543248 WebAPI/appsettings.Development.json 1 950FD53132F1E8467B42BB02640563EF92EF8C3A46B1A69075450642FE49FDC7
```

Her çalışma ayrı rapor klasörü üretir; önceki raporun üzerine yazmaz. Bağlantı bilgileri çıktı olarak yazılmaz.

### İlk doğrulamada belirlenen legacy karşılaştırma sınırı

Bu alt bölüm ilk doğrulama aşamasının bulgusudur. Takip eden karşılaştırma ve ücretsiz/eksi/sıfır incelemesi [ayrı raporda tamamlandı](collection-payment-currency-comparison-2026-09-26.md); aşağıdaki “sıradaki” ifadesi o aşamanın tarihsel kaydıdır.

MGS nesne tanımları yalnız SELECT ile okundu (`.tools/payment-legacy-view-definitions.json`, Git dışı). `Core.vPayment`, `vPaymentCurrency` içinden sıralamasız `TOP 1` seçiyor. Bu seçim belirsizliği yeni sisteme taşınmaz. `vPaymentCurrency` tarihleri 2016 alt sınırına göre kaydırıyor; bitişi kapsayan takvim kullanıyor; Ücretsiz/Hizmet Dondurma ve SQL NULL işlem türlerini eliyor. 2 yıllık dönemlerin 2016 öncesi ankraj dalı diğer çok aylık dönemlerle aynı değil. Yeni ortak takvimin tek aday vermesi, bu legacy sonuçla eşitlik kanıtı değildir. Bağlı view tanımları ve kaynak dönem/para birimi isimleri incelenerek satır bazlı fark raporu sıradaki iştir; tüm canlı view üzerinde pahalı toplu sorgu çalıştırılmadı. Ücretsiz ödeme işaretinin gerçek finansal etkisi de kaynak koddan doğrulanacak.

## Sıradaki bağımlılıklar ve kabul

1. **Tamamlandı:** Manifest/hash/sayım ve PaymentID tekilliği; müşteri/sözleşme sahipliği, dönem/veri tipi doğrulaması. Ham kayıt değişmez; hatalar satır bazlı raporlanır.
2. **Tamamlandı:** Mevcut sözleşme map'i, güncel dışlamalar ve iki kaynak kesiti farklarıyla hedef kapsam daraltıldı. Eski ödemeler yeni aboneliğe bağlanmadı.
3. **Tamamlandı (ön karşılaştırma):** Muhasebe dönemindeki tarife/para birimi adayları ve incelenmiş legacy view semantiği sabit kesitte karşılaştırıldı; üç sözleşme/dört dönem gerçek view örneğiyle doğrulandı. Ücretsiz/eksi/sıfır tutarlar işaretiyle korunur, tek başına engel değildir. Aynı kurda çoklu tarife otomatik tek aday değildir. 1.800 inceleme kaydı güvenli alt kümeden ayrı kalır. [Sonuç](collection-payment-currency-comparison-2026-09-26.md).
4. **Tamamlandı — fiziksel silme uyumu:** PaymentOperation kalıcı makbuzu + MigrationSourceRow audit'i kullanıldı; ödeme MigrationMap FK'sı oluşturulmadı. Silme sonrası kaynak kimliği korunur, tekrar ödeme yaratılmaz; mevcut fiziksel silme işleyişi değiştirilmedi.
5. **Tamamlandı — kabul edilen alt küme:** 139.678 ödeme testte aktarıldı. PaymentID/hash tekrar güvenliği, para birimi bazlı adet/tutar ve tüm alan mutabakatı başarılı; tekrar önizleme 0 yeni kayıt.
6. FollowGroupStatus ödeme değildir; ayrı kesit/doğrulama/aktarım adımıdır. Bu kesitte alınmadı.

Ödeme dalgası tamamlanmadan testteki kalan tutarlar gerçek müşteri borcu olarak sunulmaz.

## Doğrulama

- Snapshot build 0 hata/0 uyarı; gerçek kesit ve bağımsız dosya kontrolleri başarılı.
- Importer build 0 hata/3 mevcut bağımlılık uyarısı.
- Gerçek salt-okunur inceleme iki kez tamamlandı; son raporun diskten satır/scope/aday adetleri ve SHA-256 mutabakatı başarılı. Aktarıma açık satır 0.
- Yanlış manifest SHA-256, ayar dosyası/SQL bağlantısı okunmadan Türkçe hata ve başarısız çıkışla reddedildi.
- Yanlış sözleşme `inspect` komutu ödeme kesitini beklenen Türkçe hatayla reddetti (başarısız çıkış kodu doğrulandı).
- Yeni API, şema veya test altyapısı eklenmedi.
