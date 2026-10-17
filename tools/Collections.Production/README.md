# Tahsilat — canlıya özel makbuz/audit korumalı adaptör

Bu ayrı konsol uygulaması WebAPI/general seed/worker başlatmaz. Mevcut prova
aracının sabit DB kilidi kaldırılmadı. Ortak finansal işlemler
`Collections.Transfer/CollectionTransferCore.cs` içine mekanik olarak ayrıldı;
prova ve canlı adaptörleri aynı çekirdeği compile-link eder. Mevcut RequestId,
komut hash'i, ödeme snapshot ve banka kimlik yardımcıları korunur.

**Durum:** 17 Ekim'de bağlantılar kapatıldıktan sonra yeni adaptörün temiz
başlangıçtan **kesinti rollback → ilk yazma → tekrar → dört koruma** provası
yalnız AssistFlow_Performance_Restore üzerinde tamamlandı. 4.439 sözleşme,
17.269 tarife, 139.747 ödeme/makbuz ve para birimi tutarları doğrulandı;
tekrar sıfır yeni kayıt, son CHECKDB temiz. Aynı `-02` kod/plan/paketine bağlı
teknik kabul oluştu; **canlı uygulama onayı değildir**.
[Gerçek kanıtlar ve kalan kapılar](../../docs/releases/collections-2026-10-17/production-adapter-rehearsal.md).

## Hazırlama (DB bağlantısı yok)

BE kökünden güncel derlemeyi kullan:

```powershell
dotnet build tools/Collections.Production/Collections.Production.csproj
dotnet tools/Collections.Production/bin/Debug/net9.0/Collections.Production.dll plan-live .tools/BUNDLE BUNDLE_SHA .tools/YENI-LIVE-PLAN
dotnet tools/Collections.Production/bin/Debug/net9.0/Collections.Production.dll plan-rehearsal .tools/BUNDLE BUNDLE_SHA .tools/CAPTURE .tools/REVIEW .tools/PAYMENTS .tools/RELATIONS .tools/YENI-REHEARSAL-PLAN
```

Bundle SHA, `bundle-hashes.json` içeriğinin SHA-256 değeridir. Canlı plan yalnız
canlı referanslarını kullanır. Prova bağlamasında aynı MGS kaynak byte'ları,
sözleşme/tarife/ödeme ve ilişkili kayıt kapsamı zorunludur; farklı hedef ID'leri
yalnız o hedefin hash bağlı referans planından gelebilir. Prova ID'leri canlıya
kopyalanmaz. Hedefler yalnız `sql2022 / AssistFlow` veya kullanıcı onaylı
`AssistFlow_Performance_Restore` enum'udur; serbest DB/SQL parametresi yoktur.

Plan bundle/girdi/migration/hash ile adaptör, çekirdek ve Business/Data/Model/Core
sürümlerine bağlanır. Kod/derleme/girdi değişirse eski plan çalışmaz. Çıktılar
yalnız yeni `.tools` klasörleridir; eski kanıtlar ezilmez. Canlı onay şablonu
varsayılan kapalıdır. Prova şablonu yalnız kullanıcının prova talebini temsil eder,
canlı onayı veya uydurma insan kimliği değildir.

## Salt-okunur mevcut prova kontrolü

```text
verify-existing <config> <plan klasörü> <plan SHA> <onay dosyası> <onay SHA> <yeni çıktı> REHEARSAL_ONLY
```

Yalnız prova planıyla çalışır. Eski `rehearsal-receipt-v1` batch'ini **sahiplenmeden,
değiştirmeden** kontrol eder. Kaynak/makbuz/alan/tutar ve önce/sonra hash kontrolü
yapar; `DatabaseWrites=false, WritePathVerified=false` kaydeder. Aktif uygulama
veriyi değiştirirse karışık kesiti başarılı saymaz. SQL metadata IF/SELECT dışında
DB işlemi yoktur; restore, transaction hatası veya mali test yazması yapmaz.

## Yazmalı prova (bağlantılar kapandıktan sonra)

`apply`, `rollback-check`, `resilience-check` aynı yedi argümanı kullanır:

```text
<config> <plan klasörü> <plan SHA> <onay dosyası> <onay SHA> <yeni çıktı> REHEARSAL_ONLY
```

