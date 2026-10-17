# Karar karşılaştırması ve yerel aktarım inceleme paketi

Devam güncellemesi: [ödeme/banka mutabakatı](payment-reconciliation-report.md) sonrası aktif paket `-02`, ödeme adayı **139.717** oldu. Aşağıdaki `-01` sayıları ilk karar karşılaştırması aşamasının kanıtıdır; orijinal paket değiştirilmedi.

15 Ekim 2026. **Hazırlık tamamlandı; canlı uygulama onayı değildir.** Önceki [veri önizlemesi](data-preparation-report.md) tarihsel ilk sayımları içerir; aşağıdaki karar bağlı sonuçlar o önizlemenin devamıdır. Canlıya hiçbir değişiklik yapılmadı.

## Karar kanıtı ve istisnalar

Arşiv karar dosyasındaki 807 Excel kaynaklı satırın bağlı olduğu 794 karar nesnesi, hash'li 26 Eylül MGS kesiti ve 15 Ekim okumasıyla karşılaştırıldı. Orijinal Excel dosyası yeniden açılmadı; daha önce saklanan, hash'i sabit karar JSON'u ve uygulanmış plan kanıtı kullanıldı. Eski test müşteri/sözleşme ID'leri canlı hedef ID'si olarak kullanılmadı.

| Sonuç | Karar nesnesi |
| --- | ---: |
| Önceki koşullu dışlama incelemesi korunuyor | 435 |
| Açık onay; kaynak müşteri/sözleşme/tüm tarihçe değişmemiş | 3 |
| Koruma kararı var; teknik kontrollerden geçmeli | 204 |
| Önceki dışlama korunuyor | 133 |
| Cevapsız inceleme korunuyor | 2 |
| Karar alanları ile kaynak arasında farklılık; bekliyor | 6 |
| Çelişkili karar; bekliyor | 11 |
| **Toplam** | **794** |

Madde 5'in üç açık onayında toplam 13 tarihçe değişmemiş. Yalnız bu kayıtların literal `null` işlem türü için önceki ücretli dönem kararı yeniden bağlandı; genel bir null→ücretli varsayımı yok. 435 koşullu dışlama adayı otomatik kalıcı dışlama sayılmadı. Altı farklılık yalnız son kesitten beri değişim demek değildir; önceki Excel ile arşiv kaynak arasında bilinen farkları da içerir. Donuk kayıtlar genel bir kuralla elenmedi; önceki müşteri kararları geçerlidir. Karar kapıları 336 sözleşmeye bağlandı; bu sayı bağımsız bir toplam dışlama sayısı değildir.

## Kaynak farkları (26 Eylül → 15 Ekim)

| Varlık | Önceki | Güncel | Yeni | Artık gözlenmeyen | Alanı değişen |
| --- | ---: | ---: | ---: | ---: | ---: |
| Müşteri | 19.917 | 19.944 | 28 | 1 | 1 |
| Sözleşme | 9.510 | 9.515 | 7 | 2 | 172 |
| Tarife tarihçesi | 34.487 | 34.586 | 102 | 3 | 117 |
| Ödeme | 242.459 | 243.480 | 1.021 | 0 | 9 |

Kaynakta artık gözlenmeyen kayıtlar silme talimatı değildir. Bu okuma bakım/final kesiti değildir; kaynak Joblar durdurulmadı.

## Güncel aday paket

- **4.438 sözleşme** referans/sınıflandırma onayı sonrası aday; **5.077 sözleşme** inceleme/dışlama sınıfında.
- **136.504 ödeme** ön aday; **106.976 ödeme** inceleme/dışlama sınıfında. Üç yeniden bağlanan onay önceki önizlemeye 3 sözleşme ve 25 ödeme ekledi.
- Aday ödeme alt kümesi: **25.692.585,34 TRY / 115.876,56 USD**. Bunlar nihai aktarım/bakiye tutarları değildir; legacy ödeme kanıtı kontrolü açık.
- **18 eksik servis tanımı**, **2.601 müşteri tipi güncelleme önerisi** ve `stg.sp_SyncCustomers` için mevcut sınıflandırma politikasına bağlı dar fark hazırlandı. Kurumsal tip tanımı, tenant/grup ilişkileri değiştirilmedi; BANKA koruması taslakta yer alır. Ortak dbo/prosedür değişiklikleri ayrıca onay gerektirir.
- Sözleşme planı ve ödeme planı yerel dosyadır; uygulama/import komutu yoktur. Ödemede mevcut, batch'ten bağımsız legacy RequestId algoritması kullanılır.

## Yerel dosyalar ve bütünlük

Kişisel/finansal veri içeren dosyalar yalnız BE repository'sinin Git dışı `.tools` alanındadır:

- `collection-decision-replay-2026-10-15-02`: karar karşılaştırması, davranış onayları, sözleşme kapıları, varlık bazlı delta listeleri.
- `collection-live-financial-review-2026-10-15-05`: karar kapıları uygulanmış finansal inceleme.
- `collection-production-data-package-2026-10-15-01`: üç kapalı SQL taslağı, prosedür önce/sonra kanıtı, sözleşme/ödeme aday planları ve manifest.

Paket hash listeleri SHA-256:

| Paket | Hash |
| --- | --- |
| Kaynak okuma | `29B9690E4E3E8B623CDCDCAF178352DD38EE45A91413029226D029C9612CE09C` |
| Karar karşılaştırması | `3A736BE22C8954C9ED48E7268A143480A71367C44F8AE8F1AA30F945A3BE47BB` |
| Finansal inceleme | `B910A3DB1DD7D86903574ACCA14041B5C2FFCEE334E4440DDF6B8B04A7B9801E` |
| Aktarım inceleme paketi | `C9ED68E661B35A8A4C1E71143BC885355C81D551449DAE42FF50B0920116061D` |

Üç SQL taslağında `@Apply=0`, hedef veritabanı kontrolü ve transaction/lock koruması bulunur. Müşteri sınıflandırması eski alanları, prosedür değişikliği eski SQL hash'ini doğrular. Üç pakette toplam 26 dosyanın hash'i doğrulandı. 4.438 sözleşme kimliği ve 136.504 kaynak ödeme/RequestId tekil; bütün ödeme adayları aday sözleşmeye bağlı, mevcut kimlik algoritmasıyla yeniden hesaplanan RequestId'ler aynı. Prosedür farkı yalnız sınıflandırma eşlemesi, BANKA koruması ve ALTER başlığıyla sınırlı. Release araç build'i başarılı: 0 hata, mevcut Core/Model/Data kaynaklarında 76 uyarı. SQL syntax/restore/çalıştırma provası yapılmadı. Ham/prosedür dosyaları Git'e veya genel paylaşıma konmaz.

## Uygulamadan önce kalan kapılar

1. Legacy ödeme görünümü/para birimi kanıtı ve banka baseline mutabakatı.
2. Üst kart/cari, not, grup, fatura ve fiziksel dosya/CDN ilişki kontrolleri.
3. Canlı aktör/audit eşlemesi ve ortak dbo/prosedür değişiklik onayı.
4. İzole restore üzerinde şema + veri aktarımı, tekrar çalıştırma ve geri dönüş provası.
5. Bakım aralığında güncel kaynak ve hedef fingerprint'lerinin yeniden alınması; delta kararı ve son mutabakat.

Paket durumu **BLOCKED_REVIEW_PACKAGE_NOT_APPROVED_FOR_EXECUTION**. Jenkins sırası ve yayın durdurma koşulları [yayın planında](README.md) korunur.
