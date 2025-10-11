using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Models;

public class IyzicoThreeDSCallbackModel
{
    [FromForm(Name = "status")]
    public string? Status { get; set; }

    [FromForm(Name = "mdStatus")]
    public string? MdStatus { get; set; }

    [FromForm(Name = "paymentId")]
    public string? PaymentId { get; set; }

    [FromForm(Name = "conversationId")]
    public string? ConversationId { get; set; }

    [FromForm(Name = "conversationData")]
    public string? ConversationData { get; set; }

    [FromForm(Name = "errorMessage")]
    public string? ErrorMessage { get; set; }
}
