# Tahsilat aktarım staging temeli

## Kurulan yapı

`20260915180434_AddCollectionMigrationStaging` migration'ı yalnız `collection` şemasına aşağıdaki tabloları ekler:

- `MigrationBatch`: kaynak sistem, sabit kesit anahtarı, manifest hash'i ve kural/normalizasyon sürümü.
- `MigrationSourceRow`: Contract ve ContractHistory kaynak satırlarının değiştirilmeden saklanan JSON karşılığı ve SHA-256 hash'i.
- `MigrationContractStage` ve `MigrationRatePeriodStage`: ayrıştırılmış, nullable aday alanlar ve doğrulama durumu.
- `MigrationReferenceMap`: Customer, ServiceType, CurrencyType ve collection tanımları için türlenmiş hedef FK'leri ve karar kanıtı.
- `MigrationIssue`: aynı satırdaki birden fazla engel/uyarıyı geçmişiyle saklar.
- `MigrationMap`: başarılı kaynak satırı ile Contract, RatePeriod veya Payment hedefi arasındaki kalıcı idempotency bağı.

Hedef FK'ler serbest tablo adı/Id çifti değildir ve `NoAction` kullanır. Accepted referans map'inde, migration map'inde ve 32-byte hash alanlarında veritabanı check constraint'leri vardır. Aynı snapshot, kaynak satır, açık issue ve hedef map için uygun unique/filtered indeksler eklenmiştir. Büyük JSON ve açıklama alanları liste indekslerine alınmamıştır.

## Sınırlar

Bu temel otomatik müşteri oluşturmaz, benzer isimle ServiceType birleştirmez, bozuk history satırını atmaz ve legacy veritabanına yazmaz. Staging tablolarının kurulması gerçek aktarımın başladığı anlamına gelmez. İlk yükleme sabit bir kaynak kesiti, manifest ve sürümlü normalizasyonla yapılacaktır; karantinadaki satırlar kaynak payload'ı korunarak `Blocked` kalacaktır.

## Uygulama ve doğrulama

Üretilen SQL: `docs/sql/20260915180434_AddCollectionMigrationStaging.sql`.

Migration yapılandırılmış AssistFlowTest veritabanına uygulandı. Salt-okuma katalog/model sorgusu 7 Migration tablosunun varlığını ve aktarım kayıtlarının sıfır olduğunu doğruladı. Backend Release build sıfır hatayla tamamlandı; mevcut solution uyarıları kapsam dışıdır. Geçici yerel `dotnet-ef` araç klasörü işlem sonunda kaldırıldı.
