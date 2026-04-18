using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

public class NotificationDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? DeeplinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface INotificationService
{
    Task GenerateDueRemindersAsync(DateOnly businessDate, int daysBefore = 3);
    Task GenerateOverdueRemindersAsync(DateOnly businessDate);
    Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(int userId, bool unreadOnly, int page = 1, int pageSize = 50);
    Task<Result> MarkAsReadAsync(int notificationId, int userId);
    Task<int> GetUnreadCountAsync(int userId);
}

public class NotificationService : INotificationService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILogger<NotificationService> _logger;
    private readonly IBusinessClock _businessClock;
    private readonly IBusinessCalendarService _businessCalendar;

    public NotificationService(
        LoanFlowDbContext db,
        ILogger<NotificationService> logger,
        IBusinessClock businessClock,
        IBusinessCalendarService businessCalendar)
    {
        _db = db;
        _logger = logger;
        _businessClock = businessClock;
        _businessCalendar = businessCalendar;
    }

    public async Task GenerateDueRemindersAsync(DateOnly businessDate, int daysBefore = 3)
    {
        var targetDate = businessDate.AddDays(daysBefore);

        var (startUtc, endUtc) = _businessCalendar.GetBusinessDayUtcRange(businessDate);

        // C5 Fix: Get IDs of loans that already have a DueReminder for this target date
        var alreadyNotifiedLoanIds = await _db.Notifications
            .Where(n => n.Type == NotificationType.DueReminder
                     && n.CreatedAt >= startUtc
                     && n.CreatedAt < endUtc)
            .Select(n => n.LoanId)
            .ToListAsync();

        // Safety: valid user IDs to prevent FK violation
        var validUserIds = await _db.Users.Select(u => u.Id).ToListAsync();

        var dueSchedules = await _db.RepaymentSchedules
            .Include(s => s.Loan)
            .ThenInclude(l => l.Customer)
            .Where(s => s.DueDate == targetDate
                     && s.Status != InstallmentStatus.Paid
                     && s.Loan.Status == LoanStatus.Active
                     && !alreadyNotifiedLoanIds.Contains(s.LoanId)) // Dedup
            .ToListAsync();

        int count = 0;
        foreach (var s in dueSchedules)
        {
            if (!validUserIds.Contains(s.Loan.Customer.AssignedToUserId)) continue;
            _db.Notifications.Add(new Notification
            {
                UserId = s.Loan.Customer.AssignedToUserId,
                LoanId = s.LoanId,
                CustomerId = s.Loan.CustomerId,
                Type = NotificationType.DueReminder,
                Title = "Nhắc nhở thanh toán sắp đến hạn",
                Message = $"Khoản gốc và lãi tiếp theo của khoản vay {s.Loan.LoanAccountNo} là {s.RemainingDueAmount:N0} VND, đến hạn vào ngày {s.DueDate:dd/MM/yyyy}.",
                DeeplinkUrl = $"/loans/{s.LoanId}"
            });
            count++;
        }

        if (count > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Generated {Count} due reminders (deduped).", count);
        }
    }

    public async Task GenerateOverdueRemindersAsync(DateOnly businessDate)
    {
        var (startUtc, endUtc) = _businessCalendar.GetBusinessDayUtcRange(businessDate);

        // C5 Fix: Get loan IDs that already have an overdue reminder today
        var alreadyNotifiedLoanIds = await _db.Notifications
            .Where(n => n.Type == NotificationType.OverdueReminder
                     && n.CreatedAt >= startUtc
                     && n.CreatedAt < endUtc)
            .Select(n => n.LoanId)
            .ToListAsync();

        // Safety: valid user IDs to prevent FK violation
        var validUserIds = await _db.Users.Select(u => u.Id).ToListAsync();

        // Process in batches of 500 to avoid memory bomb
        const int batchSize = 500;
        int skip = 0;
        int totalGenerated = 0;

        while (true)
        {
            var overdueLoans = await _db.Loans
                .Include(l => l.Customer)
                .Where(l => l.Status == LoanStatus.Overdue
                         && !alreadyNotifiedLoanIds.Contains(l.Id))
                .OrderBy(l => l.Id)
                .Skip(skip)
                .Take(batchSize)
                .ToListAsync();

            if (overdueLoans.Count == 0) break;

            int batchCount = 0;
            foreach (var l in overdueLoans)
            {
                if (!validUserIds.Contains(l.Customer.AssignedToUserId)) continue;
                _db.Notifications.Add(new Notification
                {
                    UserId = l.Customer.AssignedToUserId,
                    LoanId = l.Id,
                    CustomerId = l.CustomerId,
                    Type = NotificationType.OverdueReminder,
                    Title = "Cảnh báo khoản vay quá hạn!",
                    Message = $"Khoản vay {l.LoanAccountNo} của bạn hiện đang có tổng nợ quá hạn {l.OutstandingAmount:N0} VND. Vui lòng thanh toán ngay để tránh phát sinh thêm phí phạt.",
                    DeeplinkUrl = $"/loans/{l.Id}"
                });
                batchCount++;
            }

            if (batchCount > 0) await _db.SaveChangesAsync();
            totalGenerated += batchCount;
            skip += batchSize;

            if (overdueLoans.Count < batchSize) break;
        }

        if (totalGenerated > 0)
            _logger.LogInformation("Generated {Count} overdue reminders (deduped, batched).", totalGenerated);
    }

    // H1 Fix: Paginated notifications
    public async Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(int userId, bool unreadOnly, int page = 1, int pageSize = 50)
    {
        var query = _db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                DeeplinkUrl = n.DeeplinkUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<NotificationDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result> MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notification == null) return Result.Failure("Notification not found.");

        notification.IsRead = true;
        notification.ReadAt = _businessClock.UtcNow;

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }
}
