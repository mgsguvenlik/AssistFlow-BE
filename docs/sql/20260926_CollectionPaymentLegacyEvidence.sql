-- MGS üzerinde yalnız okuma. Sonuç sırası compare-payment-currency kanıt biçimidir.
-- Ortak tanımlar/eşlemeler güncellenmez; fonksiyonlar çalıştırılmaz, tanımları okunur.
SELECT CurrencyID,Name FROM Definition.Currency ORDER BY CurrencyID;
SELECT PaymentTypeID,Name FROM Definition.PaymentType ORDER BY PaymentTypeID;
SELECT CONVERT(nvarchar(128),DATABASEPROPERTYEX(DB_NAME(),'Collation')) AS DatabaseCollation, YEAR(GETDATE()) AS SourceYear;
SELECT s.name AS SchemaName,o.name AS ObjectName,m.definition AS Definition
FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id
JOIN sys.sql_modules m ON m.object_id=o.object_id
WHERE (s.name='Core' AND o.name IN ('vPayment','vCustomerContractHistory','vContractHistory','vContract','vCustomer'))
OR (s.name='dbo' AND o.name IN ('vPaymentCurrency','TarihTablosu','fnTahsilatTakibi','fnTahsilatTakibiGrup'));
SELECT BatchId,SourceId,TargetCurrencyTypeId,Status
FROM AssistFlowTest.collection.MigrationReferenceMap WHERE BatchId=1 AND ReferenceKind='CurrencyType';
