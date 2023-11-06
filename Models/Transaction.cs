namespace Server.Models;

public record Transaction(int id, DateTime date, float revenue, float expenses, float net_income);
