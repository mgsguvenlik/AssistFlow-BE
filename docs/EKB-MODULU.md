# Emlak Katılım Servis Talepleri (EKB)

## Kapsam

YKB akışı temel alınarak ayrı `Ekb` entity/DTO/servis/controller ve frontend ekranları eklendi. Tenant kodu `EKB`, şema `ekb`; Customer, Product, ServiceType, WorkOrderType, kullanıcı ve diğer ortak tablolar mevcut dbo yapısını kullanır.

Müşteri formu → servis talebi → depo → teknik servis → fiyatlama → son onay → müşteri onayı → tamamlanma/iptal; ayrıca arşiv, aktivite kayıtları, ek dosyalar, servis/form görselleri, çalışma modu, raporlar, Excel/PDF çıktıları, muhasebe, fazla mesai, dashboard, SLA ve teknik servis kılavuzu EKB karşılıklarına bağlandı. Fiyatlama görselleri YKB'deki `NormalizeImageUrl` mantığını korur.

## Veritabanı ve devreye alma

- Migration: `Data/Migrations/20260905164011_AddEkbTenantModule.cs`. Yalnızca ekb şemasına yeni tablolar/indeksler/ilişkiler ekler. Ortak tabloları yeniden oluşturmaz; YKB/QNB/Bireysel tablolarını değiştirmez.
- Migration ve seed **bu geliştirme sırasında veritabanına uygulanmadı**.
- Dikkat: mevcut uygulama başlangıcındaki `UseDataSeedingAsync` önce `MigrateAsync` çağırır. Yeni sürümü çalıştırmak migration'ı otomatik uygular. Önce hedef ortamı/yedeği kontrol edin. Tablolar elle hazırlanacaksa aynı tabloları ikinci kez oluşturmamak için migration geçmişiyle koordine edilmelidir.
- `EkbWorkFlowStepSeed`, YKB ile aynı adım kodlarını ekler: CF, SR, WH, TS, PRC, APR, CAPR, CNC, CMP.
- `EkbModuleSeed`, eksikse EKB tenantını ve menülerini ekler. Tenant ID sabitlenmez; veritabanınca üretilir. `WorkFlowCustomerType.EKB = 4` bir SLA enum değeridir, tenant ID değildir.
- Adında/kodunda YKB geçen mevcut rol tanımları EKB karşılıklarına kopyalanır; Ykb menüleri Ekb menülerine çevrilir ve ortak menü yetkileri korunur. Kullanıcı ataması yapılmaz. ADMIN rolüne EKB menüleri eklenir. Kaynak YKB rollerinin veritabanında bulunması gerekir; ortak roller için EKB yetkileri yönetim ekranından ayrıca atanabilir.
- EKB müşteri ve kullanıcılarını mevcut yönetim ekranlarından EKB tenantına bağlayın. Tenant fiyatları, SLA süreleri ve bildirim adreslerini EKB için tanımlayın; YKB verileri otomatik kopyalanmaz.

## En sona bırakılan SP'ler

SP gövdeleri kullanıcı tarafından hazırlanacak; geliştirmede oluşturulmadı. Uygulama şu iki SP'yi var kabul eder:

| EKB SP | YKB referansı | Kullanım |
| --- | --- | --- |
| `ekb.usp_ReportSearchEkb` | `ykb.usp_ReportSearchYkb` | Servis rapor listesi |
| `ekb.usp_ReportSearch_LinesEkb` | `ykb.usp_ReportSearch_LinesYkb` | Satır raporu ve Excel dışa aktarımı |

Her ikisi de YKB ile aynı parametre sözleşmesini kullanır:

```text
@Page, @PageSize, @SortBy,
@CreatedFrom, @CreatedTo, @ServicesDateFrom, @ServicesDateTo,
@Search, @RequestNo, @CustomerId, @CustomerName, @TechnicianId,
@ServiceTypeId, @StepCode, @IsAgreement, @IsLocationValid, @HasImages,
@WorkFlowStatusesCsv, @TechStatusesCsv, @PricingStatusesCsv, @FinalStatusesCsv,
@ProductId, @ProductCode
```

Parametre türleri/null davranışı YKB SP'leriyle aynı olmalı. Tenant tabloları ve alan adlarında Ykb → Ekb / ykb → ekb dönüşümü yapılmalı; dbo ilişkileri aynı kalmalı.

İlk SP tek sonuç kümesinde `EkbWorkFlowService.ReportRowDto` alanlarını döndürmelidir: TotalCount, RequestNo, Title, WorkFlowStatus, StepCode, CreatedDate, CustomerId, CustomerName, City, District, ServicesDate, ServiceTypeId, ServiceTypeName, TechnicianId, Name, Subtotal, Currency.

İkinci SP'nin kolon sözleşmesi `Model/Dtos/WorkFlowDtos/EkbDtos/EkbReport/EkbReportLineRowDto.cs` dosyasındadır. `TotalCount` her satırda toplam kayıt sayısını içermeli; kolon adları DTO özellikleriyle eşleşmelidir. SP'ler hazır olmadan bu rapor çağrıları ve ilgili dışa aktarımlar çalışmaz.

## Dış sistem ayarları

Teknik servis Manitou çalışma/test akışı EKB tenant koduna bağlandı; etkinlik mevcut tenant ayarı üzerinden yönetilir. Ortak `ManitouStagingSyncBackgroundService` içindeki dış sistem dealer-kodu → tenant-ID eşlemesi değiştirilmedi. EKB dealer kodları bilinmediği için tahmin edilmedi; otomatik müşteri aktarımı istenirse gerçek kodlarla ayrıca onaylanmalıdır.

## Kontroller

Veritabanına bağlanmayan kontrol:

```powershell
dotnet run --project tools/EkbModule.Tests/EkbModule.Tests.csproj
```

Bu kontrol Mapster kayıtlarını derler, YKB/EKB model eşleşmesini ve sorgunun ekb şemasını hedeflediğini doğrular. Migration veya seed çalıştırmaz.

Derleme: backend `dotnet build AssistFlow-BE.sln`; frontend `npm run build`.

Frontend `build` komutu Vite paketlemesidir, TypeScript kontrolünü içermez. Ayrı `npx tsc --noEmit` kontrolünde EKB dosyaları ve eklenen onay tipleri için hata kalmadı; projenin diğer dosyalarındaki mevcut tip hataları devam ediyor. Genel `npm run lint` kontrolü mevcut kod ve YKB'den taşınan stil/lint sorunları nedeniyle temiz değildir; bu çalışma kapsamında toplu lint düzeltmesi yapılmadı.

DB/SP hazırlığından sonra manuel doğrulama:

1. EKB yetkili kullanıcıyla giriş; menü, dashboard ve yalnızca EKB müşteri seçimi.
2. Yeni talep numarasının EKB ile başlaması; tüm adımları ileri/geri ilerletme, inceleme ve iptal.
3. Teknik servis fotoğrafı/form görseli ve ek dosya yükleme; fiyatlama/son onayda görüntüleme, önizleme ve izinli silme.
4. EKB onay sekmesi, arşiv, servis/temel/fazla mesai raporları ve Excel/PDF çıktıları.
5. Muhasebe işlem durumu ve muhasebe ekleri; tenant fiyatları ve SLA bildirimleri.
6. Yetkisiz kullanıcının EKB işlemlerine erişememesi; YKB/QNB/Bireysel akışlarının aynı kalması.

Bu uçtan uca kontroller gerçek EKB verisi ve SP'ler olmadan yapılmış kabul edilmemelidir.
