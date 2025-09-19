# EventSourcing Bank Account - Event Sourcing .NET Web API

This is an Event Sourcing implementation for bank account management using .NET 9.0, C#, Entity Framework Core with SQLite, and following Domain-Driven Design (DDD) and CQRS patterns.

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Working Effectively

### Prerequisites and SDK Installation
- Install .NET 9.0 SDK (CRITICAL - project targets .NET 9.0, not .NET 10.0):
  - `curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0 --install-dir ~/.dotnet`
  - `export PATH="$HOME/.dotnet:$PATH"`
  - Verify: `dotnet --version` should show 9.0.x
- Install Entity Framework Core tools globally:
  - `dotnet tool install --global dotnet-ef`

### Build and Test Process
- **NEVER CANCEL build operations** - Build takes 3-5 seconds, use timeout 60+ seconds
- Full clean build process:
  ```bash
  export PATH="$HOME/.dotnet:$PATH"
  dotnet clean
  dotnet restore  # Takes ~2 seconds, NEVER CANCEL
  dotnet build --configuration Release --verbosity minimal  # Takes ~5 seconds, NEVER CANCEL
  ```

### Database Setup (MANDATORY before running)
- **ALWAYS run database migrations before first use**:
  ```bash
  export PATH="$HOME/.dotnet:$PATH"
  # Create migration if needed (first time only)
  dotnet ef migrations add InitialCreate --project EventSourcingBankAccount.Infrastructure --startup-project EventSourcingBankAccount.Api
  
  # Apply migrations (ALWAYS run this)
  dotnet ef database update --project EventSourcingBankAccount.Infrastructure --startup-project EventSourcingBankAccount.Api
  ```
- Database: SQLite (`eventsourcing.db` in API project directory)
- **If database errors occur**: Remove database file and re-run migrations:
  ```bash
  rm -f EventSourcingBankAccount.Api/eventsourcing.db*
  dotnet ef database update --project EventSourcingBankAccount.Infrastructure --startup-project EventSourcingBankAccount.Api
  ```

### Running the Application
- **ALWAYS complete build and database setup first**
- Start the API server:
  ```bash
  export PATH="$HOME/.dotnet:$PATH"
  dotnet run --project EventSourcingBankAccount.Api --configuration Release
  ```
- Application runs on: `http://localhost:5145`
- Startup time: ~3-5 seconds
- Database initializes automatically on startup

## Validation Scenarios

**MANDATORY: Test complete user scenarios after making changes:**

1. **Create Account Test**:
   ```bash
   curl -X POST "http://localhost:5145/api/BankAccount" -H "Content-Type: application/json" \
        -d '{"accountId":"test123","accountHolder":"John Doe","initialBalance":1000.50}'
   ```
   Expected: `{"success":true,"message":"账户创建成功","data":{"accountId":"test123"}}`

2. **Deposit Money Test**:
   ```bash
   curl -X POST "http://localhost:5145/api/BankAccount/test123/deposit" -H "Content-Type: application/json" \
        -d '{"amount":500.25,"description":"Test deposit"}'
   ```
   Expected: Success response with amount confirmation

3. **Withdraw Money Test**:
   ```bash
   curl -X POST "http://localhost:5145/api/BankAccount/test123/withdraw" -H "Content-Type: application/json" \
        -d '{"amount":200.00,"description":"Test withdrawal"}'
   ```
   Expected: Success response with amount confirmation

4. **Balance Query Test**:
   ```bash
   curl -X GET "http://localhost:5145/api/BankAccount/test123/balance"
   ```
   Expected: Current balance, account details, and version information

5. **Historical State Test (Event Sourcing Feature)**:
   ```bash
   curl -X GET "http://localhost:5145/api/BankAccount/test123/state?pointInTime=2025-09-19T05:31:20.000Z"
   ```
   Expected: Account state at the specified point in time

**ALL five scenarios MUST pass for the application to be considered working correctly.**

## Project Structure

