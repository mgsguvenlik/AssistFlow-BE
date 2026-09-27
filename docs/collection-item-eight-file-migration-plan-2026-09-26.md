# Madde 8 — Sözleşme dosyalarını eşleştirme ve CDN aktarım planı

## Amaç ve sınır

**Uygulama sonucu:** Kullanıcı onayıyla **1.830 sözleşme–dosya bağlantısının tamamı** mevcut test CDN'e yüklenip AssistFlowTest'e bağlandı. Güvenli çıkarma/CRC, SHA-256 ve mevcut imza/boyut politikası doğrulaması tamamlandı; Windows Defender özel taraması (remediation kapalı) tehdit bulmadı. 1.736 geçerli, 28 boş, 5 desteklenmeyen dosya ayrıldı. Canlı MGS dosya/müşteri referansı mutabakatıyla 1.830 hedef ilişki doğrulandı. Önce 5 dosya, ardından kalan 1.825 dosya uygulandı. Her nesne CDN'den geri okunarak SHA-256/boyut/tür eşitliği doğrulandı; ilk 5 dosyanın public erişimi ayrıca HTTP 200 ve hash ile denetlendi.

Son SQL mutabakatında **1.830 dosya / 1.830 sözleşme / 1.830 benzersiz CDN anahtarı**, toplam **4.244.155.574 bayt**; eksik ilişki **0**, alan farkı **0**, plan dışı ilişki **0**, silinmiş attachment **0**. Kaynakta 1.119 fiziksel yol, hedefte mevcut unique-index kuralı nedeniyle 1.830 ayrı nesne vardır. Toplam sözleşme **4.479**, tarife **17.403**, ödeme **0**, ham kaynak **63.854** sabit kaldı. İşlem günlüğü ve SQL kanıtları Git dışında korunur.

Tekrar uygulama aynı plan hash'iyle **mevcut 1.830 / yeni 0 / uygulanan 0** sonucunu verdi; mükerrer kayıt/yükleme oluşmadı. Mevcut `CollectionContractAttachmentService` liste sonucunda doğru ad, boyut ve URL doğrulandı. İşlem günlüğünde 1.830 Attached ve 1.830 CdnVerified, benzersiz anahtar 1.830; bütün plan satırlarında hash kanıtı mevcut. Kaynak RAR SHA-256 yeniden kontrol edildi ve değişmedi. Son importer build 0 hata; bağımlı projelerin mevcut 759 uyarısı var, yeni `CollectionAttachmentTransfer.cs` için derleyici uyarısı yok. FE değişmediğinden yeni frontend build/test altyapısı eklenmedi.

Son doğrulama dosyaları: `.tools/item-eight-final.json`, `.tools/item-eight-build-final.log`, `.tools/item-eight-transfer/service-list-check.json`, `.tools/item-eight-transfer/journal.ndjson`. Yerel çıkarılmış kaynak çalışma kopyası tekrar çalıştırma ve sonradan aktarılacak sözleşmeler için Git dışında tutulur; otomatik temizlik/silme yapılmadı.

Aktarılmış sözleşmelerin 74'ünde (47 benzersiz yol) dosya arşivde yok, 19'unda dosya sıfır bayt; bunlar **93 kayıtlık raporda** ayrı gösterildi. 2.554 metadata'sız sözleşmeye dosya tahmin edilmedi. Bekleyen/kapsam dışı sözleşmeler ve 310 referansı bulunamayan arşiv dosyası yüklenmedi, silinmedi. Bu sonuç yalnız mevcut aktarılmış sözleşmelerin doğrulanmış dosya kapsamını tamamlar; sonraki müşteri kararlarıyla aktarılacak sözleşmeler ayrıca ele alınır.

Kullanıcının sağladığı `Contract.rar` içindeki gerçek dosyaları legacy sözleşme referansları üzerinden AssistFlow sözleşmelerine bağlamak; doğrulanmış dosyaları **mevcut CDN altyapısını değiştirmeden** taşımak. Önce plan ve salt-okunur envanter hazırlanmıştır; bu belgede planlanan yükleme/DB işlemleri henüz uygulanmamıştır.

