# Abonelik durum değişikliği

Endpoint: `POST /api/collections/contracts/{id}/subscription`. Mevcut CollectionRead.Enabled ve ContractCreateEnabled yazma kapısı, kimlik doğrulama ve CollectionFollowUp/Edit gerekir. DTO: `freeze`, detaydan gelen base64 `rowVersion`, en fazla 70 karakter `reason`.

İşlem Türkiye saatine göre bugün geçerlidir. Aktif → donuk ve donuk → aktif dışında değişiklik kabul edilmez. Eski tarife bugün hariç olacak şekilde kapanır; yeni tarife bugün başlar. Ücret, kur ve ödeme dönemi kopyalanır. Aktifleştirme bugünü yenileme ankrajı yapar; ilk sözleşmenin 15 kesmesi yeniden uygulanmaz. Ücretsiz hizmet ve YOK/UNKNOWN hariçliği devam eder. Kayıtlı ödeme silinmez veya değiştirilmez. Yeni tarife açıklaması, oluşturan kullanıcı ve tarih durum geçişinin kaydıdır.

Tek context ve serializable transaction kullanılır. Açık tarife unique indeksi için önce eski dönem kapanır, ardından yeni dönem kaydedilir; hata ikisini ve sözleşme değişikliğini geri alır. Rowversion üzerinden eski/tekrar gönderim reddedilir. Bu işlem için başarı makbuzu/replay endpoint'i yoktur: yanıt kaybında 409 ve detay yenileme talebi döner; aynı sürümle yeniden gönderim ikinci değişiklik yaratmaz.

Günlük model sınırı: tarife bugün başladıysa aynı gün ikinci geçiş yapılamaz. Tarife gelecekte başlıyorsa, sözleşme sona ermişse, birden fazla ileri dönem varsa veya tarife eksikse açık Türkçe hatayla durur. Bu guard müşteri tarafından bildirilmiş bir iş kuralı değildir; mevcut gün hassasiyetli modelde sıfır uzunlukta tarihçe ve aynı gün mükerrer tahakkuk üretmemek için teknik sınırlamadır. Aynı gün geri alma/düzeltme ve ileri tarihli durum planlama tamamlanmış sayılmaz; bunlar ayrı kalıcı işlem audit'i ve gün içi tahakkuk politikası gerektirir.

Doğrulama: toplam 37 gerçek SQL kontrolü (oluşturma dahil), dondurma/aktifleştirme, ücretsiz/YOK, eski sürüm, rollback, commit yanıt kaybı, eşzamanlı gönderim ve aynı gün guard. 124 offline model/API kontrolü. FE lint ve Vite build başarılı. Tarayıcı E2E ve gerçek HTTP yetki matrisi açık. Testler yalnız AssistFlowTest üzerinde rastgele işaretli geçici collection verisini oluşturup temizler.

P05 kalanları: sözleşme kimlik alanı düzenleme, tarihli ücret/ücretsiz/dahil olma değişiklikleri, aynı gün düzeltme akışı, geriye tarihli finansal düzeltme sınırları. Bu endpoint genel sözleşme düzenleme yerine geçmez.
