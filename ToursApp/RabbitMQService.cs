using RabbitMQ.Client;
using System.Collections.Generic;
using System.Text;

public class RabbitMqService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private const string ExchangeName = "tours";
    private const string EmailQueueName = "tours_email_queue";
    private const string BackofficeQueueName = "tours_backoffice_queue";
    private const string DeadLetterExchangeName = "dead_letter_exchange";
    private const string DeadLetterQueueName = "dead_letter_queue";

    public RabbitMqService()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic);


        _channel.ExchangeDeclare(exchange: DeadLetterExchangeName, type: ExchangeType.Direct);

        _channel.QueueDeclare(queue: DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        _channel.QueueBind(queue: DeadLetterQueueName, exchange: DeadLetterExchangeName, routingKey: "dead_letter");

        // Opsætning af email-køen med dead-letter konfiguration
        var emailQueueArguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", DeadLetterExchangeName },
            { "x-dead-letter-routing-key", "dead_letter" }
        };
        _channel.QueueDeclare(queue: EmailQueueName, durable: true, exclusive: false, autoDelete: false, arguments: emailQueueArguments);

        _channel.QueueBind(queue: EmailQueueName, exchange: ExchangeName, routingKey: "tours.book");

        // Opsætning af backoffice-køen med samme dead-letter konfiguration
        var backofficeQueueArguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", DeadLetterExchangeName },
            { "x-dead-letter-routing-key", "dead_letter" }
        };
        _channel.QueueDeclare(queue: BackofficeQueueName, durable: true, exclusive: false, autoDelete: false, arguments: backofficeQueueArguments);

        _channel.QueueBind(queue: BackofficeQueueName, exchange: ExchangeName, routingKey: "tours.*");
    }

    // Sender en besked til den rette kø baseret på handlingen
    public void SendMessage(string message, string action)
    {
        var routingKey = action == "Book" ? "tours.book" : "tours.cancel";
        var body = Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(exchange: ExchangeName, routingKey: routingKey, basicProperties: null, body: body);
    }

    public void Dispose()
    {
        _channel.Close();
        _connection.Close();
    }
}
