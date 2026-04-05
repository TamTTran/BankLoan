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

        var entity = new LoanProduct
        {
            ProductName = req.ProductName,
            MinAmount = req.MinAmount,
            MaxAmount = req.MaxAmount,
            AllowedTenorsJson = JsonSerializer.Serialize(req.AllowedTenors),
            MonthlyInterestRate = req.MonthlyInterestRate,
            ProcessingFee = req.ProcessingFee,
            RepaymentFrequency = req.RepaymentFrequency,
            Status = req.Status
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

        entity.ProductName = req.ProductName;
        entity.MinAmount = req.MinAmount;
        entity.MaxAmount = req.MaxAmount;
        entity.AllowedTenorsJson = JsonSerializer.Serialize(req.AllowedTenors);
        entity.MonthlyInterestRate = req.MonthlyInterestRate;
        entity.ProcessingFee = req.ProcessingFee;
        entity.Status = req.Status;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success(MapToDto(entity));
    }

    /// <summary>
    /// §13.1 Installment preview — flat monthly interest model
    /// monthly_principal = principal / tenor
    /// monthly_interest = principal * monthly_rate
    /// monthly_installment = monthly_principal + monthly_interest
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

        // §13.1 Flat model
        var monthlyPrincipal = Math.Round(req.Amount / req.TenorMonths, 2);
        var monthlyInterest = Math.Round(req.Amount * product.MonthlyInterestRate, 2);
        var monthlyInstallment = monthlyPrincipal + monthlyInterest;
        var totalRepayment = monthlyInstallment * req.TenorMonths;
        var totalInterest = Math.Round(monthlyInterest * req.TenorMonths, 2);

        return Result.Success(new InstallmentPreviewDto
        {
            LoanAmount = req.Amount,
            TenorMonths = req.TenorMonths,
            MonthlyInterestRate = product.MonthlyInterestRate,
            MonthlyPrincipal = monthlyPrincipal,
            MonthlyInterest = monthlyInterest,
            MonthlyInstallment = monthlyInstallment,
            TotalRepayment = Math.Round(totalRepayment, 2),
            TotalInterest = totalInterest,
            ProcessingFee = product.ProcessingFee
        });
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
        CreatedAt = p.CreatedAt
    };
}
