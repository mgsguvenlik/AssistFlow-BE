# Tahsilat düzeltme ve fiziksel silme

## Tamamlanan kapsam

- Sözleşme detayındaki ödeme hareketlerine Düzenle ve Sil işlemleri eklendi. Yalnız `CollectionFollowUp/Edit` yetkisi olan kullanıcı görür ve kullanır.
- Düzenleme muhasebe dönemi, ödeme tarihi, pozitif tutar, para birimi ve açıklamayı değiştirir. Yeni dönem/para biriminde tarihsel ücretli tarife bulunması gerekir.
- Silme ters kayıt oluşturmaz; `collection.Payment` satırını fiziksel siler. `collection.PaymentOperation` kaydı ödeme FK'si taşımadığı için önceki içerik, kullanıcı, işlem türü ve zaman bilgisi audit olarak korunur.
- PATCH ve DELETE işlemleri rowversion ister. Başka işlemle değişen kayıt 409 ile reddedilir. Ödeme kimliğinin URL'deki sözleşmeye ait olduğu transaction içinde yeniden kontrol edilir.
- İstemci her düzeltme/silme için bir işlem anahtarı üretir. Sonuç belirsizse form kapatılamaz ve aynı içerik/anahtarla retry edilir. Sunucunun kesin 4xx cevabında kilit kaldırılır. Belirsiz altyapı sonucu 503 kullanılarak stale rowversion 409 durumundan ayrılır.
- Başarıda ödeme listesi ve ekrandaki dönem bakiyesi temizlenip yenilenir. Tüm kullanıcı mesajları Türkçedir.

## Sınırlar ve doğrulama

Toplu düzeltme/silme yoktur. Ücretsiz ödeme oluşturma ve negatif yeni kayıt desteklenmez; legacy negatif kayıtlar okunmaya devam eder. Backend Release build 0 hata, frontend Vite build ve ilgili ESLint başarılı. Debug build çalışan API'nin DLL kilidi nedeniyle tamamlanamadı; çalışan uygulama durdurulmadı. Yeni migration veya DB değişikliği yoktur. Mevcut transaction altyapısının daha önceki gerçek SQL update/delete kontrolleri korunur; bu dilim için yeni test senaryosu eklenmedi. Yeni HTTP ekran akışının kullanıcı kabulü açıktır.
