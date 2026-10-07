using Core.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.TechnicalServiceImage;

public class TechnicalServiceImageUploadDto
{
    [Required, MaxLength(100)]
    public string RequestNo { get; set; } = string.Empty;

    [Range(1, 2)]
    public TechnicalServiceImageType Type { get; set; }

    [Required, MinLength(1)]
    public List<IFormFile> Files { get; set; } = new();
}
