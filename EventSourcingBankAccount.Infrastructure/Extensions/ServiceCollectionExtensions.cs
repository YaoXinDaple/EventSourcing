using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Infrastructure.Repositories;
using EventSourcingBankAccount.Infrastructure.Services;

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
        // 注册数据库上下文
        services.AddDbContext<EventSourcingDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                // 使用 SQLite 作为默认数据库
                options.UseSqlite("Data Source=eventsourcing.db");
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        // 注册事件溯源核心服务
        services.AddScoped<IEventStore, EventStore>();
        services.AddScoped<ISnapshotStore, SnapshotStore>();
        services.AddScoped<IAggregateStore, AggregateStore>();
        
        // 注册快照策略
        services.AddScoped<ISnapshotStrategy>(provider => 
            new EventCountSnapshotStrategy(eventThreshold: 10));
        
        // 注册银行账户相关服务
        services.AddScoped<IBankAccountRepository, BankAccountRepository>();
        services.AddScoped<IBankAccountService, BankAccountService>();

        return services;
    }

    /// <summary>
    /// 添加自定义快照策略
    /// </summary>
    public static IServiceCollection AddSnapshotStrategy<T>(this IServiceCollection services)
        where T : class, ISnapshotStrategy
    {
        services.AddScoped<ISnapshotStrategy, T>();
        return services;
    }

    /// <summary>
    /// 添加组合快照策略
    /// </summary>
    public static IServiceCollection AddCompositeSnapshotStrategy(
        this IServiceCollection services,
        params ISnapshotStrategy[] strategies)
    {
        services.AddScoped<ISnapshotStrategy>(provider => 
            new CompositeSnapshotStrategy(strategies));
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
