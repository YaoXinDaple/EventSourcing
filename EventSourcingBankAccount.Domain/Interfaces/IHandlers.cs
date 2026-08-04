using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Interfaces;

/// <summary>
/// 命令处理器接口
/// </summary>
public interface ICommandHandler<in TCommand> where TCommand : DomainCommand
{
    Task HandleAsync(TCommand command);
}

/// <summary>
/// 查询处理器接口
/// </summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : DomainCommand
{
    Task<TResult> HandleAsync(TQuery query);
}
