# GYM力量健身管理系统（管理者端）

面向健身房管理者/管理员的桌面端管理系统（不含会员端 App）。

## 技术栈
- 开发语言：C#
- UI：WPF（Material Design 风格）
- 架构：MVVM（CommunityToolkit.Mvvm）
- 数据库：SQLite（默认）/ SQL Server（可切换）
- ORM：EF Core

## 目录结构
- `src/GymManager.App`：WPF UI（Views / ViewModels / Dialogs / Services）
- `src/GymManager.Domain`：实体模型与枚举（Model）
- `src/GymManager.Data`：DbContext / Repository / 初始化
- `database/`：建表 SQL 脚本（SQLite / SQL Server）
- `docs/`：ERD 与数据库设计说明

## 运行方式
1. 使用 Visual Studio 打开 `GymManager.sln`
2. 将启动项目设为 `GymManager.App`
3. 运行（首次启动会自动创建数据库表）

默认 SQLite 数据库位置：
- 安装/发布后默认存放于：`%LocalAppData%\GymManager\Data\gym.db`

配置文件：
- 开发调试：`src/GymManager.App/appsettings.json` 会复制到输出目录
- 安装/发布：程序会在首次运行时生成到：`%LocalAppData%\GymManager\appsettings.json`

## 数据库切换（SQLite / SQL Server）
在 `appsettings.json` 中修改：
```json
{
  "Database": {
    "Provider": "SQLite",
    "Sqlite": { "DbPath": "Data\\gym.db" },
    "SqlServer": { "ConnectionString": "Server=localhost;Database=GymManager;Trusted_Connection=True;TrustServerCertificate=True" }
  }
}
```

## 模块
- 教练管理：增删改查（工号为主键）
- 私教课会员：增删改查 + 费用记录 + 消课记录（剩余课程自动计算）
- 年卡会员：增删改查 + 到期提醒（默认 3 天）+ 续费
- 首页仪表盘：关键运营数据卡片 + 到期/低剩余列表

## 异常与日志
- 全局异常会写入：`%LocalAppData%\GymManager\Logs\app.log`

## 打包成单个 EXE（推荐）
> 说明：.NET 单文件发布会把依赖打包进一个 `.exe`，但首次运行仍会在同目录创建 `Data/gym.db`、`Logs/app.log` 等数据文件。

### 方式 1：一键脚本
在项目根目录执行：
```powershell
.\scripts\publish-single.ps1 -Runtime win-x64 -Configuration Release
```
输出目录：
- `src/GymManager.App\bin\Release\net8.0-windows\win-x64\publish\GymManager.App.exe`

### 方式 2：dotnet 命令
```powershell
dotnet publish .\src\GymManager.App\GymManager.App.csproj -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true
```

### 常见注意事项
- `win-x64` 适合绝大多数电脑；如果是 32 位系统请改为 `win-x86`。
- 若把 EXE 放在无写权限目录（例如 `C:\Program Files`），程序可能无法创建数据库文件；建议放在可写目录（如桌面/工作目录）。
  - 已改为写入 `%LocalAppData%\GymManager\...`，因此安装到 `Program Files` 也可正常运行。

## 生成“真正的安装包”（Setup.exe，含卸载与快捷方式）
> 使用 Inno Setup 6 生成 Windows 常见安装向导（Start Menu/桌面快捷方式/卸载入口）。

1) 安装 Inno Setup 6：
- https://jrsoftware.org/isinfo.php

2) 一键构建安装包（在项目根目录执行）：
```powershell
.\scripts\build-installer.ps1 -Runtime win-x64 -Configuration Release
```
输出目录：
- `dist\GYM力量健身管理系统_Setup_*.exe`

安装位置（全局安装 / Program Files）：
- 默认：`%ProgramFiles%\GYM力量健身管理系统\GymManager.App.exe`
