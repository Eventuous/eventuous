namespace Eventuous.Subscriptions.Channels; 

public class ChannelFullException : Exception {
    public ChannelFullException() : base("Channel worker unable to write to the channel because it's full") { }
}