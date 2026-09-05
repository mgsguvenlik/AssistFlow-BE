CREATE OR ALTER PROCEDURE [ykb].[usp_ReportSearchYkb_MultiServiceTypes]
    @Page                int             = 1,
    @PageSize            int             = 50,
    @SortBy              nvarchar(32)    = N'created_desc',

    @CreatedFrom         datetimeoffset  = NULL,
    @CreatedTo           datetimeoffset  = NULL,
    @ServicesDateFrom    datetimeoffset  = NULL,
    @ServicesDateTo      datetimeoffset  = NULL,

    @Search              nvarchar(200)   = NULL,
    @RequestNo           nvarchar(100)   = NULL,

    @CustomerId          bigint          = NULL,
    @CustomerName        nvarchar(200)   = NULL,
    @TechnicianId        bigint          = NULL,
    @ServiceTypeId       bigint          = NULL,
    @StepCode            nvarchar(50)    = NULL,

    @IsAgreement         bit             = NULL,
    @IsLocationValid     bit             = NULL,
    @HasImages           bit             = NULL,

    @WorkFlowStatusesCsv nvarchar(max)   = NULL,
    @TechStatusesCsv     nvarchar(max)   = NULL,
    @PricingStatusesCsv  nvarchar(max)   = NULL,
    @FinalStatusesCsv    nvarchar(max)   = NULL,

    @ProductId           bigint          = NULL,
    @ProductCode         nvarchar(100)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @orderBy nvarchar(200) =
        CASE @SortBy
            WHEN N'created_asc'       THEN N' ORDER BY b.CreatedDate ASC '
            WHEN N'created_desc'      THEN N' ORDER BY b.CreatedDate DESC '
            WHEN N'servicesdate_asc'  THEN N' ORDER BY b.ServicesDate ASC '
            WHEN N'servicesdate_desc' THEN N' ORDER BY b.ServicesDate DESC '
            WHEN N'title_asc'         THEN N' ORDER BY b.RequestTitle ASC '
            WHEN N'title_desc'        THEN N' ORDER BY b.RequestTitle DESC '
            ELSE                           N' ORDER BY b.CreatedDate DESC '
        END;

    DECLARE @sql nvarchar(max) =
    N';
