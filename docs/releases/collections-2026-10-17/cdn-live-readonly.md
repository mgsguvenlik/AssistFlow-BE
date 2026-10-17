# Tahsilat — canlı CDN salt-okunur kontrol sonucu

**Sonuç: mevcut ortak CDN'nin örnek okuma ve production dosya yönlendirmeleri
başarılı.** Canlı AssistFlow/MGS'de yazma, CDN upload/delete, bucket listeleme,
ayar/izin değişikliği veya deploy yapılmadı. MGS'ye bağlanılmadı.

## Doğrulananlar

- Production JSON/current-process etkin ayarlarında public adres
  `https://cdn.flowassist.mgs.com.tr`, önek `uploads`. Test/Development öneki
  `uploads-test`. Yerel Production ve Test tanımlarında endpoint/bucket aynı;
  ayrım prefix'tir, fiziksel ayrı bucket veya farklı IAM hakları varsayılmaz.
- Secret/AccessKey yalnız varlık olarak kontrol edildi, değeri raporlanmadı.
  Namespace SHA-256:
  `8A00B3ED52475F802003631DF62D090377C08C628DD2ACBD42623660CF1C2DD6`.
  Bu proses için R2 ortam override'ı görülmedi; gerçek hosting secret/config
  içeriğinin tamamı okunmadı veya karşılaştırılmadı.
- Canlı AssistFlow tablo/kolon envanteri SELECT ile alındı; collection dosya
  tabloları henüz yok. Tahsilat dosyaları için sahte URL/örnek üretilmedi.
- Mevcut ortak altyapıdaki üç kayıtlı dosyada yalnız R2 HEAD/GET ve public
  CDN GET yapıldı. Dosya içeriği diske yazılmadı veya kullanıcıya gösterilmedi.

| Örnek | Boyut (byte) | R2 / public HTTP | MIME + boyut + SHA-256 |
| --- | ---: | --- | --- |
| PDF | 124.031 | 200 /200 | Eşit |
| PNG | 120.830 | 200 /200 | Eşit |
| JPEG | 985.488 | 200 /200 | Eşit |

HTTPS normal sertifika doğrulamasıyla çalıştı; TLS kontrolü kapatılmadı.
SDK handler'ı yalnız seçilen tam nesne yollarına GET/HEAD kabul etti:
**CLI'den doğrudan 6 signed provider isteği +3 public GET**. Bucket genelinde tarama/listing
yapılmadı. Bu üç örnek tüm bucket envanteri veya yazma izni kanıtı değildir.

## Çalışan production API doğrulaması

FE Production ayarındaki `https://flowassistapi.mgs.com.tr/api` hostunun
`/uploads/{dosya}` yoluna aynı üç örnekle oturumsuz GET yapıldı. Her üçü de
**302** ile beklenen `cdn.flowassist.mgs.com.tr/uploads/...` adresine birebir
yönlendi. Redirect takip edilmedi, API body indirilmedi; ek **3 GET** yapıldı.
Bu, çalışan servisin örneklerdeki public host/prefix davranışını kanıtlar;
IIS/Jenkins bütün credential/override eşitliğini kanıtlamaz. API'nin dahili
R2 okumaları CLI signed-request sayacına dahil değildir.

## Erişim/güvenlik gözlemleri

- Public CDN dosyaları oturumsuz açılıyor. Bu mevcut kabul edilmiş public-link
  modelidir; API View yetkisi dosyanın doğrudan linkini özel yapmaz.
- Örneklerde `Cache-Control: max-age=14400` (4 saat), ilk okumada Cloudflare
  `MISS` görüldü. Metadata'yı listeden kaldırmak URL erişimini/cache'i iptal
  etmez. Bu tur CDN cache/policy değiştirilmedi veya purge yapılmadı.
- Örnek yanıtlarda X-Content-Type-Options ve Content-Disposition görülmedi.
  Doğru MIME/hash kabulü başarılıdır; gözlem ortak altyapıyı otomatik değiştirme
  gerekçesi değildir. Farklı güvenlik politikası ayrıca kararlaştırılır.
- LegacyLocalFallbackEnabled yerel Production ayarında true. Bu test public
  CDN'yi doğrudan okudu ve runtime API'de 302 gördü; local fallback başarısını
  CDN başarısı saymadı. Fallback ayarı değiştirilmedi.

## Kanıt ve doğrulama

Özel rapor `.tools/collection-cdn-live-readonly-2026-10-17-01/` altında:
settings.json, samples.private.json, result.json, runtime-redirects.json ve
hash'li evidence.json. Tablo envanteri ayrıca
`.tools/collection-cdn-live-inventory-2026-10-17-01.json` içindedir.

- R2/public sonuç SHA-256:
  `D60994C6A7B06C811F79E9444D689CF00518EA9300AB9F05225CE636919A685E`.
- Runtime redirect sonuç SHA-256:
  `467EE977B858C08B669CDEE33E124E3950B3BB36608AB1E31E8B7C3A500D80D6`.
- Birleşik salt-okuma kanıtı SHA-256:
  `883A62F2073436F6808F7ED530AC8512B9F94001E3109704C2EE3EE27E3DBF9E`.
- Araç [Collections.CdnReadOnly](../../../tools/Collections.CdnReadOnly/README.md)
  bağımsızdır; mevcut dosya adaptörünün/provanın kod ve teslim hash'leri
  değiştirilmedi. Runtime/FE/ortak CDN/Development config değişmedi.
- Araç derlendi (0 hata; mevcut Identity/NuGet erişim uyarıları var). SQL
  envanter parser/izinli statement kontrolü, iki PowerShell scriptinin syntax'ı
  ve bütün kanıt hash'leri doğrulandı; git diff --check başarılı.
  FE değişmediğinden FE build/lint yapılmadı.

## Açık kalan canlı kapılar

1. Jenkins/IIS etkin override/credential referanslarının yetkili operatörce
   teyidi; bu kontrol yeni erişim bilgisi veya yayın onayı oluşturmaz.
2. PUT/yükleme ve silme izinleri **denenmedi**. Salt-okuma başarılarını upload
   yetkisi kabulü saymayız; yalnız açık pilot onayından sonra upload sınanır.
3. Güncel dosya malware taraması, final kaynak/owner/onay, bakım/yedek ve
   finansal replay kapıları; sonra onaylı pilot/tam Tahsilat dosyası aktarımı.
4. Aktarılacak 1.821 ilişkinin gerçek CDN readback'i, kullanıcı/API menu
   yetkileri ve browser kabulü sonraki aşamadır. Eski izole prova bu genel
   örnek erişim kontrolüyle gerçek-CDN migration kabulüne çevrilmez.

Canlı uygulama, upload ve yetki değişikliği onayları kapalı kaldı. Önceki
[izole dosya provası](cdn-adapter-rehearsal.md) ve mühürlü paketler korunur.
