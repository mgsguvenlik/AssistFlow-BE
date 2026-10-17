# K03 — Müşteri, grup üst kartı ve cari eşleştirme

## Durum

27 Eylül 2026: K03 geliştirmesi ve netleşen 56 üst kartın test uygulaması tamamlandı. 18 veri istisnası ve üst kart sözleşmelerinin ayrı kontrollü aktarımı açık veri listesinde izlenir. Önceki salt-okunur incelemeler aşağıda tarihsel kanıt olarak korunur. AssistFlow canlı ve MGS değiştirilmedi.

## Uygulama sonucu

- Migration `20260927160000_AddCollectionGroupParent` yalnız AssistFlowTest'e uygulandı. `collection.GroupParent` mevcut Customer/CustomerGroups'a NoAction FK ile bağlanır; legacy kimliği ve grup bağı tekildir. Cari kod tekil varsayılmaz.
- Eksik `CustomerType.Code=G` tanımı, "Grup Üst Müşteri" adıyla oluşturuldu. Onaylı grup kodlarında G üst kartı operasyonel kapsamda; GM/GRP01 üyedir. Ortak dış müşteri aktarımının GroupTypeCodes listesi GM olarak korundu; yeni üyeler G yapılmaz.
- 56 yeni Customers kaydı + 56 GroupParent ilişkisi tek transaction ile oluşturuldu. Abone numarası boşsa boş kaldı; yapay numara veya TenantId verilmedi. Kaynak adı/cari kodu/kurumsal niteliği ve kaynak SHA-256 saklandı. CreatedUser kaynak aktarım batch'inin otomatik aktörü 0'dır; gerçek kullanıcı atfedilmedi.
- Uygulama plan hash'i: `F84176CC7CA438C77B91B391079E5070C5D7D65F5B1F1F501EC22FB222B4F2D5`. Yerel uygulama öncesi kanıt `.tools/k03-parent-apply-plan.json`, sonuç `.tools/k03-parent-apply-plan.json.applied.json`.
- Sonraki önizleme: **0 yeni / 56 uygulanmış / 18 inceleme**. 56 kimlik/alan eşleşmesi, üç grup servisi örneği ve geçersiz grup reddi başarılı. EF snapshot/model farkı yok.
- Önceki Customers kayıtlarında değişiklik **0**; yeni kayıt **56**. Sahiplik uyuşmazlığı **0**. Mevcut finansal toplamlar aynı: 4.479 sözleşme, 17.403 tarife, 139.678 ödeme, 1.830 dosya ilişkisi.
- G üst kartları genel müşteri listesinde ayrı tipleriyle görülebilir. Tenant filtreli servis seçicilerine yapay tenant atanmadı; mevcut servis talebi akışları değiştirilmedi. Kaynak senkronizasyonunun incelenen sürümü SubscriberCode üzerinden eşleşir, kaynakta bulunmayan hedefleri silen bir dal içermez. Canlı geçişte prosedür sürümü tekrar doğrulanır.
- GET `/api/collections/groups/{id}/context`: üst kart, cari kod, kurumsal nitelik ve gerçek GM üye sayısı. GET `/api/collections/groups/resolve-account`: yalnız kaydedilmiş grup üst kartları içinde cari eşleme; birden çok eşleşmede 409. Bu servis tüm legacy bireysel cari kayıtlarının eşlendiği anlamına gelmez; fatura aktarımının ayrıca kapsamlı cari doğrulaması gerekir.
- Servisler mevcut Autofac modülüne kaydedildi. Controller kimlik doğrulaması, CollectionFollowUp görüntüleme yetkisi ve modül açma kontrolünü kullanır.
- Yalnız Tahsilat sözleşme detayına **Grup Bilgileri** sekmesi eklendi. Üst kart/üye sahipliği, grup, üst müşteri, cari kod, kurumsal nitelik ve üye sayısı gösterilir. İstek sekme açıldığında mevcut sekme yapısıyla yüklenir. Eksik üst kart açıkça eşleştirme bekliyor olarak görünür; kurumsal niteliği tahmin edilmez.
- BE/FE build ve değişen FE dosyalarında ESLint/Prettier başarılı. Genel type-check mevcut proje hataları nedeniyle temiz değildir; değişen üç FE dosyasına ait hata çıkmadı. Yeni sekmenin gerçek tarayıcı kabulü ayrıca son kullanıcı kabulünde yapılacaktır.

