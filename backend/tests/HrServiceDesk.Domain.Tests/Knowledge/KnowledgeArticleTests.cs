using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Knowledge;

namespace HrServiceDesk.Domain.Tests.Knowledge;

public class KnowledgeArticleTests
{
    [Fact]
    public void Create_trims_text_and_counts_start_at_zero()
    {
        var article = KnowledgeArticle.Create("  Pay day ", " When salaries are paid ", " Last working day. ", RequestCategory.Payroll, publish: true);

        article.Title.Should().Be("Pay day");
        article.Summary.Should().Be("When salaries are paid");
        article.Body.Should().Be("Last working day.");
        article.IsPublished.Should().BeTrue();
        article.ViewCount.Should().Be(0);
        article.HelpfulCount.Should().Be(0);
    }

    [Theory]
    [InlineData("", "Body", "knowledge.invalid_title")]
    [InlineData("Title", " ", "knowledge.invalid_body")]
    public void Title_and_body_are_required(string title, string body, string code)
    {
        var act = () => KnowledgeArticle.Create(title, "", body, null, publish: false);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void Lengths_are_limited()
    {
        var act = () => KnowledgeArticle.Create("Title", new string('s', KnowledgeArticle.SummaryMaxLength + 1), "Body", null, publish: false);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("knowledge.invalid_summary");
    }

    [Fact]
    public void Views_and_helpful_votes_are_counted_and_publication_toggles()
    {
        var article = KnowledgeArticle.Create("Title", "", "Body", null, publish: false);

        article.RecordView();
        article.RecordView();
        article.RecordHelpful();
        article.SetPublished(true);

        article.ViewCount.Should().Be(2);
        article.HelpfulCount.Should().Be(1);
        article.IsPublished.Should().BeTrue();
    }
}
