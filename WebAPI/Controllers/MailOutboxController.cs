using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.MailOutbox;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class MailOutboxController
      : CrudControllerBase<MailOutboxCreateDto, MailOutboxUpdateDto, MailOutboxGetDto, long>
    {
        private readonly IMailOutboxService _mailOutboxService;

        public MailOutboxController(
            ICrudService<MailOutboxCreateDto, MailOutboxUpdateDto, MailOutboxGetDto, long> service,
            IMailOutboxService mailOutboxService,
            ILogger<MailOutboxController> logger)
            : base(service, logger)
        {
            _mailOutboxService = mailOutboxService;
        }

        /// <summary>
        /// Tek bir mail outbox kaydı için yeniden deneme tetikleme
        /// </summary>
        [HttpPost("retry/{id:long}")]
        public async Task<IActionResult> Retry(long id, CancellationToken ct)
        {
            var ok = await _mailOutboxService.RetryAsync(id, ct);
            if (!ok) return BadRequest(new { message = "Retry yapılamadı. Kayıt yok ya da geçersiz durum." });
            return Ok(new { success = true });
        }
    }
}