### Açık veri kapsamı

18 kayıt otomatik oluşturulmadı: altı boş ad ve 12 benzer unvan/kaynak tekrarı. Yeni oluşturulan 56 kartın 49 legacy sözleşmesi bu tur taşınmadı. Bu sözleşmeler için GroupParent.LegacyCustomerId→CustomerId eşlemesinin kontrollü aktarım yolunda kullanılması, mevcut madde 4 dışlamaları ve sözleşme/tarife kararları tekrar doğrulanmalıdır. Boş aboneyi genel olarak kabul edecek biçimde mevcut importer koruması gevşetilmedi. Bu veri işleri K12 aktarım listesine işlendi; K03'ün yeni kart oluşturması geçmiş ödeme/tarife/dosya aktarımı değildir.

### Tekrar çalıştırma ve canlı hazırlığı

`Collections.Import group-parents <Development JSON> <yerel rapor JSON> --install` yalnız beklenen tek migration varsa şemayı hazırlar. Son parametre olmadan sadece plan; plan SHA-256 verilirse testte kontrollü uygulama; `--verify` yalnız kontrol yapar. Hedef sabit olarak AssistFlowTest doğrulanır. Seri hale getirilebilir transaction, uygulama kilidi, kaynak/ hedef tekrar okuma, plan hash'i ve DB tekillikleri birlikte kullanılır. Plan değişirse yazma reddedilir; eski eşleştirme değişmişse uygulama durur. İnceleme kayıtları raporda kalır.

Canlı için aynı migration ve tasarım kullanılacak; test ID'leri kopyalanmayacak. Bu test aracının hedef kilidi kaldırılarak production'a yazılmaz. Canlı uygulama yolu geçiş paketinde ayrıca hazırlanır. Geri dönüşte otomatik silme/Down yok; ilişkilenen sözleşme/işlem kontrolü ve uygulama makbuzu üzerinden ayrı plan gerekir.

## Doğrulanan anlamlar

### AssistFlow üzerinde ek doğrulama — 27 Eylül

Kullanıcının test ortamında eksik müşteri olabileceği uyarısıyla gerçek `AssistFlow` veritabanı yalnız SELECT ile incelendi. 18.757 müşteri, 18.756 silinmemiş kayıt var. Kapsamdaki 74 legacy üst kart için abone numarası ve boşluk/büyük-küçük harf normalize edilmiş tam unvan üzerinden eşleşme bulunmadı; grup kodunun doğrudan SubscriberCode olarak kullanıldığı bir kayıt da bulunmadı. Bu sonuç farklı unvanla kayıt ihtimalinin kesin dışlandığı anlamına gelmez; isim benzerliği tek başına sahiplik kanıtı değildir.

Onaylı grup kodlarının altındaki müşteri kayıtları ayrıca okundu: 2.750 müşteri; tipler 2.749 BRYSL01 ve 1 BNK01. Testte uygulanmış müşteri tipi sınıflandırması bu ortama taşınmış değildir. Bu üyeleri üst kart diye kabul etmek veya production müşteri tiplerini değiştirmek için gerekçe oluşturmaz.

Dolayısıyla eksiklik yalnız test verisine dayanılarak çıkarılmıyor: AssistFlow'da da mevcut kimliklerle doğrulanmış üst kart müşteri eşleşmesi bulunamadı. Kullanıcı, bu inceleme sonrasında hâlâ eksikse yeni kart oluşturma yönünü kabul etti; önceki genel temsil onayı tekrar beklenmeyecek. Sonraki adım test ortamı için satır bazlı yeni kart önizlemesi, olası farklı unvan adayları ve ortak müşteri seçicisi etkisi kontrolüdür. AssistFlow üzerinde yalnız okuma yetkisi geçerlidir; burada kayıt oluşturulmadı.

Kanıt: `docs/sql/20260927_CollectionCustomerProductionReview.sql`; yerel ham çıktı `.tools/k03-assistflow-review.json`.

