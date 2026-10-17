# Aktör/audit eşlemesi ve izole prova hazırlığı

15 Ekim 2026. **Hazırlık tamamlandı; gerçek restore/aktarım provası yapılmadı.** Canlı üzerinde sadece sabit SELECT'lerle kullanıcı sayımı, audit FK ve mevcut backup metadata'sı okundu. Kullanıcı/rol ekleme, yeni backup, restore, DB/CDN yazma, Job veya uygulama başlatma yoktur.

## Aktör ve tarih politikası

- Teknik aktarım aktörü, mevcut importer davranışıyla uyumlu **0 = legacy aktarım sistemi**. Bu gerçek bir `dbo.Users` kaydı veya işlemi onaylayan insan değildir. Canlıda Id=0 kullanıcı **yok**; hedef EF modelinde ve ilgili canlı tablolarda bu audit alanlarının kullanıcı FK'si **yok**. Sistem aktörü önkoşulu bu kesitte geçti; uygulama anında tekrar kontrol edilir.
- Gerçek onaylayan/çalıştıran kişi **ayrı** kaydedilecek. Kullanıcının açıkça bildirdiği canlı User.Code tekil/aktif doğrulanmadan onay kimliği atanmaz; Id=1 seçilmez. Bu alan şu an boş/onay bekliyor.
- Legacy CreatedBy/ModifiedBy ham etiketleri hedef kullanıcı adına benzer diye eşlenmez. Kaynak kimliği, ham JSON, hash ve tarih/ad kanıtı MigrationSourceRow'da; entity destekliyorsa Legacy alanlarında da korunacak. Bu gereklilik henüz yazılmamış tam apply yolunun kabul kapısıdır.
- Eski importer'lardaki `AssumeLocal` sunucu saatine bağlıdır. Yeni audit planı açık **Europe/Istanbul** tarihsel offset'iyle UTC önerisi üretir; ham tarihi korur. Belirsiz/geçersiz tarihte tahmin yapılmaz. Kaynak tarih eksikse tarihi uydurmak yerine NULL veya zorunlu alanda gerekçesiyle kayıtlı aktarım zamanı kullanılacak.

## Hazırlanan audit kapsamı

| Kaynak varlık | Audit satırı |
| --- | ---: |
| Sözleşme | 4.438 |
| Tarife tarihçesi | 17.268 |
| Ödeme | 139.717 |
| Müşteri notu | 8.277 (2'si üst kart koşullu) |
| Fatura | 1.813 |
| Fatura ödemesi | 1.904 |
| **Toplam** | **173.417** |

CreatedBy/ModifiedBy alanı + ham değer bazında **42 farklı etiket** var; bu 42 tekil gerçek kişi olduğu iddiası değildir. Eksik/geçersiz/belirsiz tarih sonucu **0**. Grup geçmişi, üst kart, cari, banka ve dosyalar için ayrı politika üretildi: kaynakta olmayan kişi/tarih icat edilmez; aktarım zamanı ve kaynak/hash/ilişki makbuzu korunur.

Ödemede kararlı legacy RequestId + PaymentOperation makbuzu kullanılır. Ödeme/ham kaynak/map/makbuz aynı transaction içinde olmalı; sonradan fiziksel silinmiş ödemeyi retry geri getiremez. Ortak müşteri/prosedür değişikliklerinde eski/yeni değer, SQL hash'i ve gerçek operatör onayı ayrıca korunmalıdır. Politikayı hazırlamak, bu makbuzların canlıya yazıldığı anlamına gelmez.

## Yedek ve ortam tespiti

- msdb metadata'sında AssistFlow ve MGS için 15 Ekim tarihli **iki tam yedek** kaydı bulundu; özel pakette kaynak path/LSN/bitiş bilgileri var. Parola veya backup içeriği okunmadı.
- Her ikisinde `has_backup_checksums=false`; bu bozuk oldukları anlamına gelmez, checksum kanıtı olmadığı anlamına gelir. Dosya okunabilirliği, VERIFYONLY, restore ve CHECKDB henüz yapılmadı.
- Yedekler bağımsız zamanlarda alınmış; güncel salt-okunur kaynak/kimlik kesitiyle aynı an veya final bakım yedeği sayılmaz. Restore fingerprint farkı varsa yeni paket/kesit gerekecek.
- Onaylı ayrı SQL instance ve DATA/LOG yerleşimi henüz belirtilmedi. Canlı instanceında rastgele DB oluşturulmadı; AssistFlowTest prova hedefi olarak değiştirilmedi. Gerçek onaylayan User.Code da henüz belirtilmedi.

## Üretilen hazırlık paketi

BE `.tools/collection-audit-rehearsal-2026-10-15-01` Git dışındadır:

- `source-audit-plan.ndjson`: 173.417 kayıt için sistem aktörü, ham kişi/tarih, UTC önerisi, kaynak hash'i ve kapalı uygulama durumu.
- `legacy-actor-inventory.json`, `audit-policy.json`, `other-entity-audit-policy.json`.
- `live-metadata-read-only.json`: hassas yerel sunucu/yedek envanteri; genel paylaşıma/Git'e konmaz.
- `rehearsal-environment.example.json`: boş host/operatör ve kapalı restore onayı; gerçek bağlantı/parola içermez.
- `rehearsal-plan.json`: aktif veri ve 21 dosyalı şema manifestine hash bağlantısı, adımlar/kabul kapıları.

Paket hash listesi SHA-256: `0292EA507F5ED55E8CB0FF35D05A7C2CBCD024E1152F3825264C40392908BB31`.
Bağlı veri paketi `collection-production-data-package-2026-10-15-03`, hash `1A48ECE4B080FAEFEDC436C80ED6969679593CA583E05268FDEF5838F92CAEA3`.

Uygulama kodu/test-only araçlar değiştirilmedi; yalnız release hazırlık aracı ve dokümanlar geliştirildi. Hazırlık aracı build'i 0 hata/0 uyarı. Kaynak/audit sayımları, kimlik/hash ve timestamp sonuçları kontrol edildi. Restore, şema veya veri uygulaması başarılı diye raporlanmaz.

## Devam için gerekenler

1. Ayrı izole SQL instance/host ve güvenli bağlantı; canlı/SMS/mail/CDN/Manitou ağ çıkışı engeli.
2. O instanceın okuyabildiği doğrulanacak yedek dosyaları ve DATA/LOG hedef yolları. Mevcut canlı backup metadata'sını otomatik restore izni sayma.
3. Onaylayacak/uygulayacak canlı kullanıcının **Code** değeri.

[Prova sırası ve kabul tablosu](rehearsal/README.md) hazır. Bu bilgilerden sonra izole instance/DB kilitli uygulayıcı ve script türevleri tamamlanıp restore/tekrar/rollback kanıtı üretilecek. **Tam apply yolu henüz uygulanmış/bitmiş değil; production paketi NO-GO durumunda.**
