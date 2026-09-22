# Legacy sözleşme kesiti

`tools/Collections.Snapshot`, legacy MGS veritabanından `Core.Customer`, `Core.Contract` ve
`Core.ContractHistory` tablolarını tek bir SQL Snapshot transaction içinde salt-okuma olarak dışa aktarır.
Kaynakta hiçbir create/update/delete/alter komutu çalıştırmaz.

Bağlantı bilgisi dosyaya veya komut satırına yazılmaz:

```powershell
$env:COLLECTION_LEGACY_CONNECTION = '<salt-okuma bağlantı bilgisi>'
dotnet run --project tools/Collections.Snapshot -- export tools/Collections.Snapshot/snapshots/<kesit>
Remove-Item Env:COLLECTION_LEGACY_CONNECTION
```

Araç yalnız `MGS` veritabanını kabul eder, mevcut veya dolu klasörün üzerine yazmaz. Snapshot isolation
kaynak sunucuda etkin değilse tutarsız kesit üretmek yerine işlemi durdurur. Çıktı kişisel veri içerebilir;
Git'e eklenmez ve yalnız yetkili aktarım ortamında saklanır. Üretilen kesit önce `Collections.Import inspect`
ve `profile`, ardından yalnız AssistFlowTest için `stage` ve `validate` adımlarından geçirilir.

19 Eylül 2026 kontrolünde canlı `MGS` veritabanında Snapshot Isolation'ın kapalı olduğu doğrulandı. Araç
bu nedenle veri üretmeden durur ve yarım dosyaları temizler. Canlı veritabanında `ALTER DATABASE`
çalıştırılmayacaktır. DBA tarafından aynı ana ait, erişimi kısıtlı bir restore/kopya sağlandığında araç bu
kopyaya karşı çalıştırılacaktır.

Kaynak sistemin artık aktif kullanılmadığı iş sahibi tarafından doğrulanırsa Snapshot Isolation gerektirmeyen
`--inactive-source` seçeneği kullanılabilir. Bu seçenek yine yalnız `SELECT` çalıştırır; üç tabloyu tek
Serializable transaction içinde okuyarak kesit boyunca değişmemelerini sağlar:

```powershell
dotnet run --project tools/Collections.Snapshot -- export tools/Collections.Snapshot/snapshots/<kesit> --inactive-source
```
