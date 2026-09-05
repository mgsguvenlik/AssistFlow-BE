CREATE OR ALTER PROCEDURE [dbo].[usp_ReportSearch_Lines_MultiServiceTypes]
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

    ;WITH base AS (
        SELECT
            wf.RequestNo,
            wf.RequestTitle,
            wf.WorkFlowStatus,
            wf.CreatedDate,
            ws.Code AS StepCode,
            sr.CustomerId,
            sr.ServicesDate,
            sr.ServiceTypeId,
            sr.OracleNo AS ServiceOracleNo,
            st.Name AS ServiceTypeName,
            st.ContractNumber,
            wf.ApproverTechnicianId AS TechnicianId,
            tech.Name AS TechnicianName,
            fa0.DiscountPercent AS DiscountPercent
        FROM dbo.WorkFlows wf WITH (NOLOCK)
        JOIN dbo.ServicesRequests sr WITH (NOLOCK)
            ON sr.RequestNo = wf.RequestNo
        LEFT JOIN dbo.WorkFlowSteps ws WITH (NOLOCK)
            ON ws.Id = wf.CurrentStepId
        OUTER APPLY (
            SELECT STRING_AGG(CONVERT(nvarchar(max), lookup.Name), N', ')
                       WITHIN GROUP (ORDER BY lookup.Id) AS Name,
                   STRING_AGG(CONVERT(nvarchar(max), lookup.ContractNumber), N', ')
                       WITHIN GROUP (ORDER BY lookup.Id) AS ContractNumber
            FROM [dbo].[ServicesRequestServiceTypes] selected
            JOIN dbo.ServiceType lookup ON lookup.Id = selected.ServiceTypeId
            WHERE selected.[ServicesRequestId] = sr.Id
        ) st
        LEFT JOIN dbo.Users tech WITH (NOLOCK)
            ON tech.Id = wf.ApproverTechnicianId
        LEFT JOIN dbo.FinalApprovals fa0 WITH (NOLOCK)
            ON fa0.RequestNo = wf.RequestNo
        WHERE
            wf.IsDeleted = 0
            AND (@ServiceTypeId IS NULL OR EXISTS (SELECT 1 FROM [dbo].[ServicesRequestServiceTypes] selected WHERE selected.[ServicesRequestId] = sr.Id AND selected.ServiceTypeId = @ServiceTypeId))
            AND (
                @FinalStatusesCsv IS NULL
                OR EXISTS (
                    SELECT 1
                    FROM dbo.FinalApprovals fa WITH (NOLOCK)
                    WHERE fa.RequestNo = wf.RequestNo
                      AND fa.Status IN (
                          SELECT TRY_CAST([value] AS int)
                          FROM STRING_SPLIT(@FinalStatusesCsv, ',')
                      )
                )
            )
            AND (
                @HasImages IS NULL
                OR (
                    @HasImages = 1
                    AND EXISTS (
                        SELECT 1
                        FROM dbo.TechnicalServices t WITH (NOLOCK)
                        LEFT JOIN dbo.TechnicalServiceImages si WITH (NOLOCK)
                            ON si.TechnicalServiceId = t.Id
                        LEFT JOIN dbo.TechnicalServiceFormImages fi WITH (NOLOCK)
                            ON fi.TechnicalServiceId = t.Id
                        WHERE t.RequestNo = wf.RequestNo
                          AND (si.Id IS NOT NULL OR fi.Id IS NOT NULL)
                    )
                )
                OR (
                    @HasImages = 0
                    AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.TechnicalServices t WITH (NOLOCK)
                        LEFT JOIN dbo.TechnicalServiceImages si WITH (NOLOCK)
                            ON si.TechnicalServiceId = t.Id
                        LEFT JOIN dbo.TechnicalServiceFormImages fi WITH (NOLOCK)
                            ON fi.TechnicalServiceId = t.Id
                        WHERE t.RequestNo = wf.RequestNo
                          AND (si.Id IS NOT NULL OR fi.Id IS NOT NULL)
                    )
                )
            )
    ),
    lines AS (
        SELECT
            b.*,
            l.Id AS LineId,
            l.ProductId,
            l.Quantity,
            l.IsPriceCaptured,
            l.CapturedUnitPrice,
            l.CapturedTotal,
            l.CapturedCurrency,
            p.ProductCode,
            p.OracleProductCode AS ProductOracleCode,
            p.Description AS ProductDefinition,
            p.Price AS ProductPrice,
            p.PriceCurrency AS ProductCurrency,
            pt.[Type] AS CostType,
            c.City,
            c.SubscriberCompany AS CustomerName,
            c.SubscriberCode AS LocationCode,
            c.InstallationDate,
            ts.StartTime AS ServiceDate,
            cgpp.Price AS GroupPrice,
            cgpp.CurrencyCode AS GroupCurrency,
            cpp.Price AS CustPrice,
            cpp.CurrencyCode AS CustCurrency
        FROM base b
        JOIN dbo.ServicesRequestProducts l WITH (NOLOCK)
            ON l.RequestNo = b.RequestNo
        LEFT JOIN dbo.Product p WITH (NOLOCK)
            ON p.Id = l.ProductId
        LEFT JOIN dbo.ProductType pt WITH (NOLOCK)
            ON pt.Id = p.ProductTypeId
        LEFT JOIN dbo.Customers c WITH (NOLOCK)
            ON c.Id = b.CustomerId
        LEFT JOIN dbo.TechnicalServices ts WITH (NOLOCK)
            ON ts.RequestNo = b.RequestNo
        LEFT JOIN dbo.CustomerGroupProductPrices cgpp WITH (NOLOCK)
            ON cgpp.CustomerGroupId = c.CustomerGroupId
           AND cgpp.ProductId = l.ProductId
           AND cgpp.IsDeleted = 0
        LEFT JOIN dbo.CustomerProductPrices cpp WITH (NOLOCK)
            ON cpp.CustomerId = c.Id
           AND cpp.ProductId = l.ProductId
           AND cpp.IsDeleted = 0
        WHERE
            (@ProductId IS NULL OR l.ProductId = @ProductId)
            AND (
                @ProductCode IS NULL
                OR p.ProductCode LIKE N'%' + @ProductCode + N'%'
            )
    ),
    lines_calc AS (
        SELECT
            l.*,
            CAST(
                COALESCE(l.GroupPrice, l.CustPrice, l.ProductPrice, 0)
                AS decimal(18, 2)
            ) AS EffectiveBaseUnitPrice,
            CAST(
                COALESCE(l.GroupCurrency, l.CustCurrency, l.ProductCurrency)
                AS nvarchar(10)
            ) AS EffectiveBaseCurrency
        FROM lines l
    ),
    lines_final AS (
        SELECT
            lc.*,
            CASE
                WHEN lc.IsPriceCaptured = 1
                  OR lc.CapturedUnitPrice IS NOT NULL
                  OR lc.CapturedTotal IS NOT NULL
                  OR lc.CapturedCurrency IS NOT NULL
                THEN 1
                ELSE 0
            END AS CapturedPresent,
            CAST(
                CASE
                    WHEN lc.IsPriceCaptured = 1
                      OR lc.CapturedUnitPrice IS NOT NULL
                      OR lc.CapturedTotal IS NOT NULL
                      OR lc.CapturedCurrency IS NOT NULL
                    THEN COALESCE(lc.CapturedCurrency, lc.EffectiveBaseCurrency)
                    ELSE lc.EffectiveBaseCurrency
                END AS nvarchar(10)
            ) AS LineCurrency,
            CAST(
                CASE
                    WHEN lc.IsPriceCaptured = 1
                      OR lc.CapturedUnitPrice IS NOT NULL
                      OR lc.CapturedTotal IS NOT NULL
                      OR lc.CapturedCurrency IS NOT NULL
                    THEN COALESCE(
                        lc.CapturedUnitPrice,
                        CASE
                            WHEN lc.CapturedTotal IS NOT NULL AND lc.Quantity > 0
                            THEN lc.CapturedTotal / NULLIF(lc.Quantity, 0)
                        END,
                        lc.EffectiveBaseUnitPrice
                    )
                    ELSE lc.EffectiveBaseUnitPrice
                END AS decimal(18, 2)
            ) AS LineUnitPrice,
            CAST(
                CASE
                    WHEN lc.IsPriceCaptured = 1
                      OR lc.CapturedUnitPrice IS NOT NULL
                      OR lc.CapturedTotal IS NOT NULL
                      OR lc.CapturedCurrency IS NOT NULL
                    THEN COALESCE(
                        lc.CapturedTotal,
                        COALESCE(lc.CapturedUnitPrice, 0) * lc.Quantity,
                        lc.EffectiveBaseUnitPrice * lc.Quantity
                    )
                    ELSE lc.EffectiveBaseUnitPrice * lc.Quantity
                END AS decimal(18, 2)
            ) AS LineTotal
        FROM lines_calc lc
    )
    SELECT
        COUNT(1) OVER() AS TotalCount,
        lf.RequestNo,
        lf.City,
        lf.CustomerName,
        lf.ProductCode,
        lf.LocationCode,
        lf.ProductOracleCode,
        lf.ProductDefinition,
        lf.ServiceDate,
        lf.ServiceOracleNo,
        lf.ServiceTypeName AS WorkOrder,
        lf.Quantity,
        CASE WHEN lf.LineCurrency = N'TRY' THEN lf.LineUnitPrice END AS LineUnitPriceTL,
        CASE WHEN lf.LineCurrency = N'TRY' THEN lf.LineTotal END AS LineTotalTL,
        CASE WHEN lf.LineCurrency = N'USD' THEN lf.LineUnitPrice END AS LineUnitPriceUSD,
        CASE WHEN lf.LineCurrency = N'USD' THEN lf.LineTotal END AS LineTotalUSD,
        CASE WHEN lf.LineCurrency = N'EUR' THEN lf.LineUnitPrice END AS LineUnitPriceEUR,
        CASE WHEN lf.LineCurrency = N'EUR' THEN lf.LineTotal END AS LineTotalEUR,
        CAST(N'' AS nvarchar(100)) AS GLCode,
        CAST(N'' AS nvarchar(500)) AS MGSDescription,
        lf.ContractNumber AS [Contract No],
        lf.CostType,
        CAST(N'' AS nvarchar(500)) AS [Description],
        lf.InstallationDate,
        lf.DiscountPercent
    FROM lines_final lf
    ORDER BY
        IIF(@SortBy = N'created_desc', lf.CreatedDate, NULL) DESC,
        IIF(@SortBy = N'created_asc', lf.CreatedDate, NULL) ASC,
        IIF(@SortBy = N'servicesdate_desc', lf.ServiceDate, NULL) DESC,
        IIF(@SortBy = N'servicesdate_asc', lf.ServiceDate, NULL) ASC,
        IIF(@SortBy = N'title_desc', lf.RequestTitle, NULL) DESC,
        IIF(@SortBy = N'title_asc', lf.RequestTitle, NULL) ASC,
        IIF(@SortBy = N'product_desc', lf.ProductCode, NULL) DESC,
        IIF(@SortBy = N'product_asc', lf.ProductCode, NULL) ASC,
        lf.CreatedDate DESC
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
