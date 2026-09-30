# Tahsilat canlı geçiş hazırlığı

Durum: hazırlık aşaması. Kullanıcının 27 Eylül 2026 talebiyle testteki tüm DB geliştirmeleri ileride AssistFlow canlı ortamına taşınabilir ve denetlenebilir olarak hazırlanacak. Bu belge canlı uygulama onayı veya tamamlanmış yayın paketi değildir.

## Ortam ve temel yaklaşım

### K09 geçiş paketi — 29 Eylül

- **Son ödeme yöntemi tespiti:** `20260930230000_EnableCollectionRequestedPaymentMethods` yalnız GTS/BANK_TRANSFER mevcut kayıtlarını aktif yapar; testte uygulandı. Canlı yayında bu veri migrationı da gereklidir (aşağıdaki “yeni migration yok” önceki kod dilimini anlatır). Yeni kurulum seed'i bu iki tanımı aktif oluşturur. Diğer POS/online/legacy tanımlar pasif kalır; otomatik Down desteklenmez. Gerçek sözleşme ödeme yöntemi topluca değiştirilmez.

- **Tahsilat ekibi geri bildirimi:** toplamlar API'si, yeni sözleşme PaymentMethodId ve AnniversaryYear ile yıllık tarife planlama için BE önce, FE sonra yayınlanmalı. Yeni DB migrationı gerektirmez. Eski istemcinin AnniversaryYear göndermediği tekil tarife davranışı değişmez; ödeme yöntemsiz oluşturma isteği hash'i korunur. Ekip yıl/oran seçip onaylar; canlı gerçek sözleşmelere otomatik zam yüklemesi yapılmaz. İleri tarife dilimi standart hesaplayıcıyla devreye girer. Son HTTP/UI kabulü tamamlanmadan yayın onayı verilmiş sayılmaz.

- **30 Eylül manuel müşteri dosyaları:** `AddCollectionCustomerFileActions`, `AddCollectionCustomerAttachments` sonrasında ve yeni API yayını öncesinde uygulanmalıdır; testte uygulandı, canlıya uygulanmadı. Legacy kimlikler/hash nullable, CreatedUser/IsDeleted/RemovedUser/RemovedDate eklenir. Eski aktarımların metadata'sı korunur. Down audit alanlarını kaybettirir; otomatik geri dönüş yapılmaz. Mevcut CDN servisi kullanılır; listeden kaldırma fiziksel CDN silme değildir. Tekil SourcePath ve kaldırılmış kayıtlar korunmalı, aktarım makbuzu silinerek yeniden etkinleştirme yapılmamalıdır. Test SQL/CDN kontrolü geçti; oturumlu GET liste görüldü, yeni API yeniden başlatma/HTTP yetki kabulü açık.

- **30 Eylül müşteri dosyaları:** `20260930194336_AddCollectionCustomerAttachments` sadece testte uygulandı. Mevcut CDN sağlayıcısı değişmeden müşteri kartına ayrı arşiv ilişkisi; kaynak yol/iki legacy kimlik/karar ve SHA256 kanıtları tutulur. Canlıda migration BE/FE yayını öncesinde uygulanmalıdır. SourcePath tekilliği korunmalı; Down/makbuz silme tekrar aktarım stratejisi değildir.
- Kullanıcı eski aboneliğin dosyalarını doğrulanmış güncel karta devretmeyi onayladı, ödemeleri devretmeyi değil. Testte924→291401→16838 için 3 dosya uygulandı; içerik ve tekrar güvenliği doğrulandı. Bu hedef ID canlıya kopyalanmaz. 2206→87400 için hedef eşlemesi doğrulanmadı;16598 eski kartı kullanılmadı. Canlıda aynı karar + güncel abone tekilliği/kapsam doğrulaması ve ayrı plan hash'i gerekir.
- `activity-transfer <Development JSON> <Aktivite.rar> [--install|plan hash]` test-only, arşiv hash'i ve doğrulanmış924devriyle sınırlı araçtır; canlı yayın aracı değildir. Diğer dosyalar için otomatik isim/tahmin eşlemesi yok. SQL/CDN atomik değildir; belirsiz durumda içerik nesnesi tutulur, hash+makbuz üzerinden yeniden deneme yapılır. Müşteri dosyası ekranı ve HTTP yetki kabulü hâlâ açık.

