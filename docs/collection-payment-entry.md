# Tekil tahsilat girişi

## Kapsam

Sözleşme detayı → Ödeme hareketleri → Tahsilat ekle. Muhasebe ayı, ödeme tarihi, pozitif tutar, mevcut para birimi ve isteğe bağlı açıklama girilir. Para birimi mevcut aramalı tek select bileşeninden seçilir. Ücretsiz kayıt, negatif düzeltme, toplu işlem ve ödeme düzenleme/silme bu dilimin dışındadır.

`POST /api/collections/contracts/{id}/payments` mevcut CollectionFollowUp/Edit ve CollectionRead.Enabled/ContractCreateEnabled kapılarını kullanır. DI AutofacBusinessModule içindedir; Program.cs değiştirilmedi. Mevcut collection.Payment ve PaymentOperation tabloları kullanılır; yeni migration veya ortak dbo değişikliği yoktur.

## Kabul kriterleri ve davranış

- Aktif kullanıcı, silinmemiş sözleşme ve müşteri gerekir. Dönem ayın ilk günüdür; tutar pozitif ve en fazla iki ondalıklıdır. Açıklama en fazla 1000 karakterdir.
- Seçilen ayla kesişen, aynı para biriminde ücretli bir tarife bulunmalıdır. Güncel abonelik durumuyla geçmiş tarife yeniden yorumlanmaz: donuk/ücretsiz abonenin geçmiş ücretli dönemine tahsilat mümkündür.
- Bu kontrol borç/bakiye hesabı değildir; dönem içindeki yenileme gününü veya kalan borcu hesaplamaz. Kısmi tutar kaydedilebilir. Fazla ödeme sınırlaması, otomatik borç kapatma/dağıtma bu dilimde eklenmedi. Bunlar ana takip/bakiye entegrasyonunda ele alınacaktır.
- Uygunluk kontrolü, ödeme ve işlem makbuzu aynı serializable transaction içindedir. Aynı kullanıcı/anahtar/içerikle retry önceki işlem makbuzunu döndürür; farklı içerik aynı anahtarla kabul edilmez. Makbuz güncel ödeme varlığı/bakiye iddiası değildir.
- İstemci kayıt sırasında alanları ve detayın kapatılmasını kilitler. Sonuç belirsizse aynı payload/anahtar korunur ve aynı işlem tekrar gönderilir. Anahtar form belleğindedir; sayfa yenileme veya uygulamadan ayrılma sonrası korunmaz. Böyle bir durumda yeni ödeme girmeden hareketler kontrol edilmelidir. Finansal içerik browser storage'a yazılmaz.
- Başarıdan sonra form kapanır ve sayfalı hareket listesi yenilenir. Mesajlar Türkçedir. Liste kapalıyken ödeme isteği yoktur.

## Doğrulama — 14 Eylül

Backend solution build 0 hata; frontend Vite build başarılı. İlgili FE dosyaları lint kontrolünden geçirildi. Mevcut bağımlılık/nullable/bundle uyarıları kapsam dışındadır. Vite tam TypeScript kontrolü değildir. Kullanıcı isteğiyle yeni test paketi veya senaryosu eklenmedi. Yeni ödeme endpointinin gerçek HTTP/SQL ve tarayıcı kabulü henüz yapılmadı; build sonucu bunun yerine geçmez.

Bağlantı yeniden açılınca önceki tarayıcı fixture sözleşmesi 95'in varlığı SELECT ile doğrulandı; mevcut korumalı araç yalnız işaretli sözleşmeyi ve sıfır tutarlı tarifelerini temizledi. Ortak kayıtlar silinmedi; bu geçici veriler fiziksel silindi.
