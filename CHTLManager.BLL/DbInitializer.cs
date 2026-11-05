using CHTLManager.BLL.Security;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL;

/// <summary>Tạo tài khoản đầu tiên; dữ liệu mẫu chỉ nạp khi chạy demo và cửa hàng chưa có dữ liệu.</summary>
public static class DbInitializer
{
    public static void Initialize(bool seedDemoData)
    {
        using var db = new AppDbContext();

        if (!db.Users.Any())
        {
            db.Users.Add(new User { Username = "admin", PasswordHash = PasswordHasher.Hash("admin123"), FullName = "Quản trị viên", Role = UserRole.Admin });
            db.SaveChanges();
        }

        if (seedDemoData && !db.Products.Any() && !db.Orders.Any())
            new DemoData(db, DateTime.Now).Seed();
    }

    /// <summary>
    /// Bảy ngày hoạt động mẫu theo đúng thứ tự thời gian, qua đủ chứng từ: tồn đầu, phiếu nhập, trả tiền, bán hàng,
    /// kiểm kê có xử lý. Các con số khớp nhau như cửa hàng thật. Kết quả cố định nhờ seed ngẫu nhiên cố định.
    /// </summary>
    private sealed class DemoData
    {
        private const int OpenMinute = 7 * 60, CloseMinute = 21 * 60;

        private readonly AppDbContext _db;
        private readonly DateTime _now;
        private readonly Random _rng = new(2026);
        private User _admin = null!, _cashier = null!;
        private List<Product> _products = null!;
        private Supplier _cocaSupplier = null!, _generalSupplier = null!;

        public DemoData(AppDbContext db, DateTime now)
        {
            _db = db;
            _now = now;
        }

        public void Seed()
        {
            CreateCatalog();

            for (int dayOffset = 6; dayOffset >= 0; dayOffset--)
            {
                DateTime day = _now.Date.AddDays(-dayOffset);
                MorningEvents(dayOffset, day);
                SeedOrders(dayOffset, day);
                EveningEvents(dayOffset, day);
            }

            _db.SaveChanges();
        }

        private void CreateCatalog()
        {
            _admin = _db.Users.First(u => u.Role == UserRole.Admin);
            _cashier = _db.Users.FirstOrDefault(u => u.Role == UserRole.Cashier)
                       ?? new User { Username = "thungan", PasswordHash = PasswordHasher.Hash("123456"), FullName = "Thu ngân 01", Role = UserRole.Cashier };
            if (_cashier.Id == 0) _db.Users.Add(_cashier);

            _products = new List<Product>
            {
                new() { Barcode = "8934563138165", Name = "Mì Hảo Hảo tôm chua cay", Category = "Mì ăn liền", Price = 4500, Stock = 100 },
                new() { Barcode = "8934563138172", Name = "Mì Omachi sườn hầm", Category = "Mì ăn liền", Price = 8000, Stock = 60 },
                new() { Barcode = "8935049500018", Name = "Nước suối Aquafina 500ml", Category = "Đồ uống", Price = 6000, Stock = 120 },
                new() { Barcode = "8934588012105", Name = "Coca-Cola lon 330ml", Category = "Đồ uống", Price = 10000, Stock = 80 },
                new() { Barcode = "8935024140013", Name = "Trà xanh 0 độ 455ml", Category = "Đồ uống", Price = 10000, Stock = 70 },
                new() { Barcode = "8934673101011", Name = "Sữa tươi Vinamilk 180ml", Category = "Sữa", Price = 8500, Stock = 90 },
                new() { Barcode = "8934804023012", Name = "Bánh Oreo vani 133g", Category = "Bánh kẹo", Price = 16000, Stock = 40 },
                new() { Barcode = "8935001710015", Name = "Snack Oishi tôm cay 40g", Category = "Bánh kẹo", Price = 7000, Stock = 55 },
                new() { Barcode = "8936017360018", Name = "Cà phê G7 3in1 (hộp 18 gói)", Category = "Cà phê", Price = 52000, Stock = 25 },
                new() { Barcode = "8934868150012", Name = "Dầu gội Clear 330g", Category = "Chăm sóc cá nhân", Price = 98000, Stock = 15 },
                new() { Barcode = "8935210600019", Name = "Khăn giấy Pulppy 2 lớp", Category = "Gia dụng", Price = 12000, Stock = 50 },
                new() { Barcode = "8936079120011", Name = "Bút bi Thiên Long TL-027", Category = "Văn phòng phẩm", Price = 4000, Stock = 200 }
            };

            DateTime opening = _now.Date.AddDays(-7).AddHours(7);
            foreach (var p in _products)
            {
                p.AverageCost = p.LastCost = Math.Round(p.Price * 0.7m, 0);
                _db.Products.Add(p);
                Log(p, opening, StockMovementType.Initial, 0, p.Stock);
            }

            _cocaSupplier = new Supplier { Name = "NPP Coca-Cola", NameKey = "npp coca-cola", Phone = "0901000001" };
            _generalSupplier = new Supplier { Name = "NPP Tổng hợp", NameKey = "npp tổng hợp", Phone = "0901000002" };
            _db.Suppliers.AddRange(_cocaSupplier, _generalSupplier);
            _db.SaveChanges();
        }

