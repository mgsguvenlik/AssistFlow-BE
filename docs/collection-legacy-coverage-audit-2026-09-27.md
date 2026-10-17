# Legacy–AssistFlow Tahsilat karşılaştırması

Tarih: 27 Eylül 2026. Kapsam: kaynak kod + canlı MGS nesne tanımları/veri profili + SQL Agent envanteri + AssistFlow FE/BE ve AssistFlowTest. **Bu çalışma analizdir; uygulama, DB, Job veya veri aktarımı değiştirilmedi.**

## 1. Sonuç

**Tahsilat çekirdeği yapılmış; legacy tahsilatın tamamı henüz karşılanmış değil.** Sözleşme/tarife, dönem hesabı, bireysel ve grup takip, tekli/toplu tahsilat, ödeme düzeltme/silme ve sözleşme dosyaları mevcut. Bunlar müşteri kartının bütün bölümleri, kesilen faturalar, banka dosyasından tahsilat ve otomatik grup operasyonlarının tamamlandığı anlamına gelmiyor.

Öncelikli bulgular:

1. Müşteri bazlı **çok satırlı not geçmişi** yok. Ortak Customers.Note aynı şey değil.
2. **Kesilen fatura takibi, faturaya bağlı ödeme ve B/K fatura dosyası yükleme** karşılanmıyor. Bunlar sözleşme ödemesinden ayrı defterler.
3. **GTS/IVR dosyasından toplu ödeme girişi** yok. Mevcut takip satırlarını seçerek tahsil etme ve bir defalık legacy ödeme aktarımı bu işlevin yerine geçmez.
4. **Grup dönem durumunun ekranı/servisi var, geçmişi aktarılmamış:** kaynak 55.597 satır, hedef 0.
5. **Grup/kurumsal üst müşteri kartı, üyeler, üst kartın sözleşmeleri ve yeni üyeye sözleşme mirası** tam karşılanmıyor. Grup toplam ekranı üst müşteri kartı değildir.
6. **Grup müşteri bilgilendirme ekranı ve zamanlanmış tahsilat mailleri** yok. Ortak mail altyapısının olması bu işin yapılmış olduğu anlamına gelmez.
7. Legacy bakım ve müşteri/sözleşme senkronizasyon Job'ları hâlâ etkin. Kaynağı değişmez kabul ederek canlı geçiş yapılamaz.
8. Mevcut kodda karar uyumu/finansal düzenleme açıkları var: boş sözleşme durumu, ücretsiz ödeme işaretinin korunması, geçmiş negatif/sıfır ödemelerin düzeltme davranışı ve liste/detay hesap tarihi farkı.

Zam ve raporların detay denetimi bu raporun dışında. Buradaki `Core/Dashboard` operasyonel tahsilat ekranıdır; `Report/Dashboard` ile karıştırılmadı.

## 2. Kanıt ve sınırlar

- Legacy kaynak: `D:\MGS\MGS-PLATFORM.zip`; 179 kaynak/markup dosyası izole inceleme klasörüne çıkarıldı. Kaynak ZIP değişmedi. Paketler, derlenmiş kütüphaneler ve gizli ayarlar kopyalanmadı.
- Ana ekranların `.aspx` alanları, dinamik alan tanımları, SQL sorguları, callback/kayıt akışları incelendi. Canlı `Admin.Application` menü kayıtlarıyla ekran envanteri karşılaştırıldı.
- MGS `sys.objects`, kolonlar, bağımlılıklar, SQL modül ve trigger tanımları okundu. `msdb` üzerinden Job adımı, etkinlik, gerçek schedule ve son çalışma sonuçları okundu. Hiçbir prosedür/Job çalıştırılmadı.
- AssistFlowTest SQL nesneleri ve `collection` tablo sayımları kontrol edildi. Her iki repository `tahsilat-module`; mevcut izlenen/izlenmeyen değişiklikler korunarak çalışma ağacındaki kod esas alındı.
- Yerel ham kanıtlar: `.tools/collection-audit-{schema,definitions,jobs,target,profiles,menu}.json`. Bunlar `.tools` altında Git dışındadır; kişisel ham veri/Job metinleri paylaşım belgesine taşınmadı.
- Tablo envanteri sayıları `sys.partitions` anlık metadata sayımlarıdır; not, grup takip ve müşteri türü profilleri ayrıca SELECT COUNT ile doğrulandı. Bunlar **aktarılabilir kayıt sayısı değildir**; kapsam/kimlik/tekilleştirme kontrolleri ayrıca gerekir.
- Bu tur browser E2E, yük testi veya yeni build çalıştırılmadı. “Mevcut” kod/veri kanıtıdır; production kabulü değildir. Önceki aktarım ve UI kanıtları kendi raporlarında durur.
- Sunucuların Windows Task Scheduler/Windows Service/IIS kurulumları bu erişimle incelenmedi. ZIP'te `NetcollecApp/Global.asax` var, işaret ettiği uygulamanın C# gövdesi yok; buradan faal online tahsilat varsayılmadı. SQL tanımını okumak dış SIMS/Manitou/Netsis ortamlarının sağlıklı çalıştığını kanıtlamaz.

### Durum sözlüğü

| Durum | Anlam |
| --- | --- |
| Mevcut | Ekran/servis/model karşılığı bulundu; aşağıdaki sınırlara tabi |
| Kısmi | Temel karşılık var; belirtilen iş/alan/aktarım eksik |
| Yok | İncelenen yeni modülün route/API/model/iş akışında karşılık yok |
| Bilinçli fark | Müşteri kararı veya hatalı/kullanım dışı legacy yapı; otomatik kopyalanmaz |
| Teyit | Gerçek kullanım/finansal davranış kararı veya dış ortam kanıtı gerekir |

## 3. Ekran envanteri ve yeni karşılıkları

Legacy yollar `Pages/` altındadır. Yeni yollar CRM → Tahsilat menüsündedir; ortak müşteri yönetimi istisnası ayrıca belirtilmiştir.

