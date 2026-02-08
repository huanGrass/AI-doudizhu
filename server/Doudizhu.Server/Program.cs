using Doudizhu.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TableRoomService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => Results.Ok(new { message = "Doudizhu room server is running." }));

app.MapGet("/api/tables", (TableRoomService service) =>
{
    return Results.Ok(new TableListResponse(service.GetTables()));
});

app.MapPost("/api/tables/{tableId:int}/join", (int tableId, JoinTableRequest request, TableRoomService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.PlayerName))
    {
        return Results.BadRequest(new { error = "playerName is required." });
    }

    JoinTableResult result = service.JoinTable(tableId, request.PlayerName.Trim());
    if (!result.Exists)
    {
        return Results.NotFound(new { error = "table not found." });
    }

    if (!result.Success)
    {
        return Results.Conflict(new { error = "table is full." });
    }

    return Results.Ok(result.Table);
});

app.Run();
