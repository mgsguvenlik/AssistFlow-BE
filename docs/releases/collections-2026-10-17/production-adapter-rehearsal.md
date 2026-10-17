# Production adaptörü — yazmalı prova, 17 Ekim 2026

## Kapsam ve güncel durum

Kullanıcı backend/prova bağlantılarını kapattığını bildirdi ve yazmalı aktarım,
geri alma ve tekrar güvenliği kontrollerini onayladı. Tek yazma hedefi
`sql2022 / AssistFlow_Performance_Restore` olarak sabittir. Canlı AssistFlow ve
MGS değiştirilmez. WebAPI, worker, Jenkins, SMS, mail veya CDN başlatılmaz.

**Durum: yazmalı adaptör provası tamamlandı.** Temiz başlangıç, kontrollü
kesinti/rollback, tam aktarım, tekrar, dört koruma ve son CHECKDB başarılı.
`WritePathVerified=true`, `ProductionAuthorizationGranted=false`.
Bu teknik kabul canlı uygulama onayı değildir; canlı NO-GO kapıları korunur.

## Sabit bağlama

- İnceleme paketi: `collection-cutover-review-2026-10-17-02`.
- Bundle SHA-256: `8253BCA52AED33271A445FE6B46EBEF7A86E3F5220C48239B54E7B6F12AFD673`.
- Prova planı: `collection-production-adapter-rehearsal-plan-2026-10-17-02`.
- Plan SHA-256: `114B375A0CDC6D366FBB88CCE4AAC6E80CA1063BB8827EF7AB31175D97E9D15E`.
- Adaptör DLL SHA-256: `0E7A6D82F217AE1AE89C7395BA5F4BD07FF08386499BC84589022F0DF2C7722F`.
- Çekirdek SHA-256: `039E32052A2C85B6651DA74AC1C17B40D834C0C7761A0AE62933F741C52D9AE4`.

Plan, girdi ve Business/Data/Model/Core sürümleri çalışma öncesinde doğrulanır.
Prova referansları canlı müşteri ID'leri yerine kullanılmaz. Mühürlü inceleme
paketine geriye dönük dosya eklenmedi; bu kabul ayrı, hash bağlı kanıttır.

## Gerçekleştirilen sıra

1. Açık oturum kontrolü geçti; zorla oturum kapatma yapılmadı. Mevcut 17 Ekim
   00:00:02 AssistFlow tam yedeği VERIFYONLY ile doğrulandı ve yalnız onaylı
   disposable hedefe restore edildi. Yeni canlı yedek işlemi yapılmadı.
2. Restore sonrası CHECKDB hatasız; REPAIR çalıştırılmadı.
3. Hash bağlı 21 migration ve iki seed yalnız prova hedefine uygulandı.
4. `rollback-check`: 4.439 sözleşme, 17.269 tarife ve ilk 2.000 ödeme/makbuz/
   ham kaynak yazımından sonra kontrollü kesinti üretildi. Referans değişiklikleri
   ve çalıştırma/onay audit'i dahil bütün transaction geri alındı.
   `ControlledInterruption=true`, `StateUnchanged=true`; 19 veri/referans
   parmak izi başlangıçla aynı. Identity/rowversion sayaç boşlukları veri kaybı
   veya mükerrer kayıt anlamına gelmez.
5. `apply`: batch **2** commit edildi. 4.439 sözleşme, 17.269 tarife,
   139.747 ödeme ve 139.747 makbuz doğrulandı. Kaynak/hedef tutarlar ayrı ayrı
   TRY **26.684.052,43**, USD **115.320,76**; fark yok.
6. Aynı plan/paketle ikinci `apply`: **IDEMPOTENT_REPLAY**, sıfır yeni kayıt;
   önce/sonra 19 veri/referans parmak izi aynı. Yeni batch üretilmedi;
   mevcut batch 2'nin makbuz/audit'i doğrulandı.
7. `resilience-check`: aynı batch replay doğrulama yolunda dört kontrollü
   değişiklik denendi; hepsi başarılı ve ayrı transaction'larda geri alındı:
   silinen ödeme yeniden üretilmedi, düzenlenen ödeme ezilmedi, bozuk kaynak
   hash'i ve eksik kalıcı makbuz reddedildi. Son 19 parmak izi ilk commit'le
   aynı; deneylerden kalıcı silme/düzenleme kalmadı.
