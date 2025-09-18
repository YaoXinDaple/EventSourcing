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

// �������ݿ����� - ʹ��SQLite���ڿ����Ͳ���
builder.Services.AddDbContext<EventSourcingDbContext>(options =>
{
    // ���Դ������ļ��ж�ȡ�����ַ������������ʹ��SQLite
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        // ��������ļ���û�������ַ�����ʹ��Ĭ��SQLite
        options.UseSqlite("Data Source=eventsourcing.db");
    }
    else if (connectionString.Contains("Server=") || connectionString.Contains("Data Source=") && connectionString.Contains("Initial Catalog"))
    {
        // �����SQL Server�����ַ�����ʹ��SQL Server
        options.UseSqlServer(connectionString);
    }
    else
    {
        // ���������SQLite
        options.UseSqlite(connectionString);
    }
});

// ע��洢�ӿ�ʵ��
builder.Services.AddScoped<IEventStore, EventStore>();
builder.Services.AddScoped<ISnapshotStore, SnapshotStore>();
builder.Services.AddScoped<IAggregateStore, AggregateStore>();

// ע����ղ��ԣ�Ĭ��ʹ���¼��������ԣ�ÿ10���¼�����һ������
builder.Services.AddScoped<ISnapshotStrategy>(provider => new EventCountSnapshotStrategy(10));

// ע���������
builder.Services.AddScoped<ICommandHandler<CreateAccount>, CreateAccountHandler>();
builder.Services.AddScoped<ICommandHandler<DepositMoney>, DepositMoneyHandler>();
builder.Services.AddScoped<ICommandHandler<WithdrawMoney>, WithdrawMoneyHandler>();

// ע���ѯ������
builder.Services.AddScoped<IQueryHandler<GetAccountBalance, AccountBalanceResult>, GetAccountBalanceHandler>();
builder.Services.AddScoped<IQueryHandler<GetAccountStateAtTime, AccountStateResult>, GetAccountStateAtTimeHandler>();

// ע���¼�������
builder.Services.AddScoped<IEventHandler<AccountCreated>, AccountCreatedHandler>();
builder.Services.AddScoped<IEventHandler<MoneyDeposited>, MoneyDepositedHandler>();
builder.Services.AddScoped<IEventHandler<MoneyWithdrawn>, MoneyWithdrawnHandler>();

// ������־ϵͳ
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ȷ�����ݿ��Ѵ���
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<EventSourcingDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("���ݿ��ʼ���ɹ�");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "���ݿ��ʼ��ʧ��");
        throw;
    }
}

app.UseHttpsRedirection();
app.UseRouting();

app.MapControllers();

app.Run();
