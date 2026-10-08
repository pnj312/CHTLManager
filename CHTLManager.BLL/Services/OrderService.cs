using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL.Services
{
    public class OrderService
    {
        public OrderService()
        {
        }

        public Order? CreateOrder(Order order, List<OrderDetail> details)
        {
            if (order is null || details is null || details.Count == 0 ||
                order.UserId <= 0 || order.CustomerPayment < 0)
            {
                return null;
            }

            var quantities = new Dictionary<int, int>();
            foreach (var detail in details)
            {
                if (detail is null || detail.ProductId <= 0 || detail.Quantity <= 0)
                {
                    return null;
                }

                quantities.TryGetValue(detail.ProductId, out int currentQuantity);
                if (detail.Quantity > int.MaxValue - currentQuantity)
                {
                    return null;
                }

                quantities[detail.ProductId] = currentQuantity + detail.Quantity;
            }

            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);

            if (!db.Users.Any(user => user.Id == order.UserId && user.IsActive))
            {
                return null;
            }

            var productIds = quantities.Keys.ToList();
            var products = db.Products
                .Where(product => productIds.Contains(product.Id))
                .ToDictionary(product => product.Id);

            var savedDetails = new List<OrderDetail>(quantities.Count);
            decimal totalAmount = 0;

            foreach (var productId in productIds)
            {
                int quantity = quantities[productId];
                if (!products.TryGetValue(productId, out var product) ||
                    product.StockQuantity < quantity || product.SellingPrice < 0)
                {
                    return null;
                }

                decimal lineTotal;
                try
                {
                    lineTotal = product.SellingPrice * quantity;
                    totalAmount += lineTotal;
                }
                catch (OverflowException)
                {
                    return null;
                }

                savedDetails.Add(new OrderDetail
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.SellingPrice,
                    LineTotal = lineTotal
                });
            }

            if (order.CustomerPayment < totalAmount)
            {
                return null;
            }

            var savedOrder = new Order
            {
                UserId = order.UserId,
                CreatedAt = DateTime.Now,
                TotalAmount = totalAmount,
                CustomerPayment = order.CustomerPayment,
                ChangeAmount = order.CustomerPayment - totalAmount
            };

            db.Orders.Add(savedOrder);
            db.SaveChanges();

            foreach (var detail in savedDetails)
            {
                detail.OrderId = savedOrder.Id;
                products[detail.ProductId].StockQuantity -= detail.Quantity;
            }

            db.OrderDetails.AddRange(savedDetails);
            db.SaveChanges();
            transaction.Commit();

            return savedOrder;
        }
    }
}
