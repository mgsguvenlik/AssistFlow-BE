# Sözleşme dönem bakiyesi

## Kapsam ve kabul kriterleri

- Sözleşme detayı → Ödeme hareketleri → Dönem/devirli bakiye. Muhasebe ayı ve isteğe bağlı hesap tarihi seçilerek Hesapla çağrılır; açılışta otomatik bakiye isteği yoktur. `Önceki dönemleri dahil et` seçeneği sözleşmenin ilk tarife ayından seçilen aya kadar olan birikimi getirir.
- `GET /api/collections/contracts/{id}/balance` mevcut read servisi, CollectionFollowUp/View ve CollectionRead.Enabled ile korunur. Tenant filtresi yoktur. Yeni DI, tablo veya migration gerekmez.
- Borç, mevcut CollectionAccrualRules ve CollectionPeriodRules ile tek ay veya ilk tarife ayından hesap tarihi dahil olacak şekilde hesaplanır. Sözleşmenin inclusive bitiş tarihi exclusive sınıra çevrilir; bitişten sonra borç üretilmez. Donuk/ücretsiz dönemler borç üretmez; orijinal yenileme günü ve ödeme aralığı korunur. Güncel sözleşme durumuyla geçmiş yeniden yorumlanmaz.
- Ödeme toplamı tek dönem görünümünde aynı muhasebe ayına; devirli görünümde ilk tarife ayından seçilen aya kadar olan ve PaymentDate hesap tarihini aşmayan kayıtlara ait SQL SUM sonucudur. Legacy negatif düzeltmeler kendi işaretiyle korunur.
- Para birimleri ayrı satırdır. Kalan = hesaplanan borç − ödeme; negatif değer gizlenmez. Devirli seçenek açıkken önceki dönemlerin aynı para birimindeki kalanları toplama dahildir. Döviz çevrimi ve para birimleri arası otomatik mahsup yapılmaz.
- Eksik/çakışan/boş tarife geçmişi hesaplanamaz; sıfır borç gibi sunulmaz. Bir sözleşme için en fazla 1000 tarife okunur; daha fazlası açık hata verir. Ödemeler SQL'de gruplandığından tek tek belleğe alınmaz; para birimi grubu da 100 ile sınırlandırılır. Bu sınırlı detay özeti, ana takip listesinin server-side pagination gereksiniminin yerine geçmez.
- Yeni tahsilat sonrası eski bakiye görünümü temizlenir, kullanıcı tekrar hesaplar. Abonelik değişikliği ilgili açık bakiye ve tarife geçmişi sorgularını yeniler.
- Bu canlı okuma, tarihsel bir muhasebe snapshot'ı değildir. Hesap tarihi, borç yenileme günü ve ödeme tarihini sınırlar; sonradan yapılan fiziksel düzeltmelerin eski halini yeniden üretmez. Ayrı SQL okumaları arasında eşzamanlı değişiklik olabilir; yazma uygunluğu/borç kapatma kararı olarak kullanılmaz.

## Doğrulama

Backend Release build 0 hata, frontend Vite build ve ilgili lint başarılı. AssistFlowTest üzerinde sözleşme 96 için tek dönem ve devirli bakiye yalnız okuma olarak çalıştı; mevcut veride her iki sorgu 0 para birimi döndürdü. Mevcut proje uyarıları sürer; Vite tam TypeScript doğrulaması değildir. Yeni iş kuralı test senaryosu eklenmedi. Veritabanına yazılmadı; HTTP/tarayıcı kabulü açıktır.

## Sonraki bağımlılık

Ana tahsilat listesi için borç/ödeme birleştirmesi filtre ve sıralamadan önce SQL tarafında kurulmalıdır. Bu detay metodunu her satır için çağıran N+1 tasarım kullanılmayacaktır. Bireysel/grup görünümü, devir ve toplu işlem bağımlılıkları ana planda açıktır.
