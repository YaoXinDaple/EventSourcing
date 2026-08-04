using EventSourcingBankAccount.Infrastructure.Extensions;
using Scalar;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddEventSourcingInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// 确保数据库已创建
try
{
    await app.Services.EnsureDatabaseCreatedAsync();
    app.Logger.LogInformation("数据库初始化成功");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "数据库初始化失败");
    throw;
}

app.UseHttpsRedirection();
app.UseRouting();

app.MapControllers();

app.Run();
