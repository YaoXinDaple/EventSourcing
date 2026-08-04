using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Domain.Events;
using EventSourcingBankAccount.Domain.Exceptions;

namespace EventSourcingBankAccount.Tests.Domain;

public class BankAccountTests
{
    [Fact]
    public void NewAccount_ShouldHaveAggregateIdAndContinuousUncommittedEventVersions()
    {
        // 回归：创建事件的 AggregateId 曾为空，导致后续持久化和重放无法正确关联账户。
        var account = new BankAccount("acct-001", "Alice", 100m);

        account.Deposit(25m);
        account.Withdraw(10m);

        var events = account.GetUncommittedEvents();
        Assert.Collection(
            events,
            @event =>
            {
                Assert.IsType<AccountCreated>(@event);
                Assert.Equal("acct-001", @event.AggregateId);
                Assert.Equal(1, @event.Version);
            },
            @event =>
            {
                Assert.IsType<MoneyDeposited>(@event);
                Assert.Equal("acct-001", @event.AggregateId);
                Assert.Equal(2, @event.Version);
            },
            @event =>
            {
                Assert.IsType<MoneyWithdrawn>(@event);
                Assert.Equal("acct-001", @event.AggregateId);
                Assert.Equal(3, @event.Version);
            });
    }

    [Fact]
    public void DepositAndWithdraw_ShouldUpdateBalance()
    {
        var account = new BankAccount("acct-002", "Alice", 100m);

        account.Deposit(50m);
        account.Withdraw(30m);

        Assert.Equal(120m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deposit_WhenAmountIsNotPositive_ShouldThrowInvalidOperationException(decimal amount)
    {
        var account = new BankAccount("acct-003", "Alice", 100m);

        Assert.Throws<InvalidOperationException>(() => account.Deposit(amount));
        Assert.Equal(100m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Withdraw_WhenAmountIsNotPositive_ShouldThrowInvalidOperationException(decimal amount)
    {
        var account = new BankAccount("acct-004", "Alice", 100m);

        Assert.Throws<InvalidOperationException>(() => account.Withdraw(amount));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void Withdraw_WhenFundsAreInsufficient_ShouldThrowAndKeepAggregateClean()
    {
        var account = new BankAccount("acct-005", "Alice", 100m);
        var uncommittedCountBefore = account.GetUncommittedEvents().Count;

        Assert.Throws<InsufficientFundsException>(() => account.Withdraw(101m));

        Assert.Equal(100m, account.Balance);
        Assert.Equal(uncommittedCountBefore, account.GetUncommittedEvents().Count);
    }

    [Theory]
    [InlineData("", "Alice", 0)]
    [InlineData("   ", "Alice", 0)]
    [InlineData("acct-006", "", 0)]
    [InlineData("acct-006", "   ", 0)]
    [InlineData("acct-006", "Alice", -1)]
    public void Constructor_WhenArgumentsAreInvalid_ShouldThrowArgumentException(
        string accountId,
        string accountHolder,
        decimal initialBalance)
    {
        Assert.Throws<ArgumentException>(() => new BankAccount(accountId, accountHolder, initialBalance));
    }
}
