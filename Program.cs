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

if (builder.Environment.IsDevelopment())
{
    app.UseCors(policy =>
           policy.WithOrigins("http://localhost:8080")
    );
}
else
{
    app.UseMiddleware<ValidateOriginMiddleware>();
    app.UseCors(policy =>
           policy.WithOrigins("https://vision-client.azurewebsites.net")
    );
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGet("/transactions", async (VisionContext db, CancellationToken cancellationToken) =>
{
    var transactions = await db.Transaction.AsNoTracking().ToArrayAsync(cancellationToken: cancellationToken);
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

    var projects = await db.Project.AsNoTracking().ToArrayAsync();
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

    var dbSetProperty = db.GetType().GetProperty(selectedTable);
    if (dbSetProperty == null)
    {
        return Results.NotFound();
    }

    if (dbSetProperty.GetValue(db) is not IQueryable<dynamic> dbSet)
    {
        return Results.NotFound();
    }

    var data = dbSet.AsQueryable().AsNoTracking();

    if (!string.IsNullOrEmpty(groupBy))
    {
        IEnumerable<dynamic> result = selectedTable.ToLower() switch
        {
            "scatter_production" => Grouping(data, groupBy)
            .Select(g => new
            {
                Date = g.Key,
                Production_gross = g.Sum(p => ((Scatter_Production)p).production_gross),
                Fuel_consumption = g.Sum(p => ((Scatter_Production)p).fuel_consumption)
            }),

            "scatter_revenue" => Grouping(data, groupBy)
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(p => ((Scatter_Revenue)p).expenses),
                Net_income = g.Sum(p => ((Scatter_Revenue)p).net_income)
            }),

            "bar_revenue" => Grouping(data, groupBy)
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(p => ((Bar_Revenue)p).expenses),
                Net_income = g.Sum(p => ((Bar_Revenue)p).net_income)
            }),

            "radar_production" => Grouping(data, groupBy)
            .Select(g => new
            {
                Date = g.Key,
                Production = g.Sum(p => ((Radar_Production)p).production),
            }),

            "pie_production" => Grouping(data, groupBy)
            .Select(g => new
            {
                Date = g.Key,
                Production = g.Sum(p => ((Pie_Production)p).production),
            }),

            "transaction" => Grouping(data, groupBy)
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(p => ((Transaction)p).revenue),
                Expenses = g.Sum(p => ((Transaction)p).expenses),
                Net_income = g.Sum(p => ((Transaction)p).net_income)
            }),
            _ => (IEnumerable<dynamic>)Results.NotFound(),
        };
        return Results.Ok(result);
    }
    return Results.Ok(data);
});

IEnumerable<IGrouping<dynamic, dynamic>> Grouping(IEnumerable<dynamic> data, dynamic groupBy)
{
    switch (groupBy.ToLower())
    {
        case "year": return GroupByYear(data);
        case "quarter": return GroupByQuarter(data);
        case "month": return GroupByMonth(data);
        case "week": return GroupByWeek(data);
    };
    return (IEnumerable<IGrouping<dynamic, dynamic>>)Results.NotFound();
}

IEnumerable<IGrouping<dynamic, dynamic>> GroupByYear(IEnumerable<dynamic> data)
{
    return data
    .GroupBy(x => x.date.Year);
}

IEnumerable<IGrouping<dynamic, dynamic>> GroupByQuarter(IEnumerable<dynamic> data)
{
    return data
    .GroupBy(x => $"{x.date.Year} Q{((x.date.Month - 1) / 3) + 1}");
}

IEnumerable<IGrouping<dynamic, dynamic>> GroupByMonth(IEnumerable<dynamic> data)
{
    return data
    .GroupBy(x => $"{x.date.Year}-{x.date.Month:00}");
}

IEnumerable<IGrouping<dynamic, dynamic>> GroupByWeek(IEnumerable<dynamic> data)
{
    return data
    .GroupBy(x =>
       $"{x.date.Year} W{CultureInfo.InvariantCulture.Calendar
        .GetWeekOfYear
        (
            x.date.ToDateTime(TimeOnly.MinValue),
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday
        )}"
    );
}

app.MapGet("/dashboard/tables", (VisionContext db, CancellationToken cancellationToken) =>
{
    var tables = db.Model.GetEntityTypes()
    .Select(t => t.GetTableName())
    .Where(t => t != null && t.Contains('_') || t == "Transaction")
    .Distinct()
    .ToArray();
    return Results.Ok(tables);
});

app.MapGet("/sidenav/tables", (VisionContext db, CancellationToken cancellationToken) =>
{
    var tables = db.Model.GetEntityTypes()
    .Select(t => t.GetTableName())
    .Where(t => t != null && !t.Contains('_'))
    .Distinct()
    .ToArray();
    return Results.Ok(tables);
});

app.Run();