Önce disposable prova DB'sinin restore/şema baseline'ı hazırlanır. Mevcut veriler
aktarıcı tarafından silinmez. İlk aktarım tüm financial/source/map/makbuz/banka
tablolarının boş olmasını gerektirir. Başka kullanıcı oturumu varsa yazma
başlamadan durur; oturum zorla sonlandırılmaz. Önce `rollback-check`, sonra
`apply`, tekrar aynı `apply`, ardından `resilience-check`. Her sonuç yeni dizine.

Makbuz+ham kaynak+finansal kayıt+onay audit'i aynı Serializable transaction'dadır.
DB-scope application lock kullanılır; migration/seed/prosedür veya SQL Agent
değişikliği yapılmaz. Teknik aktör 0 ve ham legacy kişi/tarih etiketleri korunur.
Fatura/banka/not/grup/cari ve yerel dosya kanıtı da aynı kapsamda doğrulanır;
gerçek CDN upload/attachment yoktur. Kalıcı ödeme makbuzu fiziksel silmeden
sonra da kalır; tekrar silineni yaratmaz, düzenleneni ezmez. Bu **ilk kapalı
cutover batch'i** içindir; canlı kullanım başladıktan sonra genel delta importer
değildir. CDN ilişkileri açılmadan önce tam veri/replay mutabakatı tamamlanır.

Başarılı ilk aktarım, replay, kontrollü rollback ve dört dayanıklılık sonucu:

```text
certify-rehearsal <prova planı> <plan SHA> <apply çıktı> <replay çıktı> <rollback çıktı> <resilience çıktı> <yeni kabul çıktısı>
```

Bu komut yalnız çevrimdışı kanıt birleştirir. Hedef/plan/bundle, tüm veri hash'leri
ve gerçek yazma/geri alma sonuçları aynı olmalıdır. Salt-okunur sonuçtan kabul
üretilemez. `acceptance.json` insan onayı değildir, gerçek canlı onayının ayrıca
bağlaması gereken teknik kanıttır. Kod veya paket değişirse yeni prova gerekir.

## Canlı kapıları — bu çalışmada kullanılmadı

`apply ... APPLY_LIVE_ASSISTFLOW` dışında canlı hedefli komut yoktur; canlıda
silme/düzenleme/hata deneyi çalıştırılamaz. Config bağlantısı açılmadan:

- açık production authorization, doğru hedef/kapsam/token ve süreli onay;
- gerçek operatör Code'u ve dış onay kaydı; insan/bakım/yedek/ortak değişiklik/
  başlangıç izolasyonu kanıtlarının dosya hash'leri;
- aynı adaptör/bağımlılık hash'leri ve aynı paket için **tamamlanmış yazmalı
  adaptör provası** ve kaynak makbuz dosyaları;
- bakım başlangıcından sonra alınmış, 30 dakikadan eski olmayan son kesit gerekir.

Ardından source/target sabit SELECT'leri iki kez okunur. MGS kaynak byte'ları
değiştiyse yazma yok. İlk importta hedef referans parmak izi ayrıca kilitli
transaction içinde karşılaştırılır; replay'de daha önce eklenen servis/üst kart
ve değişen müşteri tipleri yanlışlıkla kaynak drift'i sayılmaz. Standalone
aracın kendi kapatılmış preview bağlantı havuzları temizlenir, başka süreçlere
ait oturumlara dokunulmaz.

DB guard'ları: tam sunucu/hedef, tam migration baseline'ı/pending=0, aktif
trigger yokluğu, Id=0 kullanıcı çakışması, aktif/tekil onaylayan kullanıcı,
hedefte başka kullanıcı oturumu olmaması ve dört ilgili legacy/hedef Job'ın
kapalı/çalışmıyor olması. Joblar araç tarafından kapatılmaz. Başlangıç worker
izolasyonu ayrıca operatör kanıtıdır; shared uygulama davranışı değiştirilmez.

Commit denendikten sonraki hata **rollback edildi** diye sunulmaz:
`commit-uncertain.json` mutabakat gerektirir. Başarılı commit'in yerel son raporu
yazılamazsa DB geri alındı kabul edilmez. Kör tekrar/Down/DROP/TRUNCATE/audit
silme yoktur. Fiziksel ağ kesintisiyle belirsiz commit kabulü ayrıca açıktır.
Genel DB backup/PIT geri dönüş, CDN ve HTTP/kullanıcı kabulü ayrı kapılardır.
