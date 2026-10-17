# Canlı operatör, rol ve ortak değişiklik onayları

Durum: **onay hazırlığı / NOT_APPROVED**. Bu adımda SQL bağlantısı açılmadı;
AssistFlow/MGS, kullanıcı/rol, prosedür, Job, config veya runtime değiştirilmedi.
Hazırlık 17 Ekim mühürlü kesitine dayanır; bakımda sayım/hash tekrar doğrulanır.

Bağlı teslim: `collection-cutover-handoff-2026-10-17-02`, manifest
`6BC34709D364D02DCD72F05F682A7C1D805690A88D8971375A98B313F2C55ABC`.
[Gerçek host/yeni sürüm kabulü](hosting-and-refresh.md).

## Doldurulacak onay listesi

| Onay | Verilecek bilgi | Kabul koşulu |
| --- | --- | --- |
| OP01 — insan operatör | Mevcut aktif **User.Code**, onaylayan kişi, dış kayıt/talep numarası, UTC tarih | Tam bir aktif eşleşme; Id=1 varsayılmaz, yeni kullanıcı oluşturulmaz |
| AU01 — ilk açılış | Görüntüleyecek **Role.Code** listesi ve etkilenen kullanıcı üyelikleri | CollectionFollowUp için HasView=1 / HasEdit=0; diğer rollerden gelen yazma hakkı da kontrol edilir |
| AU02 — sonraki yazma fazı | Yazma verilecek Role.Code listesi, zaman/ayrı onay kaydı | HasView=1 / HasEdit=1; finansal düzeltme dahil mevcut Edit kapsamının tamamı kabul edilir |
| SC01–SC06 | Aşağıdaki her değişiklik için ayrı evet/hayır ve aynı dış onay kaydı | Kapsam dışı satırlar otomatik onaylanmaz; paket/önceki değerler değişirse yeniden inceleme |

[Boş karar şablonu](governance-approval.template.json) gerçek kodlar verilene
kadar null/boş/false kalır. İmzalı cevabın **yeni özel kopyası** oluşturulur;
bu şablon veya paket oluşturmak canlı onayı değildir. Şablon adaptörün
`approval.template.json` dosyası değildir; ona doğrudan verilmez.

### Operatör ve yetki ayrımı

- User.Code işleme onay veren uygulama kullanıcısıdır; SQL bağlantı hesabı veya
  sysadmin rolü değildir. SQL kullanıcı/şifre/yetkisi bu onayla değiştirilmez.
- Teknik audit aktörü **0** insan operatör değildir. Legacy kişi/tarih bilgisi
  ham audit'te, gerçek onaylayan Code/reference ayrıca ExecutionApproval'da
  korunur. Eski kayıtları rastgele gerçek kullanıcıya bağlama yoktur.
- Yeni rol veya UserRole üyeliği oluşturma/değiştirme bu kapsamda yoktur.
  Seçilen mevcut rolün tüm üyeleri etkilenir; kapsam **tenant bağımsızdır**.
- Tahsilatın mevcut ana menü anahtarı `CollectionFollowUp`'tır. HasEdit bugün
  ödeme, tarife/zam, sözleşme düzeltmesi, ilgili dosya ve SMS gibi mevcut yazma
  eylemlerini aynı menü üzerinden kapsar. Ayrı “sadece ödeme / sadece SMS”
  yetkileri varmış gibi onay istenmez; yeni yetki modeli eklenmez.
- Etkin hak kullanıcının tüm UserRole/MenuRole üyeliklerinin birleşimidir.
  Bir role HasEdit=0 vermek başka rolün HasEdit=1 hakkını iptal etmez. Mevcut
  MenuAuthorizationFilter rolün IsDeleted alanını ayrıca join/filter etmez;
  rolü pasif yapmak tek başına yetki iptali garantisi sayılmaz. Bu tur mevcut
  authorization davranışı değiştirilmedi; etkili hak kontrolü kabul koşuludur.
