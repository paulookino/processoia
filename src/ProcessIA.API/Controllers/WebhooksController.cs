using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProcessIA.API.Models;
using Stripe;

namespace ProcessIA.API.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
public class WebhooksController(
    UserManager<User> userManager,
    IConfiguration config,
    ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"];
        var secret = config["Stripe:WebhookSecret"]!;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, secret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Invalid Stripe webhook signature: {Message}", ex.Message);
            return BadRequest();
        }

        switch (stripeEvent.Type)
        {
            case Events.CustomerSubscriptionCreated:
            case Events.CustomerSubscriptionUpdated:
                await HandleSubscriptionChange((Subscription)stripeEvent.Data.Object);
                break;

            case Events.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeleted((Subscription)stripeEvent.Data.Object);
                break;
        }

        return Ok();
    }

    private async Task HandleSubscriptionChange(Subscription subscription)
    {
        var user = await FindUserByCustomerId(subscription.CustomerId);
        if (user is null) return;

        user.SubscriptionStatus = subscription.Status switch
        {
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.Inactive
        };

        await userManager.UpdateAsync(user);
        logger.LogInformation("Subscription updated for {Email}: {Status}", user.Email, user.SubscriptionStatus);
    }

    private async Task HandleSubscriptionDeleted(Subscription subscription)
    {
        var user = await FindUserByCustomerId(subscription.CustomerId);
        if (user is null) return;

        user.SubscriptionStatus = SubscriptionStatus.Canceled;
        await userManager.UpdateAsync(user);
        logger.LogInformation("Subscription canceled for {Email}", user.Email);
    }

    private async Task<User?> FindUserByCustomerId(string customerId)
    {
        var users = await userManager.GetUsersInRoleAsync(string.Empty);
        return users.FirstOrDefault(u => u.StripeCustomerId == customerId)
            ?? (await userManager.GetUsersInRoleAsync("user"))
                .FirstOrDefault(u => u.StripeCustomerId == customerId);
    }
}
