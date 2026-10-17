# 17 Ekim — Kullanıcının belirlediği prova hedefi

**Güncel durum:** Kullanıcı üzerine restore'u açıkça onayladı; restore ve şema/seed ilk/ikinci çalışma tamamlandı. [Uygulama kanıtları](execution-2026-10-17.md). Aşağıdaki eski yedek/restore onayı bekleme tespitleri işlem öncesinin kaydıdır; yeniden onay beklenmiyor.

Kullanıcı `AssistFlow_Performance_Restore` veritabanını, aynı SQL sunucusu ve mevcut kullanıcıyla prova için belirledi. Bu, önceki ayrı instance önerisine **açık hedef tercihi istisnasıdır**; fiziksel/ağ izolasyonu sağlandığı anlamına gelmez. Ayrı instance kontrolü körlemesine kaldırılmadı; eski `00-isolation-preflight.sql` bu ortak-instance profiline uygun değildir ve burada çalıştırılmayacak.

## Salt-okunur tespit

- Bağlantı başarılı; tam hedef `AssistFlow_Performance_Restore`, ONLINE/MULTI_USER, Turkish_CI_AS, SIMPLE.
- Restore geçmişi **5 Ağustos 2026 AssistFlowTest yedeği** gösteriyor. Bu güncel AssistFlow canlı restore'u değil.
- 17.537 müşteri / 17.532 aktif müşteri, 69 migration, collection tablosu **0**. 15 Ekim canlı kaynak paketindeki müşteri kimlik/sınıflandırma fingerprint'i bu hedefe uygulama izni sayılmaz.
- TRUSTWORTHY, DB chaining ve Service Broker kapalı. Etkin DB veya server trigger'ı görülmedi. Job adımı metadata'sında hedef DB'ye açık referans **0**; bu dinamik bağlantıların yokluğunu ispatlamaz.
- 30 dış SQL bağımlılığı; bazı rapor prosedürleri doğrudan canlı `AssistFlow` veritabanına referans veriyor. Beş modül içerik inceleme işareti taşıyor. Bunlar prova sırasında çağrılmayacak; WebAPI/genel rapor/senkronizasyon/worker başlatılmayacak.
- Mevcut SQL kullanıcısı sysadmin; aynı kullanıcıyla bağlanılması DB düzeyinde güvenlik sınırı oluşturmaz. Prova uygulayıcısında tam sunucu+DB allowlist'i, sabit yazma sorguları ve cross-DB yazma/prosedür çağrısı reddi gerekir. Canlı Joblar/linked server/ağ veya instance ayarları topluca değiştirilmez.

Rapor Git dışı `.tools/collection-rehearsal-target-2026-10-17-01.json`. Yalnız SELECT çalıştırıldı; şema/veri/Job/CDN değişmedi.

## İlerleme kapısı

Mevcut hedefin üzerine güncel canlı yedeği restore etmek mevcut prova DB verisini geri döndürülmesi zor şekilde değiştirebilir. **Kullanıcının hedefin silinebilir/üzerine restore edilebilir olduğunu açıkça onaylaması gerekir.** Henüz DROP, RESTORE WITH REPLACE, migration, seed veya INSERT/UPDATE yapılmadı.

Onay sonrasında dahi önce mevcut hedefin saklama/geri dönüş gereksinimi, güncel AssistFlow yedeği ve açık DATA/LOG MOVE listesi belirlenir. Canlı dosya yolları veya kaynak backup DB'si hedef sanılmaz. Restore yalnız açıkça belirtilen prova DB'sine; gerçek AssistFlow/MGS ve onların dosyalarına dokunulmaz. Kaynak MGS bu aşamada yalnız hash'li kesit/SELECT kanıtıdır.

Yeni restore üzerinde müşteri/referans/migration fingerprint'i tekrar alınmalı; 15 Ekim paketi son kesit diye kullanılmamalıdır. Prova şema/seed ve tam apply yolu henüz hazırlanıp çalıştırılmış değildir. Gerçek insan onaylayıcı User.Code hâlâ bekleniyor; teknik aktör 0 insan onayı yerine geçmez.
