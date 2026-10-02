using HrServiceDesk.Application.Dashboard;

namespace HrServiceDesk.Application.Tests.Dashboard;

public class CsvCellTests
{
    [Theory]
    [InlineData("HR-2026-000001", "HR-2026-000001")]
    [InlineData("Payroll, team A", "\"Payroll, team A\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("", "")]
    public void Cells_are_quoted_and_formulas_neutralised(string value, string expected) =>
        DashboardHandlers.Cell(value).Should().Be(expected);
}