### Key Projects
1. **EventSourcingBankAccount.Api**: Web API controllers and startup configuration
2. **EventSourcingBankAccount.Domain**: Domain models, commands, events, aggregates, and business logic
3. **EventSourcingBankAccount.Infrastructure**: Data access, repositories, event store, and persistence

### Critical Files to Monitor
- `EventSourcingBankAccount.Api/Program.cs`: Dependency injection and startup configuration
- `EventSourcingBankAccount.Domain/Aggregates/BankAccount.cs`: Core business logic
- `EventSourcingBankAccount.Infrastructure/Data/EventSourcingDbContext.cs`: Database context
- `EventSourcingBankAccount.Infrastructure/Extensions/ServiceCollectionExtensions.cs`: Service registration
- Database migrations in `EventSourcingBankAccount.Infrastructure/Migrations/`

### Database Tables
- `Events`: Stores domain events for event sourcing
- `Snapshots`: Performance optimization snapshots
- `BankAccounts`: Account aggregate state

## Common Commands and Timing

### Build Commands (with timeouts)
- `dotnet clean`: <1 second, timeout: 30 seconds
- `dotnet restore`: 1-2 seconds, timeout: 60 seconds, **NEVER CANCEL**
- `dotnet build`: 3-5 seconds, timeout: 60 seconds, **NEVER CANCEL**
- `dotnet run`: 3-5 second startup, timeout: 60 seconds

### Database Commands
- `dotnet ef migrations add <Name>`: 2-5 seconds, timeout: 60 seconds
- `dotnet ef database update`: 1-3 seconds, timeout: 60 seconds

### No Test Suite
- This repository does not contain unit tests or integration tests
- Validation is done through API endpoint testing (see Validation Scenarios above)
- **ALWAYS manually test API functionality after changes**

## Event Sourcing Implementation Details

### Core Concepts Implemented
- **Aggregates**: `BankAccount` with event replay capability
- **Commands**: `CreateAccount`, `DepositMoney`, `WithdrawMoney`
- **Events**: `AccountCreated`, `MoneyDeposited`, `MoneyWithdrawn`
- **Queries**: `GetAccountBalance`, `GetAccountStateAtTime`
- **Snapshots**: EventCountSnapshotStrategy (every 10 events)
- **CQRS**: Separate command handlers and query handlers

### Key Features
- **Time Travel**: Query account state at any historical point
- **Event Store**: All state changes persisted as immutable events
- **Snapshots**: Performance optimization for aggregate reconstruction
- **Audit Trail**: Complete history of all account operations

## Troubleshooting

### Common Issues
1. **.NET Version Mismatch**: Ensure .NET 9.0 SDK is installed and PATH is set
2. **Database Not Found**: Run `dotnet ef database update` before starting application
3. **Build Failures**: Clean solution and restore packages: `dotnet clean && dotnet restore`
4. **API Errors**: Check database exists and migrations applied
5. **Port Conflicts**: Application uses port 5145, ensure it's available

### Error Recovery
- Database issues: Delete database file and re-run migrations
- Build issues: Clean and restore packages
- Runtime issues: Check logs in console output
- **NEVER CANCEL long-running operations** - wait for completion

## API Endpoints Summary

- `POST /api/BankAccount`: Create new account
- `POST /api/BankAccount/{id}/deposit`: Deposit money
- `POST /api/BankAccount/{id}/withdraw`: Withdraw money
- `GET /api/BankAccount/{id}/balance`: Get current balance
- `GET /api/BankAccount/{id}/state?pointInTime={datetime}`: Get historical state

## Development Workflow
1. Make code changes
2. Run `dotnet build` to verify compilation
3. Run database migrations if schema changed
4. Start application with `dotnet run`
5. **MANDATORY**: Execute all 5 validation scenarios
6. Verify API responses match expected formats
7. Check application logs for any errors

**Remember: This is an Event Sourcing system - always test historical state queries to ensure event replay works correctly.**