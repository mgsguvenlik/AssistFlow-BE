# Madde 4 — Müşteri / abonelik tekilleştirme

## Son karar ve uygulama — önceki bekleme notlarının yerine geçer

Kullanıcı eski aboneliklerin ödemelerinin **yalnız MGS'de kalacağını** ve madde 4 aktarımlarının tamamlanabileceğini onayladı. Dosyalar 8. madde kapsamındadır. AssistFlowTest'te 138 eski sözleşme / 563 tarife / 701 eşleme yedeklenerek tek transaction içinde fiziksel kaldırıldı; staging Excluded olarak ve kaynak payload'ları korunarak denetim kaydı eklendi. Hedef ödeme/dosya/takip bağımlılığı 0; MGS/dbo müşteri/dosya yazması yok.

Yedek dosyası `tools/Collections.Import/snapshots/item-four-removal-20260926-161939613/backup.json`, SHA-256 `0964BFA7D8713AC3EB5DAF74DC7194DE1B998AE0774F58DB39A136E28BDBE34C`. Yedek hedef kimliklerini, scalar kolonları, aktarım map'lerini, önceki staging/issue durumlarını ve ham kaynakları içerir; diskten tekrar okunarak doğrulandı. Otomatik geri yükleme çalıştırılmadı.

### Sonuç ve mutabakat

- Kaldırma sonrası 13 ilave eski sözleşme / 19 tarife staging'de dışlandı. Bu işlem hedefte ek silme yapmadı.
- Madde 4 toplam dışlama: **612 sözleşme / 2.001 tarife**. Dağılım: abone bazlı 242/930, isim bazlı 232/508, önceden aktarılmış eski hedef 138/563.
- Korunacak en yeni aboneliklere ait **55 sözleşme / 160 tarife** aktarıldı: 46 ACTIVE ve 9 FROZEN. Donukların bitiş tarihleri güvenli; tarih/tutar tahmini yapılmadı. Diğer maddelerin engelleri çözülmüş sayılmadı.
- Birikimli aktarım map/stage/hedef eşit: **4.474 sözleşme / 17.388 tarife**. İki eski manuel kayıt dahil fiziksel hedef **4.476 / 17.390**. Ödeme 0, dosya 0; 63.854 ham kaynak satırı korunuyor.
- Yeni 160 tarifede tutar, para birimi, ödeme dönemi, başlangıç/bitiş, davranış, üst sözleşme ve kaynak hash farkı **0**. Dışlanan kaynaklar için kalan aktarım map'i **0**.
- Staging: **Pending 3 / Blocked 1.723 / Applied 4.474 / Excluded 3.298**. Üç Pending kayıt madde 1 tarih istisnasıdır: 11998/26458/49232.
- Importer build: 0 hata / 0 uyarı. FE/API davranışı bu dilimde değiştirilmedi; yeni test altyapısı eklenmedi.
- Son tekrar önizlemeleri: abone kararı **0 değişiklik**, isim kararı **0 değişiklik**, madde 4 aktarımı **0 aday / 0 aktarılabilir**. Tekrar çalıştırma yeni veya mükerrer hedef oluşturmadı (önizlemeler yazmasızdır).

İlave isim dışlama hash'i: `C7DE7A677E02EDC3403EF39840093D901319C7F8C9584A490D4CCBA9AE5ADFC6`.
55 sözleşme aktarım hash'i: `6573B77A358F031413CD56D38EA8D0C4AF26E9C60935CC5164BEA7D989D72CD5`.
Kanıtlar: `item-four-names-20260926-162212460/plan.json`, `item-four-transfer-20260926-162327459/plan.json`, `.tools/item-four-final.json`. Plan dosyaları `tools/Collections.Import/snapshots` altında ve kişisel veri nedeniyle Git dışındadır.

Tekrar kontrol planları: `item-four-20260926-162612377`, `item-four-names-20260926-162610597`, `item-four-transfer-20260926-162603139`.

### Açık veri istisnaları

Onaylı kuralın tekil kazanan seçebildiği işlemler tamamlandı; **tüm madde 4 istisnaları kapanmış değildir**. Aynı isim incelemesinde 61 tarih eşitliği kümesi (87 sözleşme: 24 Blocked, 30 önceden Applied), 290 en yeni kayıtta boş abone kümesi (107 sözleşme: 61 Blocked, 30 önceden Applied), bir isim/abone seçimi çelişkisi (zaten dışlanmış 1 sözleşme) kaldı. Küme adetleri sözleşmesiz müşterileri de içerir. Bu belirsiz kümelerdeki önceden aktarılmış 60 sözleşme için silme kararı uydurulmadı; korundu. Abone bazlı tarih eşitlikleri ayrıca raporda izlenir ve isim kümeleriyle kesişebilir; sayılar toplanmaz. Madde 3 müşteri eşleşmeleri ile 5–7 tarife kararları bu uygulamayla kapatılmaz.

