namespace CHTLManager.WPF.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int AvailableStock { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }
}
