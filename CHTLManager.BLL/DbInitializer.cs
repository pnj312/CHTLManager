using CHTLManager.BLL.Security;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL;

/// <summary>Tạo tài khoản đầu tiên; dữ liệu mẫu chỉ nạp khi chạy demo và cửa hàng chưa có dữ liệu.</summary>
public static class DbInitializer
{
    public static void Initialize(bool seedDemoData, DateTime? now = null)
    {
        using var db = new AppDbContext();

        if (!db.Users.Any())
        {
            db.Users.Add(new User { Username = "admin", PasswordHash = PasswordHasher.Hash("admin123"), FullName = "Quản trị viên", Role = UserRole.Admin });
            db.SaveChanges();
        }

        if (seedDemoData && !db.Products.Any() && !db.Orders.Any())
            new DemoData(db, now ?? DateTime.Now).Seed();
    }

    /// <summary>
    /// Bảy ngày hoạt động mẫu theo đúng thứ tự thời gian, qua đủ chứng từ: phiếu nhập, trả tiền, bán hàng, kiểm kê có xử lý.
    /// Mọi con số đều cố định và khớp nhau như cửa hàng thật: mỗi sản phẩm có giá vốn riêng, tồn kho đều từ phiếu nhập.
    /// Sửa kịch bản này thì phải sửa cả DemoCatalogTests và docs/DEMO.md.
    /// </summary>
    private sealed class DemoData
    {
        private readonly AppDbContext _db;
        private readonly DateTime _now;
        private readonly Dictionary<DateTime, int> _receiptsPerDay = new();
        private readonly Dictionary<string, Product> _products = new();
        private User _admin = null!, _cashier = null!;
        private Supplier _drinks = null!, _food = null!, _household = null!, _unilever = null!;

        /// <summary>Hóa đơn theo số ngày trước hôm nay: "giờ sảnPhẩm:sốLượng ...".</summary>
        private static readonly Dictionary<int, string[]> Sales = new()
        {
            [6] = new[] { "08:15 hh:2 co:2", "10:40 om:1 su:2", "14:05 aq:3 oi:1", "17:30 or:1 tr:2" },
            [5] = new[] { "07:50 hh:3 aq:2", "11:20 co:4 oi:2", "15:45 dg:1 kh:1", "19:10 g7:1 bu:2" },
            [4] = new[] { "08:30 su:4", "12:15 om:2 tr:1 or:1", "16:00 co:2 aq:2", "20:05 hh:5 oi:1" },
            [3] = new[] { "09:05 dg:1", "13:40 g7:1 su:2", "18:20 co:3 hh:2" },
            [2] = new[] { "08:10 aq:4 tr:2", "11:55 or:2 kh:2", "17:15 dg:1 bu:3", "20:30 om:3" },
            [1] = new[] { "07:35 hh:4 su:2", "10:50 co:2 oi:2", "14:30 g7:1", "19:45 dg:1 aq:2" },
            [0] = new[] { "08:20 hh:2 tr:1", "10:10 dg:1 co:1" }
        };

        public DemoData(AppDbContext db, DateTime now)
        {
            _db = db;
            _now = now;
        }

        public void Seed()
        {
            CreateCatalog();
            OpeningReceipts(_now.Date.AddDays(-7).AddHours(8));

            for (int dayOffset = 6; dayOffset >= 0; dayOffset--)
            {
                DateTime day = _now.Date.AddDays(-dayOffset);
                MorningEvents(dayOffset, day.AddHours(6));
                SeedOrders(dayOffset, day);
                EveningEvents(dayOffset, day.AddHours(22));
            }

            _db.SaveChanges();
        }

