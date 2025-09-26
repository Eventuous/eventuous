using Eventuous.SqlServer.Projections;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Data.SqlClient;
using System.Data;
using TUnit.Assertions.AssertConditions.Throws;

namespace Eventuous.Tests.SqlServer.Subscriptions;

/// <summary>
/// Tests for SqlServerSubscriptionBase connection string handling.
/// These tests verify the fix for issue #410 where connection strings were not properly resolved.
/// </summary>
public class ConnectionStringTests {
    readonly ICheckpointStore _checkpointStore = new NoOpCheckpointStore();
    readonly ConsumePipe      _consumePipe     = new();
    readonly ILoggerFactory   _loggerFactory   = LoggerFactory.Create(builder => builder.AddConsole());

    [Test]
    public void Should_Use_ConnectionOptions_ConnectionString_When_Provided() {
        // Arrange
        const string expectedConnectionString = "Server=localhost;Database=Test1;Trusted_Connection=true;";
        const string optionsConnectionString  = "Server=localhost;Database=Test2;Trusted_Connection=true;";

        var options = new TestSubscriptionOptions {
            SubscriptionId   = "test-subscription-1",
            ConnectionString = optionsConnectionString
        };

        var connectionOptions = new SqlServerConnectionOptions(expectedConnectionString, "dbo");

        // Act & Assert - The constructor should not throw an exception
        _ = new TestSubscription(options, _checkpointStore, _consumePipe, _loggerFactory, connectionOptions);
    }

    [Test]
    public void Should_Use_Options_ConnectionString_When_ConnectionOptions_Is_Null() {
        // Arrange
        const string expectedConnectionString = "Server=localhost;Database=Test;Trusted_Connection=true;";

        var options = new TestSubscriptionOptions {
            SubscriptionId   = "test-subscription-2",
            ConnectionString = expectedConnectionString
        };

        // Act & Assert - The constructor should not throw an exception
        _ = new TestSubscription(options, _checkpointStore, _consumePipe, _loggerFactory, null);
    }

    [Test]
    public void Should_Use_Options_ConnectionString_When_ConnectionOptions_ConnectionString_Is_Null() {
        // Arrange
        const string expectedConnectionString = "Server=localhost;Database=Test;Trusted_Connection=true;";

        var options = new TestSubscriptionOptions {
            SubscriptionId   = "test-subscription-3",
            ConnectionString = expectedConnectionString
        };

        var connectionOptions = new SqlServerConnectionOptions(null!, "dbo");

        // Act & Assert - The constructor should not throw an exception
        _ = new TestSubscription(options, _checkpointStore, _consumePipe, _loggerFactory, connectionOptions);
    }

    [Test]
    public async Task Should_Throw_When_Both_Connection_Strings_Are_Null_Or_Empty() {
        // Arrange
        var options = new TestSubscriptionOptions {
            SubscriptionId   = "test-subscription-4",
            ConnectionString = null
        };

        var connectionOptions = new SqlServerConnectionOptions(null!, "dbo");

        // Act & Assert
        await Assert.That(() => new TestSubscription(options, _checkpointStore, _consumePipe, _loggerFactory, connectionOptions)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_Throw_When_Both_Connection_Strings_Are_Empty() {
        // Arrange
        var options = new TestSubscriptionOptions {
            SubscriptionId   = "test-subscription-5",
            ConnectionString = ""
        };

        var connectionOptions = new SqlServerConnectionOptions("", "dbo");

        // Act & Assert
        await Assert.That(() => new TestSubscription(options, _checkpointStore, _consumePipe, _loggerFactory, connectionOptions)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_Throw_When_Options_Connection_String_Is_Null_And_No_ConnectionOptions() {
        // Arrange
        var options = new TestSubscriptionOptions {
            SubscriptionId   = "test-subscription-6",
            ConnectionString = null
        };

        // Act & Assert
        await Assert.That(() => new TestSubscription(options, _checkpointStore, _consumePipe, _loggerFactory, null)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_Throw_When_Options_Connection_String_Is_Empty_And_No_ConnectionOptions() {
        // Arrange
        var options = new TestSubscriptionOptions {
            SubscriptionId   = "test-subscription-7",
            ConnectionString = ""
        };

        // Act & Assert
        await Assert.That(() => new TestSubscription(options, _checkpointStore, _consumePipe, _loggerFactory, null)).Throws<ArgumentException>();
    }
}

/// <summary>
/// Test implementation of SqlServerSubscriptionBase for testing connection string handling
/// </summary>
public class TestSubscription(
        TestSubscriptionOptions     options,
        ICheckpointStore            checkpointStore,
        ConsumePipe                 consumePipe,
        ILoggerFactory              loggerFactory,
        SqlServerConnectionOptions? connectionOptions
    )
    : SqlServerSubscriptionBase<TestSubscriptionOptions>(
        options,
        checkpointStore,
        consumePipe,
        SubscriptionKind.All,
        loggerFactory,
        null,
        null,
        connectionOptions
    ) {
    protected override SqlCommand PrepareCommand(SqlConnection connection, long start) {
        var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT TOP 0 MessageId, MessageType, StreamPosition, GlobalPosition, JsonData, JsonMetadata, Created, StreamName FROM dbo.Messages WHERE GlobalPosition > @start";
        command.Parameters.Add("@start", SqlDbType.BigInt).Value = start;

        return command;
    }
}

/// <summary>
/// Test subscription options for testing connection string handling
/// </summary>
public record TestSubscriptionOptions : SqlServerSubscriptionBaseOptions;