- İlk hedef AssistFlowTest; MGS/production ve kaynak arşiv değiştirilmeyecek.
- Yalnız hedefe aktarılmış ve mevcut sözleşmelere dosya bağlanacak. Bekleyen sözleşmeler ayrıca kuyruğa ayrılacak; madde 4 veya diğer kararlarla dışlanan abonelikler dosya üzerinden yeniden açılmayacak.
- Arşivdeki tüm dosyalar otomatik yüklenmez. İlişkisi kanıtlanamayan, boş, bozuk veya mevcut politikaya uymayan dosyalar raporlanır.
- Eksik dosya sözleşmeyi engellemez; mevcut müşteri kararına göre sözleşme dosyasız kalır. Kaynak dosyalar silinmez. Ödeme aktarımı bu işin kapsamı değildir.

## Doğrulanmış ilk envanter

Kaynak: `C:/Users/Mehmet Zeki KARA/Desktop/Contract.rar`.
SHA-256: `A35F009E7BEAF858DAFB510BC98E5518F37065F305A69D90CDF0D7A97A0EDA36`.

- RAR5, tek parça, şifresiz; arşiv boyutu 3.741.351.047 bayt, listelenen açılmış içerik 4.055.895.588 bayt.
- **1.769 dosya / 1.504 klasör**. Dosyalar `Contract/...` altında.
- Türler: 1.730 PDF, 4 JPG, 1 JPEG, 1 PNG, 29 TXT, 2 XLS, 1 ODT, 1 TIF.
- Listeye göre **28 sıfır bayt dosya**, 20 MiB sınırını aşan dosya 0; tekrarlı tam yol, şifreli dosya ve ön kontrolde tehlikeli yol/link/alternatif akış girdisi 0.
- Bu sonuç arşivin tüm içeriğinin CRC, imza veya zararlı içerik taramasından geçtiği anlamına gelmez. Henüz dosyalar çıkarılmadı.

26 Eylül güncel test aktarım eşlemelerine göre:

| Aktarılmış sözleşmelerde durum | Sözleşme/dosya bağlantısı |
| --- | ---: |
| Yol eşleşti; uzantı ve liste boyutu uygun, içerik doğrulaması bekliyor | 1.830 |
| Kayıtlı yol arşivde yok | 74 |
| Yol eşleşti fakat dosya sıfır bayt | 19 |
| Dosya yolu metadata'sı yok | 2.554 |
| Toplam aktarılmış sözleşme | 4.477 |

1.830 bağlantı **1.119 benzersiz fiziksel yola** karşılık geliyor; 477 yol birden fazla hedef sözleşmede kullanılıyor. Bulunamayan 74 bağlantı 47 benzersiz yol; aynı tam dosya adı arşivin başka klasöründe de bulunamadı. Bu, sunucuda veya başka yedekte hiç bulunamayacağı anlamına gelmez. Hedefte mevcut dosya metadata kaydı 0.

Tüm 9.498 staging sözleşmesine göre arşivdeki **310 dosyanın kayıtlı tam yolla referansı bulunamadı**. Bunlar otomatik sahipsiz/silinebilir sayılmaz: farklı kesit, metadata eksikliği veya başka ilişki ihtimali ayrı incelenir. Bekleyen sözleşmelerde 728, dışlanmış sözleşmelerde 566 uygun uzantılı dolu yol eşleşmesi vardır; bu sayılar ilk yükleme kapsamına eklenmez.

## Sıralı görevler ve kabul koşulları

### 1. Kaynağı güvenli aç ve içeriği doğrula

- Arşiv hash'ini sabitle; yeni, Git dışında ve erişimi yerel çalışma alanıyla sınırlı klasöre çıkar. Hedef dışına çıkan yollar, linkler, alternatif veri akışları ve var olan dosya üzerine yazma reddedilir.
- Yeterli disk alanını kontrol et; arşiv bütünlük/CRC kontrolünü yap. Dosyaları çalıştırma; mevcut zararlı yazılım kontrolünü kullan, taranamayanı ayrıca belirt.
- Her dosyada gerçek boyut, SHA-256 ve dosya imzasını çıkar. PDF/PNG/JPG/JPEG ve en fazla 20 MiB kuralını koru; uzantıya bakıp içerik geçerli varsayma. Boş/TXT/XLS/ODT/TIF için otomatik dönüştürme veya politika genişletmesi yok.
- **Kabul:** Tam envanter + içerik doğrulama sonuçları; kaynak arşiv değişmemiş; geçersiz dosya yükleme kuyruğunda yok.

### 2. Sözleşme ilişkilerini kesinleştir

