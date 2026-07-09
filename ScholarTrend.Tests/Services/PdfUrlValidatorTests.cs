using FluentAssertions;
using ScholarTrend.Application.Services;

namespace ScholarTrend.Tests.Services;

public class PdfUrlValidatorTests
{
    // ========================================================================
    // POSITIVE CASES — Trusted hosts phải pass
    // ========================================================================

    [Theory]
    [InlineData("https://arxiv.org/pdf/1234.5678.pdf")]           // arXiv chính
    [InlineData("https://export.arxiv.org/pdf/1234.5678")]        // arXiv export
    [InlineData("https://doi.org/10.1109/foo.2024")]              // DOI resolver
    [InlineData("https://api.openalex.org/W1234")]                // OpenAlex
    [InlineData("https://openalex.org/W1234")]                    // OpenAlex alias
    [InlineData("https://www.frontiersin.org/articles/foo.pdf")]  // FrontiersIn
    [InlineData("https://www.mdpi.com/xxx/pdf")]                  // MDPI
    [InlineData("https://peerj.com/articles/pdf")]                // PeerJ
    [InlineData("https://plos.org/global")]                       // PLOS
    [InlineData("https://plosone.org/foo")]                       // PLOS ONE
    [InlineData("https://biomedcentral.com/foo")]                 // BMC
    [InlineData("https://springeropen.com/foo")]                  // Springer OA
    [InlineData("https://nature.com/articles/s41586-021")]         // Nature
    [InlineData("https://royalsocietypublishing.org/foo")]        // Royal Society
    [InlineData("https://www.cambridge.org/core")]                // Cambridge
    [InlineData("https://cell.com/cell-reports/fulltext")]        // Cell
    [InlineData("https://elifesciences.org/articles/pdf")]        // eLife
    [InlineData("https://jmir.org/2024/1/e12345")]                // JMIR
    [InlineData("https://researchgate.net/publication/pdf")]      // ResearchGate
    [InlineData("https://core.ac.uk/download/pdf")]               // CORE
    [InlineData("https://doaj.org/articles/pdf")]                 // DOAJ
    public void IsSafe_TrustedHosts_ReturnsTrue(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeTrue(url);
        reason.Should().BeNull(url);
    }

