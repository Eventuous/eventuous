using Bookings.Domain;
using Bookings.Domain.Bookings;
using Eventuous;
using Eventuous.Extensions.AspNetCore;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using static Bookings.Application.BookingCommands;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Bookings.HttpApi.Bookings;

/// <summary>
/// This command API is for demo purposes only. It's the same as the CommandApi, but with a different route and a custom result type.
/// Check the swagger UI for the API documentation and see the custom result type in action.
/// </summary>
[Route("/custom/booking")]
public class CommandApiWithCustomResult(ICommandService<BookingState> service) : CommandHttpApiBase<BookingState, CustomBookingResult>(service) {
    [HttpPost]
    [Route("book")]
    public Task<ActionResult<CustomBookingResult>> BookRoom([FromBody] BookRoom cmd, CancellationToken cancellationToken)
        => Handle(cmd, cancellationToken);

    protected override ActionResult AsActionResult(Result<BookingState> result)
        => result.Match(
            ok => new OkObjectResult(
                new CustomBookingResult {
                    GuestId         = ok.State.GuestId,
                    RoomId          = ok.State.RoomId,
                    CustomPropertyA = "Some custom property",
                    CustomPropertyB = "Another custom property",
                    CustomPropertyC = "Yet another custom property"
                }
            ),
            error => error.Exception switch {
                ValidationException => MapValidationExceptionAsValidationProblemDetails(error),
                _                   => base.AsActionResult(result)
            }
        );

    static BadRequestObjectResult MapValidationExceptionAsValidationProblemDetails(Result<BookingState>.Error error) {
        if (error.Exception is not ValidationException exception) {
            throw new ArgumentNullException(nameof(error), "Exception in result is not of the type `ValidationException`. Unable to map validation result.");
        }

        var problemDetails = new ValidationProblemDetails() {
            Status = StatusCodes.Status400BadRequest,
            Detail = "Please refer to the errors property for additional details."
        };

        var groupFailures = exception.Errors.GroupBy(v => v.PropertyName);

        foreach (var groupFailure in groupFailures) {
            problemDetails.Errors.Add(groupFailure.Key, groupFailure.Select(s => s.ErrorMessage).ToArray());
        }

        return new(problemDetails);
    }
}

public record CustomBookingResult {
    public string GuestId { get; init; } = null!;
    public RoomId RoomId  { get; init; } = null!;

    public string? CustomPropertyA { get; init; }
    public string? CustomPropertyB { get; init; }
    public string? CustomPropertyC { get; init; }
}