        private void CreateCatalog()
        {
            _admin = _db.Users.First(u => u.Role == UserRole.Admin);
            _cashier = _db.Users.FirstOrDefault(u => u.Role == UserRole.Cashier)
                       ?? new User { Username = "thungan", PasswordHash = PasswordHasher.Hash("123456"), FullName = "Thu ngân 01", Role = UserRole.Cashier };
            if (_cashier.Id == 0) _db.Users.Add(_cashier);

            AddProduct("hh", "8934563138165", "Mì Hảo Hảo tôm chua cay", "Mì ăn liền", 4500);
            AddProduct("om", "8934563138172", "Mì Omachi sườn hầm", "Mì ăn liền", 8000);
            AddProduct("aq", "8935049500018", "Nước suối Aquafina 500ml", "Đồ uống", 6000);
            AddProduct("co", "8934588012105", "Coca-Cola lon 330ml", "Đồ uống", 10000);
            AddProduct("tr", "8935024140013", "Trà xanh 0 độ 455ml", "Đồ uống", 10000);
            AddProduct("su", "8934673101011", "Sữa tươi Vinamilk 180ml", "Sữa", 8500);
            AddProduct("or", "8934804023012", "Bánh Oreo vani 133g", "Bánh kẹo", 16000);
            AddProduct("oi", "8935001710015", "Snack Oishi tôm cay 40g", "Bánh kẹo", 7000);
            AddProduct("g7", "8936017360018", "Cà phê G7 3in1 (hộp 18 gói)", "Cà phê", 52000);
            AddProduct("dg", "8934868150012", "Dầu gội Clear 330g", "Chăm sóc cá nhân", 98000);
            AddProduct("kh", "8935210600019", "Khăn giấy Pulppy 2 lớp", "Gia dụng", 12000);
            AddProduct("bu", "8936079120011", "Bút bi Thiên Long TL-027", "Văn phòng phẩm", 4000);

            _drinks = AddSupplier("NPP Đồ uống", "0901000001");
            _food = AddSupplier("NPP Thực phẩm", "0901000002");
            _household = AddSupplier("NPP Tạp hóa", "0901000003");
            _unilever = AddSupplier("NPP Unilever", "0901000004");
            _db.SaveChanges();
        }

        private void AddProduct(string key, string barcode, string name, string category, decimal price)
        {
            var product = new Product { Barcode = barcode, Name = name, Category = category, Price = price };
            _products[key] = product;
            _db.Products.Add(product);
        }

        private Supplier AddSupplier(string name, string phone)
        {
            var supplier = new Supplier { Name = name, NameKey = name.ToLowerInvariant(), Phone = phone };
            _db.Suppliers.Add(supplier);
            return supplier;
        }

        /// <summary>Cửa hàng mở với kệ trống: toàn bộ hàng đầu kỳ đều có hóa đơn của nhà cung cấp.</summary>
        private void OpeningReceipts(DateTime at)
        {
            Receipt(_drinks, "ĐU-1001", at, ("co", 96, 96, 7000m), ("aq", 120, 120, 3800m), ("tr", 72, 72, 6800m));
            Receipt(_food, "TP-2001", at, ("hh", 100, 100, 3400m), ("om", 60, 60, 5600m), ("su", 90, 90, 6500m),
                ("or", 40, 40, 11500m), ("oi", 55, 55, 5000m), ("g7", 25, 25, 40000m));
            Receipt(_household, "TH-3001", at, ("kh", 50, 50, 8500m), ("bu", 200, 200, 2500m));
            Receipt(_unilever, "UL-4001", at, ("dg", 10, 10, 72000m));
        }

        private void MorningEvents(int dayOffset, DateTime morning)
        {
            switch (dayOffset)
            {
                case 6:
                    Pay(_unilever, 720000, morning, "Chuyển khoản, trả đủ hóa đơn UL-4001");
                    break;
                case 5:
                    Receipt(_drinks, "ĐU-1002", morning, ("co", 48, 48, 7200m));
                    break;
                case 4:
                    Pay(_drinks, 1000000, morning, "Tiền mặt");
                    break;
                case 3:
                    // Hóa đơn ghi 50 gói mì nhưng chỉ nhận 48: chỉ nợ theo số thực nhận.
                    Receipt(_food, "TP-2002", morning, ("hh", 50, 48, 3500m), ("oi", 20, 20, 5200m));
                    break;
                case 2:
                    Pay(_food, 500000, morning, "Tiền mặt");
                    break;
            }
        }

