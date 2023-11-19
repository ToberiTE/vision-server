
namespace Server.Models;

public record Bar_Revenue(int id, DateTime date, float expenses, float net_income) : IGroupByModel;

