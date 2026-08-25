/*
    Xóa cứng 1 kỳ lương ĐÃ CHỐT (Finalized) khỏi database — bỏ qua rule nghiệp vụ
    (bình thường app chỉ cho xóa kỳ Draft, xem DeletePayrollPeriodCommandHandler).

    CẢNH BÁO:
      - Đây là thao tác PHÁ HỦY DỮ LIỆU TÀI CHÍNH, không thể hoàn tác sau khi COMMIT.
      - KHÔNG áp dụng cho kỳ đã Locked (đã chi lương / đối soát xong) — nếu Status = 3,
        DỪNG LẠI và cân nhắc lại, script này sẽ chặn bằng RAISERROR nhưng vẫn nên tự kiểm tra trước.
      - PaymentObligationEvidences chứa ObjectKey trỏ tới file trên MinIO — script chỉ xóa
        bản ghi DB, KHÔNG xóa object trên MinIO. Bước 1 liệt kê các ObjectKey để tự dọn thủ công.
      - Hãy NGAO BACKUP database (hoặc ít nhất bảng liên quan) trước khi chạy phần DELETE.

    Cách dùng:
      1. Sửa @Year/@Month cho đúng kỳ cần xóa.
      2. Chạy khối "BƯỚC 0 — PREVIEW" trước, kiểm tra số liệu đúng như kỳ vọng.
      3. Chạy toàn bộ script trong 1 transaction. Nếu ổn thì COMMIT, nếu nghi ngờ thì ROLLBACK.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Year  INT = 2026;
DECLARE @Month INT = 7;

DECLARE @PeriodId INT;
SELECT @PeriodId = Id, @PeriodId = Id
FROM PayrollPeriods
WHERE PeriodYear = @Year AND PeriodMonth = @Month;

IF @PeriodId IS NULL
BEGIN
    RAISERROR('Không tìm thấy kỳ lương %d/%d.', 16, 1, @Month, @Year);
    RETURN;
END

DECLARE @Status INT;
SELECT @Status = Status FROM PayrollPeriods WHERE Id = @PeriodId;

IF @Status = 3 -- Locked
BEGIN
    RAISERROR('Kỳ %d/%d đang ở trạng thái LOCKED (đã chi lương/đối soát xong) — script này KHÔNG được thiết kế để xóa kỳ Locked. Dừng lại.', 16, 1, @Month, @Year);
    RETURN;
END

PRINT 'PeriodId = ' + CAST(@PeriodId AS VARCHAR(10)) + ', Status = ' + CAST(@Status AS VARCHAR(10)) + ' (2 = Finalized)';

-- ============================================================
-- BƯỚC 0 — PREVIEW (chỉ SELECT, không đổi dữ liệu). Chạy riêng khối này trước.
-- ============================================================
SELECT 'PayrollRows'               AS TableName, COUNT(*) AS Cnt FROM PayrollRows WHERE PayrollPeriodId = @PeriodId
UNION ALL
SELECT 'PayrollCellValues',        COUNT(*) FROM PayrollCellValues pcv
    JOIN PayrollRows pr ON pr.Id = pcv.PayrollRowId WHERE pr.PayrollPeriodId = @PeriodId
UNION ALL
SELECT 'PayrollComplianceRefs',    COUNT(*) FROM PayrollComplianceRefs WHERE PayrollPeriodId = @PeriodId
UNION ALL
SELECT 'PayrollNotes',             COUNT(*) FROM PayrollNotes WHERE PayrollPeriodId = @PeriodId
UNION ALL
SELECT 'PaymentObligations',       COUNT(*) FROM PaymentObligations WHERE PayrollPeriodId = @PeriodId
UNION ALL
SELECT 'PaymentObligationLines',   COUNT(*) FROM PaymentObligationLines pol
    JOIN PaymentObligations po ON po.Id = pol.PaymentObligationId WHERE po.PayrollPeriodId = @PeriodId
UNION ALL
SELECT 'PaymentObligationEvidences', COUNT(*) FROM PaymentObligationEvidences poe
    JOIN PaymentObligations po ON po.Id = poe.PaymentObligationId WHERE po.PayrollPeriodId = @PeriodId
UNION ALL
SELECT 'PayrollAdjustments (SourcePeriod)', COUNT(*) FROM PayrollAdjustments WHERE SourcePeriodId = @PeriodId
UNION ALL
SELECT 'PayrollAdjustments (AppliedPeriod)', COUNT(*) FROM PayrollAdjustments WHERE AppliedPeriodId = @PeriodId;

-- Danh sách ObjectKey trên MinIO cần dọn thủ công sau khi xóa DB xong:
SELECT poe.ObjectKey
FROM PaymentObligationEvidences poe
JOIN PaymentObligations po ON po.Id = poe.PaymentObligationId
WHERE po.PayrollPeriodId = @PeriodId;

-- ============================================================
-- BƯỚC 1 — XÓA THẬT (transaction). Chỉ chạy sau khi đã xem preview ở trên và đồng ý.
-- ============================================================
BEGIN TRANSACTION;

BEGIN TRY

    -- 1) PayrollAdjustments trỏ tới kỳ này (Restrict FK) — gỡ tham chiếu AppliedPeriod (đang là
    --    kỳ *khác* áp khoản chênh vào kỳ này thì không đụng tới, chỉ xóa/gỡ những gì trỏ THẲNG
    --    vào kỳ đang xóa).
    UPDATE PayrollAdjustments
    SET AppliedPeriodId = NULL, AppliedAt = NULL
    WHERE AppliedPeriodId = @PeriodId;

    DELETE FROM PayrollAdjustments
    WHERE SourcePeriodId = @PeriodId;

    -- 2) PaymentObligations của kỳ này (cascade kéo theo Lines + Evidences)
    DELETE FROM PaymentObligations
    WHERE PayrollPeriodId = @PeriodId;

    -- 3) PayrollPeriods — cascade kéo theo PayrollComplianceRefs, PayrollRows (+ PayrollCellValues), PayrollNotes
    DELETE FROM PayrollPeriods
    WHERE Id = @PeriodId;

    PRINT 'Đã xóa kỳ lương ' + CAST(@Month AS VARCHAR(2)) + '/' + CAST(@Year AS VARCHAR(4)) + ' (PeriodId=' + CAST(@PeriodId AS VARCHAR(10)) + ').';
    PRINT 'Nhớ dọn thủ công các ObjectKey MinIO đã liệt kê ở bước preview (nếu có).';

    -- An toàn: để ROLLBACK mặc định. Đổi thành COMMIT khi đã chắc chắn.
    ROLLBACK TRANSACTION;
    PRINT '>>> Đây là DRY-RUN — đã ROLLBACK, KHÔNG có gì bị xóa thật. Đổi ROLLBACK thành COMMIT ở cuối script để xóa thật.';

END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
