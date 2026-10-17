# Tahsilat — ayrı canlı dosya adaptörü ve izole prova

**Sonuç: yazmalı izole kabul tamamlandı.** 1.818 sözleşme dosyası ilişkisi,
3 müşteri dosyası ve 1.821 receipt commit edildi. Tam replay 0 INSERT /0 PUT;
10 kontrol başarılı, 15 finansal/orijinal audit parmak izi aynı. Son onarımsız
CHECKDB temiz. Gerçek CDN/HTTP çağrısı ve canlı DB yazması yoktur.

Bu kayıt [CDN hazırlık planının](cdn-transfer-and-access.md) devamıdır.
Canlı AssistFlow/MGS verisine yazılmaz; gerçek CDN'ye yükleme/istek yapılmaz.
Yeni araç `tools/Collections.Files`; mevcut runtime/API/UI/CDN veya test-only
aktarıcı kilitleri değiştirilmedi. Kullanıcının Development config'i korunur.

## Uygulanan güvenlik ve kapsam

- Canlı ve prova, ayrı kapsam/teyit ve hash'li plan ister. Prova SQL hedefi
  **sql2022 / AssistFlow_Performance_Restore** sabittir; kaynak config'teki
  katalog sabit hedefe yönlendirilir. Başka oturumda KILL, otomatik migration,
  seed, Job/restore/temizlik işlemi yapılmaz.
- Ortak `R2FileStorage` aynen kullanılır. Provada sağlayıcı `IsolatedS3`'tür:
  yalnız yerel özel dizine nesne yazar, SDK fallback HTTP handler'ı da çıkışı
  reddeder. Gerçek R2 credential/endpoint'i prova deposunda kullanılmaz.
  Public URL kontrolü **SimulatedPublicReadback** etiketlidir.
- Adaylar mühürlü bundle'dan gelir. Reconciled finansal batch, dosya kaynak
  audit hash'i, Contract map'i, tekil SubscriberCode ve güncel owner/rowversion
  doğrulanır. Elenen/bekleyen dosyalar tahminle eşleştirilmez.
- Kaynak dosyalar boyut/SHA-256/içerik imzasıyla tekrar doğrulanır; yerel kök
  dışına çıkma/link ve sınırsız arşiv okuması reddedilir. Bu malware taraması
  değildir; gerçek tarama ayrı canlı kapısıdır.
- Kararlı namespace/kaynak/owner/içerik anahtarıyla önce nesne readback, sonra
  kısa Serializable SQL transaction'ında attachment + `CdnFileReceipt` yazılır.
  Receipt mevcut MigrationSourceRow tablosunu kullanır; yeni migration yok.
  CDN nesnesi DB hatasında silinmez; var olan farklı içerik ezilmez.
- Kaldırılmış veya receipt'i korunmuş fiziksel silinmiş ilişki yeniden
  yaratılmaz. Ödeme/not/grup/sözleşme modelleri değişmez; dosya devri eski
  abonelik ödemelerini getirmez. Teknik audit aktörü 0 korunur.
- Canlı `apply-live` yolu NOT_APPROVED şablonunu **config/SQL/provider/çıktı
  açmadan önce** reddeder. Gerçek uygulama ayrıca operatör, süre, namespace,
  bakım/yedek/tarama/public exposure/finansal replay ve aynı kod/kaynak için
  başarılı dosya provası ister. Canlı plan/uygulama çalıştırılmadı.

## Plan ve kanıt zinciri

| Kayıt | Yol / SHA-256 |
| --- | --- |
| Değişmemiş inceleme paketi | `.tools/collection-cutover-review-2026-10-17-02`; bundle hash `8253BCA52AED33271A445FE6B46EBEF7A86E3F5220C48239B54E7B6F12AFD673` |
| Finansal kabul | `.tools/collection-production-adapter-acceptance-2026-10-17-02/acceptance.json`; aynı bundle için mevcut finansal yazma/rollback/replay kabulü |
| Dosya prova planı | `.tools/collection-cdn-plan-rehearsal-2026-10-17-01/plan.json`; `816629E21BCAB17FCE1ECDEF0C42FC06E720C6C29E642ADCE0D913297CCD2699` |
| Dosya hedefi | Reconciled finansal batch 2; 1.818 sözleşme ilişkisi +3 müşteri dosyası; 12 aktivite sahiplik istisnası dışarıda |
| Prova çıktısı | `.tools/collection-cdn-rehearsal-2026-10-17-01`; journal, yerel nesneler, finansal önce/sonra, result ve acceptance |
| Dosya kabul SHA-256 | `2CC5EEF20DB3C5FA1689EB579032D1D0018987D3136CAFB780BA00C6D763AC6A` |
| Sonuç SHA-256 | `3CF40FC73D6696827BF1F99E367E6D1168FE4091D43B0DEC6CD87DDC9BAC8F20` |
| Adaptör/ortak DLL birleşik hash | `999AF9A4E1559DD46E956C95B010AAD813365868F98A7E9FEB5E709FAEB211D0` |
| Son izole profil | `.tools/collection-cdn-final-2026-10-17-01.json`; CHECKDB 0 hata, REPAIR yok |

