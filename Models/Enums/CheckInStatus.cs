namespace Staybnb.Models.Enums
{
    /// <summary>
    /// Progress of a guest through the multi-step check-in workflow.
    /// NotStarted -> InProgress -> Submitted (waiting for Host) -> Verified / Rejected.
    /// </summary>
    public enum CheckInStatus
    {
        NotStarted,
        InProgress,
        Submitted,
        Verified,
        Rejected
    }
}
