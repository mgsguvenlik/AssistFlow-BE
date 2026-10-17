# Maddeler 5, 6 ve 7 — Toplu karar planı

## Kapsam ve güncel öncelik

Kullanıcı son talebinde tek dosyanın **yalnız 5, 6 ve 7. sayfalarının** birlikte değerlendirilmesini, uygulanmış 4. maddenin korunmasını istedi. Dosyanın 1 ve 4. sayfa verileri karara alınmadı. Daha önce ayrı madde 5 incelemesinde sorulan “madde 4 ile dışlanmış üç eski abonelik yeniden açılsın mı?” konusu bu kapsamla kapandı: **yeniden açılmayacak**. Diğer önceki kapsam/kimlik dışlamaları da genel “kalsın” notuyla kaldırılmaz.

Dosya: `24.09.2026 Kopya Tahsilat_Guncel_Karar_Listeleri_1_4_5_6_7.xlsx`.
SHA-256: `2CA674418FE29BEDCC4895A1172883D9D733646FC4709E8BA8142B9A13CC5315`.

İlk inceleme salt-okunur karar planıydı. Ardından kullanıcının **“netleşen kayıtları aktaralım”** onayıyla aşağıdaki üç sözleşme AssistFlowTest'e aktarıldı. Diğer satırlar otomatik uygulanabilir onay listesi değildir. Excel, MGS, production, ortak dbo müşteri tabloları, ödeme ve dosyalar değiştirilmedi.

## Netleşen alt kümenin test aktarımı — 26 Eylül 2026

| Legacy sözleşme | Hedef sözleşme | Karar verilen tarihçe / Excel hücresi | Aktarılan tarife |
| --- | --- | --- | ---: |
| 27590 | 4756 | 34133 / Madde 5 H7 | 10 |
| 48715 | 4757 | 82291 / Madde 5 H22 | 2 |
| 49221 | 4758 | 84457 / Madde 5 H23 | 1 |

- Üç sözleşme **ACTIVE** olarak toplam **13 tarife dönemiyle** aktarıldı. Yalnız belirtilen üç literal `null` işlem türü, legacy ücretli dönem davranışına uygun `Billable` olarak karara bağlandı. Ham kaynak payload'ı değiştirilmedi; dosya hash'i, kaynak hash'i ve karar hücresi çözümlenmiş audit kaydına yazıldı.
- Tutar, dönem, para birimi ve tarihler korundu. 84457 tarihçesindeki **0 tutar aynen 0** kaldı; Ücretsiz yapılmadı. Canlı MGS ile ön kontrol edilen 13 tarihçenin finansal/tarih/işlem alanlarında fark yoktu.
- Karar uygulama SHA-256: `1A00814E113C39E716AD2A55AB3288A86414DC4A285380BD659FF00820900779`. Hedef aktarım SHA-256: `BE3417A3EB7C1CD6B1FA3A8002A3EDEE37361093E7E2E63A4EBB32590C738173`. Her iki işlem test ortamı kontrolü, transaction, kilit ve yeniden hash kontrolüyle uygulandı.
- Son mutabakat: aktarılmış map/stage/hedef **4.477 sözleşme / 17.401 tarife**; iki eski manuel kayıt dahil fiziksel toplam **4.479 / 17.403**. Yeni üç sözleşmenin ve 13 tarifenin karşılaştırılan alanlarında fark **0**. Ödeme **0**, dosya **0**, ham kaynak **63.854**. Dışlanmış kayıt üzerinde kalan aktarım eşlemesi **0**.
- Tekrar karar önizlemesi **0 değişiklik**, tekrar aktarım önizlemesi **0 aday**. Importer build **0 hata / 0 uyarı**. Staging sözleşmeler: Pending **3**, Blocked **1.720**, Applied **4.477**, Excluded **3.298**.
- Bu dilimde yeni dışlama veya silme yapılmadı. Madde 4 dışlamaları korundu. Madde 6/7'deki karar çelişkileri, donuk/aktif uyuşmazlıkları, eksik para birimi/tarih bilgileri ve kaynak değişiklikleri bekliyor; tüm 5/6/7 kapsamı tamamlanmış sayılmaz.

Kanıtlar (Git dışı): `.tools/item-five-final.json`, `.tools/items567-legacy-preapply.json`; aktarım planı `tools/Collections.Import/snapshots/item-five-transfer-20260926-171434442/plan.json`, tekrar önizleme `tools/Collections.Import/snapshots/item-five-transfer-20260926-171623237/plan.json`.