Aşağıdaki ilk uygulama adetleri ve onay bekleme notları bu işlemden önceki tarihsel kayıttır.

**Sonraki inceleme sonucu:** Kullanıcı ödeme/dosya etkisi açıklanınca kaldırma yerine salt-okuma incelemesini onayladı. 599 eski sözleşmede 19.963 legacy ödeme satırı ve 256 dosya referansı bulundu. **138 hedef sözleşme/563 tarife kaldırılmadı; kaldırma ve finansal geçmiş kararı beklemede.** Aşağıdaki 0 ödeme/dosya sayıları yalnız test hedefe aittir. [Güncel etki raporu](collection-deduplication-payment-file-impact-2026-09-26.md).

## Güncel müşteri kararları

- Aynı abone numarasında kayıt tarihi tekil en yeni olan müşteri korunur; eskiler aktarıma alınmaz.
- Kullanıcı ayrıca **farklı abone numaralı aynı isimlerin de** incelenip tekilleştirilmesini onayladı.
- Bu farklı aboneliklerde **yalnız en yeni abonelik ve onun sözleşmeleri korunacak**; eski aboneliklerin sözleşmeleri yeni müşteriye bağlanmayacak.
- İçi boş kayıt yorumunda önceki açık karar korunur: hem abone numarası hem müşteri adı olmayan kayıt dikkate alınmaz. Sadece abone numarası boş, adı dolu müşteri “tamamen boş” sayılmaz.
- Madde 3 için müşteri yanıtı bekleniyor. AssistFlow'da hedef bulunmayan müşteri oluşturulmaz veya yalnız isimle mevcut müşteriye bağlanmaz.

## Kurallar ve güvenlik

Karşılaştırmada kaynak **Customer.CreatedOn** kullanılır; Contract.CreatedOn, ModifiedOn veya büyük Id “en yeni” yerine kullanılmaz. Aynı en yeni tarih veya eksik tarih varsa otomatik seçim yapılmaz. İsimler Türkçe büyük/küçük harf ve fazla boşluk açısından normalize edilir; fuzzy benzerlik, noktalama veya aksan silme uygulanmaz. Abone numarasının baştaki sıfırları korunur.

Aynı isim ve aynı abone kuralları farklı müşteri seçiyorsa otomatik karar verilmez. En yeni isim kaydında abone numarası boşsa bir abone numarası uydurulmaz. Aynı korunacak müşterinin farklı hizmet sözleşmeleri birbirine birleştirilmez.

Hedef eşleştirme yalnız tekil, silinmemiş, abone numarası aynı ve madde 2 kapsamına uygun AssistFlow müşterisine yapılır. Ortak Customers/CustomerGroups/Tenant verileri ve servis talebi ilişkileri değiştirilmez. Eski kaynak müşteri ile sözleşmeler fiziksel olarak MGS'den silinmez; test staging'de Excluded işaretlenip audit notu korunur.

Genel aktarım planı da bu iki tekilleştirme kuralını bağımsız yeniden kontrol eder. Eski Pending durumuna güvenerek mükerrer abonelik aktarılamaz; daha önce aktarılmış eski müşteri kaydı bulunan küme temizlenmeden ikinci müşteri otomatik yüklenmez.

## Uygulanan test staging işlemleri

| Dilim | Eski sözleşme dışlama | Bağlı tarife dışlama | Hedef eşleşmesi düzelen sözleşme |
| --- | ---: | ---: | ---: |
| Aynı abone numarası | 242 | 930 | 61 |
| Aynı isim, farklı abone dahil | 219 | 489 | 0 |
| Toplam | 461 | 1.419 | 61 |

İlk dilimde 61 sözleşmenin 58'i Pending olabildi, 3'ü başka engeller nedeniyle Blocked kaldı. İsim bazlı ikinci kararın önceliği nedeniyle bu ilk sayım nihai aktarım adedi değildir. Bu tur **yeni finansal sözleşme/tarife aktarımı yapılmadı**.

Plan hash'leri:

