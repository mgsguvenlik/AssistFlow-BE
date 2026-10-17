using Core.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebAPI.Authorization;

/// <summary>Localizes automatic binding errors without changing other modules' MVC behavior.</summary>
public sealed class CollectionValidationAttribute : ActionFilterAttribute
{
    public CollectionValidationAttribute() => Order = -2001;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var errors = context.ModelState.Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(x => x.Key, _ => new[] { "Girilen değer geçersiz. Lütfen alanın biçimini ve izin verilen aralığını kontrol edin." });
        context.Result = new BadRequestObjectResult(ResponseModel.Fail(
            "Gönderilen bilgiler geçersiz. Lütfen alanları kontrol edin.", validation: errors));
    }
}
