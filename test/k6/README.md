# k6 压测脚本（EventSourcingBankAccount）

本目录提供基于 k6 的压测脚本，用于测量当前 API 的 QPS 上限与在递增压力下的退化点。

目标端点（来自 Controller 路由）：
- POST /api/BankAccount                创建账户
- POST /api/BankAccount/{id}/deposit   存款
- POST /api/BankAccount/{id}/withdraw  取款
- GET  /api/BankAccount/{id}/balance   查询余额

默认 Base URL：
- https://localhost:7067（开发环境默认；亦支持 http://localhost:5145）。
- 脚本已开启 insecureSkipTLSVerify；PowerShell 脚本也会自动加上对应参数。

文件说明：
- helpers.js         常用函数（创建账号、存款、取款、余额查询）。
- steady-rps.js      稳态恒定到达率压测（constant-arrival-rate）。
- step-rps.js        阶梯递增到达率压测（ramping-arrival-rate），用于探测极限。
- run.ps1            Windows 下一键运行脚本，封装常用参数。
- steady-summary.json / step-summary.json  每次运行后自动导出的结果汇总。

准备工作：
1) 启动 API（确保数据库已创建；项目已内置 EnsureCreated）。
2) 安装 k6（并加入 PATH）：https://k6.io/docs/get-started/installation/

快速使用（建议通过 PowerShell 运行 run.ps1）：
- 稳态恒定到达率：
  - 参数示例：-Mode steady -RPS 300 -Duration 120s -Accounts 200 -Amount 1 -BaseUrl https://localhost:7067 -Insecure
  - 衡量：失败率 < 1%，p95 延迟 < 500ms，即为可承载。
- 阶梯递增压力：
  - 参数示例：-Mode step -StartRps 200 -StepRps 200 -Steps 8 -StageDuration 45s -Accounts 500 -BaseUrl https://localhost:7067 -Insecure
  - 衡量：找到失败率急剧上升或 p95 大幅恶化的阶段，即为近似极限。

常用参数（env 或 run.ps1 同名参数）
- BASE_URL         目标地址，默认 https://localhost:7067
- ACCOUNTS         预创建账号数量，默认 steady:50 / step:100
- INIT_BALANCE     初始余额，默认 100000
- AMOUNT           单笔金额，默认 1
- RPS              稳态模式下的到达率
- DURATION         稳态模式持续时间
- PRE_VUS / MAX_VUS  预分配/最大 VU 数
- START_RPS / STEP_RPS / STEPS / STAGE_DURATION  阶梯模式参数

输出结果与判定：
- 运行结束后生成 steady-summary.json 或 step-summary.json，可从 metrics 中查看：
  - http_req_failed（失败率）
  - http_req_duration（延时分位）
  - 自定义计数/趋势（ops、rt_p95 等）
- 建议将失败率阈值控制在 1%-2% 内，并观察 p95/p99 延迟变化趋势来判断可承载的 QPS。

注意事项：
- 本项目默认使用本地 SQLite，极高并发下会出现文件级锁争用；这是实际瓶颈的一部分。
- 若需测试更高极限，可切换到 SQL Server 并调大连接池。
- API 默认启用 HTTPS 重定向；k6 已配置不校验证书，或使用 http 端口 5145。

示例排障：
- 证书错误：传入 -Insecure 或使用 http://localhost:5145
- 429/5xx 激增：增大 Accounts 以减少单账号热点；降低 STEP_RPS 或延长 STAGE_DURATION。

