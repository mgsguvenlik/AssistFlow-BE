# Sözleşme oluşturma — 13 Eylül

## Uygulama

`POST api/collections/contracts` mevcut CollectionFollowUp/Edit yetkisini kullanır. Kullanıcı kimliği oturum claim'inden alınır; istemcinin actor alanı yoktur. DI kaydı AutofacBusinessModule'dadır. Geçerli mevcut müşteri/servis/para birimi ve aktif collection tanımları kontrol edilir; ortak tabloya yeni müşteri/servis/tanım eklenmez.

Sözleşme ve ilk tarife aynı transaction'dadır. RequestId ve içerik hash'i sözleşmede kalır; aynı kullanıcı/aynı içerik tekrarında aynı sözleşme kimliği döner. Yeni bir anahtarla aynı iş verisini göndermek ayrı bir sözleşme talebidir; domain seviyesinde müşteri+servis tekilliği icat edilmedi. Fiziksel sözleşme silme bu akışta yoktur; gelecekte eklenirse request tombstone ayrıca korunmalıdır.

Tam dönem tutarı saklanır, ödeme/tahakkuk kaydı yazılmaz. Başlangıç 1–15 ise ilk tarife imza gününde, 16+ ise sonraki ayın aynı gününde başlar. Orijinal gün ayrı saklanır. İmza tarihi sözleşmede korunur. İlk borç tarihinden önce biten sözleşmenin tutarı korunur ancak aralığı Suspended tarifedir. Ücretsiz ilk tarifede Free; diğer durumlarda YOK/boş veya donuk Suspended, VAR+aktif Billable olur. Ücretsiz ve donuk birlikteyse Free borç üretmez; abonelik durumu ayrı saklanır. Gelecekteki durum geçişleri mevcut tarifeyi topluca yeniden yorumlamamalıdır.

## Veritabanı

`20260913081707_AddCollectionContractCreationRequest` uygulandı: collection.Contract'a nullable CreationRequestId/CreationPayloadHash, filtreli unique indeks ve eşli null/32-byte CHECK eklendi. Eski kayıtlar backfill edilmedi. SQL script'i hedef guard, uygulama kilidi ve transaction içerir. Migration Down işlem anahtarlarını yok edeceğinden veri yedeği ve açık onay olmadan uygulanmaz.

## Retry

Global AppDataContext retry ayarı değiştirilmedi. Yalnız bu komut mevcut SQL Server execution strategy sınıfıyla sınırlı retry yapar; EF'nin retriesiz iç provider'ının InvalidOperationException/DbUpdateException sarmalaması alttaki SQL transient sınıflandırıcısına açılır. Gerçek deadlock testi bu farkı ortaya çıkardı ve düzeltme sonrası geçti. En fazla 5 retry, en fazla 2 saniye retry gecikmesi; kalıcı hata sonsuz denenmez.

Frontend aynı form içindeki aynı payload için aynı UUID'yi kullanır. Senkron busy kilidi vardır; işlem sürerken form kapatılamaz, hata halinde değerler korunur. Form kapatılıp yeniden açılırsa işlem anahtarı korunmaz; arayüz mesajı bunu açıkça sınırlar. Müşteri ve tanım seçimleri istek üzerine sayfalı yüklenir; tüm müşteri listesi indirilmez.

## Test komutu

`dotnet run --project tools/Collections.Sql.Tests -- --create-contract-fixtures WebAPI/appsettings.Development.json`

Yalnız AssistFlowTest kabul edilir; geçici sözleşme/tarifeler rastgele işaret ve request anahtarlarıyla ayrılır, finally'de yalnız bu kayıtlar silinir. Ortak kullanıcı/müşteri/servis/kur satırları sadece okunur. Zorla süreç kapatılırsa finally garantisi yoktur; kalıntı temizliği genel tablo silmeyle yapılmamalıdır. Identity sayaçları ilerleyebilir.

19 SQL kontrolü, 112 model/API kontrolü ve 91 saf iş kuralı kontrolü geçti. FE lint/build başarılı, genel TypeScript baseline temiz değildir. Simüle edilen commit yanıt kaybı gerçek ağ failover testi değildir.

## Henüz açılmadı

Development'ta CollectionRead.Enabled ve CollectionRead.ContractCreateEnabled varsayılan false. DB'de CollectionFollowUp menü kaydı yok. Ortak menü/rol tablolarına kullanıcı onayı ve hangi role hangi yetkinin verileceği belirlenmeden müdahale edilmedi. Özellik açıldıktan sonra yetkili/yetkisiz gerçek oturumlarla browser kabulü gerekir; formun kullanıcıya açık production kabulü henüz tamamlanmış değildir.
