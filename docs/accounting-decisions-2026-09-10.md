# Muhasebe kararları — 10 Eylül 2026

> Güncelleme: 11 Eylül kullanıcı açıklaması ve teknik karar yetkisiyle VAR/YOK/boş ve başlangıç sınırları kapatılmıştır. Güncel kaynak `accounting-decisions-2026-09-11.md`; aşağıdaki bekleme maddeleri tarihsel kayıttır.

Bu belge müşterinin yeni cevaplarını kaydeder ve önceki teknik varsayımların yerine geçer. Önceki belgeler tarihsel kanıttır, yeni cevaplarla çelişen bölümleri uygulama kuralı değildir.

## Kabul edilen cevaplar

- VAR/YOK/boş: müşterinin sözleşme durumu ve tahsilata eklenip eklenmeme durumunu ifade eder. Önceki “tahsilata etkisi yok” kararı iptal. Ancak hangi değerin hangi dahil olma durumuna karşılık geldiği cevapta açık değil; borç uygunluk filtresi henüz kodlanmayacak.
- Ücretsiz: hizmet sürer, borç oluşmaz. Bu kural korunur. Ödeme yöntemi ile tarihçe alanlarının teknik eşdeğerliği cevapta ayrıca açıklanmamıştır; kaynak alanlar kayıpsız ve ayrı tutulacak, otomatik birleştirme yok.
- Kart bilgileri tutulmuyor; planlanan online tahsilat entegrasyonu yapılmamış. Kart/banka kartı alanları ve sırf bunlar için eklenen Bank model/configuration taslağı mevcut geliştirmeden çıkarıldı. Online tahsilat gelecekte ayrı kapsam. Eski PaymentMethod kimlikleri tarihsel kaynak metadata/eşleme olarak korunur; bunları korumak POS entegrasyonu geliştirmek değildir. Kaynak DB veya ham legacy kayıtlar silinmedi.
- Periyot tutarı tam dönem tutarıdır: üç aylık 3.000 TL, üç ayda bir 3.000 TL. Mevcut interval/tam tutar hesabı bu cevapla uyumlu.
- Başlangıçta kıst yok, tam tutar ve aynı-gün yenileme söyleniyor. Ek açıklama: ayın 15'inden önce o ay, 15'inden sonra sonraki ay; 28'inde imzalanan o aya alınmıyor. Önceki “imza gününde mutlaka borç” varsayımı iptal.

## Kodlamadan önce netleşmesi gereken hesap sınırları

1. VAR, YOK, boş için tek tek tahsilata dahil/hariç eşlemesi; boşta otomatik dahil/hariç seçilmeyecek. Dahil değilken eski borç/ödemeler görünmez veya silinmiş kabul edilmeyecek.
2. Ayın tam 15'inde imzalanan sözleşmenin ilk borç ayı.
3. 28 Eylül imzasında ilk borç 1 Ekim mi, 28 Ekim mi? Yenileme günü 28 mi? “Sonraki ay” tahakkuk tarihi mi yoksa yalnız rapor/muhasebe ayı mı? Aynı-gün ile ay sınıflaması ayrıştırılmalı.
4. Son cümledeki “ikisini de imzaladıysa” ifadesi hangi senaryoyu anlatıyor? “2'sinde” anlamı tahmin edilmeyecek.

Bu cevaplar gelene kadar genel gün-ankrajlı takvim primitive'i korunur; onaylı başlangıç tarihini girdiden alır. İmza tarihi → ilk tahakkuk/rapor ayı dönüşümünü uyguladığı iddia edilmez. Aktivasyon ve ilk borç üretimi kapalı kalır. Mevcut takvim testleri teknik tarih üretimi testleridir, yeni 15'i kesme kuralının kabul testi değildir.

## Etkilenen görevler

- P02: Bank taslağı/kart kolonları çıkarıldı; POS online entegrasyon yok. Durum dahil olma matrisi bekleniyor.
- P01/P05/P06/P12: imza tarihi, ilk borç tarihi, yenileme ankrajı ve rapor ayı ayrımının yeni açıklamayla tasarımı; legacy bilinmeyen gün uydurulmaz.
- P08: kart form alanı eklenmeyecek; durum/başlangıç açıklamaları matris sonrası gösterilecek.
- P15: VAR/YOK/boş ve 14/15/16/28 tarihli başlangıç sınır testleri matris netleşince zorunlu.

İptal edilen kart taslağının iki dosyası yalnız bu agent'ın henüz DB'ye uygulanmamış yeni dosyalarıydı; kaynak veri silinmedi. Gerekirse önceki taslak konuşma/değişiklik kaydından yeniden oluşturulabilir.
