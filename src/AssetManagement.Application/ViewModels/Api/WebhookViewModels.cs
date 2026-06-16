using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class WebhookSubscriptionVm
    {
        public int Id { get; set; }

        public string EventType { get; set; }

        public string TargetUrl { get; set; }

        public bool IsActive { get; set; }

        public string CreatedByUserId { get; set; }
    }

    public class WebhookSubscriptionEditVm
    {
        [Required]
        [StringLength(100)]
        public string EventType { get; set; }

        [Required]
        [StringLength(500)]
        public string TargetUrl { get; set; }

        [StringLength(200)]
        public string Secret { get; set; }
    }
}
