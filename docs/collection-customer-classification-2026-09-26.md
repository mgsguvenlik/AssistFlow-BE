# Test müşteri sınıflandırması — 26 Eylül 2026

## Karar ve kapsam

- Bireysel: `MGS`, `MGSK`, `MGI`, `MGIK`, **`STB`, `STBG`**.
- Grup Üyesi: açıkça onaylanan `DECH`, `BNT`, `BNTS`, `BNTK`, `TKN`, `ABN` ve legacy grup ana kartı/GM üyeliği ile doğrulanmış diğer kodlar. Tam liste [karar verisindedir](data/collection-customer-classification.json).
- `FIN*`, `YKB*`, `EMK` hakediş kapsamındadır; tahsilatın iki sınıfına alınmaz.
- Önceki belirsiz kod listesinden yalnız STB/STBG bireysel kapsama alınmıştır. MGST dahil aşağıdaki diğer kodlarda tahsilat yapılmayacaktır. Bu karar, daha önce doğrulanmış grup kodlarını iptal etmez.
- Kapsam dışı olmak müşteriyi silmek veya mevcut müşteri tipini boşaltmak değildir. Yeni/bilinmeyen bir kod otomatik sınıflandırılmaz.
- Yetki yalnız `192.168.1.8 / AssistFlowTest` içindir. Legacy MGS ve production üzerinde yazma yapılmadı.

## Yapılan işlem

`dbo.CustomerType` içinde mevcut **4 / Bireysel / N** ve **7 / Grup Üyesi / GM** kullanıldı. Yeni tanım eklemek gerekmedi; canlı ortamın GRP01 kodu testteki GM kaydını yeniden adlandırmak için gerekçe sayılmadı.

| Sonuç | Silinmemiş müşteri sayısı |
| --- | ---: |
| Bireysel kapsam, mevcut tip korundu | 4.829 |
| Bunun içinde STB | 40 |
| Bunun içinde STBG | 89 |
| Bireysel → Grup Üyesi olarak güncellendi | 2.701 |
| Önceki belirsiz listeden tahsilat dışı; tip değişmedi | 528 |
| FIN*/YKB* hakediş; tip değişmedi | 9.471 |

STB/STBG toplam 129 kayıt ilk kontrolde zaten Bireysel tipindeydi. Yukarıdaki sayılar `IsDeleted=0` müşteri sayılarıdır; aktif/donuk abonelik veya aktarıma uygun sözleşme sayısı değildir.

Ayrıca grup kodu BNTK olduğu halde BANKA tipinde bulunan bir test müşterisi korunmuştur. Grup ilişkisi olmayan iki test kaydına da dokunulmamıştır. BANKA tipi 6, tüm müşterilerin CustomerGroupId/TenantId değerleri ve kapsam dışındaki müşteri tipleri transaction içinde kontrol edilmiştir. CustomerGroups/CustomerType tanımları değiştirilmedi.

## Senkronizasyon etkisi

Manitou staging akışı banka dışındaki müşterilerin tipini 4 olarak getiriyordu. Mevcut **test** `stg.sp_SyncCustomers` prosedürünün yalnız müşteri tipi eşlemesine kod sınıflandırması eklendi; mevcut BANKA kaydı korunur. Diğer alanların senkronizasyon mantığı değiştirilmedi. Prosedürün kendisi bu çalışma sırasında çalıştırılmadı; staging kayıtları işlenmedi.

İşlem [PowerShell aracı](../tools/Set-CollectionCustomerClassification.ps1) ve [parametreli SQL](sql/20260926_ClassifyCollectionCustomers.sql) ile yapıldı. Önceki değerler ve prosedür tanımı Git dışına kaydedildi. Uygulanan plan SHA-256:

`F0245A72C6B7B576AEA398791085D2FBA26BE8D4A9B54FA4412770499F790836`

Yerel uygulama kanıtları: `tools/Collections.Import/snapshots/customer-classification-20260926-123917461/`.

İlk denemede doğrulama sorgusu zaman aşımına uğradı; müşteri/prosedür değişikliklerinin geri alındığı ve açık istek kalmadığı bağımsız bağlantıda doğrulandı. JSON bir kez indeksli ara tabloya alınarak doğrulama düzeltildi. İkinci uygulama yaklaşık 2 saniyede tamamlandı. Sonraki önizleme **7.530 aday / 0 değişiklik** döndürdü; tip dağılımı bağımsız bağlantıda 4=5.357, 7=2.701, 6=9.473, boş=1 olarak doğrulandı. 5.357 Bireysel tipli kaydın 528'i tahsilat dışında olduğundan **CustomerType tek başına tahsilat uygunluğu değildir**.

