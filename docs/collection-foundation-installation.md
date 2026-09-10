# Tahsilat temel şema kurulumu

## Uygulanan kapsam

10 Eylül 2026: `192.168.1.8 / AssistFlowTest` üzerinde `20260910150310_AddCollectionFoundation` uygulandı. Kullanıcının tahsilat tabloları kurulum yetkisi kapsamında yalnız `collection` şeması nesneleri ve standart EF migration geçmişine yeni kayıt eklendi. Ortak dbo iş tabloları, eski migration kayıtları ve legacy MGS değiştirilmedi.

Tablolar: Contract, ContractStatus, SubscriptionStatus, PaymentMethod, PaymentFrequency, GroupStatus, ContractRatePeriod, ContractPeriodFollowUp, Payment, PaymentOperation.

Toplam 10 tablo, 21 ikincil indeks, 12 NoAction FK, 14 etkin ve güvenilir CHECK oluşturuldu. Commit sonrası bağımsız bağlantıda tüm tablolar boş bulundu; ortak FK hedefleri dbo.Customers/ServiceType/CurrencyType olarak doğrulandı. Bu çalışma veri aktarımı, seed, finansal iş akışı veya production kabulü değildir.

## Tekrarlanabilir kurulum

`tools/Install-CollectionFoundation.ps1` varsayılan olarak yalnız ön kontrol yapar. `-Apply` açıkça verilirse hash ile sabitlenmiş `docs/sql/20260910150310_AddCollectionFoundation.sql` dosyasını çalıştırır. Hedef yalnız Development yapılandırmasındaki onaylı AssistFlowTest olabilir; credential çıktıya yazılmaz. SQL dosyası tek başına transaction içermez, doğrudan çalıştırılmamalıdır.

Kurucu uygulama kilidi, hedef/baseline migration/ortak anahtar kontrolleri, XACT_ABORT, sınırlı lock timeout ve tek transaction kullanır. Nesne sayıları/ilişki güvenliği doğrulanmadan commit etmez. Mevcut collection nesneleri veya kurulum kaydı varsa yeniden uygulamayı reddeder; bunları silip yeniden oluşturmaz. Bağlantı commit sırasında koparsa kör tekrar yerine yeni bağlantıdan migration kaydı ve nesneler doğrulanmalıdır.

SQL EF 9.0.5 ile yalnız önceki yerel migration ile bu migration arasından üretildi. Global `database update`, host startup ve genel seed çalıştırılmadı. DB'deki repository dışı dört tarihsel migration korunmuştur; ayrıntı geliştirme planındadır.

## Doğrulama ve sınırlar

94 offline model/sorgu/API/migration kontrolü ve 76 saf iş kuralı kontrolü geçti. Migration Up komutları collection-only model farkıyla birebir, target model aktif AppDataContext ile uyumlu. Solution build: 0 hata, 47 uyarı (artımlı); eski paket/sürüm ve kod uyarıları sürüyor.

Gerçek SQL rollback/commit belirsizliği/eşzamanlı retry/rowversion senaryoları bu metadata kontrolüyle kanıtlanmış değildir; sonraki kabul işidir. CollectionRead varsayılan kapalıdır; finansal yazma servisi dış API'ye açılmadı. Muhasebenin belirsiz ilk borç ve VAR/YOK matrisi yorumlanarak aktive edilmedi.

## Geri alma

Kurulum transaction'ı hata halinde kendi eklemelerini geri alır. Commit sonrası EF Down, collection tablolarını ve içlerindeki veriyi fiziksel olarak siler; otomatik çalıştırılmaz. Daha sonra geri alma gerekiyorsa modül erişimi kapatılıp veri/dosya/audit yedeği ve iş birimi onayı alınmalıdır. Mevcut tablolar boş olsa bile yeniden doğrulama ve açık silme onayı olmadan Down çalıştırılmaz. Ortak tablo veya eski migration geçmişini “düzeltme” bu kapsamda değildir.
