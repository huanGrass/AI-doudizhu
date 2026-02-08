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
        return Results.BadRequest(new ErrorResponse("playerName is required."));
    }

    JoinTableResult result = service.JoinTable(tableId, request.PlayerName.Trim());
    if (!result.Exists)
    {
        return Results.NotFound(new ErrorResponse("table not found."));
    }

    if (!result.Success)
    {
        return Results.Conflict(new ErrorResponse("table is full."));
    }

    return Results.Ok(result.Table);
});

app.MapGet("/api/tables/{tableId:int}/state", (int tableId, string? playerName, TableRoomService service) =>
{
    TableStateResult result = service.GetTableState(tableId, playerName);
    if (!result.Exists || result.State == null)
    {
        return Results.NotFound(new ErrorResponse("table not found."));
    }

    return Results.Ok(result.State);
});

app.MapPost("/api/tables/{tableId:int}/ready", (int tableId, ReadyRequest request, TableRoomService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.PlayerName))
    {
        return Results.BadRequest(new ErrorResponse("playerName is required."));
    }

    ReadyResult result = service.SetReady(tableId, request.PlayerName.Trim(), request.Ready);
    if (!result.Exists)
    {
        return Results.NotFound(new ErrorResponse("table not found."));
    }

    if (!result.PlayerInTable)
    {
        return Results.Conflict(new ErrorResponse("player not in table."));
    }

    if (!result.Success || result.State == null)
    {
        return Results.Conflict(new ErrorResponse("ready update failed."));
    }

    return Results.Ok(result.State);
});

app.MapPost("/api/tables/{tableId:int}/bid", (int tableId, BidRequest request, TableRoomService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.PlayerName))
    {
        return Results.BadRequest(new ErrorResponse("playerName is required."));
    }

    BidResult result = service.SubmitBid(tableId, request.PlayerName.Trim(), request.CallLandlord);
    if (!result.Exists)
    {
        return Results.NotFound(new ErrorResponse("table not found."));
    }

    if (!result.PlayerInTable)
    {
        return Results.Conflict(new ErrorResponse(result.Error ?? "player not in table."));
    }

    if (!result.Success || result.State == null)
    {
        return Results.Conflict(new ErrorResponse(result.Error ?? "bid failed."));
    }

    return Results.Ok(result.State);
});

app.MapPost("/api/tables/{tableId:int}/play", (int tableId, PlayRequest request, TableRoomService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.PlayerName))
    {
        return Results.BadRequest(new ErrorResponse("playerName is required."));
    }

    PlayResult result = service.SubmitPlay(tableId, request.PlayerName.Trim(), request.Pass, request.Cards);
    if (!result.Exists)
    {
        return Results.NotFound(new ErrorResponse("table not found."));
    }

    if (!result.PlayerInTable)
    {
        return Results.Conflict(new ErrorResponse(result.Error ?? "player not in table."));
    }

    if (!result.Success || result.State == null)
    {
        return Results.Conflict(new ErrorResponse(result.Error ?? "play failed."));
    }

    return Results.Ok(result.State);
});

app.MapPost("/api/tables/{tableId:int}/leave", (int tableId, LeaveRequest request, TableRoomService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.PlayerName))
    {
        return Results.BadRequest(new ErrorResponse("playerName is required."));
    }

    LeaveResult result = service.LeaveTable(tableId, request.PlayerName.Trim());
    if (!result.Exists)
    {
        return Results.NotFound(new ErrorResponse("table not found."));
    }

    if (!result.PlayerInTable)
    {
        return Results.Conflict(new ErrorResponse("player not in table."));
    }

    if (!result.Success || result.State == null)
    {
        return Results.Conflict(new ErrorResponse("leave failed."));
    }

    return Results.Ok(result.State);
});

app.MapPost("/api/tables/{tableId:int}/restart", (int tableId, RestartRequest request, TableRoomService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.PlayerName))
    {
        return Results.BadRequest(new ErrorResponse("playerName is required."));
    }

    RestartResult result = service.RequestRestart(tableId, request.PlayerName.Trim());
    if (!result.Exists)
    {
        return Results.NotFound(new ErrorResponse("table not found."));
    }

    if (!result.PlayerInTable)
    {
        return Results.Conflict(new ErrorResponse(result.Error ?? "player not in table."));
    }

    if (!result.Success || result.State == null)
    {
        return Results.Conflict(new ErrorResponse(result.Error ?? "restart failed."));
    }

    return Results.Ok(result.State);
});

app.Run();
