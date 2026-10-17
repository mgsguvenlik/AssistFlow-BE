# Güncel MGS → AssistFlow canlı eşleme önizlemesi

15 Ekim 2026. **Hazırlık önizlemesi tamamlandı; aktarım uygulanmadı.** MGS ve AssistFlow üzerinde yalnız sabit SELECT sorguları ve bağlantıya özel okuma/lock timeout ayarları kullanıldı. WebAPI/seed/migration/Job çalıştırılmadı; test işlem verisi kullanılmadı.

## Kaynak ve bütünlük

| Kaynak | Kayıt |
| --- | ---: |
| MGS müşteri | 19.944 |
| Sözleşme | 9.515 |
| Tarife tarihçesi (zam/ücretsiz/dondurma dahil) | 34.586 |
| Sözleşme ödemesi | 243.480 |
| Banka dosyası işlem kimliği | 83.543 |
| Müşteri notu | 12.934 |
| Grup dönem durumu geçmişi | 55.597 |
| Kesilen fatura takibi | 5.478 |
| Kesilen fatura ödemesi | 5.822 |
| Eski Core.Invoice | 0 |
| Canlı müşteri referansı (silinmiş dahil) | 18.884 |

MGS tanımları ve canlı CustomerType/CustomerGroups ilişki kodları, ServiceType ve CurrencyType referansları da okundu. Kart/banka son kullanma bilgileri alınmadı. Banka kimliklerinde boş/mükerrer FileType+FileRowID **0**.

İki okumada 22 kaynak/hedef veri dosyasının row-count ve SHA-256 değerleri aynı bulundu. Bu yalnız gözlem aralığında veri değişimi görülmediğini gösterir; **tablolar/veritabanları arası tek transactional snapshot veya kesme garantisi değildir**. Kaynak Joblar çalışıyor; final geçişte bakım kesiti yeniden alınacak.

## Canlı müşteri eşlemesi

Mevcut ortak sınıflandırma politikası (`CollectionCustomerClassification`) ve abone normalizasyonu kullanıldı. Aynı abone ve normalize aynı isimlerde yalnız tarihi tekil en yeni kayıt seçilir; eksik/eşit tarihte kimlik numarasına göre karar verilmez. Boş kimlik ve elenen eski abonelikler korunmaz; MGS'de silme yapılmaz.

| Kimlik önizleme sonucu | Müşteri |
| --- | ---: |
| Tekil canlı eşleme + mevcut tip/kapsam uyumlu | 3.538 |
| Tekil canlı eşleme + onaylı grup koduna göre GRP01/GM tip önerisi | 2.601 |
| Eski abonelik dışlama kararı | 1.842 |
| Abone/ad birlikte boş | 357 |
| Tekilleştirmede tarih eksik/eşit | 145 |
| Abone yok, başka kimlik bilgisi var; inceleme | 4.998 |
| Canlıda abone karşılığı yok; inceleme | 1.985 |
| Eşleşen canlı grubun kodu kapsam dışında | 4.476 |
| Üst kart/cari özel eşleme gerektiren | 2 |
| **Toplam** | **19.944** |

Bu sınıflar ham müşteri kimlik önizlemesidir; sözleşmesi olmayan müşteriler ve kapsamı ayrıca netleştirilecek G/A üst kartlar da vardır. “Canlıda yok” sayısı otomatik müşteri oluşturma listesi değildir. Daha önce 3. madde için beklenen karar yerine varsayım üretilmedi.

2.601 tip önerisi tekil canlı müşteri bazındadır. Her öneride eski tip, canlı grup/tenant/abone ve kaynak hedef-row hash'i bulunur; önerilen tip canlıdan kodla çözülür. **Customers güncellenmedi; TenantId/CustomerGroupId/BANKA veya mevcut Kurumsal tanımı değiştirilmedi.** Prosedür farkı ve ortak kayıt uygulaması ayrıca onaylanacak.

Servis referanslarında **1 mevcut eşleme (Bakım), 18 yeni tanım adayı** var. Fuzzy eşlemeyle mevcut Arıza/Alarm gibi farklı hizmetler kullanılmadı; eksik kayıtlar henüz eklenmedi. Canlı/tanım ID'leri testten alınmadı.

## Finansal ön kontrol

| Sonuç | Kayıt |
| --- | ---: |
| Ön teknik kontrollerden geçen, referans/sınıflandırma kapısı bekleyen sözleşme | 4.435 |
| Dışlama veya ek inceleme grubundaki sözleşme | 5.080 |
| **Toplam sözleşme** | **9.515** |
| Ön ödeme adayı; legacy kanıtı ve final kontroller bekliyor | 136.479 |
| İnceleme/dışlama grubundaki ödeme | 107.001 |
| **Toplam ödeme** | **243.480** |

