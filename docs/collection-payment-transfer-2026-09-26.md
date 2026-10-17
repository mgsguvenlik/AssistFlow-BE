# Ödeme aktarımı — AssistFlowTest (26–27 Eylül)

## Kapsam ve uygulama

Kullanıcının ödeme aktarımlarını tamamlama talebiyle yalnız doğrulanmış alt küme uygulanır. MGS, production, ortak dbo tabloları ve CDN değiştirilmez. Tutar/işaret/ücretsiz bilgisi/açıklama/ödeme tarihi/muhasebe dönemi aynen korunur. Eski aboneliklere ait dışlanmış ödemeler yeni aboneliklere bağlanmaz.

- Kaynak kesit: `payment-review-20260926-225543248`.
- Manifest: `950FD53132F1E8467B42BB02640563EF92EF8C3A46B1A69075450642FE49FDC7`.
- Referans kanıtı: `.tools/payment-legacy-reference.json`, SHA-256 `BC99DEDB20513700B3929CC0D8750DDBE4A4D06E8C92650071E18E38F238EACD`.
- Onaylı alt kümenin plan SHA-256 değeri: `E3F765C9A68F9C97B560F2BA8C8025BCB7C603E40705F1EB4BBBED3754163433`.
- Önizleme: **139.678 yeni / 0 önceden aktarılmış**. Hedef başlangıç ödeme sayısı 0.
- Uygulama **27 Eylül 00:00:31 Türkiye saati** tamamlandı; sonuç aşağıdadır. Önizleme tek başına commit kanıtı değildir.

## Tekrar güvenliği ve fiziksel silme

Yeni şema kurulmadı. Önceki MigrationMap FK/tombstone tasarım işi, mevcut kalıcı **PaymentOperation** makbuzu ve **MigrationSourceRow** audit'i kullanılarak çözüldü:

1. MGS PaymentID için batch'ten bağımsız `CollectionPaymentImportIdentity.For(id)` kimliği üretilir. Sabit namespace `AssistFlow:collection:legacy-payment:v1:MGS:` ve kanonik sayısal PaymentID'nin SHA-256 ilk 16 baytı Guid olur. Bu algoritma değiştirilmez.
2. Aynı transaction'da Payment, ham kaynak/hash içeren MigrationSourceRow ve deterministik RequestId ile PaymentOperation(Create, ActorUserId=0) yazılır. Makbuz hash'i mevcut `CollectionPaymentCommandRules`, son değerler mevcut `CollectionPaymentSnapshotRules` ile oluşturulur. Hedef kaydı değiştiren yeni API/iş akışı yoktur.
3. Mevcut makbuzun kaynak/hash/işlem/kullanıcı alanları eşleşiyorsa tekrar eklenmez. Hedef ödeme sonradan düzenlenmişse üzerine yazılmaz; silinmişse geri oluşturulmaz. Makbuz veya kaynak audit'i tutarsızsa işlem durur.
4. PaymentOperation.PaymentId bilerek FK değildir ve mevcut fiziksel ödeme silme işlemi bu makbuzu korur. **Ödemeler için MigrationMap satırı oluşturulmaz**; sözleşme/tarife eşlemeleri aynı yapıda kalır. Makbuz ve kaynak audit'i silinmemelidir.
5. Bağımsız `review-payments` mevcut aktarım makbuzlarını `PAYMENT_ALREADY_IMPORTED` gerekçesiyle ayırır. Aktarım aracı aynı transaction'da uygunluğu yeniden hesaplayıp mevcut makbuzları ayrıca doğrular; “ön aday” sayısı yeni eklenecek sayıyla karıştırılmaz.

## Yazmadan önce / commit öncesi kontroller

Canlı MGS Customer/Contract/ContractHistory/Payment tablolarının tamamı aynı kaynak salt-okunur transaction'da kesitteki kolon sırası/JSON biçimiyle yeniden hash'lenir. Dört hash/sayımın eşit olması gerekir. Kaynak para birimi/dönem tanımları, dokuz SQL nesnesi, yıl ve collation yeniden kontrol edilir. Hedef para birimi eşlemeleri, sözleşme/tarife/müşteri kapsamı, kaynak–hedef tutarlılığı ve legacy para birimi karşılaştırması aynı hedef Serializable transaction içinde tekrar çalışır.

Hedef yalnız AssistFlowTest guard'ı ile açılır. Uygulama kilidi ve plan hash'i zorunludur; 750'lik bellek/EF yazma grupları **tek transaction** içindedir. Her yeni ödemenin tüm snapshot alanları tekrar okunup karşılaştırılır; önceden bulunan ödeme değişemez. Ödeme/makbuz/kaynak sayıları eşit olmadan commit yoktur. Ayrı toplamlar para birimi bazındadır; negatif/sıfır/ücretsiz satırlar dönüştürülmez. Legacy CreatedOn/ModifiedOn ortak sözleşme aktarımındaki tarih ayrıştırmasıyla korunur, kullanıcı kimliği uydurulmaz (0); orijinal metin kaynak audit'indedir.

Kesit dosyaları aktarım öncesi kaynak yedeğidir. Yerel `transfer-plan.ndjson`, `transfer-preview.json`, `inserted-identities.json` ve commit sonrası `transfer-result.json` Git dışındaki yeni karşılaştırma klasörüne yazılır. `inserted-identities.json` tek başına commit kanıtı değildir. Kesinti/yanıt kaybında önce makbuzlarla tekrar önizleme yapılır; kör tekrar/silme uygulanmaz. Geri alma otomatik değildir; yeni kullanıcı hareketleri incelenmeden aktarılan ödemeler silinmez.

