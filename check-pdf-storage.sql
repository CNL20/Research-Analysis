-- Kiểm tra PDFs đã được tải về (Status = 'Ready')
SELECT
    pf.Id              AS PaperPdfFileId,
    pf.ResearchPaperId AS PaperId,
    rp.Title,
    pf.LocalRelativePath,
    pf.Status,
    pf.SizeBytes,
    pf.Sha256,
    pf.ExternalSource,
    pf.SourceUrl,
    pf.AttemptCount,
    pf.EnqueuedAt,
    pf.CompletedAt,
    pf.FailureReason
FROM PaperPdfFiles pf
LEFT JOIN ResearchPapers rp ON rp.Id = pf.ResearchPaperId
ORDER BY pf.EnqueuedAt DESC
LIMIT 50;

-- Thống kê theo Status
SELECT
    Status,
    COUNT(*)                        AS Count,
    SUM(SizeBytes) / 1024 / 1024    AS TotalSizeMB
FROM PaperPdfFiles
GROUP BY Status
ORDER BY Count DESC;
