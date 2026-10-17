-- Explicit user approval: CollectionFollowUp menu and Admin view/edit only.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF DB_NAME() <> N'AssistFlowTest' THROW 51000, N'Hedef veritabanı uygun değil.', 1;
    DECLARE @lock int;
    EXEC @lock=sys.sp_getapplock @Resource=N'CollectionAdminGrant', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
    IF @lock<0 THROW 51000, N'Yetki kilidi alınamadı.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.Roles WHERE Id=1 AND Code=N'ADMIN' AND IsDeleted=0)
        THROW 51000, N'Onaylı Admin rolü doğrulanamadı.', 1;
    IF (SELECT COUNT(*) FROM dbo.Menus WHERE Name=N'CollectionFollowUp')>1
        THROW 51000, N'Mükerrer menü kaydı var.', 1;
    DECLARE @menu bigint=(SELECT Id FROM dbo.Menus WHERE Name=N'CollectionFollowUp');
    IF @menu IS NULL
    BEGIN
        INSERT dbo.Menus(Name,Description) VALUES(N'CollectionFollowUp',N'Tahsilat sözleşmeleri ve takip');
        SET @menu=CONVERT(bigint,SCOPE_IDENTITY());
    END;
    IF (SELECT COUNT(*) FROM dbo.MenuRole WHERE ModulId=@menu AND RoleId=1)>1
        THROW 51000, N'Mükerrer menü yetkisi var.', 1;
    IF EXISTS(SELECT 1 FROM dbo.MenuRole WHERE ModulId=@menu AND RoleId=1)
        UPDATE dbo.MenuRole SET HasView=1,HasEdit=1 WHERE ModulId=@menu AND RoleId=1;
    ELSE
        INSERT dbo.MenuRole(ModulId,RoleId,HasView,HasEdit) VALUES(@menu,1,1,1);
    COMMIT;
    SELECT m.Id,m.Name,mr.RoleId,mr.HasView,mr.HasEdit FROM dbo.Menus m
        JOIN dbo.MenuRole mr ON mr.ModulId=m.Id WHERE m.Id=@menu AND mr.RoleId=1;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
