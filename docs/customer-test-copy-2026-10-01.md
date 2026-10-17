# Canlı müşterilerin test ortamına tamamlanması — 1 Ekim 2026

Kullanıcı canlı AssistFlow'da bulunup AssistFlowTest'te bulunmayan Customers kayıtlarının abone numarasına göre tespit edilmesini ve teste taşınmasını istedi. İşlem tamamlandı; canlı veritabanında yalnız SELECT kullanıldı.

| Kontrol | Önce | Sonra |
| --- | ---: | ---: |
| Canlı Customers | 18.854 | 18.854 |
| Test Customers | 17.593 | 18.917 |
| Canlıda olup testte abone numarası karşılığı olmayan müşteri | 1.324 | 0 |

## Eşleme ve kapsam

- SubscriberCode baş/son boşlukları alınarak Turkish_CI_AS ile karşılaştırıldı. Baştaki sıfırlar, iç boşluklar ve noktalama korunur. Testte mevcut silinmiş kart da mevcut karşılık sayılır; canlandırma veya güncelleme yapılmaz. Kaynak silinmiş kayıtlar yeni kart olarak oluşturulmaz. Kaynakta boş abone numarası 0, eksik aboneler arasında tekrar 0.
- Müşteri kartının tüm skaler alanları taşındı; hedef Id identity tarafından üretildi. Canlı Id'leri hedef kimliği olarak kullanılmadı. Mevcut test müşteri kartları güncellenmedi/silinmedi.
- CustomerGroups.Code ile gruplar, Tenants.Code ile tenantlar, Users.Code ile audit kullanıcıları eşlendi. Kaynakta CreatedUser=0 olan iki sistem kaydının audit değeri 0 olarak korundu; başka kullanıcıya mal edilmedi. Kullanıcı hesapları/şifreleri veya tenantlar kopyalanmadı.
- Testte eksik olan **EMLK** ve **STBT** grup tanımları kaynak Code/GroupName ile eklendi; ikisinde de üst grup yoktu. Mevcut grup tanımları değiştirilmedi.
- Müşteri tiplerinin ortamlar arası karşılığı: BRYSL01→N, KRMSL01→G, GRP01→GM, BNK01→BNK01. Sadece yeni kartlarda daha önce kabul edilen grup kodu sınıflandırması uygulandı: bireysel kodlar N, onaylı grup kodları GM; BANKA korunur. Hariç/belirsiz kodlarda yeni tahsilat yetkisi/kapsam kararı verilmedi. Yeni kartların sonucu: **250 Bireysel, 66 Grup Üyesi, 1.008 BANKA**. Tenant dağılımı: MGS 316, QNB 108, YKB 900.
- Sözleşmeler, ödemeler, dosyalar, servis talepleri ve ürün ilişkileri bu işin kapsamı değildir; otomatik aktarılmadı. Tahsilat staging/dosya eşleme istisnaları ayrı görevlerde yeniden incelenmelidir.

## Uygulama ve doğrulama

`tools/Collections.Import/CustomerTestCopy.cs` mevcut aktarım aracına `copy-live-customers` komutunu ekler. Sunucu 192.168.1.8 ve hedef AssistFlowTest zorunludur. Önce salt-okunur plan üretir, yalnız doğru SHA256 ile uygular. Kaynak/hedef müşteri şeması birebir kontrol edilir; aktif hedef trigger'ı, mükerrer abone veya belirsiz ilişki halinde yazma durur. Hedef transaction/uygulama kilidi ve Customers tablosu kilidi tekrar/eşzamanlı eklemeyi engeller. Kaynak müşteri verileri plan sırasında okunur ve hedef geçici tabloya toplu yüklenir; yalnız test Customers ve gerekli eksik CustomerGroups satırlarına INSERT yapılır.

- Plan SHA256: `C0E68EE253A712CB844081E2D22BAE3AD0664378CE0767D42EACA6FB3D118812`.
- Uygulama: **1.324 müşteri, 2 grup**; tek transaction başarıyla commit edildi.
- Tüm aktarılan alanlar eşlenmiş kesitle SQL EXCEPT kullanılarak karşılaştırıldı: fark 0. Hedef sayım farkı 1.324; kalan eksik abone 0.
- Commit sonrasında bağımsız salt-okunur profil tekrar çalıştırıldı: canlı 18.854, test 18.917, eksik abone 0. Aynı abone numaraları sonraki planda tekrar aday olmaz.
- Aktarım aracı Debug/Release build başarılı, 0 hata. Uygulama/FE değişikliği veya migration gerekmedi. Branch tahsilat-module; commit/merge/push yapılmadı.

Kişisel müşteri bilgisi içeren plan, kaynak/hedef kimlik eşlemesi ve tam alan kesiti Git dışındaki `tools/Collections.Import/snapshots/customer-test-copy-20261001-200345-094/` altında saklanır: `plan.json`, `inserted.json`, `transferred-values.json`, `result.json`, `plan.sha256`. Salt-okunur ön/son profil JSON'ları da aynı snapshots dizinindedir. Bu dosyalar kaynak verilerin kanıtıdır; versiyon kontrolüne alınmamalıdır.

Bu komut **test tamamlama aracıdır**, canlı aktarım/yayın aracı değildir. Canlıya test müşteri Id'leri veya bu tamamlama INSERT'leri uygulanmaz. İlerideki tahsilat canlı aktarımı hedef abone numarası ve tanım kodlarını yeniden eşlemelidir.
