namespace Server.Models;

public record Transaction(int id, string date, float revenue, float expenses, float net_income);
