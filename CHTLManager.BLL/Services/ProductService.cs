using System;
using System.Collections.Generic;
using System.Linq;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL.Services
{
    public class ProductService
    {
        public ProductService()
        {
        }

        public List<Product> GetAll()
        {
            using var db = new AppDbContext();
            return db.Products.AsNoTracking().OrderBy(p => p.Id).ToList();
        }

        public List<Product> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return GetAll();
            }

            keyword = keyword.Trim();

            using var db = new AppDbContext();
            return db.Products.AsNoTracking()
                .Where(p => p.Barcode.Contains(keyword) || p.Name.Contains(keyword))
                .OrderBy(p => p.Id)
                .ToList();
        }

        public Product? GetByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return null;
            }

            using var db = new AppDbContext();
            return db.Products.AsNoTracking().FirstOrDefault(p => p.Barcode == barcode);
        }

        public bool Add(Product product)
        {
            if (!IsValidProduct(product))
            {
                return false;
            }

            using var db = new AppDbContext();
            if (db.Products.Any(p => p.Barcode == product.Barcode))
            {
                return false;
            }

            db.Products.Add(product);
            db.SaveChanges();
            return true;
        }

        public bool Update(Product product)
        {
            if (!IsValidProduct(product))
            {
                return false;
            }

            using var db = new AppDbContext();
            var existing = db.Products.Find(product.Id);
            if (existing is null || db.Products.Any(p => p.Id != product.Id && p.Barcode == product.Barcode))
            {
                return false;
            }

            existing.Barcode = product.Barcode;
            existing.Name = product.Name;
            existing.ImportPrice = product.ImportPrice;
            existing.SellingPrice = product.SellingPrice;
            existing.StockQuantity = product.StockQuantity;
            existing.Unit = product.Unit;

            db.SaveChanges();
            return true;
        }

        public bool Delete(int id)
        {
            using var db = new AppDbContext();
            var product = db.Products.Find(id);
            if (product is null)
            {
                return false;
            }

            db.Products.Remove(product);
            db.SaveChanges();
            return true;
        }

        public bool UpdateStock(int productId, int quantityChange)
        {
            using var db = new AppDbContext();
            var product = db.Products.Find(productId);
            if (product is null)
            {
                return false;
            }

            long newQuantity = (long)product.StockQuantity + quantityChange;
            if (newQuantity < 0 || newQuantity > int.MaxValue)
            {
                return false;
            }

            product.StockQuantity = (int)newQuantity;
            db.SaveChanges();
            return true;
        }

        private static bool IsValidProduct(Product? product)
        {
            return product is not null
                && !string.IsNullOrWhiteSpace(product.Barcode)
                && !string.IsNullOrWhiteSpace(product.Name)
                && product.ImportPrice >= 0
                && product.SellingPrice >= 0
                && product.StockQuantity >= 0;
        }
    }
}
