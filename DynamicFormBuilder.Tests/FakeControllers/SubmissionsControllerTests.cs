using DynamicFormBuilder.Controllers;
using DynamicFormBuilder.Models.Submission;
using DynamicFormBuilder.Repositories.Auth;
using DynamicFormBuilder.Repositories.Billing;
using DynamicFormBuilder.Repositories.Form;
using DynamicFormBuilder.Repositories.Submission;
using DynamicFormBuilder.Services;
using DynamicFormBuilder.Tests.Factories;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Security.Claims;
using Xunit;

namespace DynamicFormBuilder.Tests.Controllers;

public class SubmissionsControllerTests
{
    private readonly Mock<IFormRepository> _formRepo = new();
    private readonly Mock<ISubmissionPdfFactory> _submissionPdfFactory = new();
    private readonly Mock<ISubmissionSettingsRepository> _submissionSettingsRepository = new();
    private readonly Mock<IFormSubmissionRepository> _submissionRepo = new();
    private readonly Mock<ISubmissionAccessTokenRepository> _submissionAccessTokenRepo = new();
    private readonly Mock<IAuthRepository> _authRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPdfService> _pdfService = new();
    private readonly IConfiguration _configuration;

    public SubmissionsControllerTests()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["App:FrontendBaseUrl"] = "http://localhost:4200"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues!)
            .Build();
    }

    private FormSubmissionsController CreateController()
    {
        return new FormSubmissionsController(
            formRepo: _formRepo.Object,
            submissionPdfFactory: _submissionPdfFactory.Object,
            submissionSettingsRepository: _submissionSettingsRepository.Object,
            authRepo: _authRepo.Object,
            configuration: _configuration,
            emailService: _emailService.Object,
            pdfService: _pdfService.Object,
            formSubmissionRepository: _submissionRepo.Object,
            submissionAccessTokenRepository: _submissionAccessTokenRepo.Object
            );
    }

    private static FormSubmission CreatePendingSubmission()
    {
        var submission = SubmissionTestFactory.CreatePendingExternalSubmission();

        return submission;
    }

    private static SubmissionAccessToken CreateValidAccessToken()
    {
        return new SubmissionAccessToken
        {
            SubmissionId = "submission-1",
            Email = "client@test.com",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            IsRevoked = false
        };
    }

    [Fact]
    public async Task UpdateByAccessToken_WhenTokenIsMissing_ShouldReturnBadRequest()
    {
        var controller = CreateController();

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "",
            RowVersion = 2,
            Answers = new List<FormAnswerDto>()
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateByAccessToken_WhenSubmissionDoesNotExist_ShouldReturnNotFound()
    {
        var controller = CreateController();

        _submissionRepo
            .Setup(x => x.GetByIdAsync("submission-1"))
            .ReturnsAsync((FormSubmission?)null);

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "token-123",
            RowVersion = 2,
            Answers = new List<FormAnswerDto>()
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(SubmissionStatus.Drafted)]
    [InlineData(SubmissionStatus.Cancelled)]
    [InlineData(SubmissionStatus.Completed)]
    [InlineData(SubmissionStatus.Expired)]
    public async Task UpdateByAccessToken_WhenSubmissionStatusIsNotPending_ShouldReturnBadRequest(
    SubmissionStatus status)
    {
        var controller = CreateController();
        var submission = CreatePendingSubmission();
        submission.Status = status;

        _submissionRepo
            .Setup(x => x.GetByIdAsync("submission-1"))
            .ReturnsAsync(submission);

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "token-123",
            RowVersion = 2,
            Answers = new List<FormAnswerDto>()
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateByAccessToken_WhenRowVersionDoesNotMatch_ShouldReturnConflict()
    {
        var controller = CreateController();
        var submission = CreatePendingSubmission();

        _submissionRepo
            .Setup(x => x.GetByIdAsync("submission-1"))
            .ReturnsAsync(submission);

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "token-123",
            RowVersion = 999,
            Answers = new List<FormAnswerDto>()
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task UpdateByAccessToken_WhenTokenBelongsToAnotherSubmission_ShouldReturnForbid()
    {
        var controller = CreateController();
        var submission = CreatePendingSubmission();

        _submissionRepo
            .Setup(x => x.GetByIdAsync("submission-1"))
            .ReturnsAsync(submission);

        var tokenEntity = CreateValidAccessToken();
        tokenEntity.SubmissionId = "another-submission";

        _submissionAccessTokenRepo
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(tokenEntity);

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "valid-token",
            RowVersion = 2,
            Answers = new List<FormAnswerDto>()
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<ForbidResult>();
    }

    /*
     * External user can update only his fields.
     */
    [Fact]
    public async Task UpdateByAccessToken_ShouldReject_WhenRequestContainsOwnerField()
    {
        var controller = CreateController();
        var submission = CreatePendingSubmission();

        _submissionRepo
            .Setup(x => x.GetByIdAsync("submission-1"))
            .ReturnsAsync(submission);

        _submissionAccessTokenRepo
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateValidAccessToken());

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "valid-token",
            RowVersion = 2,
            Answers = new List<FormAnswerDto>
        {
            new() { FieldId = "client-name", Value = "New Client Name" },
            new() { FieldId = "owner-signature", Value = "/uploads/hacked.png" }
        }
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<ForbidResult>();

        _submissionRepo.Verify(x => x.UpdateAsync(It.IsAny<FormSubmission>()), Times.Never);
    }

    /*
     * Invalid fields are not allowed.
     */
    [Fact]
    public async Task UpdateByAccessToken_WhenRequestContainsOwnerField_ShouldReturnForbid()
    {
        var controller = CreateController();
        var submission = CreatePendingSubmission();

        _submissionRepo
            .Setup(x => x.GetByIdAsync("submission-1"))
            .ReturnsAsync(submission);

        _submissionAccessTokenRepo
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateValidAccessToken());

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "valid-token",
            RowVersion = 2,
            Answers = new List<FormAnswerDto>
        {
            new() { FieldId = "owner-signature", Value = "/uploads/hacked-owner.png" }
        }
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<ForbidResult>();
        _submissionRepo.Verify(x => x.UpdateAsync(It.IsAny<FormSubmission>()), Times.Never);
    }

    /*
     * Required field can not be empty
     */
    [Fact]
    public async Task UpdateByAccessToken_WhenRequiredClientFieldIsMissing_ShouldReturnBadRequest()
    {
        var controller = CreateController();
        var submission = CreatePendingSubmission();

        submission.Answers.RemoveAll(x => x.FieldId == "client-email");

        _submissionRepo
            .Setup(x => x.GetByIdAsync("submission-1"))
            .ReturnsAsync(submission);

        _submissionAccessTokenRepo
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateValidAccessToken());

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "valid-token",
            RowVersion = 2,
            Answers = new List<FormAnswerDto>
        {
            new() { FieldId = "client-name", Value = "Updated Name" }
        }
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    /*
     * Successfully external update should increase rowVersion and timeStampt     
     */
    [Fact]
    public async Task UpdateByAccessToken_WhenRequestIsValid_ShouldUpdateSubmissionMetadata()
    {
        var controller = CreateController();
        var submission = CreatePendingSubmission();

        _submissionRepo
            .Setup(x => x.GetByIdAsync("submission-1"))
            .ReturnsAsync(submission);

        _submissionAccessTokenRepo
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateValidAccessToken());

        var oldRowVersion = submission.RowVersion;

        var request = new UpdateSubmissionByAccessTokenRequest
        {
            Token = "valid-token",
            RowVersion = oldRowVersion,
            Answers = new List<FormAnswerDto>
        {
            new() { FieldId = "client-name", Value = "Updated Name" },
            new() { FieldId = "client-email", Value = "updated@mail.com" },
            new() { FieldId = "client-signature", Value = "/uploads/client-new.png" }
        }
        };

        var result = await controller.UpdateByAccessToken("submission-1", request);

        result.Should().BeOfType<NoContentResult>();
        submission.ExternalConfirmed.Should().BeTrue();
        submission.ExternalConfirmedAtUtc.Should().NotBeNull();
        submission.UpdatedAtUtc.Should().BeAfter(DateTime.MinValue);
        submission.RowVersion.Should().Be(oldRowVersion + 1);

        _submissionRepo.Verify(x => x.UpdateAsync(submission), Times.Once);
    }
}