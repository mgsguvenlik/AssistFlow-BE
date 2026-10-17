# 17 Ekim — Makbuz ve audit korumalı veri aktarım provası

**Sonuç: bu kapsamdaki veri provası tamamlandı.** İlk aktarım, tekrar, kontrollü
kesinti, dört koruma senaryosu ve son CHECKDB başarılı. Production geçişi henüz
onaylanmış veya uygulanmış değildir.

## Kapsam

Kullanıcının açık talebiyle yalnız `sql2022 / AssistFlow_Performance_Restore`
üzerinde çalışılır. Canlı **AssistFlow ve MGS'ye DDL/DML/ayar yazması yapılmaz**.
Önceki restore/şema sonucu [ayrı kayıttadır](execution-2026-10-17.md).

Bu, onaylanmış aynı-instance ve hedef-kilitli provadır; fiziksel/ağ izolasyonlu
başka sunucu olduğu iddia edilmez. WebAPI, genel seed, worker, Jenkins, Job,
SMS, mail, Manitou ve CDN çalıştırılmaz. Prova kimlikleri canlıya kopyalanmaz.

## Girdi ve uygulama

- 17 Ekim MGS salt-okuma kesiti: 9.517 sözleşme, 34.588 tarihçe, 243.518 ödeme.
  İki okuma hash'i aynı; final bakım anının transaction snapshot'ı değildir.
- Adaylar: **4.439 sözleşme, 17.269 tarife, 139.747 ödeme**. İnceleme/dışlama
  kapsamındaki **5.078 sözleşme ve 103.771 ödeme** otomatik kabul edilmez.
- Hash doğrulamalı capture, karar/finansal inceleme, ödeme ve ilişki paketleri
  kullanılır. Eksik servis tanımları, müşteri sınıflandırmaları ve grup üst
  kartları yalnız prova kopyasında uygulanır; ortak canlı verisi için onay
  verilmiş sayılmaz.
- [Collections.Rehearsal](../../../../tools/Collections.Rehearsal/README.md)
  ayrı uygulayıcıdır; mevcut test importer ve production hazırlık araçlarının
  güvenlik kilidi kaldırılmamıştır. Uygulama mevcut kalıcı ödeme RequestId,
  komut hash ve audit snapshot yardımcılarını kullanır.
- Ödeme + kalıcı makbuz + ham kaynak/hash aynı transaction içinde yazılır.
  Teknik aktör 0; legacy kişi/tarih etiketleri ham JSON içinde korunur.
  Europe/Istanbul → UTC dönüşümü açıktır; ad benzerliğiyle kullanıcı uydurulmaz.
- Uzun tarife açıklamasının tamamı ham kaynak audit'inde korunur. 100 karakterlik
  kısa değişiklik nedeni alanına sığmıyorsa açıklama kesilmez, kısa aktarım
  etiketi kullanılır. Şema genişletilmez.
- Dosyalarda **1.818 sözleşme ilişkisi ve 3 müşteri arşiv dosyası** yalnız yerel
  hash/sahiplik kanıtı olarak audit'e alınır; sahte CDN adresi veya attachment
  oluşturulmaz. Gerçek CDN yükleme/erişim kontrolü ayrı iştir.

## Doğrulama durumu

- Araç derlendi: **0 hata**, 9 uyarı; mevcut bağımlılık/nullable uyarıları.
- İlk uzun açıklama denemesi hata verdi ve tamamen geri alındı. Önce/sonra
  tablo parmak izleri aynı; başarısız denemenin kanıtı korunur.
- Düzeltilmiş kontrollü kesinti provası başarılı: sözleşme/tarife/referanslar
  ile ilk **2.000 ödeme, makbuz ve kaynak audit** yazımından sonra tüm transaction
  rollback edildi. Satır parmak izleri birebir aynı. Identity/rowversion
  sayaçlarındaki boşluklar beklenir; eski kimlikler yeniden kullanılmaz.
- Tam aktarım commit edildi, batch 3: **4.439 sözleşme, 17.269 tarife,
  139.747 ödeme ve 139.747 kalıcı ödeme makbuzu**. İlk aktarımda eksik/değişmiş
  ödeme 0. Kaynak ve hedef ayrı para birimi toplamları birebir:
  **TRY 26.684.052,43; USD 115.320,76**.
- **8.281 not, 26.228 grup geçmişi (+2 aynı tekrar yalnız audit), 56 grup üst
  kartı, 1.813 fatura ve 1.904 fatura ödemesi/makbuzu**, 13.790 cari kod ve
  83.543 banka tekrar-koruma kimliği aktarıldı. Cari kodlardan yalnız sorun
  bulunmayan 2.891'ine müşteri atanır; belirsizlerin sahibi boş, inceleme notu
  korunur. 2.602 mevcut müşteri tipi yalnız prova kopyasında değiştirildi;
  banka tipi/tenant sahipliği değiştirilmedi. Toplam **301.514 ham audit satırı**.
