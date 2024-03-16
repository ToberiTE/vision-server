namespace Server.Models;

public record Transaction(int Id, DateOnly Date, int Revenue, decimal Net_income, decimal Expenses);
