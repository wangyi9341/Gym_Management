# 项目整体架构说明

本项目为 **WPF 管理者端**，采用 **MVVM** 分层，目标是让管理者一打开系统即可“一目了然”地看到运营数据，并能快速完成日常维护。

## 1) 分层与职责

### `GymManager.Domain`（领域层 / Model）
- 定义实体：`Coach`、`PrivateTrainingMember`、`AnnualCardMember` 等
- 定义枚举：`Gender`、`AnnualCardStatus`
- 定义领域异常：`DomainValidationException`

> 该层不依赖 WPF，便于后续扩展到 Web/API/其它端。

### `GymManager.Data`（数据访问层）
- `GymDbContext`：EF Core DbContext（SQLite / SQL Server 通用）
- Repository：`CoachRepository`、`PrivateTrainingMemberRepository`、`AnnualCardMemberRepository`
- `DbInitializer`：首次启动自动建表 + SQLite 推荐 PRAGMA

### `GymManager.App`（应用层 + UI）
包含以下关键子层：

#### UI（View）
- `Views/`：各模块页面（Dashboard、教练、私教课会员、年卡会员）
- `Dialogs/`：新增/编辑弹窗（符合需求的“弹窗式操作”）

#### ViewModel（状态与命令）
- `ViewModels/`：页面 ViewModel，负责：
  - 列表数据加载
  - 命令（新增/编辑/删除/续费/新增缴费/消课）
  - 与服务层交互
  - 提供 UI 所需的可绑定属性

#### Services（业务用例）
业务逻辑集中在 `Services/`：
- `CoachService`：教练 CRUD + 工号唯一性校验
- `PrivateTrainingMemberService`：私教课会员 CRUD + 缴费/消课规则
- `AnnualCardMemberService`：年卡 CRUD + 续费规则
- `DashboardService`：仪表盘聚合查询

#### Infrastructure（基础设施）
- `DbContextProvider`：统一创建 DbContext（支持配置切换 SQLite / SQL Server）
- `AppEvents`：模块间简单事件总线（数据变更后触发刷新）
- `AppLogger`：全局日志写入 `Logs/app.log`

## 2) 到期提醒实现

到期提醒规则（默认）：
- “即将到期”：截止日期在 **未来 3 天内（含今天）**
- “已过期”：截止日期早于今天

实现方式：
- `DashboardViewModel` 每 10 分钟自动刷新（`DispatcherTimer`）
- 首次启动与数据变更（新增/编辑/续费）也会触发刷新
- 主界面顶部横幅突出显示到期会员（点击可跳转到年卡模块并自动筛选“即将到期”）

## 3) 数据验证与异常处理

### 数据验证
- 弹窗 ViewModel 基于 `ObservableValidator` + DataAnnotations
- 服务层再次校验关键规则（例如：消课不能让剩余课程为负）

### 异常处理
- 页面操作异常：弹框提示（不导致程序退出）
- 全局异常：捕获并写入 `Logs/app.log`，同时弹框提示

