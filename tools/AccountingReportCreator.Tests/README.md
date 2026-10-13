# Muhasebe raporu: talebi başlatan filtresi

YKB ve EKB muhasebe raporlarında `CreatedUserId`, ilgili `WorkFlow.CreatedUser` alanına uygulanır. Filtre toplam kayıt hesabından ve sayfalamadan önce çalışır; mevcut metin, servis türü, tarih ve muhasebe durumu filtreleriyle birlikte kullanılabilir.

`accounting-service-report/creators` seçenekleri yalnız ilgili tenant modülünde muhasebe raporuna girebilen akışların başlatanlarından oluşturulur. Rapor görüntüleme yetkisi yeterlidir; Kullanıcılar menüsünün erişimi genişletilmez. Dönen bilgiler kimlik, ad ve kullanıcı koduyla sınırlıdır. Tarihî kayıtları filtreleyebilmek için pasif/silinmiş kullanıcıların adları korunur; bulunmayan kullanıcılar kimlik numarasıyla gösterilir.

## Kontroller

```powershell
dotnet build tools/AccountingReportCreator.Tests/AccountingReportCreator.Tests.csproj -m:1 -p:OutputPath=bin/accounting-creator-build/
dotnet tools/AccountingReportCreator.Tests/bin/accounting-creator-build/AccountingReportCreator.Tests.dll
```

Test kendine ait `MgsAccountingCreatorTests_<guid>` LocalDB veritabanını oluşturup temizler; uygulama ayarlarını veya canlı veritabanını kullanmaz. Gerçek servis sorgularıyla 16 kontrol yapılır: iki tenantın varsayılan rapor kapsamı, `WorkFlow.CreatedUser` eşleşmesi, servis talebi/muhasebe kullanıcısının yanlışlıkla eşleşmemesi, kayıt sayısı ve sayfalama, birleşik filtreler, temizlenmiş kullanıcı seçimi, tarihî kullanıcılar, kimlik karşılığı bulunmayan kayıtlar ve endpoint yetkileri.

Frontend servis kontrolleri frontend kök dizininde çalıştırılır:

```powershell
node --test tools/accountingReportCreator.test.mjs
```

İki tenantta parametre aktarımı, mevcut filtrelerle birlikte kullanılması, temizlenen/negatif seçimin gönderilmemesi ve tenant'a ait seçenek endpoint'i dahil 5 kontrol vardır.

Yerel mock tarayıcı kontrolünde iki ekranda ad/kod araması, `Ayşe` için `ayse` araması, kullanıcı seçimiyle ilk sayfaya dönme, seçimi kaldırma ve Filtreleri Temizle doğrulandı. Geçici tarayıcı dosyaları ve sunucu kontrol sonrasında kaldırıldı/kapatıldı.

Migration veya seed gerekmez. Frontend ve backend birlikte yayınlanmalıdır. Bu çalışma sırasında canlıya yayın yapılmadı.
