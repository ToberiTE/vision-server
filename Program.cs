using Microsoft.EntityFrameworkCore;
using Python.Runtime;
using Server;
using Server.Models;
using System.Globalization;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var pythonDLLPath = string.Empty;
var pythonScriptPath = string.Empty;
var connection = string.Empty;

if (builder.Environment.IsDevelopment())
{
    pythonDLLPath = builder.Configuration.GetSection("Python")["DLL_PATH"];
    pythonScriptPath = builder.Configuration.GetSection("Python")["SCRIPT_PATH"];
    builder.Configuration.AddEnvironmentVariables().AddJsonFile("appsettings.Development.json");
    connection = builder.Configuration.GetConnectionString("LOCAL_SQL_CONNECTIONSTRING");
}
else
{
    pythonDLLPath = Environment.GetEnvironmentVariable("PYTHON_DLL_PATH");
    pythonScriptPath = Environment.GetEnvironmentVariable("PYTHON_SCRIPT_PATH");
    connection = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING");
}
Runtime.PythonDLL = pythonDLLPath;

async Task<dynamic> ForecastData(List<string> dates, List<double> revenues, int? period)
{
    try
    {
        if (!PythonEngine.IsInitialized)
        {
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
        }

        string? forecasts = string.Empty;
        IEnumerable<dynamic>? result = null;
        List<Dictionary<string, dynamic>>? forecastList = [];

        await Task.Run(() =>
        {
            using (Py.GIL())
            {
                PythonEngine.Exec($@"import sys; sys.path.insert(0, '{pythonScriptPath?.Replace("\\", "\\\\")}')");

                using PyObject mod = Py.Import("forecastservice");
                dynamic forecast_data = mod.GetAttr("forecast_data");

                using PyObject obj = forecast_data(dates, revenues, period);

                forecasts = obj.ToString();
                forecastList = JsonSerializer.Deserialize<List<Dictionary<string, dynamic>>>(forecasts ?? "");
                result = forecastList?.Select(t => new
                {
                    Date = DateTime.Parse((string)t["ds"].GetString()).ToString("yyyy-MM-dd"),
                    Yhat = Math.Round(t["yhat"].GetDouble()),
                    Yhat_lower = Math.Round(t["yhat_lower"].GetDouble()),
                    Yhat_upper = Math.Round(t["yhat_upper"].GetDouble())
                });
            }
        });

        return result ?? [];
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.ToString());
    }
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
    return Results.Created($"/transactions/{model.Id}", model);
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
    return Results.Created($"/projects/{model.Id}", model);
});

app.MapGet("/selectedTable", async (string selectedTable, string? groupBy, string? shouldForecast, int? period, VisionContext db, CancellationToken cancellationToken) =>
{
    dynamic? dbSetProperty = null;
    IEnumerable<dynamic>? forecastData = [];

    dbSetProperty = db.GetType().GetProperty(selectedTable);

    if (dbSetProperty == null)
    {
        return Results.NotFound();
    }

    if (dbSetProperty.GetValue(db) is not IQueryable<dynamic> dbSet)
    {
        return Results.NotFound();
    }

    var data = dbSet.AsQueryable().AsNoTracking();

    if (shouldForecast == "true" && period > 0)
    {
        var x = await data.ToListAsync(cancellationToken);
        var dates = x.Select(x => (string)x.Date.ToString("yyyy-MM-dd")).ToList();
        var revenues = x.Select(x => (double)x.Revenue).ToList();

        forecastData = await ForecastData(dates, revenues, period);

        return Results.Ok(forecastData);
    }

    if (!string.IsNullOrEmpty(groupBy))
    {
        IEnumerable<dynamic> result = selectedTable.ToLower() switch
        {
            "scatter_production" => (await Grouping(data, groupBy))
            .Select(g => new
            {
                Date = g.Key,
                Production_gross = g.Sum(p => ((Scatter_Production)p).Production_gross),
                Fuel_consumption = g.Sum(p => ((Scatter_Production)p).Fuel_consumption)
            }),

            "scatter_revenue" => (await Grouping(data, groupBy))
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(p => ((Scatter_Revenue)p).Expenses),
                Net_income = g.Sum(p => ((Scatter_Revenue)p).Net_income)
            }),

            "bar_revenue" => (await Grouping(data, groupBy))
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(p => ((Bar_Revenue)p).Expenses),
                Net_income = g.Sum(p => ((Bar_Revenue)p).Net_income)
            }),

            "radar_production" => (await Grouping(data, groupBy))
            .Select(g => new
            {
                Date = g.Key,
                Production = g.Sum(p => ((Radar_Production)p).Production),
            }),

            "pie_production" => (await Grouping(data, groupBy))
            .Select(g => new
            {
                Date = g.Key,
                Production = g.Sum(p => ((Pie_Production)p).Production),
            }),

            "transaction" => (await Grouping(data, groupBy))
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(p => ((Transaction)p).Revenue),
                Expenses = g.Sum(p => ((Transaction)p).Expenses),
                Net_income = g.Sum(p => ((Transaction)p).Net_income),
            }),
            _ => [],
        };
        return Results.Ok(result);
    }

    var returnData = await data.ToListAsync(cancellationToken);
    return Results.Ok(returnData);
});

Task<IEnumerable<IGrouping<dynamic, dynamic>>> Grouping(IEnumerable<dynamic> data, string groupBy)
{
    switch (groupBy.ToLower())
    {
        case "year": return GroupByYear(data);
        case "quarter": return GroupByQuarter(data);
        case "month": return GroupByMonth(data);
        case "week": return GroupByWeek(data);
    }
    ;
    return Task.FromResult(Enumerable.Empty<IGrouping<dynamic, dynamic>>());
}

async Task<IEnumerable<IGrouping<dynamic, dynamic>>> GroupByYear(IEnumerable<dynamic> data)
{
    return await Task.Run(() => data
    .GroupBy(x => x.Date.Year));
}

async Task<IEnumerable<IGrouping<dynamic, dynamic>>> GroupByQuarter(IEnumerable<dynamic> data)
{
    return await Task.Run(() => data
    .GroupBy(x => $"{x.Date.Year} Q{((x.Date.Month - 1) / 3) + 1}"));
}

async Task<IEnumerable<IGrouping<dynamic, dynamic>>> GroupByMonth(IEnumerable<dynamic> data)
{
    return await Task.Run(() => data
    .GroupBy(x => $"{x.Date.Year}-{x.Date.Month:00}"));
}

async Task<IEnumerable<IGrouping<dynamic, dynamic>>> GroupByWeek(IEnumerable<dynamic> data)
{
    return await Task.Run(() => data
    .GroupBy(x =>
       $"{x.Date.Year} W{CultureInfo.InvariantCulture.Calendar
        .GetWeekOfYear
        (
            x.Date.ToDateTime(TimeOnly.MinValue),
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday
        )}"
    ));
}

app.MapGet("/dashboard/tables", (VisionContext db, CancellationToken cancellationToken) =>
{
    var tables = db.Model.GetEntityTypes()
    .Select(t => t.GetTableName())
    .Where(t => t != null && t.Contains('_') || t == "Transaction")
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

