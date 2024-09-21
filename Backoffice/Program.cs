using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    static void Main(string[] args)
    {
        var consumer = new BackofficeConsumer();
        consumer.Consume();
    }
}

public class BackofficeConsumer
{
    public void Consume()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Definerer argumenter for køen, som angiver dead-letter exchange og routing-key
        var arguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "dead_letter_exchange" },
            { "x-dead-letter-routing-key", "dead_letter" }
        };

        // Opretter en kø til backoffice-processering af tours
        channel.QueueDeclare(queue: "tours_backoffice_queue", durable: true, exclusive: false, autoDelete: false, arguments: arguments);

        // Opretter en consumer, som skal håndtere beskeder modtaget fra køen
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            // Modtager beskeden og konverterer den fra byte-array til string
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            string reason;

            try
            {
                // Validerer beskeden. Hvis den ikke er gyldig, sendes den til dead-letter exchange
                if (!IsValidMessage(message, out reason))
                {
                    // Sender beskeden til dead-letter exchange med årsagen til fejl
                    var deadLetterBody = Encoding.UTF8.GetBytes($"{message}|Reason: {reason}");
                    Console.WriteLine($"Publishing to DLX: {message}");
                    channel.BasicPublish("dead_letter_exchange", "dead_letter", null, deadLetterBody);
                    Console.WriteLine($"Invalid message, sent to DLX: {message}");

                    // Afviser beskeden og forhindrer requeueing
                    channel.BasicReject(ea.DeliveryTag, false);
                    return;
                }

                // Hvis beskeden er gyldig, udskrives den, og den bliver ack'et
                Console.WriteLine($"Received booking message: {message}");
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                // Hvis der opstår en fejl, afvises beskeden og sendes ikke tilbage til køen
                Console.WriteLine($"Error processing message: {ex.Message}");
                channel.BasicReject(ea.DeliveryTag, false);
            }
        };

        // Starter consumeren på den specificerede kø uden automatisk at kvittere beskeder
        channel.BasicConsume(queue: "tours_backoffice_queue", autoAck: false, consumer: consumer);
        Console.ReadLine();
    }

    // Funktion til at validere beskedens format og indhold
    private bool IsValidMessage(string message, out string reason)
    {
        var parts = message.Split('|');

        if (parts.Length < 4)
        {
            reason = "Message does not contain enough parts.";
            return false;
        }

        var name = parts[1];
        if (!Regex.IsMatch(name, @"^[a-zA-Z\s]+$"))
        {
            reason = "Name contains invalid characters (numbers or symbols).";
            return false;
        }

        reason = null;
        return true;
    }
}
