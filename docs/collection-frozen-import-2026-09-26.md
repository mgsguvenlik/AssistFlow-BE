# Madde 1 — Donuk kayıtların Donuk olarak aktarılması

## Müşteri kararı

26 Eylül 2026, madde 2 aktarımından sonra: **“Donuk kayıtlar donuk olarak gelsin.”** Bu karar önceki donukları aktarmama kararını geçersiz kılar. Abonelik FROZEN olarak taşınır; ACTIVE'a çevrilmez. YOK sözleşmeler, kapsam dışı müşteri kodları ve diğer maddelerin çözümlenmemiş eşleştirme/tarife istisnaları kendiliğinden dahil edilmez.

Güncel abonelik durumu geçmiş tarifenin davranışını topluca değiştirmez. Tutarlar ve geçmiş ücretli/ücretsiz dönemler korunur; bilinmeyen donma tarihi bugünden veya son değiştirilme tarihinden türetilmez.

## Test uygulaması

| Kontrol | Sonuç |
| --- | ---: |
| Önceden Pending donuk aday | 653 |
| Donuk olarak aktarılan sözleşme | 648 |
| Aktarılan tarife dönemi | 2.524 |
| Donma/bitiş tarihi belirsizliği nedeniyle bekleyen | 5 |
| Yeni kayıtların hedef abonelik durumu | 648 FROZEN |
| Yeni tarihçe alan uyuşmazlığı | 0 |
| Bugünden sonra ücretli dönem üretme adayı (yeni küme) | 0 |
| Birikimli aktarılmış sözleşme / tarife | 4.557 / 17.791 |
| İki eski manuel kayıt dahil hedef sözleşme / tarife | 4.559 / 17.793 |
| Hedef ödeme | 0 |

Aktarılan 648 kaydın mevcut bitiş tarihleri 07.12.2016–31.08.2026 aralığındadır. Sözleşme ve tahsilat hesapları bu sınırı zaten kullanır; yeni borcu engellemek için tarihçe değiştirilmedi veya yeni bir durum tarihi uydurulmadı. Detay ekranının mevcut abonelik durumu gösterimi FROZEN bilgisini kullanır; ayrı frontend bileşeni gerekmedi.

Plan SHA-256: `29A59B2A2CEE10DFBB1E93831D0635B52D5E428231E72CE54C1CB3A907FCBDEB`.

Yeni tarife teknik mutabakatı (güncel borç toplamı değildir):

| Para birimi / davranış | Satır | Dönem tutarları toplamı |
| --- | ---: | ---: |
| TRY / Billable | 2.251 | 793.403,52 |
| TRY / Free | 1 | 121,33 |
| USD / Billable | 271 | 17.215,50 |
| USD / Free | 1 | 70,80 |

Aktarım yalnız AssistFlowTest `collection` kayıtlarına, Serializable transaction ve mevcut uygulama kilidiyle yapıldı. Plan uygulama içinde yeniden doğrulandı. MGS, production, ortak dbo, ödeme ve fiziksel dosyalar değiştirilmedi. Importer derlemesi 0 hata / 0 uyarı. Son SQL kontrolünde tutar, para birimi, sıklık, davranış, başlangıç/bitiş, bağlı sözleşme ve kaynak hash eşleşti.

Uygulama sonrası tekrar önizleme **5 aday / 0 aktarılabilir / 0 tarife** döndürdü; yalnız tarih incelemesi gereken beş kayıt kaldı, aktarılanlar tekrar seçilmedi. Tekrar plan hash'i `AA94C097591BE1BA915BA468453BEF3977A8798CEAF1B96801D81A082AB53431`.

Güncel staging: Pending 5 / Blocked 2.250 / Applied 4.557 / Excluded 2.686. Donuk olup başka engel taşıyan 1.215 Blocked kayıt ve önceki kapsam kararlarıyla dışarıda olan 1.275 donuk kayıt bu işlemle açılmadı. Donuk olma tek başına engel değildir; diğer nedenler ayrı değerlendirilir.

## Tarihi gerekli olan kayıtlar

**Madde 4 son uygulaması:** Eski abonelik olarak 32882, 32883, 46344, 46345 yedekli şekilde test hedefinden kaldırıldı. Önceden aktarılmış açık tarih riski 12'den **8** kayda indi: **487, 740, 1898, 2194, 2573, 45063, 47667, 47668**. Ayrıca 55 yeni madde 4 sözleşmesinin 9'u FROZEN olarak güvenli bitiş tarihiyle aktarıldı; bu 8 riskli kayda yenisi eklenmedi. Aktarım bekleyen tarih istisnaları 11998/26458/49232 olarak sabit. Aşağıdaki 12'li liste ilk incelemenin tarihsel sonucudur.

**Daha sonraki madde 4 güncellemesi:** 44287 ve 44289, aynı isimde en yeni aboneliği koruma kararıyla aktarım dışı bırakıldı. Aktarım bekleyen tarih istisnaları artık **11998, 26458, 49232** (3 kayıt). Aşağıdaki 5 kayıttan oluşan liste ilk donuk incelemesinin tarihsel sonucudur. [Güncel tekilleştirme raporu](collection-customer-deduplication-2026-09-26.md).

Yeni aktarılmayan legacy sözleşme kimlikleri: **11998, 26458, 44287, 44289, 49232**. Bitiş tarihi yok; son dönem Billable/açık ve kesitte Hizmet Dondurma başlangıcı bulunmuyor. Bunları olduğu gibi almak donukken borç üretir; son tarifeyi bütünüyle Suspended yapmak ise bilinmeyen geçmiş dönemleri değiştirir. Gerçek donma tarihi veya bu belirsizlik için müşteri kararı gerekir. Ham staging kayıtları silinmedi, müşteri kimlikleri/tutarları değiştirilmedi.

Ayrıca daha önceki aktarımlarda aynı açık ücretli dönem sorunu taşıyan **12** donuk sözleşme tespit edildi: **487, 740, 1898, 2194, 2573, 32882, 32883, 45063, 46344, 46345, 47667, 47668**. Bu tur oluşturulmadılar ve tarihçeleri düzeltilmedi/silinmedi. Tarih kararı alınmadan geçmişi değiştirecek bir düzeltme yapılmayacak; bu konu production kabulünden önce kapatılmalıdır. Bitişi boş olan diğer eski kayıt 1700'ün tarifesi kapalıdır ve bu 12'ye dahil değildir.

## Tekrar çalışma

```powershell
dotnet run --project tools/Collections.Import -- transfer-frozen-batch 1 WebAPI/appsettings.Development.json
```

Hash verilmeden komut yalnız önizlemedir. Güncel önizleme hash'i dördüncü argüman olarak verilirse uygulama yapılır; hiçbir uygun kayıt yoksa yazmadan döner. Genel `plan/apply` de yeni donuk kabulü ve borç üretmeme kontrolünü kullanır. `transfer-item-two-batch` önceki madde 2'nin aktif kapsamıyla sınırlı kalır. Plan sürümü yenilendiğinden eski kararın hash'leri kullanılamaz.

Yerel Git dışı kanıtlar: `tools/Collections.Import/snapshots/item-one-frozen-20260926-134653637/plan.json`, `item-one-frozen-20260926-134758729/plan.json` ve `.tools/item-one-postflight.json`.
