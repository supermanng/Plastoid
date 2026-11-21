using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Identity.Domain;

public enum DataExportStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
}

/// <summary>NDPA data-subject access request (story A3). Fulfilled by the DataExportJob within 24 h.</summary>
public sealed class DataExportRequest : Entity
{
    private DataExportRequest()
    {
    }

    private DataExportRequest(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid UserId { get; private set; }
    public DataExportStatus Status { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? JsonBlobKey { get; private set; }
    public string? PdfBlobKey { get; private set; }
    public string? FailureReason { get; private set; }
    public int Attempts { get; private set; }

    public static DataExportRequest Create(Guid tenantId, Guid userId, DateTime utcNow)
        => new(NewId(), tenantId) { UserId = userId, Status = DataExportStatus.Pending, RequestedAt = utcNow };

    public void Complete(string jsonKey, string pdfKey, DateTime utcNow)
    {
        Status = DataExportStatus.Completed;
        JsonBlobKey = jsonKey;
        PdfBlobKey = pdfKey;
        CompletedAt = utcNow;
        FailureReason = null;
    }

    public void RecordFailure(string reason, int maxAttempts, DateTime utcNow)
    {
        Attempts++;
        FailureReason = reason.Length > 500 ? reason[..500] : reason;
        if (Attempts >= maxAttempts)
        {
            Status = DataExportStatus.Failed;
            CompletedAt = utcNow;
        }
    }
}