| Legacy ekran | Legacy işlev | AssistFlow karşılığı | Sonuç |
| --- | --- | --- | --- |
| `Core/Customer.aspx` | N bireysel kartı; kimlik, not, sözleşme/tarihçe, ödeme, fatura, dosya | Ortak `/customer-management/customer-list`, `/customer-management/customer-edit/:id`; tahsilatta `/crm/collections/contracts` ve `contracts/:id` | Kısmi; müşteri merkezli bütünleşik tahsilat kartı yok |
| `Core/CustomerGroupMember.aspx` | GM üyenin aynı finansal alt bölümleri; grup ilişkisi | Ortak CustomerGroupId; grup takipten üye sözleşmesine iniş | Kısmi; tüm üyeler/kart/not/fatura bütünlüğü yok |
| `Core/CustomerGroup.aspx` | Type=G üst grup kartı, üye listesi/sayısı, kendi sözleşme/ödeme/not/faturaları | Ortak müşteri grupları ve takip grup özeti | Kısmi; üst grup müşteri varlığıyla eşdeğer değil |
| `Core/CustomerCorporate.aspx` | Type=G ve kurumsal grup kodu; üye listesi + üst kart finansal alanları | Ortak CustomerType/Kurumsal kaydı var; ayrı tahsilat karşılığı yok | Eksik; kurumsal süreç tamamlandı denemez |
| `Core/Dashboard.aspx` | Bireysel sözleşme-dönem borç takibi, geniş arama/filtre, tek/toplu tahsil | `/crm/collections/tracking` bireysel görünüm; `CollectionTrackingService`, `CollectionPaymentService` | Kısmi; hesap/ödeme var, dönem aralığı ve filtre/iletişim alanı farkları var |
| `Core/DashboardGroup.aspx` | Üye sözleşme-dönem satırları, kurumsal/grup ayrımı, durum/açıklama, ödeme | Takip grup özeti → üye sözleşmeleri; `/crm/collections/tracking/:id`, `CollectionGroupFollowUpService` | Kısmi; geçmiş/durum listesi/otomatik Ödendi farkları var |
| `Core/InvoiceFollow.aspx?Type=B` | Bireysel kesilen fatura, ödenen/kalan, ödeme girişi | Yok | Eksik |
| `Core/InvoiceFollow.aspx?Type=K` | Kurumsal kesilen fatura takibi | Yok | Eksik; B/K ayrı kapsam korunmalı |
| `Core/InvoiceFollowLoad.aspx?Type=B/K` | Excel → cari eşleştirme → hata/kuyruk → kesilen fatura | Yok | Eksik |
| `Core/Payment.aspx` | Banka GTS/IVR Excel'i → BulkPayment → kontrol → Payment | Yok | Eksik; takipteki “toplu tahsil et” ile farklı |
| `Crm/CollectiveMail.aspx` | Grup müşteri bilgilendirme; şablon, dönem, müşteri, test gönderimi | Ortak mail altyapısı var; tahsilat ekranı/üreticisi yok | Eksik |
| `Crm/MailTemplate.aspx` + `Definition.CollectiveMail` | Bilgilendirme şablonu ve sonraki dönem seçimi | Mevcut ortak mail yapıları yeniden kullanılabilir | Tahsilat bağlantısı/taşınmış yedi şablon akışı yok |
| `Crm/Company.aspx` | Ortak CRM müşteri kartı; Core finansal alt sorgularını da içeriyor | Ortak müşteri yönetimi var | Yeni bir ikinci müşteri tablosu yapılmaz; finansal alt bölümler yukarıdaki eksiklerle aynı |
| `Definition/ContractStatus.aspx`, `SubscriptionStatus.aspx`, `PaymentMethod.aspx`, `PaymentType.aspx` | Tanım CRUD | `collection` tanımları + `GET /api/collections/definitions` ve seçiciler | Veri/okuma mevcut, tahsilata özel tanım yönetimi CRUD ekranı yok |
| `Definition/Group.aspx`, `ServiceType.aspx`, `Currency.aspx`, `City.aspx`, `CrmSystems.aspx` | Ortak tanımlar | CustomerGroups, ServiceType, CurrencyType ve ortak şehir/sistem yapıları | Yeniden tablo açılmaz; grup hiyerarşisi/anlam ve alan eşleşmesi ayrıca korunmalı |
| `Definition/Bank.aspx` | Sözleşme kredi kartı bankası | Yeni kart/POS kapsam dışı | Bilinçli fark; banka dosyası yükleme işi bundan ayrı |
| `Core/Supplier.aspx`, tedarikçi tanımları | Tedarikçi kartı, stok/tedarik bağlamı | CRM/satın alma sınırı | Tahsilat eksiği olarak yeni kapsam yaratılmadı |
| `Core/Raise.aspx`, `dbo.Raise/RunRaise`; `Pages/Report/*` | Zam ve raporlar | Yeni zam/ödeme/sözleşme rapor route'ları mevcut | Ayrıntılı denkliğe bu tur hüküm verilmedi; sonraki faz |

Legacy ekrandaki kısayollar, sütun göster/gizle, Excel çıktısı ve aynı sayfada tablo/form görünümü ortak WebForms kabuğunun özellikleridir. Yeni sistemde ortak DataTable, server-side sayfalama, aramalı select, `/id` detay, Geri ve sekmeler kullanılıyor. Eski popup/pencere kabuğunu birebir taşımak gerekmiyor; işlev eksikleri aşağıda ayrı tutuldu. “Aktivite Raporu” başlıklı gizli-header panel, müşteri kimlik formunun kabuğudur; bağımsız yapılmış bir tahsilat aktivite raporu sayılmadı.

## 4. Customer.aspx alan/element karşılaştırması

### 4.1 Ana müşteri bilgileri

Kaynak: `Core/Customer.aspx.cs:842–914`; grup/kurumsal farkları `CustomerCorporate.aspx.cs:814`, `CustomerGroup.aspx.cs:815`, `CustomerGroupMember.aspx.cs:789`. Yeni: `Model/Concrete/Customer.cs`, FE `CustomerForm/components/*` ve `Data/Concrete/EfCore/Collections/CollectionContractReadQuery.cs`.

| Legacy alan | Yeni karşılık | Açık nokta |
| --- | --- | --- |
| CustomerID; CreatedBy/On, ModifiedBy/On | Customers.Id ve mevcut audit alanları | Ortak müşteri audit'i var; legacy tüm işlem günlüğü taşınmış değil |
| SubscriberNo | Customers.SubscriberCode | Onaylı eşleştirme anahtarı; belirsizler aktarım kararlarına tabi |
| Name | SubscriberCompany | Ortak müşteri ekranında mevcut |
| AccountNo / Cari Kod | Eşdeğer doğrulanmış mali cari alanı yok | Fatura yüklemedeki cari eşleştirme için kritik; CustomerShortCode/OracleCode/LocationCode varsayımla yerine konmaz |
| GroupID, GroupCode, GroupName | CustomerGroupId → CustomerGroups.Id/Code/GroupName | Legacy GroupID bazen Definition.Group, GM'de Core.Customer üst kartıdır; ID'ler doğrudan kopyalanamaz |
| GtsNo, IvrNo | collection.Contract.GtsNo/IvrNo; detay kimlik formu | Legacy vCustomer bunları sözleşmelerden birleştirir; müşteride ikinci kolon gerekmez. Takipte GTS/IVR araması yok |
| CityID/CityName, District, Adress | Customers.City/District/SubscriberAddress | Ortak formda var; legacy FK → yeni metin/ortak tanım eşleşmesi ayrı veri kontrolü |
| PriorityID/PriorityName | Eşdeğer tahsilat müşteri önceliği yok | Gerekli ise collection genişletmesi; servis talebi önceliğiyle karıştırılmaz |
| Contact; Telephone, Telephone2; eMail | ContactName1/2, Phone1/2, Email1/2 | Ortak formda var; tahsilat listesi/özetinde hızlı iletişim karşılığı eksik |
| Description | Customers.Note kısa not alanı | Yalnız kavramsal karşılık; legacy Description aktarımı garanti edilmiş değil. Çok satırlı Core.Comment yerine geçmez |
| PreviousAccountNo | Eşdeğer operasyonel alan yok | Ham migration kaynakları aslı korur; kullanıcı araması/görüntüsü yok |
| SimsID | Eşdeğer eski SIMS kimliği alanı yok | Yeni SerialNo/Manitou kimliği otomatik eşdeğer kabul edilmez |
| Type=N/GM/G/A | CustomerType + onaylı grup kodu politikası | Legacy ve yeni müşteri tipi birebir aynı kategori sistemi değil |
| GroupCardNo, MemberCount (G/K kartları) | CustomerGroups.Code; üyeler CustomerGroupId ile bulunabilir | Üst müşteri kartı, üye sayısı ve kendi finansal bilgileri için bütünleşik UI yok |
| TaxAdministration, TaxNo, ControlType | vCustomer/Company.aspx ve dış senkronizasyon alanları | Customer.aspx ana input listesinde değiller; ortak Customer modelinde birebir mali/legacy alan karşılığı yok. Yeni alan eklemek için onay gerekir |
| ChildModifiedOn, IsCalculate | Kaynak invalidation/cache alanları | On-demand hesap nedeniyle yeni tabloda birebir gerekli değil; iş/audit anlamı ayrıştırılır |

