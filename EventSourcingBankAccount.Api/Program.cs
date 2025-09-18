using Microsoft.EntityFrameworkCore;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Repositories;
using EventSourcingBankAccount.Infrastructure.Handlers;
using EventSourcingBankAccount.Domain.Commands;
using EventSourcingBankAccount.Domain.Queries;
using EventSourcingBankAccount.Domain.Events;
using EventSourcingBankAccount.Domain.Core;
using Scalar;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 配置数据库连接 - 使用SQLite便于开发和测试
builder.Services.AddDbContext<EventSourcingDbContext>(options =>
{
    // 可以从配置文件中读取连接字符串，但这里简化使用SQLite
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        // 如果配置文件中没有连接字符串，使用默认SQLite
        options.UseSqlite("Data Source=eventsourcing.db");
    }
    else if (connectionString.Contains("Server=") || connectionString.Contains("Data Source=") && connectionString.Contains("Initial Catalog"))
    {
        // 如果是SQL Server连接字符串，使用SQL Server
        options.UseSqlServer(connectionString);
    }
    else
    {
        // 否则假设是SQLite
        options.UseSqlite(connectionString);
    }
});

// 注册存储接口实现
builder.Services.AddScoped<IEventStore, EventStore>();
builder.Services.AddScoped<ISnapshotStore, SnapshotStore>();
builder.Services.AddScoped<IAggregateStore, AggregateStore>();

// 注册快照策略，默认使用事件计数策略，每10个事件创建一个快照
builder.Services.AddScoped<ISnapshotStrategy>(provider => new EventCountSnapshotStrategy(10));

// 注册命令处理器
builder.Services.AddScoped<ICommandHandler<CreateAccount>, CreateAccountHandler>();
builder.Services.AddScoped<ICommandHandler<DepositMoney>, DepositMoneyHandler>();
builder.Services.AddScoped<ICommandHandler<WithdrawMoney>, WithdrawMoneyHandler>();

// 注册查询处理器
builder.Services.AddScoped<IQueryHandler<GetAccountBalance, AccountBalanceResult>, GetAccountBalanceHandler>();
builder.Services.AddScoped<IQueryHandler<GetAccountStateAtTime, AccountStateResult>, GetAccountStateAtTimeHandler>();

// 注册事件处理器
builder.Services.AddScoped<IEventHandler<AccountCreated>, AccountCreatedHandler>();
builder.Services.AddScoped<IEventHandler<MoneyDeposited>, MoneyDepositedHandler>();
builder.Services.AddScoped<IEventHandler<MoneyWithdrawn>, MoneyWithdrawnHandler>();

// 配置日志系统
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// 确保数据库已创建
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<EventSourcingDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("数据库初始化成功");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "数据库初始化失败");
        throw;
    }
}

app.UseHttpsRedirection();
app.UseRouting();

app.MapControllers();

app.Run();
