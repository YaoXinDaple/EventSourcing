using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using EventSourcingBankAccount.Domain.Commands;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Domain.Queries;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Infrastructure.Handlers;
using EventSourcingBankAccount.Infrastructure.Repositories;

namespace EventSourcingBankAccount.Infrastructure.Extensions;

/// <summary>
/// 基础设施层依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加事件溯源基础设施服务
    /// </summary>
    public static IServiceCollection AddEventSourcingInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddLogging();

        // 注册数据库上下文
        services.AddDbContext<EventSourcingDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // 未配置连接串时使用本地 SQLite，保持开发环境默认行为。
                options.UseSqlite("Data Source=eventsourcing.db");
            }
            else if (IsSqlServerConnectionString(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                // 判断规则：包含 Server=，或同时包含 Data Source= 与 Initial Catalog=，视为 SQL Server；其它连接串按 SQLite 处理。
                options.UseSqlite(connectionString);
            }
        });

        // 注册事件溯源核心服务
        services.AddScoped<IEventStore, EventStore>();
        services.AddScoped<ISnapshotStore, SnapshotStore>();
        services.AddScoped<IAggregateStore, AggregateStore>();
        
        // 注册快照策略
        services.AddSingleton<ISnapshotStrategy>(provider => 
            new EventCountSnapshotStrategy(eventThreshold: 10));
        
        // 注册命令处理器
        services.AddScoped<ICommandHandler<CreateAccount>, CreateAccountHandler>();
        services.AddScoped<ICommandHandler<DepositMoney>, DepositMoneyHandler>();
        services.AddScoped<ICommandHandler<WithdrawMoney>, WithdrawMoneyHandler>();

        // 注册查询处理器
        services.AddScoped<IQueryHandler<GetAccountBalance, AccountBalanceResult>, GetAccountBalanceHandler>();
        services.AddScoped<IQueryHandler<GetAccountStateAtTime, AccountStateResult>, GetAccountStateAtTimeHandler>();

        return services;
    }

    private static bool IsSqlServerConnectionString(string connectionString)
    {
        // 判断规则：包含 Server=，或同时包含 Data Source= 与 Initial Catalog=，视为 SQL Server。
        var containsServer = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);
        var containsDataSource = connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);
        var containsInitialCatalog = connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);

        return containsServer || (containsDataSource && containsInitialCatalog);
    }

    /// <summary>
    /// 添加自定义快照策略
    /// </summary>
    public static IServiceCollection AddSnapshotStrategy<T>(this IServiceCollection services)
        where T : class, ISnapshotStrategy
    {
        services.AddSingleton<ISnapshotStrategy, T>();
        return services;
    }

    /// <summary>
    /// 添加组合快照策略
    /// </summary>
    public static IServiceCollection AddCompositeSnapshotStrategy(
        this IServiceCollection services,
        params Type[] strategyTypes)
    {
        foreach (var strategyType in strategyTypes)
        {
            if (!typeof(ISnapshotStrategy).IsAssignableFrom(strategyType))
            {
                throw new ArgumentException(
                    $"类型 {strategyType.Name} 必须实现 {nameof(ISnapshotStrategy)}",
                    nameof(strategyTypes));
            }

            services.AddSingleton(strategyType);
        }

        services.AddSingleton<ISnapshotStrategy>(provider =>
        {
            var strategies = strategyTypes
                .Select(strategyType => (ISnapshotStrategy)provider.GetRequiredService(strategyType))
                .ToArray();
            return new CompositeSnapshotStrategy(strategies);
        });
        return services;
    }

    /// <summary>
    /// 确保数据库创建
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventSourcingDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