WITH base AS (
    SELECT
        wf.RequestNo,
        wf.RequestTitle,
        wf.WorkFlowStatus,
        wf.CreatedDate,
        ws.Code           AS StepCode,
        sr.CustomerId,
        sr.ServicesDate,
        sr.ServiceTypeId,
        st.Name           AS ServiceTypeName,
        wf.ApproverTechnicianId AS TechnicianId,
        tech.Name AS TechnicianName
    FROM ykb.YkbWorkFlow             wf WITH (NOLOCK)
    JOIN ykb.YkbServicesRequest      sr WITH (NOLOCK) ON sr.RequestNo = wf.RequestNo
    LEFT JOIN ykb.YkbWorkFlowStep    ws WITH (NOLOCK) ON ws.Id = wf.CurrentStepId
    OUTER APPLY (
            SELECT STRING_AGG(CONVERT(nvarchar(max), lookup.Name), N'', '')
                       WITHIN GROUP (ORDER BY lookup.Id) AS Name,
                   STRING_AGG(CONVERT(nvarchar(max), lookup.ContractNumber), N'', '')
                       WITHIN GROUP (ORDER BY lookup.Id) AS ContractNumber
            FROM [ykb].[YkbServicesRequestServiceTypes] selected
            JOIN dbo.ServiceType lookup ON lookup.Id = selected.ServiceTypeId
            WHERE selected.[YkbServicesRequestId] = sr.Id
        ) st
    LEFT JOIN dbo.Users              tech WITH (NOLOCK) ON tech.Id = wf.ApproverTechnicianId
    WHERE wf.IsDeleted = 0
';

    IF @RequestNo IS NOT NULL
        SET @sql += N'  AND wf.RequestNo LIKE ''%'' + @RequestNo + ''%'' ';
    IF @CreatedFrom IS NOT NULL
        SET @sql += N'  AND wf.CreatedDate >= @CreatedFrom ';
    IF @CreatedTo IS NOT NULL
        SET @sql += N'  AND wf.CreatedDate <  @CreatedTo ';
    IF @ServicesDateFrom IS NOT NULL
        SET @sql += N'  AND sr.ServicesDate >= @ServicesDateFrom ';
    IF @ServicesDateTo IS NOT NULL
        SET @sql += N'  AND sr.ServicesDate <  @ServicesDateTo ';
    IF @CustomerId IS NOT NULL
        SET @sql += N'  AND sr.CustomerId = @CustomerId ';
    IF @CustomerName IS NOT NULL
        SET @sql += N'  AND EXISTS (SELECT 1 FROM dbo.Customers c WITH (NOLOCK)
                                     WHERE c.Id = sr.CustomerId
                                       AND c.SubscriberCompany LIKE ''%'' + @CustomerName + ''%'') ';
    IF @TechnicianId IS NOT NULL
        SET @sql += N'  AND wf.ApproverTechnicianId = @TechnicianId ';
    IF @ServiceTypeId IS NOT NULL
        SET @sql += N'  AND EXISTS (SELECT 1 FROM [ykb].[YkbServicesRequestServiceTypes] selected WHERE selected.[YkbServicesRequestId] = sr.Id AND selected.ServiceTypeId = @ServiceTypeId) ';
    IF @StepCode IS NOT NULL
        SET @sql += N'  AND ws.Code = @StepCode ';
    IF @IsAgreement IS NOT NULL
        SET @sql += N'  AND wf.IsAgreement = @IsAgreement ';
    IF @IsLocationValid IS NOT NULL
        SET @sql += N'  AND wf.IsLocationValid = @IsLocationValid ';
    IF @Search IS NOT NULL
        SET @sql += N'  AND (
               wf.RequestNo LIKE ''%'' + @Search + ''%'' OR
               wf.RequestTitle LIKE ''%'' + @Search + ''%'' OR
               EXISTS (
                   SELECT 1 FROM dbo.Customers cc WITH (NOLOCK)
                   WHERE cc.Id = sr.CustomerId
                     AND (cc.SubscriberCompany LIKE ''%'' + @Search + ''%'' OR
                          cc.ContactName1      LIKE ''%'' + @Search + ''%'')
               )
           ) ';
    IF @WorkFlowStatusesCsv IS NOT NULL
        SET @sql += N'  AND wf.WorkFlowStatus IN (SELECT TRY_CAST([value] AS int) FROM STRING_SPLIT(@WorkFlowStatusesCsv,'','')) ';
    IF @TechStatusesCsv IS NOT NULL
        SET @sql += N'  AND EXISTS (
               SELECT 1 FROM ykb.YkbTechnicalService ts WITH (NOLOCK)
               WHERE ts.RequestNo = wf.RequestNo
                 AND ts.ServicesStatus IN (SELECT TRY_CAST([value] AS int) FROM STRING_SPLIT(@TechStatusesCsv,'',''))
           ) ';
    IF @PricingStatusesCsv IS NOT NULL
        SET @sql += N'  AND EXISTS (
               SELECT 1 FROM ykb.YkbPricing pr WITH (NOLOCK)
               WHERE pr.RequestNo = wf.RequestNo
                 AND pr.Status IN (SELECT TRY_CAST([value] AS int) FROM STRING_SPLIT(@PricingStatusesCsv,'',''))
           ) ';
    IF @FinalStatusesCsv IS NOT NULL
        SET @sql += N'  AND EXISTS (
               SELECT 1 FROM ykb.YkbFinalApproval fa WITH (NOLOCK)
               WHERE fa.RequestNo = wf.RequestNo
                 AND fa.Status IN (SELECT TRY_CAST([value] AS int) FROM STRING_SPLIT(@FinalStatusesCsv,'',''))
           ) ';
    IF @ProductId IS NOT NULL
        SET @sql += N'  AND EXISTS (
               SELECT 1 FROM ykb.YkbServicesRequestProduct l WITH (NOLOCK)
               WHERE l.RequestNo = wf.RequestNo AND l.ProductId = @ProductId
           ) ';
    IF @ProductCode IS NOT NULL
        SET @sql += N'  AND EXISTS (
               SELECT 1
               FROM ykb.YkbServicesRequestProduct l WITH (NOLOCK)
               JOIN dbo.Product p WITH (NOLOCK) ON p.Id = l.ProductId
               WHERE l.RequestNo = wf.RequestNo AND p.ProductCode LIKE ''%'' + @ProductCode + ''%''
           ) ';
    IF @HasImages IS NOT NULL
        SET @sql += N'
          AND (
               (@HasImages = 1 AND EXISTS (
                    SELECT 1
                    FROM ykb.YkbTechnicalService t WITH (NOLOCK)
                    LEFT JOIN ykb.YkbTechnicalServiceImage si WITH (NOLOCK) ON si.YkbTechnicalServiceId = t.Id
                    LEFT JOIN ykb.YkbTechnicalServiceFormImage fi WITH (NOLOCK) ON fi.YkbTechnicalServiceId = t.Id
                    WHERE t.RequestNo = wf.RequestNo AND (si.Id IS NOT NULL OR fi.Id IS NOT NULL)
               ))
               OR
               (@HasImages = 0 AND NOT EXISTS (
                    SELECT 1
                    FROM ykb.YkbTechnicalService t WITH (NOLOCK)
                    LEFT JOIN ykb.YkbTechnicalServiceImage si WITH (NOLOCK) ON si.YkbTechnicalServiceId = t.Id
                    LEFT JOIN ykb.YkbTechnicalServiceFormImage fi WITH (NOLOCK) ON fi.YkbTechnicalServiceId = t.Id
                    WHERE t.RequestNo = wf.RequestNo AND (si.Id IS NOT NULL OR fi.Id IS NOT NULL)
               ))
          )
        ';

    SET @sql += N'
),
totals AS (
    SELECT
        l.RequestNo,
        SUM(CASE
                WHEN l.IsPriceCaptured = 1
                    THEN COALESCE(l.CapturedTotal, COALESCE(l.CapturedUnitPrice, 0) * l.Quantity)
                ELSE l.Quantity * p.Price
            END) AS Subtotal,
        MAX(CASE
                WHEN l.IsPriceCaptured = 1
                    THEN COALESCE(l.CapturedCurrency, p.PriceCurrency)
                ELSE p.PriceCurrency
            END) AS Currency
    FROM ykb.YkbServicesRequestProduct l WITH (NOLOCK)
    JOIN base b ON b.RequestNo = l.RequestNo
    LEFT JOIN dbo.Product p WITH (NOLOCK) ON p.Id = l.ProductId
    GROUP BY l.RequestNo
)
SELECT
    COUNT(1) OVER()               AS TotalCount,
    b.RequestNo,
    b.RequestTitle                AS Title,
    b.WorkFlowStatus,
    b.StepCode,
    b.CreatedDate,
    b.CustomerId,
    NULL        AS CustomerName,
    NULL        AS City,
    NULL        AS District,
    b.ServicesDate,
    b.ServiceTypeId,
    b.ServiceTypeName,
    b.TechnicianId,
    b.TechnicianName AS Name,
    COALESCE(t.Subtotal, 0)       AS Subtotal,
    COALESCE(t.Currency, ''TRY'') AS Currency