- AU01 ve AU02 listelerinde aynı Role.Code seçilirse o rolün tüm üyeleri ikinci
  fazda yazma kazanır; kalıcı salt-okuyucularla aynı rolün paylaşılmaması ayrıca
  teyit edilir. Önceki ve istenen haklar her rol/faz için ayrı kaydedilir.
- Ortak lookup okuma izni, Customer/ServiceType genel CRUD yazma izni demek
  değildir. Tahsilat açılacak diye CustomerList/ServiceTypeList gibi diğer
  menülere geniş hak verilmez; ilgili endpoint kendi mevcut yetkisini korur.

## Ortak değişiklik kapsamı

| Kod | Nesne / önerilen kapsam | Değişmeyecekler | Tek uygulayıcı |
| --- | --- | --- | --- |
| SC01 | dbo.ServiceType: **18** eksik hizmet adı, mevcut aktif eşleşmeler kullanılır | Var olan servislerin adı/ContractNumber/IsDeleted değeri değiştirilmez, pasif eşleşme otomatik açılmaz | Finansal aktarıcı, ana transaction içinde |
| SC02 | dbo.Customers: **2.602** sabit planlı mevcut kayıtta yalnız CustomerTypeId → mevcut GRP01 | CustomerGroupId, TenantId, abone no, ad/iletişim değişmez; BANKA korunur | Finansal aktarıcı, ana transaction içinde |
| SC03 | dbo.CustomerType: yoksa **G / Grup Üst Kartı** tanımı | Bireysel/Kurumsal/BANKA/GRP01 yeniden adlandırılmaz veya silinmez | Finansal aktarıcı, ana transaction içinde |
| SC04 | **56** doğrulanmış üst kart için dbo.Customers + collection.GroupParent, yeni hedef kimlikleri; TenantId=null | Mevcut müşteriler silinmez/birleştirilmez; **18** incelemedeki üst kart alınmaz; belirsiz bireysel müşteri yaratılmaz | Finansal aktarıcı, ana transaction içinde |
| SC05 | stg.sp_SyncCustomers: incelenmiş müşteri tipi CASE'i ve hedef BANKA koruması | Diğer kolon eşlemeleri/iş akışları aynı; prosedür çağrılmaz, Job/Manitou açılmaz | Ayrı hash-korumalı prosedür taslağı |
| SC06 | dbo.Menus / dbo.Configurations: eksik CollectionFollowUp ve CollectionRateChangeSmsTemplate | Mevcut şablon/metin ezilmez; SMS gönderilmez; seed ile rol verilmez | Ayrı onaylı menü/şablon seed'i |

SC03/SC04 yeni ortak müşteri kayıtları oluşturur; yalnız collection tablosu
değişikliği değildir. Ortak listelerde görünme etkisi de onay kapsamındadır.
Müşteri kararlarıyla daha önce testte uygulanmış olmak canlı yetkisi değildir.

### Satır listeleri ve dar prosedür farkı

Kişisel veri içeren ayrıntılar Git dışı orijinal inceleme bundle'ındadır:

- `review/classification-proposals.json`: 2.602 hedef müşteri, önceki tip/grup/
  tenant/abone ve önerilen tip. Sadece bu kapsam; genel CustomerGroups UPDATE'i yok.
- `data/group-parent-plan.json`: yalnız `PROPOSE_PARENT_CREATE_REQUIRES_APPROVAL`
  durumundaki 56 satır; PARENT_REVIEW durumundaki 18 satır hariç.
- `data/10-service-inserts-review.sql`: 18 isimli inceleme listesi; aktif aynı
  isim tekrarlarında yeni kayıt yaratılmaz. Yeni ID'ler eski ServiceTypeID değildir.
- `data/sync-procedure-before.txt`, `sync-procedure-after.txt`: fark yalnız
  CREATE→ALTER başlığı, kaynak CustomerTypeId CASE'i ve BANKA hedef korumasıdır.

