using HrServiceDesk.Application.Tickets.Files;

namespace HrServiceDesk.Application.Tests.Tickets;

public class FilePolicyTests
{
    private static readonly AttachmentOptions Options = new() { MaxFileSizeBytes = 1024, MaxFilesPerRequest = 3 };

    private static UploadedFile File(string name, byte[] content, string field = "proof") =>
        new(field, name, content.Length, () => new MemoryStream(content));

    private static readonly byte[] PdfBytes = [.. "%PDF-1.7 test"u8];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] ExeBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03];

    [Fact]
    public void Accepts_allowed_types_and_sets_content_type_from_the_content()
    {
        var (accepted, errors) = FilePolicy.Check([File("payslip.PDF", PdfBytes), File("photo.png", PngBytes)], Options);

        errors.Should().BeEmpty();
        accepted.Select(a => a.ContentType).Should().Equal("application/pdf", "image/png");
    }

    [Fact]
    public void Rejects_a_disguised_executable()
    {
        var (accepted, errors) = FilePolicy.Check([File("invoice.pdf", ExeBytes)], Options);

        accepted.Should().BeEmpty();
        errors.Should().ContainSingle().Which.Message.Should().Contain("does not match its extension");
    }

    [Theory]
    [InlineData("script.exe", "not an accepted file type")]
    [InlineData("page.html", "not an accepted file type")]
    public void Rejects_unlisted_extensions(string name, string expected)
    {
        FilePolicy.Check([File(name, PdfBytes)], Options).Errors.Should().ContainSingle().Which.Message.Should().Contain(expected);
    }

    [Fact]
    public void Rejects_empty_and_oversized_files_and_too_many_files()
    {
        FilePolicy.Check([File("a.pdf", [])], Options).Errors.Single().Message.Should().Contain("is empty");
        FilePolicy.Check([File("a.pdf", [.. PdfBytes, .. new byte[1100]])], Options).Errors.Single().Message.Should().Contain("exceeds");
        FilePolicy.Check([File("a.pdf", PdfBytes), File("b.pdf", PdfBytes), File("c.pdf", PdfBytes), File("d.pdf", PdfBytes)], Options)
            .Errors.Single().Message.Should().Contain("At most 3");
    }

    [Theory]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("C:\\Users\\me\\payslip.pdf", "payslip.pdf")]
    [InlineData("pay<slip>\u0007.pdf", "payslip.pdf")]
    [InlineData("...", "file")]
    [InlineData(null, "file")]
    public void File_names_are_sanitized(string? input, string expected) =>
        FilePolicy.SafeFileName(input).Should().Be(expected);
}