Aşağıdaki sayımlar ilk incelemenin tarihsel karar envanteridir; uygulama sonrası güncel durum yukarıdadır.

## Excel yanıtlarının özeti

| Madde / sayfa | Veri aralığı | Kalsın | Silinsin / silinebilir | Yanıtsız | Satır |
| --- | --- | ---: | ---: | ---: | ---: |
| 5 — Null İşlem Türü | A6:R24, yanıt H6:H24 | 14 | 1 | 4 | 19 |
| 6 — Tarife Çakışmaları | A6:V662, yanıt H6:H662 | 208 | 449 | 0 | 657 |
| 7 — Para Birimi Tutar | A6:V136, yanıt H6:H136 | 33 | 98 | 0 | 131 |
| **Toplam** | | **255** | **548** | **4** | **807** |

“Grup firması olduğu gibi kalsın” da koruma isteğidir; dbo.CustomerType değiştirme talebi sayılmaz. Büyük/küçük harf ve sondaki nokta farkları aynı anlamda birleştirildi. Tarih/tutar/para birimi için ayrılmış düzeltme alanları üç sayfada da boş; kararlar yalnız Tahsilat Açıklama sütunundadır.

807 satır **794 tekil karar nesnesine** karşılık gelir: 625 ContractHistory + 169 Contract. Referans verilen farklı sözleşme sayısı 450'dir. Aynı tarihçenin birden fazla sayfada görünmesi mükerrer işlem üretmez. Bu adetler farklı müşterilerin sayısı değildir.

## Ortak karar kuralları

1. **Önce uygulanmış madde 4:** Eski abonelikler yeniden açılmaz. Onlara ait ödemeler yalnız MGS'de kalır. Excel'deki “kalsın” notu tekilleştirme kararını iptal etmez.
2. **Kararı kaynak kimliğine bağla:** Tarihçe numarası dolu satır ContractHistory kararıdır. Tarihçe numarası boş, sözleşme numarası dolu satır sözleşme seviyesindedir. İsimle başka sözleşmeye aktarım yapılmaz. Sözleşme kararı altında kalan bütün tarihçeler ayrıca dikkate alınır.
3. **Silinsin / silinebilir:** Yeni sisteme aktarım dışında bırakma adayıdır; MGS'de fiziksel silme değildir. Bir tarihçe notu bütün müşteri veya abonelik için toplu silmeye genişletilmez. Sözleşme seviyesinde dışlama ise bağlı tarihçelerle birlikte planlanır; aynı sözleşmede koruma notu varsa otomatik uygulanmaz.
4. **Olduğu gibi kalsın:** Tutar, tarih, para birimi, dönem ve ham bilgi korunur; mevcut bir tutar yeniden hesaplanmaz. Bu not, eksik para birimini doldurmaz veya iki çakışan tarifeden hangisinin esas alınacağını seçmez. Kaynak hata yeni sisteme aynen taşınarak çift borç üretilemez.
5. **Madde 5 özel kuralı:** Yalnız yanıtlı literal `null` işlem türleri, diğer kontrollerden geçerse legacy normal ücretli dönem davranışı (`Billable`) ile eşlenebilir. Gerçek SQL NULL veya başka bilinmeyen değerler için genel varsayım eklenmez. 0 tutar 0 kalır, kendiliğinden Ücretsiz'e dönüştürülmez. [Dayanak](collection-item-five-review-2026-09-26.md).
6. **Donuk notu:** Excel'de adı geçen kayda özgüdür; önceki “donuk kayıtlar donuk gelsin” kararı topluca iptal edilmez. Özellikle kaynak üyelik koduyla çelişen “donuk silinsin” notları ayrıca teyit edilmelidir.
7. **Boş yanıt:** Aynı kaynak kimliği için diğer iki sayfada açık yanıt varsa birlikte değerlendirilir. Hiç yanıt yoksa yeni karar uydurulmaz; önceki dışlama varsa korunur.
8. **Kaynak değişmişse:** Eski kesit hash'i sessizce güncellenmez. Güncel kaynakla fark, yeni kesit/karar sürümü ve uygulama önizlemesinde ele alınır. Eski plan hash'iyle yazılmaz.
9. **Dosya ve ödeme sınırı:** Dosyalar madde 8; bu çalışma ödeme taşıması veya fiziksel dosya işlemi içermez.

## Tekilleştirilmiş ön sınıflandırma

