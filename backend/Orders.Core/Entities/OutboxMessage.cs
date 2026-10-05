namespace Orders.Core.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty; // ex: "OrderCreated"
    public string Payload { get; set; } = string.Empty; // JSON com os dados (ex: { "OrderId": "..." })
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Se estiver preenchido, a mensagem já foi enviada ao RabbitMQ
    public DateTime? ProcessedAt { get; set; } 
}
