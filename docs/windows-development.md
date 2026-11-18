# Windows 开发与验证

## 环境边界

本项目约定 Mac 只编辑源文件。所有 SDK 安装、NuGet 还原、编译、测试、运行和发布均在 Windows 执行。本轮未执行以下命令，未确认 Windows 构建或 UI 结果。

## 首次准备

1. 安装支持 .NET 10 的 Visual Studio，并选择“.NET 桌面开发”工作负载；命令行构建也可以使用相应 SDK / Build Tools。
2. 在首次代码推送完成后，从 `https://github.com/cmmzxx/product-archive.git` 克隆或拉取代码到 Windows；也可复制完整源文件目录。
3. 在仓库根目录运行 `dotnet --info`，确认使用稳定版 .NET 10 SDK。`global.json` 以 `10.0.100` 为最低 SDK 基线，允许同一主次版本的较新 feature band。
4. 首次还原测试依赖需要能够访问 NuGet，或使用公司已配置的包源；不在项目中写入包源凭据。

演示模式不需要明道云密钥、网络、TopSolid 安装或 SDK。使用独立程序完成界面检查后，再进行 TopSolid 菜单验证。

## 构建与测试

可执行 `scripts/verify.ps1`；脚本额外将 TRX 测试报告写入 `TestResults`。主要命令如下：

```powershell
dotnet restore .\TopSolidMingdao.sln
dotnet build .\TopSolidMingdao.sln --configuration Release --no-restore
dotnet test .\tests\TopSolidMingdao.Tests\TopSolidMingdao.Tests.csproj --configuration Release --no-build --no-restore
.\scripts\start.ps1
```

测试覆盖 v3 HTTP 认证、产品映射、服务端搜索、分页、异常与取消，以及演示服务、缺失元数据和过期请求处理。测试均使用假 HTTP handler，不要求真实凭据，也不请求网络；测试项目使用 Windows 目标，不能用 Mac 运行代替。

真实产品列表检查（默认模式）：

- start.ps1 会隐藏输入连接令牌；也可预先提供 TOPSOLID_MINGDAO_TOKEN。不要输入 `Bearer ` 前缀，不要将令牌放进 appsettings.json。
- 顶部显示“产品研发PLM-测试版 · 产品档案”，读取名称、编码；当前核验有 14 条，数据变化后以实际返回为准。
- 搜索“温控阀”应按当前业务数据筛选；当前核验有 2 条。接口中的编码为空时显示“—”。
- 未提供令牌或使用失效令牌时，应有明确错误，不能回退为演示数据。
- 选择产品后不请求图纸，右侧提示暂未启用。
- 进一步按 [明道云接入说明](mingdao-connection.md) 检查分页、刷新、网络错误与日志。

演示模式检查（修改 DataMode 为 Demo 后，可直接 dotnet run）：

- 顶部明确显示演示标识；启动后显示产品但不自动选择。
- 搜索 `PRD-0125`、中文产品名和不存在关键词；检查防抖、Enter 和空状态。
- 默认列表第一条加载 50 个条目，“加载更多”后共 57 个；部分产品显示无图纸。
- 连续切换产品、翻页和刷新，右侧内容与当前选择一致。
- 从另一个工作目录启动 EXE，并检查 100%、150% DPI 布局、长名称、表格横向滚动和窗口关闭。

## 发布

```powershell
.\scripts\publish.ps1
```

产物目录为 `artifacts\win-x64`，入口为 `TopSolidMingdao.exe`。该脚本发布自包含文件夹，分发时需要复制整个目录，不能只复制 EXE。自包含运行时安全补丁需随后续应用发布维护。

正常运行不需要管理员权限。本地日志位于：

```text
%LOCALAPPDATA%\TopSolidMingdao\logs\app.log
```

日志超过约 1 MiB 时轮转为 `app.previous.log`，只保留这两份；记录异常类型和调用栈，不记录异常消息、请求正文、密钥或附件 URL。

## TopSolid 验证

准确的菜单配置方式仍待目标 TopSolid 版本确认，当前不提供猜测的配置文件或命令。验证后应记录版本、配置步骤、含空格路径处理、配置移除方式，以及关闭工具后 TopSolid 与当前文档是否正常。

第一阶段程序不连接 TopSolid 进程，也不操作 CAD 文件。

## 当前未验证项

- NuGet 包还原、C# / XAML 编译与测试执行结果。
- WPF 数据绑定、布局、DPI、键盘操作和实际 UI 线程行为。
- Windows 自包含发布与部署后的启动、日志和退出行为。
- Windows 中 C# 客户端读取真实明道云的运行结果与目标设计人员令牌权限。独立 HTTP 核验已完成，详见接入说明。
- 关联图纸查询与分页仍未接入。
- 目标 TopSolid 外部程序启动入口。
