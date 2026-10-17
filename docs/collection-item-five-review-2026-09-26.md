# Madde 5 — Tahsilat ekibinin Excel yanıtı ön kontrolü

**Sonraki kapsam güncellemesi:** Kullanıcı 5/6/7 yanıtlarının birlikte değerlendirilmesini, yapılmış madde 4'ün korunmasını istedi. Bu nedenle burada sorulan üç eski aboneliği yeniden açma konusu artık bekleyen karar değildir: dışlamaları korunur. 77219 için diğer sayfada dışlama yanıtı bulundu; yanıtsız durumda kalan tarihçe sayısı 2'dir. [Güncel ortak karar planı](collection-items-5-6-7-joint-decision-2026-09-26.md). Aşağıdaki yalnız madde 5'e dayalı sayım ve sorular ilk incelemenin tarihsel kaydıdır.

## Kapsam

Kaynak: `5 - Null İşlem Türü.xlsx`, `5 - Null İşlem Türü` sayfası, başlık A5:R5, veri A6:R24, müşteri yanıtı H6:H24 (`TAHSİLAT AÇIKLAMA`). Dosya SHA-256: `6EBCDA7B21D6ABDC3AD20807B5345C435F9701658C91292556672D2DC4F3B18C`.

Kullanıcı önce kontrol, uygunluk sonrasında aktarım istedi. Bu çalışma **salt-okunur incelemedir**: MGS ve AssistFlowTest üzerinde yalnız SELECT çalıştırıldı. Excel, kaynak payload, staging, hedef kayıtlar veya uygulama davranışı değiştirilmedi. Aktarım/silme yapılmadı. Dosyadaki açıklamalar müşteri karar girdisi olarak okundu; MGS'de doğrudan silme talimatı kabul edilmedi.

## Excel ve kaynak doğrulaması

- 19 tekil tarihçe, 16 farklı sözleşme referansı var; mükerrer tarihçe kimliği yok.
- 14 satır `KAYIT OLDUĞU GİBİ KALSIN`, 1 satır `DONUK KAYIT SİLİNEBİLİR.`, 4 satır yanıtsız.
- 19 tarihçenin tamamı staging ve canlı MGS'de bulundu. Excel'deki kimlik, sözleşme/müşteri referansı, başlangıç ay/yıl, bitiş günü, tutar, para birimi, ödeme dönemi, işlem türü ve açıklama staging ham verisiyle eşleşiyor. Ham payload SHA-256 değerleri doğrulandı.
- Canlı MGS karşılaştırmasında **84571** tarihçesinin `ProcessType` değeri artık `Başlangıç`; Excel/kesit değeri `null`. Diğer kontrol edilen alanlarda fark yok. Staging ham kaydı sessizce yenilenmedi. Bu tarihçenin sözleşmesi 49228 zaten madde 4 nedeniyle dışlanmış durumda.
- 11 tarihçenin bağlı olduğu 8 sözleşme hem kesitte hem canlı `Core.Contract` içinde yok. Bunlar müşteri yokluğu ile aynı sorun değildir; tarihçeyi başka bir sözleşmeye otomatik bağlama yetkisi yok.
- Dosya, önceki listeden 4 donuk tarihçe çıkarıldıktan sonraki 19 satırlık listedir. Sonradan gelen “donuklar donuk olarak gelsin” kararı nedeniyle bu 4 satırın süreç tipi yanıtı bu dosyayla verilmiş sayılamaz.

## “Olduğu gibi kalsın” davranışının teknik karşılığı

Excel/kesitteki 19 `ProcessType` değeri SQL NULL değil, **literal `null` metnidir**. Canlı kaynakta bunların 18'i aynı, biri `Başlangıç` oldu.

Salt-okunur nesne tanımı incelemesi: `dbo.fnTahsilatTakibi`, `dbo.fnTahsilatTakibiGrup`, `Core.vContractHistory`, `Core.vCustomerContractHistory`. View işlem türünü dönüştürmeden geçiriyor; tahsilat fonksiyonları `ProcessType <> 'Ücretsiz'` ve `ProcessType <> 'Hizmet Dondurma'` koşullarını kullanıyor. Literal `null` bu filtrelerden geçer, gerçek SQL NULL ise geçmez. Bu tespit tek başına müşteri/dönem/diğer filtrelerden de geçildiği veya kesin borç oluştuğu iddiası değildir.

**Önerilen, henüz uygulanmamış eşleme:** Yalnız bu dosyada “kalsın” denilen ve diğer kontrollerden geçen satırlarda mevcut tutar/tarih/para birimi/dönem aynen korunarak `Billable` kullanılması legacy süreç tipi davranışını korur. Kaynak `ProcessType` ham metni ve müşteri yanıtı audit'te korunmalı; bütün NULL/bilinmeyen işlem türlerine genel Billable varsayımı eklenmemeli. Tutarı 0 olan 84457 aynen 0 kalmalı; müşteri söylemeden `Free` veya farklı bir tutara dönüştürülmemeli.

## Satır bazında sınıflandırma

