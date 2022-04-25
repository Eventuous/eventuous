namespace Eventuous.Producers; 

public delegate ValueTask AcknowledgeProduce(ProducedMessage message);

public delegate ValueTask ReportFailedProduce(ProducedMessage message, string error, Exception? exception);