Prosedür, onaylı bireysel kodları (MGS/MGSK/MGI/MGIK/STB/STBG) ve doğrulanmış
GM kod listesini kullanır; kaynak tipi BANKA ise korur, mevcut hedef BANKA'yı
ezmez. FIN*/YKB*/EMK için yeni tahsilat kapsamı açılmaz; belirsiz kodlara yeni
genel kural eklenmez. Eski prosedürün diğer alanları eşlemesi aynen sürer;
“tüm senkronizasyon artık tahsilat dışı yan etkisiz” sonucu çıkarılmaz.

**Özel önkoşul:** incelenmiş prosedür CASE'inde 4/6/7 ID'leri var. Bunların
canlıda sırasıyla BRYSL01/BNK01/GRP01 olduğuna son kesitte tekrar bakılır.
Farklıysa taslak çalıştırılmaz; yeniden üretim/inceleme/prova gerekir.

SQL module hash'i (SQL Server nvarchar/varbinary üzerinden):

- Önce: `7E6F301164802E3DCA96C6A8291D31CF61B160A4DB41047AD6505A545D81315C`.
- İncelenen sonra: `C7A77A88BEE20F2B90858100B32F2BA1E4ADD16208D530B44287D40CB30F1A2C`.
- Prosedür script dosyası SHA-256: `3BEE2C9806A5E45E892C61DE02781E6C5EEF0BCD3F885A2DD31BFC52B5F25AB0`.

UTF-8 dosya SHA'sı ile SQL module hash'i aynı algoritma girdisi değildir;
birbirinin yerine kullanılmaz. Script bilinmeyen önceki sürümde durur; onaylı
sonra hash'i zaten varsa tekrar ALTER yapmadan çıkar.

## Kritik: ortak veriyi iki kez uygulama

`10-service-inserts-review.sql` ve `20-customer-classification-review.sql`
**inceleme taslağıdır; aktarıcıdan önce ayrıca çalıştırılmayacak**. Aktarıcı ilk
çalışmada eski müşteri/tip/servis/para birimi parmak izini doğrular. Bu tabloları
önce değiştirmek baseline'ı bozar. G tipi/56 üst kart da aynı aktarıcının
transaction'ındadır. Onay vermek bu dört değişikliği önceden çalıştırma izni değildir.

`40-menu-sms-seed.sql` için **ApprovedRoleCode NULL kalacak**. Mevcut scriptte
bir rol verilirse HasView=1 ve HasEdit=1 birlikte eklenir; salt-okunur açılış için
uygun değildir. Mevcut farklı hakta hata verir. Rol matrisi için gerçek Code ve
önceki MenuRole değerleri olmadan kesin yazmalı yetki SQL'i üretilmez veya
uygulanmaz; seed'in parametresiyle bu ayrı onay atlanmaz.

## Uygulama sırası — henüz uygulanmayacak

1. OP01/AU01/AU02 ve SC01–SC06 kararlarını kişi/tarih/dış kayıt numarasıyla al;
   reddedilen kapsam varsa aktarıcı/planı yeniden değerlendir. Teknik aktör 0
   veya JSON'da true yazmak gerçek insan onayı değildir.
2. [Salt-okuma ön kontrolü](operator-role-preflight.sql) daha sonra yetkili
   oturumda çalıştır: aktif/tekil Code, tüm rol üyelikleri ve etkin hak,
   mükerrer menü/izin, tip-ID eşleşmesi, servis ve prosedür hash'ini kaydet.
   Bu dosya bu hazırlıkta çalıştırılmadı. Sonuçlar özel dosyada korunur.
3. Bakım, BE/Job yazıcı duruşu, kaynak kesiti, tam/log yedek ve geri dönüş
   kapılarını kapat; güncel paket ve aynı sürüm/paket provası onaylarını bağla.
   Yeni kesit/paket değişiminde eski kabul otomatik geçmez; 30 dakika sınırı korunur.