        private void EveningEvents(int dayOffset, DateTime evening)
        {
            switch (dayOffset)
            {
                case 3:
                    Shortage("or", 2, evening, DiscrepancyResolution.NormalLoss, "Mất");
                    break;
                case 2:
                    Shortage("su", 3, evening, DiscrepancyResolution.NormalLoss, "Hết hạn");
                    break;
                case 1:
                    Shortage("oi", 1, evening, DiscrepancyResolution.NormalLoss, "Hỏng");
                    var torn = Shortage("kh", 2, evening, DiscrepancyResolution.Compensation, "Làm rách khi xếp hàng");
                    torn.ResponsibleUser = _cashier;
                    torn.CompensationAmount = torn.Value;
                    _db.CompensationPayments.Add(new CompensationPayment
                    {
                        StockDiscrepancy = torn,
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
            string[] plan = Sales[dayOffset];
            for (int i = 0; i < plan.Length; i++)
            {
                string[] parts = plan[i].Split(' ');
                DateTime time = day + TimeSpan.Parse(parts[0]);

                // Mở app sớm trong ngày thì hóa đơn hôm nay vẫn có đủ, nhưng không được nằm ở tương lai.
                if (time > _now)
                {
                    DateTime earlier = _now.AddSeconds(i - plan.Length);
                    time = earlier < _now.Date ? _now.Date : earlier;
                }

                var lines = new List<OrderDetail>();
                foreach (string part in parts.Skip(1))
                {
                    string[] item = part.Split(':');
                    var product = _products[item[0]];
                    int quantity = int.Parse(item[1]);
                    product.Stock -= quantity;
                    lines.Add(new OrderDetail { Product = product, Quantity = quantity, UnitPrice = product.Price, UnitCost = product.AverageCost });
                }

                decimal total = OrderService.CalculateTotal(lines);
                decimal step = total <= 50000 ? 10000 : 50000;
                decimal paid = Math.Ceiling(total / step) * step;

                _db.Orders.Add(new Order
                {
                    Code = $"HD{day:yyyyMMdd}-{i + 1:D4}",
                    CreatedAt = time,
                    Cashier = i % 4 == 3 ? _admin : _cashier,
                    Total = total,
                    CustomerPaid = paid,
                    Change = OrderService.CalculateChange(total, paid),
                    Details = lines
                });
            }
        }

        private void Pay(Supplier supplier, decimal amount, DateTime at, string note)
            => _db.SupplierPayments.Add(new SupplierPayment { Supplier = supplier, Amount = amount, PaidAt = at, UserId = _admin.Id, Note = note });

        private void Receipt(Supplier supplier, string invoice, DateTime at,
            params (string Key, int Invoiced, int Received, decimal Cost)[] lines)
        {
            _receiptsPerDay[at.Date] = _receiptsPerDay.GetValueOrDefault(at.Date) + 1;
            var receipt = new PurchaseReceipt
            {
                Code = $"PN{at:yyyyMMdd}-{_receiptsPerDay[at.Date]:D4}",
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

            foreach (var (key, invoiced, received, cost) in lines)
            {
                var product = _products[key];
                receipt.Lines.Add(new PurchaseReceiptLine
                {
                    Product = product,
                    InvoicedQuantity = invoiced,
                    ReceivedQuantity = received,
                    UnitCost = cost,
                    CostBaseStock = product.AverageCost > 0 ? product.Stock : 0
                });
                product.AverageCost = CostCalculator.AddToAverage(product.Stock, product.AverageCost, received, cost);
                product.LastCost = cost;

                int before = product.Stock;
                product.Stock += received;
                var movement = Log(product, at, StockMovementType.ReceiptIn, before, product.Stock);
                movement.PurchaseReceipt = receipt;
                movement.Supplier = supplier.Name;
                movement.InvoiceNo = invoice;
            }
        }

        private StockDiscrepancy Shortage(string key, int missing, DateTime at, DiscrepancyResolution resolution, string note)
        {
            var product = _products[key];
            int before = product.Stock;
            product.Stock -= missing;
            Log(product, at, StockMovementType.StockCount, before, product.Stock);

            var discrepancy = new StockDiscrepancy
            {
                Product = product,
                CountedByUserId = _admin.Id,
                CountedAt = at,
                BookStock = before,
                CountedStock = product.Stock,
                Delta = -missing,
                UnitCost = product.AverageCost,
                Status = DiscrepancyStatus.Resolved,
                Resolution = resolution,
                ResolvedByUserId = _admin.Id,
                ResolvedAt = at,
                Note = note
            };
            _db.StockDiscrepancies.Add(discrepancy);
            return discrepancy;
        }

        private StockMovement Log(Product product, DateTime at, StockMovementType type, int before, int after)
        {
            var movement = new StockMovement
            {
                Product = product,
                UserId = _admin.Id,
                CreatedAt = at,
                Type = type,
                BeforeStock = before,
                AfterStock = after,
                Delta = after - before,
                UnitPrice = product.Price
            };
            _db.StockMovements.Add(movement);
            return movement;
        }
    }
}
