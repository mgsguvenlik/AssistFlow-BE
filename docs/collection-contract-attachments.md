# Sözleşme dosyaları

Yeni dosyalar mevcut `IFileStorage`/Cloudflare R2 uygulamasıyla yüklenir. `collection.ContractAttachment` yalnız sözleşme ilişkisi, orijinal ad, mevcut CDN anahtarı, içerik türü, boyut ve audit alanlarını tutar. Ortak CDN altyapısı ve `dbo` tabloları değişmez. Kurulum migration'ı: `20260916202500_AddCollectionContractAttachment`; SQL: `sql/20260916202500_AddCollectionContractAttachment.sql`.

`GET api/collections/contracts/{id}/attachments?page=1&pageSize=20` görüntüleme yetkisiyle sayfalı metadata döndürür. `POST` düzenleme yetkisiyle `file` multipart alanını alır. En fazla 20 MB PDF/PNG/JPG kabul edilir; uzantı, MIME ve dosya imzası kontrol edilir. FE listeyi yalnız dosyalar bölümü açıldığında ister. CDN URL'si mevcut genel erişim modeline uygundur; kullanıcı bu yapının değiştirilmeden kullanılmasını onayladı.

Upload'da CDN yazısı başarılı olup DB commit sonucu belirsizleşirse CDN nesnesi körlemesine silinmez; aksi halde geçerli metadata kırılabilir. API Türkçe belirsizlik mesajı verir ve kullanıcıdan tekrar yüklemeden önce listeyi yenilemesini ister. Böyle durumdaki olası sahipsiz CDN nesneleri operasyonel inceleme gerektirir. Legacy `FileAttachmentName`/`FileAttachmentPath` staging'de ham olarak korunur; yerel legacy dosyaların yeni CDN'e fiilen taşınması ayrı kaynak dosya kesiti ve erişim doğrulaması gerektirir. Bu dilimde legacy dosya transferi yapılmadı.

## Legacy dosya envanteri

### Güncel kaynak — kullanıcı tarafından sağlanan Contract.rar

Sunucu erişim denemesinden sonra kullanıcı fiziksel kaynak arşivini sağladı. 26 Eylül'de 1.769 dosya listelendi; kullanıcı onayı sonrasında CRC, imza/boyut/hash ve antivirüs kontrolü tamamlandı. Güncel aktarılmış sözleşmelerde **1.830 bağlantı / 1.119 fiziksel kaynak yolu** mevcut CDN'in `uploads-test` alanına yüklendi ve test sözleşmelerine bağlandı. Bütün içerikler geri okunarak hash doğrulandı; son SQL alan/eşleme farkı 0. Arşivde yolu bulunamayan 74 ve boş dosyalı 19 sözleşme ayrı raporlandı, sözleşmeleri korundu. [Uygulama sonuçları ve işletim notları](collection-item-eight-file-migration-plan-2026-09-26.md) güncel kayıttır; aşağıdaki 20 Eylül sayıları tarihseldir.

**Önceki çoklu bağlantı niyeti hakkında düzeltme:** Mevcut `UX_ContractAttachment_StoredFileName` tekillik kuralı aynı CDN anahtarının birden fazla attachment satırında kullanılmasını engeller. Ortak altyapıyı ve şemayı değiştirmemek için yeni planda sözleşme–dosya ilişkisi başına kararlı ayrı anahtar kullanılacak; tekrar denemede aynı ilişki çoğaltılmayacak. Aşağıdaki eski “aynı fiziksel nesneyi bir kez yükleyip çok sözleşmeye bağlama” ifadesi uygulanmış davranış değildir ve güncel planla değiştirilmiştir.

### 26 Eylül — fiziksel kaynak sunucusuna erişim kontrolü

Kullanıcı sözleşme dosyalarının `192.168.1.20` sunucusunda bulunduğunu belirtti ve ilgili dosyaların burada salt-okunur aranmasını istedi. SMB TCP 445 erişimi başarılı; ancak mevcut Windows oturumuyla `net view \\192.168.1.20` paylaşım listelemesi **sistem hatası 5 / Erişim engellendi** sonucunu verdi. Paylaşımlar ve fiziksel dosyalar henüz listelenemedi. Bu bir dosya yokluğu sonucu değildir.

Sonraki adım: kullanıcı sunucu paylaşımına Windows üzerinden okuma yetkili oturum açmalı ve erişilebilir UNC paylaşım/klasör yolunu sağlamalı. Ardından güncel sözleşme metadata envanteriyle fiziksel yollar eşleştirilecek; bulunan, bulunamayan ve erişilemeyen kayıtlar ayrı raporlanacak. Bu erişim kontrolünde sunucu/DB/CDN yazması, dosya taşıma veya silme yapılmadı. Aşağıdaki 20 Eylül sayıları tarihseldir; yeni aramada güncel aktarım kapsamı esas alınacak.

20 Eylül 2026 tarihinde başarıyla aktarılmış 2.439 sözleşme için staging dosya
alanları yeniden sınıflandırıldı. İlk plan yalnız null kontrolü yaptığı için iki
alanı da boş metin olan 809 kaydı metadata var gibi saymıştı. Düzeltilmiş sonuç:

- 1.584 sözleşmede dolu dosya adı/yolu bulunuyor.
- 809 sözleşmede dosya adı ve yolu boş; bunlar dosya adayı değildir.
- 1.572 dosya adayı PDF/JPG/JPEG uzantısıyla mevcut upload politikasına uygundur.
- 12 kayıt `BOŞ.txt` olduğu için desteklenmeyen uzantıdır ve sözleşme dosyası
  olarak CDN'e yüklenmeyecektir.
- Dolu metadata 1.096 benzersiz legacy yolu temsil eder. 459 yol birden fazla
  sözleşmede kullanılmış, bu gruplarda toplam 947 sözleşme satırı vardır.
- Yollar yapısal olarak `~/Content/Contract/...` biçimine uygundur. Dosya
  adlarında geçen çift nokta karakterleri üst dizin geçişi sayılmaz.

Kaynak kod ZIP'i ve mevcut çalışma klasörlerinde fiziksel `Content/Contract`
deposu bulunmadı. Bu nedenle dosya varlığı/hash/MIME/imza kontrolü ve CDN
yüklemesi yapılmadı. Fiziksel kaynak kökü sağlanınca önce benzersiz 1.096 yol
okunacak, uzantıya değil dosya imzasına göre doğrulanacak, aynı fiziksel nesne
gereksiz yere tekrar yüklenmeden ilgili sözleşmelere metadata bağlanacaktır.
