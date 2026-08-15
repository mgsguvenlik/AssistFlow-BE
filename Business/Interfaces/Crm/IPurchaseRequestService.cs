using Core.Common;
using Model.Dtos.Crm.PurchaseAttachment;
using Model.Dtos.Crm.PurchaseRequest;
using Model.Dtos.Crm.PurchaseRequestAction;
using Model.Dtos.Crm.PurchaseRequestHistory;
using Model.Dtos.Crm.PurchaseRequestItem;
using Model.Dtos.Crm.PurchaseRequestStep;
using Model.Dtos.Crm.PurchaseRequestTask;

namespace Business.Interfaces.Crm
{
    public interface IPurchaseRequestService
    {
        // =====================================================
        // REQUEST
        // =====================================================

        Task<ResponseModel<PurchaseRequestGetDto>> CreateAsync(
            PurchaseRequestCreateDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<PurchaseRequestGetDto>> UpdateAsync(
            PurchaseRequestUpdateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Satın alma talebini fiziksel olarak silmez.
        /// Talebi Cancelled durumuna geçirir.
        /// </summary>
        Task<ResponseModel<bool>> CancelAsync(
            long id,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<PurchaseRequestDetailDto>> GetDetailAsync(
            long id,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<PaginatedList<PurchaseRequestGetDto>>> GetPagedAsync(
            QueryParams queryParams,
            CancellationToken cancellationToken = default);


        // =====================================================
        // ITEM
        // =====================================================

        Task<ResponseModel<PurchaseRequestItemGetDto>> AddItemAsync(
            long purchaseRequestId,
            PurchaseRequestItemCreateDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<PurchaseRequestItemGetDto>> UpdateItemAsync(
            PurchaseRequestItemUpdateDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<bool>> DeleteItemAsync(
            long itemId,
            CancellationToken cancellationToken = default);


        // =====================================================
        // ATTACHMENT
        // =====================================================

        Task<ResponseModel<PurchaseAttachmentGetDto>> AddAttachmentAsync(
            long purchaseRequestId,
            PurchaseAttachmentCreateDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<bool>> DeleteAttachmentAsync(
            long attachmentId,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<List<PurchaseAttachmentGetDto>>> GetAttachmentsAsync(
            long purchaseRequestId,
            CancellationToken cancellationToken = default);


        // =====================================================
        // PROCESS
        // =====================================================

        Task<ResponseModel<PurchaseRequestDetailDto>> ProcessActionAsync(
            PurchaseRequestProcessActionDto dto,
            CancellationToken cancellationToken = default);


        // =====================================================
        // ACTION
        // =====================================================

        Task<ResponseModel<List<PurchaseRequestActionGetDto>>> GetActionsAsync(
            long purchaseRequestId,
            CancellationToken cancellationToken = default);


        // =====================================================
        // HISTORY
        // =====================================================

        Task<ResponseModel<List<PurchaseRequestHistoryGetDto>>> GetHistoryAsync(
            long purchaseRequestId,
            CancellationToken cancellationToken = default);


        // =====================================================
        // TASK
        // =====================================================

        Task<ResponseModel<List<PurchaseRequestTaskGetDto>>> GetTasksAsync(
            long purchaseRequestId,
            CancellationToken cancellationToken = default);


        // =====================================================
        // STEP
        // =====================================================

        Task<ResponseModel<List<PurchaseRequestStepGetDto>>> GetStepsAsync(
            CancellationToken cancellationToken = default);
    }
}