Ortak müşteri yönetimi tenant alanına sahip olsa da tahsilat sorgu/işlemlerinin tenant'sız kalması mevcut karardır. Eksik alanı tamamlamak adına mevcut servis talebi/ortak müşteri davranışı değiştirilemez.

### 4.2 Alt paneller, sözleşme ve ödeme alanları

| Legacy bölüm / alanlar | Yeni karşılık | Durum / eksik |
| --- | --- | --- |
| **Notlar:** CommentID, Comment, oluşturan/değiştiren/tarihler; ekle/düzelt/sil | Customers.Note yalnız tek metin; CustomerActivityLog servis talebi aktivitesi | **Yok:** çok satırlı tahsilat notu, liste/CRUD ve 12.820 notun aktarımı |
| **Müşteri Satış Sözleşmeleri:** ContractID/No, müşteri, ServiceType | Sözleşme listesi + create/detail, collection.Contract | Mevcut; Product bağı eklenmemiş. Legacy kaynak ContractID migration audit/map'te; No/InheritContractID için yeni operasyonel alan yok |
| Başlangıç ay/yıl, bitiş | StartDate/EndDate; yeni kayıtta gerçek gün, tarife ankrajı | Oluşturma mevcut; sonradan başlangıç/bitiş düzeltme/sonlandırma komutu yok |
| SubscriptionStatus Aktif/Donuk | CollectionSubscriptionService, Abonelik İşlemleri | Mevcut; bugün etkili freeze/reactivate, aynı gün tekrar ve geçmiş tarih düzeltme kısıtlı |
| ContractStatus VAR/YOK/boş | collection.ContractStatus; oluşturma seçicisi | Aktarım kuralı ile yeni kayıt/durum komutu tutarlılığı eksik; §9 |
| PaymentMethod (POS/havale vb.) | collection.PaymentMethod ve Contract.PaymentMethodId | Tarihsel veri korunuyor; create DTO/formda yok, kimlik düzenlemede yok, detail projection'da yok; **kullanım kısmi** |
| PaymentType 1/2/3/4/6/12/24/36 ay | ContractRatePeriod.PaymentFrequencyId | Mevcut; dönem tutarı aylığa bölünmüyor. Mevcut sözleşmenin sıklık/para birimi değişimi komutu yok |
| Amount, CurrencyID | Tarife dönemindeki Amount/CurrencyTypeId | Mevcut; zam ayrıntısı bu denetimde kapsam dışı |
| CreditCardBankID, CreditCardExpirationDate | Yok | Kart/online POS müşteri kararıyla kapsam dışı; kopyalanmayacak |
| FileAttachmentName/Path, yükle/aç, Dosya Sil | ContractAttachment; Dosyalar sekmesi, mevcut CDN | Liste/yükle/aç ve net sözleşmelerin aktarımı var; silme/değiştirme karşılığı yok |
| **Sözleşme tarihçesi:** başlangıç/bitiş, PaymentType, Amount, Currency, ProcessType, Description | Tarife Geçmişi; BillingBehavior, ChangeReason | Okuma ve kontrollü yeni dönem var. Keyfî geçmiş CRUD/fiziksel düzeltme yok; güvenli geçmiş düzeltme kapsamı tamamlanmalı |
| ProcessType Başlangıç/Fiyat Artışı/Fiyat İndirimi/Ücretsiz/Hizmet Dondurma | Billable/Free/Suspended + gerekçe | Finansal davranış karşılığı var; kaynak işlem metni/audit gösterimi tam eşdeğer değil; null/çakışmalar karar kuyruğunda |
| **Müşteri Ödemeleri:** ödeme ID, sözleşme seçimi, dönem ay/yıl, tarih, tutar, açıklama, Free | Sözleşme detayı Ödeme Hareketleri; tekli/toplu giriş/düzeltme/silme | Mevcut ama sözleşme merkezli. Müşterinin tüm sözleşmelerini kapsayan kart listesi yok. Sözleşmeler arası ödeme taşıma yok; Free/negatif düzeltme farkı §9 |
| **Müşteri Faturaları:** SozlesmeNo, FaturaNo, FaturaTarihi, OdemeTutari, EntegreDurumu | Yok | Legacy markup var ama dört kartta `FaturaGrid` için çalışan data binding/CRUD bağlantısı bulunmadı; Core.Invoice da 0. **Çalışan süreç diye varsayılmamalı**, kullanım teyidi gerekir |
| **Kesilen Fatura Takibi:** no/tarih/tutar/kur/ödenen/kalan/açıklama/proje | Yok | Çalışan Core.InvoiceFollow/vInvoiceFollow süreci; sözleşme borcundan bağımsız eksik |
| **Kesilen Fatura Ödemeleri:** fatura seçimi/tarih/tutar/açıklama | Yok | Core.InvoiceFollowPayment ayrı ilişki; collection.Payment'e karıştırılamaz |
| **Müşteri Dosyalar** dosya yöneticisi | Yalnız sözleşmeye bağlı CDN dosyaları var | Legacy `~/Content/Aktivite/{CustomerID}/` ayrı klasörü; `Contract.rar` aktarımı bunu tamamlamış sayılmaz. Fiziksel varlık bu tur doğrulanmadı |
| Kayıt bilgisi; kayıt/alt kayıt değişiklikleri | Ortak audit + PaymentOperation + migration kaynak audit'i | Kısmi; eski Admin.Log veya tüm değişiklikleri sunan müşteri tahsilat günlüğü yok |
| SIMS senkronizasyon butonu | Ortak Manitou müşteri staging'i var | Legacy SIMSTOMGS sözleşme etkilerinin eşdeğeri değil; §8 |
| Yeni/sil müşteri, yeni/sil sözleşme | Ortak müşteri CRUD; yeni sözleşme var | Tahsilatta sözleşme silme/bağımlılık yönetimi yok. Legacy kontrolsüz müşteri/sözleşme silme birebir kopyalanmaz |

