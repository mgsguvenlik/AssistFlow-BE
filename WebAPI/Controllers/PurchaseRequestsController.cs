using Business.Interfaces.Crm;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Crm.PurchaseAttachment;
using Model.Dtos.Crm.PurchaseRequest;
using Model.Dtos.Crm.PurchaseRequestItem;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class PurchaseRequestsController : ControllerBase
    {
        private readonly IPurchaseRequestService _purchaseRequestService;

        public PurchaseRequestsController(
            IPurchaseRequestService purchaseRequestService)
        {
            _purchaseRequestService = purchaseRequestService;
        }


        // =====================================================
        // REQUEST
        // =====================================================

        /// <summary>
        /// Yeni satın alma talebi oluşturur.
        /// Talep Draft durumunda oluşturulur.
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> Create(
            [FromBody] PurchaseRequestCreateDto dto,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.CreateAsync(
                dto,
                cancellationToken);

            if (result.IsSuccess && result.Data != null)
            {
                return CreatedAtAction(
                    nameof(GetDetail),
                    new { id = result.Data.Id },
                    result);
            }

            return ToActionResult(result);
        }


        /// <summary>
        /// Satın alma talebinin ana bilgilerini günceller.
        /// </summary>
        [HttpPost("update/{id:long}")]
        public async Task<IActionResult> Update(
            [FromRoute] long id,
            [FromBody] PurchaseRequestUpdateDto dto,
            CancellationToken cancellationToken)
        {
            if (dto.Id != id)
            {
                return BadRequest(
                    ResponseModel.Fail(
                        "Route id ile body id eşleşmiyor.",
                        Core.Enums.StatusCode.BadRequest));
            }

            var result = await _purchaseRequestService.UpdateAsync(
                dto,
                cancellationToken);

            return ToActionResult(result);
        }


        /// <summary>
        /// Satın alma talebini fiziksel olarak silmez.
        /// Talebi Cancelled durumuna geçirir.
        /// </summary>
        [HttpPost("cancel/{id:long}")]
        public async Task<IActionResult> Cancel(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.CancelAsync(
                id,
                cancellationToken);

            return ToActionResult(result);
        }


        /// <summary>
        /// Satın alma talebi detayını getirir.
        /// </summary>
        [HttpGet("detail/{id:long}")]
        public async Task<IActionResult> GetDetail(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.GetDetailAsync(
                id,
                cancellationToken);

            return ToActionResult(result);
        }


        /// <summary>
        /// Satın alma taleplerini server-side pagination ile getirir.
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] QueryParams queryParams,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.GetPagedAsync(
                queryParams,
                cancellationToken);

            return ToActionResult(result);
        }


        // =====================================================
        // ITEM
        // =====================================================

        /// <summary>
        /// Satın alma talebine ürün/hizmet kalemi ekler.
        /// </summary>
        [HttpPost("{purchaseRequestId:long}/items/create")]
        public async Task<IActionResult> AddItem(
            [FromRoute] long purchaseRequestId,
            [FromBody] PurchaseRequestItemCreateDto dto,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.AddItemAsync(
                purchaseRequestId,
                dto,
                cancellationToken);

            return ToActionResult(result);
        }


        /// <summary>
        /// Satın alma talebi ürün/hizmet kalemini günceller.
        /// </summary>
        [HttpPost("items/update/{id:long}")]
        public async Task<IActionResult> UpdateItem(
            [FromRoute] long id,
            [FromBody] PurchaseRequestItemUpdateDto dto,
            CancellationToken cancellationToken)
        {
            if (dto.Id != id)
            {
                return BadRequest(
                    ResponseModel.Fail(
                        "Route id ile body id eşleşmiyor.",
                        Core.Enums.StatusCode.BadRequest));
            }

            var result = await _purchaseRequestService.UpdateItemAsync(
                dto,
                cancellationToken);

            return ToActionResult(result);
        }


        /// <summary>
        /// Satın alma talebinden ürün/hizmet kalemini siler.
        /// </summary>
        [HttpPost("items/delete/{id:long}")]
        public async Task<IActionResult> DeleteItem(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.DeleteItemAsync(
                id,
                cancellationToken);

            return ToActionResult(result);
        }


        // =====================================================
        // ATTACHMENT
        // =====================================================

        /// <summary>
        /// Storage'a yüklenmiş bir dosyanın
        /// PurchaseRequest attachment metadata kaydını oluşturur.
        /// </summary>
        [HttpPost("{purchaseRequestId:long}/attachments/create")]
        public async Task<IActionResult> AddAttachment(
            [FromRoute] long purchaseRequestId,
            [FromBody] PurchaseAttachmentCreateDto dto,
            CancellationToken cancellationToken)
        {
            /*
             * DTO içerisindeki PurchaseRequestId yerine
             * route üzerindeki id esas alınır.
             */
            dto.PurchaseRequestId = purchaseRequestId;

            var result = await _purchaseRequestService.AddAttachmentAsync(
                purchaseRequestId,
                dto,
                cancellationToken);

            return ToActionResult(result);
        }


        /// <summary>
        /// Satın alma talebine ait attachment kaydını
        /// ve storage üzerindeki dosyayı siler.
        /// </summary>
        [HttpPost("attachments/delete/{id:long}")]
        public async Task<IActionResult> DeleteAttachment(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.DeleteAttachmentAsync(
                id,
                cancellationToken);

            return ToActionResult(result);
        }


        /// <summary>
        /// Talebe ait dosyaları getirir.
        /// </summary>
        [HttpGet("{purchaseRequestId:long}/attachments")]
        public async Task<IActionResult> GetAttachments(
            [FromRoute] long purchaseRequestId,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.GetAttachmentsAsync(
                purchaseRequestId,
                cancellationToken);

            return ToActionResult(result);
        }


        // =====================================================
        // WORKFLOW PROCESS
        // =====================================================

        /// <summary>
        /// Kullanıcının mevcut satın alma workflow adımında
        /// seçtiği aksiyonu çalıştırır.
        ///
        /// Örn:
        /// SUBMIT
        /// APPROVE
        /// RESEARCH_COMPLETED
        /// PROCUREMENT_COMPLETED
        /// REJECT
        /// vb.
        /// </summary>
        [HttpPost("process-action")]
        public async Task<IActionResult> ProcessAction(
            [FromBody] PurchaseRequestProcessActionDto dto,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.ProcessActionAsync(
                dto,
                cancellationToken);

            return ToActionResult(result);
        }


        // =====================================================
        // ACTION
        // =====================================================

        /// <summary>
        /// Mevcut kullanıcının ilgili talebin aktif step'inde
        /// kullanabileceği aksiyonları getirir.
        /// </summary>
        [HttpGet("{purchaseRequestId:long}/actions")]
        public async Task<IActionResult> GetActions(
            [FromRoute] long purchaseRequestId,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.GetActionsAsync(
                purchaseRequestId,
                cancellationToken);

            return ToActionResult(result);
        }


        // =====================================================
        // HISTORY
        // =====================================================

        /// <summary>
        /// Satın alma talebinin workflow geçmişini getirir.
        /// </summary>
        [HttpGet("{purchaseRequestId:long}/history")]
        public async Task<IActionResult> GetHistory(
            [FromRoute] long purchaseRequestId,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.GetHistoryAsync(
                purchaseRequestId,
                cancellationToken);

            return ToActionResult(result);
        }


        // =====================================================
        // TASK
        // =====================================================

        /// <summary>
        /// Satın alma talebine ait workflow task kayıtlarını getirir.
        /// </summary>
        [HttpGet("{purchaseRequestId:long}/tasks")]
        public async Task<IActionResult> GetTasks(
            [FromRoute] long purchaseRequestId,
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.GetTasksAsync(
                purchaseRequestId,
                cancellationToken);

            return ToActionResult(result);
        }


        // =====================================================
        // STEP
        // =====================================================

        /// <summary>
        /// Aktif satın alma workflow step tanımlarını getirir.
        /// </summary>
        [HttpGet("steps")]
        public async Task<IActionResult> GetSteps(
            CancellationToken cancellationToken)
        {
            var result = await _purchaseRequestService.GetStepsAsync(
                cancellationToken);

            return ToActionResult(result);
        }


        // =====================================================
        // RESPONSE HELPERS
        // =====================================================

        private IActionResult ToActionResult(ResponseModel response)
        {
            if (response.StatusCode == Core.Enums.StatusCode.NoContent)
            {
                return StatusCode(
                    (int)Core.Enums.StatusCode.NoContent);
            }

            return StatusCode(
                (int)response.StatusCode,
                response);
        }


        private IActionResult ToActionResult<T>(
            ResponseModel<T> response)
        {
            if (response.StatusCode == Core.Enums.StatusCode.NoContent)
            {
                return StatusCode(
                    (int)Core.Enums.StatusCode.NoContent);
            }

            return StatusCode(
                (int)response.StatusCode,
                response);
        }
    }
}