# Sabit legacy kesitini staging'e alma

## Girdi ve kullanım

`tools/Collections.Import` canlı legacy veritabanına bağlanmaz. Tutarlı, yazmaya kapalı bir kaynaktan önceden dışa aktarılmış üç UTF-8 NDJSON dosyası ve manifest ister:

```text
<kesit klasörü>/
  manifest.json
  customers.ndjson
  contracts.ndjson
  contract-history.ndjson
```

Manifest örneği:

```json
{
  "sourceSystem": "MGS",
  "snapshotKey": "onayli-kesit-kimligi",
  "ruleVersion": "contract-v1",
  "normalizationVersion": "subscriber-v1"
}
```

Her satır bir tam JSON nesnesidir. Kaynak kolon adları ve null/boş farkı korunur. `CustomerID`, `ContractID` ve `ContractHistoryID` ilgili dosyada zorunlu ve tekildir. Tarihçedeki `ContractID` ham üst anahtar olarak saklanır. Kaynak değerleri keyfî biçimde düzeltmek, ilk eşleşeni seçmek veya eksik para birimini varsaymak bu aşamada yapılmaz.

Çalışma alanı içinde geçici kesit kullanılacaksa `tools/Collections.Import/snapshots/` Git tarafından yok sayılır. Tercihen erişimi kısıtlı, repository dışı bir klasör kullanılmalıdır.

```powershell
dotnet run --project tools/Collections.Import -- inspect <kesit klasörü>
dotnet run --project tools/Collections.Import -- profile <kesit klasörü>
dotnet run --project tools/Collections.Import -- stage <kesit klasörü> WebAPI/appsettings.Development.json
dotnet run --project tools/Collections.Import -- validate <kesit klasörü> WebAPI/appsettings.Development.json
dotnet run --project tools/Collections.Import -- map <onaylı-eşleme.json> WebAPI/appsettings.Development.json
```

`inspect` veritabanına bağlanmadan JSON biçimini, kimlik tekilliğini, satır sayılarını ve SHA-256 manifest özetini kontrol eder. `stage` yalnız `192.168.1.8 / AssistFlowTest` hedefini kabul eder; production için açılmamıştır. Önce `inspect` ve kaynak kesitinin iş birimince sabitlenmesi gerekir.

`profile` aynı dosya/hash ön kontrolünden sonra veritabanına bağlanmadan müşteri türü, servis tipi, sözleşme/abonelik durumu, ödeme yöntemi/dönemi, para birimi ve tarihçe süreç tipinin değer-adet dağılımını verir. Dosya metadata çiftlerinin tam/eksik/yok sayısını gösterir; abone numarası, müşteri adı, dosya yolu veya ham JSON yazdırmaz. Beklenmeyen metin ve sayısal olmayan kimlikler değeri gösterilmeden topluca sayılır. Bu çıktı eşleme ve kaynak kesit incelemesi içindir; aktarım onayı veya fiziksel dosya doğrulaması değildir.

## Tekrar ve sınırlar

- Aynı `SourceSystem + SnapshotKey` farklı manifest, kural veya normalizasyon sürümüyle gelirse reddedilir.
- Aynı kesitte daha önce saklanan satırın hash'i farklıysa ham veri ezilmez. Aynı hash'li satır tekrar yazılmaz.
- Dosya incelemesi ile yazma arasında hash değişimi kontrol edilir. Her 500 satırda kaynak ve varsa stage izdüşümü birlikte kaydedilir; kesinti sonrası aynı dosyalarla devam edilebilir. Sonunda üç varlık sayısı manifestle eşleştirilir.
- Müşteri ham satırları, sözleşme eşlemesi için tutulur; mevcut `dbo.Customers` kaydını değiştirmez. Sözleşme ve tarihçe stage satırları `Pending` kalır. Bu komut hiçbir `collection.Contract`, tarife veya ödeme oluşturmaz.
- Kişisel kaynak alanları JSON içinde bulunabileceğinden kesit dosyaları, uygulama logları ve Git'e eklenmemelidir. `inspect` yalnız sayı/hash, `profile` yalnız izinli kodların adetlerini yazdırır.

`validate` aynı dosyaların manifest hash ve satır sayısını staging ile karşılaştırmadan çalışmaz. Kaynak SubscriberNo ile hedef SubscriberCode yalnız trim + Türkçe büyük/küçük harf duyarsız tam eşitlikle karşılaştırılır. Kaynakta/ hedefte tekrar, eksik/silinmiş hedef, tarihçe üst sözleşme veya müşteri uyuşmazlığı, geçersiz/ters/çakışan tarih aralıkları, tekrarlı başlangıç, birden fazla açık dönem ve tarihçesiz sözleşme için ayrı `MigrationIssue` kodları açar. Sorun ortadan kalkmışsa önceki açık issue çözüldü olarak işaretlenir; ham payload korunur. Sorunlu stage satırı `Blocked`, bu kontrolleri geçen satır `Pending` kalır: henüz hizmet, para birimi, müşteri tipi ve iş kararı eşlemeleri tamamlanmadığı için `Ready` veya gerçek finansal kayda geçmez.

