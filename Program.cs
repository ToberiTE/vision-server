using Microsoft.EntityFrameworkCore;
using Server;
using Server.Models;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connection = string.Empty;
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddEnvironmentVariables().AddJsonFile("appsettings.Development.json");
    connection = builder.Configuration.GetConnectionString("LOCAL_SQL_CONNECTIONSTRING");
}
else
{
    connection = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING");
}

builder.Services.AddDbContext<VisionContext>(options =>
    options.UseSqlServer(connection));

var app = builder.Build();

app.UseMiddleware<ValidateOriginMiddleware>();

if (builder.Environment.IsDevelopment())
{
    app.UseCors(x => x
       .SetIsOriginAllowed(origin =>
       {
           if (origin == "http://localhost")
           {
               return true;
           }
           return false;
       })
    );
}
else
{
    app.UseCors(policy =>
           policy.WithOrigins("https://vision-client.azurewebsites.net")
    );
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGet("/transactions", async (VisionContext db, CancellationToken cancellationToken) =>
{
    var transactions = await db.Transaction.AsNoTracking().ToListAsync();
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

    var projects = await db.Project.AsNoTracking().ToListAsync();
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
                        var queryYear = dbSet.AsQueryable().AsNoTracking();
                        var groupedByYear = queryYear.AsEnumerable().GroupBy(x => x.date.Year).Select(g => new
                        {
                            Date = g.Key,
                            Expenses = g.Sum(x => (float)barExpenses?.GetValue(x)),
                            Net_income = g.Sum(x => (float)barNetIncome?.GetValue(x))
                        }).ToList();
                        return Results.Ok(groupedByYear);

                    case "Quarter":
                        var queryQuarter = dbSet.AsQueryable().AsNoTracking();
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
                        var queryMonth = dbSet.AsQueryable().AsNoTracking();
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
                        var queryWeek = dbSet.AsQueryable().AsNoTracking();
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
                        var query = dbSet.AsQueryable().AsNoTracking();
                        var groupedByYear = query.AsEnumerable().GroupBy(x => x.date.Year).Select(g => new
                        {
                            Date = g.Key,
                            Production = g.Sum(x => (float)radarProduction?.GetValue(x)),
                        }).ToList();
                        return Results.Ok(groupedByYear);

                    case "Quarter":
                        var queryQuarter = dbSet.AsQueryable().AsNoTracking();
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
                        var queryMonth = dbSet.AsQueryable().AsNoTracking();
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
                        var queryWeek = dbSet.AsQueryable().AsNoTracking();
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
                        var queryYear = dbSet.AsQueryable().AsNoTracking();
                        var groupedByYear = queryYear.AsEnumerable().GroupBy(x => x.date.Year).Select(g => new
                        {
                            Date = g.Key,
                            Production = g.Sum(x => (float)pieProduction?.GetValue(x)),
                        }).ToList();
                        return Results.Ok(groupedByYear);

                    case "Quarter":
                        var queryQuarter = dbSet.AsQueryable().AsNoTracking();
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
                        var queryMonth = dbSet.AsQueryable().AsNoTracking();
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
                        var queryWeek = dbSet.AsQueryable().AsNoTracking();
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
