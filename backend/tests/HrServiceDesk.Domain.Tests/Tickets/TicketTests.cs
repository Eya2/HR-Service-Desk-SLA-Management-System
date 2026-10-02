using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Domain.Tests.Tickets;

public class TicketTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private static RequestType Type(RequestCategory category = RequestCategory.Payroll, bool confidential = false) =>
        RequestType.Create("Payslip correction", "", category, confidential, TicketPriority.High, FormSchema.Empty);

    [Fact]
    public void Submit_starts_new_with_the_type_defaults()
    {
        var requester = Guid.NewGuid();

        var ticket = Ticket.Submit("HR-2026-000001", Type(), requester, "  March payslip ", null, "{}");

        ticket.Status.Should().Be(TicketStatus.New);
        ticket.Priority.Should().Be(TicketPriority.High);
        ticket.Title.Should().Be("March payslip");
        ticket.Description.Should().BeEmpty();
        ticket.RequesterId.Should().Be(requester);
        ticket.IsConfidential.Should().BeFalse();
        ticket.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void Confidential_category_always_makes_cases_confidential()
    {
        var type = Type(RequestCategory.Confidential, confidential: false);

        type.IsConfidential.Should().BeTrue();
        Ticket.Submit("HR-2026-000002", type, Guid.NewGuid(), "Report", null, "{}").IsConfidential.Should().BeTrue();
    }

    [Fact]
    public void Inactive_request_types_cannot_be_submitted()
    {
        var type = Type();
        type.Deactivate();

        var act = () => Ticket.Submit("HR-2026-000003", type, Guid.NewGuid(), "Title", null, "{}");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.request_type_inactive");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_is_required(string title)
    {
        var act = () => Ticket.Submit("HR-2026-000004", Type(), Guid.NewGuid(), title, null, "{}");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.invalid_title");
    }

    [Fact]
    public void Comments_are_trimmed_and_must_not_be_empty()
    {
        var ticket = Ticket.Submit("HR-2026-000005", Type(), Guid.NewGuid(), "Title", null, "{}");

        var comment = ticket.AddComment(Guid.NewGuid(), "  Please check  ", isInternal: true, Now);

        comment.Body.Should().Be("Please check");
        comment.IsInternal.Should().BeTrue();
        comment.TicketId.Should().Be(ticket.Id);
        ticket.Comments.Should().ContainSingle();
        var act = () => ticket.AddComment(Guid.NewGuid(), " ", isInternal: false, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("comment.invalid_body");
    }

    [Fact]
    public void Attachments_belong_to_the_ticket()
    {
        var ticket = Ticket.Submit("HR-2026-000006", Type(), Guid.NewGuid(), "Title", null, "{}");

        var attachment = ticket.AddAttachment("payslip.pdf", "application/pdf", 1234, "key", Guid.NewGuid(), "payslip", Now);

        attachment.TicketId.Should().Be(ticket.Id);
        ticket.Attachments.Should().ContainSingle().Which.FieldKey.Should().Be("payslip");
    }

    [Theory]
    [InlineData(2026, 1, "HR-2026-000001")]
    [InlineData(2027, 123456, "HR-2027-123456")]
    public void References_are_zero_padded(int year, long number, string expected) =>
        TicketReference.Format(year, number).Should().Be(expected);

    [Fact]
    public void References_reject_out_of_range_numbers()
    {
        var act = () => TicketReference.Format(2026, 1_000_000);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
