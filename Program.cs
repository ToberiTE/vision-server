using Microsoft.EntityFrameworkCore;
using Server;
using Server.Models;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string activeConnectionPath = "Config/active-connection.json";

string selectedConnection = "";

if (File.Exists(activeConnectionPath))
{
    string active = System.Text.Json.JsonSerializer.Deserialize<string>(File.ReadAllText(activeConnectionPath)) ?? "";
    if (!string.IsNullOrEmpty(active))
    {
        selectedConnection = active;
    }
}
else
{
    selectedConnection = "";
}

List<string> dbConnections = new();
string connectionsPath = "Config/connections.json";

if (File.Exists(connectionsPath))
{
    string connectionStrings = File.ReadAllText(connectionsPath);
    if (!string.IsNullOrEmpty(connectionStrings))
    {
        dbConnections = System.Text.Json.JsonSerializer.Deserialize<List<string>>(connectionStrings) ?? new List<string>();
    }
}
else
{
    File.Create(connectionsPath).Close();
    string connectionStrings = File.ReadAllText(connectionsPath);
    dbConnections = System.Text.Json.JsonSerializer.Deserialize<List<string>>(connectionStrings) ?? new List<string>();
}

builder.Services.AddDbContext<VisionContext>(options =>
    options.UseSqlServer(selectedConnection));

var app = builder.Build();

app.UseCors(x => x
    .AllowAnyMethod()
    .AllowAnyHeader()
    .SetIsOriginAllowed(origin => true)
    .AllowCredentials());

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGet("/connections", () => { return dbConnections; });

app.MapPost("/connections", (string connString) =>
{
    string path = "Config/connections.json";
    dbConnections?.Add(connString);
    string json = System.Text.Json.JsonSerializer.Serialize(dbConnections);

    if (File.Exists(path))
    {
        File.WriteAllText(path, json);
    }
    else
    {
        File.Create(path).Close();
    }

    File.WriteAllText(path, json);
});

app.MapPost("/connections/set", (string conn) =>
{
    string path = "Config/active-connection.json";
    if (!string.IsNullOrEmpty(conn))
    {
        selectedConnection = conn;
        string json = System.Text.Json.JsonSerializer.Serialize(selectedConnection);

        if (File.Exists(path))
        {
            File.WriteAllText(path, json);
        }
        else
        {
            File.Create(path).Close();
        }

        File.WriteAllText(path, json);
    }
});

app.MapGet("/connections/active", () =>
{
    string path = "Config/active-connection.json";
    string active = System.Text.Json.JsonSerializer.Deserialize<string>(File.ReadAllText(path)) ?? "";
    if (!string.IsNullOrEmpty(active))
    {
        return active;
    }
    return "";
});

app.MapDelete("/connections/delete", (string conn) =>
{
    string path = "Config/connections.json";
    dbConnections?.Remove(conn);
    List<string> connections = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? new List<string>();
    connections?.Remove(conn);
    File.WriteAllText("connections.json", System.Text.Json.JsonSerializer.Serialize(connections));
    if (conn == selectedConnection) { selectedConnection = ""; }
}
);

app.MapGet("/transactions", async (VisionContext db) =>
{
    var transactions = await db.Transaction.ToListAsync();
    return Results.Json(transactions);
});

app.MapPost("/transactions", async (Transaction model, VisionContext db, CancellationToken cancellationToken) =>
{
    db.Transaction?.Add(model);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/transactions/{model.id}", model);
});

app.MapGet("/projects", async (VisionContext db) =>
{

    var projects = await db.Project.ToListAsync();
    return Results.Json(projects);
});

app.MapPost("/projects", async (Project model, VisionContext db, CancellationToken cancellationToken) =>
{
    db.Project?.Add(model);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/projects/{model.id}", model);
});