- **30 Eylül ek kararı:** YOK geçişi geçmişi korur; Türkiye saatine göre ertesi gün yeni Suspended tarife açılır. Aynı günün oluşmuş borçları da korunur. Mevcut audit tablosuyla çalışır; ek migration yok. Test doğrulaması geçti, gerçek sözleşmeler topluca değiştirilmedi. Tarife tarih düzeltmesinde kesintisiz tarihçe şartı korunur.
- Aktivite.rar doğrulandı ancak 15 asıl dosyanın hiçbirinde kesin korunmuş tahsilat müşteri eşlemesi yok. Kaynak dosya eksikliği değil, sahiplik/kapsam kararı bekleniyor. Dosyalar CDN'e aktarılmadı. Canlı müşteri kimlikleri test adaylarından kopyalanamaz; yeniden eşleme ve dosya kimlik/hash mutabakatı gerekir.

- `20260929130125_AddCollectionContractCorrections` yalnız testte uygulandı. Uyumlu BE/FE yayını öncesinde uygulanmalı: sözleşme oluşturma işlemi de silme makbuzunu bu tabloda kontrol eder. Ortak dbo tabloları değişmez.
- ContractCorrection ve PaymentOperation kayıtları kalıcı audit/tekrar korumasıdır. Down, toplu temizlik veya test ID'lerini canlıya kopyalama yayın/geri dönüş yöntemi değildir. Fiziksel silinen sözleşmenin makbuzu korunmazsa eski oluşturma isteği yeniden kayıt üretebilir.
- Yeni düzeltme/taşıma/silme komutları mevcut CollectionFollowUp Edit ve ContractCreateEnabled kapısına bağlı. Müşteri dosyasını listeden kaldırma CDN URL'sini iptal etmez; fiziksel dosya arşivde kalır.
- Test-only `k09-check` geçici SQL/CDN kayıtlarıyla doğrulandı ve kendi kayıtlarını temizledi; canlıda çalıştırılmaz. Gerçek veri düzeltmesi/silmesi ayrıca hedef ve etki onayı gerektirir. Banka işlem kimliği ve legacy makbuzları ödeme taşıma/silmede korunmalıdır.
- K09 bütünü kapatılmadı: VAR/YOK geçmiş etkisi, ayrı müşteri dosyası kaynağı ve HTTP/browser kabulü açık. Bunlar çözülmeden nihai yayın kabulü verilmez. Tarihsel tarife aralığı düzeltmesi komşu dilim çakışmasını ve eski/yeni aralıktaki ödemeleri kontrol eder; yenileme günü/borç davranışı korunur.

### K07 geçiş paketi — 29 Eylül

- `20260929100739_AddCollectionInvoiceLoads`, ardından `20260929101445_AddCollectionInvoiceLoadRowAudit` yalnız testte uygulandı. Canlıya sürümlü migration ile alınacak; mevcut müşteri veya CDN şemasını değiştirmez.
- `invoice-accounts <Development JSON> <rapor> [--install | plan SHA-256]` test-only araçtır. Önizleme/hash uygulanmadan cari bağlanmaz. 2.910 test eşlemesi canlı CustomerId olarak kullanılamaz; canlı korunan stage/map/GroupParent üzerinden ayrı plan gerekir. Güncelleme eski kaynakta kalmayan kodu otomatik aktif bırakmaz; hata durumuna geçirir.
- Kaynak dosyalar mevcut R2FileStorage'da değişmez hash anahtarıyla tutulur. Varsayılan sağlayıcı/izinler/CDN yapısı değiştirilmedi. DB+CDN atomik olmadığından belirsiz sonuçta nesne silinmez, aynı içerikle yeniden deneme yapılır. Test doğrulama dosyaları temizlendi; gerçek müşteri dosyası bu geliştirmede toplu yüklenmedi.
- InvoiceLoadRow ImportedKey ve uygulayan/tarih kayıtları finansal silme sonrasında korunur. Canlı geri dönüşte faturaları/queue/audit'i Down ile toplu silmek yerine yedek+mutabakat ve kontrollü uygulama geri dönüşü gerekir. Yeniden dosya yükleme silinmiş faturayı canlandırmamalıdır.
- Test CDN/SQL entegrasyonu ve build/lint geçti. 699 çoklu cari kod ve diğer eşleme istisnaları K12'de; gerçek dosya/browser kabulü ve canlı yayın onayı açık.

