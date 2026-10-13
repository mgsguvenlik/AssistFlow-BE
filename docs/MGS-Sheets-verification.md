# MGS Sheets — kurulum ve doğrulama

## İlk sürümün akışı

Tablolarım ekranı yalnızca kullanıcıya görünür tabloları getirir; yönetim izni bütün tabloları kapsar. Ad, sahip adı, güncelleme tarihi ve kendi tabloları ile filtreleme, sıralama ve sunucuda sayfalama vardır. Yeni tablo boş oluşturulabilir veya XLSX yüklenebilir. Editör A/B/C hücreleri, 10 worksheet, worksheet adı/sırası, satır/sütun ekleme-silme, boyutlandırma, temel biçimler ve toplu yapıştırma sunar.

Paylaşım görünürlük sağlar. Görünür tabloyu düzenleme ve indirme için SheetsList Edit gerekir; SheetsManage Edit yöneticiyi de yetkilendirir. Sahip kendi tablosunu paylaşır/siler; SheetsManage Edit bütün tabloları yönetir. Rol adları kullanılmaz. Paylaşılabilir kullanıcılar mevcut aktif kullanıcı/menü ilişkilerinden sorgulanır. API kontrolleri arayüzden bağımsızdır.

Kaydet, kullanıcının açtığı workbook sürümünün tümünü yayınlar. Başka kullanıcının sonradan yaptığı değişiklikler, kullanıcının yüklemediği satırlarda dahi önceki açılış sürümüyle değiştirilir. Bunun için client bütün veriyi göndermez: açılış revision kimliği, yapı işlemleri ve değişen hücreler yeterlidir. SQL transaction/UPDLOCK/HOLDLOCK yayınları sıralar. Diğer kullanıcıya kayıt bildirimi gelir; açık taslağı otomatik değiştirilmez. Aktif kullanıcı ve odak bilgisi SignalR ile gösterilir.

XLSX içe aktarma mevcut taslağa worksheet ekler, aktif sürümü ancak Kaydet yayınlar. İndirme kaydedilmiş bütün workbook'u verir. Değerler, worksheet adları/sırası, temel yazı/dolgu/hizalama/sayı-tarih biçimleri ve satır/sütun boyutları korunur. Formül ifadeleri yerine varsa önbellek değerleri alınır; hesaplama, grafik, pivot, makro, birleşik hücre, kenarlık ve koşullu biçim desteği yoktur. Tarayıcı sayı gösterimi temel formatların bir alt kümesini uygular; saklanan Excel format kodu dışa aktarımda korunur.

## Kurulum

1. Backend bağımlılıklarını restore edip `dotnet build AssistFlow-BE.sln` çalıştırın. `AddMgsSheets` migration yalnızca beş `sheets` tablosunu oluşturur. Uygulamanın mevcut startup migration/seed mekanizması migration'ı uygular; bu geliştirmede gerçek uygulama DB'si çalıştırılmadı/değiştirilmedi.
2. Frontend'de `npm ci`, ardından `npm run build` çalıştırın. RevoGrid ve SignalR bağımlılıkları lock dosyasına sabitlenmiştir.
3. Mevcut rol yönetiminde `SheetsList`, `SheetsCreate`, `SheetsManage` menülerini atayın. Listeye giriş için View, içerik/indirme için List Edit, oluşturma için Create View+Edit gerekir. Yönetici için List View, Create View ve Manage View+Edit ataması menülerin görünmesini ve yönetim işlemlerini sağlar. Seed yeni rol veya kullanıcı izni oluşturmaz.
4. API proxy üzerinde `/api/sheets-hub` WebSocket/long-polling erişimini, mevcut kimlik doğrulamayı ve gerekiyorsa sticky sessions ayarını doğrulayın. Presence tek API sürecinin belleğindedir; birden fazla API instance için backplane ve ortak presence tasarımı ayrıca gereklidir.
5. Upload sınırı 512 MiB'dir. Reverse proxy/IIS ve geçici dosya dizininin disk kapasitesi/uzun istek sürelerini ortama göre ayarlayın. Export sunucuda geçici XLSX üretir, yetkiyi yeniden denetler ve dosyayı response kapandığında siler. Aktarım işlemleri bu sürümde HTTP isteği içinde yürür; background job yoktur.

