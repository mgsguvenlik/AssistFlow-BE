# Canlı CDN salt-okuma kontrolü

Bu bağımsız CLI yalnız üç kayıtlı ortak dosya örneğini inceler; uygulama
hostu/worker başlatmaz. Kaynak `appsettings.json` + Production JSON + bu
prosesin ortam değişkenleridir. Aktif IIS/Jenkins secret/override eşitliğini
ve upload iznini doğruladığı iddia edilmez.

```powershell
dotnet build tools/Collections.CdnReadOnly/Collections.CdnReadOnly.csproj -m:1
dotnet tools/Collections.CdnReadOnly/bin/Debug/net9.0/Collections.CdnReadOnly.dll inspect .tools/YENI-OKUMA-RAPORU
```

- SQL hedefi yalnız sql2022/AssistFlow, ApplicationIntent ReadOnly; sorgular
  SELECT ve session guard'dır. Önceden envanteri görülmüş sabit üç tablodan
  PDF/PNG/JPEG için en fazla birer küçük örnek seçilir. MGS bağlantısı yoktur.
- Signed S3 HTTP handler yalnız seçilen **tam nesne yollarına** GET/HEAD
  geçirir; PUT/POST/DELETE ve bucket listelemesi mümkün değildir. SDK retry
  kapalı; redirect izlenmez. Mevcut R2FileStorage yalnız URL üretmek için kullanılır.
- Public CDN'ye oturumsuz GET; HTTPS/sertifika doğrulaması normal handler'da
  açık, redirect kapalı. Her body en fazla 5 MiB, okuma en fazla 30 saniye.
  İçerikler yalnız bellekte hash'lenir; disk çıktısı olarak dosya kaydedilmez.
- Boyut/MIME/SHA-256 origin/public/DB arasında karşılaştırılır; 200 HTML veya
  farklı MIME/hash başarı sayılmaz. Metadata 403/404 ayrıca raporlanır.
- Endpoint/bucket hash'leri ve credential varlık bilgisi raporlanır; gerçek
  credential, signed URL, müşteri adı veya içerik genel loga basılmaz.
  StoredFileName örnekleri yalnız Git dışı samples.private.json içindedir.
- Mevcut ortak dosyaların erişimi Tahsilat dosyalarının aktarıldığını veya
  bütün bucket'ın sağlıklı olduğunu kanıtlamaz. Aynı bucket'ta test/live
  ayrımı prefix'e bağlıdır; farklı credential/IAM izolasyonu varsayılmaz.

`Read-CollectionCdnInventory.ps1` yalnız sys tablo/kolon SELECT envanteri alır.
`Read-CollectionCdnRuntimeRedirect.ps1` aynı özel örnek listesini kullanıp FE
Production API hostunun /uploads GET yanıtlarını inceler. Redirect izlenmez,
body indirilmez. Beklenen public adresin birebir eşliği kaydedilir; tam
hosting config'i veya JWT/menu yetkisi bu yolla kabul edilmiş olmaz.

Çıktılar yeni özel .tools dizininde tutulur; paketlere gerçek config veya
physical dosya eklenmez. Hiçbir kontrol upload/delete/config/SQL yazmasına
izin vermez. Ortak CDN/FE/API davranışı değiştirilmez.
