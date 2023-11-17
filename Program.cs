using Microsoft.EntityFrameworkCore;
using Server;
using Server.Models;
using System.Globalization;
using System.Reflection;

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
    var transactions = await db.Transaction.AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
    return Results.Json(transactions);
});

app.MapPost("/transactions", async (Transaction model, VisionContext db, CancellationToken cancellationToken) =>
{
    db.Transaction?.Add(model);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/transactions/{model.id}", model);
});

app.MapGet("/projects", async (VisionContext db, CancellationToken cancellationToken) =>
{

    var projects = await db.Project.AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
    return Results.Json(projects);
});

app.MapPost("/projects", async (Project model, VisionContext db, CancellationToken cancellationToken) =>
{
    db.Project?.Add(model);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/projects/{model.id}", model);
});

app.MapGet("/selectedTable", async (string selectedTable, string? groupBy, VisionContext db, CancellationToken cancellationToken) =>
{
    var dbSet = GetDbSet(db, selectedTable);
    if (dbSet == null)
    {
        return Results.NotFound();
    }

    if (!string.IsNullOrEmpty(groupBy))
    {
        var elementType = dbSet.AsQueryable().ElementType;
        var dateProperty = elementType.GetProperty("date");
        if (dateProperty == null)
        {
            return Results.NotFound("Date property not found.");
        }

        var groupByProperty = elementType.GetProperty(groupBy.ToLowerInvariant());
        if (groupByProperty == null)
        {
            return Results.NotFound($"Property for grouping by '{groupBy}' not found.");
        }

        var groupedData = GroupData(dbSet, groupBy, dateProperty, groupByProperty);
        return Results.Ok(groupedData);
    }
    else
    {
        var data = await dbSet.Cast<dynamic>().ToListAsync(cancellationToken);
        return Results.Ok(data);
    }
});

IQueryable<dynamic>? GetDbSet(VisionContext db, string selectedTable)
{
    var dbSetProperty = db.GetType().GetProperty(selectedTable);
    return dbSetProperty?.GetValue(db) as IQueryable<dynamic>;
}

dynamic GroupData(IEnumerable<dynamic> dbSet, string groupBy, PropertyInfo dateProperty, PropertyInfo groupByProperty)
{
    var query = dbSet.AsQueryable().AsNoTracking();
    return groupBy switch
    {
        "Year" => GroupByDatePart(query, dateProperty, groupByProperty, x => x.Year),
        "Quarter" => GroupByDatePart(query, dateProperty, groupByProperty, x => (x.Month - 1) / 3 + 1),
        "Month" => GroupByDatePart(query, dateProperty, groupByProperty, x => x.Month),
        "Week" => GroupByDatePart(query, dateProperty, groupByProperty, x => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(x, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)),
        _ => Results.BadRequest($"Invalid groupBy parameter: {groupBy}"),
    };
}

dynamic GroupByDatePart(IQueryable<dynamic> query, PropertyInfo dateProperty, PropertyInfo groupByProperty, Func<DateTime, int> datePartSelector)
{
    return query
        .AsEnumerable()
        .GroupBy(x => new
        {
            DatePart = datePartSelector((DateTime)dateProperty.GetValue(x))
        })
        .Select(g => new
        {
            Date = g.Key.DatePart,
            Sum = g.Sum(x => Convert.ToSingle(groupByProperty.GetValue(x)))
        })
        .ToArray();
}

app.MapGet("/dashboard/tables", (VisionContext db) =>
{
    var tables = db.Model.GetEntityTypes()
    .Select(t => t.GetTableName())
    .Where(t => t != null && t.Contains('_'))
    .Distinct()
    .ToArray();
    return Results.Ok(tables);
});

app.MapGet("/sidenav/tables", (VisionContext db) =>
{
    var tables = db.Model.GetEntityTypes()
    .Select(t => t.GetTableName())
    .Where(t => t != null && !t.Contains('_'))
    .Distinct()
    .ToArray();
    return Results.Ok(tables);
});

app.Run();