Repository/UoW ve Sheets transaction aynı scoped AppDataContext'i kullanır. Program.cs'deki repository kaydı ikinci context oluşturmak yerine mevcut scoped context'i alacak şekilde düzeltildi. Bu ortak kayıt değişikliği mevcut servislerin de aynı context'i paylaşmasını sağlar. Button bileşenine gerçek HTML disabled özelliği eklendi; klavye ve erişilebilirlik davranışı görsel durumla eşleşir.

## Doğrulama

2026-10-05 tarihinde yerel ortamda:

- Backend solution build başarılı; mevcut projelerden gelen uyarılar sürüyor.
- `dotnet run --project tools/Sheets.Tests/Sheets.Tests.csproj -- --sql`: 55 senaryo grubu başarılı. Son koşu, açık Visual Studio/IIS Express oturumunun Debug DLL'lerini kilitlemesi nedeniyle `--configuration Release` ile yapıldı. Test runner yalnızca `MgsSheetsTests_<GUID>` adlı kendi geçici LocalDB veritabanını oluşturur ve finally bloğunda siler. Gerçek migration SQL'i, aktif kullanıcı/menü/ACL, server-side filtreler, son satır/kolon penceresi, açılış sürümünün tamamının üzerine yazılması, gerçek eşzamanlı kayıt, parça boyutları, değişmez sürümler ve fiziksel cascade silme doğrulandı.
- Gerçek localhost HTTP sunucusunda JWT ile 401/403/404 kontrolleri, multipart XLSX oluşturma/ekleme, korunan dosya indirme, SignalR handshake/Join ve erişim kaldırma doğrulandı.
- OpenXmlValidator ile dışa aktarılan dosyalar doğrulandı. Türkçe metin, literal `=`, sayılar, tarih, worksheet sırası, renk/yazı/hizalama, satır yüksekliği ve boş hücrelere miras kalan satır/sütun biçimleri roundtrip edildi.
- Frontend production build başarılı. Yeni Sheets dosyaları, servis, route, Button ve yerel fixture üzerinde hedefli ESLint temiz. Proje genelinde `npm run lint` ve `npx tsc --noEmit`, mevcut başka modül hataları nedeniyle temiz değil; yeni Sheets dosyalarında typecheck hatası yok. Ayrıntılı yerel çıktılar `sheets-lint-all.log` ve `sheets-types.log` dosyalarındadır (Git'e alınmaz).
- Yerel tarayıcı fixture'ında React 19/RevoGrid hücre düzenleme, 2x2 toplu yapıştırma, açık Kaydet, worksheet ekleme ve milyonuncu satıra gitme doğrulandı. Görüntüleyen kullanıcıda düzenleme/indirme/paylaşım kontrolleri yok; grid hücresi düzenlemeye açılmıyor. Bu fixture uygulama API'sini mock eder; gerçek kullanıcı oturumuyla uçtan uca uygulama kabulü yerine geçmez.

Frontend tarayıcı fixture'ı: `npm run dev -- --host 127.0.0.1 --port 5197`, ardından `/tools/sheets-browser.html`. Örnek isteklerin tamamı mock edilir, gerçek veritabanına veya kullanıcı verilerine erişmez. Görsel: [MGS-Sheets-preview.jpg](MGS-Sheets-preview.jpg).

## Ölçek sınırları ve kalan teslimat kapıları

- Workbook 10 worksheet; worksheet 100 kolon/1.000.000 satır. Satır penceresi 1–256; 32 MiB response sınırında pencere küçültülmelidir. Browser bütün workbook'u belleğe almaz.
- Kaydet başına en fazla 50.000 hücre değişikliği ve 2.000 yapı işlemi. Büyük aktarımlar XLSX yolunu kullanır. Hücre metni Excel sınırı olan 32.767 karakterle sınırlıdır; API/proxy request sınırları ayrıca geçerlidir.
- Milyon satır boyutu ve son koordinat seyrek veriyle doğrulandı; 1 milyar **dolu** hücre, 512 MiB XLSX, çok sayıda eşzamanlı kullanıcı ve milyon satırın tümünde farklı biçimler için kaynak/süre ölçümü yapılmadı. Tam kapasiteyi production garantisi olarak değerlendirmeyin.
- Revision ve eski değişmez parçalar açık oturumlar için saklanır; otomatik retention/GC yoktur. Workbook silinince tamamı fiziksel silinir. DB alanı ve eski sürüm birikimi izlenmelidir.
- Production rollout öncesi gerçek login/rol yönetimi ile menü ve ekran kabulü, temsilî yoğun dosyalarla bellek/disk/SQL/süre yük testi ve kullanılan reverse proxy/topoloji doğrulaması gerekir.

## Oluşturma sonrası iptal/boş editör düzeltmesi

Tablo oluşturulduktan sonraki okuma sırasında iptal edilen isteğin Axios hata yakalayıcısında oturumu temizlediği görüldü. `ERR_CANCELED` artık oturum temizleme ve genel hata loglama akışından çıkarılır; hata, isteği yapan bileşene iptal olarak iletilir. Gerçek yetkisiz HTTP yanıtlarının mevcut davranışı korunur. React StrictMode veya üst üste yükleme sonucu eski metadata yanıtı yeni editör durumunu değiştirmez. Grid ilk satır penceresi hazır olduğunda bağlanır.

Geliştirme sunucusunda RevoGrid'in `InvalidCharacterError` ile boş kaldığı da yeniden üretildi. Vite dependency pre-bundling dışında tutulan React wrapper/core, Stencil loader ve lazy bileşenlerin aynı module runtime üzerinden yüklenmesini sağlar. Repodaki hem `vite.config.js` hem `vite.config.ts` güncellendi. Exclude ayarı geliştirme sunucusu içindir; production build ayrıca başarılıdır.

Yerel StrictMode fixture'ında geciktirilmiş metadata/satır yanıtlarıyla ilk açılış **1 pencere isteği / 0 iptal**, iptalden sonra token+oturum korunması, görünür hücre, düzenleme ve Kaydet doğrulandı. HTTP testinde boş tablo oluşturma → sürümü açma → satır penceresi yükleme eklendi. Değişen frontend dosyalarında ESLint temiz; ilgili dosyalarda TypeScript hatası yok, proje genelindeki mevcut hatalar devam ediyor. Görsel: [MGS-Sheets-cancellation-preview.jpg](MGS-Sheets-cancellation-preview.jpg).

Sunucu istemcinin kapattığı isteklerde CancellationToken'ı kullanmaya devam eder; middleware bunu normal istemci iptali olarak loglar. Visual Studio tüm thrown TaskCanceledException'larda duracak şekilde ayarlıysa satır aralığını değiştirirken meşru iptallerde de durabilir; bu tek başına kaydın başarısız olduğu anlamına gelmez. Debug oturumunu yeniden başlatıp frontend'i yeniledikten sonra oluşturulmuş tablo Tablolarım üzerinden tekrar açılabilir; yeniden oluşturmak gerekmez.

## Türkçe arayüz ve araç şeridi

Referans görsele göre tam genişlikte çalışma alanı, Giriş/Ekle/Veri/Görünüm araç sekmeleri, yazı ve dolgu renk paletleri, hizalama düğmeleri, hücre değeri alanı ve tablonun altında sayfa sekmeleri/satır gezinmesi uygulandı. Filtre koşulları, operatörleri, yer tutucuları ve düğme metinleri kütüphanenin yerelleştirme API'siyle Türkçe gösterilir. Oluşturma, liste, paylaşım, işlem geçmişi ve sunucu doğrulama mesajları da Türkçeleştirildi. Yeni sayfalar `Sayfa 1` biçiminde adlandırılır; eski otomatik `Sheet1` adları arayüzde `Sayfa 1` olarak gösterilir. Kullanıcı verileri ve Excel dosyalarındaki özel adlar topluca değiştirilmez.

Filtre sonrası görünür sıra ile fiziksel satırın karışmaması için hücre/odak/düzenleme olaylarında modelin gerçek satır adresi kullanılır. Aralık biçimlendirme ve temizleme görünür satır modellerinden adreslenir. Grid dışındaki alanların klavye ve pano olayları grid kısayollarından ayrılır; araç şeridine tıklarken seçili aralık korunur.

Doğrulama:

- Frontend production build, modül/fixture üzerinde ESLint ve Prettier geçti. `npx tsc --project tools/tsconfig.json --noEmit` temiz; genel proje kontrolünde diğer modüllerdeki mevcut 144 hata devam ediyor.
- Backend Release çözüm derlemesi 0 hata; gerçek veritabanı kullanılmadan 10 snapshot senaryo grubu geçti. Bu arayüz değişikliği için ek migration veya gerçek veritabanı güncellemesi gerekmez.
- Yerel mock fixture'da durum filtresiyle yalnız 2. ve 6. satırlar gösterildi. B2 değer alanından düzenlendi; B6 korundu. B2:C6 görünür aralığına 2×2 yapıştırma ve dolgu rengi uygulandı; Kaydet sonrasında gizli üçüncü satırın değeri/biçimi korundu.
- Türkçe filtre menüsü/renk paleti, sayfa ekleme varsayılan adı, araç sekmeleri ve 1.000.000. satıra gezinme doğrulandı. Görüntüleyende Kaydet/indirme/paylaşım düğmeleri yok, hücre değeri alanı ve grid düzenlemesi kapalı.
- Görseller gerçek API oturumu yerine yerel mock fixture'dan alınmıştır: [Yeni tasarım](MGS-Sheets-modern-preview.jpg), [Türkçe filtre](MGS-Sheets-turkish-filter-preview.jpg).

## Sütun başlığı hizalama düzeltmesi

2026-10-06: Uygulamanın üst menüsündeki `.header-wrapper` stili RevoGrid'in aynı adlı başlık sarmalayıcısına da uygulanıyordu. Başlıklardaki 16 piksel yatay boşluk ve `display: flex`, yalnızca MGS Tablolar grid'i için sıfırlandı.

- Yerel mock fixture'da düzeltme öncesinde başlıklar veri hücrelerinden yaklaşık 16 piksel sağdaydı. Düzeltme sonrasında 120, 140, 170 ve 210 piksel sütunlarda sağ/sol kenar farkı 0 piksel ölçüldü.
- Yatay kaydırma 390 piksel konumundayken görünür sütunların başlık/hücre kenarları yine 0 piksel farkla hizalandı.
- Başlıktaki filtre düğmesinden Türkçe filtre menüsünün açılması ve kapanması doğrulandı.
- Frontend production build, modül/fixture ESLint ve değişen CSS dosyasının Prettier kontrolü geçti.
- Görsel yerel mock fixture'dan alındı: [Düzeltilmiş sütun hizaları](MGS-Sheets-alignment-preview.jpg).

## Otomatik ve kalıcı hücre ölçüleri

Satırların alt kenarından sürükleyerek yükseklik değiştirme açıldı; sütun sürükleme ölçüleri de Kaydet taslağına bağlandı. Metni kaydırılan hücrelerde biçimlendirilmiş metin, satır sonları, yazı tipi/boyutu ve sütun genişliğinden gereken yükseklik hesaplanır. İçeriği göstermek için gereken yükseklik en alt sınırdır; kullanıcının daha yüksek ölçüsü korunur. Görünüm sekmesindeki içeriğe göre ayarlama düğmeleri seçili satır/sütunları yüklenen satır penceresine göre ölçer.

Otomatik ölçüler düzenleyenin ziyaret ettiği pencerelerde taslağa aktarılır. Sürükleme sırasında aynı hedefe ait ardışık ölçü komutları birleştirilir; satır/sütun ekleme ve silme öncesindeki koordinat sırası korunur. Ölçü değiştirmek pencereyi yeniden yüklemez. Kaydet, mevcut `rowHeight`/`columnWidth` sözleşmesiyle ölçüleri saklar; yeni migration gerekmez. Ölçüler 10–1000 piksel sınırındadır; hesaplanan satır yüksekliği de 1000 piksele kadar artar. Uzun kesintisiz metinlerin ölçümü sınırlı önek araması kullanır.

2026-10-06 doğrulaması:

- Yerel mock fixture'da uzun açıklama, üç satırlı metin ve 20 punto metin için hücre `scrollHeight`/`clientHeight` değerleri eşitti; metinler komşu satıra taşmadı.
- Gerçek tarayıcı sürüklemesinde ikinci satır 180→208 piksele, F sütunu 260→300 piksele değiştirildi. Kaydet ve editörü yeniden açma sonrasında her iki ölçü korundu.
- Ödendi filtresiyle gerçek 2. ve 6. satırlar gösterildi. Görünür ikinci satır sürüklenip Kaydet yapıldığında yalnızca gerçek 6. satırın yüksekliği 29→69 piksel olarak saklandı; 2. ve gizli 3. satırın ölçüleri korundu.
- Yalnızca görüntüleme modunda satır/sütun sürükleme kenarları yoktu; filtre sonrasında 2. ve 6. satırın kayıtlı 208/69 piksel yükseklikleri korundu.
- Görünüm sekmesindeki içeriğe göre ayarla işlemiyle 208 piksel yüksekliğindeki seçili satır 66 piksele getirildi; Kaydet ve yeniden açma sonrasında 66 piksel korundu.
- Frontend production build, modül/fixture ESLint, Prettier ve `tools/tsconfig.json` TypeScript kontrolü geçti. Ölçüm/boyut komutu yardımcıları üzerinde 13 saf kontrol geçti.
- Backend `tools/Sheets.Tests` Release çalıştırmasında 13 snapshot senaryo grubu geçti. Boyutların JSON ile yeniden açılışta ve yapı değişikliklerinde korunması ile sınırların reddi doğrulandı; SQL veya gerçek veritabanı kullanılmadı.
- Görsel yerel mock fixture'dan alınmıştır: [Satır yüksekliği ve kalıcı ölçüler](MGS-Sheets-sizing-preview.jpg).

## Hücre klavye kısayolları

2026-10-06: Ctrl+C/Ctrl+V hücre ve aralık değerlerini panoya aktarır; Ctrl+A yüklenen pencerenin filtre sonrası görünür hücrelerini seçer. Ctrl+S mevcut Kaydet akışını kullanır ve açık hücre düzenlemesinin tamamlanmasını bekler. Ctrl+Z geri alır; Ctrl+Y ve Ctrl+Shift+Z yineler. Metin düzenleyicilerinde metin seçimi, kopyalama ve doğal metin geri alma/yineleme davranışları korunur. Giriş sekmesine Geri al/Yinele düğmeleri eklendi.

Geçmiş hücre, toplu yapıştırma, biçim, kullanıcı ölçüsü ve sayfa/satır/sütun yapı işlemlerini taslak görüntüleriyle saklar. En fazla 100 adım ve toplam 200.000 ağırlıklı kayıt tutulur; büyük taslaklarda daha az adım kalabilir. Başarılı Kaydet, yeniden yükleme veya içe aktarılmış sürümü açma geçmişi sıfırlar. Başarısız Kaydet taslağı ve geçmişi korur. Otomatik yükseklik hesapları ayrı geri alma adımı oluşturmaz. Bu değişiklik backend sözleşmesini veya migration'ı değiştirmez.

RevoGrid'in asenkron kopyalama metodu yerine yerel copy olayının içinde senkron TSV yazılır. Filtre panelindeki pano olayları grid'e ulaşmadan durdurulur; filtre kutusuna yapıştırma seçili hücreyi değiştirmez. Filtreleme sanal satır konumunu aynı tutsa bile `beforecellfocus` ile fiziksel hücre adresi güncellenir; aralık geçmişi doğru odağı saklar.

Yerel mock fixture doğrulaması:

- Ctrl+C seçili hücre değerini aynı copy olayında panoya yazdı. B2:C3 aralığına 2×2 Ctrl+V tek geri alma adımı olarak işlendi; Ctrl+Z dört eski değeri geri getirdi, Ctrl+Y yeniden uyguladı. Geri alma sonrası yeni düzenleme yineleme dalını temizledi.
- Açık hücre düzenleyicisinde Ctrl+S son yazılan değeri kaydetti. Enter'dan hemen sonra Ctrl+S de son değeri kaydetti; her işlem için bir kayıt isteği görüldü ve editörü yeniden açınca değerler korundu.
- Örnek başarısız Kaydet sonrasında taslak ve Geri al korundu; Ctrl+Z eski hücre değerini getirdi. Başarılı Kaydet sonrasında geçmiş düğmeleri kapandı.
- Ctrl+A/Ctrl+C, 128 satır ve 100 sütunluk yüklenen pencerenin tamamını kopyaladı. Metin alanında Ctrl+A/C, Ctrl+Z/Y doğal metin davranışıyla çalıştı; çalışma kitabı geçmişini değiştirmedi.
- Kalın biçimi Ctrl+Z ve Ctrl+Shift+Z ile geri alınıp yinelendi. Silinen satır Ctrl+Z ile hücreleri ve 1.000.000 satır boyutuyla geri geldi. Seçili satır yüksekliği 85→160 piksel değiştirildi; Ctrl+Z 85, Ctrl+Y 160 pikseli getirdi.
- Ödendi filtresinde gerçek 2. ve 6. satırlar gösterildi. B6'ya Ctrl+V, Ctrl+Z ve Ctrl+Y sırasında seçili adres B6 kaldı. Kaydet sonrası B6 değişti; B2 ve gizli B3 değerleri korundu. Filtre değer kutusuna yapıştırılan Ödendi, hücreyi değiştirmedi.
- Yalnızca görüntüleme modunda Ctrl+C çalıştı; Ctrl+V/S/Z hücreyi veya kaydı değiştirmedi.
- Frontend production build, Sheets/fixture ESLint, Prettier ve `tools/tsconfig.json` TypeScript kontrolü geçti. Geçmiş yardımcısı üzerinde sekiz saf senaryo grubu geçti. Proje genelindeki diğer modül hataları önceki bölümde belirtilmiştir.
- Görsel yerel mock fixture'dan alınmıştır: [Klavye kısayolları ve geçmiş araçları](MGS-Sheets-keyboard-preview.jpg).

## SignalR bağlantısını POST ile kapatma

2026-10-07: Kullanıcının canlıdaki `DELETE /api/sheets-hub?id=...` isteği için bildirdiği CORS hatası ve POST kullanılması isteği üzerine yalnızca MGS Tablolar bağlantı kapatma akışı değiştirildi.

`SheetsHubHttpClient`, SignalR'ın Long Polling kapatma çağrısını ağda `POST /api/sheets-hub/disconnect?id=...` olarak gönderir. Aynı hub'ın origin/path eşleşmesi aranır; müzakere, mesaj gönderme, okuma ve diğer adreslerdeki istekler değiştirilmez. Bağlantı kimliği, Bearer başlığı, çerez/credentials, zaman aşımı ve iptal sinyali korunur. WebSocket bağlantısının protokolü değişmez.

`SheetsHubDisconnectMiddleware`, yalnızca bu POST adresini sunucunun içinde mevcut SignalR kapatma akışına yönlendirir. Routing öncesinde uygulanır; mevcut CORS ve `[Authorize]` kontrolleri çalışmaya devam eder. İşlem sonunda veya hatada orijinal HTTP metodu/adresi geri yüklenir. Ağda DELETE gönderilmez; bağlantının sunucuda zaman aşımını beklemeden temizlenmesi korunur. CORS origin listesi genişletilmedi; migration gerekmez.

Doğrulama:

- İstemci yardımcı kontrolünde URL/query, Authorization, credentials, timeout ve abortSignal korunması; diğer origin/path/metotların değişmemesi; orijinal isteğin değiştirilmemesi ve çerez aktarımı geçti.
- Kurulu SignalR istemcisiyle zorlanmış Long Polling başlangıcı, JSON handshake, mesaj, normal kapanış ve başlangıç sırasında mesaj gönderme hatası senaryoları geçti. Alttaki HTTP istemcisine her kapatmada bir POST ulaştı; hiçbir DELETE ulaşmadı. Kontroller gerçek veritabanı veya canlı bağlantı kullanmadan örnek HTTP istemcisiyle çalıştırıldı.
- Frontend production build, değişen dosyalarda ESLint, modül TypeScript ve Prettier kontrolleri geçti.
- Backend Release derlemesi 0 hatayla tamamlandı. Veritabanı kullanılmadan 22 senaryo grubu geçti. Gerçek localhost SignalR/Long Polling JSON handshake ve JWT ile mesaj çağrısından sonra POST kapatma kabul edildi; bekleyen okuma sonlandı, `OnDisconnected` çalıştı ve bağlantı kaldırıldı. Tekrar kapatma/okuma 404, eksik kimlik 400, oturumsuz kapatma 401 döndü. POST preflight 204, izin verilen origin/credentials başlıkları, izin verilmeyen origin'in reddi ve hata durumunda method/path geri yüklenmesi doğrulandı.
- Canlı API'ye oturum/veri içermeyen `OPTIONS /api/sheets-hub/disconnect` isteğinde POST preflight sonucu 204 oldu. `Access-Control-Allow-Origin: https://flowassist.mgs.com.tr`, credentials, authorization başlığı ve POST metodu doğru döndü. Canlıda mevcut bir bağlantıya kapatma isteği gönderilmedi.

Yayın sırası: Önce backend yeni POST uyumluluğuyla, ardından frontend yeni HTTP istemcisiyle yayınlanmalıdır. Backend eski SignalR istemcileriyle uyumlu kalır; yalnız frontend yayınlanırsa yeni kapatma adresi henüz bulunmayabilir. Bu doğrulama kaynak kod ve yerel testleri kapsar; canlı uygulama sürümleri bu çalışma sırasında yayınlanmadı.