| Legacy | Mevcut AssistFlow karşılığı / sınır |
| --- | --- |
| N bireysel | Onaylı grup kodu + N/BRYSL01 müşteri tipi |
| GM grup üyesi | Onaylı grup kodu + GM/GRP01 müşteri tipi; mevcut Customers kaydı |
| G üst müşteri kartı | CustomerGroups kodu eşleşiyor; grubun kendisi finansal müşteri veya sözleşme sahibi değildir |
| Kurumsal ekran | G üst kartı ve Definition.Group.GroupType=Kurumsal; A tipi veya KRMSL01 ile eşdeğer kabul edilemez |
| GM.GroupID | Core.Customer üst kartının CustomerID değeri; dbo.CustomerGroups.Id değildir |
| AccountNo | Mali cari kod; CustomerShortCode, SubscriberCode veya LocationCode yerine geçirilemez |

FIN*/YKB*/EMK ve diğer onaylı dışlamalar korunur. Kurumsal görünüm yeni bir kapsam açmaz; onaylı grup kapsamının içinde bir bağlamdır. Üst kartın kendi sözleşmeleri ile üyelerin sözleşmeleri ayrı sahipliktedir; borç veya ödeme bir üyeye taşınamaz.

## Canlı envanter

- Legacy G üst kartı: 237.
- Onaylı GM kodlarına sahip üst kart: 74; 74 farklı kod ve her biri için tam bir hedef CustomerGroups eşleşmesi.
- Bu 74 üst kartın kendi sözleşmeleri: 58. Bu sayı aktarılabilir sözleşme sayısı değildir; diğer aktarım engelleri ayrıca incelenecektir.
- Abone numarasıyla aktif hedef Customers eşleşmesi: 0. Bunların 71'inde abone numarası boş; kalan 3'ünde de eşleşme bulunmadı. İsimle otomatik eşleme yapılmadı.
- 74 üst kartın 68'inde cari kod dolu. Kaynağın tüm müşteri kümesinde 699 tekrar eden cari kod grubu var; cari koddan tek müşteri seçerken TOP 1 kullanılmaz.
- Onaylı bireysel kodlara sahip ayrıca 4 G kartı var; bunlar G harfinden dolayı GM kapsamına çevrilmez.
- Kapsam içi kurumsal üst kart: 9; kendi sözleşmeleri toplam 7.

| Kurumsal grup kodu | Üst kart sözleşmesi |
| --- | ---: |
| FISV | 0 |
| BANV | 2 |
| MARK | 1 |
| GAP | 0 |
| NEOT | 1 |
| BOGA | 0 |
| TKNS | 1 |
| DECH | 1 |
| BNTS | 1 |

## Önerilen tasarım ve karar noktası

Grubun tanımı ve üyeleri için mevcut dbo.CustomerGroups / dbo.Customers kullanılmaya devam edilir. Tahsilata özgü cari kod, legacy üst kart kimliği ve kurumsal bağlam collection şemasında bir ek kayıtla tutulabilir; ortak müşteri kolonları değişmez.

Ancak mevcut collection.Contract.CustomerId zorunlu olarak dbo.Customers'a bağlıdır. Eşleşmeyen üst kartın 58 sözleşmesini bu yapıda tutabilmek için iki farklı uygulama mümkündür:

1. **Öneri:** Doğrulanan üst kartlar için mevcut Customers içinde gerçek müşteri kayıtları oluşturmak/eşlemek; tahsilat ek kaydıyla CustomerGroups'a üst kart ilişkisini belirtmek. Abone numarası uydurulmaz, üyeler yeniden oluşturulmaz. Ortak müşteri listeleri/servis seçicilerinde görünürlük etkisi, oluşturma öncesinde doğrulanır. Eksik müşteri oluşturma daha önce kullanıcı kararı gerektiren kapsamda olduğundan otomatik uygulanmadı.
2. Sözleşmeye Customers dışında doğrudan grup sahipliği eklemek. Bu, tüm ödeme/tarife/dosya/aktarım/yetki sorgularının sahiplik kurallarını genişletir; daha büyük mimari değişikliktir.

