using IdentityService.Application.TailorInvitations.AcceptTailorInvitation;
using IdentityService.Application.TailorInvitations.IssueTailorInvitation;

namespace IdentityService.Api.TailorInvitations;

public static class TailorInvitationEndpoints
{
    public static IEndpointRouteBuilder MapTailorInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tailor-invitations").WithTags("TailorInvitations");

        group.MapPost("/", IssueInvitationAsync)
            .WithName("IssueTailorInvitation")
            .Produces<IssueTailorInvitationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/accept", AcceptInvitationAsync)
            .WithName("AcceptTailorInvitation")
            .Produces<AcceptTailorInvitationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }

    private static async Task<IResult> IssueInvitationAsync(
        IssueTailorInvitationRequest request,
        IssueTailorInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors[nameof(request.Email)] = ["Email address is required."];

        if (request.TailorProfileId == Guid.Empty)
            errors[nameof(request.TailorProfileId)] = ["Tailor profile ID is required."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await handler.HandleAsync(
            new IssueTailorInvitationCommand(request.Email, request.TailorProfileId),
            cancellationToken);

        var response = new IssueTailorInvitationResponse(result.InvitationId.Value, result.ExpiresAt);
        return Results.Created($"/api/tailor-invitations/{response.InvitationId}", response);
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptTailorInvitationRequest request,
        AcceptTailorInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.InvitationId == Guid.Empty)
            errors[nameof(request.InvitationId)] = ["Invitation ID is required."];

        if (string.IsNullOrWhiteSpace(request.Token))
            errors[nameof(request.Token)] = ["Invitation token is required."];

        if (string.IsNullOrWhiteSpace(request.Password))
            errors[nameof(request.Password)] = ["Password is required."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await handler.HandleAsync(
            new AcceptTailorInvitationCommand(request.InvitationId, request.Token, request.Password),
            cancellationToken);

        return Results.Ok(new AcceptTailorInvitationResponse(result.Email.Value));
    }
}
