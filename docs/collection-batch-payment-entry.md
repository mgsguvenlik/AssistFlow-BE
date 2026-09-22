# Toplu tahsilat girişi

## Kapsam

Tahsilat Takibi ekranının sözleşme bazlı görünümünde borcu bulunan satırlar seçilir. En fazla 50 kayıt, ortak ödeme tarihi ve isteğe bağlı açıklamayla kaydedilir. Her satır kendi sözleşme, muhasebe dönemi, para birimi ve ekranda görülen kalan tutarını taşır. Para birimleri çevrilmez; grup özeti üzerinden otomatik dağıtım yapılmaz.

`POST /api/collections/contracts/payments/batch` mevcut `CollectionFollowUp/Edit` yetkisini, yazma feature flag'ini, Autofac kaydını ve `collection.Payment`/`PaymentOperation` altyapısını kullanır. Yeni tablo veya migration yoktur.

## Güvenlik ve tutarlılık

- Kayıt anında sözleşme, müşteri, ücretli tarife, yenileme günü, sözleşme bitişi ve dönem ödemeleri tekrar okunur. Güncel kalan tutar ekrandaki tutardan farklıysa satır atlanır ve liste yenilenmesi istenir.
- Toplu isteğin ana işlem kimliğinden her sözleşme/dönem/para birimi için deterministik alt işlem kimliği üretilir. Başarılı satırlar mevcut PaymentOperation makbuzuyla tekrar oynatılır; ağ hatası sonrası aynı payload ile retry çift ödeme oluşturmaz.
- Satırlar aynı DbContext üzerinde sırayla ve ayrı serializable transaction'larda işlenir. Böylece bir kaydın stale olması diğer geçerli kayıtları engellemez; sonuç başarı/atlanan sayısını açıkça bildirir.
- Altyapı sonucu belirsizleşirse işlem anahtarı ve form korunur. Kesin kullanıcı/validasyon reddinde anahtar yenilenebilir.
- Tüm kullanıcı mesajları Türkçedir; teknik hata ayrıntısı döndürülmez.

## Doğrulama

Backend Release solution build ve frontend production Vite build sıfır hatayla tamamlandı. Mevcut solution nullable/NuGet ve bundle-size uyarıları sürmektedir. Kullanıcı tercihi doğrultusunda yeni test paketi veya veritabanı yazma senaryosu eklenmedi.
