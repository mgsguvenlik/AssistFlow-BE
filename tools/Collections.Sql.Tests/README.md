# Tahsilat SQL transaction kabul testleri

Repository kökünden (AssistFlow-BE):

```powershell
dotnet run --project tools/Collections.Sql.Tests -- --apply-test-fixtures WebAPI/appsettings.Development.json
```

Yalnız `192.168.1.8 / AssistFlowTest` kabul edilir. Credential dosyadan okunur, çıktıya yazılmaz. Host/seed/migration başlatılmaz. Mevcut Customer, ServiceType ve CurrencyType kimlikleri yalnız okunur; yeni işaretli test sözleşmesi, ödeme ve işlem makbuzları collection içinde oluşturulur. Finansal iş kuralları veya HTTP yetkisi değil, persistence primitive'i sınanır.

Kontroller: create/replay, farklı kullanıcı/içerik çatışması, rowversion, makbuz yazımı öncesi enjekte edilen hatada rollback, tekrar deneme, commit sonrası TimeoutException ile yanıt kaybı simülasyonu, bariyerle iki işlemi yazma sınırına getiren yarış, fiziksel silme ve silme sonrası eski isteğin yeniden ödeme oluşturmaması. SQL retry açık test context'i kullanılır; gerçek ağ kesintisi/SQL failover veya production yapılandırma kabulü iddiası değildir.

`finally` yalnız çalışmanın sözleşme kimliği + rastgele işareti ve ürettiği request anahtarlarını temizler. Ortak kayıtlar silinmez. Identity sayaçları test/rollback nedeniyle ilerleyebilir, reseed yapılmaz. Süreç zorla öldürülürse finally çalışamayabilir; çıktıda yazan test kimliği/işareti ile kayıtlar incelenmeli, genel tablo temizliği yapılmamalıdır. Commit belirsizliği sırasında fixture kurulumu için otomatik kalıntı keşfi henüz yoktur.

Üretim kodu DI veya endpoint'e açılmamıştır. Kullanıcı yetkisi ve muhasebe eligibility matrisi ayrıca tamamlanmalıdır.
