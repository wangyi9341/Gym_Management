# 数据库设计说明

本项目支持两种数据库：

1. `SQLite`：单机部署、开箱即用（默认推荐）
2. `SQL Server`：多终端共享数据库、并发更强

对应建表脚本：

- `database/sqlite_schema.sql`
- `database/sqlserver_schema.sql`

## 表结构摘要

### 1) Coaches（教练）
- 关键字段：`EmployeeNo`（工号，主键）、`Name`
- 审计字段：`CreatedAt`、`UpdatedAt`

### 2) PrivateTrainingMembers（私教课会员）
- 关键字段：`Name`、`Gender`、`Phone`
- 费用/课程字段：`PaidAmount`、`TotalSessions`、`UsedSessions`
- 计算字段（由应用层计算）：`RemainingSessions = TotalSessions - UsedSessions`

### 3) PrivateTrainingFeeRecords（私教课费用记录）
- 归属：`MemberId -> PrivateTrainingMembers.Id`
- 关键字段：`Amount`、`PaidAt`

### 4) PrivateTrainingSessionRecords（私教课课程消耗记录）
- 归属：`MemberId -> PrivateTrainingMembers.Id`
- 关键字段：`SessionsUsed`、`UsedAt`

### 5) AnnualCardMembers（年卡会员）
- 关键字段：`Name`、`Gender`、`Phone`、`StartDate`、`EndDate`
- 到期提醒核心索引：`IX_AnnualCardMembers_EndDate`

## 关系设计

- `PrivateTrainingMembers (1) -> (N) PrivateTrainingFeeRecords`
- `PrivateTrainingMembers (1) -> (N) PrivateTrainingSessionRecords`

详见：`docs/ERD.md`

## 索引设计（到期提醒）

到期提醒的查询条件为：

- 只看“即将到期”：`EndDate` 在未来 3 天内
- 或“已过期”：`EndDate < 今天`

因此必须为 `AnnualCardMembers.EndDate` 建立索引（脚本已包含）。

SQLite 查询示例：
```sql
-- 即将到期（未来 3 天内，含今天）
SELECT *
FROM AnnualCardMembers
WHERE date(EndDate) >= date('now')
  AND date(EndDate) <= date('now', '+3 day')
ORDER BY date(EndDate) ASC;
```

SQL Server 查询示例：
```sql
DECLARE @today date = CAST(GETDATE() AS date);

SELECT *
FROM dbo.AnnualCardMembers
WHERE EndDate >= @today
  AND EndDate <= DATEADD(day, 3, @today)
ORDER BY EndDate ASC;
```

