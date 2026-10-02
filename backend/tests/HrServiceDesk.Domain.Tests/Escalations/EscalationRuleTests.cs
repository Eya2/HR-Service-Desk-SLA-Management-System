using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Escalations;
using HrServiceDesk.Domain.Sla;

namespace HrServiceDesk.Domain.Tests.Escalations;

public class EscalationRuleTests
{
    private static readonly Guid Payslip = Guid.NewGuid();

    private static EscalationFacts Facts(SlaState state, int? withoutResponse = null, bool final = false, Guid? type = null) =>
        new(type ?? Payslip, state, withoutResponse, final);

    [Theory]
    [InlineData(SlaState.OnTrack, false)]
    [InlineData(SlaState.AtRisk, true)]
    [InlineData(SlaState.Breached, true)]
    public void At_risk_rules_fire_at_risk_or_worse(SlaState state, bool expected) =>
        EscalationRule.Create("R", EscalationTrigger.AtRisk, null, EscalationAction.NotifyAssignee, null, null)
            .IsTriggeredBy(Facts(state)).Should().Be(expected);

    [Fact]
    public void Breach_rules_fire_only_on_breach()
    {
        var rule = EscalationRule.Create("R", EscalationTrigger.Breached, null, EscalationAction.NotifyManager, null, null);

        rule.IsTriggeredBy(Facts(SlaState.AtRisk)).Should().BeFalse();
        rule.IsTriggeredBy(Facts(SlaState.Breached)).Should().BeTrue();
    }

    [Fact]
    public void No_response_rules_count_business_hours_without_an_answer()
    {
        var rule = EscalationRule.Create("R", EscalationTrigger.NoResponseFor, 4, EscalationAction.NotifyAssignee, null, null);

        rule.IsTriggeredBy(Facts(SlaState.OnTrack, withoutResponse: 239)).Should().BeFalse();
        rule.IsTriggeredBy(Facts(SlaState.OnTrack, withoutResponse: 240)).Should().BeTrue();
        rule.IsTriggeredBy(Facts(SlaState.Breached, withoutResponse: null)).Should().BeFalse("the case was answered");
    }

    [Fact]
    public void Rules_ignore_closed_cases_other_request_types_and_inactive_rules()
    {
        var rule = EscalationRule.Create("R", EscalationTrigger.Breached, null, EscalationAction.BumpPriority, null, Payslip);

        rule.IsTriggeredBy(Facts(SlaState.Breached, final: true)).Should().BeFalse();
        rule.IsTriggeredBy(Facts(SlaState.Breached, type: Guid.NewGuid())).Should().BeFalse();
        rule.Update("R", EscalationTrigger.Breached, null, EscalationAction.BumpPriority, null, Payslip, isActive: false);
        rule.IsTriggeredBy(Facts(SlaState.Breached)).Should().BeFalse();
    }

    [Fact]
    public void Rules_are_validated()
    {
        var noHours = () => EscalationRule.Create("R", EscalationTrigger.NoResponseFor, null, EscalationAction.NotifyAssignee, null, null);
        noHours.Should().Throw<DomainException>().Which.Code.Should().Be("escalation.invalid_hours");

        var noTeam = () => EscalationRule.Create("R", EscalationTrigger.Breached, null, EscalationAction.ReassignToTeam, null, null);
        noTeam.Should().Throw<DomainException>().Which.Code.Should().Be("escalation.missing_team");
    }
}
