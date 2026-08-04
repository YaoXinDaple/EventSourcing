using EventSourcingBankAccount.Domain.Commands;
using EventSourcingBankAccount.Domain.Exceptions;
using EventSourcingBankAccount.Infrastructure.Handlers;
using EventSourcingBankAccount.Tests.TestInfrastructure;

namespace EventSourcingBankAccount.Tests.Infrastructure;

public class CommandHandlerTests
{
    [Fact]
    public async Task CreateAccountHandler_WhenAccountAlreadyExists_ShouldRejectDuplicate()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var handler = new CreateAccountHandler(host.AggregateStore);
        var command = new CreateAccount("acct-command-duplicate", "Alice", 100m);

        await handler.HandleAsync(command);

        await Assert.ThrowsAsync<AccountAlreadyExistsException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task DepositMoneyHandler_WhenAccountDoesNotExist_ShouldThrow()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var handler = new DepositMoneyHandler(host.AggregateStore);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new DepositMoney("acct-command-missing", 10m, "deposit")));
    }

    [Fact]
    public async Task WithdrawMoneyHandler_WhenAccountDoesNotExist_ShouldThrow()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var handler = new WithdrawMoneyHandler(host.AggregateStore);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new WithdrawMoney("acct-command-missing", 10m, "withdraw")));
    }
}
