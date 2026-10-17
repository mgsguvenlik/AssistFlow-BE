# Tahsilat takip listesi

## Tamamlanan kapsam

- CRM → Tahsilat → Tahsilat Takibi ekranı ve `GET /api/collections/tracking` eklendi. Mevcut `CollectionFollowUp` görüntüleme yetkisi ve modül açma kapısı kullanılır.
- Liste tek muhasebe ayını sorgular. Abone/müşteri metni, müşteri, servis tipi ve para birimi filtreleri bulunur. Liste ilk açılışta istek atmaz; kullanıcı Sorgula dediğinde çalışır.
- Borç satırı, seçilen ayda yenileme tarihi bulunan ücretli tarife döneminden oluşur. Ödeme aynı sözleşme, muhasebe ayı ve para biriminde SQL tarafında toplanır. Kalan = borç − ödeme; para birimleri birleştirilmez.
- Sıralama ve 25/50/100 kayıtlık sayfalama SQL tarafında filtrelerden sonra uygulanır. Sabit ContractId/CurrencyTypeId bağlayıcı sırası vardır. N+1 detay çağrısı veya bütün kayıtları belleğe alma yoktur.
- Sözleşmenin inclusive bitiş tarihi vade tarihinden önceyse satır oluşmaz. Donuk/ücretsiz/YOK dönemler persisted `BillingBehavior` üzerinden borç üretmez; güncel durum geçmiş tarifeyi yeniden yorumlamaz.
- Görünüm seçimi sözleşme bazlı liste ile grup özetini ayırır. Grup özeti mevcut `dbo.CustomerGroup` bağlantısı üzerinden, grup ve para birimi bazında borç/ödeme/kalan toplamı ile farklı sözleşme sayısını döndürür. Grubu olmayan müşteriler yalnız sözleşme bazlı görünümde kalır.
- Salt-okuma incelemede mevcut CustomerType değerlerinin grup ayrımı için güvenilir olmadığı görüldü: Bireysel ve BANKA kayıtlarının neredeyse tamamında CustomerGroupId doludur. Bu nedenle ortak CustomerType verisi değiştirilmedi ve grup finansal özeti CustomerGroup üzerinden kuruldu.
- Seçilen dönemde ödeme olup vadesi gelen ücretli tarife satırı bulunmayan sözleşme/para birimleri de listelenir. Bunlar `Yalnız ödeme`, sıfır borç ve negatif kalan tutarla açıkça işaretlenir; sessizce kaybolmaz.
- Dönemler arası devir, toplu tahsilat ve fiziksel düzeltme sonraki bağımlılıklardır.

## Doğrulama

Backend solution build 0 hata, frontend Vite build ve ilgili ESLint kontrolleri başarılı. AssistFlowTest üzerinde bireysel ve grup sorguları yalnız okuma olarak çalıştı; SQL çevirisi, istisna birleşimi ve sayfalama doğrulandı. Mevcut veride 1 bireysel/1 grup sonuç döndü. Veritabanı değiştirilmedi. Yeni iş kuralı test paketi/senaryosu eklenmedi.
# Grup görünümü güncellemesi

Grup özetinde yalnız mevcut `CustomerGroupId` ile bağlı müşteriler yer alır. Gruba bağlı olmayan bireysel müşteriler sahte bir toplam grup altında birleştirilmez; sözleşme bazlı görünümde kalırlar. Grup satırındaki “Grubu incele”, seçilen dönem ve para biriminde sunucu taraflı grup filtresiyle üye sözleşmelerine iner; “Grup özetine dön” önceki özeti açar.

Gruba bağlı sözleşme satırındaki “Durum” düğmesi açıldığında `GET /api/collections/tracking/{contractId}/group-status?period=...` çağrılır; panel kapalıyken istek atılmaz. Yetkili kullanıcı `PUT` ile grup durum etiketini ve en fazla 500 karakter açıklamayı kaydeder. Eşzamanlı değişiklikte rowversion çatışması yenileme ister. Etiket finansal ödeme/bakiye hesabından ayrıdır; eski legacy “Ödendi” etiketinin otomatik ödeme yaratma davranışı yeni sistemde uygulanmaz. Bu, müşterinin manuel tahsilat ve etiket bağımsızlığı kararına uygundur.

Sözleşme satırındaki “Sözleşmeyi incele” seçilen sözleşmenin doğrudan detayını açar; sözleşme listesini ayrıca sorgulamak gerekmez. Detay kapanınca URL'deki sözleşme kimliği temizlenir.

Salt görüntüleme yetkisi olan kullanıcı finansal satırları görebilir; toplu tahsilat düğmesi ve seçim kutuları yalnız `CollectionFollowUp/Edit` yetkisiyle gösterilir. Sunucu tarafındaki aynı yetki kontrolü korunur. İlk sorgu öncesinde dönem seçme yönlendirmesi gösterilir.

Bakiye durumu filtresi tüm kayıtlar, pozitif kalan, sıfır/negatif kalan ve borç dönemi bulunmayan yalnız ödeme kayıtlarını ayırır. Grup görünümünde koşul bireysel üyeye değil grup/para birimi net toplamına uygulanır; grup satırına inildiğinde mutabakat için tüm üye sözleşmeleri listelenir. Sayım ve sayfalama filtrelenmiş sorgudan hesaplanır.

“Filtrelenen tüm kayıtları CSV indir” geçerli sorgunun bütün sonuçlarını (ekrandaki sayfayla sınırlamadan) aynı sıralamada akış halinde üretir. Yetki View'dur; Türkçe başlık ve UTF-8 BOM kullanılır. Kullanıcı metinleri CSV kaçışı ve formül enjeksiyonu korumasından geçer. Excel exporter'ın tüm veriyi bellekte tutan yolu kullanılmaz.
