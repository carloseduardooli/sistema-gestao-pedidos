namespace Orders.Core.Entities;
using Orders.Core.Enums;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Cliente { get; set; } = string.Empty;
    public string Produto { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pendente;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}