Ön ödeme adayları TRY **25.646.742,34**, USD **115.876,56**. Bu tutarlar yalnız aday alt kümenin tutarıdır; kesin aktarım tutarı, dönem borcu veya yeni sistem bakiye toplamı değildir. Referansları hazır olmayan sözleşmelerin ödemeleri de onay bekler.

Ön incelemede mevcut `LegacyPaymentFrequencyRules`, `CollectionPeriodRules`, `PaymentCurrencyCandidateRules` kaynak kodları yeniden kullanılır. Ödeme para birimi keyfi TRY yapılmaz; dönem/vade tarife adayından tekil çözülemeyenler ayrılır. Her sözleşmenin bütün tarihçe seti kontrol edilir; hatalı dilimi atıp eksik tarihçe aktarmak yoktur.

Başlıca sözleşme nedenleri: YOK **2.138**, elenen eski abonelik **859**, canlı abone karşılığı bulunamayan **850**, kaynak müşteri kaydı bulunmayan **554**, güncel tutar/tarihçe uyumsuzluğu **423**, çakışma **292**, tarih aralığı tersliği **202**, para birimi belirsizliği **140**, Donuk yürürlük incelemesi **106**, işlem türü/önceki karar kanıtı gerektiren **12**. **Neden sayıları çakışabilir, toplanmaz**. Bu liste yeni toplu dışlama kararı değildir.

Donuk kayıtlar genel olarak elenmedi. Yalnız açık ücretli tarihçe ile güncel donuk durumunun finansal yürürlük tarihi belirsizse incelemeye ayrıldı. Önceki madde 5/6/7 onayları iptal edilmedi: güncel satırlarla eski kararın finansal alan/hash kanıtı karşılaştırılıp yeniden bağlanacak; `null` işlem türüne toplu Billable varsayımı yapılmadı.

Not/fatura/grup geçmişi için kaynak→canlı müşteri/sözleşme ilişki dosyaları ayrıca hazır. Bunlar tam işlem içeriği, cari/üst kart veya mükerrer dönem doğrulaması tamamlanmış import onayı değildir. Dosya adını görmek fiziksel dosyanın/CDN sahipliğinin doğrulandığı anlamına gelmez.

## Git dışında tutulan yerel paketler

- `.tools/collection-live-preview-2026-10-15-03/`: izinli kolonlarla ham NDJSON'lar, kimlik eşleme, capture manifesti, hash listesi.
- `.tools/collection-live-financial-review-2026-10-15-04/`: sözleşme/tarife, ödeme, not, grup, fatura ilişki incelemeleri; tip/referans ve banka baseline önerileri; hash listesi.
- Capture manifest SHA-256: `3A95B1D764FF6C040CF3A3987CBAB8DDA8FD019E35B2C3591B6F025E26313004`.
- Kaynak paket hash listesi SHA-256: `29B9690E4E3E8B623CDCDCAF178352DD38EE45A91413029226D029C9612CE09C`.
- İnceleme paket hash listesi SHA-256: `B4303BB6C225DF8D8C5FF2BF9EE1E60ED1888413703FC9B5415808BE5ABCBBDE`.

Bu klasörler kişisel/finansal veri içerir; Git'e, mail ekine veya herkese açık URL'ye konmaz. Repoda yalnız anonim özet ve araçlar bulunur. Build başarılı (0 hata/0 uyarı); kaynak ve çıktı dosya hash'leri/doğrulama sayımları kontrol edildi. İzole SQL restore/prova henüz yapılmadı.

## Sıradaki bağımlılıklar

1. Önceki müşteri kararlarını güncel finansal satırlarla kanıtlı tekrar bağla; yeni değişmiş kayıtları ayrı delta listesine koy. Kaybolan source parent / müşteri / üst kart / cari eşlemelerini eski onaylarla yeniden incele. Eski soruları otomatik tekrar müşteriye yöneltme veya yeni cevap varsayma.
2. Canlı ServiceType insert, müşteri sınıflandırma ve `stg.sp_SyncCustomers` dar farkını eski/yeni değerlerle ayrıca paketle. Mevcut tanım isimlerini ve ortak akışları bozma.
3. Legacy ödeme görünümü/para birimi kanıtını ve banka baseline'ını güncel kaynakla doğrula; not/grup/fatura/dosya mutabakatlarını tamamla.
4. Kontrollü canlı apply yolu ve restore provaları; her batch/komutta tekrar koruması, rollback ve son kesit hash kapısı. Hazırlık aracında **apply komutu yoktur**.

Güncel genel sıra ve durdurma koşulları: [README.md](README.md).
