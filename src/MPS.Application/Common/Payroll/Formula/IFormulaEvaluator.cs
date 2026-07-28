namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Kết quả tính 1 biểu thức: giá trị số (hoặc text) + cây giải thích cách tính.</summary>
public readonly record struct EvaluationResult(decimal Value, ExplanationNode Explanation, string? Text = null)
{
    public bool IsTruthy => Text is null ? Value != 0m : !string.IsNullOrEmpty(Text);
}

/// <summary>Tính 1 nút AST trong ngữ cảnh 1 dòng nhân viên, trả giá trị + explanation.</summary>
public interface IFormulaEvaluator
{
    EvaluationResult Evaluate(FormulaNode node, FormulaEvaluationContext context);
}
