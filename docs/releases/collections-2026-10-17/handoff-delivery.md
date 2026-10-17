# Prova sonrası canlı geçiş paketi — teslim kaydı

17 Ekim 2026, yalnız yerel/offline hazırlık tamamlandı. **DB bağlantısı açılmadı;
canlı AssistFlow ve MGS'ye işlem uygulanmadı.** WebAPI/Jenkins/Job/CDN/SMS/mail
başlatılmadı. Runtime veya FE/BE iş mantığı değiştirilmedi; kullanıcının
Development config değişikliği korundu.

## Üretilen paket

BE kökünde Git dışı `.tools/collection-cutover-handoff-2026-10-17-01`:

- **258 içerik dosyası**, 467.836.552 byte (manifest hariç).
- Manifest: `handoff-manifest.json`.
- Manifest SHA-256: `AAC3F982035C99167F9AEF8446F4470B374442D09F8FAB4959D3CB084B5111D5`.
- Önceki bundle SHA-256: `8253BCA52AED33271A445FE6B46EBEF7A86E3F5220C48239B54E7B6F12AFD673`.
- Canlı plan SHA-256: `20FC5BAE18844C91DF0FC5E7E3DBE6CEBD7B686042A35ECA05404E71B9C24D2D`.
- Teknik kabul SHA-256: `C2C5208823D042ACE0E42AA332A759561CEADA5779BD20ADA387220C1B2042B4`.

İçerik: byte'ları değişmemiş orijinal veri/şema/karar paketi, provada çalışan
adaptör runtime'ı ve bağımlılıkları, hash bağlı canlı/prova planları, kapalı
onay şablonları, gerçek yazmalı prova kabulü/alt kanıtları, son CHECKDB profili
ve uygulama belgeleri. Config/credential, gerçek arşivler ve CDN nesneleri yok.
Bu kişisel veri içeren özel bir teslimdir; GitHub'a veya açık linke konmaz.

## Doğrulama

- PowerShell syntax kontrolü hatasız.
- Paket üretiminden bağımsız ikinci okumada 258 dosyanın tamamı manifestle
  SHA-256 düzeyinde eşleşti.
- Yanlış bundle hash'iyle olumsuz deneme, çıktı dizini oluşmadan reddedildi.
- Canlı şablon `NOT_APPROVED`; `ProductionApplyAllowed=false`.
- Kaynak bundle değiştirilmedi; teknik kabul aynı adaptör/çekirdek ve
  Business/Data/Model/Core DLL hash'lerine bağlı.
- `git diff --check` başarılı; bu adımda FE/BE çalışma kodu değişmediği için
  uygulama build/lint veya yeni bir test altyapısı çalıştırılmadı.

## Sonraki sıra

[Tam bakım ve kabul sırası](execution-handoff.md) aktif uygulama planıdır.
Öncelik, **BE açılışındaki genel seed/ilk Manitou worker etkisinin kontrollü
izolasyonu**, canlı operatör/rol ve ortak fark onayları, Job/bakım/yedek-PIT ve
CDN/HTTP kabulüdür. Sonra bakım sonrası yeni kaynak/paket ve gerektiğinde
aynı pakete bağlı yeni prova gerekir. Eski kesit veya eski timestamp'li
onay çalıştırılmaz; 30 dakika kapısı atlanmaz. Tam DB/PIT geri dönüşü ve
fiziksel ağ kesintili commit-belirsizliği kabulü transaction rollback
provasıyla tamamlanmış sayılmaz.

**Paketleme tamamlandı; operasyonel kapılar ve canlı GO tamamlanmadı.**
Bu aşamada kaynak DB'yi okuma veya yeni prova restore'u da yapılmadı.
