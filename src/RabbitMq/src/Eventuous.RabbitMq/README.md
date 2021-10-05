# Eventuous RabbitMQ support

This package adds support for RabbitMQ to applications built with Eventuous. 
It includes the following components:

- Subscription (`RabbitMqSubscription`)
- Message producer (`RabbitMqProducer`)

Remember that RabbitMQ doesn't support ordered message delivery.

For each "stream", you will get an exchange, where the messages will be produced to.

Creating a subscription will add a queue and an exchange. The queue and subscription exchange names will be the subscription id. 
The subscription queue binds to the subscription exchange, the subscription exchange binds to the exchange of the "stream".