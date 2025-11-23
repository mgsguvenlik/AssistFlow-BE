using Business.Interfaces;
using Core.Common;
using Core.Utilities.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace WebAPI.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public abstract class CrudControllerBase<TCreateDto, TUpdateDto, TGetDto, TKey> : ControllerBase
    {
        protected readonly ICrudService<TCreateDto, TUpdateDto, TGetDto, TKey> _service;
        protected readonly ILogger _logger;
        private ICustomerProductPriceService service;

        protected CrudControllerBase(
            ICrudService<TCreateDto, TUpdateDto, TGetDto, TKey> service,
            ILogger logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>GET /api/[controller]  -> Paged list</summary>
        [HttpGet]
        public virtual async Task<Microsoft.AspNetCore.Mvc.IActionResult> GetPaged([FromQuery] QueryParams q)
        {
            var resp = await _service.GetPagedAsync(q);
            return ToActionResult(resp);
        }

        /// <summary>GET /api/[controller]/{id} -> By Id</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(404)]
        public virtual async Task<IActionResult> GetById([FromRoute] TKey id)
        {
            var resp = await _service.GetByIdAsync(id);
            return ToActionResult(resp);
        }

        /// <summary>POST /api/[controller] -> Create</summary>
        [HttpPost]
        [ProducesResponseType(400)]
        public virtual async Task<IActionResult> Create([FromBody] TCreateDto dto)
        {
            var resp = await _service.CreateAsync(dto);

            // Eğer 201 ve DTO’da Id varsa, Location header için CreatedAtAction kullan
            if (resp.IsSuccess && resp.StatusCode == Core.Enums.StatusCode.Created && TryGetId(resp.Data, out var newId))
            {
                return CreatedAtAction(nameof(GetById), new { id = newId }, resp);
            }

            return ToActionResult(resp);
        }

        /// <summary>PUT /api/[controller]/{id} -> Update</summary>
        [HttpPost("update")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public virtual async Task<IActionResult> Update([FromBody] TUpdateDto dto)
        {
            var resp = await _service.UpdateAsync(dto);
            return ToActionResult(resp);
        }

        /// <summary>DELETE /api/[controller]/{id} -> Delete</summary>
        [HttpPost("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public virtual async Task<IActionResult> Delete([FromRoute] TKey id)
        {
            var resp = await _service.DeleteAsync(id);

            // 204 için gövdesiz dön
            if (resp.StatusCode == Core.Enums.StatusCode.NoContent && resp.IsSuccess)
                return NoContent();

            return ToActionResult(resp);
        }

        // ----------------- Helpers -----------------

        protected IActionResult ToActionResult(ResponseModel resp)
        {
            if (resp.StatusCode == Core.Enums.StatusCode.NoContent)
                return StatusCode((int)Core.Enums.StatusCode.NoContent);

            return StatusCode((int)resp.StatusCode, resp);
        }

        protected IActionResult ToActionResult<T>(ResponseModel<T> resp)
        {
            if (resp.StatusCode == Core.Enums.StatusCode.NoContent)
                return StatusCode((int)Core.Enums.StatusCode.NoContent);

            return StatusCode((int)resp.StatusCode, resp);
        }

        /// <summary>Body DTO’da Id varsa route id ile eşit mi kontrol eder.</summary>
        protected bool RouteIdMatchesBodyId<TDto>(TKey routeId, TDto dto, out IActionResult? result)
        {
            result = null;
            var prop = typeof(TDto).GetProperty(CommonConstants.Id, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return true; // Id yoksa kontrol etmeyelim

            var value = prop.GetValue(dto);
            if (value == null) return true;

            try
            {
                var bodyId = (TKey)Convert.ChangeType(value, typeof(TKey))!;
                if (!EqualityComparer<TKey>.Default.Equals(routeId, bodyId))
                {
                    result = BadRequest(new ResponseModel(false, Messages.RouteIdBodyIdMismatch, Core.Enums.StatusCode.BadRequest));
                    return false;
                }
            }
            catch
            {
                result = BadRequest(new ResponseModel(false, Messages.BodyIdTypeInvalid, Core.Enums.StatusCode.BadRequest));
                return false;
            }

            return true;
        }

        /// <summary>GetDto’dan Id’yi almaya çalışır (CreatedAtAction için).</summary>
        protected bool TryGetId<T>(T? obj, out object? id)
        {
            id = null;
            if (obj == null) return false;

            var prop = typeof(T).GetProperty(CommonConstants.Id, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return false;

            id = prop.GetValue(obj);
            return id != null;
        }
    }
}