- AssistFlowTest geliştirme/prova hedefidir; AssistFlow canlı hedefi şu anda yalnız okunur. MGS kaynaktır, değiştirilmez.
- Test veritabanı canlı üzerine kopyalanmaz. Onaylı şema değişiklikleri ve kaynak veriler hedef ortamın mevcut kimlikleriyle yeniden eşlenir.
- Testteki CustomerId, CustomerGroupId, CustomerTypeId, ServiceTypeId, CurrencyTypeId, sözleşme/dosya kimlikleri canlıda aynı varsayılmaz. Her ortamın eşleştirme raporu ayrı üretilir; belirsiz/çoklu eşleşmeler uygulanmaz.
- Şema migrationları, tanım verileri, ortak müşteri güncellemeleri, aktarım ve dosya ilişkileri ayrı adımlar olarak kaydedilir. Tekrar çalıştırma davranışı ve uygulama önkoşulları her adımda belirtilir.
- Uygulayıcıların test DB kilidi kaldırılıp canlıya yönlendirilmesi bir yayın yöntemi değildir. Hedefi açık seçilen, izin verilen DB'yi doğrulayan ve önizleme/uygulama ayrımı bulunan yayın yolu hazırlanır. Test fixture araçları canlıda çalıştırılmaz.
- Hassas kesitler, müşteri listeleri, bağlantı bilgileri, yedekler ve erişim anahtarları Git'e konmaz. Repoda komutlar, kurallar, anonim sonuçlar ve kanıt dosyalarının hash referansları tutulur.

## Değişiklik envanteri

| Parça | Mevcut kaynak | Canlı hazırlığı / kabul |
| --- | --- | --- |
| collection temel şema | AddCollectionFoundation | Gerçek migration geçmişi/FK/kolon farkıyla doğrula; yalnız beklenen collection DDL |
| Yenileme günü | AddCollectionOriginalAnchorDay | Nullable eski davranış korunur; tahmini günle geri doldurma yok |
| Oluşturma isteği | AddCollectionContractCreationRequest | İstek tekilliği ve tekrar güvenliği |
| Aktarım hazırlık tabloları | AddCollectionMigrationStaging, AddCollectionMigrationCustomerTypeMap | Canlıya ait yeni kesit, hedef eşlemeleri ve uygulama kayıtları |
| Sözleşme dosyaları | AddCollectionContractAttachment | Mevcut CDN nesnesi/ortamı ve hedef sözleşme bağı doğrulanır |
| Takip indeksleri | 20260927123000_AddCollectionTrackingIndexes | Hedefte iki indeksin kolon/filtre tanımı ve migration kaydı doğrulanır |
| Tahsilat tanımları | CollectionDefinitionSeed | Yalnız modül seed'i; mevcut kodlara göre eksikler, ikinci çalışmada değişiklik yok |
| Ortak tanımlar | CustomerType, ServiceType, CurrencyType ve gerekli diğer tanımlar | Mevcut kod/anlam eşleşmesi; yeni ve değişecek kayıtlar ayrı önizleme |
| Müşteri tipleri | docs/data/collection-customer-classification.json, tools/Set-CollectionCustomerClassification.ps1 | Canlıya özel fark raporu; onaylı kodlar; önceki değer yedeği; TenantId/CustomerGroupId korunur |
| Müşteri senkronizasyonu | Test stg.sp_SyncCustomers düzenlemesi | Canlıdaki mevcut prosedür sürümü okunur; dar fark hazırlanır; test prosedürü bütünüyle üstüne yazılmaz |
| K03 üst kart/cari | collection-k03-customer-context-2026-09-27.md | Kesin kimlik/aday incelemesi, hedef müşteri oluşturma/eşleme, servis seçicilerine etki ve tekrar güvenliği |
| K03 testte uygulanmış ilişki | 20260927160000_AddCollectionGroupParent; Collections.Import group-parents | G tipi ve 56 test üst kartı/ilişkisi; canlıda kod/kimlik yeniden çözülür, test makbuzları canlı uygulanmış sayılmaz. 18 istisna ayrı kalır; 49 üst kart sözleşmesi ayrıca aktarım bekler |
| Sözleşme/tarife/ödeme | Collections.Import, ilgili aktarım raporları | Kabul edilmiş kararlar, kaynak kimliği, para birimi/tutar ve alan mutabakatı; elenen eski abonelikler korunmaz |
| Menü/yetki/uygulama ayarları | Mevcut CRM Tahsilat menüsü ve işlem yetkileri | Hedefte eksik kayıtlar ve açılış ayarları ayrı yayın adımı; genel seed çalıştırılmaz |
| K04 müşteri notları | 20260927180000_AddCollectionCustomerNotes; Collections.Import customer-notes; collection-k04-customer-card-notes-2026-09-27.md | Testte 8.387 not; 4.431 eşleme istisnası/2 kapsam dışı. Legacy kimlik/hash/audit korunur. Canlı müşteri/migration eşlemesi yeniden çözülür; test ID ve plan hash'i canlıda kullanılmaz. UI ve komut kabulü tamamlanmadan yayın kabulü verilmez |
| K05 grup dönem geçmişi | 20260927190000_AddCollectionGroupFollowUpSource; Collections.Import group-history; collection-k05-group-history-2026-09-27.md | Testte 26.207 dönem. Kaynak kimliği/hash/indeksler; tam adla durum eşlemesi ve korunmuş sözleşme eşlemesi. 2 birebir tekrar tekilleştirildi; kaynak kimlikleri raporda. Canlıda yeniden önizleme; otomatik Ödendi davranışı ve UI kabulü açık |
| K06–K11 çıktıları | Görev tamamlandıkça buraya eklenecek | Fatura/dosya yükleme/senkronizasyon/bildirim bağımlılıkları ayrı kayıt |

