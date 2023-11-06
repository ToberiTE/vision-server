namespace Server.Models;

public record Project(int id, string customer, string description, Status status, double elapsed_time, double estimated_time, float price);

public enum Status
{
    Delivered,
    Completed,
    Active,
    Pending,
    Inactive,
    Cancelled
}