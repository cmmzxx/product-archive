# TopSolid 明道云集成助手

第一阶段 WPF 应用，当前接入「产品研发PLM-测试版 → 产品档案」产品列表，支持名称/编码搜索、分页和刷新。

**当前状态：已探测并通过独立 HTTP 请求核验真实产品接口；C# 客户端、WPF 与测试代码尚未在 Windows 编译或执行。TopSolid 尚未实机验证。**

## 开发约定

- Mac 只编写代码，不安装 .NET SDK、不还原 NuGet、不编译或运行测试。
- Windows 负责安装工具链、还原依赖、构建、测试、WPF 调试、发布和 TopSolid 验证。
- 使用 .NET 10 LTS、C#、WPF、MVVM；不引用 TopSolid SDK。

## 仓库与协作

远程仓库：[cmmzxx/product-archive](https://github.com/cmmzxx/product-archive)，主分支为 `main`。项目协作规则见 [AGENTS.md](AGENTS.md)。

首次推送完成后，可在 Windows 获取代码：

```powershell
git clone https://github.com/cmmzxx/product-archive.git
cd product-archive
```

`.gitattributes` 统一跨平台换行，`.editorconfig` 约定编辑格式；本地凭据、配置、日志、构建产物和测试报告通过 `.gitignore` 排除。

提交到 `main`、发起 Pull Request 或手动触发工作流时，GitHub Actions 在 Windows 上还原依赖、构建并运行假 HTTP 测试，保留 TRX 报告 7 天；不配置真实明道云令牌，也不访问真实业务接口。工作流只有推送到 GitHub 后才会执行。

## 工程结构

```text
src/
  TopSolidMingdao.Core/             产品、图纸、分页模型与只读服务契约
  TopSolidMingdao.Infrastructure/   演示数据、v3 HTTP 客户端、认证与产品字段映射
  TopSolidMingdao.Desktop/          WPF 界面、ViewModel、命令、配置与本地日志
tests/
  TopSolidMingdao.Tests/            Windows 上执行的服务与 ViewModel 测试
scripts/                           Windows 验证和发布脚本
docs/                              需求与 Windows 开发说明
```

## 当前功能

- 左侧产品列表、名称/编码搜索、400 ms 防抖、分页和刷新。
- 真实模式只读取产品名称、编码和记录 ID；没有系列字段，不显示虚构的系列/类别列。
- 真实模式选择产品不查询图纸，右侧明确提示“关联图纸暂未启用”。图纸浏览仅保留在演示模式。
- 请求取消和版本校验，覆盖旧数据、旧错误、旧加载状态及 A → B → A 切换。
- 显式演示模式：125 条产品；默认列表第一条有 57 个图纸条目；部分产品无图纸。
- v3 产品列表 HTTP 查询，Bearer + HAP-Appid 认证、集中字段映射、30 秒超时和明确的失败提示。
- 元数据格式化、滚动本地错误日志、窗口退出时取消请求。

上述为代码范围说明，并非 Windows 功能验收通过记录。接口核验记录和字段 ID 见 [明道云接入说明](docs/mingdao-connection.md)。

## 在 Windows 开始

安装支持 .NET 10 的 Visual Studio 和“.NET 桌面开发”工作负载，或对应 SDK / Build Tools。具体步骤见 [Windows 开发与验证](docs/windows-development.md)。

在 Windows PowerShell、仓库根目录执行：

```powershell
.\scripts\verify.ps1
.\scripts\start.ps1
```

首次启动脚本会以隐藏输入方式读取连接令牌，只传给当前启动进程，不写入文件或命令行。也可以事先设置 `TOPSOLID_MINGDAO_TOKEN`。Visual Studio 启动时需在其启动环境中提供同一变量。测试工程引用 WPF 程序，因此也只在 Windows 执行。

## 数据模式

配置位于 Desktop 项目的 `appsettings.json`，构建和发布时复制到 EXE 目录；程序不依赖当前工作目录。

```json
{
  "DataMode": "Mingdao",
  "Mingdao": {
    "ApiBaseUrl": "https://vithk.cn/api/",
    "DisplayName": "产品研发PLM-测试版 · 产品档案",
    "AppId": "a0f730b1-5450-48a0-98df-82382a95ca13",
    "ProductWorksheetId": "6ad8a8c3cb2cc6d673abc2f5",
    "ProductNameFieldId": "6ad8a8c3cb2cc6d673abc2fa",
    "ProductCodeFieldId": "6ad8a8c3cb2cc6d673abc2fb"
  }
}
```

- `Mingdao`：默认真实产品列表模式；未提供令牌时在产品区域提示配置凭据，不回退为演示数据。
- `Demo`：将 DataMode 改为 Demo 后使用演示数据，界面持续显示演示标识，不联网；可直接 `dotnet run`，不需要令牌。
- 配置缺失或模式无效：提示启动失败，不静默采用默认模式。

配置只包含地址和字段等非秘密信息。真实令牌不写进配置、Git、安装包或启动参数；Mac 已有 MCP 凭据没有复制到项目。环境变量是当前 Windows 联调方式，正式分发前需确认令牌范围并接入适当的秘密存储。

## 后续接入

1. 在 Windows 编译、执行测试，并使用授权令牌验证实际 WPF 产品列表。
2. 核实发给设计人员的令牌权限与有效期；客户端只读不代表令牌本身只有只读权限。
3. 当前核验到的 14 条记录编码均为空；按“—”展示，不修改明道云数据。
4. 后续再接图纸关系、正式凭据存储和 TopSolid 外部启动验证。

当前没有下载、预览、上传、CAD/PDM 操作，也未创建空 CadBridge 项目。

完整范围和验收要求见 [V1.1 需求文档](docs/requirements-v1.1.md)。
