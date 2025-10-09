using Microsoft.EntityFrameworkCore;

namespace SharpLlama.Entities;

public partial class NorthwindStarterContext : DbContext
{
    public NorthwindStarterContext()
    {
    }

    public NorthwindStarterContext(DbContextOptions<NorthwindStarterContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AiContextChunk> AiContextChunks { get; set; }

    public virtual DbSet<CatalogTableOfContent> CatalogTableOfContents { get; set; }

    public virtual DbSet<Civi> Civis { get; set; }

    public virtual DbSet<CiviContactform001> CiviContactform001s { get; set; }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<CompanyType> CompanyTypes { get; set; }

    public virtual DbSet<Contact> Contacts { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<EmployeePrivilege> EmployeePrivileges { get; set; }

    public virtual DbSet<Learn> Learns { get; set; }

    public virtual DbSet<Mru> Mrus { get; set; }

    public virtual DbSet<NorthwindFeature> NorthwindFeatures { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<OrderDetailStatus> OrderDetailStatuses { get; set; }

    public virtual DbSet<OrderStatus> OrderStatuses { get; set; }

    public virtual DbSet<Privilege> Privileges { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductCategory> ProductCategories { get; set; }

    public virtual DbSet<ProductVendor> ProductVendors { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderDetail> PurchaseOrderDetails { get; set; }

    public virtual DbSet<PurchaseOrderStatus> PurchaseOrderStatuses { get; set; }

    public virtual DbSet<State> States { get; set; }

    public virtual DbSet<StockTake> StockTakes { get; set; }

    public virtual DbSet<String> Strings { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<TaxStatus> TaxStatuses { get; set; }

    public virtual DbSet<Title> Titles { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    public virtual DbSet<Welcome> Welcomes { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=LAPTOP-57G6RU3I\\TLEXDEVSQL;Initial Catalog=NorthwindStarter;Integrated Security=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiContextChunk>(entity =>
        {
            entity.HasKey(e => e.ChunkId).HasName("PK__AI_Conte__FBFF9D2059B719B7");

            entity.ToTable("AI_ContextChunks");

            entity.Property(e => e.ChunkId).HasColumnName("ChunkID");
            entity.Property(e => e.SourceKey).HasMaxLength(100);
            entity.Property(e => e.SourceTable).HasMaxLength(100);
        });

        modelBuilder.Entity<CatalogTableOfContent>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("Catalog_TableOfContents");
        });

        modelBuilder.Entity<Civi>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Civi___3214EC07CAE21CB7");

            entity.ToTable("Civi_");
        });

        modelBuilder.Entity<CiviContactform001>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Civi_con__3214EC07772BADEA");

            entity.ToTable("Civi_contactform001");

            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .HasColumnName("email");
            entity.Property(e => e.FirstName)
                .HasMaxLength(512)
                .HasColumnName("firstName");
            entity.Property(e => e.Message).HasColumnName("message");
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.Property(e => e.CompanyId)
                .ValueGeneratedNever()
                .HasColumnName("CompanyID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.CompanyTypeId).HasColumnName("CompanyTypeID");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.StandardTaxStatusId)
                .HasColumnType("decimal(18, 4)")
                .HasColumnName("StandardTaxStatusID");

            entity.HasOne(d => d.CompanyType).WithMany(p => p.Companies)
                .HasForeignKey(d => d.CompanyTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Companies_CompanyTypes");
        });

        modelBuilder.Entity<CompanyType>(entity =>
        {
            entity.Property(e => e.CompanyTypeId)
                .ValueGeneratedNever()
                .HasColumnName("CompanyTypeID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.CompanyType1).HasColumnName("CompanyType");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.Property(e => e.ContactId)
                .ValueGeneratedNever()
                .HasColumnName("ContactID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyID");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");

            entity.HasOne(d => d.Company).WithMany(p => p.Contacts)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contacts_Companies");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(e => e.EmployeeId)
                .ValueGeneratedNever()
                .HasColumnName("EmployeeID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.SupervisorId).HasColumnName("SupervisorID");
        });

        modelBuilder.Entity<EmployeePrivilege>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.EmployeePrivilegeId).HasColumnName("EmployeePrivilegeID");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.PrivilegeId).HasColumnName("PrivilegeID");
        });

        modelBuilder.Entity<Learn>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("Learn");

            entity.Property(e => e.Id).HasColumnName("ID");
        });

