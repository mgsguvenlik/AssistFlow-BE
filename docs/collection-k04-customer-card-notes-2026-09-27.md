# K04 — Tahsilat müşteri kartı ve not geçmişi

27 Eylül 2026.

## Uygulanan kapsam

- Tahsilat altında `/crm/collections/customers/:id`: üstte müşteri, abone, grup, iletişim ve adres özeti; altta Sözleşmeler, Ödeme Hareketleri, Not Geçmişi ve uygun müşterilerde Grup Bilgileri sekmeleri.
- Geri düğmesi sözleşme ana listesine döner. Sözleşme listesi/detayı, takip listesinin müşteri satırları ve grup üst kartı üzerinden erişilir.
- Sözleşmeler mevcut API'nin CustomerId filtresini kullanır. Ödemeler yalnız kartın kendi sözleşmelerinden gelir; üst kart üyelerinin ödemeleriyle birleştirilmez.
- Not ekleme/düzenleme/silme GET/POST API, mevcut menü View/Edit yetkileri, modül/işlem açma bayrakları, AutofacBusinessModule ve Türkçe ortak toast üzerinden çalışır.
- Notlar collection.CustomerNote tablosundadır. Ortak Customer.Note salt okunur özet olarak ayrı gösterilir; Customer tablosunun yapısı ve diğer müşteri ekranları değişmedi.
- Not metni düz metindir; HTML çalıştırılmaz. 10000 karakter sınırı, zorunlu metin, müşteri kapsamı, sunucu tarafında 25 satırlık UI sayfalama (API en fazla 100), müşteri/tarih indeksi ve rowversion çakışma kontrolü uygulanır.
- Not silme audit bilgilerini koruyan soft delete kullanır. Finansal düzeltme/silme davranışına etkisi yoktur.
- Yeni işlemlerde gerçek kullanıcı/tarih; legacy notta özgün CommentID/CustomerID, oluşturan/değiştiren adı/tarihi ve kaynak SHA-256 korunur. Eski kullanıcı ID'leri yeni kullanıcıya tahminen eşlenmez.
- Kaynak kanıtı: Customer.aspx.cs, SQL_Comment SELECT/INSERT/UPDATE/DELETE. Sözleşme ve ödeme geçmişi mevcut servislerden; yeni finansal süreç eklenmedi.

## Test şeması ve aktarım

Migration: `20260927180000_AddCollectionCustomerNotes`; yalnız AssistFlowTest'e uygulandı. Snapshot/model eşitliği araç tarafından doğrulandı.

`Collections.Import customer-notes <Development JSON> <yerel rapor JSON> [--install | --verify | plan SHA-256]`

- Parametresiz mod önizleme; --install yalnız beklenen tek migrationı uygular; --verify salt okunur kayıt/servis kontrolüdür.
- Hedef sunucu/veritabanı sabit test kontrolü, seri hale getirilebilir transaction, benzersiz LegacyCommentId ve yeniden hesaplanan plan hash'i bulunur.
- MGS yalnız SELECT ile okunur. Sahiplik yalnız korunmuş sözleşmenin migration/stage eşlemesi veya doğrulanmış GroupParent ilişkisi üzerinden bulunur; ad/abone numarasından yeni tahmin yapılmaz.
- Kaynak veya eski aktarım eşlemesi değişmişse üzerine yazılmaz. Kullanıcı düzenlemeleri ve silinmiş notlar tekrar aktarımda diriltilmez.
- Tarihlerin legacy ham halleri saklanır; CreatedDate için Türkiye tarihsel saat dilimi kullanılır, boş kaynak tarihte aktarım zamanı kullanılır.

| Sonuç | Adet |
| --- | ---: |
| Kaynak Core.Comment | 12.820 |
| Aktarılan ve tekrar kontrolde eşleşen | 8.387 |
| Korunmuş/doğrulanmış müşteri eşlemesi bulunmayan | 4.431 |
| Tahsilat kapsamı dışındaki müşteri | 2 |
| Tekrar çalışmada yeni aday | 0 |

4.431 kayıt kesin silme/dışlama kararı değildir. Elenen eski abonelikler, henüz aktarılmayan müşteriler ve eşleştirme eksikleri satır bazında K12'de ayrıştırılmalıdır. Bu müşterilere ait notlar başka müşteriye taşınmadı.

Yerel ham raporlar (Git dışı, müşteri/not metni içerir): `.tools/k04-notes-plan.json`, `.applied.json`, `.tools/k04-notes-verify.json`.
Uygulanan plan SHA-256: D07060DDCEDF567453C0EFA94B1B6386C202709752F1A9C1FCE5E97F5A0580A2.

## Doğrulama ve kalan kabul

- BE solution ayrı çıktı klasöründe derlendi; çalışan API dosya kilidi yüzünden normal Debug kopyalama başarısız olduğundan izole çıktı kullanıldı.
- FE production build ve değişen FE dosyalarının ESLint kontrolü başarılı. Genel type-check mevcut proje hataları nedeniyle temiz değil; yeni müşteri dosyalarında hata görülmedi.
- Gerçek test verisiyle müşteri kartı, not sayfalama, ödeme sorgusu; aktarım sayısı/kimlik/hash ve tekrar güvenliği doğrulandı.
- Çalışan API yeni sürümle yeniden başlatılmalı; giriş yapılmış tarayıcıda sekmeler, not ekleme/düzenleme/silme, çakışma ve yetki kabulü henüz tamamlanmadı. K04 nihai kabulü bu kontrollerden sonra kapatılacak.
- 4.431 eşleşmeyen notun neden bazlı ayrıntılı dağılımı K12 veri işi olarak açık.

## Canlı hazırlığı

Migration ve servisler sürümlüdür. Test kimlikleri production'a taşınmaz; hedef müşteri/migration/üst kart eşlemesi yeniden çözülür. Test aracının hedef kilidi kaldırılıp doğrudan canlıya yazılmaz. Canlı geçişte önizleme ve hash'li uygulama yolu ayrıca hedef doğrulamasıyla hazırlanır. Rollback otomatik Down değildir; yeni kullanıcı notları ve düzenlemeleri kontrol edilmeden tablo silinmez. AssistFlow canlı ve MGS değiştirilmedi.

