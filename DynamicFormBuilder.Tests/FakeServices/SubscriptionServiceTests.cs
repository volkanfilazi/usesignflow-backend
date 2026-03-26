using DynamicFormBuilder.Models.Billing;
using DynamicFormBuilder.Repositories.Billing;
using DynamicFormBuilder.Services.Billing;
using DynamicFormBuilder.Tests.Fakes;
using FluentAssertions;
using Moq;
using Xunit;

namespace DynamicFormBuilder.Tests.FakeServices;

public class SubscriptionServiceTests
{
    [Fact]
    public async Task GetOrCreateForUserAsync_WhenSubscriptionDoesNotExist_ShouldCreateFreeSubscriptionWithOneMonthPeriod()
    {
        var userId = "user-1";
        var now = new DateTime(2026, 3, 10, 14, 0, 0, DateTimeKind.Utc);

        var clock = new FakeClock
        {
            UtcNow = now
        };

        var repository = new Mock<ISubscriptionRepository>();

        repository
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync((UserSubscription?)null);

        repository
            .Setup(x => x.CreateAsync(It.IsAny<UserSubscription>()))
            .Returns(Task.CompletedTask);

        var service = new SubscriptionService(clock, repository.Object);

        var result = await service.GetOrCreateForUserAsync(userId);

        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.PlanCode.Should().Be(PlanCode.Free);
        result.Status.Should().Be(SubscriptionStatus.Active);
        result.CurrentPeriodStartUtc.Should().Be(now);
        result.CurrentPeriodEndUtc.Should().Be(now.AddMonths(1));
        result.CancelAtPeriodEnd.Should().BeFalse();

        repository.Verify(x => x.CreateAsync(It.Is<UserSubscription>(s =>
            s.UserId == userId &&
            s.PlanCode == PlanCode.Free &&
            s.Status == SubscriptionStatus.Active &&
            s.CurrentPeriodStartUtc == now &&
            s.CurrentPeriodEndUtc == now.AddMonths(1) &&
            s.CancelAtPeriodEnd == false
        )), Times.Once);
    }

