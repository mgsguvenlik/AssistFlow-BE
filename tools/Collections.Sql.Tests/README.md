# Tahsilat SQL transaction kabul testleri

## Salt-okuma tanım servisi doğrulaması

`dotnet run --project tools/Collections.Sql.Tests -- --read-definitions WebAPI/appsettings.Development.json`

Beş tanım türünde gerçek servis sorgusunu, ilk/ikinci sayfaların sırasını ve toplam sayısını doğrular. Geçersiz tür/sayfa boyutunu kontrol eder. Hiçbir seed, fixture veya veri yazma çalıştırmaz. Aktif seçim listesidir; pasif tarihsel referansların detay görüntülemesi ayrıca uygulanmalıdır.

## Kalıcı tanım kurulumu (test fixture değildir)

```powershell
dotnet run --project tools/Collections.Sql.Tests -- --seed-definitions WebAPI/appsettings.Development.json
```

Bu açık mod mevcut SeedRunner'a yalnız CollectionDefinitionSeed verir; global seed/host/migration çalıştırmaz. Collection tanımlarını **kalıcı olarak** ekler, silmez. Aynı seed iki kez çalıştırılır; mevcut kayıtların Id/Code/Name/IsActive değerleri ve dönem alanlarının korunması, tekrar çalıştırmanın değişiklik üretmemesi ve 30 legacy kod karşılığı doğrulanır. Yeni tarihsel ödeme yöntemleri pasiftir; diğer tanımlar aktiftir. Sözleşme durumunda IsActive seçilebilirliktir, borç uygunluğu değildir; UNKNOWN/NONE borç üretmez.

Seed transaction içindeki uygulama kilidiyle eşzamanlı seed çalıştırmalarını sıralar. Var olan kodda dönem uyuşmazlığı veya farklı kodda ad/interval çakışması varsa otomatik düzeltme yapmaz. DB unique kısıtları da korunur. Kullanıcı düzenlediği mevcut ad/aktiflik değerleri ezilmez. Genel otomatik başlangıç seed listesine eklenmemiştir; production kurulumu ayrıca planlanmalıdır.

## Geçici ödeme testleri

Repository kökünden (AssistFlow-BE):

```powershell
dotnet run --project tools/Collections.Sql.Tests -- --apply-test-fixtures WebAPI/appsettings.Development.json
```

Yalnız `192.168.1.8 / AssistFlowTest` kabul edilir. Credential dosyadan okunur, çıktıya yazılmaz. Host/seed/migration başlatılmaz. Mevcut Customer, ServiceType ve CurrencyType kimlikleri yalnız okunur; yeni işaretli test sözleşmesi, ödeme ve işlem makbuzları collection içinde oluşturulur. Finansal iş kuralları veya HTTP yetkisi değil, persistence primitive'i sınanır.

Kontroller: create/replay, farklı kullanıcı/içerik çatışması, rowversion, makbuz yazımı öncesi enjekte edilen hatada rollback, tekrar deneme, commit sonrası TimeoutException ile yanıt kaybı simülasyonu, bariyerle iki işlemi yazma sınırına getiren yarış, fiziksel silme ve silme sonrası eski isteğin yeniden ödeme oluşturmaması. SQL retry açık test context'i kullanılır; gerçek ağ kesintisi/SQL failover veya production yapılandırma kabulü iddiası değildir.

`finally` yalnız çalışmanın sözleşme kimliği + rastgele işareti ve ürettiği request anahtarlarını temizler. Ortak kayıtlar silinmez. Identity sayaçları test/rollback nedeniyle ilerleyebilir, reseed yapılmaz. Süreç zorla öldürülürse finally çalışamayabilir; çıktıda yazan test kimliği/işareti ile kayıtlar incelenmeli, genel tablo temizliği yapılmamalıdır. Commit belirsizliği sırasında fixture kurulumu için otomatik kalıntı keşfi henüz yoktur.

Üretim kodu DI veya endpoint'e açılmamıştır. Kullanıcı yetkisi ve muhasebe eligibility matrisi ayrıca tamamlanmalıdır.
