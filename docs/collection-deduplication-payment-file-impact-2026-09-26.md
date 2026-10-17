# Madde 4 — Legacy ödeme ve dosya etkisi

## Sonraki kullanıcı kararı — 26 Eylül

**Eski aboneliklerin ödemeleri yeni sisteme aktarılmayacak; yalnız MGS'de kalacak.** Bu cevapla madde 4'ün ödeme bağımlılığı kapandı. Kullanıcı madde 4 aktarımlarının tamamlanmasını onayladı. Dosyalar 8. maddede ele alınacak; metadata ve kaynak bağlantıları korunur, bu işlem dosya silme/aktarma izni değildir.

Bu onay sonrasında AssistFlowTest'teki 138 eski sözleşme ve 563 tarife, 701 aktarım eşlemesiyle birlikte tam yerel yedek alınarak kaldırıldı ve staging kayıtları Excluded oldu. MGS ve dbo müşteri verilerine dokunulmadı. Yedek: `tools/Collections.Import/snapshots/item-four-removal-20260926-161939613/backup.json`; SHA-256 `0964BFA7D8713AC3EB5DAF74DC7194DE1B998AE0774F58DB39A136E28BDBE34C`. Bu yedek tek başına tam veritabanı yedeği değildir; hedef/map/stage/issue kaynak değerleriyle kontrollü geri yükleme için tutulur, otomatik geri yükleme denenmedi.

**Aşağıdaki envanter ve karar bekleme metinleri, bu son onaydan önceki salt-okuma incelemesinin tarihsel kaydıdır.** 19.963 ödeme satırı aktarılacak bir kuyruk değildir; elenen aboneliklerin MGS'de korunacak geçmişidir.

**Son kullanıcı kapsam düzeltmesi:** Dosya konusu **8. maddede** ayrıntılı ele alınacak. Aşağıdaki dosya envanteri 8. maddeye girdi olarak saklanır; bu çalışma kapsamında yeni fiziksel dosya incelemesi, CDN aktarımı veya silme yapılmaz. **4. maddede açık karar artık eski ödeme geçmişinin akıbetidir; dosya kararını burada tekrar istemeyeceğiz.**

## Kapsam ve işlem sınırı

Kullanıcı, ödeme/dosya etkisi bilinmeden kaldırma yapılmamasını ve önerilen salt-okunur incelemeyi onayladı. **Bu onay veri silme onayı değildir.** Bu çalışma boyunca MGS ve AssistFlowTest'e yalnız SELECT gönderildi. Hedef sözleşme/tarife/ödeme, staging, müşteri veya fiziksel dosya değiştirilmedi.

İncelenen kaynak kimlikleri, önceki madde 4 karar planlarından çıkarılan **599 tekil ContractID**'dir: aynı abone numarası nedeniyle dışlanan 242, aynı isim nedeniyle dışlanan 219 ve önceden hedefe aktarılmış fakat yeni kararla eski sayılan 138 sözleşme. Kaynakta bulunamayan sözleşme 0.

## Doğrudan sözleşme bağlantıları

| Küme | Sözleşme | Ödeme satırı bulunan sözleşme | Core.Payment satırı | Dosya adı/yolu dolu sözleşme |
| --- | ---: | ---: | ---: | ---: |
| Aynı abone numarasıyla dışlanan | 242 | 212 | 9.326 | 141 |
| Aynı isimle dışlanan | 219 | 140 | 4.417 | 50 |
| Önceden aktarılmış, kaldırılması bekletilen | 138 | 125 | 6.220 | 65 |
| **Toplam** | **599** | **477** | **19.963** | **256** |

461 dışlanmış sözleşmenin toplam etkisi **13.743 ödeme satırı ve 191 sözleşmede dosya metadata'sıdır**. Önceki hedef kontrolünde söylenen “0 ödeme / 0 dosya”, yalnız AssistFlowTest'e henüz aktarılmamış bağlantıları ifade ediyordu; legacy için geçerli değildir.

## Ödeme bulguları ve sınırlar

- Core.Payment, hem CustomerID hem ContractID taşır. İncelenen doğrudan 19.963 satırda ödeme müşterisi ile sözleşme müşterisi uyuşmazlığı 0.
- Bu sayı başarılı nakit tahsilat adedi olarak yorumlanmamalıdır: **328 satır Free='Evet'**, 19.635 satır Free='Hayır'; **3 eksi tutarlı, 1 sıfır tutarlı** satır var. Diğer 19.959 satır pozitif; tutarı null olan satır yok. Bu sınıflar birbiriyle kesişebilir.
- Ödeme tarihi boş veya ay/yıl aralığı geçersiz satır yok. Görülen tarihler 12.01.2002–23.09.2026 aralığında; önceden aktarılmış 138 sözleşmeye bağlı ödemelerde de son tarih **23.09.2026**. Eski müşteri oluşturma tarihi, finansal hareketin eski veya gereksiz olduğunu kanıtlamaz.
- Ödeme tablosunda ayrı para birimi kolonu yok. Core.vPayment, para birimini dbo.vPaymentCurrency üzerinden ödeme ay/yılı ile tarife geçmişinden bulur ve `TOP 1` kullanır. Çakışan/eksik tarihçelerde bu yöntem güvenli aktarım eşlemesi sayılmaz. Para birimleri doğrulanmadan tüm tutarlar toplanmadı ve finansal bakiye sonucu çıkarılmadı. Madde 6/7 kararları bu nedenle ödeme aktarımını da etkiler.
- Aynı etkilenen müşteriler üzerinden ek incelemede **başka mevcut sözleşmelere bağlı 2.320 ödeme satırı** ve **kaynak sözleşmesi bulunamayan 237 ödeme satırı (7 müşteri)** görüldü. Bunlar yukarıdaki 19.963'e dahil değildir; müşteri bazlı toplu silme bunları da etkileyebilir. Başka sözleşmelere ait ödemeler bu çalışma kapsamına taşınmadı.
- İlgili dolu SubscriberNo değerleriyle Core.BulkPayment ara tablosunda eşleşen satır bulunmadı. Bu, farklı GTS/IVR gibi alternatif eşleştirmelerle ara kayıt olmadığını kanıtlamaz. BulkPayment zaten doğrudan kesinleşmiş tahsilat tablosu kabul edilmedi; kart/iletişim alanları okunmadı.