| Sonuç | Tekil nesne | Açıklama |
| --- | ---: | --- |
| Madde 4 dışlaması korunur | 67 | Sözleşme veya bağlı tarihçe yeniden dahil edilmez. |
| Diğer önceki dışlamalar korunur | 66 | Kimliksiz/kapsam dışı gibi önceki nedenler iptal edilmez. |
| Aktarım dışı bırakma adayı | 435 | 83 sözleşme satırı + 352 tarihçe satırı. Aşağıdaki donuk/kaynak değişikliği denetimleri nedeniyle tamamı uygulamaya hazır değildir. |
| Korunacak, teknik kontrolleri tamamlanacak | 213 | Finansal eksiklik/çakışma kontrolleri halen geçerlidir; bu sayı aktarılabilir kayıt sayısı değildir. |
| Sözleşme ve tarihçe kararları çelişiyor | 11 | 3 sözleşme ve bağlı 8 tarihçe; bekletilir. |
| Yanıt halen yok | 2 | Tarihçe 77221 ve 77224; üst sözleşmeleri de kaynakta yok. |
| **Toplam** | **794** | Birbirini dışlayan sınıflar. |

807 satırın hiçbirinde kontrol edilen kimlik, sözleşme/müşteri referansı, başlangıç ay/yıl, bitiş günü, tutar, para birimi, dönem ve ilgili tarihçe işlem/açıklama alanlarında Excel–staging ham veri farkı yok. Okunan ham payload hash'leri doğrulandı. İncelenen karar nesnelerinde daha önce hedefe aktarılmış kayıt yok; bazıları önceki kararlarla Excluded durumda. Genel hedefteki diğer kayıtlar bu sayıya dahil değildir.

## Uygulama öncesinde ayrılması gereken durumlar

### A. Aynı sözleşmede farklı kararlar

Üçünde de sözleşme satırında silme, tarihçe satırlarında koruma notu bulunuyor. Notun yalnız hatalı güncel fiyatı mı, bütün sözleşmeyi mi kastettiği çıkarılamaz.

| Legacy sözleşme | Madde 6 sözleşme satırı | Korunması istenen tarihçeler | Madde 6 tarihçe satırları |
| --- | ---: | --- | --- |
| 46454 | 138 — SİLİNEBİLİR | 71407, 77398 | 573–574 |
| 47683 | 143 — DONUK KAYIT SİLİNEBİLİR | 68467, 69789, 74210 | 586–588 |
| 47761 | 146 — SİLİNEBİLİR | 68777, 69795, 74209 | 594–596 |

Öneri: bu üç sözleşmenin bütünlüğünü koruyarak bekletmek; ne sözleşmeyi dışlayıp “kalsın” tarihçelerini kaybetmek, ne de silme yanıtını yok saymak.

### B. “Donuk silinsin” notu ile kaynak üyelik kodu

Canlı `Definition.SubscriptionStatus`: **12=Aktif, 13=Donuk**. Excel'deki 445 “donuk” notlu satırın 360'ı kaynak sözleşmede Aktif, 7'si Donuk, 78'i sözleşme/üyelik bilgisi bulunmayan veya boş kayıtlara denk geliyor. Aynı nesne farklı sayfalarda tekrar edebildiğinden bunlar satır adetleridir.

435 dışlama adayının **398 tekil nesnesinde** donuk notu var: **317 Aktif / 6 Donuk / 75 doğrulanamayan**. Bu nedenle “notta donuk yazıyor” diye aktif kayıtları veya tarihçelerini otomatik dışlamak güvenli değildir. Kullanıcının genel donuk kabul kararını değiştirmeden, bu notun kayda özgü kesin dışlama mı yoksa yalnız donuksa dışlama mı olduğu belirlenmelidir.

### C. Koruma notu teknik hatayı çözmüyor

- Madde 7'deki 33 “kalsın” satırından biri madde 4 kapsamında zaten dışlanmış. Kalan **32 tarihçenin para birimi hâlâ belirlenemiyor**: 24'ünde CurrencyID boş; 8'inde CurrencyID=15 fakat canlı Definition.Currency.Name boş. 15'e TL/USD/EUR gibi bir ad tahmin edilmez. Doğru Para Birimi / Doğru Dönem Tutarı alanları doldurulmamış.
- Madde 6'da koruma istenen çakışmalar için Doğru Başlangıç/Doğru Bitiş/Doğru Tutar alanları boş. Koruma sınıfındaki tekil nesnelerde 142 PERIOD_OVERLAP, 35 CONTRACT_CURRENT_RATE_MISMATCH issue'u var; ayrıca 24 aynı başlangıç ve 12 ters tarih aralığı uyarısı bulunuyor. **Issue adetleri kesişebilir, toplanmaz.** “Olduğu gibi” yanıtı güncel sözleşme fiyatı mı son tarihçe mi öncelikli sorusunu çözmüyor.
- Koruma sınıfında 29 tarihçenin üst sözleşmesi yok. Yeni sözleşme üretme veya başka sözleşmeye bağlama yetkisi çıkarılmadı.
- “Grup firması” notları, mevcut madde 2 CustomerGroups.Code sınıflandırmasına veya ortak dbo kayıtlarına değişiklik getirmiyor.

