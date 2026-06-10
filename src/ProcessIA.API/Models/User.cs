using Microsoft.AspNetCore.Identity;

namespace ProcessIA.API.Models;

public class User : IdentityUser
{
    public string? StripeCustomerId { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Inactive;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LegalProcess> Processes { get; set; } = [];
}

public enum SubscriptionStatus
{
    Inactive,
    Active,
    PastDue,
    Canceled
}