## Dosya bulguları ve sınırlar

- 256 sözleşmede hem dosya adı hem yolu dolu; tek alanı dolu kısmi metadata 0. Bunlar **156 benzersiz dosya yoluna** karşılık geliyor.
- 188 sözleşmenin dosya yolu başka bir legacy sözleşmede de kullanılıyor. 94 sözleşmenin referans verdiği **42 benzersiz yol**, 599 sözleşmelik inceleme kümesinin dışında da kullanılıyor. Sözleşme dışlama kararı fiziksel dosya silme kararı değildir.
- 4 referansın dosya uzantısı mevcut PDF/PNG/JPG/JPEG kabul listesinde değil; dosya sayısı/gerçek içerik kabulüyle karıştırılmamalıdır.
- MGS-PLATFORM.zip içinde dolu `Content/Contract` fiziksel dosya girdisi 0. DB'de yol bulunması dosyanın diskte/CDN'de mevcut olduğunu doğrulamaz. Kaynak sunucudaki fiziksel kök ayrıca sağlanıp doğrulanmalıdır; bulunmayan dosya varmış gibi taşınmadı. Yeni CDN altyapısı veya dosya silme işlemi yok.
- 92 sözleşmede doğrudan Core.Payment satırı ve sözleşme dosya metadata'sı birlikte yok. Bu sadece incelenen iki bağlantı içindir; otomatik olarak “güvenle silinebilir” sonucu çıkarılmaz.

## Karar / sonraki adım

**138 hedef sözleşme ve 563 tarife, ödeme geçmişi kararı netleşmeden kaldırılmayacak; 461 staging dışlaması finansal geçmiş için nihai aktarım kararı sayılmayacak.** Kaynak kayıtlar ve ham staging korunduğundan dışlama geri değerlendirilebilir. Dosya kararları 8. maddeye bırakıldı. 4. madde için şu ayrım müşteriyle netleşmeli:

> Yalnız en yeni aboneliğin kullanılmasına karar verdik. Ancak eski aboneliklerde ödeme geçmişi bulunuyor. Eski aboneliklerin ödemeleri yeni sistemde de korunacak mı, yoksa yeni sisteme aktarılmayıp yalnız eski sistemde mi kalacak?

Öneri: operasyonel tekilleştirme ile tarihsel finansal korumayı ayrı ele almak. Eski ödemeleri güncel aboneliğe otomatik bağlamak veya eski sözleşme/ödeme geçmişini kaldırmak bu incelemenin sonucu olarak uygulanmaz. Kullanıcı/müşteri ödeme kararı sonrası sözleşme–tarife–ödeme eşlemesi ve mutabakatı hazırlanır. Dosya adı/yolu ve eski sözleşme kimliği bağlantıları 8. madde için korunur. Bu belge yeni bir ekran/özellik geliştirme kararı değildir.

## Kanıtlar

26 Eylül 2026 canlı salt-okuma envanteri; kaynak ve hedef yazması yok. SQL sorguları explicit 599 kimlikle çalışır; dosya adı/yolu ve müşteri bilgileri içeren satır listeleri Git dışında `.tools` altında tutulur:

- `.tools/item-four-legacy-impact.sql` / `.json`: sözleşme bazlı doğrudan ödeme ve dosya envanteri; Core.vPayment tanımı.
- `.tools/item-four-legacy-related.sql` / `.json`: müşteri üzerinden ek ödemeler, paylaşılan dosya yolları, BulkPayment sayımı ve dbo.vPaymentCurrency tanımı.
- `.tools/item-four-payment-quality.sql` / `.json`: Free işareti, tarih ve dönem sayımları.
- `.tools/item-four-legacy-schema.json`: doğrulanan tablo/kolon envanteri.
- Legacy kaynak ZIP içindeki `wwwroot/Pages/Core/Customer.aspx.cs`, ödeme CRUD alanları ve sözleşme dosyası metadata ilişkisini doğrular. Kaynaktaki dosya silme yordamı incelendi, **çalıştırılmadı**.

Bu bir mutabakat öncesi bağımlılık envanteridir; nihai ödeme aktarımı veya fiziksel belge bütünlüğü raporu değildir.
