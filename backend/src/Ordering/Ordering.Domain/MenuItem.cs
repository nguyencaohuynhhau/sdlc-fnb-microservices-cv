using Shared.Kernel;

namespace Ordering.Domain;

public sealed class MenuItem : Entity
{
    private MenuItem()
    {
    }

    public MenuItem(string name, decimal price)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; } = "";

    public decimal Price { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Còn hàng hay không — hình chiếu từ sự kiện của inventory (lát C), người dùng không sửa.</summary>
    public bool IsAvailable { get; private set; } = true;

    public void SetAvailable(bool available) => IsAvailable = available;
}
