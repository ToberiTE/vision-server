namespace Server.Models;

public record Project(int Id, string Customer, string Description, Status Status, double Elapsed_time, double Estimated_time, float Price);

public enum Status
{
    Delivered,
    Completed,
    Active,
    Pending,
    Inactive,
    Cancelled
}