        private Product Find(string barcode) => _products.First(p => p.Barcode == barcode);

        private void MorningEvents(int dayOffset, DateTime day)
        {
            DateTime morning = day.AddHours(6);
            switch (dayOffset)
            {
                case 5:
                    Receipt(_cocaSupplier, "0001025", morning, 1, ("8934588012105", 24, 24, 7200m));
                    break;
                case 4:
                    _db.SupplierPayments.Add(new SupplierPayment
                    {
                        Supplier = _cocaSupplier,
                        Amount = 100000,
                        PaidAt = morning,
                        UserId = _admin.Id,
                        Note = "Tiền mặt"
                    });
                    break;
                case 3:
                    // Hóa đơn ghi 50 gói mì nhưng chỉ nhận 48: chỉ nợ theo số thực nhận.
                    Receipt(_generalSupplier, "TH-778", morning, 1,
                        ("8934563138165", 50, 48, 3100m), ("8935001710015", 20, 20, 5000m));
                    break;
            }
        }

        private void EveningEvents(int dayOffset, DateTime day)
        {
            DateTime evening = day.AddHours(22);
            switch (dayOffset)
            {
                case 3:
                    Shortage("8934804023012", 2, evening, DiscrepancyResolution.NormalLoss, "Mất");
                    break;
                case 2:
                    Shortage("8934673101011", 3, evening, DiscrepancyResolution.NormalLoss, "Hết hạn");
                    break;
                case 1:
                    Shortage("8935001710015", 1, evening, DiscrepancyResolution.NormalLoss, "Hỏng");
                    var broken = Shortage("8935210600019", 2, evening, DiscrepancyResolution.Compensation, "Làm rách khi xếp hàng");
                    broken.ResponsibleUser = _cashier;
                    broken.CompensationAmount = 16000;
                    _db.CompensationPayments.Add(new CompensationPayment
                    {
                        StockDiscrepancy = broken,
                        Amount = 10000,
                        PaidAt = evening,
                        UserId = _admin.Id,
                        Note = "Thu lần 1"
                    });
                    break;
            }
        }

