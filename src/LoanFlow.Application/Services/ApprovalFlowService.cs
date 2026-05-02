using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.ApprovalFlow;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanFlow.Application.Services;

public class ApprovalFlowService : IApprovalFlowService
{
    private readonly LoanFlowDbContext _db;
    private readonly IServiceProvider _serviceProvider;

    public ApprovalFlowService(LoanFlowDbContext db, IServiceProvider serviceProvider)
    {
        _db = db;
        _serviceProvider = serviceProvider;
    }

    public async Task<Result<ApprovalRequestResponse>> SubmitRequestAsync(SubmitApprovalRequest request, int userId)
    {
        var pendingRequest = new PendingApprovalRequest
        {
            ActionType = request.ActionType,
            Payload = request.Payload,
            RequestedByUserId = userId,
            Status = "Pending"
        };

        _db.PendingApprovalRequests.Add(pendingRequest);
        
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = "ApprovalRequestSubmitted",
            EntityType = "PendingApprovalRequest",
            NewValuesJson = AuditHelper.ToJson(new { actionType = request.ActionType, payload = request.Payload })
        });

        await _db.SaveChangesAsync(); // save to generate Id
        
        // Fetch to get navigation properties if needed, or map manually
        var user = await _db.Users.FindAsync(userId);
        
        return Result.Success(MapToDto(pendingRequest, user?.FullName ?? "Unknown"));
    }

    public async Task<IEnumerable<ApprovalRequestResponse>> GetPendingRequestsAsync()
    {
        var requests = await _db.PendingApprovalRequests
            .Include(p => p.RequestedBy)
            .Where(p => p.Status == "Pending")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return requests.Select(p => MapToDto(p, p.RequestedBy?.FullName ?? "Unknown"));
    }

    public async Task<Result<ApprovalRequestResponse>> ApproveRequestAsync(int id, int checkerUserId)
    {
        return await ProcessRequestAsync(id, checkerUserId, true, null);
    }

    public async Task<Result<ApprovalRequestResponse>> RejectRequestAsync(int id, ProcessApprovalRequest request, int checkerUserId)
    {
        if (string.IsNullOrWhiteSpace(request.RejectionReason))
            return Result.Failure<ApprovalRequestResponse>("Rejection reason is required.");

        return await ProcessRequestAsync(id, checkerUserId, false, request.RejectionReason);
    }

    private async Task<Result<ApprovalRequestResponse>> ProcessRequestAsync(int id, int checkerUserId, bool isApproved, string? rejectionReason)
    {
        var req = await _db.PendingApprovalRequests
            .Include(p => p.RequestedBy)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (req == null)
            return Result.Failure<ApprovalRequestResponse>("Request not found.");

        if (req.Status != "Pending")
            return Result.Failure<ApprovalRequestResponse>($"Cannot process request. Status is already {req.Status}.");

        // Simple Segregation of Duties: Maker cannot be Checker
        if (req.RequestedByUserId == checkerUserId)
            return Result.Failure<ApprovalRequestResponse>("Maker cannot be the Checker of their own request.");

        req.Status = isApproved ? "Approved" : "Rejected";
        req.ApprovedByUserId = checkerUserId;
        req.DecidedAt = DateTime.UtcNow;
        req.RejectionReason = rejectionReason;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = checkerUserId,
            ActionCode = isApproved ? "ApprovalRequestApproved" : "ApprovalRequestRejected",
            EntityType = "PendingApprovalRequest",
            EntityId = id,
            NewValuesJson = AuditHelper.ToJson(new { reason = rejectionReason })
        });

        await _db.SaveChangesAsync();

        // Delegate to specific services based on ActionType
        if (isApproved)
        {
            var executionResult = await ExecuteApprovedActionAsync(req, checkerUserId);
            if (!executionResult.IsSuccess)
            {
                return Result.Failure<ApprovalRequestResponse>(executionResult.Error ?? "Approved action execution failed.");
            }
        }

        var checker = await _db.Users.FindAsync(checkerUserId);

        var dto = MapToDto(req, req.RequestedBy?.FullName ?? "Unknown");
        dto.ApprovedByUserName = checker?.FullName;

        return Result.Success(dto);
    }

    private async Task<Result> ExecuteApprovedActionAsync(PendingApprovalRequest req, int checkerUserId)
    {
        using var scope = _serviceProvider.CreateScope();

        return req.ActionType switch
        {
            "ManualAdjustment" => await scope.ServiceProvider
                .GetRequiredService<ILoanAdjustmentService>()
                .ApplyApprovedAdjustmentAsync(req.Id, checkerUserId),
            "LoanRestructure" => await ExecuteLifecycleActionAsync<RestructureApprovalPayload>(
                req.Payload,
                async payload =>
                {
                    var result = await scope.ServiceProvider
                    .GetRequiredService<ILoanLifecycleService>()
                    .RestructureAsync(payload.LoanId, payload.Request, checkerUserId);
                    return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Loan restructure failed.");
                }),
            "LoanReschedule" => await ExecuteLifecycleActionAsync<RescheduleApprovalPayload>(
                req.Payload,
                async payload =>
                {
                    var result = await scope.ServiceProvider
                    .GetRequiredService<ILoanLifecycleService>()
                    .RescheduleAsync(payload.LoanId, payload.Request, checkerUserId);
                    return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Loan reschedule failed.");
                }),
            "LoanWriteOff" => await ExecuteLifecycleActionAsync<WriteOffApprovalPayload>(
                req.Payload,
                async payload =>
                {
                    var result = await scope.ServiceProvider
                    .GetRequiredService<ILoanLifecycleService>()
                    .WriteOffAsync(payload.LoanId, payload.Request, checkerUserId);
                    return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Loan write-off failed.");
                }),
            "LateFeeWaive" => await ExecuteLifecycleActionAsync<LateFeeWaiveApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<ILateFeeService>()
                    .WaiveLateFeeAsync(payload.LateFeeId, checkerUserId)),
            "LateFeeAdjust" => await ExecuteLifecycleActionAsync<AdjustFeeApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<IReversalService>()
                    .AdjustFeeAsync(payload.LoanId, payload.Request, checkerUserId)),
            "LoanDisburse" => await ExecuteLifecycleActionAsync<DisburseApprovalPayload>(
                req.Payload,
                async payload =>
                {
                    var result = await scope.ServiceProvider
                        .GetRequiredService<ILoanDisbursementService>()
                        .DisburseAsync(payload.LoanId, payload.Request, checkerUserId);
                    return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Loan disbursement failed.");
                }),
            "LoanEarlySettle" => await ExecuteLifecycleActionAsync<EarlySettleApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<ILoanPaymentService>()
                    .EarlySettleAsync(payload.LoanId, payload.Request, checkerUserId)),
            "SuspenseApply" => await ExecuteLifecycleActionAsync<ApplySuspenseApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<ILoanPaymentService>()
                    .ApplySuspenseAsync(payload.SuspenseId, checkerUserId)),
            "ExposureOverride" => await ExecuteLifecycleActionAsync<ExposureOverrideApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<IExposureLimitService>()
                    .OverrideExposureAsync(payload.CustomerId, payload.Reason, checkerUserId)),
            "CollateralRelease" => await ExecuteLifecycleActionAsync<CollateralReleaseApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<ICollateralLifecycleService>()
                    .ReleaseCollateralAsync(payload.CollateralId, checkerUserId)),
            "CollateralSeize" => await ExecuteLifecycleActionAsync<CollateralSeizeApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<ICollateralLifecycleService>()
                    .SeizeCollateralAsync(payload.CollateralId, checkerUserId)),
            "AccountingPeriodClose" => await ExecuteLifecycleActionAsync<AccountingPeriodCloseApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<IAccountingPeriodService>()
                    .ClosePeriodAsync(payload.Year, payload.Month, checkerUserId, payload.Notes)),
            "AccountingPeriodReopen" => await ExecuteLifecycleActionAsync<AccountingPeriodReopenApprovalPayload>(
                req.Payload,
                payload => scope.ServiceProvider
                    .GetRequiredService<IAccountingPeriodService>()
                    .ReopenPeriodAsync(payload.Year, payload.Month, checkerUserId)),
            _ => Result.Failure($"HIGH-005: Unknown approval action type '{req.ActionType}'. No handler registered.")
        };
    }

    private static async Task<Result> ExecuteLifecycleActionAsync<TPayload>(
        string payloadJson,
        Func<TPayload, Task<Result>> executor)
    {
        TPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TPayload>(
                payloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return Result.Failure("Invalid approval payload format.");
        }

        if (payload is null)
            return Result.Failure("Approval payload is required.");

        return await executor(payload);
    }

    private static ApprovalRequestResponse MapToDto(PendingApprovalRequest entity, string requestedByName)
    {
        return new ApprovalRequestResponse
        {
            Id = entity.Id,
            ActionType = entity.ActionType,
            Payload = entity.Payload,
            Status = entity.Status,
            RequestedByUserId = entity.RequestedByUserId,
            RequestedByUserName = requestedByName,
            CreatedAt = entity.CreatedAt,
            ApprovedByUserId = entity.ApprovedByUserId,
            DecidedAt = entity.DecidedAt,
            RejectionReason = entity.RejectionReason
        };
    }
}