### D. Canlı kaynakta kesitten farklı altı nesne

| Nesne | Alan | Kesit/Excel | Canlı MGS |
| --- | --- | --- | --- |
| Tarihçe 84571 | ProcessType | literal null | Başlangıç |
| Sözleşme 31415 | EndDate | boş | 21.09.2026 |
| Sözleşme 48871 | EndDate | boş | 24.09.2026 |
| Sözleşme 44440 | PaymentTypeID | 30 | 26 |
| Sözleşme 990 | PaymentTypeID | 30 | 26 |
| Tarihçe 84724 | Amount | 0 | 900 |

84571 madde 4 nedeniyle dışarıda kalır. 31415/48871 dışlama adayı, diğer üç nesne koruma sınıfındadır. Bunlar ilk okunan alan kümesindeki farklardır; tüm MGS veritabanının değişmediği iddia edilmez. Henüz snapshot veya hedef güncellemesi yapılmadı.

## Madde 5 ile birleşen sonuç

- Önceki incelemedeki 46387/46506/49228 sözleşmelerinin dışlaması **korunur**; bu konu için yeniden madde 4 onayı istenmez.
- 33948 hem madde 5 satır 6 hem madde 7 satır 46'da dışlama yanıtı taşıyor; tek işlem olarak planlanır.
- Madde 5'te yanıtsız 77219 için madde 6 satır 628'de `silinebilir` yanıtı var; birleşik planda dışlama adayı olur.
- Yanıtsız 35580 önceki kimliksiz müşteri dışlamasında kalır; 77221/77224 halen yanıtsızdır.
- 34133/82291/84457 için farklı sayfalarda karşıt karar yok. **27590/48715/49221 sözleşmeleri, toplam 13 tarifesiyle ilk kontrollü aktarım önizlemesi adaylarıdır.** Bu, aktarım yapılmış veya bütün transaction önkoşulları doğrulanmış demek değildir.
- 37923'ün bağlı olduğu 30149 sözleşmesindeki çakışan 80662/84333 tarifelerine madde 6 satır 307–308'de “kalsın” denmiş; yeni tarih veya öncelik verilmediğinden engel sürer.

## Geliştirme / uygulama sırası

1. Bu toplu planın madde 4'ü koruyan karar sınırını esas al; 1/4 sayfa verilerini işleme sokma.
2. Çelişkili 3 sözleşme, donuk/aktif uyuşmazlıkları, kaynak değişiklikleri ve yanıtı eksik finansal bilgiler için ayrı bekleme listelerini koru.
3. Net kararlar için hash'e bağlı satır karar uygulayıcısı hazırla. Açıklamanın hangi workbook/sheet/satırdan geldiğini audit'e yaz; ham payload'ı değiştirme. Tarihçe dışlama ile sözleşme dışlamasını ayır.
4. Önce staging karar önizlemesi; ardından bütün sözleşme-tarihçe kümesini yeniden doğrulayan hedef aktarım önizlemesi. Eksik tarifeli sözleşmeyi veya yeni boşluk/çakışmayı örtülü olarak kabul etme.
5. Uygulama ayrıca istendiğinde yalnız AssistFlowTest üzerinde transaction/kilit/hash/idempotency ile çalıştır; MGS salt-okunur kalsın. Önceki dışlamalar retry ile açılmasın. Sayım/alan mutabakatı ve tekrar önizleme yap.

## Tekrar inceleme kanıtı

Satır bazındaki karar listesi Git dışında `.tools/items567-decisions.json`, ayrıntılar `.tools/items567-decision-details.json`; workbook verisi `.tools/items567-workbook.json`. DB okumaları `.tools/items567-review.json`, `.tools/items567-legacy.json`, kod tanımları `.tools/items567-currency.json`. Kişisel bilgiler içeren bu ara veriler Git'e dahil edilmez.

İlk salt-okunur inceleme sonunda staging sözleşmeleri: Pending 3 / Blocked 1.723 / Applied 4.474 / Excluded 3.298. O aşamada yeni aktarım **0** idi. Sonradan onaylanan üç sözleşmenin uygulama sonucu üstteki güncel aktarım bölümündedir.
