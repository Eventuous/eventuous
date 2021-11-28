# Eventuous Google PubSub support

This package adds support for [Google PubSub](https://cloud.google.com/pubsub) to applications built with Eventuous. 
It includes the following components:

- Subscription (`GooglePubSubSubscription`)
- Message producer (`GooglePubSubProducer`)

Both subscription and producer support ordering keys.

PubSub Lite is not supported at the moment due to lack of C# SDK.