- Aynı paket ikinci kez çalıştırıldı: **0 yeni kayıt**; tüm 19 veri/referans
  parmak izi önce/sonra aynı. 139.747 ödeme ve kalıcı makbuz tekrar doğrulandı.
- Dört ayrı atılabilir transaction kontrolü başarılı ve geri alındı:
  **DELETE_PAYMENT** (makbuz kalır, ödeme yeniden yaratılmaz), **EDIT_PAYMENT**
  (düzenlenen tutar ezilmez), **CORRUPT_SOURCE** (bozuk audit hash'i reddedilir),
  **REMOVE_RECEIPT** (eksik makbuzla devam edilmez). Bunlar aynı committed-batch
  replay doğrulama yolunu kullanır; tarayıcı/API kabul testi değildir.
- Kontrollerin önce/sonra **19 veri/referans parmak izi aynı**; ayrıca ilk
  commit'in son durumuyla da birebir aynı. Deneme değişiklikleri kalıcı değil.
- Son **CHECKDB temiz**, REPAIR yok. **95 migration** korunuyor. Önceki şema
  sonrası profille karşılaştırmada SQL Agent etkinlik/adım hash metadatası,
  tanım/menü/şablon/rol snapshot'ları aynı. SMS kuyruğu ve gerçek attachment
  tabloları boş; dış servis çağrısı 0.

## Özel kanıt dizinleri

BE `.tools` altında, Git dışında:

- `collection-rehearsal-transfer-plan-2026-10-17-01/plan.json`:
  SHA-256 `383728247C84DA592BD37D87D04F63BD7BE05FAA2CADBA2B55C0FCFC6C69D712`.
- `collection-rehearsal-transfer-rollback-2026-10-17-01/failure.json`:
  ilk şema sınırı hatası ve tam rollback kanıtı.
- `collection-rehearsal-transfer-rollback-2026-10-17-02/result.json`:
  kontrollü kesinti/rollback başarısı.
- `collection-rehearsal-transfer-apply-2026-10-17-01/`: tam aktarım çalışması.
- `collection-rehearsal-transfer-replay-2026-10-17-01/result.json`:
  0 yeni kayıt ve aynı parmak izleri.
- `collection-rehearsal-transfer-resilience-2026-10-17-01/result.json`:
  dört başarılı/geri alınmış kontrol; commit sonrası durum korunur.
- `collection-rehearsal-data-after-2026-10-17-01.json`: son CHECKDB, şema,
  sayım, tanım/rol ve Job metadata profili.

| Kanıt | SHA-256 |
| --- | --- |
| Kesinti/rollback sonucu | `1D55BEBEAFA0EA217797A33EDB28730561A93BF8154FA390CB59FCEB16093FD3` |
| İlk aktarım sonucu | `5DC12A7057A5DED4268BBC9C424D0F3B5C84CE155E04318BA0F91FC4023E5207` |
| İkinci çalışma sonucu | `BB78DC4FB0DB6A6F91C14A9ED06C68F47AD10591B2F69946B0EB555345ED1B85` |
| Dört koruma kontrolü | `0A94F16D0CDDE8F6C5C61DBF9AD5A2804C71A4FF1417FF25F82E9423BA3DBCD9` |
| Son DB profili | `F656D61F9DBCF9568F8AF149722D2AB09522AAE8517FC2EFE4CF20203FA00A5E` |

Uygulanan araç kaynak SHA-256:
`6A50A3615B866D0E12EBE30F7BAC4EE0CAB019A4FB2D48F5D49929C6EC49503E`.
Derlenen DLL SHA-256:
`465AD137A5996094831B46C417F1AD451DEA1A03CD776787EEB0EF03DB607CBC`.

## Canlıya geçişte ayrı kalan kapılar

Son bakım kesiti ve canlı referans/karar hash'lerinin yeniden hazırlanması;
gerçek operatör/rol onayı; ortak müşteri/prosedür farklarının onayı; production
CDN ve dosya güvenlik/erişim kontrolleri; uygulama başlangıcı, yetki ve ekran
kabulü; operasyonel geri dönüş planı. Transaction rollback provası, import
sonrası başka DB'ye baseline restore'u veya belirsiz ağ/commit senaryosunun
ayrıca sınandığı anlamına gelmez. Production durumu **NO-GO** olarak korunur.