8. Son CHECKDB hatasız, REPAIR yok; 95 migration, 34 collection tablosu,
   ONLINE/MULTI_USER. TRUSTWORTHY/chaining/Broker kapalı. Job/step metadata'sı
   bu restore sonrası başlangıç profiliyle aynı. Sekiz tanım/menü/SMS/rol
   snapshot'ı önceki doğrulanmış aynı şema baseline'ıyla birebir aynı.
9. `certify-rehearsal` aynı kod, plan, paket ve gerçek yazma/rollback/replay/
   koruma kanıtlarını doğrulayarak teknik kabul kaydını oluşturdu.

Mevcut adaptör yeniden derlendi: **0 hata /3 uyarı**. Derleme hash'i mevcut
`-02` planıyla aynı kaldı; finansal kod veya kullanıcı Development config'i
değiştirilmedi. FE değişikliği yoktur.

| İlişki | Doğrulanan kayıt |
| --- | ---: |
| Not | 8.281 |
| Grup durum geçmişi | 26.228 |
| Grup alias — yalnız ham audit | 2 |
| Grup üst kartı | 56 |
| Fatura | 1.813 |
| Fatura ödemesi | 1.904 |
| Cari kodu | 13.790 |
| Banka tekrar-koruma kimliği | 83.543 |
| Ham kaynak audit'i | 301.515 |

Ham audit sayısına bu adaptörün plan/onay bağlamasını koruyan bir
`ExecutionApproval` kaydı dahildir. Böylece önceki aktarıcının 301.514 satırlık
kanıtından bir fazladır; kaynak finansal kayıt sayısı değişmemiştir.

## Özel kanıt dizinleri

Kişisel veri içeren dosyalar Git dışı BE `.tools` altında tutulur:

- `collection-adapter-restore-plan-2026-10-17-03`
- `collection-adapter-restore-apply-2026-10-17-01`
- `collection-adapter-restored-2026-10-17-01.json`
- `collection-adapter-schema-2026-10-17-01.json`
- `collection-production-adapter-rollback-2026-10-17-01`
- `collection-production-adapter-apply-2026-10-17-01`
- `collection-production-adapter-replay-2026-10-17-01`
- `collection-production-adapter-resilience-2026-10-17-01`
- `collection-production-adapter-final-2026-10-17-01.json`
- `collection-production-adapter-acceptance-2026-10-17-01/acceptance.json`

| Kanıt | SHA-256 |
| --- | --- |
| İlk aktarım `result.json` | `A92ACC7CA076550B21E1088381319F0EF4E2E45225D483F79D996D7BE6817E67` |
| Tekrar `result.json` | `B5B394254F2EBCCEC59FF75E39F4259BCE98145EE0174F75C3B50C5FEC7FEAC1` |
| Kesinti `rollback.json` | `243A8F80572365F55733EBD39E5574A80E9268C16FB1E41089A80063769DD3E2` |
| Dört koruma `result.json` | `38DAE3581E6565577BD108D2CFE8A6B09B91920186200E2A5C5FDCD85C9E31DC` |
| Son profil/CHECKDB | `C398B60B599786F9163494715F0B4D074BBF76A3BD0E5C9CD0C64E6521C267A8` |
| Teknik kabul `acceptance.json` | `C2C5208823D042ACE0E42AA332A759561CEADA5779BD20ADA387220C1B2042B4` |

Kabul kaydı alt kanıtların yollarını/hash'lerini ve adaptör/bağımlılık
hash'lerini içerir. Yeni kod/paket için bu kabul otomatik geçerli sayılmaz.

## Kabul sınırı

Transaction rollback, genel DB backup/PIT geri dönüşüyle aynı değildir. Fiziksel
ağ kesintisinde belirsiz commit, CDN dosya aktarımı/erişim politikası, kullanıcı
HTTP kabulü, operatör/rol, ortak tablo/prosedür onayı, Job ve ilk worker
izolasyonu, bakım sonrası taze kaynak kesiti ve canlı uygulama yetkisi ayrı
kapılardır. Prova başarısı bunları otomatik açmaz.
