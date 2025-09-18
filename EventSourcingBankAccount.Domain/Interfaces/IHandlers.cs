using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Interfaces;

/// <summary>
/// ��������ӿ�
/// </summary>
public interface ICommandHandler<in TCommand> where TCommand : DomainCommand
{
    Task HandleAsync(TCommand command);
}

/// <summary>
/// �¼��������ӿ�
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task HandleAsync(TEvent @event);
}

/// <summary>
/// ��ѯ�������ӿ�
/// </summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : DomainCommand
{
    Task<TResult> HandleAsync(TQuery query);
}