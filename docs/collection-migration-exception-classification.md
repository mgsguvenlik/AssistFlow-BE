# Tahsilat Aktarım İstisna Sınıflandırması

## Kapsam

20 Eylül 2026 tarihinde `mgs-20260919-131119Z` kesiti, manifest
`F61D471E7E7F1CCF8BAD923AE4BC8AA15F1EA09DC71D4CF01C8A7C7472244263`
üzerinden işlendi. Müşteri kararına göre legacy sözleşme durumu `YOK` olan
kayıtlar tahsilat kapsamına alınmadı.

Önce salt-okuma planı üretildi. Plan SHA-256 değeri
`CC36C14B7A228D8C322B499FAD77F455A507CC6D5DB0529AE73CB3B24D42A6B6`
ile tekrar doğrulandıktan sonra yalnız AssistFlowTest staging kayıtları
güncellendi.

## Uygulanan Hariç Tutma

- 2.139 sözleşme `Excluded` yapıldı.
- Bu sözleşmelere bağlı 8.655 tarife dönemi `Excluded` yapıldı.
- 2.857 açık issue, müşteri kapsam kararı açıklaması korunarak `Ignored`
  durumunda kapatıldı.
- Hedef `collection.Contract`, tarife, ödeme veya dosya kaydı oluşturulmadı.
- Legacy MGS veritabanında değişiklik yapılmadı.

Son staging mutabakatı:

- Sözleşme: `Blocked=4.920`, `Applied=2.439`, `Excluded=2.139`
- Tarife dönemi: `Pending=13.810`, `Blocked=2.376`, `Applied=9.609`,
  `Excluded=8.655`

## Kalan Açık İstisnalar

Issue adetleri aynı kaynak kayıtta birden fazla nedenle çakışabilir; toplamları
tekil sözleşme sayısı değildir.

| Sınıf | Başlıca issue | Adet |
| --- | --- | ---: |
| Müşteri türü | `CUSTOMER_TYPE_MISMATCH` | 1.945 |
| Tarife dönem bütünlüğü | `CONTRACT_PARENT_MISSING` | 1.481 |
| Kapsam kararı | `CONTRACT_NOT_INCLUDED` | 1.030 |
| Müşteri eşleşmesi | `CUSTOMER_TARGET_MISSING` | 1.009 |
| Tarife dönem bütünlüğü | `PERIOD_OVERLAP` | 712 |
| Tarife dönem bütünlüğü | `HISTORY_MISSING` | 549 |
| Müşteri eşleşmesi | `CUSTOMER_ORPHAN` | 543 |
| Tarife dönem bütünlüğü | `HISTORY_INVALID` | 478 |
| Tarife dönem bütünlüğü | `CONTRACT_CURRENT_RATE_MISMATCH` | 392 |
| Müşteri eşleşmesi | `SUBSCRIBER_SOURCE_DUPLICATE` | 323 |
| Referans eşlemesi | `CURRENCY_MAP_MISSING` | 218 |
| Tarih/veri kalitesi | `RANGE_REVERSED` | 200 |
| Müşteri eşleşmesi | `SUBSCRIBER_BLANK` | 138 |
| Müşteri türü | `CUSTOMER_TYPE_MAP_MISSING` | 76 |
| Kapsam kararı | `SUBSCRIPTION_STATUS_UNKNOWN` | 59 |
| Tarife dönem bütünlüğü | `PERIOD_START_DUPLICATE` | 58 |
| Süreç türü | `PROCESS_TYPE_UNKNOWN` | 23 |
| Tarife dönem bütünlüğü | `MULTIPLE_OPEN_PERIODS` | 15 |
| Tarife dönem bütünlüğü | `HISTORY_CUSTOMER_MISMATCH` | 14 |
| Finansal veri kalitesi | `AMOUNT_INVALID` | 7 |
| Tarih/veri kalitesi | `DATE_INVALID` | 3 |

