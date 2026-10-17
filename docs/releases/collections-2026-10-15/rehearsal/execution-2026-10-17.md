# 17 Ekim — Onaylı prova restore'u ve şema kurulumu

**Devam kaydı:** Makbuz/audit korumalı finansal veri aktarımı sonraki adımda
gerçekleştirildi. [Veri aktarım provası ve koruma kontrolleri](data-transfer-2026-10-17.md).
Aşağıdaki “veri importu henüz yok” ifadeleri bu ilk restore/şema adımının
tarihsel durumudur; güncel kabul için devam raporu kullanılır.

Kullanıcı `AssistFlow_Performance_Restore` içeriğinin silinebilir olduğunu ve **güncel canlı yedeğinin yalnız bu DB üzerine restore edilmesini** açıkça onayladı. Aynı sunucu/kullanıcı tercihi korundu. **Canlı AssistFlow ve MGS üzerinde DDL/DML/ayar değişikliği yapılmadı.** Prova henüz uçtan uca veri aktarımı kabulü değildir.

## Restore sonucu

- En güncel mevcut AssistFlow tam yedeği: **17 Ekim 2026**; backup metadata bitişi `00:00:02` (SQL sunucusunun kaydettiği saat). HEADERONLY ile DatabaseName/BackupType/BackupSetGUID doğrulandı; VERIFYONLY başarılı.
- `REPLACE` yalnız açıkça onaylanan mevcut prova DB'si için kullanıldı. Önceki 5 Ağustos AssistFlowTest kopyası artık aktif içerik değildir. Eski prova DB'sinin ayrıca yeni yedeği alınmadı; eski içeriğin geri alınabilirliği mevcut eski backup dosyalarının bulunmasına bağlıdır, doğrulanmış sayılmaz.
- MDF: `H:\sqldata\AssistFlow_Performance_Restore.mdf`; LDF: `G:\sqllog\AssistFlow_Performance_Restore_log.ldf`. Bu yolların yalnız hedef DB'ye ait olduğu ve yeterli disk büyüme alanı doğrulandı. Canlı DB dosyaları hedef alınmadı. Başka oturum yokluğu kontrol edildi; başka DB oturumları sonlandırılmadı.
- Restore sonrası ONLINE/MULTI_USER. TRUSTWORTHY, DB chaining ve Service Broker kapalı. Restore sonrası **CHECKDB temiz**, REPAIR yapılmadı.
- Güncel yedek kopyasında **18.886 müşteri / 18.885 aktif müşteri**, **74 migration** bulundu. Bu mevcut yedek saatinin kopyasıdır, canlıyla aynı an olduğu iddia edilmez.

## Şema / seed provası

Production SQL dosyaları değiştirilmeden ayrı prova türev paketi üretildi. Her batch tam `sql2022` + `AssistFlow_Performance_Restore` guard'ı taşır. Dosya SHA'ları ve sıralı predecessor kontrolü korundu; ilk hatada durma ve açık transaction rollback davranışı var. Farklı/yarım migration setinde devam edilmez.

- **21 Tahsilat migrationı + 2 seed** ilk çalıştırmada başarılı.
- Son migration sayısı **95**, `collection` tablo sayısı **34**.
- Tanımlar: 8 ödeme dönemi, 3 sözleşme durumu, 2 abonelik durumu, 7 grup durumu, 10 ödeme yöntemi. Tahsilat menüsü ve varsayılan SMS şablonu yalnız prova DB'sine eklendi.
- Rol kodu NULL bırakıldı; yeni rol/yetki verilmedi. Konfigürasyon seed'i mesaj göndermez. WebAPI/genel seed/worker/Jenkins başlatılmadı; SMS/mail/Manitou/CDN çağrısı yapılmadı.
- Paket **ikinci kez** çalıştırıldı. Migration seti, sekiz tanım/menü/şablon/rol snapshot'ı, müşteri sayısı ve tüm tablo sayımları ilk çalışma sonuyla aynı bulundu. Kimlik/tutar üreten finansal import henüz olmadığı için bu, **veri import idempotency testi değildir**.
- İkinci çalışma sonrası CHECKDB yine temiz. İlk inceleme ile son inceleme arasındaki SQL Agent job etkinlik/adım hash metadata'sı aynı; Joblar açılıp kapatılmadı.

## Özel kanıtlar ve araçlar

