# Not, grup, fatura, cari ve dosya mutabakatı

15 Ekim 2026. **Salt-okunur inceleme tamamlandı; aktarım uygulanmadı.** Belirsiz kayıtlar için otomatik müşteri oluşturma, sahiplik değiştirme, veri silme veya kapsam genişletme yapılmadı. DB/CDN yazması ve Job değişikliği yoktur.

## Güncel kesit

Canlı müşteri kontrolünde ilk okumadan sonra **1 yeni müşteri ve 3 değişmiş kayıt** görüldü. Eski hedef eşlemesiyle devam edilmedi; MGS ve canlı referanslar yeni `collection-live-preview-2026-10-15-04` kesitine alındı. Karar karşılaştırması, finansal önizleme ve ödeme kanıtı yeniden üretildi. MGS kaynak sayıları ve **4.438 sözleşme / 139.717 ödeme** aday kümesi değişmedi. Canlı referanslarda silinmiş dahil 18.885 müşteri var. Sonuçlar tutarlı bakım kesiti değildir; final geçişte yeniden doğrulanmalıdır.

MGS müşterilerinin cari kodları, grup kurumsallığı ve canlı grup ağacı SELECT ile zenginleştirildi. Bu veriler iki kez aynı bulundu; kaynak müşteri/grup ve canlı müşteri/tip/para birimi alanları yeni kesitle tekrar karşılaştırıldı. Testteki işlem verisi veya hedef kimlikleri kopyalanmadı.

## İlişki sonuçları

| Varlık | Kaynak | Teknik aday | Ek koşul / inceleme |
| --- | ---: | ---: | --- |
| Müşteri notu | 12.934 | 8.275 | 2 not üst kart oluşturma/onayına bağlı; 4.657 inceleme/kapsam dışı |
| Grup durum geçmişi | 55.597 | 26.228 | 2 birebir aynı tekrar tek temsil; 29.367 inceleme/kapsam dışı |
| Kesilen fatura | 5.478 | 1.813 | 3.665 inceleme/kapsam dışı |
| Kesilen fatura ödemesi | 5.822 | 1.904 | 3.918 inceleme/kapsam dışı |
| Cari kod | 13.786 | 2.890 | 10.896 sahipliği çözümlenmemiş/tekrarlı/kapsam dışı |
| Kapsam içi grup üst kartı | 74 | 56 oluşturma önerisi | 18 kimlik/grup/çakışma incelemesi |

“Teknik aday” kesin aktarım onayı değildir; müşteri sınıflandırması, tanımlar, aktör, final kesit ve izole prova kapıları açıktır. İnceleme ile kalıcı dışlama aynı anlamda kullanılmaz. Satır bazlı nedenler yerel planlarda korunur; nedenler kesiştiğinden ayrıca toplanmaz.

### Not ve üst kart

Not sahibi yalnız korunacak sözleşmenin doğrulanmış müşterisi veya ayrı doğrulanmış üst kart önerisiyle belirlenir. Ad/abone benzerliğiyle eski abonelik notu başka müşteriye aktarılmaz. Boş notlar ve 10.000/200 karakter sınırları kontrol edildi. Ham not/audit metni korunur; henüz aktör uydurulmadı.

56 üst kart önerisi, mevcut canlı grup kodu tekil olan ve canlıda benzer kart çakışması görülmeyen kayıtlardır. Ad/ilk sekiz harf benzerliği yalnız inceleme sinyalidir, otomatik eşleşme değildir. **Canlıda `G` üst kart tipi yoktur; gereken tip/ortak Customers oluşturma ayrıca onaylanıp paketlenmeden bu öneriler uygulanmaz.** Mevcut Kurumsal tipi, tenant veya CustomerGroups ilişkisi değiştirilmez. 18 inceleme kaydı için test eşlemesi canlıya taşınmadı.

### Grup geçmişi

Sözleşmenin korunacak aday kümede ve müşterinin grup kapsamında olması kontrol edildi. Dönem/tanım/açıklama kontrolü uygulandı. Aynı sözleşme/dönemde **82 kaynak satırında çelişkili durum** var; tarih bilgisi olmadan en yüksek kimlik “en yeni” sayılmadı. İki birebir aynı tekrar tek temsil edilir; bu, kaynakta silme değildir. Tanımlar uygulamada seed sonrası ad/kodla bağlanacak; legacy ID hedef ID diye kullanılmayacak.

### Fatura ve ödemeleri

Fatura B/K türü müşteri tipinden türetilmedi; kaynak türü esas alındı. Tarih, işaretli decimal tutar, para birimi, metin uzunluğu ve müşteri sahipliği kontrol edildi. Bir ödeme satırı sorunluysa faturanın tamamı bekletildi; eksik ödeme geçmişiyle yanıltıcı bakiye üretilmedi. `Core.Invoice` kaynak tablosunda kayıt **0**; dolu kaynak `Core.InvoiceFollow` ve `Core.InvoiceFollowPayment` tablolarıdır.

| Aday fatura para birimi | Fatura adedi | Fatura tutarı | Bağlı ödeme | Net kalan |
| --- | ---: | ---: | ---: | ---: |
| TRY | 1.812 | 7.164.161,73 | 7.157.277,76 | 6.883,97 |
| USD | 1 | 0,00 | 0,00 | 0,00 |

Bu toplamlar yalnız tam zinciri doğrulanan aday alt kümedir; tüm müşterilerin cari bakiyesi veya sözleşme tahsilat toplamı değildir. 1.813 faturanın ödeme sahipliği ve kalan = tutar − bağlı ödeme eşitliği ayrıca doğrulandı.