        modelBuilder.Entity<Mru>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("MRU");

            entity.Property(e => e.DateAdded).HasColumnType("datetime");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.MruId).HasColumnName("MRU_ID");
            entity.Property(e => e.Pkvalue).HasColumnName("PKValue");
        });

        modelBuilder.Entity<NorthwindFeature>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.NorthwindFeaturesId).HasColumnName("NorthwindFeaturesID");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.OrderId)
                .ValueGeneratedNever()
                .HasColumnName("OrderID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.OrderDate).HasColumnType("datetime");
            entity.Property(e => e.OrderStatusId).HasColumnName("OrderStatusID");
            entity.Property(e => e.PaidDate).HasColumnType("datetime");
            entity.Property(e => e.ShippedDate).HasColumnType("datetime");
            entity.Property(e => e.ShipperId).HasColumnName("ShipperID");
            entity.Property(e => e.TaxStatusId)
                .HasColumnType("decimal(18, 4)")
                .HasColumnName("TaxStatusID");

            entity.HasOne(d => d.Customer).WithMany(p => p.OrderCustomers)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_Orders_Companies");

            entity.HasOne(d => d.Employee).WithMany(p => p.Orders)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK_Orders_Employees");

            entity.HasOne(d => d.OrderStatus).WithMany(p => p.Orders)
                .HasForeignKey(d => d.OrderStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_OrderStatus");

            entity.HasOne(d => d.Shipper).WithMany(p => p.OrderShippers)
                .HasForeignKey(d => d.ShipperId)
                .HasConstraintName("FK_Orders_Companies1");

            entity.HasOne(d => d.Tax).WithMany(p => p.Orders)
                .HasForeignKey(d => d.TaxId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_TaxStatus");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.Property(e => e.OrderDetailId)
                .ValueGeneratedNever()
                .HasColumnName("OrderDetailID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.OrderDetailStatusId).HasColumnName("OrderDetailStatusID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
        });

        modelBuilder.Entity<OrderDetailStatus>(entity =>
        {
            entity.ToTable("OrderDetailStatus");

            entity.Property(e => e.OrderDetailStatusId)
                .ValueGeneratedNever()
                .HasColumnName("OrderDetailStatusID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.SortOrder).HasColumnType("decimal(18, 4)");
        });

        modelBuilder.Entity<OrderStatus>(entity =>
        {
            entity.ToTable("OrderStatus");

            entity.Property(e => e.OrderStatusId)
                .ValueGeneratedNever()
                .HasColumnName("OrderStatusID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.SortOrder).HasColumnType("decimal(18, 4)");
        });

        modelBuilder.Entity<Privilege>(entity =>
        {
            entity.Property(e => e.PrivilegeId)
                .ValueGeneratedNever()
                .HasColumnName("PrivilegeID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.ProductId)
                .ValueGeneratedNever()
                .HasColumnName("ProductID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.ProductCategoryId).HasColumnName("ProductCategoryID");

            entity.HasOne(d => d.ProductCategory).WithMany(p => p.Products)
                .HasForeignKey(d => d.ProductCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_ProductCategories");
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.Property(e => e.ProductCategoryId)
                .ValueGeneratedNever()
                .HasColumnName("ProductCategoryID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
        });

        modelBuilder.Entity<ProductVendor>(entity =>
        {
            entity.Property(e => e.ProductVendorId)
                .ValueGeneratedNever()
                .HasColumnName("ProductVendorID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.VendorId).HasColumnName("VendorID");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVendors)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductVendors_Products");

            entity.HasOne(d => d.Vendor).WithMany(p => p.ProductVendors)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductVendors_Companies");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.Property(e => e.PurchaseOrderId)
                .ValueGeneratedNever()
                .HasColumnName("PurchaseOrderID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ApprovedById).HasColumnName("ApprovedByID");
            entity.Property(e => e.ApprovedDate).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.PaymentDate).HasColumnType("datetime");
            entity.Property(e => e.ReceivedDate).HasColumnType("datetime");
            entity.Property(e => e.StatusId).HasColumnName("StatusID");
            entity.Property(e => e.SubmittedById).HasColumnName("SubmittedByID");
            entity.Property(e => e.SubmittedDate).HasColumnType("datetime");
            entity.Property(e => e.VendorId).HasColumnName("VendorID");

            entity.HasOne(d => d.ApprovedBy).WithMany(p => p.PurchaseOrderApprovedBies)
                .HasForeignKey(d => d.ApprovedById)
                .HasConstraintName("FK_PurchaseOrders_Employees");

            entity.HasOne(d => d.Status).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.StatusId)
                .HasConstraintName("FK_PurchaseOrders_PurchaseOrderStatus");

            entity.HasOne(d => d.SubmittedBy).WithMany(p => p.PurchaseOrderSubmittedBies)
                .HasForeignKey(d => d.SubmittedById)
                .HasConstraintName("FK_PurchaseOrders_Employees1");

            entity.HasOne(d => d.Vendor).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.VendorId)
                .HasConstraintName("FK_PurchaseOrders_Companies");
        });

        modelBuilder.Entity<PurchaseOrderDetail>(entity =>
        {
            entity.Property(e => e.PurchaseOrderDetailId)
                .ValueGeneratedNever()
                .HasColumnName("PurchaseOrderDetailID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");
            entity.Property(e => e.ReceivedDate).HasColumnType("datetime");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseOrderDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_PurchaseOrderDetails_Products");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderDetails)
                .HasForeignKey(d => d.PurchaseOrderId)
                .HasConstraintName("FK_PurchaseOrderDetails_PurchaseOrders");
        });

        modelBuilder.Entity<PurchaseOrderStatus>(entity =>
        {
            entity.HasKey(e => e.StatusId);

            entity.ToTable("PurchaseOrderStatus");

            entity.Property(e => e.StatusId)
                .ValueGeneratedNever()
                .HasColumnName("StatusID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.SortOrder).HasColumnType("decimal(18, 4)");
        });

        modelBuilder.Entity<State>(entity =>
        {
            entity.HasNoKey();
        });

        modelBuilder.Entity<StockTake>(entity =>
        {
            entity.ToTable("StockTake");

            entity.Property(e => e.StockTakeId)
                .ValueGeneratedNever()
                .HasColumnName("StockTakeID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.StockTakeDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<String>(entity =>
        {
            entity.Property(e => e.StringId)
                .ValueGeneratedNever()
                .HasColumnName("StringID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingId);

            entity.Property(e => e.SettingId)
                .ValueGeneratedNever()
                .HasColumnName("SettingID");
        });

        modelBuilder.Entity<TaxStatus>(entity =>
        {
            entity.HasKey(e => e.TaxId);

            entity.ToTable("TaxStatus");

            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.TaxStatus1).HasColumnName("TaxStatus");
            entity.Property(e => e.TaxStatusId)
                .HasColumnType("decimal(18, 4)")
                .HasColumnName("TaxStatusID");
        });

        modelBuilder.Entity<Title>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Title1).HasColumnName("Title");
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => e.SettingId);

            entity.Property(e => e.SettingId)
                .ValueGeneratedNever()
                .HasColumnName("SettingID");
        });

        modelBuilder.Entity<Welcome>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("Welcome");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Welcome1).HasColumnName("Welcome");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