Kaynak panel kanıtları: `Customer.aspx:909` notlar; `1022` sözleşmeler; `1697` ödemeler; `1923` bağlı olmadığı saptanan FaturaGrid; `2034` kesilen faturalar; `2194` fatura ödemeleri; `2340` müşteri dosyaları. `Customer.aspx.cs:1091` çalışan alt grid kayıtları, `1066` müşteri dosya kökü, `4055` SIMS çağrısı. Kurumsal/grup kartlarında aynı alt finansal paneller ayrıca incelendi.

## 5. Bireysel, kurumsal ve grup süreçleri

### 5.1 Bireysel

- Legacy `Type='N'`, `Customer.aspx` ve `Dashboard.aspx`; 4.532 müşteri, bu müşterilere bağlı 5.643 sözleşme (tüm kaynak; aktarım kapsamı değil).
- Yeni bireysel kapsam MGS/MGSK/MGI/MGIK/STB/STBG kodları + uygun CustomerType ile belirleniyor. Bu müşteri kararı legacy türün üzerinde önceliklidir.
- Sözleşme, tarife, dondurma/aktifleşme, dönem/devirli bakiye, kısmi/tek/toplu ödeme ve dosyalar mevcut.
- Notlar, kesilen faturalar/fatura ödemeleri, dosyadan GTS/IVR tahsilat, bütünleşik müşteri kartı ve bazı düzeltmeler eksik.

### 5.2 Kurumsal

- Legacy'de **Kurumsal = Type A değildir.** `CustomerCorporate.aspx.cs:111` filtresi `Type='G'` ve `GroupCardNo`nun `Definition.Group.GroupType='Kurumsal'` kodlarında bulunmasıdır.
- Kaynakta bu koşulda 18 üst kart / bunlara doğrudan bağlı 8 sözleşme var. Type A ayrıca 5.345 müşteri / 0 sözleşme; sırf A harfinden kurumsal tahsilat sonucu çıkarılamaz.
- Bireysel/kurumsal kesilen fatura ayrımı ayrı `InvoiceFollow.Type` alanında; kaynakta 4.674 Bireysel, 804 Kurumsal fatura var. Bu sınıflama müşteri türüyle aynı alan değildir.
- Yeni operasyonel sınıflama yalnız Individual/Group/Excluded/Unknown. `KRMSL01` üçüncü bir tahsilat sınıfı olarak sorguya alınmıyor. Bu nedenle ortak CustomerType'ta “Kurumsal” satırı olması legacy kurumsal akışın taşındığını kanıtlamaz.
- FIN*/YKB*/EMK ve diğer onaylı dışlamalar hakediş nedeniyle korunacak; “eksik kurumsal ekran” gerekçesiyle kapsam dışı müşteriler yeniden dahil edilmeyecek.
- Yapılacak: kurumsal üst kart/fatura envanteri onaylı kod politikasıyla kesiştirilecek; gerçekten kapsamda kalan süreçler ortak yapıda karşılanacak. Yalnız hakediş kapsamındaysa kontrollü dışlama raporlanacak. Yeni bir kiracı/tenant tahsilat sistemi kurulmayacak.

### 5.3 Grup ve grup üyesi

- Legacy üst kart G, üye GM. `CustomerGroup.aspx` G olup GroupCardNo'su Definition.Group'ta bulunmayanları gösterir. GM'nin GroupID'si **üst Core.Customer.CustomerID** değeridir. Kaynak toplam G 237 müşteri/139 sözleşme; GM 9.732 müşteri/3.092 sözleşme.
- Yeni CustomerGroups grup tanımı ve CustomerGroupId üyelik ilişkisi, onaylı GM sınıflaması, para birimi bazlı grup toplamı ve üyelerin sözleşmelerine iniş mevcut.
- Eksik: grubun kendi müşteri kartı/iletişim/alacaklı bilgisi, sözleşmesi olmayan üyeler dahil tam üye listesi, üst sözleşme–üye ilişkisi ve yeni üyeye sözleşme oluşturma süreci.
- `SIMSTOMGS` yeni GM için üst sözleşmeden kopya sözleşme ve Başlangıç tarihçesi oluşturuyor. Yeni ortak Manitou staging'i bunu yapmıyor.
- `RefreshMemberContract` / `InsertRefreshMemberContract` ise tüm üye sözleşme/tarihçesini silip yeniden kuruyor; tanımın kendisi **istenmediği için kullanım dışı** olduğunu söylüyor, grup ekranı çağrıları yorumda. Bu tehlikeli tam eşitleme yeni eksik iş olarak uygulanmayacak.
- Grup dönemi etiketleri Mahsup/İptal/Beklemede/Ücretsiz/Fatura Kesildi/Ödendi/Onaylandı mevcut. Kaynak 55.597 takip kaydı, 2.638 farklı sözleşme; yeni ContractPeriodFollowUp **0**. Önce korunan sözleşme eşlemesiyle aktarım kapsamı çıkarılmalı.
- Legacy DashboardGroup tahsilat butonu ayrıca durum 6/Ödendi yazıyor. Yeni ödeme servisi takip etiketi yazmıyor; etiket ayrı manuel komut. Finansal bağımsızlık korunmalı; otomatik işaretlemenin müşteri beklentisi ayrıca netleştirilmeli.

## 6. Operasyonel takip farkları

| İş | Legacy | Yeni durum |
| --- | --- | --- |
| Hesap yöntemi | Cursor + TarihTablosu; Report.Follow/FollowGroup cache; sayfa açılışında RunFollow | SQL tabanlı seçili dönem sorgusu + ayrık tarife takvimi; gereksiz cache/Job taşınmadı |
| Dönem görünümü | Geçmiş dönemler birlikte; ilk ay tarihi `< getdate()+7` filtresi | Tek seçili ay; detayda devirli bakiye var. Tüm geçmiş açık dönemlerin tek iş listesi yok |
| Finansal alan | ContractAmount, PaymentAmount, Amount=kalan, PaymentDate | Tahakkuk/ödenen/kalan/para birimi/vade var; son ödeme tarihi ve bazı iletişim alanları listede yok |
| Arama | Abone/ad, GTS/IVR, grup, telefon/e-posta, açıklama/şehir | Takipte abone/ad/grup; CustomerId/GroupId/ServiceType/Currency filtreleri |
| Filtre | Üyelik durumu; ödeme yöntemi hariç tutma; grup türü/durum sütun filtreleri | Bakiye filtresi var; üyelik/ödeme yöntemi/grup etiketi/kurumsal tür filtreleri yok |
| Grup durum çalışma listesi | Durum/açıklama ana gridde | Ayrı `/tracking/:id` dönem formunda; listede durum kolonu/filtre/toplu etiket yok |
| Tahsil et | Pozitif kalan tutarı döneme yazar | Mevcut; seçili en fazla 50 kayıt, güncel bakiye ve retry kontrolü |
| Tarih hesabı | Ay başlangıcı ağırlıklı, 2016 alt sınırı; grup fonksiyonunda 2023 sonrası ve güncel Aktif filtresi | Gerçek yenileme günü, açık tarife aralıkları; donuk geçmişi güncel duruma bakarak silmeme kararı |
| İhracat | Grid Excel | Takipte aynı sorgunun CSV çıktısı mevcut; legacy grid düzeni birebir taşınmadı |

