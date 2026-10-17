# K01 / K02 uygulama kaydı — 27 Eylül 2026

## Tamamlanan kod

- `CollectionStartRules.IsIncluded`: null/boş/UNKNOWN/EXISTS dahil; NONE ve tanınmayan kodlar hariç. Sözleşme oluşturma, aktifleştirme ve tekil tarife değişimi aynı kuralı kullanır. Donuk/ücretsiz dönem kuralları ve tarihsel veriler değiştirilmedi.
- Ödeme güncellemesi mevcut `IsFree` işaretini korur. Ücretsiz/sıfır/negatif tarihsel ödemelerde sadece açıklama değişebilir; dönem/tarih/tutar/para birimi FE'de kilitli ve sunucuda denetlenir. Açıklama düzeltmesi için geçmiş dönemde yeniden ücretli tarife bulunması aranmaz. Normal pozitif ödeme değişikliğinde mevcut tarife uygunluğu aranır; yeni negatif/sıfır ödeme yaratılmaz.
- Mevcut transaction, yetki/kapsam, rowversion, before/after audit ve kalıcı işlem anahtarı korunur. Komut hash formatı değiştirilmedi; eski tamamlanmış isteklerin tekrar yanıtı korunur. Fiziksel silme akışı değişmedi.
- Bakiye sorgusuna isteğe bağlı `FullPeriod` eklendi. Varsayılan false olduğundan mevcut API tüketicilerinin tarih kesitli davranışı aynı. True iken seçilen ayın tüm vadeleri ve muhasebe dönemine yazılmış bütün ödemeler hesaba katılır; ayrıca AsOfDate gönderilirse reddedilir. Yanıt kapsam bilgisini taşır. Bu tutar bugüne kadar gerçekleşen borç diye adlandırılmaz.
- Takip listesinden sözleşmeye seçili dönem URL ile taşınır; ödeme sekmesi açılır, bakiye formu aynı dönem/tam dönem kapsamıyla hazırlanır. Hesap kullanıcı istediğinde çağrılır. Devir seçimi varsayılan kapalıdır; para birimleri ayrı kalır. Diğer menüler, ortak UI/CDN/DI ve GET/POST yapısı değişmedi.

## Doğrulama

- `dotnet build AssistFlow-BE.sln --no-restore -c Release`: başarılı, 0 hata. Son derlemede 781 proje uyarısı var; çözüm uyarısız değildir.
- Mevcut `tools/Collections.Tests` çalıştırıldı: 91 kontrol başarılı. Önceki “yalnız EXISTS” beklentisi müşteri kararıyla güncellendi. Yeni test altyapısı eklenmedi.
- FE `npm run build`, değişen altı FE dosyasında ESLint ve Prettier: başarılı.
- `tsc --noEmit`: 144 kapsam dışı hata; Tahsilat/CollectionContractService dosyalarında 0 hata. Genel type-check başarılı sayılmadı.
- Mevcut SQL kontrol aracının `--read-balance` yolu genişletildi. AssistFlowTest üzerinde sadece SELECT: sözleşme 96 dönem/devir sorguları başarılı; Eylül 2026 takip listesinden 10 satırda para birimi bazında borç/ödeme/kalan ile tam dönem detayı eşleşti. Bu, bütün portföy/grup/yük testi değildir.
- `git diff --check`: her iki repo başarılı (yalnız mevcut CRLF uyarıları).
- `Collections.Model.Tests` şema delta kontrolünde durdu. Kontrol, on temel collection entity'sini kaldırarak ürettiği baseline ile güncel modeli karşılaştırıyor; bu tur entity/configuration/migration değişikliği yapılmadı. Mevcut model kontrolünün güncel şemaya uyumu ayrıca incelenmeli; diğer kontrolleri geçmiş sayılmadı.

## Kabul sınırı / sıradaki iş

- K01 uygulandı; ücretsiz/negatif/sıfır ödeme açıklamasının yazmalı UI kabulü bu tur yapılmadı. Gerçek finansal kayıtlar deneme amacıyla değiştirilmedi.
- K02 tamamlandı. K01'in gerçek finansal kayıt üzerinde yazmalı kullanıcı kabulü ve daha geniş eşzamanlı production yük ölçümü, K02'nin değil son geçiş kabulünün parçasıdır.
- K09'da daha kapsamlı finansal düzeltme ele alınana kadar tarihsel istisna kayıtlarının finansal alanları düzenleme formundan değiştirilemez; ayrı bir iade veya ters kayıt akışı eklenmedi.
- Şema/migration/seed/veri aktarımı/DB yazması/Job/CDN değişikliği yapılmadı. MGS ve production'a dokunulmadı; her iki repo tahsilat-module dalında. Commit veya merge yapılmadı.

## K02 devam dilimi — geçmiş dönem ve filtreler

Bu bölüm K02'nin uygulama ve kabul kaydıdır.

### Uygulama

