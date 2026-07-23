namespace ScholarTrend.Application.Services;

/// <summary>
/// Xác thực URL PDF trước khi tải — whitelist hosts an toàn để tránh SSRF.
/// </summary>
public static class PdfUrlValidator
{
    private static readonly HashSet<string> TrustedHostSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        // arXiv (luôn open access)
        "arxiv.org",
        "export.arxiv.org",

        // DOI resolver (chỉ landing page, không phải PDF)
        "doi.org",

        // OpenAlex / OpenAlex mirror
        "openalex.org",
        "api.openalex.org",

        // Một số publisher open-access phổ biến — mở rộng thêm khi cần
        "frontiersin.org",
        "mdpi.com",
        "peerj.com",
        "plos.org",
        "plosone.org",
        "biomedcentral.com",
        "springeropen.com",
        "nature.com",                // Nature Communications OA
        "royalsocietypublishing.org",
        "cambridge.org",
        "cell.com",                  // Cell Reports OA
        "elifesciences.org",
        "jmir.org",
        "researchgate.net",
        "core.ac.uk",                // CORE aggregator
        "doaj.org",
        "nih.gov",                   // PubMed / NCBI
        "ncbi.nlm.nih.gov",          // PMC
        "europepmc.org"              // Europe PMC
    };

    public static bool IsSafe(string? url, out string? failureReason)
    {
        failureReason = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            failureReason = "URL is empty";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            failureReason = "URL is not a valid absolute URI";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            failureReason = $"Unsupported scheme: {uri.Scheme}";
            return false;
        }

        var host = uri.Host;
        var matched = TrustedHostSuffixes.Any(suffix =>
            host.Equals(suffix, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase));

        if (!matched)
        {
            failureReason = $"Host '{host}' is not in the trusted host list";
            return false;
        }

        return true;
    }
}