2016/2023 sabit tarih sınırları, grubun tüm geçmiş borcunu güncel Aktif filtresiyle kaybetme, SQL string birleştirme ve belirsiz TOP 1 eşleştirme hataları korunacak alışkanlık sayılmamalı. Buna karşılık geçmiş açık dönemlere kolay erişim ve aramada gerekli iş alanları gerçek eksiktir.

## 7. DB karşılıkları ve aktarım durumu

### 7.1 İş verisi

| Legacy nesne / anlık toplam | Yeni karşılık | Sonuç |
| --- | --- | --- |
| Core.Customer 19.917 | dbo.Customers + CustomerGroups + CustomerType; MigrationSourceRow | Onaylı eşleşme/sınıflama; bütün müşteriler yeni yaratılmadı |
| Core.Contract 9.510 | collection.Contract 4.479; 4.477'si aktarım | Kısmi/onaylı alt küme; kalan kapsam/karar istisnaları |
| Core.ContractHistory 34.487 | ContractRatePeriod 17.403; 17.401 aktarım | Kısmi; tarihçe kararları/süreç türleri ve çakışmalar ayrıca |
| Core.Payment 242.459 | Payment 139.678 | Netleşen alt küme aktarıldı; kapsam dışı/eski abonelik ödemeleri MGS'de kalır |
| Core.Comment 12.820 | Yok (Customers.Note eşdeğer değil) | Aktarım ve operasyon eksik |
| Core.FollowGroupStatus 55.597 | ContractPeriodFollowUp 0 | Model/API/UI var, aktarım yok |
| Core.Invoice 0 | Yok | Kaynak kartta bağlanmamış bölüm; aktif işlev teyidi |
| Core.InvoiceFollow 5.478 | Yok | İş süreci + aktarım eksik |
| Core.InvoiceFollowPayment 5.822 | Yok | Ayrı fatura ödeme defteri eksik |
| Core.InvoiceFollowLoad 12.635 | Yok | Dosya kuyruğu, hata/başarı/geçmiş ve aktarım izi eksik |
| Core.BulkPayment 83.294 | Yok | 63.387 GTS + 16.546 IVR dönüştürülmüş; 2.334 GTS + 1.027 IVR başarısız. Bu profilde Kuyrukda yok; operasyon işlevi yine eksik |
| Contract dosya metadata'sı | ContractAttachment 1.830 | 1.119 fiziksel dosyadan 1.830 ilişki test CDN'e aktarılmış; mevcut aktarılmış sözleşme kapsamı |
| Content/Aktivite/{CustomerID} | Yok | Ayrı müşteri dosyaları; kaynak dosya varlığı ve kapsamı açık |
| Core.ContractSil 7.821 / ContractHistorySil 19.435 | Yok | Arşiv/silinen kopya olarak ayrı tutulur; tekrar aktif sözleşme diye aktarılmaz |
| Admin.Log | Ortak audit + PaymentOperation + migration audit | Eski tahsilat log aktarımı ve müşteri üzerinden tarihçe erişimi yok |
| Report.Follow 303.658 / FollowGroup 49.497 | CollectionTrackingService | Hesaplanmış cache satırlarını ödeme/borç ana kaydı diye taşımak gerekmiyor |

**Ödeme mutabakatı:** 139.678 aktarım, hedef sözleşmeli kapsamda 1.800 inceleme, 71.234 dışlanmış sözleşme, 27.607 bekleyen sözleşme, 2.127 kaynak sözleşme yok, 13 eski kesit dışında = 242.459. Önceki aktarım sonucu ve para birimi tutarları [ödeme aktarım raporunda](collection-payment-transfer-2026-09-26.md). “Tüm ödemeler taşındı” yerine “netleşen alt küme taşındı” ifadesi kullanılmalı.

Canlı sözleşme/tarihçe sayıları staging'deki 9.498/34.450 kesitinden farklı. Bunun nedeni tek başına bu sayımla belirlenemez; ancak yeniden kesit/son değişiklik mutabakatı gereğini destekler. Müşteri kararı bekleyen kayıtlar bu audit nedeniyle tekrar içeri alınmadı.

### 7.2 Tanımlar ve sorgu nesneleri

| Legacy nesneler | Yeni karşılık / işlem |
| --- | --- |
| Definition.ContractStatus / SubscriptionStatus | collection.ContractStatus (3) / SubscriptionStatus (2) |
| Definition.PaymentType / PaymentMethod / GroupStatus | collection.PaymentFrequency (8) / PaymentMethod (10) / GroupStatus (7) |
| Definition.ServiceType / Currency | Mevcut dbo.ServiceType/CurrencyType, onaylı migration eşlemeleri |
| Definition.Group / vGroup | CustomerGroups + kod politikası; legacy üst Core.Customer bağını ayrıca ele almak gerekir |
| Definition.City/CityRegion/System/Priority | Ortak karşılıklar öncelikli; Priority tahsilat alanı ve şehir kimliği eşliği kısmi. Definition.System servis/CRM bağlamıyla karıştırılmaz |
| Definition.Bank | Kart alanı müşteri kararıyla dışarıda |
| Definition.CollectiveMail + Crm.vCollectiveMail + Workflow.MailTemplate | Grup bilgilendirme akışına kaynak; yeni tahsilat bağlantısı yok |
| Core.vCustomer/vContract/vContractHistory/vCustomerContract/vCustomerContractHistory | Ortak EF müşteri/kontrat/tarife projeksiyonları ve okuma servisleri; view'ların birebir kopyası gerekmez |
| Core.vPayment + dbo.vPaymentCurrency | Payment.CurrencyTypeId; aktarımda belirsiz kur TOP 1 ile seçilmez; tarihsel karşılaştırma raporu mevcut |
| Core.vFollowGroup | Takip finansal sorgusu + ayrı GroupFollowUp servisi; listeye durum birleşimi ve tarihçe aktarımı eksik |
| Core.vInvoiceFollow/vInvoiceFollowLoad/vInvoiceFollowPayment | Karşılık yok; fatura işiyle birlikte yapılmalı |
| dbo.fnTahsilatTakibi / fnTahsilatTakibiGrup / TarihTablosu | CollectionTrackingService + CollectionAccrualRules/CollectionPeriodRules/CollectionStartRules |
| dbo.fnContractPaymentV1 / fnTahsilatTakibiV1 / fnTahsilatTakibiOtomatikOdemeV1 | Eski alternatif hesaplar envantere alındı; mevcut Core dashboard/Job aktif yolu RunFollow → güncel fonksiyonlar. V1 adından aktif online ödeme sonucu çıkarılmadı |
| Report.Customer/CustomerContract/CustomerPayment/Dashboard/NetsisInvoice ve pivotlar | Rapor fazında ayrıntı; operasyonel fatura eksikleri rapor fazına ötelenmez |
| Core.MasterUpdate_Contract / ContractHistory / Payment; Core.CustomerLog | Kaynak müşteri IsCalculate/ChildModifiedOn + Admin.Log. Yeni hesap invalidation istemiyor; transaction/audit kısmen karşılıyor; eski log aktarımı yok |