BE `.tools` altında; yedek/sunucu metadatası Git'e konmaz:

- `collection-rehearsal-restore-plan-2026-10-17-01/restore-plan.json`: hash kontrollü restore planı.
- `collection-rehearsal-restore-apply-2026-10-17-01/`: VERIFYONLY, başlangıç ve tamamlanma makbuzları. Plan SHA `2EA5C1B0E88BC11C87F4D07C09C3015C542C0D11F9994C2F4C8FA6547E85D03C`.
- `collection-rehearsal-restored-profile-2026-10-17-01.json`: ilk restore/CHECKDB profili.
- `collection-rehearsal-schema-2026-10-17-01/`: 23 dosyalı prova SQL paketi; manifest SHA `CD5DE611AEB1D99CC6D394C2BBA1D10930485DA220899AA3B7ABA64BAD6FDC39`.
- `collection-rehearsal-schema-first-2026-10-17-01.json`, `collection-rehearsal-schema-second-2026-10-17-01.json`: iki çalışma makbuzu.
- `collection-rehearsal-schema-after-first-2026-10-17-01.json` ve `collection-rehearsal-schema-after-second-2026-10-17-01.json`: sayım/snapshot kontrolü. Son profil SHA `D6C83ED38C40A205698C156AD17E41A75F318CBAA00BE5CEA6443B1E896A6E11`.

Araçlar: `Restore-CollectionRehearsal.ps1` varsayılan olarak yalnız plan üretir; hash olmadan restore yapmaz. `Prepare-CollectionRehearsalSchema.ps1` çevrimdışıdır. `Invoke-CollectionRehearsalSchema.ps1` yalnız prova DB'sinde hash'li paket çalıştırır. `Read-CollectionRehearsalTarget.ps1 -CheckDb` sadece açık prova hedefinde REPAIR olmadan CHECKDB uygular. Hiçbiri production hedefinde çalışacak şekilde genelleştirilmedi.

## Veri provasına geçiş

17 Ekim güncel MGS kesiti ve restore hedefinin müşteri/tanım referansları `preview-rehearsal` ile yeniden alındı. Manifest `TargetDatabase=AssistFlow_Performance_Restore`, `RehearsalOnly=true` taşır. İki okuma/hash aynı bulundu; transactional son bakım kesiti değildir. Production paket üreticisinin bu hedef referanslarını **DB bağlantısından ve dosya üretiminden önce reddettiği** olumsuz kontrolle doğrulandı; prova ID'leri canlıya taşınmaz.

- Kaynak: **19.948 müşteri, 9.517 sözleşme, 34.588 tarife tarihçesi, 243.518 ödeme, 12.939 not**. 15 Ekim kesitine göre +4 müşteri/+2 sözleşme/+2 tarihçe/+38 ödeme/+5 not var. Yeni kayıtlar eski karara otomatik kabul edilmedi.
- Önceki 794 karar tekrar karşılaştırıldı; aynı üç açık literal-null onayı kaynak değişmemiş olduğundan korundu.
- Prova hedefi için **4.439 sözleşme** referans onayı sonrası aday, **5.078** sözleşme inceleme/dışlama; para birimi ve legacy kanıtı kontrolü sonrası **139.747 ödeme** aday, **103.771** ödeme inceleme/dışlama. Bu sınıflar tamamı kalıcı ret anlamına gelmez.
- 83.543 banka kimliğinde sorun 0. Gerçek ödeme/tarife/fatura/not/grup/dosya/banka importu henüz yapılmadı; SMS kuyruğu ve finansal tablolar boş kalıyor.
- Yeni özel kanıtlar: `collection-rehearsal-source-2026-10-17-01`, `collection-rehearsal-decisions-2026-10-17-01`, `collection-rehearsal-financial-review-2026-10-17-01`, `collection-rehearsal-payments-2026-10-17-01`.

Kalanlar: güncel kaynak/karar/ödeme/ilişki mutabakatı, tam hedef-kilitli sözleşme/tarife/ödeme/makbuz ve ilişkili veri uygulayıcısı, tüm tutar/hash mutabakatı, ikinci veri importu ve silme/düzenleme/yarım işlem tekrarları, geri dönüş provası, production CDN ve insan onaylayıcı Code'u. Gerçek finansal kayıt henüz import edilmedi. Production yayın durumu **NO-GO** olarak korunur.
