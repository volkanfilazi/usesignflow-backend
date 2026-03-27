namespace DynamicFormBuilder.Services
{
    using DynamicFormBuilder.Models;
    using DynamicFormBuilder.Services.Billing;
    using DynamicFormBuilder.Models.Billing;
    using DynamicFormBuilder.Repositories.Auth;

    public class AuthService
    {
        private readonly IAuthRepository _repo;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IBillingService _billingService;

        public AuthService(
            IAuthRepository repo, 
            ISubscriptionService subscriptionService,
            IBillingService billingService)
        {
            _repo = repo;
            _subscriptionService = subscriptionService;
            _billingService = billingService;
        }

        public async Task<DeleteAccountResult> SoftDeleteUserAsync(string userId, DeleteAccountRequest request)
        {
            var user = await _repo.GetByIdAsync(userId);

            if (user == null)
                return DeleteAccountResult.UserNotFound;

            if (user.IsDeleted)
                return DeleteAccountResult.AlreadyDeleted;
            
            var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!isPasswordValid)
                return DeleteAccountResult.InvalidPassword;

            user.IsDeleted = true;
            user.DeletedAtUtc = DateTime.UtcNow;
            user.DeleteReason = request.Reason;
            user.UpdatedAtUtc = DateTime.UtcNow;

            user.RefreshTokens.Clear();
            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.EmailVerificationTokenHash = null;
            user.EmailVerificationTokenExpiresAtUtc = null;
            user.EmailVerified = false;
            user.Email = $"deleted_{user.Id}@deleted.local";
            user.FullName = "Deleted User";
            user.IsAnonymized = true;

            await _repo.UpdateAsync(user);

            return DeleteAccountResult.Success;
        }

        public async Task<DeleteAccountResult> DeleteAccountAsync(string userId, DeleteAccountRequest request)
        {
            var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);

            if (subscription.PlanCode != PlanCode.Free &&
                !string.IsNullOrWhiteSpace(subscription.LemonSubscriptionId) &&
                subscription.Status == SubscriptionStatus.Active)
            {
                await _billingService.CancelRenewalAsync(subscription.LemonSubscriptionId);
            }

            return await this.SoftDeleteUserAsync(userId, request);
        }
    }
}