FROM base b
LEFT JOIN totals t ON t.RequestNo = b.RequestNo
' + @orderBy + N'
OFFSET (@Page - 1) * @PageSize ROWS
FETCH NEXT @PageSize ROWS ONLY;
';

    EXEC sp_executesql
        @sql,
        N'@Page int, @PageSize int, @SortBy nvarchar(32),
          @CreatedFrom datetimeoffset, @CreatedTo datetimeoffset,
          @ServicesDateFrom datetimeoffset, @ServicesDateTo datetimeoffset,
          @Search nvarchar(200), @RequestNo nvarchar(100),
          @CustomerId bigint, @CustomerName nvarchar(200), @TechnicianId bigint, @ServiceTypeId bigint, @StepCode nvarchar(50),
          @IsAgreement bit, @IsLocationValid bit, @HasImages bit,
          @WorkFlowStatusesCsv nvarchar(max), @TechStatusesCsv nvarchar(max), @PricingStatusesCsv nvarchar(max), @FinalStatusesCsv nvarchar(max),
          @ProductId bigint, @ProductCode nvarchar(100)',
        @Page, @PageSize, @SortBy,
        @CreatedFrom, @CreatedTo,
        @ServicesDateFrom, @ServicesDateTo,
        @Search, @RequestNo,
        @CustomerId, @CustomerName, @TechnicianId, @ServiceTypeId, @StepCode,
        @IsAgreement, @IsLocationValid, @HasImages,
        @WorkFlowStatusesCsv, @TechStatusesCsv, @PricingStatusesCsv, @FinalStatusesCsv,
        @ProductId, @ProductCode;
END