Bu envanter başlangıç listesidir. Canlıya geçişten önce migration dışındaki SQL/prosedür/menü/ayar değişiklikleri dahil testte uygulanmış tüm adımlar taranıp uygulama paketiyle birebir karşılaştırılır.

## Uygulama sırası

1. **Hedef inceleme:** AssistFlow şeması, migration geçmişi, tanım kodları, müşteri ve grup eşleşmeleri, mevcut modül verisi, prosedür/Job sürümleri salt-okunur raporlanır. Ortak uygulamanın diğer geliştirmeleriyle farklar çözülür.
2. **Sürümlü paket:** FE/BE commitleri, migration listesi, dar SQL değişiklikleri, tanım/sınıflandırma/aktarım komutları, karar sürümü ve dosya hash'leri sabitlenir. Her adım için önkoşul, beklenen etki, tekrar ve hata sonrası devam yöntemi yazılır.
3. **Prova:** Canlıya benzer izole ortamda paket baştan sona uygulanır; test örneği kayıtlarının yayına karışmadığı, ikinci çalışmada mükerrer kayıt oluşmadığı doğrulanır. Veri ve şema adımları ayrı ölçülür.
4. **Geçiş hazırlığı:** Bakım penceresi, yedek ve geri yükleme doğrulaması, kaynak son kesiti, çalışan MGS Job'ları ve yeni görevlerin açılış sırası belirlenir. Kaynak kullanılmıyor varsayımıyla delta atlanmaz. Paket ve beklenen etki canlı uygulama onayına sunulur.
5. **Şema ve temel eşleme:** Onaylanan migrationlar, tahsilat tanımları, ortak tanım/müşteri farkları ve gerekli senkronizasyon düzeltmesi sıralı uygulanır. Beklenmeyen şema veya eşleşme farkında ilgili adım durur.
6. **Kaynak aktarımı:** Hedef müşteri/üst kart ilişkileri → sözleşmeler → tarifeler → ödemeler → dosya ilişkileri; grup geçmişi/not/fatura gibi ek işler kendi FK bağımlılık sırasına göre uygulanır. Testteki deneme kayıtları taşınmaz. Her veri kümesinden sonra adet/tutar/kimlik/alan mutabakatı yapılır.
7. **Açılış:** Uyumlu FE/BE sürümü, menü/yetkiler ve onaylanan modül ayarları açılır; temel kullanıcı akışları ve gerçek veri sorgu süreleri doğrulanır. Bildirim/otomasyonlar ancak alıcı, tekrar güvenliği ve kaynak Job geçişi tamamlanınca açılır.
8. **İzleme:** Finansal toplamlar, hata oranı, yavaş sorgular, dosya açılması ve Job sonuçları izlenir; açık istisnalar sayımla teslim edilir.

## Geri dönüş

