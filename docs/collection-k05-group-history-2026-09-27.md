# K05 — Grup tahsilat durum geçmişi

27 Eylül 2026. Geliştirme ve net kayıtların test aktarımı uygulandı; oturumlu ekran kabulü açık.

## Legacy karşılığı ve davranış

Core.FollowGroupStatus, ContractID + PeriodYear + PeriodMonth için mevcut son etiketi/açıklamayı saklar. Kaynakta değişiklik zamanı veya kullanıcı kolonları yoktur; geçmiş kullanıcı/tarih uydurulmaz. Aynı dönemdeki bütün eski değişiklikleri içeren bir olay günlüğü değildir.

DashboardGroup.aspx.cs SQL_UPDATE dönem kaydını günceller/ekler. SQL_UPDATE_ODENDI ise ödeme aksiyonunda durum 6 yazar. Bu geliştirmede geçmiş dönem durumları taşındı ve mevcut manuel durum düzenleme korundu. Ödeme kaydetme/silme/düzeltme ile otomatik durum değiştirme eklenmedi; etiket finansal borç/ödeme hesabının girdisi değildir. Legacy'nin ödeme aksiyonunda her durumda Ödendi yazması tam ödeme ispatı sayılmaz. Otomatik Ödendi davranışı son kullanıcı kabulünde ayrıca ele alınacak; bu tur karşılandı diye raporlanmaz.

## Uygulanan yapı

- Tahsilat Takibi içinden Grup durum geçmişi bağlantısı: /crm/collections/group-status-history.
- Grup, durum (pasif tarihsel tanımlar dahil), başlangıç/bitiş ayı ve müşteri/abone/açıklama filtreleri.
- GET /api/collections/tracking/group-status-history; mevcut CollectionFollowUp görüntüleme yetkisi, modül açma kontrolü ve onaylı grup müşteri kapsamı.
- SQL tarafında filtre/sıralama/sayfalama; API sayfa boyutu en fazla 100. Dönem ve kimlikle deterministik sıralama.
- Grup detayındaki Dönem Geçmişi sekmesi yalnız ilgili sözleşmenin geçmişini getirir. Dönem satırından mevcut /tracking/:id?period=... detayına gidilir.
- Mevcut POST durum kaydetme ve rowversion denetimi kullanılır. Kaydetme sonrası açık geçmiş listelerinin verisi yenilenir. Türkçe mesajlar ortak toast üzerinden gösterilir.
- Grup seçici mevcut CollectionDefinitionSelect'i kullanır; tanım enumuna sona CustomerGroup=6 eklendi, mevcut değerler değişmedi.
- Tahsilat dışı müşteri/servis ekranları değiştirilmedi.

## Şema ve aktarım

Migration: 20260927190000_AddCollectionGroupFollowUpSource; yalnız AssistFlowTest'e uygulandı.

collection.ContractPeriodFollowUp:
- LegacyFollowGroupStatusId (nullable, tekil indeks).
- SourceHash (binary(32)).
- IsDeleted/Period/Id çalışma listesi indeksi.
- Mevcut sözleşme/dönem tekillik ve FK kuralları korunur.

Collections.Import group-history <Development JSON> <yerel rapor JSON> [--install | --verify | plan SHA-256]