## Servis talebi etkisi

- CustomerType müşteri tipidir; CustomerGroups ayrı grup/fiyat/hakediş/onarım ilişkilerini taşır. Bu iki alan birbirinin yerine kullanılmadı.
- İncelenen MGS/YKB/QNB/EKB servis talebi formlarında banka alanlarını açan kontrol CustomerTypeId=6'dır. Bu tipler ve tenant ilişkileri korunmuştur.
- SLA'daki iş akışı müşteri tipi enum'u dbo.CustomerType değildir.
- Bu sonuç kod/DB incelemesidir; tüm tenantlarda gerçek kullanıcıyla uçtan uca servis talebi testi yapıldığı iddiası değildir.

## Tahsilat dışı kalan önceki belirsiz kodlar

| Kod | Silinmemiş müşteri |
| --- | ---: |
| AEG | 0 |
| AESG | 30 |
| ASR | 3 |
| AVIG | 10 |
| CRFT | 10 |
| ELTR | 4 |
| ENKA | 0 |
| ENNE | 5 |
| ENPA | 9 |
| HOLD | 247 |
| KNSL | 1 |
| KOSE | 12 |
| MERT | 1 |
| MGSB | 43 |
| MGSD | 33 |
| MGSM | 0 |
| MGST | 69 |
| MKE | 1 |
| NEAT | 1 |
| NISA | 2 |
| NM | 41 |
| ODC | 0 |
| PANA | 2 |
| PİNE | 1 |
| TASG | 0 |
| TEB | 2 |
| ZODG | 1 |
| **Toplam** | **528** |

## Madde 2 entegrasyonu — tamamlandı

- Onaylı JSON kararı Model derlemesine gömülü tek kaynaktır; uygulama sorguları ve importer aynı kod listesi/politika hash'ini kullanır. Ortamlar arasında sabit tip Id eşlemesi yapılmaz: N/BRYSL01 ve GM/GRP01 karşılıkları desteklenir. Listede olmayan kodlar ve uygun kodda yanlış müşteri tipi operasyonel kapsamdan dışlanır.
- Takip listesi ve CSV aynı SQL filtresini kullanır. Bireysel görünüm yalnız bireysel, grup özeti yalnız GM müşterileridir; gruptan üyeye iniş yalnız o gruptaki GM sözleşmelerini getirir. STB/STBG için grup durum düğmesi gösterilmez; API de bu işlemi reddeder. Sayfalama/sıralama sunucuda kalır.
- Tahsilat müşteri seçicisi mevcut definitions API'ye eklenen Customer türünden gelir; ortak müşteri API'si ve diğer tenant ekranları filtrelenmedi. Mevcut aramalı Select ve toast yapısı korunur.
- Yeni sözleşme, sözleşme düzenleme, abonelik/tarife değişimi, tek/toplu ödeme ve ödeme düzenleme/silme için kapsam kontrolü transaction içinde uygulanır. Önceden tamamlanmış idempotent komutun makbuzunu tekrar okumak yeni işlem değildir. Grup durum komutunda ayrıca GM kontrolü vardır.
- Sözleşme listesi, raporu, detay, tarife/ödeme geçmişi ve mevcut dosyalar kayıpsız okunmaya devam eder. Kapsam dışı detayda Türkçe açıklama ve salt-okunur finansal arayüz vardır. Toplu zam ekranı rapor API'sinin `EligibleOnly` filtresiyle yalnız uygun sözleşmeleri seçer; normal tarihsel raporun kapsamı daraltılmaz.
- CustomerService'in dış kaynaktan **yeni müşteri** oluşturma yolunda onaylı grup kodları mevcut GM/GRP01 tanımına gider. Banka kuralları, mevcut müşteriler, CustomerGroups ve TenantId değiştirilmez. Test staging prosedüründeki koruma önceki bölümdeki gibidir.
- Yeni kesit validator'ı müşteri tipini yalnız legacy N/GM harfine göre değil onaylı hedef grup koduna göre doğrular. Plan ve apply uygun müşteriyi bağımsız yeniden kontrol eder; eski politika sürümüyle üretilmiş plan hash'i kullanılmaz.

## Mevcut staging üzerinde madde 2 sonucu

