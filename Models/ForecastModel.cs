namespace Server.Models;
public record ForecastModel
{
    public dynamic? Ds { get; set; }
    public dynamic? Yhat { get; set; }
    public dynamic? Yhat_lower { get; set; }
    public dynamic? Yhat_upper { get; set; }
}