using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Domain.Tests.Tickets;

public class SatisfactionRatingTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Requester = Guid.NewGuid();

    private static Ticket ClosedCase(bool closed = true)
    {
        var type = RequestType.Create("Work certificate", "", RequestCategory.Certificates, false, TicketPriority.Medium, FormSchema.Empty);
        var ticket = Ticket.Submit("HR-2026-000100", type, Requester, "Certificate", null, "{}", Now);
        var agent = Guid.NewGuid();
        ticket.ChangeStatus(TicketStatus.Open, TransitionActor.Agent, agent, Now.AddMinutes(1));
        ticket.ChangeStatus(TicketStatus.Resolved, TransitionActor.Agent, agent, Now.AddMinutes(2), "Sent");
        if (closed)
            ticket.ChangeStatus(TicketStatus.Closed, TransitionActor.Requester, Requester, Now.AddMinutes(3));
        return ticket;
    }

    [Fact]
    public void The_requester_rates_a_closed_case_and_the_timeline_records_it()
    {
        var ticket = ClosedCase();

        var rating = SatisfactionRating.Create(ticket, Requester, 4, "  Fast  ", Now.AddHours(1));

        rating.Score.Should().Be(4);
        rating.Comment.Should().Be("Fast");
        rating.TicketId.Should().Be(ticket.Id);
        ticket.Events.Should().Contain(e => e.Type == TicketEventType.Rated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void The_score_is_between_one_and_five(int score)
    {
        var act = () => SatisfactionRating.Create(ClosedCase(), Requester, score, null, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("rating.invalid_score");
    }

    [Fact]
    public void Only_the_requester_rates()
    {
        var act = () => SatisfactionRating.Create(ClosedCase(), Guid.NewGuid(), 5, null, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("rating.not_requester");
    }

    [Fact]
    public void Only_closed_cases_are_rated()
    {
        var act = () => SatisfactionRating.Create(ClosedCase(closed: false), Requester, 5, null, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("rating.not_closed");
    }
}