Legacy `EndDate` dahilî kabul edilir; stage `EffectiveToExclusive` alanına bir sonraki gün yazılır. Geçersiz/taşan tarih tahmin edilmez, karantinaya alınır. Doğrulama DB güncellemelerini 500 satırlık dilimlerde yapar; kesinti sonrası aynı kesit üzerinde tekrar çalışabilir. Henüz gerçek kaynak kesiti bulunmadığından bu komut gerçek veri üzerinde çalıştırılmadı.

`map` girdisi `sourceSystem`, `snapshotKey`, `approvedByUserId` ve `entries` dizisini içerir. Her girişte `kind`, `sourceId`, `targetId`, `expectedValue`, `evidence` zorunludur. Örneğin `{"kind":"ServiceType","sourceId":"1","targetId":2033,"expectedValue":"BAKIM","evidence":"Muhasebe onaylı eşleme"}`. `expectedValue` servis tipi için Name, diğer türler için Code'dur. Bu komut hedef tanım oluşturmaz, mevcut `dbo` kayıtlarını değiştirmez; yalnız doğrulanan ve açıkça onaylanan eşlemeyi `collection.MigrationReferenceMap` tablosuna ekler. Kesit olmadan çalışmaz. Müşteri kimliği eşlemesi ayrı `validate` sürecindedir.

Yeniden `validate` çalıştırıldığında sözleşmedeki ServiceTypeID ile tarihçedeki CurrencyID ve PaymentTypeID için kabul edilmiş eşleme zorunludur. Legacy `Customer.aspx.cs` kaynak kodunda PaymentMethodID ayrı ödeme yöntemi, ContractHistory.PaymentTypeID ayrı ödeme tipi olarak kullanılır; müşteri onaylı dönem seçeneklerine karşılık geldiği için yalnız açık `PaymentFrequency` eşlemesi kabul edilir. Tarihçe tutarı negatif olmayan, invariant ondalık biçimde ve hedef `decimal(18,2)` sınırında olmalıdır; biçim/ölçek belirsizse karantinaya alınır. Kaynak müşteri türü `Customer.Type` alanındaki N/GM/A/G kodudur; hedef müşterinin `CustomerTypeId` değeri açıkça onaylı eşlemeyle aynı olmalıdır. `A` kodunun anlamı tahmin edilmez. Sözleşmede dolu ContractStatusID, SubscriptionStatusID veya PaymentMethodID varsa bunlar da açık eşleme ister. Eksikler ayrı blocker issue oluşturur ve ilgili satır `Blocked` kalır; karar eklenince tekrar doğrulamada issue çözülür. Bu aşamada eşlemesi tamamlanan satır dahi `Ready` yapılmaz: finansal dahil olma ve tarihsel süreç kontrolleri yükleme provasından önce tamamlanmalıdır.

Legacy `ProcessType` seçicisinin beş değeri kaynak koddan doğrulandı: Başlangıç/Fiyat Artışı/Fiyat İndirimi tarihçede borçlanabilir tarife, Ücretsiz borçsuz hizmet, Hizmet Dondurma borçsuz donuk dönem olarak izdüşürülür. Tanınmayan/boş değer tahmin edilmez. Sözleşme durumu yalnız `EXISTS` (VAR) ise tahsilata dahil edilebilir; YOK/boş/UNKNOWN sözleşme ve tarihçesi yüklemeye açılmaz. Abonelik durumu ACTIVE/FROZEN dışında veya eşlemesizse bloke edilir. Bu karar bugünkü durumdan geçmişteki dondurma tarihini üretmez; tarihçedeki açık dönem ve gün hassasiyeti ayrıca kontrol edilmeden `Ready` statüsü verilmez.

Legacy `Contract` ayrıca güncel tutar, para birimi ve PaymentTypeID taşır. Tek açık `ContractHistory` dönemi varsa bu üç alanın onaylı hedef eşlemeleriyle aynı olması gerekir; fark `CONTRACT_CURRENT_RATE_MISMATCH` olarak karantinaya alınır. Her iki ham değer korunur, otomatik düzeltme yapılmaz.

Sözleşmedeki `FileAttachmentName`/`FileAttachmentPath` değerleri stage izdüşümüne taşınır. Eksik çift veya hedef metadata uzunluk sınırını aşan değer ayrı issue ile karantinaya alınır; ham JSON kaybolmaz. Bu metadata, legacy fiziksel dosyanın CDN'e taşındığı anlamına gelmez. Kaynak dosya içeriği ayrıca sağlanıp doğrulanmadan yeni `ContractAttachment` kaydı oluşturulmaz.
