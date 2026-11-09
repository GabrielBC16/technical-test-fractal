using MaintenanceBoard.Domain.Tickets;

namespace MaintenanceBoard.Tests.Tickets;

public sealed class TicketTransitionPolicyTests
{
    private readonly TicketTransitionPolicy _policy = new();

    // All 16 pairs are listed explicitly so an accidental new transition fails the tests.
    [Theory]
    [InlineData(TicketStatus.Pending, TicketStatus.Pending, false)]
    [InlineData(TicketStatus.Pending, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Pending, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Pending, TicketStatus.Rejected, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Pending, false)]
    [InlineData(TicketStatus.InProgress, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Rejected, true)]
    [InlineData(TicketStatus.Closed, TicketStatus.Pending, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Rejected, false)]
    [InlineData(TicketStatus.Rejected, TicketStatus.Pending, false)]
    [InlineData(TicketStatus.Rejected, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.Rejected, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Rejected, TicketStatus.Rejected, false)]
    public void Validate_enforces_the_complete_transition_matrix(
        TicketStatus currentStatus,
        TicketStatus newStatus,
        bool isAllowed)
    {
        Assert.Equal(isAllowed, _policy.CanTransition(currentStatus, newStatus));

        var exception = Record.Exception(() => _policy.Validate(
            currentStatus, newStatus, assigneeCount: 1,
            cause: "Power supply failure.", comment: "Duplicate incident."));

        if (isAllowed)
        {
            Assert.Null(exception);
        }
        else
        {
            var violation = Assert.IsType<TicketTransitionException>(exception);
            Assert.Equal(TicketTransitionViolation.InvalidTransition, violation.Violation);
        }
    }

