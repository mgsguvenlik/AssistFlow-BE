# Tahsilat veri aktarım kararları — 20 Eylül 2026

Bu belge müşterinin e-posta cevabını kaydeder. Önceki teknik varsayımlarla çeliştiği yerde bu belge geçerlidir.

## Kesinleşen kararlar

- Sözleşme durumu boş veya `Belirtilmemiş` olan kayıtlar tahsilata dahil edilecektir. Buradaki alanın amacı müşterinin sözleşmesinin bulunup bulunmadığını göstermektir. Açık `YOK` değeri sözleşme yok anlamında aktarım dışında kalır; boş/`Belirtilmemiş` artık YOK kabul edilmez.
- Müşteri eşleştirme istisnalarında hem abone numarası hem de müşteri adı olmayan kayıtlar dikkate alınmayacaktır. Alanlardan en az biri doluysa satır, müşteri kararı gelmeden otomatik dışlanmayacaktır.
- Legacy dosya bilgisi bulunmasına rağmen fiziksel sözleşme dosyasına erişilemiyorsa sözleşmenin dosyasız aktarılması ve eksik dosyanın ayrıca raporlanması uygundur. Mevcut CDN altyapısı değiştirilmeyecek ve bulunmayan dosya için sahte kayıt oluşturulmayacaktır.

## Müşteri incelemesi beklenen listeler

- Madde 2: Legacy müşteri türü ile AssistFlow müşteri türü uyuşmayan kayıtlar. Müşteri, kişi listesini inceleyerek tür değişikliği kararını bildirecek.
- Madde 3: AssistFlow'da aynı abone numarasıyla karşılığı bulunamayan müşteriler. Yeni müşteri oluşturma, mevcut müşteriyle eşleştirme veya aktarım dışı bırakma kararı satır bazında beklenecek.
- Madde 5: İşlem türü `null` olan sözleşme tarihçeleri. Normal ücretli dönem veya aktarım dışı kararı beklenecek.
- Madde 6: Çakışan tarife tarihleri ile güncel sözleşme tutarı/son tarihçe tutarı farklı kayıtlar. Esas alınacak kayıt veya düzeltilecek bilgi beklenecek.
- Madde 7: Para birimi ya da tutarı belirlenemeyen tarifeler. Doğru değer ve aktarım kararı beklenecek.

Bu listeler müşteriye sekiz sayfalı `Tahsilat_Musteri_Karar_Listeleri.xlsx` çalışma kitabında iletilmiştir. Cevap gelene kadar ilgili satırlar staging alanında karantinada kalır; otomatik müşteri oluşturulmaz, müşteri türü değiştirilmez ve finansal değer tahmin edilmez.

## Uygulama sırası

1. Boş/`Belirtilmemiş` sözleşmeleri ve bağlı tarihçelerini yeniden doğrulayan salt-okuma planı üret.
2. Hem abone numarası hem müşteri adı boş olan istisnaları ayrı ve denetlenebilir dışlama kümesine al.
3. Müşteri dönüşü gerektirmeyen sözleşmeleri mevcut idempotent aktarım akışıyla işle.
4. Fiziksel dosyası olmayan sözleşmeleri engellemeden aktar; eksik dosya raporunu koru.
5. Maddeler 2, 3, 5, 6 ve 7 için doldurulmuş çalışma kitabı geldiğinde satır kararlarını doğrula ve ancak sonrasında staging kararlarına uygula.