Paketin fiziksel arşiv dosyalarını içermediği sınır korunur. Yerel Contract
kökü ve Aktivite.rar yalnız okunur; test dosya URL'leri production'a taşınmaz.
Yeni araç kaynak dosyaları/kanıtları ayrı olduğundan eski finansal teslimin
275 dosyalı manifesti veya kabulü değiştirilmez. Dosya adaptörü sonraki
teslimde kendi kod/plan/prova hash'leriyle ayrıca paketlenmelidir.

## Prova kabul kriterleri

1. Gerçek izole DB commit'leriyle tüm 1.821 attachment ve aynı sayıda receipt;
   tüm içerikler mevcut R2FileStorage üzerinden yalnız yerel depoya.
2. Upload sonrası, commit öncesi ve commit sonrası/journal öncesi kontrollü
   kesintilerin aynı nesne/receipt ile devamı; çift ilişki/PUT olmaması.
3. Tam replay: 0 yeni attachment /0 yeni PUT.
4. Yanlış müşteri ve değişmiş dosya audit hash'inin reddi.
5. Uzak bozuk içerik ve metadata 403'ün reddi; nesnenin üzerine yazılmaması.
6. Kaldırılmış dosyanın yeniden etkinleşmemesi; kaldırma deneyi rollback.
7. Mevcut sözleşme ve müşteri dosya liste servisleriyle uyum.
8. Finansal tablolar ve orijinal kaynak audit parmak izlerinin önce/sonra eşitliği.

İlk bağlantı denemesi çalışma alanının ağ kısıtında durdu; onaylı sandbox dışı
erişimle prova hedefi okundu. Canlı katalog açılmadı. Araç build başarılı;
mevcut Identity uyumluluk ve NuGet güvenlik listesine erişim uyarıları var.
Ortak projelerdeki mevcut EF sürüm uyarıları kapsam dışıdır; bunlar sessizce
giderilmiş sayılmaz.

Kabulün dört dosyası bağımsız olarak tekrar hash'lendi; karşılaştırılan 15
finansal/audit parmak izi eşit. Business/Data/Model/Core DLL'leri önceki
finansal kabul ile aynı. NOT_APPROVED canlı uygulama ve yanlış prova teyidi
negatif denemeleri config/SQL/provider/çıktı açılmadan reddedildi.

`Prepare-CollectionCdnHandoff.ps1` yalnız offline paketler: kaynak, runtime,
kapalı onay, plan/prova/son profil ve belgeler. Fiziksel dosyalar, yerel sahte
nesneler, gerçek config veya credential pakete dahil edilmez. Yeni teslim yolu
`.tools/collection-cdn-handoff-2026-10-17-01`; asıl finansal teslim değişmez.

## Canlı için ayrı kalan kapılar

Gerçek CDN credential/namespace/izin/TLS/cache, güncel malware taraması,
public-link kabulü, onaylı pilot ve tam upload, tam public readback, API/UI
yetki ve tarayıcı kabulü henüz yapılmadı. Izole kabul bu kapıları açmaz.
DB'de yalnız StoredFileName saklanır; prova servisinin izole ayarla ürettiği
URL **isolated.invalid** adresidir. Normal CDN ayarlı uygulama aynı DB'den URL
üretirse gerçek CDN'de olmayan anahtarlara yönelir. Bu kayıtlar production'a
veya paylaşılan test uygulamasına gerçek dosya gibi taşınmaz.
Yeni izole prova gerekirse mevcut kendi kanıtları korunur; veri otomatik
silinmez. Finansal ilk-kurulum doğrulamasının dosya tabloları boş koşulu,
dosya fazından sonra kör tekrar çalıştırılmaz.

Kullanım ve onay alanları: [araç README'si](../../../tools/Collections.Files/README.md).