| Excel satırı | Tarihçe | Sözleşme | Müşteri yanıtı | Ön kontrol sonucu |
| --- | --- | --- | --- | --- |
| 6 | 33948 | 27545 | Donuk kayıt silinebilir | Aktarım dışı bırakma adayı. Üst sözleşme kaynakta yok, hedef aktarımı yok; kaynak veya müşteri silinmez. Bu tekil karar tüm donuk aboneliklere genellenmez. |
| 7 | 34133 | 27590 | Olduğu gibi kalsın | İşlem türü dışında açık tarihçe engeli görünmüyor; 10 tarihçeli sözleşme aktarım adayı. |
| 8 | 35411 | 28725 | Olduğu gibi kalsın | Üst sözleşme yok ve para birimi eksik. Yeni sözleşme veya para birimi tahmin edilmez. |
| 9 | 35580 | 28768 | Yanıt boş | Önceden kimliksiz/kayıp müşteri kararıyla dışlanmış. Yeni onay sayılmaz. |
| 10 | 37923 | 30149 | Olduğu gibi kalsın | Bu satırın türü çözülebilir; aynı sözleşmede 80662/84333 tarihçeleri çakışıyor. Madde 6 olmadan tüm sözleşme aktarılamaz. |
| 11 | 66377 | 46387 | Olduğu gibi kalsın | Madde 4 isim tekilleştirmesinde eski abonelik olarak dışlanmış. Yeni notun önceki karara istisna olup olmadığı teyit edilmeli. |
| 12 | 85072 | 46506 | Olduğu gibi kalsın | Madde 4 abone numarası tekilleştirmesinde dışlanmış; para birimi de eksik. |
| 13 | 77221 | 48439 | Yanıt boş | Üst sözleşme yok; yanıt yok. Bekletilir. |
| 14–16 | 77216, 77217, 77218 | 48448 | Olduğu gibi kalsın | Üst sözleşme yok, açık dönemler çakışıyor; 77218 para birimi eksik. |
| 17 | 77215 | 48449 | Olduğu gibi kalsın | Üst sözleşme yok. Bekletilir. |
| 18–19 | 77213, 77214 | 48450 | Olduğu gibi kalsın | Üst sözleşme yok; açık dönemler çakışıyor. |
| 20 | 77219 | 48451 | Yanıt boş | Üst sözleşme yok, çakışma da var; yanıt yok. |
| 21 | 77224 | 48452 | Yanıt boş | Üst sözleşme yok; yanıt yok. |
| 22 | 82291 | 48715 | Olduğu gibi kalsın | İşlem türü dışında açık tarihçe engeli görünmüyor; 2 tarihçeli sözleşme aktarım adayı. |
| 23 | 84457 | 49221 | Olduğu gibi kalsın | İşlem türü dışında açık tarihçe engeli görünmüyor; 1 tarihçeli sözleşme aktarım adayı. 0 tutar korunmalı. |
| 24 | 84571 | 49228 | Olduğu gibi kalsın | Madde 4 isim tekilleştirmesinde dışlanmış. Canlı kaynak türü ayrıca Başlangıç olmuş; kesit değiştirilmedi. |

14 “kalsın” satırının birbirini dışlayan dağılımı: **3 aktarım adayı + 1 başka tarihçede çakışma + 7 üst sözleşme yok + 3 madde 4 ile çelişki = 14**. Adaylar 3 sözleşme / toplam 13 tarihçeye karşılık gelir; bu sayı henüz transaction içi tam aktarım planı sonucu veya aktarılmış kayıt sayısı değildir.

## Karar gereken nokta ve önerilen sıra

1. Madde 4 nedeniyle dışlanan 46387/46506/49228 sözleşmelerine ait üç “kalsın” yanıtının eski aboneliği yeniden dahil etme istisnası mı, yalnız işlem türü yanıtı mı olduğu netleşmeli. **Öneri:** Madde 4 dışlamasını korumak; örtülü yeniden dahil etme yapmamak.
2. Diğer engeli bulunmayan 27590/48715/49221 için yalnız ilgili üç satırın literal-null davranışını onaylı eşlemeye bağlamak, sonra kaynak hash/kimlik/son abonelik/kapsam/tarife kümesi kontrollü önizleme hazırlamak. Yeni durumda yeni hash doğrulanmadan aktarım yapılmaz.
3. 33948 yalnız hedef aktarım kapsamından dışlanır; MGS ve ham kaynak silinmez. Kullanıcı bu tur yalnız kontrol istediği için henüz staging kararı da uygulanmadı.
4. Üst sözleşmesi olmayan kayıtlar, yanıtsızlar ve madde 6/7 bağımlıları açık kalır; tahminle sözleşme, para birimi, dönem bitişi üretilmez.
5. Dosyalar madde 8; ödemeler bu çalışmanın konusu değil. Madde 4 eski ödeme kararı değişmez.

## Salt-okunur kanıtlar

Git dışındaki `.tools/item-five-workbook.json`, `item-five-review.json`, `item-five-legacy-review.json`, `item-five-legacy-logic.json`, `item-five-comparison.json` dosyalarında kaynak hash, Excel satırları, MGS karşılaştırması ve issue dağılımı bulunur. Excel yeniden kaydedilmedi; kişisel veri içeren ara çıktılar Git'e dahil edilmez.