Kalan 1.030 `CONTRACT_NOT_INCLUDED` kaydının kaynak değer kırılımı:

- Kaynak sözleşme durumu `14`: 853 sözleşme. Bu değer mevcut eşlemede
  `UNKNOWN/Belirtilmemiş` anlamındadır; `YOK` kabul edilmedi.
- Kaynak sözleşme durumu boş: 177 sözleşme. Otomatik olarak `YOK` veya `VAR`
  kabul edilmedi.

Kalan 23 `PROCESS_TYPE_UNKNOWN` satırının tamamı gerçek JSON null değil,
legacy metin değeri olan literal `null` kaydıdır. Kanıt olmadan ücretli,
ücretsiz veya donuk davranışa dönüştürülmedi.

## Sonraki Çözüm Sırası

1. `CONTRACT_NOT_INCLUDED` içindeki 853 `UNKNOWN/Belirtilmemiş` ve 177 boş
   kaydı `YOK` ile karıştırmadan ayrı karar/inceleme listesinde tutmak.
2. Hedef müşterisi bulunmayan, abone numarası boş veya tekrarlı kayıtları ayrı
   mutabakat listesine çıkarmak.
3. Kaynak ve hedef müşteri türü uyuşmazlıklarını N/GM/A/G karar matrisiyle
   incelemek; ortak `dbo.Customer` kaydını otomatik değiştirmemek.
4. Parent eksikliği, overlap ve tarih tersliği bulunan tarife dönemlerini veri
   kalitesi sınıfında çözmek; tarih uydurmamak.
5. Para birimi, süreç türü ve tutar istisnalarını yalnız kanıtlı eşleme veya
   açık karar ile kapatmak.

## Müşteri Mutabakat Raporu

Müşteri eşleşmesi ve müşteri türü sorunları için 3.958 tekil sözleşmelik CSV
raporu Git dışındaki kesit alanında üretildi. Dosya 3.959 satırdır (başlık
dahil); SHA-256 değeri
`C3B0BA749F4E7E6F5154DA4D0F85FE140B7C02307DB0BE6F16159A7FA980AF9C`.

Issue adetleri:

- `CUSTOMER_TYPE_MISMATCH`: 1.945
- `CUSTOMER_TARGET_MISSING`: 1.009
- `CUSTOMER_ORPHAN`: 543
- `SUBSCRIBER_SOURCE_DUPLICATE`: 323
- `SUBSCRIBER_BLANK`: 138
- `CUSTOMER_TYPE_MAP_MISSING`: 76

Bir sözleşmede birden fazla issue bulunabileceği için issue toplamı tekil rapor
satırı sayısından yüksek olabilir.

Tür uyuşmazlığı matrisi:

- Beklenen `GM - Grup Üyesi`, mevcut `N - Bireysel`: 1.937
- Beklenen `GM - Grup Üyesi`, mevcut `BNK01 - BANKA`: 6
- Beklenen `G - Grup/Kurumsal`, mevcut `N - Bireysel`: 1
- Beklenen `N - Bireysel`, mevcut `BNK01 - BANKA`: 1

Ortak `dbo.Customer.CustomerTypeId` otomatik değiştirilmedi. Özellikle 1.937
GM→N farkı toplu ortak veri değişikliği gerektirebileceğinden müşteri kararı ve
etki kontrolü olmadan uygulanmayacaktır.

## Araç Güvenceleri

`Collections.Import classify` salt-okuma planı ve sınıflandırma üretir.
`exclude-yok` yalnız aynı plan SHA-256 değeriyle, Serializable transaction ve
SQL uygulama kilidi altında çalışır. Plan değişirse yazma başlamadan durur.
`customer-exceptions` açık müşteri issue'larını birleştirerek Git dışındaki
kesit alanına CSV mutabakat raporu yazar; ortak müşteri verisini değiştirmez.
