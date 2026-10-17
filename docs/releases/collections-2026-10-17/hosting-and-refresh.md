# Gerçek prova hostu ve yeni backend paketi — 17 Ekim

## Kapsam

**Durum: gerçek izole host kabulü ve yeni sürüme bağlı yazmalı prova tamamlandı.**
Teknik kabul canlı yetki değildir; yeni kapalı teslim ayrı dizinde üretildi.

Yalnız `sql2022 / AssistFlow_Performance_Restore` üzerinde çalışma yetkilidir.
Canlı AssistFlow/MGS'ye DDL/DML/Job/config/deploy yapılmaz. Kaynak veri ve şema
paketi önceki mühürlü `collection-cutover-review-2026-10-17-02`'dir; bu çalışma
yeni bir bakım-sonu kaynak kesiti alındığı anlamına gelmez.

## Gerçek hosting kabulü

- Gerçek derlenmiş WebAPI.dll ve Program.cs, ayrı child process, gerçek Kestrel
  ve loopback HTTP ile açıldı; test sunucusu veya yalnız Build kontrolü değildir.
- Ortam `CollectionRehearsal`, target katalog ve uygulama adı sabit; bağlantı
  metadata observer'ı yalnız prova hedefi/sunucu/oturum sayısını SELECT ile
  doğrular. Diğer oturumlar zorla kapatılmaz.
- Migration/seed iki girişte de kapalı, Manitou ilk turdan önce kapalıdır.
  SMS ve Tahsilat kapalı, SMS simülasyonu açıktır.
- Diğer dört worker'ın açılış etkileri bulundu: mail constructor'ı DB okur;
  Helpdesk, SLA ve periyodik rapor ilk turda yan etki oluşturabilir. Yalnız
  `IsolatedRehearsalMode=true` olduğunda bunlar **oluşturulmadan** atlanır.
  Manitou gerçek worker'ı çalıştırılır ve kendi kapalı kontrolünden döner.
- İzolasyon modu yalnız özel ortam + AssistFlow_Performance_Restore + sabit
  uygulama adı + kapalı startup/Tahsilat/SMS ayarlarıyla options validation'dan
  geçer; Production/canlı katalog reddedilir. Varsayılan false'tur. Gerçek
  production ortamında diğer worker'ların normal görevleri global kapatılmaz.
- Yalnız tanımsız GET probe'unda **404** alındı: middleware/HTTP host çalışıyor;
  finansal controller veya kimlikli kullanıcı kabulü yapıldığı anlamına gelmez.
- Yalnız araca ait child process kapatıldı; bu graceful shutdown kabulü değil,
  kısa ömürlü izole host kontrolüdür. Başka BE veya SQL oturumu sonlandırılmadı.
- `.tools/collection-host-isolation-before-2026-10-17-01.json` ve `after-...-01`
  arasında tablo sayım/metadata, migration, seed/rol, Job ve modül profilleri
  aynı. Bu profil kontrolü tüm satırların kriptografik hash'i gibi sunulmaz.

İlk üç özel host çıktısı log kodlaması nedeniyle Passed=false kaldı; ilk süreç
ön kontrolü aynı PowerShell'deki gözlemci havuzundan dolayı başlamadan durdu.
Oturum KILL veya veri temizliği uygulanmadı. Windows child stdout'u OEM-857
olarak okunarak `collection-host-isolation-2026-10-17-04` gerçek kabulü alındı.
Eski başarısız çıktılar silinmedi veya başarıya çevrilmedi. Hassas prova config'i
ve yalnız bu hosta ait DataProtection anahtarları Git dışındadır/paketlenmez.

## Yeni sürüm kontrolleri

- Çözüm build: 0 hata /811 mevcut uyarı; aktarıcı build: 0 hata /9 uyarı.
- 18 bağlantısız startup kontrolü geçti; yeni framework yok. Buna Production/
  canlı katalog reddi ve diğer worker constructor'ının prova modunda
  oluşturulmaması, normal modda çalışması dahildir.
- Core/Business/Data/Model DLL'leri gerçek WebAPI ve yeni aktarıcıda aynı;
  hash'ler eski kabul üzerinde elle değiştirilmedi. Finansal çekirdek kaynak
  hash'i değişmedi: `039E32052A2C85B6651DA74AC1C17B40D834C0C7761A0AE62933F741C52D9AE4`.
- Yeni prova planı `collection-production-adapter-rehearsal-plan-2026-10-17-03`:
  `20E39C60C7AD1C94D25BF0FA2C52257DF97346634E4E69BB07A56118B6C3B7B4`.