Applied/Excluded içeren batch 1'e genel validator çalıştırılmadı. Yeni `review-customer-scope-batch` komutu, mevcut staging kesitinin kimliğini/hash'ini ve müşteri tip/grup/abone eşleşmesini tekrar kontrol ederek yalnız madde 2 issue'larını güncelledi. Yalnız tekil, silinmemiş, abone numarası değişmemiş hedeflerde işlem yapar. Kaynak ve hedef müşteri eşlemesini, tarifeleri, diğer maddelerin issue'larını değiştirmez. SQL transaction ve aktarım uygulama kilidi kullanılır; onaylanan plan uygulama anında yeniden hesaplanır.

Uygulanan plan: `A0B22FE99FDBF8AB04E805B97F56FA735C729D0B6366D7531A506C4A5EE45C3A`.

| Sonuç | Adet |
| --- | ---: |
| Audit notuyla kapatılan eski CUSTOMER_TYPE_MISMATCH | 1.945 |
| Yeni CUSTOMER_COLLECTION_EXCLUDED engeli | 70 |
| Bu engelden çıkıp Pending olan sözleşme | 1.389 |
| Değerlendirilen kümede başka engelle/kapsam nedeniyle Blocked kalan | 557 |
| İşlem sonrası batch Pending / Blocked | 1.389 / 2.250 |
| Değişmeyen batch Applied / Excluded | 3.173 / 2.686 |
| Değişmeyen hedef sözleşme / tarife / ödeme | 3.175 / 11.867 / 0 |

Tekrar önizleme **0 değişiklik / 0 kapatılacak issue** döndürdü. Kapanan issue'lar silinmedi; karar notu/tarihi korundu. Kalan 76 CUSTOMER_TYPE_MAP_MISSING kaydı, tekil/doğrulanmış hedef müşterisi olmayan satırlardadır; bunlar müşteri kimliği/eşleştirme maddeleri kapsamında bekler, tahminle tip atanmaz. 1.389 Pending satır bu aşamada **aktarılmadı**; kalan maddelerin genel aktarım önkoşulları ayrıca değerlendirilecektir. Donuk/dönem/tutar/tarihçe kararları bu işlemle iptal edilmiş değildir.

Tekrar önizleme komutu:

```powershell
dotnet run --project tools/Collections.Import -- review-customer-scope-batch 1 WebAPI/appsettings.Development.json
```

Yalnız güncel önizleme hash'i dördüncü argüman olarak verilirse test staging yazması yapılır. `review-customer-scope <kesit klasörü> <ayar yolu> [hash]` özgün kesit dosyalarıyla da çalışır. Bağlantı bilgileri komut çıktısına veya rapora yazılmaz.

## Doğrulama

- Backend solution build: 0 hata; mevcut uyarılar devam ediyor. Importer build: 0 hata/0 uyarı.
- FE production build ve değişen dosyaların ESLint kontrolü başarılı. Tam TypeScript kontrolü projedeki diğer dosyalarda 144 hata nedeniyle geçmiyor; değişen tahsilat dosyalarında kalan type hatası yok. Toplu zam DTO'sunun mevcut eksik rowVersion tipi de tamamlandı.
- AssistFlowTest üzerinde gerçek EF/SQL okuması: bireysel 4.829 / grup 2.701; STB/STBG 129 bireysel ve grupta 0; banka/MGST/MGSB uygun müşteriler arasında yok. Eylül 2026 takibi bireysel 1.100 / grup 0; yıllık grubun vade ayı Ocak 2026'da bireysel 1.201 / grup özeti 1. Dolu grup özeti ve üye tahakkuk/ödeme toplamları, liste/CSV sayımları aynı.
- Customer lookup, yalnız-uygun rapor, kapsam dışı tarihsel detay/ödeme okuması ve yeni sözleşme komutunun kapsam nedeniyle reddi doğrulandı. Kontrol sırasında veri oluşturulmadı; yeni kalıcı test projesi eklenmedi.
- Gerçek tarayıcı/HTTP oturum kabulü ve tüm tenant servis talebi uçtan uca testi bu teknik kontrollerin yerine geçmiş sayılmaz. Çalışan API yeni derlemeyi henüz yüklememişse test öncesinde normal geliştirme süreciyle yeniden başlatılmalıdır; bu çalışma genel startup/seed çalıştırmadı.

Production değişikliği ve bütün modülün kabulü bu test sınıflandırmasının parçası değildir.