- `PeriodFrom` isteğe bağlıdır; `Period` son aydır. Eski tek ay istekleri aynen desteklenir. İlk/son ay dahil, en fazla 600 ay; ters/geçersiz aralık servis sınırında reddedilir. Sabit 2016/2023 alt sınırı yoktur.
- SQL takvim kaynağı parametreli OPENJSON ile yalnız ayları üretir; finansal kayıtlar belleğe taşınmaz. Borç ve ödeme katkıları UNION ALL ile bir kez gruplanır; bir sözleşme/dönem/para birimi tek satırdır. Bir ayda birden fazla geçerli yenileme varsa ödeme toplamı çoğaltılmaz. Sayım, filtre, sıra ve sayfalama SQL'dedir. Kararlı sıralamaya dönem de katıldı.
- Mevcut Repository, sorgunun tamamı aynı injected AppDataContext üzerinde kalacak biçimde kullanılır. Paylaşılan repository factory ayrı context yarattığından iki farklı context'e ait IQueryable birleştirilmez. Mevcut Autofac servis kaydı kullanılır; Program.cs veya ortak DI değiştirilmez.
- UI'de başlangıç ayı boş bırakılırsa tek ay; geçmiş açıklar için aralık + Borcu kalanlar. Her dönem ayrı satır ve grup özeti ayrı dönemdir. Grup üyesine geçiş yalnız seçili ayı açar; grup özetine dönüş aralığı geri getirir. Detay bağlantısı/toplu ödeme kalemi satırın kendi dönemini taşır. CSV aynı aralık/filtre sorgusudur.
- Filtreler: güncel abonelik durumu, ödeme yöntemi dahil/hariç. Hariç yöntemde yöntemi boş olanlar korunur. Pasif tanımlar sadece açıkça istendiğinde filtre seçiminde görünür; kayıt formlarının varsayılan aktif tanım davranışı değişmez.
- Ortak arama: abone/ad/grup adı-kodu, GTS/IVR, Phone1/2, Email1/2, City, Customer.Note. Bu, henüz taşınmamış çok satırlı legacy not geçmişinde arama iddiası değildir. Grup dönem etiketi K05'e, kurumsal bağlam filtresi K03'e bağlı kalır.
- Tarih aralığı devir/mahsuplaştırma değildir. Para birimleri birleştirilmez. Güncel donuk durumu geçmiş ücretli dönemleri otomatik silmez; durum filtresi yalnız kullanıcı seçerse uygulanır.

### Doğrulananlar

- BE Release solution ve FE build geçti; değişen 3 FE dosyasında ESLint/Prettier geçti. Global type-check 144 mevcut kapsam dışı hatada duruyor; Tahsilat dosyalarında 0 hata.
- Mevcut SQL kontrol aracının sadece `--read-tracking` yolu kullanıldı; test fixture/seed/ödeme yazması yapılmadı.
- Eylül 2026: bireysel 1.083, grup 24 satır. Ocak–Mart 2026: 3.479 satır; aralık sayımı üç tek ayın toplamıyla eşit.
- İlk 10 aralık satırının 10'u detay borç/ödeme hesabıyla eşleşti; 162 ve 192 de buna dahildir.
- Üç grup/dönem üye toplamı, ters aralık reddi, örnek yöntem dahil/hariç ve abonelik filtresi; seçilen müşteri için CSV/liste sayımı (6 satır) kontrol edildi.
- 2006 Ocak–2026 Eylül, Borcu kalanlar: **50.463 kayıt**, sadece 25 satırlık sayfa alındı. Aynı veride ilk uygulama 27,51 sn; sorgu yapısının iyileştirilmesiyle 11,44 sn. `collection.ContractRatePeriod` ve `collection.Payment` için iki dar takip indeksi AssistFlowTest'e migration ile uygulandı/doğrulandı. Bu değer test ortamı gözlemidir; p95/eşzamanlı kullanıcı yük testi son production kabulünde yapılır.
- Tarayıcıda Ocak–Mart 2026 sorgusu gerçek API ile açıldı: 3.479 kayıt, dönem etiketleri, borç/ödeme/kalan alanları, aralık seçicileri, yeni filtreler ve server-side sayfalama görünür/doğru çalıştı. Tahsilat oluşturma veya veri değiştiren bir buton kullanılmadı.

### K02 kararları ve kalan sınırlar

- 162: 421 numaralı tarife 2017-10-01'de biterken 422, 2017-11-01'de başlıyor. 425/426 arasında 2021-10-31 → 2021-11-01 boşluğu da var.
- 192: 585 numaralı tarife 2018-01-01'de biterken 586, 2018-02-01'de başlıyor.
- Tek ay/tam dönem detay hesabı yalnız seçilmiş dönemi kesen tarife satırlarını kullanır. Böylece 162/192 gibi geçmişte boşluk bulunan sözleşmeler, seçili dönem geçerli ise takip listesiyle aynı hesaplanır. Seçili ay bizzat boşluktaysa tarife borcu uydurulmaz; o aya ait ödeme yine görünür. Devirli hesap (`IncludeCarryOver`) bütün zaman çizelgesini doğrulamaya devam eder; boşlukta güvenli olarak reddedilir.
- Geçmiş tarife verisi, sözleşme, ödeme ve ortak `dbo` tabloları değiştirilmedi. Uygulanan migration yalnız AssistFlowTest'te iki `collection` performans indeksidir. Production geçişinde aynı migration standart yayın hattından uygulanmalıdır.
- K03'ün müşteri/grup/kurumsal üst kart çalışması K02'den bağımsız sıradaki geliştirmedir.