4. Onaylı collection şema/definition/menü-SMS seed'lerini sırayla uygula;
   menü-SMS seed rol kodu NULL. Genel startup seed/migration/Manitou kapalı kalır.
   Ayrı SC05 prosedür taslağı yalnız doğru önceki hash üzerinde uygulanır;
   EXEC sp_SyncCustomers yapılmaz, Job veya Manitou açılmaz.
5. Ortak servis/müşteri tipi/G/üst kart için ayrı SQL yok. Kaynak/hedef
   baseline'ı ve başka oturum yokluğu doğrulandıktan sonra onaylı production
   adaptörünü çalıştır. Bu dört fark ve tüm veri/audit/makbuz aynı transaction'dadır.
   Beklenmeyen mevcut değişiklikte dur; hash'i elle değiştirip devam etme.
6. Finansal mutabakat ve aynı batch replay'i **kullanıcı yazmaları/CDN açılmadan**
   bitir. Commit belirsizse önce mutabakat; kör tekrar, silme, audit temizliği yok.
7. Ayrı AU01 onayıyla yalnız CollectionFollowUp için gerçek rol/önceki değer
   matrisi hazırlanıp prova edilir ve uygulanır. Bütün üyelerde etkin View/Edit
   doğrulanır. Salt-okuma kullanıcısında başka rolden Edit varsa karar alınmadan
   erişim açılmaz. Diğer menüler/roller/üyelikler sessizce değiştirilmez.
8. Production hosting override ve normal diğer worker işletimi onaylanarak
   kullanıcı Jenkins BE→FE yayınını yapar. IsolatedRehearsalMode production'da
   kullanılamaz. Yetkili okuma/yetkisiz 403/Excel/lookup/dosya kabulü yapılır.
9. AU02 ayrı kabulünden sonra onaylı rol matrisinde Edit açılır; finansal
   feature flag'leri ayrıca kontrol edilir. SMS şablon/alıcı/gerçek gönderim ve
   dış sistem Job/Manitou açılışı yine ayrı kapılardır.

Üç ayrı değişiklik grubu tek global transaction değildir: schema/seed, prosedür
ve rol değişikliklerinin commit'i finansal adaptör rollback'iyle geri alınmaz.
Her grubun önce/sonra kanıtı ayrı tutulur; yalnız finansal aktarım atomiktir.

## Durdurma / geri dönüş

Kimlik/rol/önceki değer/hash veya satır kapsamı farklıysa otomatik uygulama yok.
Rol geri dönüşünde yalnız bu yayınla değişen hakkın hâlâ beklenen sonra değerde
olduğu doğrulanarak eski hak geri alınabilir; başka güncel yetkiler ezilmez.
Import sonrası kullanılan servis/G/üst kartlar topluca silinmez. Prosedür geri
dönüşü ayrı onaylı ALTER ile eski gövdeye yapılır; DROP/CREATE yapılmaz. Eski
dosyadaki CREATE başlığıyla çalıştırılmaz; ALTER başlığı module hash'ini
değiştirebileceğinden geri dönüşün gerçek hash'i ayrıca kaydedilir.
Tam DB/PIT restore'u bütün modülleri etkiler; ayrıca onay ve yedek kanıtı gerekir.

Henüz gerçek User.Code/Role.Code ve imzalı kararlar yoktur. **Onay listesi hazır,
canlı yetki/ortak değişiklik uygulaması yapılmadı.** Paketler ve runtime hash'leri
korundu; bu hazırlık yeni finansal prova gerektiren kod değişikliği içermez.

Doğrulama: yerel ScriptDom parser ile SQL sözdizimi hatasız; üst statement'lar
yalnız SELECT/DECLARE/SET ve hedefi koruyan IF→THROW'dur. Şablonun tüm onayları
kapalı ve rol listeleri boş kontrol edildi. DB bağlantısı/SQL yürütme yapılmadı.
Runtime değişmediği için yeniden build veya finansal prova çalıştırılmadı.
