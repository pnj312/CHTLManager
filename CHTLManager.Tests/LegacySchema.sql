CREATE TABLE "Products" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,
    "Barcode" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "Price" decimal(18,0) NOT NULL,
    "Stock" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL
);


CREATE TABLE "Users" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "Username" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "FullName" TEXT NOT NULL,
    "Role" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL
);


CREATE TABLE "Orders" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY AUTOINCREMENT,
    "Code" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CashierId" INTEGER NOT NULL,
    "Total" decimal(18,0) NOT NULL,
    "CustomerPaid" decimal(18,0) NOT NULL,
    "Change" decimal(18,0) NOT NULL,
    CONSTRAINT "FK_Orders_Users_CashierId" FOREIGN KEY ("CashierId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "OrderDetails" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_OrderDetails" PRIMARY KEY AUTOINCREMENT,
    "OrderId" INTEGER NOT NULL,
    "ProductId" INTEGER NOT NULL,
    "Quantity" INTEGER NOT NULL,
    "UnitPrice" decimal(18,0) NOT NULL,
    CONSTRAINT "FK_OrderDetails_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_OrderDetails_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "StockMovements" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_StockMovements" PRIMARY KEY AUTOINCREMENT,
    "ProductId" INTEGER NOT NULL,
    "UserId" INTEGER NOT NULL,
    "OrderId" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "BeforeStock" INTEGER NOT NULL,
    "AfterStock" INTEGER NOT NULL,
    "Delta" INTEGER NOT NULL,
    "Reason" TEXT NOT NULL,
    "UnitPrice" decimal(18,0) NOT NULL,
    "Supplier" TEXT NULL,
    "InvoiceNo" TEXT NULL,
    CONSTRAINT "FK_StockMovements_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_StockMovements_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_StockMovements_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);


CREATE INDEX "IX_OrderDetails_OrderId" ON "OrderDetails" ("OrderId");


CREATE INDEX "IX_OrderDetails_ProductId" ON "OrderDetails" ("ProductId");


CREATE INDEX "IX_Orders_CashierId" ON "Orders" ("CashierId");


CREATE UNIQUE INDEX "IX_Orders_Code" ON "Orders" ("Code");


CREATE UNIQUE INDEX "IX_Products_Barcode" ON "Products" ("Barcode");


CREATE INDEX "IX_StockMovements_OrderId" ON "StockMovements" ("OrderId");


CREATE INDEX "IX_StockMovements_ProductId" ON "StockMovements" ("ProductId");


CREATE INDEX "IX_StockMovements_UserId" ON "StockMovements" ("UserId");


CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");


