using Core.Common;
using Core.Enums;
using System.ComponentModel.DataAnnotations;
namespace Model.Dtos.WorkFlowDtos.WorkFlowSlaSetting;
public class WorkFlowSlaSettingQueryParams : QueryParams
{
    [EnumDataType(typeof(WorkFlowCustomerType))]
    public WorkFlowCustomerType? CustomerType { get; set; }
    [EnumDataType(typeof(WorkFlowPriority))]
    public WorkFlowPriority? Priority { get; set; }
    public bool? IsActive { get; set; }
    public bool? HasNotificationEmails { get; set; }
}