app.MapGet("/selectedTable", (string selectedTable, string? groupBy, VisionContext db, CancellationToken cancellationToken) =>
{
    var type = Type.GetType(selectedTable);
    var dbSetProperty = db.GetType().GetProperty(selectedTable);
    if (dbSetProperty == null)
    {
        return Results.NotFound();
    }
    var dbSet = (IEnumerable<dynamic>?)dbSetProperty.GetValue(db);
    if (dbSet == null)
    {
        return Results.NotFound();
    }

    if (!string.IsNullOrEmpty(groupBy))
    {
        switch (selectedTable)
        {
            case "Bar_Revenue":
                var bar_revenue = dbSet.AsQueryable().ElementType;
                var barDate = bar_revenue.GetProperty("date");
                var barExpenses = bar_revenue.GetProperty("expenses");
                var barNetIncome = bar_revenue.GetProperty("net_income");

                switch (groupBy)
                {
                    case "Year":
                        var queryYear = dbSet.AsQueryable();
                        var groupedByYear = queryYear.AsEnumerable().GroupBy(x => x.date.Year).Select(g => new
                        {
                            Date = g.Key,
                            Expenses = g.Sum(x => (float)barExpenses?.GetValue(x)),
                            Net_income = g.Sum(x => (float)barNetIncome?.GetValue(x))
                        }).ToList();
                        return Results.Ok(groupedByYear);

                    case "Quarter":
                        var queryQuarter = dbSet.AsQueryable();
                        var groupedByQuarter = queryQuarter.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              Quarter = ((x.date.Month - 1) / 3) + 1
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year} Q{g.Key.Quarter}",
                              Expenses = g.Sum(x => (float)barExpenses?.GetValue(x)),
                              Net_income = g.Sum(x => (float)barNetIncome?.GetValue(x))
                          }).ToList();
                        return Results.Ok(groupedByQuarter);

                    case "Month":
                        var queryMonth = dbSet.AsQueryable();
                        var groupedByMonth = queryMonth.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              x.date.Month
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year}-{g.Key.Month:00}",
                              Expenses = g.Sum(x => (float)barExpenses?.GetValue(x)),
                              Net_income = g.Sum(x => (float)barNetIncome?.GetValue(x))
                          }).ToList();
                        return Results.Ok(groupedByMonth);

                    case "Week":
                        var queryWeek = dbSet.AsQueryable();
                        var groupedByWeek = queryWeek.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              Week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                              x.date,
                              CalendarWeekRule.FirstFourDayWeek,
                              DayOfWeek.Monday
                            )
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year} W{g.Key.Week}",
                              Expenses = g.Sum(x => (float)barExpenses?.GetValue(x)),
                              Net_income = g.Sum(x => (float)barNetIncome?.GetValue(x))
                          }).ToList();
                        return Results.Ok(groupedByWeek);
                }
                break;

            case "Radar_Production":
                var radar_production = dbSet.AsQueryable().ElementType;
                var radarDate = radar_production.GetProperty("date");
                var radarProduction = radar_production.GetProperty("production");

                switch (groupBy)
                {
                    case "Year":
                        var query = dbSet.AsQueryable();
                        var groupedByYear = query.AsEnumerable().GroupBy(x => x.date.Year).Select(g => new
                        {
                            Date = g.Key,
                            Production = g.Sum(x => (float)radarProduction?.GetValue(x)),
                        }).ToList();
                        return Results.Ok(groupedByYear);

                    case "Quarter":
                        var queryQuarter = dbSet.AsQueryable();
                        var groupedByQuarter = queryQuarter.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              Quarter = ((x.date.Month - 1) / 3) + 1
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year} Q{g.Key.Quarter}",
                              Production = g.Sum(x => (float)radarProduction?.GetValue(x)),
                          }).ToList();
                        return Results.Ok(groupedByQuarter);

                    case "Month":
                        var queryMonth = dbSet.AsQueryable();
                        var groupedByMonth = queryMonth.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              x.date.Month
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year}-{g.Key.Month:00}",
                              Production = g.Sum(x => (float)radarProduction?.GetValue(x)),
                          }).ToList();
                        return Results.Ok(groupedByMonth);

                    case "Week":
                        var queryWeek = dbSet.AsQueryable();
                        var groupedByWeek = queryWeek.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              Week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                              x.date,
                              CalendarWeekRule.FirstFourDayWeek,
                              DayOfWeek.Monday
                            )
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year} W{g.Key.Week}",
                              Production = g.Sum(x => (float)radarProduction?.GetValue(x)),
                          }).ToList();
                        return Results.Ok(groupedByWeek);
                }
                break;

            case "Pie_Production":
                var pie_production = dbSet.AsQueryable().ElementType;
                var pieDate = pie_production.GetProperty("date");
                var pieProduction = pie_production.GetProperty("production");

                switch (groupBy)
                {
                    case "Year":
                        var queryYear = dbSet.AsQueryable();
                        var groupedByYear = queryYear.AsEnumerable().GroupBy(x => x.date.Year).Select(g => new
                        {
                            Date = g.Key,
                            Production = g.Sum(x => (float)pieProduction?.GetValue(x)),
                        }).ToList();
                        return Results.Ok(groupedByYear);

                    case "Quarter":
                        var queryQuarter = dbSet.AsQueryable();
                        var groupedByQuarter = queryQuarter.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              Quarter = ((x.date.Month - 1) / 3) + 1
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year} Q{g.Key.Quarter}",
                              Production = g.Sum(x => (float)pieProduction?.GetValue(x)),
                          }).ToList();
                        return Results.Ok(groupedByQuarter);

                    case "Month":
                        var queryMonth = dbSet.AsQueryable();
                        var groupedByMonth = queryMonth.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              x.date.Month
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year}-{g.Key.Month:00}",
                              Production = g.Sum(x => (float)pieProduction?.GetValue(x)),
                          }).ToList();
                        return Results.Ok(groupedByMonth);

                    case "Week":
                        var queryWeek = dbSet.AsQueryable();
                        var groupedByWeek = queryWeek.AsEnumerable()
                          .GroupBy(x => new
                          {
                              x.date.Year,
                              Week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                              x.date,
                              CalendarWeekRule.FirstFourDayWeek,
                              DayOfWeek.Monday
                            )
                          })
                          .Select(g => new
                          {
                              Date = $"{g.Key.Year} W{g.Key.Week}",
                              Production = g.Sum(x => (float)pieProduction?.GetValue(x)),
                          }).ToList();
                        return Results.Ok(groupedByWeek);
                }
                break;
            default:
                break;
        }
    }
    else
    {
        var data = dbSet.Cast<dynamic>().ToList();
        return Results.Ok(data);
    }
    return Results.NotFound();
});

app.MapGet("/dashboard/tables", (VisionContext db, CancellationToken cancellationToken) =>
{
    var tables = db.Model.GetEntityTypes()
    .Select(t => t.GetTableName())
    .Where(t => t != null && t.Contains('_'))
    .Distinct()
    .ToList();
    return Results.Ok(tables);
});

app.MapGet("/sidenav/tables", (VisionContext db, CancellationToken cancellationToken) =>
{
    var tables = db.Model.GetEntityTypes()
    .Select(t => t.GetTableName())
    .Where(t => t != null && !t.Contains('_'))
    .Distinct()
    .ToList();
    return Results.Ok(tables);
});

app.Run();
