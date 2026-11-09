using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets.Exceptions;
using MaintenanceBoard.Infrastructure.Persistence;

namespace MaintenanceBoard.Tests.Infrastructure;

public sealed class SqlErrorTranslatorTests
{
    [Theory]
    [InlineData(50001, typeof(TicketNotFoundException), "ticket.not_found")]
    [InlineData(50002, typeof(InvalidTicketTransitionException), "ticket.invalid_transition")]
    [InlineData(50003, typeof(TicketConcurrencyException), "ticket.concurrent_modification")]
    [InlineData(50004, typeof(TicketCauseRequiredException), "ticket.cause_required")]
    [InlineData(50005, typeof(TicketRejectionReasonRequiredException), "ticket.rejection_reason_required")]
    [InlineData(50006, typeof(TicketAssignmentLimitException), "ticket.assignment_limit")]
    [InlineData(50007, typeof(TicketAssignmentJustificationRequiredException), "ticket.assignment_justification_required")]
    [InlineData(50008, typeof(InvalidTicketAssigneeException), "ticket.invalid_assignee")]
    [InlineData(50009, typeof(TicketAssignmentConflictException), "ticket.assignment_conflict")]
    [InlineData(50010, typeof(UnauthenticatedUserException), "authentication.required")]
    [InlineData(50011, typeof(ForbiddenOperationException), "access.forbidden")]
    [InlineData(50012, typeof(RequestValidationException), "request.validation_failed")]
    [InlineData(50013, typeof(TicketCommentRequiredException), "ticket.comment_required")]
    [InlineData(50014, typeof(ImmutableTicketHistoryException), "ticket.history_immutable")]
    public void Expected_SQL_errors_have_stable_application_failures(int number, Type expectedType, string code)
    {
        var inner = new Exception("Validation-only SQL failure.");

        var result = SqlErrorTranslator.TranslateNumber(number, "A controlled business rule failed.", inner);

        Assert.NotNull(result);
        Assert.IsType(expectedType, result);
        Assert.Equal(code, result.Code);
        if (result is RequestValidationException validation)
        {
            Assert.Equal("A controlled business rule failed.", Assert.Single(validation.Errors["request"]));
        }
        else
        {
            Assert.Same(inner, result.InnerException);
            Assert.Equal("A controlled business rule failed.", result.Message);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(18456)]
    [InlineData(50015)]
    [InlineData(51090)]
    public void Unexpected_or_deployment_errors_are_not_disguised_as_business_failures(int number)
    {
        Assert.Null(SqlErrorTranslator.TranslateNumber(number, "An operational failure."));
    }
}