- `FileAttachmentPath` → arşivdeki tam göreli yol → legacy ContractID → mevcut MigrationMap.TargetContractId zincirini kullan.
- Yalnız `~/Content/` kök eşlemesi, ayraç normalizasyonu ve yinelenen `/` temizliği yapılır. Türkçe harfler, GUID ve alt klasörler korunur. Klasör adı sözleşme ID'si veya müşteri adı tek başına eşleşme kanıtı kabul edilmez.
- Legacy canlı metadata'sını kesitle salt-okunur karşılaştır; değişmiş bağlantıyı otomatik kullanma. Hedefin hâlâ mevcut ve aktarım kapsamında olduğunu kontrol et.
- İlişkisiz veya farklı adlı dosyalar için olası eşleşmeleri raporla; benzer isimle otomatik bağlama yok.
- **Kabul:** Her aday için arşiv hash'i, dosya hash'i, kaynak yol, legacy sözleşme ve hedef kimlik kanıtı; çelişkili bağlantı 0.

### 3. Tekrar çalıştırılabilir aktarım önizlemesi hazırla

- Mevcut `IFileStorage` / `R2FileStorage.UploadAsync`, `ExistsAsync` ve `GetPublicUrl` kullanılır; yeni CDN servisi, bucket veya ortak altyapı değişikliği yok.
- Mevcut `UX_ContractAttachment_StoredFileName` tekillik kuralı **aynı CDN anahtarını birden fazla sözleşmeye bağlamaya izin vermiyor**. Bu yüzden mevcut şemayı koruyan ilk çözüm: her sözleşme–dosya ilişkisi için ayrı, kararlı ve çakışmasız dosya anahtarı. Aynı kaynak dosya farklı sözleşmelerde ayrı CDN nesnesi olabilir; aynı ilişkinin retry'ında yeni nesne üretilmez. İlk 1.119 fiziksel dosya en çok 1.830 ilişki/nesne adayıdır; içerik kontrolüyle azalabilir.
- Ortak fiziksel nesne/çoklu ilişki modeli bu aktarımın önkoşulu yapılmaz; mevcut unique index kaldırılmaz. Önceki “bir kez yükle, çok sözleşmeye bağla” niyeti mevcut şema nedeniyle bu planda uygulanmaz.
- CDN ortam ayarlarını ve mevcut erişim modelini gizli değerleri göstermeden doğrula; yanlış ortam/bucket veya beklenmeyen dış erişim bulunursa yüklemeyi durdur. Sözleşme belgeleri hassas olduğundan yeni paylaşım/erişim politikası açılmaz.
- İşlem günlüğü yükleme öncesinde ilişki, hash ve seçilen anahtarı kaydeder. Anahtar mevcutsa yalnız varlık kontrolü yeterli sayılmaz; boyut/içerik mutabakatı gerekir. DB/CDN sonucu belirsiz işlemler yeniden doğrulanmadan tekrarlanmaz veya silinmez.
- **Kabul:** Plan hash'i, nesne/ilişki adetleri, toplam aktarım boyutu, kapsam ve istisna listesi belli; DB/CDN yazması yok.

### 4. Küçük bir grupla doğrula, ardından kontrollü aktar

- Doğrulanmış önizleme üzerinden önce az sayıda PDF/görsel ve ortak kaynaklı bağlantı dene; mevcut sözleşme detay ekranında doğru kayda ait dosyanın açıldığını doğrula.
- Sınırlı paralellik ve akış tabanlı yükleme kullan; arşivi/dosyaları topluca belleğe alma. Dosya içeriğini veya erişim bilgilerini loglama.
- Önce CDN nesnesi yüklenir/doğrulanır, ardından mevcut `collection.ContractAttachment` ilişkisi yazılır. Mevcut legacy importer'daki sistem audit yaklaşımı korunur: `CreatedUser=0`; gerçek bir kullanıcının hesabı taklit edilmez veya yeni kullanıcı yaratılmaz. API üzerinden manuel yüklemedeki geçerli kullanıcı kontrolü değiştirilmez. Uzun ağ yüklemesi boyunca DB transaction açık tutulmaz.
- Her başarılı ilişkiyi kaydet; kesintiden sonra tamamlananları atla. DB commit belirsizliğinde nesneyi otomatik silme; doğrula ve kaldığı yerden sürdür.
- **Kabul:** Başarılı ilişki başına doğru hedef kayıt ve erişilebilir doğru içerik; yanlış sözleşmeye bağlanan veya tekrar yüklenen ilişki 0.