Karar gelmeden ortak müşteriler oluşturulmaz ve üst kart sözleşmeleri mevcut bir üyeye bağlanmaz. Üst kartları atlamak K03'ü tamamlamak değildir.

## Devam sırası ve kabul

### Tekrar çalıştırılabilir önizleme tamamlandı

`tools/Preview-CollectionGroupCustomers.ps1` eklendi. SettingsPath, açık Database (AssistFlowTest/AssistFlow) ve OutputPath alır; yalnız SELECT çalıştırır. Aynı onaylı kod politikasını kullanır; grup tekilliği, kaynak abone/unvan tekrarları, boş ad, mevcut müşteride abone/tam normalize unvan ve ilk sekiz harf benzerliği kontrolleri üretir. Benzerlik eşleme kararı değil, inceleme nedenidir. Silinmiş müşteriler de çakışma kontrolüne dahildir.

İki ortamda da 74 üst karttan 56 yeni kart adayı, 18 inceleme çıktı. Yeni kart adaylarına ait 49 legacy sözleşme var; bunlar otomatik aktarım onayı değildir. Altı boş adlı kart AREN/SHN/CONS/VAN/ISMR/ACT; bu kartların kendi sözleşmesi yok. Diğer 12 kartın benzer unvan adayları mevcut: UYKA/NEOG/TENG/GAP/NORM/UGUR/SORS/PARA/HELP/TRKS/NEOT/DECH. PARA/HELP kaynak adları ayrıca aynı. İsimden üst kart sahipliği tahmin edilmeyecek.

Plan hash'i hedef veritabanı, araç sürümü (dosya hash'i), politika, kaynak/ hedef kayıtlar ve önizleme kararlarını kapsar. Kaynak ile hedef okumaları tek değişmez kesit garantisi vermez; uygulama öncesi yeniden okuma ve transaction kontrolü gereklidir. Rapor `ReadyToApply=false` üretir; uygulama komutu değildir. Ham JSON müşteri bilgisi içerdiğinden Git dışında `.tools` altında saklanır.

Müşteri seçim etkisi: `CustomerService` içindeki tenant filtresi yalnız tenant seçildiğinde uygulanıyor; genel müşteri araması tüm müşteri kartlarını görebilir. Üst kartlara yapay TenantId veya grup üyesi anlamı verilerek gizleme yapılmayacak. Sonraki uygulama dilimi, üst kart/üye ayrımını collection ek ilişkisinde tanımlayıp uygun tip ve seçim davranışını korumalı; bundan sonra 56 aday için test uygulama planı hazırlanmalı. Henüz müşteri oluşturulmadı veya sözleşme bağlanmadı.

1. Üst kart temsil biçimini netleştir; seçilen yol için mevcut servis talebi/müşteri seçici etkisini incele.
2. collection ek kayıt modelini, mevcut Autofac servisini ve GET/POST API'yi uygula; cari kod belirsizliğinde açık hata üret.
3. Tam kod/kimlik eşleştirmeli önizleme oluştur; kapsam, kaynak hash'i, tekillik ve tekrar güvenliğini doğrula.
4. Netleşen kayıtları testte uygula; üst kart ve üye sözleşme/ödeme sahiplikleri ayrı kalsın. Tahsilat altındaki müşteri detayları K04'te bu bağlamı kullanacak.
5. Kabul: 74 grup eşleşmesi korunur; kapsam dışı kod açılmaz; cari kod tekrarlarında rastgele seçim olmaz; ortak Customers/CustomerGroups çoğaltılmaz; üretim/MGS yazması yapılmaz.

## Kanıt

- Salt-okunur sorgu: `docs/sql/20260927_CollectionCustomerContextProfile.sql`.
- Yerel ham sonuç: `.tools/k03-context-profile.json` (kişisel veri içerebileceği için yayımlanmaz).
- Legacy: `Pages/Core/CustomerCorporate.aspx.cs` liste koşulu ve `CustomerGroup.aspx.cs` GM üye bağlantısı; `InvoiceFollowLoad.aspx.cs` AccountNo eşleştirmesi.
- Hedef: `Model/Concrete/Customer.cs`, `CustomerGroup.cs`, `Collections/CollectionContract.cs`, ortak `CollectionCustomerClassification` kararı.
