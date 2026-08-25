using System.Globalization;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Text;
using MPS.Domain.Entities.Payroll.Banking;
using Cells = MPS.Application.Features.Payroll.PaymentDocuments.Common.SalaryPaymentFormCells;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>
/// Dịch <see cref="SalaryPaymentPlan"/> thành danh sách ô cần ghi + số dòng cần chèn.
///
/// <para>Tách khỏi handler để test được toàn bộ phép tính toạ độ: sau khi chèn dòng, bảng 2
/// và dòng tổng cộng trôi xuống, tính sai một dòng là số tiền rơi vào ô khác.</para>
/// </summary>
public static class SalaryPaymentEditBuilder
{
    private const string Currency = "VND";

    public static IReadOnlyList<XlsxRowInsertion> BuildInsertions(SalaryPaymentPlan plan)
    {
        var insertions = new List<XlsxRowInsertion>();

        if (plan.ExtraRowsForInHouse > 0)
        {
            insertions.Add(new XlsxRowInsertion(
                Cells.Sheet,
                BeforeRow: Cells.Table1LastPrebakedRow + 1,
                Count: plan.ExtraRowsForInHouse,
                CloneStyleFromRow: Cells.Table1LastPrebakedRow));
        }

        if (plan.ExtraRowsForOthers > 0)
        {
            insertions.Add(new XlsxRowInsertion(
                Cells.Sheet,
                BeforeRow: Cells.Table2LastPrebakedRow + 1,
                Count: plan.ExtraRowsForOthers,
                CloneStyleFromRow: Cells.Table2LastPrebakedRow));
        }

        return insertions;
    }

    /// <param name="company">
    /// Tên + địa chỉ pháp nhân, lấy từ <c>ContractParty</c> — cấu hình chi trả chỉ giữ thông tin
    /// ngân hàng, không giữ địa chỉ doanh nghiệp.
    /// </param>
    public static IReadOnlyList<XlsxCellEdit> BuildEdits(
        SalaryPaymentPlan plan,
        CompanyPaymentSetting setting,
        SalaryPaymentCompanyProfile company,
        int periodYear,
        int periodMonth,
        DateTime valueDate)
    {
        var edits = new List<XlsxCellEdit>();

        AddHeader(edits, setting, company, periodYear, periodMonth, valueDate, plan);
        AddTable(edits, plan.InHouseBank, Cells.Table1FirstDataRow, includeBranchColumns: false);

        // Bảng 2 trôi xuống đúng bằng số dòng đã chèn cho bảng 1.
        var table2FirstRow = Cells.Table2FirstDataRow + (uint)plan.ExtraRowsForInHouse;
        AddTable(edits, plan.OtherBanks, table2FirstRow, includeBranchColumns: true);

        AddTotals(edits, plan);

        return edits;
    }

    private static void AddHeader(
        List<XlsxCellEdit> edits,
        CompanyPaymentSetting setting,
        SalaryPaymentCompanyProfile company,
        int periodYear,
        int periodMonth,
        DateTime valueDate,
        SalaryPaymentPlan plan)
    {
        void Put(Cells.LabelledField field, string? value)
            => edits.Add(XlsxCellEdit.OfText(Cells.Sheet, field.Cell, field.Render(value)));

        Put(Cells.DocumentNo, $"BL-{periodMonth:00}/{periodYear}");
        Put(Cells.IssueDate, valueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        Put(Cells.Branch, setting.SalaryBankBranch);

        // Tên trên lệnh chi ưu tiên tên chủ tài khoản đã khai (ngân hàng đối chiếu theo tên này),
        // thiếu thì mới rơi về tên pháp nhân.
        Put(Cells.CompanyName, setting.SalaryAccountName ?? company.LegalName);
        Put(Cells.CustomerCif, setting.SalaryCustomerCif);
        Put(Cells.CompanyAddress, company.Address);
        Put(Cells.CompanyPhone, setting.SalaryContactPhone);

        Put(Cells.DebitAccount, setting.SalaryAccountNo);
        Put(Cells.Currency, Currency);

        Put(Cells.TotalFigures, plan.TotalAmount.ToString("N0", CultureInfo.InvariantCulture));
        Put(Cells.TotalWords, VietnameseMoneyWords.ToWords(plan.TotalAmount));
        Put(Cells.ValueDate, valueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        Put(Cells.PaymentContent, $"Thanh toan luong thang {periodMonth:00}/{periodYear}");
    }

    private static void AddTable(
        List<XlsxCellEdit> edits,
        IReadOnlyList<SalaryPaymentBeneficiary> beneficiaries,
        uint firstDataRow,
        bool includeBranchColumns)
    {
        for (var i = 0; i < beneficiaries.Count; i++)
        {
            var row = firstDataRow + (uint)i;
            var b = beneficiaries[i];

            edits.Add(XlsxCellEdit.OfNumber(Cells.Sheet, Cells.ColOrdinal + row, i + 1));
            edits.Add(XlsxCellEdit.OfText(Cells.Sheet, Cells.ColFullName + row, b.FullName));
            edits.Add(XlsxCellEdit.OfText(Cells.Sheet, Cells.ColAccountNo + row, b.AccountNo));
            edits.Add(XlsxCellEdit.OfNumber(Cells.Sheet, Cells.ColAmount + row, b.Amount));
            edits.Add(XlsxCellEdit.OfText(Cells.Sheet, Cells.ColAddress + row, b.Address));
            edits.Add(XlsxCellEdit.OfText(Cells.Sheet, Cells.ColBank + row, b.BankCode));
            edits.Add(XlsxCellEdit.OfText(Cells.Sheet, Cells.ColNote + row, b.EmployeeCode));

            if (!includeBranchColumns) continue;

            edits.Add(XlsxCellEdit.OfText(Cells.Sheet, Cells.ColBranch + row, b.BankBranch));
            edits.Add(XlsxCellEdit.OfText(Cells.Sheet, Cells.ColProvince + row, b.ProvinceFormName));
        }
    }

    private static void AddTotals(List<XlsxCellEdit> edits, SalaryPaymentPlan plan)
    {
        // Dòng tổng trôi xuống theo tổng số dòng đã chèn của CẢ HAI bảng.
        var shift = (uint)(plan.ExtraRowsForInHouse + plan.ExtraRowsForOthers);

        var amountCell = Cells.TotalAmountCell[..1] + (Cells.TotalCountRow + shift);
        edits.Add(XlsxCellEdit.OfNumber(Cells.Sheet, amountCell, plan.TotalAmount));

        var countCell = Cells.TotalCountWords.Cell[..1] + (Cells.TotalCountRow + shift);
        edits.Add(XlsxCellEdit.OfText(Cells.Sheet, countCell,
            Cells.TotalCountWords.Render(VietnameseMoneyWords.ToPlainWords(plan.TotalCount) + " món")));

        var wordsCell = Cells.TotalAmountWords.Cell[..1] + (Cells.TotalAmountRow + shift);
        edits.Add(XlsxCellEdit.OfText(Cells.Sheet, wordsCell,
            Cells.TotalAmountWords.Render(VietnameseMoneyWords.ToWords(plan.TotalAmount))));
    }
}