- --install yalnız beklenen tek K05 migrationını uygular ve model/snapshot eşitliğini denetler.
- Önizleme MGS'den yalnız SELECT okur. Hedef yalnız AssistFlowTest olabilir.
- Sahiplik MGS Contract migration eşlemesi + halen korunmuş ve grup kapsamında sözleşme üzerinden belirlenir.
- Durumlar Türkçe büyük/küçük harf ve kenar boşluğu normalize edilmiş tam adla eşlenir; kaynak ID hedef ID olarak varsayılmaz.
- Boş sözleşme, eksik/çoklu eşleşme, geçersiz dönem, tanımsız durum, uzun açıklama, hedef dönem çakışması ve farklı içerikli tekrarlar ayrı raporlanır.
- Aynı sözleşme/dönemde durum ve açıklaması birebir aynı iki çift bulundu. Her çift tek kayıt olarak temsil edildi; küçük kaynak kimliği yalnız kanonik seçimdir, en yeni kayıt iddiası değildir. İki fazla kaynak satırı ham raporda korunur.
- Serializable transaction + benzersiz kaynak kimliği + tekrar hesaplanan SHA-256 planı. Önceden aktarılan kaynağın kimliği/hash'i değişmişse uygulama durur; mevcut kullanıcı düzenlemesinin üstüne yazılmaz.
- Kaynakta audit zamanı olmadığından CreatedDate aktarım zamanı, CreatedUser=0 otomatik aktarım aktörüdür. Bu, eski işlem tarihi/kullanıcısı diye sunulmaz.

## Sonuç

| Sonuç | Adet |
| --- | ---: |
| Legacy kaynak satırı | 55.597 |
| Testte oluşturulan/eşleşen dönem kaydı | 26.207 |
| Birebir aynı tekrar, tek kayıtta temsil | 2 |
| Korunmuş sözleşme eşlemesi bulunmayan | 29.089 |
| Sözleşme kimliği boş | 96 |
| Grup tahsilat kapsamı dışında | 203 |
| Son kontrolde yeni aktarım adayı | 0 |

29.089 satır tümüyle kalıcı dışlama sayılmaz; henüz aktarılmamış sözleşmeler ve onaylı elenen abonelikler sonraki veri mutabakatında ayrıştırılacak. Finansal sözleşme/tarife/ödeme tablolarına bu araç yazmaz.

Yerel ham raporlar Git dışında .tools altında:
- k05-group-history-plan.json ve .applied.json: ilk 26.205 kayıt.
- k05-group-history-identical.json ve .applied.json: iki tekilleştirilmiş dönem.
- k05-group-history-verify.json: son kontrol, 26.207 eşleşme ve sıfır yeni aday.

İlk uygulama hash'i: 563E80FE9824AFA320C2345BD59A36FFF6CB0B8F527CC50DA2DE6991CD949085.
İkinci uygulama hash'i: D60BA80117A02004FFACC2D446D9FB89626AC5B091E0686D13BB0C2BBD40DCC5.

## Doğrulama / kabul

- BE solution izole çıktı klasöründe ve aktarım aracı derlendi.
- FE production build ve değişen ekranlarda ESLint başarılı.
- Genel proje type-check mevcut başka hatalar nedeniyle temiz değildir; K05 dosyaları/tanım/route filtreli çıktısında hata yok.
- Testte model/snapshot uyumu, migration, gerçek EF liste sorgusu ve ilk üç dönem kaydının mevcut detay servisiyle durum/açıklama eşitliği doğrulandı.
- Tekrar çalıştırmada 26.207 AlreadyApplied; yeni Ready=0.
- Güncel API yeniden başlatıldıktan sonra oturumlu tarayıcıda filtreler, sayfalama, sekmeler, durum düzenleme, yetki/çakışma kabulü açık.
- Ödeme aksiyonu sonrasındaki otomatik Ödendi davranışı bu tur uygulanmadı; nihai legacy davranış kabulünde açık.
- K12 veri işi: eşleşmeyen 29.089 ve kimliği boş 96 kaydın kalan kaynak/sözleşme kararlarıyla mutabakatı.

## Canlı geçiş

Migration ve araç sürümlüdür; test ID/plan hash'i canlıda kullanılmaz. Önce canlı sözleşme ve durum tanımları eşlenir; kaynak son kesitiyle yeni plan/mutabakat alınır. Test hedef kilidi gevşetilerek canlıya yazılmaz; canlı uygulama yolu ayrı hazırlanır. Otomatik Down/kolon silme yapılmaz; kaynak kimliği ve kullanıcı tarafından düzenlenmiş durumlar korunarak geri dönüş planlanır. MGS ve AssistFlow canlı değişmedi.