- Abone bazlı: `44F77D06C6588AA0317E9D8F239EC2E6AAC68A82A72B345AE2944F93BA643439`.
- İsim bazlı: `EE0C8DC24BED30D6A8B0F6DCAEDD5B65E73A4E3A8C67A84B7EC9D55DBDC87FB6`.

İki ayrı tekrar önizleme **0 değişiklik** verdi. Hedef sözleşme/tarife/ödeme sayıları **4.559 / 17.793 / 0** değişmedi. Hatalı hedef eşleşmesi 0; yeni dışlanan sözleşmeye bağlı Applied tarihçe 0. Importer build 0 hata / 0 uyarı.

Staging son durumu: **Pending 58 / Blocked 1.736 / Applied 4.557 / Excluded 3.147**. Başka engeli olan eski aboneliğin tüm sözleşmesi dışlanınca o sözleşmeye ait issue'lar “çözüldü” değil **Ignored** olarak, dışlama gerekçesiyle kapatıldı; yeni/korunan abonelikteki tarife engelleri tahminle giderilmedi.

## İnceleme sonuçları

- Aynı abone numarası: 188 küme; 7 kümede en yeni tarih eşit.
- Normalize edilmiş aynı isim: 1.086 küme. Bunların 571'inde farklı dolu abone numaraları var; 252'sinde tüm abone numaraları boş.
- Farklı aboneli isim kümelerinin 238'inde birden fazla adres, 148'inde birden fazla legacy grup bulunuyor. Kullanıcı bu ayrımı bilerek yalnız en yeni aboneliğin kalmasını onayladı; bu sayılar otomatik birleştirme için yeni izin gereksinimi değildir.
- İsim incelemesinde 61 küme tarih eşitliği, 290 küme en yeni kaydın abone numarasının boş olması, 1 küme isim/abone en yeni seçim çelişkisi nedeniyle bekledi. Bu küme sayıları sözleşme sayısı değildir; sözleşmesiz müşteriler de kesitte bulunur.
- **87 kümede eski müşterilere bağlı 138 sözleşme test hedefe daha önce aktarılmış. Bağlı 563 tarife dönemi, 0 ödeme ve 0 dosya var.** Mevcut hedef kaydı silme staging dışlamasından ayrıldı; bunlara dokunulmadı. Geri dönüş yedeği ve kullanıcı onayıyla ayrı kontrollü kaldırma gerekir.
- Hem adı hem abone numarası boş fakat kaynak müşteri kaydı mevcut olan 5 sözleşme zaten Excluded; yeni işlem yok. Önceden dışlanan kayıp müşteri bağlantıları bu beş sayısına dahil değildir.
- Madde 1'de tarihi bekleyen 44287 ve 44289, bu yeni isim tekilleştirme kararıyla eski abonelik olarak dışlandı. Donma tarihi bekleyen yeni aktarım adayları artık **11998, 26458, 49232**. Önceki aktarılmış 12 tarih istisnası henüz değiştirilmedi.

## Kanıt ve komutlar

Kişisel veri içeren satır listeleri Git dışında tutulur:

- `tools/Collections.Import/snapshots/item-four-20260926-140417598/plan.json`
- `tools/Collections.Import/snapshots/item-four-names-20260926-140825853/plan.json`
- Zenginleştirilmiş tekrar isim incelemesi: `item-four-names-20260926-141004833/plan.json`.
- Salt-okunur mutabakat: `.tools/item-four-postflight.json`.

```powershell
dotnet run --project tools/Collections.Import -- review-customer-duplicates-batch 1 WebAPI/appsettings.Development.json
dotnet run --project tools/Collections.Import -- review-customer-names-batch 1 WebAPI/appsettings.Development.json
dotnet run --project tools/Collections.Import -- transfer-item-four-batch 1 WebAPI/appsettings.Development.json
dotnet run --project tools/Collections.Import -- remove-obsolete-item-four-batch 1 WebAPI/appsettings.Development.json
```

Hash olmadan yazmasız önizlemedir. Dördüncü argüman güncel önizleme hash'i olursa review komutları yalnız test staging'e, transfer komutu test sözleşme/tarife tablolarına uygular. Remove komutu yalnız sabit onaylı 138 kimlik için hedef bağımlılık denetimi ve tam yedek sonrası kaldırma yapar; daha önce uygulanmış audit/Excluded/map durumu varsa yazmadan döner. İşlemler tek transaction/ortak aktarım kilidi altındadır. Ham kaynak hash'leri, müşteri karar politikası, hedefler, staging sürümleri, açık issue'lar ve mevcut aktarım eşlemeleri doğrulanır. Production, legacy veya ortak dbo yazması yoktur.