### Cari kod

Mevcut `Trim().ToUpperInvariant()` kuralı korundu; kod uzunluğu ve **kaynak genelinde** tekillik kontrol edildi. **700 kod** birden fazla kaynak müşteriye ait; birinin ödemesi/faturası diğerine bağlanmadı. 2.890 teknik adayda sahiplik korunacak müşteri/üst kart zincirinden gelir. Çözülemeyen kodlar `collection.InvoiceAccount` için inceleme planında kalır; ortak Customers.AccountNo alanına yazma yoktur.

## Fiziksel dosyalar

Arşiv dosya manifesti sabit hash ile doğrulandı. Sağlanan yerel Contract kökündeki **1.769 dosyanın** boyut ve SHA-256 değerleri yeniden okundu; önceki içerik kanıtıyla eşleşti. Dosya çalıştırılmadı, değiştirilmedi veya silinmedi. Yinelenen yol ayraçları mevcut importer kuralıyla normalize edildi; kök dışına çıkma/link/reparse yolları reddedildi. Boyut/uzantı/imza uygunluğu hash'i doğrulanan önceki manifest üzerinden korundu.

| Aday 4.438 sözleşmenin dosya durumu | Bağlantı |
| --- | ---: |
| Yerel içerikle doğrulanan; canlı CDN aktarımı/geri okuması bekleyen | **1.817** |
| Metadata yok | 2.533 |
| Verilen arşivde bulunamadı | 69 |
| Sıfır bayt | 19 |

Eksik/boş toplam **88** dosya bağlantısı, müşterinin dosyasız aktarım kararına göre ayrıca raporlandı; sözleşmeyi silmez. Arşivde bulunamaması başka sunucuda da kesin yok anlamına gelmez. 5.077 bekleyen/kapsam dışı sözleşmenin dosyaları aday kapsamına eklenmedi. Bağlantı sayısı benzersiz fiziksel dosya sayısı değildir.

Aktivite arşivi bilinen SHA-256 ile doğrulandı; 15 asıl dosya belleğe okunarak hash/imza kontrolünden geçirildi, Thumb dosyaları kapsam dışında tutuldu. Önceden onaylı `924 → 291401` devir zinciri güncel müşteri kimliğiyle tekrar doğrulandı: **3 dosya teknik aktarım adayı**. `2206 → 87400` için canlı müşteri hâlâ bulunamadığından üç dosyası ve diğer dokuz dosya, toplam **12 dosya bekliyor**. Bu izin eski ödeme/notları taşımaya genişletilmedi.

**Canlı CDN'e yükleme/geri okuma veya yeni zararlı yazılım taraması yapılmadı.** Test CDN nesnesinin varlığı production sahiplik kanıtı sayılmadı. Mevcut CDN servisi değiştirilmeyecek; ayrı yayın adımında izinli production prefix/erişim/içerik doğrulaması gerekecek.

## Paket ve doğrulama

Aktif özel klasörler BE `.tools` altındadır; kişisel/finansal veri Git'e eklenmez:

- Kaynak `collection-live-preview-2026-10-15-04`; karar `collection-decision-replay-2026-10-15-03`; finansal inceleme `collection-live-financial-review-2026-10-15-06`.
- Ödeme `collection-payment-reconciliation-2026-10-15-02`; ilişki `collection-relation-reconciliation-2026-10-15-02`.
- **Birleşik aktif inceleme paketi `collection-production-data-package-2026-10-15-03`**. Önceki paketler tarihsel kanıttır; ilişki `-01` ilk kontrol çıktısıdır, yayın girdisi olarak kullanılmaz.

| Hash listesi | SHA-256 |
| --- | --- |
| Güncel kaynak | `3D89AFD5374D0439B957CAAED5F30430798A52CAAE7EBEFCB049CDDFE5529DB7` |
| İlişki mutabakatı | `75C6C1CE68DDED20EBB744FEED28D16066BE13F2F6B9421A2A16A3ACB310CFC1` |
| Birleşik paket | `1A48ECE4B080FAEFEDC436C80ED6969679593CA583E05268FDEF5838F92CAEA3` |

Altı pakette toplam 95 dosyanın hash'i doğrulandı; fatura ödeme/bakiye kontrolü ve kapalı SQL taslakları denetlendi. Release aracı build'i **0 hata/0 uyarı**. Uygulama FE/BE işleyişi veya veritabanı değiştirilmedi. Şema/veri restore, tekrar ve geri dönüş provası henüz yapılmadı.

## Sonraki sıra

1. Aktör/audit eşlemesi, eksik üst kart tipi ve ortak kayıt oluşturma taslağı; sahipliği belirsiz 18 üst kart, cari/dosya/finansal istisnaları ayrı karar listesinde koru.
2. Canlıya yazmayan apply hazırlığı ve **izole restore üzerinde** şema + bütünlüklü veri aktarımı/idempotency/rollback provası. Testte sonradan girilen işlemleri canlı verisi diye kullanma.
3. Ortak dbo/prosedür ve production CDN işlemleri için açık uygulama onayı; bakım kesitinde kaynak/hedef fingerprint'lerini yenile ve mutabakatı tekrar çalıştır.

Paket **BLOCKED_REVIEW_PACKAGE_NOT_APPROVED_FOR_EXECUTION**; satırlar `ReadyToApply=false`. [Jenkins/yayın sırası](README.md) korunur.