- Veri yüklenmiş finansal tablolarda otomatik Down migration veya toplu silme kullanılmaz.
- Başarısız transaction geri alınır; tamamlanmış adımların audit/makbuz/kimlik eşlemeleri korunur ve yeniden uygulama öncesi kontrol edilir.
- Ortak müşteri güncellemelerinde önceki değerler ve uygulanan değerler saklanır; sonradan değişmiş bir kaydı ezerek geri dönüş yapılmaz.
- Uygulama sürümü geri alınırken şema uyumluluğu doğrulanır. Geçişten sonra girilmiş canlı işlemler varsa eski yedeğe doğrudan dönüş yerine bu işlemler korunarak ayrı kurtarma kararı uygulanır.
- Yedek geri yükleme gerekiyorsa etkilenecek son canlı işlemler ve diğer AssistFlow modülleri belirlenir; tüm veritabanını geri yüklemek varsayılan adım değildir.

## Her yeni DB işi için kapanış kaydı

K03 uygulama: yukarıdaki migration AssistFlowTest'te uygulandı; 56 yeni Customers+GroupParent ve eksik G tipi tek transaction'da kaydedildi. Mevcut müşteri alanları/finansal tablolar değişmedi. Son kontrol 0 yeni/56 uygulanmış/18 inceleme, sahiplik farkı 0, EF model farkı yok. Kaynak hash'i ve uygulama öncesi plan/sonuç dosyaları yerel kanıttır. Ortak müşteri listesinde G tipi üst kart görünürlüğü beklenir; tenant ataması yapılmaz. Otomatik Down/silme uygulanmaz. Canlıda bu migration, G tipi/kart önizlemesi ve API/FE aynı paket içinde ele alınır; test aracı production hedefini reddeder.

K03 önizleme: `tools/Preview-CollectionGroupCustomers.ps1` hem AssistFlowTest hem AssistFlow üzerinde salt-okunur çalıştırıldı. Araç/politika/kaynak/hedef/karar hash'leri hedefe özeldir. Yeniden çalıştırma veri değiştirmez; geri dönüş gerektiren DB yazması yoktur. Canlı uygulama komutu olarak kullanılmaz. Üst kart uygulama yolu ve collection ek kayıt modeli henüz hazırlanmadı. Sonuç: iki ortamda da 56 yeni aday/18 inceleme; farklı unvan adayları veya boş adlar otomatik oluşturulmaz.

Görev tamamlanırken: kaynak migration/script/araç yolu, uygulandığı ortam, önkoşullar, hedef eşleştirme yöntemi, beklenen değişiklik, test sonucu, tekrar davranışı, geri dönüş yöntemi ve canlıda uygulanıp uygulanmadığı kaydedilir. Yalnız testte elle yapılmış ve yeniden üretilemeyen değişiklik tamamlanmış yayın işi sayılmaz.

## Açık hazırlıklar

- [ ] Gerçek AssistFlow şema/migration/prosedür envanteri ve paket kapsamı karşılaştırması.
- [ ] Teste kilitli araçlardan ayrı kontrollü canlı önizleme/uygulama yolu.
- [ ] K03 ve sonraki DB değişikliklerinin envantere eklenmesi.
- [ ] Canlıya yakın ortamda tam prova ve idempotent tekrar kontrolü.
- [ ] CDN ortamı/nesne sahipliği ve canlı URL ilişki planı.
- [ ] FE/BE sürüm paketi, menü/yetki ve senkronizasyon değişikliklerinin son listesi.
- [ ] Son kaynak kesiti, Job geçişi, yedek/geri dönüş ve uygulama onayı.

İlgili kayıtlar: [ana plan](module-development-plan.md), [temel kurulum](collection-foundation-installation.md), [K03](collection-k03-customer-context-2026-09-27.md), [ödeme aktarımı](collection-payment-transfer-2026-09-26.md).
# K06 ek paketi — 29 Eylül 2026

Güncel: K06 net test aktarımı tamamlandı (1.847 fatura / 1.939 ödeme), fatura bazlı mutabakat geçti; kaynak/canlı değişmedi. İkinci migration `20260929084242_AddCollectionInvoiceOperations` yalnız testte uygulandı. İlk fatura migrationından sonra dağıtılır. Ödeme komutları bu audit tablosu olmadan etkinleştirilmez.

