# Tahsilat — hedef kilitli veri provası

Bu araç yalnız `sql2022 / AssistFlow_Performance_Restore` için hazırlanmıştır.
17 Ekim adaptör çalışmasında finansal metotlar ortak
`../Collections.Transfer/CollectionTransferCore.cs` dosyasına ayrıldı. Bu aracın
hedef, capture ve REHEARSAL_ONLY kilitleri korunur. Eski raporlardaki kaynak/DLL
hash'leri tarihsel derlemeye aittir; yeni canlı adaptörünün yazmalı provası ayrıca
gerekir. Yeni araç: [Collections.Production](../Collections.Production/README.md).
Canlıya uygulama komutu yoktur. Config içindeki AssistFlow bağlantısı yalnız
sunucu/kimlik bilgisi için okunur; bağlantı açılmadan DB adı sabit prova hedefine
çevrilir. WebAPI, genel seed, migration, worker, SMS veya CDN başlatılmaz.

Girdiler `.tools` altındaki hash doğrulanmış prova capture, finansal inceleme,
ödeme mutabakatı ve ilişki paketleridir. İncelemede sorunlu bulunan kayıtlar
otomatik kabul edilmez. Yeni üst kart ve müşteri tipi değişiklikleri yalnız
atılabilir prova kopyasında uygulanır; canlı için onay anlamına gelmez.

## Sıra

`dotnet build tools/Collections.Rehearsal/Collections.Rehearsal.csproj`

Derlenen DLL şu komutları destekler:

1. `plan <config> <capture> <review> <payments> <relations> <yeni çıktı>`:
   yalnız okur; boş collection hedefini ve müşteri/tanım parmak izlerini kontrol
   edip plan SHA-256 değerini verir.
2. `rollback-check <config> <plan klasörü> <plan SHA> <yeni çıktı> REHEARSAL_ONLY`:
   ilk aktarım öncesi çalışır; ilk 2.000 ödeme, kaynak audit ve makbuz yazımından
   sonra kontrollü hata üretir. Aynı transaction içindeki bütün değişiklikleri
   geri alır; öncesi/sonrası satır hash'leri eşit olmalıdır. SQL identity/rowversion
   sayaçları geri sarılmaz; kimlik boşlukları veri kaybı değildir.
3. `apply <config> <plan klasörü> <plan SHA> <yeni çıktı> REHEARSAL_ONLY`:
   tek transaction içinde aktarır; kaynak, sahiplik, tarife, para birimi, tutar,
   audit ve makbuz mutabakatı geçmeden commit etmez. Aynı batch tekrarında yalnız
   doğrular, yeni satır yazmaz. Kalıcı ödeme kimliği mevcut uygulamadaki helper'dır.
   Bu kapalı prova batch'inin tam sayımları denetlenir; plan dışı yeni finansal
   kayıtlar bulunan çalışan bir sisteme genel amaçlı delta importer değildir.
4. `resilience-check <config> <plan klasörü> <plan SHA> <yeni çıktı> REHEARSAL_ONLY`:
   commit sonrası dört ayrı geri alınan transaction ile silinen/düzenlenen ödeme,
   bozuk kaynak hash'i ve eksik makbuz senaryolarını aynı replay doğrulama yolunda
   sınar. Silinen kayıt yaratılmamalı; düzenlenen ezilmemeli; bozuk audit/makbuz
   reddedilmelidir. Son veri parmak izi başlangıçla aynı olmalıdır.

Her çıktı klasörü yeni olmalıdır. Hatalı deneme kanıtlarının üstüne yazılmaz.
Kişisel veri, kaynak JSON, makbuz ve ayrıntılı raporlar Git dışındaki `.tools`
altında tutulur. Teknik aktör 0'dır; eski kişi etiketleri ve yerel tarihler ham
audit içinde korunur, tarih dönüşümü açık Europe/Istanbul kuralıyla yapılır.

Uzun legacy tarife açıklamaları 100 karakterlik `ChangeReason` alanına kesilerek
yazılmaz: kısa aktarım etiketi kullanılır, tam metin `ContractHistory` kaynak
audit satırında korunur. Şema değiştirilmez.

Dosyalar yalnız doğrulanmış yerel hash/ilişki kanıtı olarak audit'e alınır;
CDN'ye yüklenmez ve sahte attachment oluşturulmaz. Gerçek CDN aktarımı, uygulama
açılışı, yetki/ekran kabulü, son bakım kesiti ve production geçiş onayı ayrı
kapılardır. Bu prova ağ kesintisinde belirsiz commit veya import sonrası tam
backup/restore geri dönüşünün ayrıca doğrulandığı anlamına gelmez.