## Bekleyen kayıtlar

| Kaynak kapsamı | Ödeme |
| --- | ---: |
| Bu dalganın doğrulanmış alt kümesi | 139.678 |
| Hedef sözleşmesi var, kayıt incelemesi gerekiyor | 1.800 |
| Daha önce kapsam dışında bırakılan sözleşme | 71.234 |
| Sözleşme kararı/aktarımı bekliyor | 27.607 |
| Kaynak sözleşmesi yok | 2.127 |
| Sözleşme kesitinin dışında | 13 |
| Toplam kaynak | 242.459 |

1.800 kayıt için kesişen nedenler: 1.226 kaynak sözleşme hash farkı, 145 tarihçe içerik farkı, 134 tarihçe küme farkı, 9 ödeme–müşteri uyuşmazlığı, 1 güncel kapsam dışı müşteri; 560 hedef para birimi/tarife adayı yok, 566 legacy aday yok, 1 yalnız legacy aday var. Neden sayıları toplanmaz. Satır bazlı kimlikler `comparison.ndjson` içindedir; daha önce paylaşılan 7 takvim istisnası da buradadır. Yeni müşteri kararı uydurulmaz.

## Çalıştırma

AssistFlow-BE dizininde (plan hash'i yoksa yalnız önizleme):

```powershell
dotnet tools/Collections.Import/bin/Debug/net9.0/Collections.Import.dll transfer-payments tools/Collections.Snapshot/snapshots/payment-review-20260926-225543248 WebAPI/appsettings.Development.json 1 950FD53132F1E8467B42BB02640563EF92EF8C3A46B1A69075450642FE49FDC7 .tools/payment-legacy-reference.json BC99DEDB20513700B3929CC0D8750DDBE4A4D06E8C92650071E18E38F238EACD
```

Uygulamak için son argüman olarak yukarıdaki plan hash'i eklenir. Kaynak/hedef değişirse yeni önizleme değerlendirilir. Kimlikler veya finansal alanlar elle değiştirilerek engel aşılmaz.

Bağımsız son SQL sayım/tutar kontrolü: `docs/sql/20260926_CollectionPaymentTransferReconciliation.sql`.

## Uygulama sonucu

**139.678 ödeme** tek transaction ile AssistFlowTest'e aktarıldı. Ödeme batch'i **2** (`payments-mgs-20260926-195543Z`); çözülmemiş satırlar nedeniyle durum NeedsReview korunur. Her ödeme için kaynak audit'i ve kalıcı makbuz mevcut: **139.678 / 139.678 / 139.678**. Ödeme MigrationMap satırı **0**, dolayısıyla bu aktarımdan kaynaklı ödeme silme FK engeli yoktur. Silme sonrası geri yaratmama mevcut makbuz tasarımına dayanır; gerçek müşteri ödemesi doğrulama için silinmedi.

| Para birimi | Ödeme | Toplam | Ücretsiz işaretli | Negatif | Sıfır |
| --- | ---: | ---: | ---: | ---: | ---: |
| TL — CurrencyTypeId 1 | 137.961 | 26.229.413,21 | 3.205 | 25 | 13 |
| USD — CurrencyTypeId 2 | 1.717 | 116.045,96 | 3 | 0 | 0 |

Önizleme, commit öncesi bütün ödeme alanları ve commit sonrası bağımsız SQL adet/tutar kontrolleri eşleşti. Hedef sözleşme **4.479**, tarife **17.403**, aktif dosya **1.830** değişmedi. MGS dört tablo hash'i de yeniden kesitle eşleşti; kaynağa yazma yapılmadı. Ortak dbo, production ve CDN aynı kaldı.

Kanıt klasörü: `tools/Collections.Snapshot/snapshots/payment-review-20260926-225543248/review-20260926-205256-9c6d7cb31bdd4977b96831cb26f640c8/currency-20260926-205300-10fef5593ad74da1afc7e80c09a0a6e8`. `transfer-result.json` Committed=true; `inserted-identities.json` hash'i `7CD1599D412F655E125C47996CAD04E8F219289D2807CF8E00AA364FEB03F54C`. Bağımsız SQL sonucu `.tools/payment-transfer-reconciliation-20260927.json`.

Importer build 0 hata/3 mevcut bağımlılık uyarısı; yeni test altyapısı eklenmedi. Aktarım istisnaları ayrıca `payment-exceptions.json` dosyasında üretilir. Tekrar önizleme sonucu aşağıda kayıtlıdır.

Tekrar önizleme **0 yeni / 139.678 önceden aktarılmış** verdi; plan hash'i aynı. Hiçbir ödeme yeniden yazılmadı. Son rapor/1.800 satırlık istisna listesi: `review-20260926-210203-68b937108288469793723cde5d7ec9ea/currency-20260926-210206-e3060f5b60494d318032ad9b32158c66` (aynı kesit klasörünün altında). Bu dalganın doğrulanmış alt kümesi tamamlandı; kalan 1.800 ödeme ve sözleşmesi bekleyen/kapsam dışı diğer kayıtlar aktarılmış sayılmaz.