`invoice-transfer` yalnız AssistFlowTest'e izin verir; hash-korumalı atomik aktarım, legacy silme izlerini dikkate alma ve kaynak/hedef mutabakatı içerir. Canlı için hedef koruması körlemesine kaldırılmaz: ayrı onay, canlı kimlik eşlemesi/önizleme ve yedekleme gerekir. Audit tablosu gerçek işlem aldıktan sonra Down ile silinmez. İnceleme listesi 3.631 fatura / 3.883 ödeme K12'de kalır. Tarayıcı/oturumlu yetki kabulü yerel uygulama çalışmadığından açık; canlıya çıkış onayı verilmiş değildir.

Önceki aşama notları:

Fatura önizleme aracı eklendi ve test hedef eşlemeleriyle çalıştırıldı: 1.847 fatura / 1.939 ödeme hazır. Bu sayılar test eşlemesine özgüdür; canlıya doğrudan uygulanmaz. Tam kaynak sayıları 5.478 / 5.822. Canlı geçişte korunan müşteri ve para birimi eşlemesi yeniden çıkarılıp önizleme/hash yenilenmelidir. Önizleme aracı yalnız okur; fatura/ödeme aktarımı henüz uygulanmadı.

`20260929080736_AddCollectionInvoices` migrationı yalnız testte uygulandı; canlıda uygulanmadı. collection.Invoice / InvoicePayment ve indekslerini oluşturur, ortak Customer/CurrencyType şemasını değiştirmez. Uygulama yeni sürümü çalıştırılmadan önce hedef şema hazır olmalıdır. Test kurulum aracı `invoice-setup` canlıya karşı çalışmaz; canlı migration mevcut onaylı dağıtım süreciyle, yedek/geri dönüş planı sonrasında uygulanır.

Fatura aktarımı bu pakette yapılmadı. Canlı aktarımda test CustomerId/CurrencyTypeId taşınmaz; hedefe özel eşleme ve kaynak hash'i yeniden doğrulanır. Tablolara finansal veri yazıldıktan sonra Down ile tablo silmek geri dönüş yöntemi değildir; veri yedeği ve mutabakat zorunludur. K06 ödeme komutları/audit/aktarımı ve gerçek veri kabulü tamamlanmadan bu pakete production-ready denmez.
# K08 hazırlık notu — 29 Eylül 2026

- K08 migration sırası: `20260929111421_AddCollectionBankLoads`, `20260929112405_AddCollectionBankReferenceIndexes`. Testte uygulandı; canlıda henüz uygulanmadı. Yalnız collection tabloları/indeksleri etkilenir.
- `bank-setup <Development JSON> --install` yalnız beklenen K08 migrationlarını testte kurar. Parametresiz önizleme MGS'den kimlikleri SELECT ile okuyup hash verir; aynı hash ile çalıştırma hedef collection.BankTransaction/Baseline'a geçmiş korumasını ekler. Araç test-only; canlı için aynı kaynak kesitiyle ayrı gözden geçirilmiş plan gerekir, test CustomerId/ContractId değerleri kopyalanmaz.
- Son kaynak kesiti: 83.387 kayıt/83.387 tekil kimlik/0 boş; hash `27B3A18F5A1448A89CECAEB49A9D87A6D045498A11E83EE55BD8B1C06D4E77CE`. Kaynakta yeni işlemler varsa yayından önce yeniden salt okunur profil ve hash mutabakatı gerekir. Baseline yokken hazır ödeme oluşmaz. Aktarılmayan eski aboneliklerin banka kimlikleri de korumaya dahildir.
- Finansal silme sonrası BankTransaction ve PaymentOperation audit'i korunur. Geri dönüşte bu tabloları Down ile silmek tekrar ödeme riskidir; yedek + mutabakat + uygulama sürümü geri dönüşü tercih edilir. Başarılı/yarım batch'i yeniden çalıştırma yalnız kalan satırları işleyebilir.
- Orijinal kart içeren dosya saklanmaz; izinli ödeme alanlarından üretilen JSON mevcut CDN'ye gider. Sağlayıcı/config değişmedi. Belirsiz upload sonucunda aynı dosya/dönemle tekrar edilir; paylaşılan hash dosyası körlemesine silinmez.
- Test SQL/CDN ve eşzamanlı çift komut kontrolü geçti, geçici kayıtlar temizlendi. Gerçek müşteri dosyası + oturumlu UI/HTTP yetki kabulü yapılmadan canlı yayın onayı verilmez. İadeler/iptaller otomatik ödeme değildir. Ayrıntı: [K08 sonucu](collection-k08-bank-payments-2026-09-29.md).