- Ayrı kapalı canlı planı `collection-production-adapter-live-plan-2026-10-17-03`:
  `3D59FE431E232A6A401C3B7F724DD919B6DF5B5080725F823C904B37845F2AB2`.

## Yeni yazmalı prova — durum

Yeni sürüme bağlı temiz başlangıç restore'u yalnız disposable hedefte yapıldı;
CHECKDB temiz, 21 migration ve iki seed tekrar uygulandı. Kontrollü kesinti ve
rollback başarılı. Tam aktarım batch 2 commit edildi: 4.439 sözleşme, 17.269
tarife, 139.747 ödeme/makbuz; TRY 26.684.052,43 / USD 115.320,76 kaynakla aynı.
8.281 not, 26.228 grup geçmişi (+2 alias audit), 56 üst kart, 1.813 fatura,
1.904 fatura ödemesi, 13.790 cari, 83.543 banka kimliği ve 301.515 audit
doğrulandı. Tekrar sıfır yeni kayıt. Dört koruma geçti/geri alındı;
19 veri/referans parmak izi ilk commit'le aynı. Yeni teknik kabul üretildi.

Finansal prova sonrası gerçek host ikinci kez aynı DLL'lerle açıldı, 404 HTTP
probe ve tüm kapalı başlangıç logları doğrulandı; yalnız araca ait host kapandı.
Bu son host öncesi/sonrası sayım/şema/seed/rol/modül/Job profilleri aynı;
son CHECKDB temiz (REPAIR yok), 95 migration/34 collection tablosu.

## Güncel kanıt ve paket bağlaması

BE `.tools` altında yeni kanıtlar; eski dosyaların üzerine yazılmadı:

- `collection-production-adapter-{rollback,apply,replay,resilience}-2026-10-17-02`.
- `collection-production-adapter-acceptance-2026-10-17-02/acceptance.json`:
  `7F0C9271BFD49F414851EF9DCD473F89E74D707D9E71EB12A4A5F5760EF0AA14`.
- `collection-host-isolation-2026-10-17-05/result.json`:
  `7D908024B23309D5D73CEE165FB8EBA874C726353241B6A0A092E418EA1F340B`.
  Son kabul WebAPI/Business/Data/Model/Core hash'lerini de kaydeder.
- `collection-production-adapter-final-2026-10-17-02.json`:
  `D267C6E446B307645AA20E92B5EC043299A4252615713143A2AA564FCA2FBD30`.
- İkinci host öncesi profil: `collection-host-isolation-before-2026-10-17-02.json`.

WebAPI SHA: `01C20D5AD605F90A798690CEE65556AA82A660E7BE84A4D71DA4CEC7DA4C51A3`.
Adapter SHA: `052646E0C2B88992B7A789A3832A77EBF3077CF3AC32E971D9655043AA620A75`.
Host ve adaptörün dört backend bağımlılığı birbirine ve kabul/plan hash'lerine
birebir eşleştirilir. Paketleyici yeni host kanıtı olmadan bu eşleşmeyi varsaymaz.
Yeni teslim adı `collection-cutover-handoff-2026-10-17-02`:
**275 dosya /468.616.497 byte** (manifest hariç), ikinci okumada tüm dosyaların
hash'i eşleşti. Manifest SHA:
`6BC34709D364D02DCD72F05F682A7C1D805690A88D8971375A98B313F2C55ABC`.
Host proof, startup kaynakları ve WebAPI runtime metadata'sı da pakettedir;
appsettings/credential ve test DataProtection anahtarları dışarıda tutuldu.
Paket içi belgeler mühürleme anındaki kesittir; bu repo belgesindeki teslim
hash'i sonradan yazılmış güncel kayıttır, paket geriye dönük değiştirilmedi.

Yeni runtime'da kapalı canlı onayı ve eski kodun planı, config/SQL/çıktı açılmadan
reddedildi. Canlı plan `NOT_APPROVED`, kabul
`ProductionAuthorizationGranted=false`; kaynak/şema inceleme bundle'ı değişmedi.

## Kabul sınırı

Bu gerçek host kontrolü **özel izole prova ortamına** aittir. Canlı production
hosting/IIS/Jenkins override'ının gerçekten ulaştığı, diğer production
worker'ların yetkili işletimi, SQL Job durdurma/devam, dış Manitou çağrısı,
kimlikli ekran kabulü, gerçek CDN/SMS ve backup-PIT ayrı kapılardır. Güncel
bakım kesiti/hash veya kod değişirse aynı pakete bağlı yeni kabul gerekir;
30 dakikalık son kesit kapısı korunur. Canlı onayı NOT_APPROVED kalır.
