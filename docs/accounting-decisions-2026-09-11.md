# Tahsilat kararları — 11 Eylül 2026

Bu belge, kullanıcının son açıklaması ve sınır durumlarında inisiyatif alma yetkisiyle önceki açık maddeleri kapatır. 10 Eylül belgesinin çelişen bekleme maddelerinin yerine geçer. Teknik tercihler müşterinin açık cevabı gibi sunulmayacak; müşteriye bildirilecektir.

## Müşterinin açıklamasıyla kesinleşenler

- VAR / EXISTS: sözleşme mevcut, tahsilata dahil.
- YOK / NONE: sözleşme yok, tahsilata dahil değil.
- Gün hesabı yok; ödeme periyodunun tam tutarı alınır.
- Ayın 15'inden önce başlangıç mevcut ay, sonrasında başlangıç sonraki ay hesabına alınır. Yenileme günü korunur.

## Kullanıcının yetkisiyle alınan teknik kararlar

- Tam 15'inde başlayan sözleşme mevcut aya dahildir: kesme sınırı 1–15 dahil / 16–ayın sonu.
- Boş veya UNKNOWN sözleşme durumu, açıklığa kavuşana kadar yeni borç üretimine dahil edilmez. Bilinmeyen/eşlenemeyen kaynak değerleri VAR'a çevrilmez; aktarım istisnası olarak korunur.
- İlk borç tarihi: 1–15 başlangıçta imza tarihi; 16 ve sonrasında sonraki ayın aynı günü. Örnek: 14 Eylül → 14 Eylül; 15 Eylül → 15 Eylül; 16 Eylül → 16 Ekim; 28 Eylül → 28 Ekim.
- Kısa ayda karşılık gelen gün yoksa ayın son günü kullanılır; sonraki dönemlerde asıl imza günü korunur. Örnek: 31 Ocak → Şubat sonu → 31 Mart (aylık). Takvimde clamp edilmiş ilk tarih yeni kalıcı gün ankrajı yapılmamalıdır.
- İlk borçtan sonraki tarihler seçilen periyot aralığıyla ilerler; kesme kuralı dönem tutarını bölmez. Örneğin üç aylık periyotta 28 Eylül başlangıç → 28 Ekim ilk borç → 28 Ocak sonraki borç.
- Muhasebe ayı ilk borç tarihinin ayıdır; ayın ilk günüyle temsil edilen takip Period alanı gerçek borç tarihinden ayrıdır.
- “İkisini de” ifadesinden ek bir iş kuralı çıkarılmaz; yukarıdaki açık gün aralıkları yeterlidir.

## Korunan sınırlar ve uygulama işleri

- Tahsilata dahil olmak tek başına borç oluşturmaz: ücretsiz/donuk ve geçerlilik kuralları ayrıca uygulanır.
- YOK/boş olmak mevcut borç, ödeme, makbuz veya geçmiş kayıtların silinmesi/gizlenmesi değildir. Durumun sonradan VAR olması geçmişi otomatik yeniden hesaplatmaz; geleceğe etkili durum geçişi ayrıca uygulanır.
- Tarihi bilinmeyen legacy kayıtlar için imza günü uydurulmaz. Bu kurallar yeni başlangıç hesapları içindir; geçmiş tutarlar topluca yeniden yazılmaz.
- P05/P06 için artık bu sınırlar nedeniyle müşteri yanıtı beklenmez. Kod entegrasyonu ve testler henüz bu belgeyle tamamlanmış sayılmaz. İlk tarih ile orijinal ankrajın ayrımı, 14/15/16/28/29/30/31 günleri, kısa ay/artık yıl, üç aylık dönem, VAR/YOK/UNKNOWN ve ücretsiz/donuk birleşimleri test edilmelidir.

## Müşteriye gönderilecek kısa not

Tahsilat kuralını şöyle uyguluyoruz: VAR olan sözleşmeler dahil, YOK ve durumu boş olanlar hariç olacak. Ayın 15'i dahil ilk 15 günde başlayanlar mevcut aya, 16'sından itibaren başlayanlar sonraki aya alınacak. Gün hesabı yapılmadan tam dönem ücreti uygulanacak, yenileme günü korunacak. Örneğin 28 Eylül başlangıcının ilk borcu 28 Ekim olacak. Bu kurallar mevcut borç ve ödemeleri silmeyecek veya geriye dönük değiştirmeyecek.
