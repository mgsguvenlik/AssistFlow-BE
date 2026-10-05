# MGS Sheets geliştirme planı

## Kabul edilen kapsam
- Workbook başına 10 worksheet; worksheet başına 100 kolon ve 1.000.000 satır.
- A/B/C hücre alanı, birden fazla worksheet, açık Kaydet, bütün workbook için son başarılı kayıt kazanır.
- Sahip veya yönetici paylaşır/siler. Yönetici bütün workbook'ları görür/düzenler.
- Görünür workbook + SheetsList Edit içerik düzenleme/indirme hakkı verir; tablo paylaşımı görünürlüğü belirler.
- Excel: yeni workbook veya mevcut draft'a worksheet ekleme. XLSX; veri, ad/sıra, temel biçimler.
- Formül hesaplama, grafik, pivot, makro ve birleşik hücreler bu sürümün kapsamı dışında.
- Fiziksel silme; mevcut kullanıcı/rol/menü altyapısı. Rol adına bağlı kontrol yok.

## İş parçaları ve kabul ölçütleri
1. Veri modeli, sınırlar ve yetkilendirme: sheets şeması, migration, menü seed; görünmeyen workbook'a hiçbir API erişemez.
2. İçerik ve kayıt: değişmez sürümler, seyrek hücre blokları, görünür satır penceresi, yapı değişiklikleri ve işlem kaydı. Eski açılış sürümünden kayıt yeni kaydı bütün workbook kapsamında değiştirir.
3. Excel: akışla okuma/yazma, temel biçimler, boyut doğrulama, yetkili indirme; içe aktarma Kaydet'e kadar aktif sürümü değiştirmez.
4. Frontend: mevcut ortak bileşenlerle server-side liste/filtreler, oluşturma, editör, paylaşım, worksheet yönetimi, RevoGrid, Kaydet.
5. SignalR: yetkili katılım, aktif kullanıcı/hücre ve kayıt bildirimi; başka kullanıcının draft'ını otomatik değiştirmez.
6. Doğrulama: backend build, bağımsız modül testleri, FE build/typecheck/lint; gerçek SQL Server ve tarayıcı ile izin/Excel/eşzamanlılık/ölçek senaryoları.

## Saklama ve yükleme
Revision, worksheet metadata ve blok referanslarını içerir. Hücreler 128 fiziksel satırın adreslendiği, en fazla 512 KiB JSON içeren değişmez parçalarda saklanır. Boş hücreler oluşturulmaz; boş hücrenin satır/sütun biçimi görünür pencere için üretilir. Satır/kolon ekleme ve silme, aralık eşlemelerini değiştirir; mevcut büyük veri yeniden numaralanmaz. Kaydet açılış revision'ını temel alır ve değişen parçaları kopyalar. Paylaşım revision'dan ayrıdır. Eski revision'lar açık editörler için korunur; workbook silinince bütün revision ve bloklar fiziksel silinir.

## Uygulama durumu

1. **Tamamlandı:** `sheets` şemasında Workbooks, Revisions, DataBlocks, Access ve Activities tabloları; EF migration; menü seed ve sunucuda aktif kullanıcı/menü/sahiplik/paylaşım kontrolleri.
2. **Tamamlandı:** değişmez sürümler, seyrek saklama, sabit fiziksel hücre adresleri, yapı işlemleri, kayıt denetimi ve SQL transaction ile workbook başına sıralı yayınlama.
3. **Tamamlandı:** Open XML ile XLSX okuma/yazma, temel hücre ve satır/sütun biçimleri, yeni tabloya yükleme ve açık taslağa worksheet ekleme.
4. **Tamamlandı:** server-side liste/filtre/sıralama, oluşturma, RevoGrid editörü, toplu kopyala/yapıştır, çoklu worksheet, paylaşım ve fiziksel silme.
5. **Tamamlandı:** SignalR katılım/odak/kayıt bildirimi, yeniden bağlantı ve erişimi kaldırılan kullanıcının gruptan çıkarılması.
6. **Yerel doğrulama tamamlandı:** SQL Server LocalDB, HTTP/SignalR, XLSX doğrulaması ve tarayıcı akışları. Gerçek uygulama veritabanına deployment yapılmadı. Azami dolu veri için yük testi ayrı teslimat kapısıdır.

Teknoloji: RevoGrid Core/React 4.28.0 (MIT), ASP.NET Core SignalR 9, DocumentFormat.OpenXml 3.1.1 (MIT); mevcut .NET 9/EF Core/SQL Server katmanları korunur. Ücretli grid eklentisi kullanılmaz.

## İşletim ve doğrulama notları
10 x 100 x 1.000.000 teorik sınır, 1 milyar dolu hücreyi ölçmeden performans garantisi anlamına gelmez. Yoğun XLSX aktarımı, disk/DB ve uzun süreli HTTP sınırları gerçek ortamda ölçülmelidir. Revision birikimi ayrıca izlenmelidir. Menü seed rol oluşturmaz ve izin atamaz: SheetsList, SheetsCreate ve SheetsManage mevcut rol yönetiminden atanır.

Kurulum, doğrulama komutları, ölçülen senaryolar ve kalan işletim çalışmaları [MGS-Sheets-verification.md](MGS-Sheets-verification.md) dosyasındadır.