Trigger'lar inserted/deleted içinden scalar değişkene tek kayıt seçiyor; çok satırlı işlemin tam audit'ini garanti etmiyor. Bu desen taşınmamalı. `ContractRatePeriod` para/tarih/ankraj, `PaymentOperation` kalıcı makbuz, `Migration*` ham kaynak/eşleme/karantina tabloları yeni tasarımın gerekli yapılarıdır; legacy'de birebir isimli tablo aranmamalı.

## 8. Job ve otomasyon denetimi

### 8.1 Canlı SQL Agent bulguları

Saatler SQL Agent'ın kayıtlı sunucu saatidir. Başarılı son çalışma, her satırın doğru sonuçlandığı veya dış mailin alıcıya ulaştığı anlamına gelmez.

| Job / gerçek takvim | Okunan iş | Yeni sistem karşılığı / sonuç |
| --- | --- | --- |
| **Platform Tahsilat Bakım**, etkin, günlük 01:00; son başarı 26 Eylül 01:00 | RunFollow @Force=1 + RunFollowGroup @Force=1; Report cache'lerini günceller | Sorgu sırasında hesaplama var; aynı Job gereksiz. Büyük veri performansı ve yeni ay otomatik hesap kabulü yine gerekli |
| **Platform SIMStoMGS-MANITOUTOMGS**, etkin, saatlik; son başarı 27 Eylül 00:00 | Adına rağmen adım yalnız SIMSTOMGS çağırıyor; müşteri kimliği/iletişim, sözleşme durum/bitiş, yeni GM ve miras sözleşme/tarihçe | Ortak Manitou müşteri staging'i **kısmi karşılık**; tahsilat sözleşmesine bu iş akışı bağlı değil |
| **Platform GrupTopluMail**, etkin, günlük **08:00**; son başarı 26 Eylül 08:00 | spCollectiveMailControl + spWorkflowMail | Tahsilat bilgilendirme Job/üreticisi yok. Aynı Job'daki teklif iş akışı maili ayrı CRM işi |
| **Platform NetsisToPlatfromJob**, etkin, 5 dakikada bir; son başarı 27 Eylül 00:10 | NETSIS_TO_PLATFORM, MGS2026 stoklarından Crm.Product/fiyat güncelleme | Tahsilat faturası/ödemesi entegrasyonu değil; ürün işinin sınırında. Bunu fatura eksikliği kapandı saymak yanlış |
| **Manitou2Flowassist**, etkin, 10 dakikada bir; son başarı 27 Eylül 00:10 | **AssistFlow** DB'sinde stg.sp_RunMainIntegration | AssistFlowTest'te aynı otomatik yürütmenin kanıtı değil. Testte sp_SyncCustomers/Groups var, sp_RunMainIntegration yok; collection sözleşme otomasyonu bulunmadı |
| **SIMS REPORT DATA**, etkin, saatlik | FillSimsStatus; SimsStatus/SimsStatusDetail aylık snapshot | Abone sayısı/rapor bağımlılığı; ayrıntılı rapor dönüşümü sonraki faz |
| **GrupAboneSayilariMail**, etkin, günlük 08:00, dört adım | Manitou grup/abone snapshot, Mgs_Corporate/master mail adımları, tarihsel abone sayısı | Abone sayısı/operasyonel rapor bağımlılığı; finansal Payment üretmez. Sonraki rapor/operasyon sınırı |
| **Platform Mgsk Abone Adedi**, etkin, gerçek saat **00:06** | SimsStatus aktif abone toplamını maille gönderir | Rapor/bilgilendirme; schedule adındaki “06.00” yürütme saati değildir |
| Platform SIMSServiceToMGS; spServiceReminder | Servis kayıt senkronizasyonu / servis hatırlatma | Mevcut servis talebi alanı; yeni tahsilat işi olarak kopyalanmaz |
| Platform Takvim Hatırlatma | Crm.Calendar / spCalendarMail | Ortak takvim; tahsilat borç Job'ı değil |
| RefreshManitouLogSynonym | Manitou log kaynağı rotasyonu | Dış entegrasyon altyapısı; borç Job'ı değil |
| WebSiteToLead | Web lead aktarımı; son görülen çalışma başarısız | Tahsilat dışı; bu tur müdahale edilmedi |
| DBA backup/log/mirror, index, policy bakım Job'ları | Altyapı bakımı | Tahsilat işlevi olarak taşınmaz; canlı geçişte operasyon ekibiyle koordine edilir |

`RunFollow` dalı tam yenilemeyi `@Force=-1` ile seçiyor; Job ise 1 gönderiyor. `RunFollowGroup` parametresi tinyint olmasına rağmen -1 dalı içeriyor. Job adına veya Force değerine bakıp “her gece tam rebuild” sonucu çıkarılmamalı. Gerçek adım seçili IsCalculate müşterilerinde delete/insert cache hesabıdır.

### 8.2 Önemli otomasyon bağımlılıkları

**A. Borç hesabı:** legacy Contract/History/Payment trigger'ları → Customer.IsCalculate → RunFollow/Group → Report.Follow/Group → ekran. Yeni sistem tarife/ödeme verisini doğrudan okuyor. Borç oluşturma Job'ı yok diye hesaplamanın yapılmadığı söylenemez; karşılık mimari olarak değiştirilmiş.

**B. SIMS sözleşme etkisi:** `SIMSTOMGS:11` SIMS kimliğini eşler; `24–34` bazı abone numaralarını PreviousAccountNo'ya alıp boşaltır; `41–78` sözleşme durum/bitişini etkiler; `159–263` müşteri açar; `268–363` yeni GM'ye üst sözleşme/tarihçe üretir. Yeni Manitou servisinin MonitoringStatus'ı Customers'a taşıması, collection.SubscriptionStatus/tarife aralıklarını yönetmesi değildir. Otomatik borç durdurma/açma için hangi dış olayın finansal yürürlük sayılacağı belirlenmeden senkronizasyona yazma eklenmemeli.

**C. Grup mailleri:** yedi CollectiveMail tanımı, grup üst kartı e-postası, dönem, GÖZLEM hizmeti, aktif/ücretsiz olmayan pozitif kalan ve Fatura Kesildi durumunu kullanır. `spCollectiveMailControl` gerçek koşulları: 15 yenileme, 8 yenileme hatırlatma + fatura kesimi, **25** ödeme hatırlatma (yorum 28 diyor), ayın son günü hizmet kesintisi/iptal bilgilendirmesi, 20 fatura ödeme hatırlatması. İptal adlı mail **otomatik sözleşme iptali değildir**; okunan akış mail gönderir. Yeni karşılıkta mevcut MailOutboxDispatcher kullanılmalı; ayrı mail altyapısı kurulmaz. Alıcı/şablon/idempotency ve test önizleme doğrulanmadan gönderim açılmaz.

**D. Zamanlanmış olmayan kuyruklar:** BulkPayment ve InvoiceFollowLoad dönüşümleri ekran callback'lerinden çalışıyor. İşlemde “kuyruk” kelimesi olması otomatik Job olduğu anlamına gelmiyor. GTS/IVR ve fatura yükleme eksikleri ayrı UI/servis işleri.

