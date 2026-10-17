-- Ortak dbo insert ADAY listesi; yalnız SELECT. Uygulama scripti değildir.
-- Kimlikler canlıdan çözülür; mevcut tanım/tipler yeniden adlandırılmaz.
SET NOCOUNT ON;
IF DB_NAME()<>N'AssistFlow' THROW 51000,N'Hedef AssistFlow olmalıdır.',1;
WITH SourceServices AS (
 SELECT * FROM (VALUES
 (N'26',N'Gözlem'),(N'27',N'GPRS'),(N'28',N'Kiralık'),(N'29',N'Kiralık + Açma Kapama Rapor'),
 (N'30',N'Gözlem + Servis Bakım'),(N'31',N'Gözlem + GPRS'),(N'32',N'Gözlem + Açma Kapama Rapor'),
 (N'33',N'Gözlem + Aylık Rapor'),(N'34',N'Gözlem + Anlık Rapor'),(N'1032',N'Servis'),
 (N'2032',N'Hizmet Yok'),(N'2033',N'Bakım'),(N'2034',N'GPRS Modülü'),(N'2035',N'SMS Hizmeti'),
 (N'2036',N'GPRS Vodafone'),(N'2037',N'8 GB GPRS'),(N'2038',N'Sağlık Takip Bilekliği'),
 (N'2039',N'GPRS TTKom'),(N'2040',N'STB GSM + Gözlem')) s(LegacyId,Name)
)
SELECT s.LegacyId,s.Name,COUNT(t.Id) AS ActiveMatches,MIN(t.Id) AS CandidateLiveId,
 CASE COUNT(t.Id) WHEN 0 THEN N'EKLEME ADAYI — ad/diğer yakın kayıtlar ayrıca incelenmeli'
 WHEN 1 THEN N'MEVCUT KAYDA EŞLEME ADAYI' ELSE N'ÇAKIŞMA — UYGULAMA YOK' END AS Decision
 FROM SourceServices s LEFT JOIN dbo.ServiceType t ON t.IsDeleted=0
 AND UPPER(REPLACE(REPLACE(LTRIM(RTRIM(t.Name)),N'  ',N' '),N'  ',N' ')) COLLATE Turkish_CI_AS
 = UPPER(s.Name) COLLATE Turkish_CI_AS GROUP BY s.LegacyId,s.Name ORDER BY s.LegacyId;
-- G/A üst kart ve cari tipleri: eski Kurumsal/BANKA tipini ezmek yerine ayrı karar gerekir.
SELECT Id,Name,Code FROM dbo.CustomerType ORDER BY Code;