    /*
     * old period: 10 March 2026 14:00 → 10 April 2026 14:00
     * now: 12 April 2026 10:00
     * new period: 10 April 2026 14:00 → 10 May 2026 14:00
     * **/
    [Fact]
    public async Task GetOrCreateForUserAsync_WhenFreeSubscriptionExpired_ShouldAdvanceToNextPeriod()
    {
        // Arrange
        var userId = "user-1";

        var clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 4, 12, 10, 0, 0, DateTimeKind.Utc)
        };

        var existingSubscription = new UserSubscription
        {
            UserId = userId,
            PlanCode = PlanCode.Free,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStartUtc = new DateTime(2026, 3, 10, 14, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEndUtc = new DateTime(2026, 4, 10, 14, 0, 0, DateTimeKind.Utc),
            CancelAtPeriodEnd = false
        };

        var repository = new Mock<ISubscriptionRepository>();

        repository
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(existingSubscription);

        repository
            .Setup(x => x.UpsertByUserIdAsync(It.IsAny<UserSubscription>()))
            .Returns(Task.CompletedTask);

        var service = new SubscriptionService(clock, repository.Object);

        // Act
        var result = await service.GetOrCreateForUserAsync(userId);

        // Assert
        result.CurrentPeriodStartUtc.Should().Be(new DateTime(2026, 4, 10, 14, 0, 0, DateTimeKind.Utc));
        result.CurrentPeriodEndUtc.Should().Be(new DateTime(2026, 5, 10, 14, 0, 0, DateTimeKind.Utc));

        repository.Verify(x => x.UpsertByUserIdAsync(It.Is<UserSubscription>(s =>
            s.UserId == userId &&
            s.CurrentPeriodStartUtc == new DateTime(2026, 4, 10, 14, 0, 0, DateTimeKind.Utc) &&
            s.CurrentPeriodEndUtc == new DateTime(2026, 5, 10, 14, 0, 0, DateTimeKind.Utc)
        )), Times.Once);
    }

    /*
     * user skipped multiple billing cycles
     * system must catch up to the current billing period
     * and not stop at only one month
     */
    [Fact]
    public async Task GetOrCreateForUserAsync_WhenFreeSubscriptionExpiredForMultipleMonths_ShouldAdvanceUntilCurrentPeriod()
    {
        // Arrange
        var userId = "user-1";

        var clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 5, 25, 9, 0, 0, DateTimeKind.Utc)
        };

        var existingSubscription = new UserSubscription
        {
            UserId = userId,
            PlanCode = PlanCode.Free,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStartUtc = new DateTime(2026, 1, 10, 14, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEndUtc = new DateTime(2026, 2, 10, 14, 0, 0, DateTimeKind.Utc),
            CancelAtPeriodEnd = false
        };

        var repository = new Mock<ISubscriptionRepository>();

        repository
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(existingSubscription);

        repository
            .Setup(x => x.UpsertByUserIdAsync(It.IsAny<UserSubscription>()))
            .Returns(Task.CompletedTask);

        var service = new SubscriptionService(clock, repository.Object);

        // Act
        var result = await service.GetOrCreateForUserAsync(userId);

        // Assert
        result.CurrentPeriodStartUtc.Should().Be(new DateTime(2026, 5, 10, 14, 0, 0, DateTimeKind.Utc));
        result.CurrentPeriodEndUtc.Should().Be(new DateTime(2026, 6, 10, 14, 0, 0, DateTimeKind.Utc));
    }


    /*
     * paid user
     * old period: 10 March 2026 14:00 → 10 April 2026 14:00
     * now: 15 April 2026 10:00 (expired)
     * expected:
     * period should NOT change
     * because renew is handled by payment provider (Lemon)
     */
    [Fact]
    public async Task GetOrCreateForUserAsync_WhenPaidSubscriptionExpired_ShouldNotAdvancePeriod()
    {
        var userId = "user-1";

        var clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        var existingSubscription = new UserSubscription
        {
            UserId = userId,
            PlanCode = PlanCode.Pro,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStartUtc = new DateTime(2026, 3, 10, 14, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEndUtc = new DateTime(2026, 4, 10, 14, 0, 0, DateTimeKind.Utc),
            CancelAtPeriodEnd = false
        };

        var repository = new Mock<ISubscriptionRepository>();

        repository
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(existingSubscription);

        var service = new SubscriptionService(clock, repository.Object);

        var result = await service.GetOrCreateForUserAsync(userId);

        result.CurrentPeriodStartUtc.Should().Be(new DateTime(2026, 3, 10, 14, 0, 0, DateTimeKind.Utc));
        result.CurrentPeriodEndUtc.Should().Be(new DateTime(2026, 4, 10, 14, 0, 0, DateTimeKind.Utc));

        repository.Verify(x => x.UpsertByUserIdAsync(It.IsAny<UserSubscription>()), Times.Never);
    }

    /*
     * old period: 10 March 2026 → 10 April 2026
     * payment received at: 10 April 2026
     *
     * expected:
     * new period: 10 April 2026 → 10 May 2026
     */
    [Fact]
    public async Task HandlePaidSubscriptionRenewalAsync_WhenPaymentReceived_ShouldUpdatePeriodCorrectly()
    {
        var userId = "user-1";

        var existingSubscription = new UserSubscription
        {
            UserId = userId,
            PlanCode = PlanCode.Pro,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStartUtc = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEndUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        var repository = new Mock<ISubscriptionRepository>();

        repository
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(existingSubscription);

        repository
            .Setup(x => x.UpsertByUserIdAsync(It.IsAny<UserSubscription>()))
            .Returns(Task.CompletedTask);

        var clock = new FakeClock { UtcNow = DateTime.UtcNow };

        var service = new SubscriptionService(clock, repository.Object);

        var newStart = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var newEnd = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        await service.HandlePaidSubscriptionRenewalAsync(userId, newStart, newEnd);

        // Assert
        repository.Verify(x => x.UpsertByUserIdAsync(It.Is<UserSubscription>(s =>
            s.UserId == userId &&
            s.CurrentPeriodStartUtc == newStart &&
            s.CurrentPeriodEndUtc == newEnd &&
            s.Status == SubscriptionStatus.Active
        )), Times.Once);
    }
}