## Madde 2 sözleşme aktarımı — 26 Eylül

**Daha sonraki madde 1 kararı:** Donukları dışlama kararı kaldırıldı; [donukların durumunu koruyarak aktarılması](collection-frozen-import-2026-09-26.md) güncel karardır. Bu bölüm madde 2 çalışmasının o andaki sonuçlarını korur.

Yukarıdaki staging incelemesinden **sonra**, kullanıcının yalnız madde 2 için verdiği aktarım onayı uygulandı. Önceki “yeni aktarım yok” sayımları inceleme anını gösterir; güncel sonuç aşağıdadır.

| Sonuç | Adet |
| --- | ---: |
| Madde 2 kapsamında Pending aday | 1.389 |
| Aktarılan aktif sözleşme | 736 |
| Aktarılan bağlı tarife dönemi | 3.402 |
| Aktarılmayan donuk sözleşme | 653 |
| Aktif adaylarda kalan başka engel | 0 |
| Birikimli aktarılmış sözleşme / tarife (map = stage = hedef) | 3.909 / 15.267 |
| İki eski manuel kayıt dahil hedef sözleşme / tarife | 3.911 / 15.269 |
| Hedef ödeme | 0 |

- Plan hash: `898DE078C4541386955D3980EF3B241E24B64A3D84B4FCA65B750FA38F5F6829`.
- Plan yalnız madde 2 karar notuyla çözülmüş `CUSTOMER_TYPE_MISMATCH` kaydı olan sözleşmeleri seçer. Ham staging JSON/hash, müşteri kimliği/kapsamı, açık engeller, tüm tarihçe kümesi, tarife alanları, referanslar ve mevcut aktarım eşlemeleri yeniden kontrol edilir. Önizleme ve Serializable transaction içindeki uygulama aynı seçimi/hash'i kullanır; değişmiş plan uygulanmaz.
- Donuk 653 sözleşme yüklenmedi. Ham kayıtları ve Pending durumları korunur; Pending burada aktarım izni değildir. Genel importer da donukları atlar. Önceden aktarılmış kayıtlar bu işlemde silinmedi.
- Yeni 736 sözleşmenin tamamı `ACTIVE` / `EXISTS`; madde 2 dışı yeni sözleşme 0. Tarihçe tutar, para birimi, ödeme sıklığı, davranış, başlangıç/bitiş, bağlı sözleşme ve payload hash karşılaştırmasında uyuşmazlık 0.
- Aktif sözleşmenin geçmişindeki ücretsiz veya askıda dönemler kendi davranışıyla korunur; bu, güncel üyeliği donuk bir sözleşmenin aktarılması değildir.
- Yeni tarihçe mutabakatı: TRY Billable 3.113 satır / 3.506.373,30; TRY Free 1 / 205,00; USD Billable 287 / 19.826,94; USD Suspended 1 / 35,40. Bunlar tarihçe dönem tutarlarının teknik toplamıdır, tahsil edilecek güncel borç değildir.
- Başka madde issue'ları, ödeme ve dosyalar, ortak dbo, legacy ve production değiştirilmedi. Güncel staging Pending 653 / Blocked 2.250 / Applied 3.909 / Excluded 2.686.
- Uygulama sonrası aynı kapsamın yazmasız tekrar önizlemesi: **0 aktarılabilir sözleşme / 0 tarife**, kalan 653 adayın tamamı donuk. Böylece tamamlanan küme tekrar seçilmedi. Tekrar plan hash'i `61C79867C29E61C0F6E1A8B66508383CAEEE8A427EA8798FB44C7562D57BB255`.
- Önizleme ve uygulama planları Git dışındaki `tools/Collections.Import/snapshots/item-two-20260926-133707872/plan.json` ve `item-two-20260926-133745295/plan.json` altında; alan bazlı salt-okunur son kontrol `.tools/item-two-postflight.json` içindedir. Importer build 0 hata / 0 uyarı.

Yazmasız tekrar kontrolü:

```powershell
dotnet run --project tools/Collections.Import -- transfer-item-two-batch 1 WebAPI/appsettings.Development.json
```

Komuta dördüncü argüman olarak güncel plan hash'i eklenirse yalnız AssistFlowTest'te uygulama yapılır. Yeni aktarılabilir sözleşme yoksa yazmadan döner. Diğer müşteri karar maddeleri ayrı değerlendirmeye bırakılmıştır.