**E. Ortak AssistFlow worker'ları:** Autofac'ta MailOutboxDispatcher, SlaNotificationDispatcher, ManitouStagingSyncBackgroundService, PeriodicReportScheduler, HelpdeskBackgroundWorker kayıtlı. Tahsilat borcu/grup mirası/grup mail planlaması yapan worker bulunmadı. `App_Code/Scheduler.cs` legacy takvim UI data source'udur, finansal background Job değildir.

**Canlı geçiş riski:** “MGS artık kullanılmıyor” kullanıcı bilgisini uygulama kullanıcı trafiği olarak değerlendirmek gerekir; otomatik yazan Job'lar devam ediyor. Yeni aktarım öncesi kesit/hash farkı kontrolü sürdürülmeli. Production geçişte Job bağımlılıklarıyla onaylı kesme/freeze/son delta planı hazırlanmalı. Bu denetimde hiçbir Job durdurulmadı; aynı Job'daki CRM işlerini yanlışlıkla kesmekten kaçınılmalı.

## 9. Mevcut implementasyonda tespit edilen uyumsuzluklar

### A01 — Boş/Belirtilmemiş durum kararı uygulama komutlarında tutarlı değil

`Calculation/CollectionStartRules.cs:9` yalnız EXISTS değerini dahil sayıyor. Create sözleşme durumunu null/UNKNOWN kabul ettiği halde Suspended tarife yaratıyor; abonelik aktifleşme ve tarife değişim servisleri de aynı kurala bağlı. Müşterinin boş durumu dahil etme kararı aktarımda boş kaynağı EXISTS'e eşleyerek ele alınmış; bu durum genel yeni kayıt davranışını otomatik düzeltmiyor.

Kabul: belgenin varlık durumu ile finansal dahil olma kararı kayıpsız ifade edilmeli; YOK hariç, onaylı boş/Belirtilmemiş mevcut karar çerçevesinde aynı sonucu vermeli. Aktif/ücretsiz/donuk ve imza günü kuralları bozulmamalı. Eski “yalnız VAR” plan maddesi artık tek başına yeterli değil.

### A02 — Aktarılmış ücretsiz ödemenin düzenlenmesi işareti bozabilir

`CollectionPaymentService.UpdateAsync` payload'ı `IsFree=false` ile kuruyor; `CollectionPaymentTransaction:98` bu değeri mevcut ödemeye yazıyor. FE ödeme listesindeki düzenleme eylemi ücretsiz kayıtları özel olarak korumuyor. Pozitif ücretsiz bir satırda yalnız açıklama düzeltmesi bile, diğer kontroller geçerse, IsFree'yi false yapabilir. 3.208 aktarılmış ücretsiz satır bu nedenle ayrı kabul kapsamıdır.

Aynı serviste giriş/düzeltme yalnız pozitif tutar kabul ediyor; korunmuş 25 negatif/13 sıfır satır aynı değerle düzeltilemiyor. Legacy'de tutar ve Free bağımsızdı. Bunlar “aktarım başarılı” ile kapatılmış sayılamaz. Canlı veri üzerinde deneme yapılmadı; bulgu koddan doğrulandı.

Kabul: düzenlenmeyen tarihsel işaret aynen korunur; negatif/sıfır düzeltme semantiği açık olur, UI gerçekte yapılamayan düzenlemeyi açıklamasız sunmaz; fiziksel silme/kalıcı makbuz ve retry korunur. Yeni online POS/iade sistemi icat edilmez.

### A03 — Takip listesi ile detay bakiyesinde zaman kapsamı farklı

`CollectionTrackingService.BuildRows` seçilen ayın tüm vadelerini ve döneme yazılmış ödemelerini alıyor; `GetBalanceAsync` varsayılan olarak bugüne kadar, PaymentDate <= asOf ile hesaplıyor. Aynı ayın henüz gelmemiş yenileme günü/ileri tarihli ödemesinde tutarlar farklı olabilir. Detayda devir seçimi ayrıca başka kapsamdır.

Kabul: kullanıcıya seçili ay planı mı, bugüne kadar gerçekleşen borç mu gösterildiği net olmalı; aynı hesap tarihi/kapsamı seçildiğinde liste/detay/toplam mutabık olmalı. Henüz yapılmış performans veya tüm borç mutabakatı iddiası yok.

### A04 — Kontrollü finansal düzeltme kapsamı eksik

Kimlik güncellemesi yalnız servis tipi/GTS/IVR. Sözleşme durumu/ödeme yöntemi/başlangıç/bitiş, geçmiş tarife düzeltmesi, yanlış sözleşmeye işlenmiş ödeme ve sözleşme silme için tam güvenli UI/komut zinciri yok. Legacy keyfî fiziksel silmesi aynen kopyalanmamalı; ancak müşteri “manuel müdahale/fiziksel finansal silme” kararı varken bu işleri yapılmış saymak da doğru değil. Ödeme fiziksel silmesi mevcut ve ayrı olarak korunacak.

## 10. Güncellenmiş geliştirme sırası

Bu sıra önceki “ödeme istisnaları → son kabul” özetinin yerine geçer. Yeni ürün özelliği değil, mevcut legacy işlerin kapsama uygun tamamlanmasıdır. Aşağıdakiler **bu tur uygulanmadı**.

