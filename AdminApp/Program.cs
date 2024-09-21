using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        var adminConsumer = new AdminConsumer();
        adminConsumer.Consume();
    }
}

public class AdminConsumer
{
    public void Consume()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "dead_letter_queue", durable: true, exclusive: false, autoDelete: false);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Received undeliverable message: {message}");

            channel.BasicAck(ea.DeliveryTag, false);
        };

        channel.BasicConsume(queue: "dead_letter_queue", autoAck: false, consumer: consumer);

        Console.WriteLine("Admin: Listening for undeliverable messages...");
        Console.ReadLine();
    }
}