        private void SeedOrders(int dayOffset, DateTime day)
        {
            // Hôm nay chỉ tạo hóa đơn trước thời điểm hiện tại.
            int lastMinute = dayOffset == 0 ? Math.Min(CloseMinute, (int)(_now - _now.Date).TotalMinutes - 5) : CloseMinute;
            if (lastMinute <= OpenMinute) return;

            int count = dayOffset == 0 ? 2 : _rng.Next(3, 6);
            var times = Enumerable.Range(0, count)
                .Select(_ => day.AddMinutes(_rng.Next(OpenMinute, lastMinute)))
                .OrderBy(t => t)
                .ToList();

            int seq = 0;
            foreach (var time in times)
            {
                var lines = new List<OrderDetail>();
                foreach (var p in _products.OrderBy(_ => _rng.Next()).Take(_rng.Next(1, 5)))
                {
                    int qty = Math.Min(_rng.Next(1, 4), p.Stock - 5);
                    if (qty <= 0) continue;

                    p.Stock -= qty;
                    lines.Add(new OrderDetail { Product = p, Quantity = qty, UnitPrice = p.Price, UnitCost = p.AverageCost });
                }
                if (lines.Count == 0) continue;

                decimal total = OrderService.CalculateTotal(lines);
                decimal step = total <= 50000 ? 10000 : 50000;
                decimal paid = Math.Ceiling(total / step) * step;

                seq++;
                _db.Orders.Add(new Order
                {
                    Code = $"HD{day:yyyyMMdd}-{seq:D4}",
                    CreatedAt = time,
                    Cashier = _rng.Next(4) == 0 ? _admin : _cashier,
                    Total = total,
                    CustomerPaid = paid,
                    Change = OrderService.CalculateChange(total, paid),
                    Details = lines
                });
            }
        }

        private void Receipt(Supplier supplier, string invoice, DateTime at, int sequence,
            params (string Barcode, int Invoiced, int Received, decimal Cost)[] lines)
        {
            var receipt = new PurchaseReceipt
            {
                Code = $"PN{at:yyyyMMdd}-{sequence:D4}",
                Supplier = supplier,
                InvoiceNo = invoice,
                InvoiceKey = invoice.ToUpperInvariant(),
                ReceivedAt = at,
                CreatedByUserId = _admin.Id,
                ConfirmedByUserId = _admin.Id,
                ConfirmedAt = at,
                Status = ReceiptStatus.Confirmed
            };
            _db.PurchaseReceipts.Add(receipt);

            foreach (var (barcode, invoiced, received, cost) in lines)
            {
                var p = Find(barcode);
                receipt.Lines.Add(new PurchaseReceiptLine { Product = p, InvoicedQuantity = invoiced, ReceivedQuantity = received, UnitCost = cost });
                p.AverageCost = CostCalculator.AddToAverage(p.Stock, p.AverageCost, received, cost);
                p.LastCost = cost;

                int before = p.Stock;
                p.Stock += received;
                var movement = Log(p, at, StockMovementType.ReceiptIn, before, p.Stock);
                movement.PurchaseReceipt = receipt;
                movement.Supplier = supplier.Name;
                movement.InvoiceNo = invoice;
            }
        }

        private StockDiscrepancy Shortage(string barcode, int missing, DateTime at, DiscrepancyResolution resolution, string note)
        {
            var p = Find(barcode);
            int before = p.Stock;
            p.Stock -= missing;
            Log(p, at, StockMovementType.StockCount, before, p.Stock);

            var discrepancy = new StockDiscrepancy
            {
                Product = p,
                CountedByUserId = _admin.Id,
                CountedAt = at,
                BookStock = before,
                CountedStock = p.Stock,
                Delta = -missing,
                UnitCost = p.AverageCost,
                Status = DiscrepancyStatus.Resolved,
                Resolution = resolution,
                ResolvedByUserId = _admin.Id,
                ResolvedAt = at,
                Note = note
            };
            _db.StockDiscrepancies.Add(discrepancy);
            return discrepancy;
        }

        private StockMovement Log(Product p, DateTime at, StockMovementType type, int before, int after)
        {
            var movement = new StockMovement
            {
                Product = p,
                UserId = _admin.Id,
                CreatedAt = at,
                Type = type,
                BeforeStock = before,
                AfterStock = after,
                Delta = after - before,
                UnitPrice = p.Price
            };
            _db.StockMovements.Add(movement);
            return movement;
        }
    }
}
