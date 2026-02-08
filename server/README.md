# 斗地主房间服务端

这是一个基于 `ASP.NET Core (.NET 8)` 的最小房间服务，用于联机大厅的桌子列表与入桌。

## 启动

```powershell
dotnet run --project server/Doudizhu.Server
```

默认地址（`launchSettings`）：`http://localhost:5014`

## API

1. `GET /api/tables`
- 返回当前桌子与玩家列表。

2. `POST /api/tables/{tableId}/join`
- 请求体：

```json
{
  "playerName": "Madlee"
}
```

- 结果：
  - `200`：成功加入，返回最新桌子信息
  - `404`：桌子不存在
  - `409`：桌子已满

## 数据说明

- 当前数据为进程内内存数据，服务重启后会重置。
- 每桌最多 `3` 人。
- 相同玩家名重复加入同一桌时会视为幂等请求。
