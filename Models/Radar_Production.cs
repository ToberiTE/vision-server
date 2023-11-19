
namespace Server.Models;

public record Radar_Production(int id, DateTime date, float production) : IGroupByModel;

