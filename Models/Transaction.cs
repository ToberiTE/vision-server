namespace Server.Models;

public record Transaction(int id, DateOnly date, float revenue, float expenses, float net_income);
