using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.LoanProduct;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanFlow.Application.Services;

public interface ILoanProductService
{
    Task<IEnumerable<LoanProductDto>> GetAllAsync();
    Task<Result<LoanProductDto>> GetByIdAsync(int id);
    Task<Result<LoanProductDto>> CreateAsync(CreateLoanProductRequest request);
    Task<Result<LoanProductDto>> UpdateAsync(int id, UpdateLoanProductRequest request);
    Task<Result<InstallmentPreviewDto>> PreviewInstallmentAsync(InstallmentPreviewRequest request);
}

public class LoanProductService : ILoanProductService
{
    private readonly LoanFlowDbContext _db;

    public LoanProductService(LoanFlowDbContext db) => _db = db;

    public async Task<IEnumerable<LoanProductDto>> GetAllAsync()
    {
        return await _db.LoanProducts
            .OrderBy(p => p.ProductName)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<Result<LoanProductDto>> GetByIdAsync(int id)
    {
        var p = await _db.LoanProducts.FindAsync(id);
        if (p is null) return Result.Failure<LoanProductDto>("Loan product not found.");
        return Result.Success(MapToDto(p));
    }

    public async Task<Result<LoanProductDto>> CreateAsync(CreateLoanProductRequest req)
    {
        // PRD-VAL-003: unique product name
        if (await _db.LoanProducts.AnyAsync(p => p.ProductName == req.ProductName))
            return Result.Failure<LoanProductDto>("PRD-VAL-003: Product name already exists.");

        // PRD-VAL-001: max >= min
        if (req.MaxAmount < req.MinAmount)
            return Result.Failure<LoanProductDto>("PRD-VAL-001: Max amount must be >= min amount.");

        // PRD-VAL-002: allowed tenors not empty
        if (req.AllowedTenors == null || req.AllowedTenors.Count == 0 || req.AllowedTenors.Any(t => t <= 0))
            return Result.Failure<LoanProductDto>("PRD-VAL-002: Product must have at least one valid tenor.");

        // Validate grace period doesn't exceed minimum tenor
        if (req.GracePeriodMonths > 0 && req.AllowedTenors.Any(t => t <= req.GracePeriodMonths))
            return Result.Failure<LoanProductDto>("PRD-VAL-004: Grace period must be less than all allowed tenors.");

        var entity = new LoanProduct
        {
            ProductName = req.ProductName,
            MinAmount = req.MinAmount,
            MaxAmount = req.MaxAmount,
            AllowedTenorsJson = JsonSerializer.Serialize(req.AllowedTenors),
            MonthlyInterestRate = req.MonthlyInterestRate,
            ProcessingFee = req.ProcessingFee,
            RepaymentFrequency = req.RepaymentFrequency,
            Status = req.Status,
            // Bank-grade fields
            ProductType = req.ProductType,
            InterestMethod = req.InterestMethod,
            GracePeriodMonths = req.GracePeriodMonths,
            RequiresCollateral = req.RequiresCollateral,
            MaxLtvRatio = req.MaxLtvRatio,
            EarlySettlementFeeRate = req.EarlySettlementFeeRate,
            LateFeeRate = req.LateFeeRate,
            LateFeeGraceDays = req.LateFeeGraceDays,
            PromotionalRate = req.PromotionalRate,
            PromotionalMonths = req.PromotionalMonths,
            Description = req.Description
        };
        _db.LoanProducts.Add(entity);
        await _db.SaveChangesAsync();
        return Result.Success(MapToDto(entity));
    }

    public async Task<Result<LoanProductDto>> UpdateAsync(int id, UpdateLoanProductRequest req)
    {
        var entity = await _db.LoanProducts.FindAsync(id);
        if (entity is null) return Result.Failure<LoanProductDto>("Loan product not found.");

        if (req.MaxAmount < req.MinAmount)
            return Result.Failure<LoanProductDto>("PRD-VAL-001: Max amount must be >= min amount.");

        if (req.AllowedTenors == null || req.AllowedTenors.Count == 0 || req.AllowedTenors.Any(t => t <= 0))
            return Result.Failure<LoanProductDto>("PRD-VAL-002: Product must have at least one valid tenor.");

        // PRD-VAL-003: unique name check on rename
        if (entity.ProductName != req.ProductName && await _db.LoanProducts.AnyAsync(p => p.ProductName == req.ProductName && p.Id != id))
            return Result.Failure<LoanProductDto>("PRD-VAL-003: Product name already exists.");

        if (req.GracePeriodMonths > 0 && req.AllowedTenors.Any(t => t <= req.GracePeriodMonths))
            return Result.Failure<LoanProductDto>("PRD-VAL-004: Grace period must be less than all allowed tenors.");

        entity.ProductName = req.ProductName;
        entity.MinAmount = req.MinAmount;
        entity.MaxAmount = req.MaxAmount;
        entity.AllowedTenorsJson = JsonSerializer.Serialize(req.AllowedTenors);
        entity.MonthlyInterestRate = req.MonthlyInterestRate;
        entity.ProcessingFee = req.ProcessingFee;
        entity.Status = req.Status;
        // Bank-grade fields
        entity.ProductType = req.ProductType;
        entity.InterestMethod = req.InterestMethod;
        entity.GracePeriodMonths = req.GracePeriodMonths;
        entity.RequiresCollateral = req.RequiresCollateral;
        entity.MaxLtvRatio = req.MaxLtvRatio;
        entity.EarlySettlementFeeRate = req.EarlySettlementFeeRate;
        entity.LateFeeRate = req.LateFeeRate;
        entity.LateFeeGraceDays = req.LateFeeGraceDays;
        entity.PromotionalRate = req.PromotionalRate;
        entity.PromotionalMonths = req.PromotionalMonths;
        entity.Description = req.Description;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success(MapToDto(entity));
    }

    /// <summary>
    /// Installment preview — supports both Flat and ReducingBalance models.
    /// Flat: monthly_principal = P/n, monthly_interest = P × r (constant)
    /// ReducingBalance: EMI = P × r × (1+r)^n / ((1+r)^n - 1)
    /// </summary>
    public async Task<Result<InstallmentPreviewDto>> PreviewInstallmentAsync(InstallmentPreviewRequest req)
    {
        var product = await _db.LoanProducts.FindAsync(req.ProductId);
        if (product is null) return Result.Failure<InstallmentPreviewDto>("Loan product not found.");

        if (product.Status != "Active")
            return Result.Failure<InstallmentPreviewDto>("APP-VAL-004: Product is not active.");

        // APP-VAL-001: amount within product range
        if (req.Amount < product.MinAmount || req.Amount > product.MaxAmount)
            return Result.Failure<InstallmentPreviewDto>(
                $"APP-VAL-001: Requested amount must be between {product.MinAmount:N0} and {product.MaxAmount:N0}.");

        // APP-VAL-002: tenor in allowed list
        var allowedTenors = product.GetAllowedTenors();
        if (!allowedTenors.Contains(req.TenorMonths))
            return Result.Failure<InstallmentPreviewDto>(
                $"APP-VAL-002: Tenor {req.TenorMonths}M is not allowed. Allowed: [{string.Join(",", allowedTenors)}]");

        if (product.InterestMethod == InterestMethod.ReducingBalance)
            return Result.Success(PreviewReducingBalance(product, req));

        return Result.Success(PreviewFlat(product, req));
    }

    // ── Flat preview (§13.1) ────────────────────────────────────────────────
    private static InstallmentPreviewDto PreviewFlat(LoanProduct product, InstallmentPreviewRequest req)
    {
        var monthlyPrincipal = Math.Round(req.Amount / req.TenorMonths, 2);
        var monthlyInterest = Math.Round(req.Amount * product.MonthlyInterestRate, 2);
        var monthlyInstallment = monthlyPrincipal + monthlyInterest;
        var totalRepayment = monthlyInstallment * req.TenorMonths;
        var totalInterest = Math.Round(monthlyInterest * req.TenorMonths, 2);

        return new InstallmentPreviewDto
        {
            LoanAmount = req.Amount,
            TenorMonths = req.TenorMonths,
            MonthlyInterestRate = product.MonthlyInterestRate,
            InterestMethod = "Flat",
            MonthlyPrincipal = monthlyPrincipal,
            MonthlyInterest = monthlyInterest,
            MonthlyInstallment = monthlyInstallment,
            TotalRepayment = Math.Round(totalRepayment, 2),
            TotalInterest = totalInterest,
            ProcessingFee = product.ProcessingFee,
            GracePeriodMonths = product.GracePeriodMonths,
            PromotionalRate = product.PromotionalRate,
            PromotionalMonths = product.PromotionalMonths
        };
    }

    // ── Reducing Balance preview (EMI formula) ──────────────────────────────
    private static InstallmentPreviewDto PreviewReducingBalance(LoanProduct product, InstallmentPreviewRequest req)
    {
        var principal = req.Amount;
        var rate = product.MonthlyInterestRate;
        var grace = product.GracePeriodMonths;
        var repaymentMonths = req.TenorMonths - grace;

        // EMI = P × r × (1+r)^n / ((1+r)^n - 1)
        decimal emi;
        if (rate == 0)
        {
            emi = Math.Round(principal / repaymentMonths, 2);
        }
        else
        {
            double r = (double)rate;
            double p = (double)principal;
            double n = repaymentMonths;
            emi = Math.Round((decimal)(p * r * Math.Pow(1 + r, n) / (Math.Pow(1 + r, n) - 1)), 2);
        }

        // Simulate total interest: grace period interest + EMI-based interest
        decimal totalInterest = 0;
        decimal remaining = principal;

        // Grace period: only interest, no principal
        for (int i = 0; i < grace; i++)
        {
            var effectiveRate = (product.PromotionalRate.HasValue && i < product.PromotionalMonths)
                ? product.PromotionalRate.Value : rate;
            totalInterest += Math.Round(remaining * effectiveRate, 2);
        }

        // Principal repayment period
        for (int i = grace; i < req.TenorMonths; i++)
        {
            var effectiveRate = (product.PromotionalRate.HasValue && i < product.PromotionalMonths)
                ? product.PromotionalRate.Value : rate;
            var interestPart = Math.Round(remaining * effectiveRate, 2);
            totalInterest += interestPart;
            var principalPart = emi - interestPart;
            if (principalPart > remaining) principalPart = remaining;
            remaining -= principalPart;
        }

        var firstMonthInterest = Math.Round(principal * rate, 2);
        var totalRepayment = principal + totalInterest;

        return new InstallmentPreviewDto
        {
            LoanAmount = principal,
            TenorMonths = req.TenorMonths,
            MonthlyInterestRate = rate,
            InterestMethod = "ReducingBalance",
            MonthlyPrincipal = Math.Round(emi - firstMonthInterest, 2),
            MonthlyInterest = firstMonthInterest,
            MonthlyInstallment = emi,
            TotalRepayment = Math.Round(totalRepayment, 2),
            TotalInterest = Math.Round(totalInterest, 2),
            ProcessingFee = product.ProcessingFee,
            GracePeriodMonths = grace,
            PromotionalRate = product.PromotionalRate,
            PromotionalMonths = product.PromotionalMonths
        };
    }

    private static LoanProductDto MapToDto(LoanProduct p) => new()
    {
        Id = p.Id,
        ProductName = p.ProductName,
        MinAmount = p.MinAmount,
        MaxAmount = p.MaxAmount,
        AllowedTenors = p.GetAllowedTenors(),
        MonthlyInterestRate = p.MonthlyInterestRate,
        ProcessingFee = p.ProcessingFee,
        RepaymentFrequency = p.RepaymentFrequency,
        Status = p.Status,
        CreatedAt = p.CreatedAt,
        // Bank-grade fields
        ProductType = p.ProductType,
        InterestMethod = p.InterestMethod,
        GracePeriodMonths = p.GracePeriodMonths,
        RequiresCollateral = p.RequiresCollateral,
        MaxLtvRatio = p.MaxLtvRatio,
        EarlySettlementFeeRate = p.EarlySettlementFeeRate,
        LateFeeRate = p.LateFeeRate,
        LateFeeGraceDays = p.LateFeeGraceDays,
        PromotionalRate = p.PromotionalRate,
        PromotionalMonths = p.PromotionalMonths,
        Description = p.Description
    };
}