    [Theory]
    [InlineData(TicketStatus.Pending, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed)]
    [InlineData(TicketStatus.Pending, TicketStatus.Rejected)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Rejected)]
    public void Validate_accepts_exact_English_status_names(TicketStatus currentStatus, TicketStatus newStatus)
    {
        var exception = Record.Exception(() => _policy.Validate(
            currentStatus.ToString(), newStatus.ToString(), 1,
            "Power supply failure.", "Duplicate incident."));

        Assert.Null(exception);
    }

    public static TheoryData<string?> UnknownStatusNames =>
    [
        null, "", " ", "Unknown", "Resolved", "pending", "inprogress", "CLOSED", "rejected",
        " Pending", "Pending ", "InProgress ", "\tPending", "0", "1", "99", "-1", "Pending, InProgress"
    ];

    [Theory]
    [MemberData(nameof(UnknownStatusNames))]
    public void Validate_rejects_unknown_current_status_names(string? currentStatus)
    {
        var exception = Assert.Throws<TicketTransitionException>(() =>
            _policy.Validate(currentStatus, "InProgress", 1));

        Assert.Equal(TicketTransitionViolation.InvalidTransition, exception.Violation);
    }

    [Theory]
    [MemberData(nameof(UnknownStatusNames))]
    public void Validate_rejects_unknown_target_status_names(string? newStatus)
    {
        var exception = Assert.Throws<TicketTransitionException>(() =>
            _policy.Validate("Pending", newStatus, 1));

        Assert.Equal(TicketTransitionViolation.InvalidTransition, exception.Violation);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Validate_rejects_undefined_enum_values(int unknownStatus)
    {
        var currentException = Assert.Throws<TicketTransitionException>(() =>
            _policy.Validate((TicketStatus)unknownStatus, TicketStatus.InProgress, 1));
        var targetException = Assert.Throws<TicketTransitionException>(() =>
            _policy.Validate(TicketStatus.Pending, (TicketStatus)unknownStatus, 1));

        Assert.Equal(TicketTransitionViolation.InvalidTransition, currentException.Violation);
        Assert.Equal(TicketTransitionViolation.InvalidTransition, targetException.Violation);
        Assert.False(_policy.CanTransition((TicketStatus)unknownStatus, TicketStatus.InProgress));
        Assert.False(_policy.CanTransition(TicketStatus.Pending, (TicketStatus)unknownStatus));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Starting_attention_requires_at_least_one_assignee(int assigneeCount)
    {
        var exception = Assert.Throws<TicketTransitionException>(() =>
            _policy.Validate(TicketStatus.Pending, TicketStatus.InProgress, assigneeCount));

        Assert.Equal(TicketTransitionViolation.AssigneeRequired, exception.Violation);
        Assert.Equal("Assign at least one operator before starting ticket attention.", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Starting_attention_accepts_assigned_tickets_without_cause_or_comment(int assigneeCount)
    {
        var exception = Record.Exception(() =>
            _policy.Validate(TicketStatus.Pending, TicketStatus.InProgress, assigneeCount));

        Assert.Null(exception);
    }

    public static TheoryData<string?> MissingText =>
    [
        null, "", " ", "\t", "\r\n", " \t\r\n ", "\u00A0", "\u2003", "\u202F", "\u3000"
    ];

    [Theory]
    [MemberData(nameof(MissingText))]
    public void Closing_requires_a_non_empty_cause(string? cause)
    {
        var exception = Assert.Throws<TicketTransitionException>(() =>
            _policy.Validate(TicketStatus.InProgress, TicketStatus.Closed, 1, cause, "Repair completed."));

        Assert.Equal(TicketTransitionViolation.CauseRequired, exception.Violation);
        Assert.Equal("A non-empty cause is required to close the ticket.", exception.Message);
    }

    [Theory]
    [MemberData(nameof(MissingText))]
    public void Rejecting_pending_tickets_requires_a_non_empty_reason(string? comment)
    {
        AssertRejectionRequiresReason(TicketStatus.Pending, comment);
    }

    [Theory]
    [MemberData(nameof(MissingText))]
    public void Rejecting_in_progress_tickets_requires_a_non_empty_reason(string? comment)
    {
        AssertRejectionRequiresReason(TicketStatus.InProgress, comment);
    }

    [Theory]
    [InlineData("Power supply failure.")]
    [InlineData("  Power supply failure.  ")]
    [InlineData("First finding.\nAdditional finding.")]
    public void Closing_accepts_a_cause_without_requiring_a_comment(string cause)
    {
        var exception = Record.Exception(() =>
            _policy.Validate(TicketStatus.InProgress, TicketStatus.Closed, 1, cause));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(TicketStatus.Pending)]
    [InlineData(TicketStatus.InProgress)]
    public void Rejecting_requires_neither_a_cause_nor_an_assignee(TicketStatus currentStatus)
    {
        var exception = Record.Exception(() => _policy.Validate(
            currentStatus, TicketStatus.Rejected, assigneeCount: 0, comment: "Duplicate incident."));

        Assert.Null(exception);
    }

    [Fact]
    public void A_cause_does_not_allow_skipping_attention()
    {
        var exception = Assert.Throws<TicketTransitionException>(() => _policy.Validate(
            TicketStatus.Pending, TicketStatus.Closed, 1, cause: "Power supply failure."));

        Assert.Equal(TicketTransitionViolation.InvalidTransition, exception.Violation);
    }

    [Fact]
    public void Invalid_transition_takes_precedence_over_target_state_guards()
    {
        var exception = Assert.Throws<TicketTransitionException>(() =>
            _policy.Validate(TicketStatus.Closed, TicketStatus.Rejected, 0));

        Assert.Equal(TicketTransitionViolation.InvalidTransition, exception.Violation);
    }

    private void AssertRejectionRequiresReason(TicketStatus currentStatus, string? comment)
    {
        var exception = Assert.Throws<TicketTransitionException>(() => _policy.Validate(
            currentStatus, TicketStatus.Rejected, 1, cause: "Known cause.", comment: comment));

        Assert.Equal(TicketTransitionViolation.RejectionReasonRequired, exception.Violation);
        Assert.Equal("A non-empty rejection reason is required.", exception.Message);
    }
}
