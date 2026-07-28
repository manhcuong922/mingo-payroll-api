namespace MPS.Domain.Enums.Payroll;

/// <summary>Hình thức đã sử dụng để nộp BHXH/TNCN thực tế bên ngoài Operix.</summary>
public enum PaymentMethod
{
    BankTransfer = 1,
    DirectPayment = 2,
    BhxhOnline = 3,
    ETax = 4,
}