| Sıra / görev | Bağımlılık | Kabul kriteri |
| --- | --- | --- |
| K01 — A01/A02 karar ve ödeme düzenleme uyumu | Mevcut kararlar/kod | Boş durum tutarlı; ücretsiz işaret açıklama düzenlemesinde korunur; tarihsel negatif/sıfır davranışı açık; mevcut audit/retry korunur |
| K02 — Hesap zamanı ve açık dönem takip eşliği | K01 | Aynı tarih/kapsamda liste–detay tutarı eşleşir; gerekli geçmiş açık dönem görünümü/filtreler server-side, para birimleri ayrıdır |
| K03 — Müşteri/grup/kurumsal bağlam ve cari eşleştirme | Onaylı kod politikası | N/GM/G/K anlamları ve kapsamdaki üst kartlar eşlenir; FIN/YKB/EMK geri alınmaz; Customer/Group tablosu çoğaltılmaz; ortak değişiklik gerekiyorsa önce onay |
| K04 — Tahsilat müşteri kartı ve not geçmişi | K03 | `/id` detay/geri/sekme; temel bilgiler + müşterinin sözleşme/ödemeleri; çok satırlı not CRUD, eski kimlik/audit ile kontrollü aktarım; sadece Tahsilat altında UI |
| K05 — Grup dönem geçmişi aktarımı ve çalışma listesi | K02/K03; korunan sözleşme map'i | 55.597 kaynak satır aday/dışlama/hata olarak ayrılır; dönem+kontrat tekilliği, durum/açıklama, tekrar güvenliği; listede filtre/detay; otomatik Ödendi kararı açık |
| K06 — Kesilen fatura ve bağlı ödeme | K03/K04 | B/K ayrımı, no/tarih/tutar/kur/proje, kısmi ödeme/kalan; invoice ödeme ve contract ödeme çift sayılmaz; yetki/concurrency/audit; ilişkili legacy veri mutabakatı |
| K07 — Kesilen fatura dosyası yükleme | K06 + cari eşleştirme | Önizleme/tekil eşleme/kuyruk/hata listesi; yeniden yükleme çift fatura üretmez; TOP 1 belirsiz eşleme yok; mevcut CDN kullanılır |
| K08 — GTS/IVR dosyasından ödeme | K01/K03 + mevcut PaymentService | Başarılı/başarısız banka sonucu ayrılır; GTS/IVR birden çok eşleşme karantinada; dönem/tutar/kur/işlem tipi doğrulanır; idempotency; ham kart verisi depolanmaz; tarihsel Payment tekrar yaratılmaz |
| K09 — Sözleşme yaşam döngüsü/düzeltme ve dosya eksikleri | K01/K03/K06 bağımlılık politikası | Durum/yöntem/bitiş ve gerekli geçmiş düzeltme güvenli komut; fiziksel silmede ilişkiler+audit korunur; dosya silme/değiştirme ve müşteri dosyası kapsamı net |
| K10 — Dış senkronizasyon ve yeni GM sözleşme süreci | K03/K09 + dış olay yetkisi | Mevcut Manitou/ortak yapıyla uyum; müşteri update ≠ sözleşme update; kaynak/tarih belli, aynı olay çift sözleşme üretmez; eski toplu sil-yeniden kur davranışı yok |
| K11 — Grup bilgilendirme ekranı/otomasyonu | K05/K06/K10 + üst grup iletişim kaynağı | Yedi legacy amaç, doğru dönem/durum/borç filtresi; ortak mail outbox; test önizleme ve gönderim izleri; çift gönderim yok; onaysız gerçek mail yok |
| K12 — Kalan veri kararları + geçiş/kabul | Önceki işler; madde 3/5/6/7 satır kararları | Kaynak son delta/hash; dosya/ödeme/not/fatura/grup mutabakatı; yetki ve gerçek veri sorgu ölçümü; onaylı backup/rollback/Job geçişi; production onayı |
| K13 — Zam ve rapor ayrıntılı karşılaştırması | Bu faz sonrası | Kullanıcının ertelediği ayrı faz; bu rapor tamamlandı demiyor |

K03/K09/K10/K11'de davranışı etkileyen gerçek kararlar uygulama öncesi ele alınır. Bu sırayı yürütmek, ortak Customer'a izinsiz kolon ekleme veya production üzerinde değişiklik izni vermez.

## 11. Gerçek açık konular / bilinçli taşınmayacaklar

### Uygulama öncesi netleştirilecek sınırlar

- Kapsamda kalan kurumsal/grup üst kartların finansal muhatabı ve iletişimi mevcut Customers/CustomerGroups ile nasıl temsil edilecek? İsim benzerliğiyle birleştirme yok; kod politikası değişmeyecek.
- Grup tahsilatında otomatik Ödendi etiketi mi, mevcut ayrı manuel etiket mi korunacak? Etiket finansal ödeme yerine geçmeyecek.
- Güncel dış sistem hangi olay/tarihle sözleşmeyi dondurur/aktifleştirir ve yeni üyeye hangi üst sözleşme uygulanır? SIMS davranışını Manitou MonitoringStatus'a varsayımla eşitlemek doğru değil.
- GTS/IVR dosya yükleme halen operasyonel ihtiyaç mı? Online POS yapılmaması, geçmişte çalışan banka Excel yüklemesini otomatik kapsam dışı yapmaz. Örnek güncel formatla teyit edilmeli.
- Müşteri dosyaları `Content/Aktivite` gerçekten var mı ve madde 8'in yeni kapsamına alınacak mı? Sözleşme dosyalarının 1.830 ilişkisiyle karıştırılmayacak.
- Legacy'de bağlanmamış `Müşteri Faturaları` bölümü için gerçek çalışan başka sürüm/entegrasyon var mı? Aksi halde boş/sahte panel oluşturulmayacak.
- Hâlen bekleyen madde 3/5/6/7 kayıt kararları, mali alan eklenmesi için ortak tablo onayı ve production/Job kesme izni.

### Yeniden tartışılmayacak mevcut kararlar

- Donuklar donuk olarak gelir; donuk/ücretsiz dönemde yeni borç yok; donuk döneme sonradan borç yok.
- YOK dışarıda; onaylı boş/Belirtilmemiş dahil. Tekilleştirmede elenen eski abonelik ödemeleri MGS'de kalır.
- Farklı tenant tahsilat yetkisi kurulmaz; Customer/Product/ServiceType çoğaltılmaz; Product ilişkisi eklenmez.
- Kart/online POS yok; mevcut CDN değiştirilmez; UI yalnız Tahsilat altında, üst özet + sekme + `/id` + Geri.
- GET/POST, Autofac, ortak toast/Türkçe metin, mevcut ortak altyapı, server-side sayfalama devam eder.
- Kullanım dışı destructive RefreshMemberContract, rastgele TOP 1 eşleme, hard-coded 2016/2023 kesimleri ve satır-satır ağır cache hesabı taşınmaz.

## 12. Uygulama kanıtlarına hızlı erişim

- Route/UI: `AssistFlow-FE/src/configs/routes.config/crmRoute.ts`; `src/views/CRM/Collections/CollectionContractDetail.tsx`, `CollectionTrackingList.tsx`, `CollectionTrackingDetail.tsx`, `components/CollectionPayment*.tsx`, `CollectionContractAttachments.tsx`.
- Ortak müşteri: `Model/Concrete/Customer.cs`, `CustomerGroup.cs`; FE `CustomerManagement/CustomerList/CustomerForm`.
- API: `WebAPI/Controllers/CollectionContractsController.cs`, `CollectionTrackingController.cs`, `CollectionDefinitionsController.cs`.
- İş mantığı: `Business/Services/Crm/Collections/CollectionTrackingService.cs`, `CollectionContractReadService.cs`, `CollectionPaymentService.cs`, `CollectionPaymentTransaction.cs`, `CollectionGroupFollowUpService.cs`, `CollectionContractCreateService.cs`, `CollectionSubscriptionService.cs`, `Calculation/*`.
- Kapsam: `Model/Concrete/Collections/CollectionCustomerClassification.cs`, `Data/Concrete/EfCore/Collections/CollectionCustomerScopeQuery.cs`.
- Ortak otomasyon: `Business/DependencyResolvers/Autofac/AutofacBusinessModule.cs:117`, `Business/Services/ManitouStagingSyncBackgroundService.cs` ve test DB `stg.sp_SyncCustomers`, `stg.sp_SyncCustomerGroups`.
- Önceki aktarım kanıtları: [ödeme](collection-payment-transfer-2026-09-26.md), [dosya](collection-item-eight-file-migration-plan-2026-09-26.md), [müşteri sınıflama](collection-customer-classification-2026-09-26.md), [tekilleştirme](collection-customer-deduplication-2026-09-26.md), [5–7 kararları](collection-items-5-6-7-joint-decision-2026-09-26.md).

**Kapanış:** inceleme raporu ve revize plan hazır. Kod, migration, yeni tablo, gerçek gönderim, Job durdurma veya yeni aktarım bu analiz kapsamında yapılmadı. Modül henüz legacy'nin bütün operasyonlarını karşılayan production-ready aşamada değil.
