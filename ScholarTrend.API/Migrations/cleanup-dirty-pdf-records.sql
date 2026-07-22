-- =====================================================================
-- Migration: Cleanup dirty PDF records (post HTML-as-PDF bug)
-- =====================================================================
-- Bug: Khi user gọi API analyze paper, hệ thống tự động download PDF
--      từ SourceUrl. Nếu URL trả về HTML (404 page, v.v.) thì:
--      - HTML bytes được lưu thành LocalRelativePath file (KHÔNG phải PDF)
--      - Status = Ready (vì download "thành công")
--      - AnalysisStatus = Completed với kết quả rác (do PdfPig parse HTML → text rác → AI)
--
-- Mục đích:
--   1. Đánh dấu các record dirty là Failed (không xóa cứng — paper vẫn còn trong DB)
--   2. Xóa các analysis cache không hợp lệ để user có thể retry sau khi fix
--   3. Log lại để audit
--
-- Cách chạy: chạy từng block, đọc kết quả trước khi chạy DELETE.
--            Repo của tôi đã được patch bằng code fix — sau migration này,
--            user retry analysis sẽ:
--            - Download path có ValidationHelper check magic-bytes → FAIL nếu là HTML
--            - Status cập nhật thành Failed (không lưu file rác nữa)
--            - Trả về null + message rõ ràng
-- =====================================================================

BEGIN;

-- ============================================================
-- STEP 1: Kiểm tra — trước khi xóa, xem có bao nhiêu record bẩn
-- ============================================================
SELECT 'STEP 1: Dry-run — đếm record dirty' AS step;

-- 1a. Tất cả PaperPdfFile đã "Completed" analysis
SELECT
    COUNT(*) AS total_completed_analyses,
    COUNT(*) FILTER (WHERE AnalysisError IS NOT NULL) AS has_error,
    COUNT(*) FILTER (WHERE AnalysisResultJson IS NOT NULL) AS has_result_json
FROM "PaperPdfFiles"
WHERE "AnalysisStatus" = 'Completed';

-- 1b. Các record có SHA256 suspect (early bug có thể set null, hash plaintext, etc.)
SELECT
    "Id",
    "ResearchPaperId",
    "SourceUrl",
    "Status",
    "AnalysisStatus",
    "SizeBytes",
    "ContentType",
    "Sha256",
    "FailureReason",
    "CompletedAt"
FROM "PaperPdfFiles"
WHERE "AnalysisStatus" = 'Completed'
  AND (
    -- Không có SHA256 (download path on-demand cũ chưa set)
    "Sha256" IS NULL
    -- Hoặc ContentType không phải PDF
    OR ("ContentType" IS NOT NULL AND "ContentType" <> 'application/pdf')
    -- Hoặc SizeBytes quá nhỏ (< 5KB) — hầu hết paper PDF phải > 5KB
    OR "SizeBytes" < 5120
    -- Hoặc AnalysisError có sẵn dù Status = Completed (inconsistent state)
    OR "AnalysisError" IS NOT NULL
  )
ORDER BY "ResearchPaperId";

-- ============================================================
-- STEP 2: Verify — các record này thật sự là dirty
-- ============================================================
-- Tùy chọn: spot-check bằng cách peek vào vài file PDF.
-- Nếu local:
--   xxd /uploads/papers/<id>.pdf | head -1    -- phải thấy "%PDF"
-- Nếu B2: dùng b2 CLI hoặc xem HTTP HEAD Content-Type.

SELECT 'STEP 2: Per-record detail (TOP 20)' AS step;

SELECT
    pf."Id",
    pf."ResearchPaperId",
    rp."Title",
    pf."SourceUrl",
    pf."SizeBytes",
    pf."ContentType",
    pf."Sha256",
    LEFT(pf."AnalysisResultJson"::text, 200) AS result_preview,
    pf."AnalysisError",
    pf."CompletedAt"
FROM "PaperPdfFiles" pf
LEFT JOIN "ResearchPapers" rp ON rp."Id" = pf."ResearchPaperId"
WHERE pf."AnalysisStatus" = 'Completed'
  AND (
    pf."Sha256" IS NULL
    OR (pf."ContentType" IS NOT NULL AND pf."ContentType" <> 'application/pdf')
    OR pf."SizeBytes" < 5120
    OR pf."AnalysisError" IS NOT NULL
  )
ORDER BY pf."ResearchPaperId"
LIMIT 20;

-- ============================================================
-- STEP 3: Invalidate dirty cache — set AnalysisStatus = 'Failed'
-- ============================================================
-- Lý do không xóa cứng: có thể cần audit/debug sau.
-- Chỉ clear AnalysisResultJson + AnalysisError, set lại Status về Failed.
--
-- *** CHẠY SAU KHI ĐÃ VERIFY STEP 1 & 2 ***
SELECT 'STEP 3: Cleanup dirty PDF records' AS step;

UPDATE "PaperPdfFiles"
SET
    "AnalysisStatus" = 'Failed',
    "AnalysisError" = COALESCE("AnalysisError", 'Invalidated by post-bug migration: missing SHA256 / wrong Content-Type / suspicious size'),
    "AnalysisResultJson" = NULL,
    -- Cũng đánh dấu download Status = Failed để logic retry biết
    "Status" = CASE
        WHEN "Status" = 'Ready' THEN 'Failed'
        ELSE "Status"
    END,
    "FailureReason" = COALESCE("FailureReason", 'Cleanup: invalid PDF cache from pre-fix bug')
WHERE "AnalysisStatus" = 'Completed'
  AND (
    "Sha256" IS NULL
    OR ("ContentType" IS NOT NULL AND "ContentType" <> 'application/pdf')
    OR "SizeBytes" < 5120
    OR "AnalysisError" IS NOT NULL
  );

-- ============================================================
-- STEP 4: Verify — xác nhận đã cleanup
-- ============================================================
SELECT 'STEP 4: After cleanup' AS step;

SELECT
    "Id",
    "ResearchPaperId",
    "Status",
    "AnalysisStatus",
    "AnalysisError",
    "FailureReason"
FROM "PaperPdfFiles"
WHERE "ResearchPaperId" IN (5, 7, 29, 32)  -- papers report lỗi
ORDER BY "ResearchPaperId";

-- ============================================================
-- STEP 5: Test thử bằng tay — gọi API analyze paper #5 trên dev env
-- ============================================================
-- Sau migration, gọi POST /api/papers/5/analyze (hoặc endpoint tương đương)
-- EXPECTED:
--   - Log: "PDF validation FAILED for paper 5: missing %PDF- magic header"
--   - Status trong DB: Failed, FailureReason chứa "validation FAILED"
--   - API response: 200 OK với body null (hoặc error message rõ ràng)
--
-- Nếu Status đã Failed sẵn: API sẽ thử re-download → orchestrator xử lý
--         → nếu URL giờ trả về PDF OK → Status chuyển Ready
--         → nếu URL vẫn trả về HTML → Status Failed, FailureReason cập nhật

COMMIT;