### 5. Son mutabakat ve eksik dosya raporu

- Planlanan/yüklenen/bağlanan/bekleyen sayıları, dosya boyutu ve hash doğrulamaları karşılaştırılır. Tekrar önizleme tamamlanan kayıtlarda 0 yeni işlem vermelidir.
- Eksik yol, sıfır bayt, geçersiz tür/imza, kaynaksız dosya, bekleyen sözleşme ve kapsam dışı sözleşme ayrı listelenir. Eksik dosya nedeniyle sözleşme silinmez veya iptal edilmez.
- Mevcut dosya listeleme/görüntüleme akışı kısa manuel kontrolden geçirilir; gereksiz yeni ekran veya test altyapısı kurulmaz. Kod değişirse ilgili build çalıştırılır.
- **Kabul:** Kullanıcıya net aktarım özeti + kayıt bazında istisna raporu; kaynak/production/ortak tablolar korunmuş, iş planı güncel.

## Güncel durum / kanıt

Tamamlandı: arşiv listesi ve hash, test DB salt-okunur metadata envanteri, normalize tam yolla ilk eşleştirme, mevcut CDN/ilişki kısıtlarının incelenmesi, bu plan.

İlk plan hazırlanırken çıkarma/doğrulama/yükleme yapılmamıştı. Kullanıcı onayı sonrası bunlar tamamlandı; güncel sonuç üstteki uygulama bölümündedir. Yeni CDN altyapısı, şema, controller veya ekran geliştirmesi yapılmadı.

Git dışındaki çalışma kanıtları: `.tools/item-eight-archive.json`, `.tools/item-eight-inventory.json`, `.tools/item-eight-match.json`; yerel okuma araçları `.tools/Inspect-CollectionArchive.ps1` ve `.tools/Match-CollectionArchive.ps1`. Müşteri/dosya adlarını içeren bu listeler kaynak kontrolüne eklenmez.

## Tekrar çalıştırma ve operasyon notu

`tools/Collections.Import/CollectionAttachmentTransfer.cs` mevcut Business projesini referans alır; CDN servisini kopyalamaz/değiştirmez. API controller/DI/FE değişikliği ve DB migration yoktur. Anahtarlar `uploads-test/mgs-test-{ilişki-ve-içerik-hash}.{uzantı}` altında; production prefix'i reddedilir.

Önizleme:

```powershell
dotnet tools/Collections.Import/bin/Debug/net9.0/Collections.Import.dll transfer-attachments WebAPI/appsettings.Development.json .tools/item-eight-verified.json
```

Uygulama aynı komuta plan SHA-256 ve pozitif adet sınırı eklenerek yapılır. Bu kesitin onaylı planı: `4D633B3EC0A1800FCA0F5C3B2550EF332C191D5AC3966004111C92CF89A58A24`. Uygulayıcı sabit test sunucusu/DB, kesit, arşiv hash'i, yerel dosya hash'i, MGS salt-okunur referansı ve hedef rowversion'larını denetler. Mevcut ortak aktarım kilidi session seviyesinde alınır; dosya yüklerken uzun DB transaction açılmaz. En fazla 4 CDN yükleme/doğrulama paralel, EF işlemleri seri ve kısa Serializable transaction'lardır.

Yerel `.tools/item-eight-transfer/journal.ndjson` her ilişki için Started → CdnVerified → Attached adımlarını diske flush ederek kaydeder. Retry, mevcut anahtarlı nesneyi silmez/üzerine yazmaz; tekrar okuyup SHA-256/Content-Type/boyut doğrular ve eksik DB bağını tamamlar. Mevcut hedef satır varsa alan uyuşmazlığında durur; aynı ilişkiyi çoğaltmaz. Sonuç belirsizliğinde hedef/CDN dosyası körlemesine silinmez.

Önizleme planı ve tüm istisnalar `.tools/item-eight-transfer/plan-4D633B3EC0A1800FCA0F5C3B2550EF332C191D5AC3966004111C92CF89A58A24.json`; kullanıcıya uygun 93 kayıtlık eksik/boş dosya listesi `.tools/item-eight-transfer/eksik-bos-dosyalar.md`. Her yükleme CDN'den geri okunarak kaynak SHA-256 ile karşılaştırılır. Kullanıcının açtığı oturumda hedef 2349 dosyası detay ekranında listelendi ve linkten CDN görseli (1200×1600) açıldı; yeni UI kodu eklenmedi.