    [Theory]
    [InlineData("http://arxiv.org/pdf/1234.pdf")]                       // http (không phải https) vẫn OK
    [InlineData("https://ARXIV.ORG/pdf/1234.pdf")]                      // uppercase host
    [InlineData("https://ArXiv.Org/Pdf/1234.pdf")]                      // mixed case
    public void IsSafe_HostVariations_ReturnsTrue(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out _);
        result.Should().BeTrue(url);
    }

    [Theory]
    [InlineData("https://api.elsevier.com/x.pdf")]                      // KHÔNG nằm trong whitelist
    [InlineData("https://api.springer.com/x.pdf")]                      // KHÔNG nằm trong whitelist
    [InlineData("https://www.ieee.org/x")]                               // KHÔNG nằm trong whitelist
    public void IsSafe_UntrustedPublisherHost_ReturnsFalse(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out _);
        result.Should().BeFalse(url);
    }

    [Theory]
    [InlineData("https://www.frontiersin.org/articles/10.3389/foo.pdf?download=true")]        // query string
    [InlineData("https://www.mdpi.com/2076-393X/1/1/2/pdf#page=5")]                            // fragment
    [InlineData("https://www.mdpi.com:443/foo.pdf")]                                            // port tường minh
    [InlineData("https://peerj.com/articles/1234-5678/peerj-preprint-12345.pdf?version=1")]    // multi params
    public void IsSafe_TrustedHostWithQueryOrPort_ReturnsTrue(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeTrue(url);
        reason.Should().BeNull(url);
    }

    // ========================================================================
    // NEGATIVE CASES — URL không hợp lệ phải fail
    // ========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]                                                    // whitespace
    [InlineData("\t\n")]
    public void IsSafe_NullOrEmpty_ReturnsFalseWithReason(string? url)
    {
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeFalse();
        reason.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("not-a-url")]                                               // không phải URL
    [InlineData("://missing-scheme.com/foo.pdf")]                           // thiếu scheme
    [InlineData("javascript:alert(1)")]                                     // javascript scheme
    [InlineData("data:application/pdf;base64,JVBERi0xLjQK")]                // data URI
    [InlineData("mailto:test@example.com")]
    [InlineData("//arxiv.org/foo.pdf")]                                     // protocol-relative
    public void IsSafe_InvalidOrDangerousUri_ReturnsFalse(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeFalse();
        reason.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("ftp://arxiv.org/foo.pdf")]                                 // ftp scheme
    [InlineData("file:///c:/secret.pdf")]                                   // local file
    [InlineData("ssh://arxiv.org/x")]                                       // ssh
    [InlineData("telnet://arxiv.org/x")]                                    // telnet
    [InlineData("gopher://arxiv.org/x")]                                    // gopher
    [InlineData("ldap://arxiv.org/x")]                                      // ldap
    [InlineData("chrome://arxiv.org/x")]                                    // chrome
    public void IsSafe_NonHttpScheme_ReturnsFalse(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeFalse();
        reason.Should().NotBeNullOrEmpty();
        reason.Should().Contain("scheme", "reason should explain the scheme rejection");
    }

    [Theory]
    [InlineData("https://evil.com/foo.pdf")]                                // domain thù địch
    [InlineData("https://attacker.io/payload.pdf")]                         // fake domain
    [InlineData("https://elsevier.com/foo.pdf")]                            // publisher KHÔNG whitelist
    [InlineData("https://springer.com/foo.pdf")]                            // publisher KHÔNG whitelist
    [InlineData("https://ieee.org/x.pdf")]                                  // publisher KHÔNG whitelist
    [InlineData("https://wiley.com/foo.pdf")]                               // publisher KHÔNG whitelist
    [InlineData("https://google.com/search?q=arxiv")]                       // search engine
    public void IsSafe_UntrustedHost_ReturnsFalse(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeFalse();
        reason.Should().NotBeNullOrEmpty();
        reason.Should().Contain("not in the trusted host list");
    }

    [Theory]
    [InlineData("https://localhost/foo.pdf")]
    [InlineData("https://127.0.0.1/foo.pdf")]
    [InlineData("https://192.168.1.1/foo.pdf")]
    [InlineData("https://10.0.0.1/foo.pdf")]
    [InlineData("https://0.0.0.0/foo.pdf")]
    public void IsSafe_LocalNetworkHosts_ReturnsFalse(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeFalse();
        reason.Should().NotBeNullOrEmpty();
    }

    // ========================================================================
    // SSRF / BYPASS ATTEMPTS
    // ========================================================================

    [Theory]
    [InlineData("https://arxiv.org.evil.com/foo.pdf")]                     // suffix attack: arxiv.org.evil.com
    [InlineData("https://fakearxiv.org/foo.pdf")]                          // prefix fake
    [InlineData("https://notarxiv.org/foo.pdf")]                           // prefix not
    [InlineData("https://arxiv-org.evil.io/foo.pdf")]                      // dash attack
    [InlineData("https://myarxiv.org/foo.pdf")]                            // myarxiv
    public void IsSafe_HostSuffixSpoofing_ReturnsFalse(string url)
    {
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeFalse();
        reason.Should().Contain("not in the trusted host list");
    }

    [Theory]
    [InlineData("https://arxiv.org@evil.com/foo.pdf")]                     // userinfo attack
    [InlineData("https://evil.com#@arxiv.org/foo.pdf")]                    // fragment trick
    public void IsSafe_UserInfoOrFragmentTrick_ReturnsFalse(string url)
    {
        // URL này hoặc parse được (host=evil.com) hoặc fail; đều KHÔNG trust
        var result = PdfUrlValidator.IsSafe(url, out var reason);
        result.Should().BeFalse();
    }
}
