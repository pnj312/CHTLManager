using System;

namespace CHTLManager.DAL.Entities
{
    public class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CustomerPayment { get; set; }
        public decimal ChangeAmount { get; set; }
    }
}
