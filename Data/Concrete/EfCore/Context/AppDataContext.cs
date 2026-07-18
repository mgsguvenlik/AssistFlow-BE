using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Model.Concrete.Qnb;

namespace Data.Concrete.EfCore.Context
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options) : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerGroup> CustomerGroups { get; set; }
        public DbSet<CurrencyType> CurrencyTypes { get; set; }
        public DbSet<Model.Concrete.Model> Models { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductType> ProductTypes { get; set; }
        public DbSet<ProgressApprover> ProgressApprovers { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<ServiceType> ServiceTypes { get; set; }
        public DbSet<SystemType> SystemTypes { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<Region> Regions { get; set; }
        public DbSet<Configuration> Configurations { get; set; }
        public DbSet<Seeding.Infrastructure.SeedHistory> SeedHistories { get; set; } = null!;
        public DbSet<WorkFlow> WorkFlows { get; set; }
        public DbSet<WorkFlowStep> WorkFlowSteps { get; set; }
        public DbSet<ServicesRequest> ServicesRequests { get; set; }
        public DbSet<ServicesRequestProduct> ServicesRequestProducts { get; set; }
        public DbSet<CustomerProductPrice> CustomerProductPrices { get; set; }
        public DbSet<CustomerGroupProductPrice> CustomerGroupProductPrices { get; set; }
        public DbSet<TenantProductPrice> TenantProductPrices { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<TechnicalService> TechnicalServices { get; set; }
        public DbSet<TechnicalServiceImage> TechnicalServiceImages { get; set; }
        public DbSet<TechnicalServiceFormImage> TechnicalServiceFormImages { get; set; }
        public DbSet<WorkFlowTransition> WorkFlowTransitions { get; set; }
        public DbSet<WorkFlowActivityRecord> WorkFlowActivityRecords { get; set; }
        public DbSet<WorkFlowReviewLog> WorkFlowReviewLogs { get; set; } = default!;
        public DbSet<Pricing> Pricings { get; set; } = default!;
        public DbSet<MailOutbox> MailOutboxes { get; set; } = default!;
        public DbSet<FinalApproval> FinalApprovals { get; set; } = default!;

        public DbSet<Menu> Menus { get; set; }
        public DbSet<MenuRole> MenuRoles { get; set; }

        public DbSet<Notification> Notifications { get; set; } = default!;

        public DbSet<CustomerSystemAssignment> CustomerSystemAssignments { get; set; }
        public DbSet<WorkFlowArchive> WorkFlowArchives { get; set; }
        public DbSet<Tenant> Tenants { get; set; } = null!;
        public DbSet<UserFeedback>  UserFeedbacks { get; set; } = null!;
        public DbSet<WorkFlowSlaSetting> WorkFlowSlaSettings { get; set; } = null!;
        public DbSet<WorkingHourPolicy> WorkingHourPolicies { get; set; }
        public DbSet<TechnicalServiceWorkSession> TechnicalServiceWorkSessions { get; set; }

        public DbSet<WorkOrderType> WorkOrderTypes { get; set; } = null!;
        public DbSet<ServicesRequestWorkOrderType> ServicesRequestWorkOrderTypes { get; set; } = null!;


        #region YKB
        public DbSet<YkbCustomerForm> YkbCustomerForms { get; set; } = default!;
        public DbSet<YkbServicesRequest> YkbServicesRequests { get; set; } = default!;
        public DbSet<YkbServicesRequestWorkOrderType> YkbServicesRequestWorkOrderTypes { get; set; } = default!;
        public DbSet<YkbServicesRequestProduct> YkbServicesRequestProducts { get; set; } = default!;
        public DbSet<YkbTechnicalService> YkbTechnicalServices { get; set; } = default!;
        public DbSet<YkbTechnicalServiceImage> YkbTechnicalServiceImages { get; set; } = default!;
        public DbSet<YkbTechnicalServiceFormImage> YkbTechnicalServiceFormImages { get; set; } = default!;
        public DbSet<YkbPricing> YkbPricings { get; set; } = default!;
        public DbSet<YkbFinalApproval> YkbFinalApprovals { get; set; } = default!;
        public DbSet<YkbWarehouse> YkbWarehouses { get; set; } = default!;
        public DbSet<YkbWorkFlow> YkbWorkFlows { get; set; } = default!;
        public DbSet<YkbWorkFlowStep> YkbWorkFlowSteps { get; set; } = default!;
        public DbSet<YkbWorkFlowActivityRecord> YkbWorkFlowActivityRecords { get; set; } = default!;
        public DbSet<YkbWorkFlowArchive> YkbWorkFlowArchives { get; set; } = default!;
        public DbSet<YkbWorkFlowReviewLog> YkbWorkFlowReviewLogs { get; set; } = default!;
        public DbSet<YkbTechnicalServiceWorkSession> YkbTechnicalServiceWorkSessions { get; set; } = default!;
        public DbSet<YkbWorkflowAttachment> YkbWorkflowAttachments { get; set; }

        #endregion  

        #region QNB
        public DbSet<QnbCustomerForm> QnbCustomerForms { get; set; } = default!;
        public DbSet<QnbServicesRequest> QnbServicesRequests { get; set; } = default!;
        public DbSet<QnbServicesRequestProduct> QnbServicesRequestProducts { get; set; } = default!;
        public DbSet<QnbTechnicalService> QnbTechnicalServices { get; set; } = default!;
        public DbSet<QnbTechnicalServiceImage> QnbTechnicalServiceImages { get; set; } = default!;
        public DbSet<QnbTechnicalServiceFormImage> QnbTechnicalServiceFormImages { get; set; } = default!;
        public DbSet<QnbPricing> QnbPricings { get; set; } = default!;
        public DbSet<QnbFinalApproval> QnbFinalApprovals { get; set; } = default!;
        public DbSet<QnbWarehouse> QnbWarehouses { get; set; } = default!;
        public DbSet<QnbWorkFlow> QnbWorkFlows { get; set; } = default!;
        public DbSet<QnbWorkFlowStep> QnbWorkFlowSteps { get; set; } = default!;
        public DbSet<QnbWorkFlowActivityRecord> QnbWorkFlowActivityRecords { get; set; } = default!;
        public DbSet<QnbWorkFlowArchive> QnbWorkFlowArchives { get; set; } = default!;
        public DbSet<QnbWorkFlowReviewLog> QnbWorkFlowReviewLogs { get; set; } = default!;
        public DbSet<QnbTechnicalServiceWorkSession> QnbTechnicalServiceWorkSessions { get; set; } = default!;
        public DbSet<QnbServicesRequestWorkOrderType> QnbServicesRequestWorkOrderTypes { get; set; } = default!;

        #endregion

        /// <summary>
        ///MZK Not Diğer entity konfigürasyonları daha sonra eklenecek.
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            #region YKB

            modelBuilder.Entity<YkbServicesRequestProduct>()
                        .Property(x => x.CapturedUnitPrice)
                        .HasPrecision(18, 2);

            modelBuilder.Entity<YkbServicesRequestProduct>()
                        .Property(x => x.CapturedTotal)
                        .HasPrecision(18, 2);

            modelBuilder.Entity<YkbTechnicalService>()
                        .Property(x => x.StartTime)
                        .HasConversion(
                            v => v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value.DateTime, DateTimeKind.Utc) : v
                        );

            // Gerek görürsen YKB için özel index’ler:
            modelBuilder.Entity<YkbWorkFlow>()
                        .HasIndex(x => x.RequestNo);

            modelBuilder.Entity<YkbServicesRequest>()
                        .HasIndex(x => x.RequestNo);

            modelBuilder.Entity<YkbCustomerForm>()
                        .HasIndex(x => x.RequestNo);

            modelBuilder.Entity<YkbServicesRequestWorkOrderType>()
                .HasKey(x => new { x.YkbServicesRequestId, x.WorkOrderTypeId });

            modelBuilder.Entity<YkbServicesRequestWorkOrderType>()
                .HasOne(x => x.YkbServicesRequest)
                .WithMany(x => x.YkbServicesRequestWorkOrderTypes)
                .HasForeignKey(x => x.YkbServicesRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<YkbServicesRequestWorkOrderType>()
                .HasOne(x => x.WorkOrderType)
                .WithMany(x => x.YkbServicesRequestWorkOrderTypes)
                .HasForeignKey(x => x.WorkOrderTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<YkbWorkflowAttachment>()
                .HasIndex(x => x.RequestNo);
            #endregion


            modelBuilder.Entity<WorkFlowSlaSetting>(entity =>
            {
                entity.ToTable("WorkFlowSlaSettings");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.CustomerType)
                    .IsRequired()
                    .HasComment("Müşteri/İş birimi tipi (General, Ykb, Individual, Corporate)");

                entity.Property(x => x.Priority)
                    .IsRequired()
                    .HasComment("İş akışı öncelik seviyesi");

                entity.Property(x => x.SlaDurationHours)
                    .IsRequired()
                    .HasComment("SLA süresi (saat)");

                entity.Property(x => x.NotificationBeforeHours)
                    .IsRequired()
                    .HasComment("Bildirim gönderilecek süre (saat önce)");

                entity.Property(x => x.NotificationEmails)
                    .HasMaxLength(1000)
                    .HasComment("Bildirim gönderilecek e-posta adresleri (virgülle ayrılmış)");


                entity.Property(x => x.IsActive)
                    .IsRequired()
                    .HasDefaultValue(true)
                    .HasComment("Aktif mi");

                entity.Property(x => x.Description)
                    .HasMaxLength(500)
                    .HasComment("Açıklama");

                // Composite unique index: CustomerType + Priority kombinasyonu benzersiz olmalı
                entity.HasIndex(x => new { x.CustomerType, x.Priority })
                    .IsUnique()
                    .HasDatabaseName("IX_WorkFlowSlaSettings_CustomerType_Priority");

                entity.HasIndex(x => x.IsActive)
                    .HasDatabaseName("IX_WorkFlowSlaSettings_IsActive");
            }); 

            /// ProgressApprover Entity Configuration
            modelBuilder.Entity<ProgressApprover>(b =>
            {
                b.HasOne(x => x.CustomerGroup)
                .WithMany(c => c.ProgressApprovers)
                .HasForeignKey(x => x.CustomerGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            });

            /// User Entity Configuration
            modelBuilder.Entity<User>()
                .HasIndex(x => x.TechnicianEmail)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(x => x.TechnicianCode)
                .IsUnique();
            modelBuilder.Entity<UserRole>(e =>
            {
                e.ToTable("UserRole");

                // Aynı (UserId, RoleId) çifti bir kez bulunabilsin
                e.HasIndex(x => new { x.UserId, x.RoleId })
                 .IsUnique();

                // İlişkiler
                e.HasOne(x => x.User)
                 .WithMany(u => u.UserRoles)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Role)
                 .WithMany(r => r.UserRoles)
                 .HasForeignKey(x => x.RoleId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            /// ProductType Entity Configuration
            modelBuilder.Entity<ProductType>(e =>
            {
                e.ToTable("ProductType");
                e.Property(x => x.Type).HasMaxLength(100).IsRequired();
                e.Property(x => x.Code).HasMaxLength(50);
                e.HasIndex(x => x.Code).IsUnique(false);
            });

            /// CurrencyType Entity Configuration
            modelBuilder.Entity<CurrencyType>(e =>
            {
                e.ToTable("CurrencyType");
                e.Property(x => x.Code).HasMaxLength(10).IsRequired();
                e.Property(x => x.Name).HasMaxLength(100);
                e.HasIndex(x => x.Code).IsUnique(); // USD/EUR gibi benzersiz
            });

            /// Brand Entity Configuration
            modelBuilder.Entity<Brand>(e =>
            {
                e.ToTable("Brand");
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Desc).HasMaxLength(500);
                e.HasIndex(x => x.Name).IsUnique(false);
            });

            /// Model Entity Configuration
            modelBuilder.Entity<Model.Concrete.Model>(e =>
            {
                e.ToTable("Model");
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Desc).HasMaxLength(500);
                e.HasOne(x => x.Brand)
                 .WithMany(b => b.Models)
                 .HasForeignKey(x => x.BrandId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.BrandId, x.Name }).IsUnique(); // aynı markada aynı model tek
            });

            /// Product Entity Configuration
            modelBuilder.Entity<Product>(e =>
            {
                e.ToTable("Product");
                e.Property(x => x.ProductCode).HasMaxLength(100);
                e.Property(x => x.OracleProductCode).HasMaxLength(100);
                e.Property(x => x.SystemType).HasMaxLength(100);
                e.Property(x => x.Description).HasMaxLength(1000);
                e.Property(x => x.PriceCurrency).HasMaxLength(10);
                e.Property(x => x.Price).HasPrecision(18, 2);
                e.Property(x => x.CorporateCustomerShortCode).HasMaxLength(50);
                e.Property(x => x.OracleCustomerCode).HasMaxLength(100);
                e.Property(x => x.ServiceFeePercentage).HasPrecision(18, 2);


                // Brand ilişkisi
                e.HasOne(x => x.Brand)
                 .WithMany(b => b.Products)
                 .HasForeignKey(x => x.BrandId)
                 .OnDelete(DeleteBehavior.SetNull);

                // Model ilişkisi
                e.HasOne(x => x.Model)
                 .WithMany(m => m.Products)
                 .HasForeignKey(x => x.ModelId)
                 .OnDelete(DeleteBehavior.SetNull);

                // CurrencyType ilişkisi
                e.HasOne(x => x.CurrencyType)
                 .WithMany(c => c.Products)
                 .HasForeignKey(x => x.CurrencyTypeId)
                 .OnDelete(DeleteBehavior.SetNull);

                // ProductType ilişkisi
                e.HasOne(x => x.ProductType)
                 .WithMany(pt => pt.Products)
                 .HasForeignKey(x => x.ProductTypeId)
                 .OnDelete(DeleteBehavior.SetNull);

                // 🆕 TenantProductPrices ilişkisi
                e.HasMany(x => x.TenantProductPrices)
                 .WithOne(tp => tp.Product)
                 .HasForeignKey(tp => tp.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Indexler
                e.HasIndex(x => x.ProductCode).IsUnique(false);
                e.HasIndex(x => x.OracleProductCode).IsUnique(false);
                e.HasIndex(x => new { x.BrandId, x.ModelId });
                e.HasIndex(x => new { x.ProductTypeId, x.CurrencyTypeId });
            });

            /// SystemType Entity Configuration
            modelBuilder.Entity<SystemType>(e =>
            {
                e.ToTable("SystemType");
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.Code).HasMaxLength(50);
                e.HasIndex(x => x.Code).IsUnique(false);
            });

            /// ServiceType Entity Configuration
            modelBuilder.Entity<ServiceType>(e =>
            {
                e.ToTable("ServiceType");
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.ContractNumber).HasMaxLength(50);
            });


            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Data.Seeding.Infrastructure.SeedHistory>()
                        .HasIndex(x => x.Key)
                        .IsUnique();

            ///WorkFlow Entity Configuration 
            modelBuilder.Entity<WorkFlow>(b =>
           {
               b.Property(x => x.RequestNo).IsRequired().HasMaxLength(100);
               b.HasIndex(x => x.RequestNo).IsUnique();
           });


            // CustomerProductPrice: Customer + Product tekil olsun
            modelBuilder.Entity<CustomerProductPrice>()
                .HasIndex(x => new { x.CustomerId, x.ProductId })
                .IsUnique();

            modelBuilder.Entity<CustomerProductPrice>()
                .HasOne(x => x.Customer)
                .WithMany(c => c.CustomerProductPrices)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerProductPrice>()
                .HasOne(x => x.Product)
                .WithMany(p => p.CustomerProductPrices)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // CustomerGroupProductPrice: Group + Product tekil olsun
            modelBuilder.Entity<CustomerGroupProductPrice>()
                .HasIndex(x => new { x.CustomerGroupId, x.ProductId })
                .IsUnique();

            modelBuilder.Entity<CustomerGroupProductPrice>()
                .HasOne(x => x.CustomerGroup)
                .WithMany(g => g.GroupProductPrices)
                .HasForeignKey(x => x.CustomerGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerGroupProductPrice>()
                .HasOne(x => x.Product)
                .WithMany(p => p.GroupProductPrices)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);



            // TenantProductPrices: Tenant + Product tekil olsun
            modelBuilder.Entity<TenantProductPrice>(e =>
            {
                e.ToTable("TenantProductPrice"); // 🆕 Tablo adı açık

                // 🔹 Composite Unique Index
                e.HasIndex(x => new { x.TenantId, x.ProductId })
                    .IsUnique();

                // 🔹 Price precision (diğer fiyat tablolarıyla tutarlı)
                e.Property(x => x.Price)
                    .HasPrecision(18, 2)
                    .IsRequired();

                // 🔹 CurrencyCode
                e.Property(x => x.CurrencyCode)
                    .HasMaxLength(10);

                // 🔹 Name
                e.Property(x => x.Name)
                    .HasMaxLength(200);

                // 🔹 Tenant İlişkisi
                e.HasOne(x => x.Tenant)
                    .WithMany(t => t.TenantProductPrices)
                    .HasForeignKey(x => x.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                // 🔹 Product İlişkisi
                e.HasOne(x => x.Product)
                    .WithMany(p => p.TenantProductPrices)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WorkFlowTransition>()
                    .HasOne(t => t.FromStep)
                    .WithMany(s => s.OutgoingTransitions)
                    .HasForeignKey(t => t.FromStepId)
                    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkFlowTransition>()
                .HasOne(t => t.ToStep)
                .WithMany(s => s.IncomingTransitions)
                .HasForeignKey(t => t.ToStepId)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<WorkFlowTransition>(entity =>
            {
                // FromStep (Başlangıç Adımı) İlişkisi:
                // WorkFlowStep'teki OutgoingTransitions koleksiyonuna bağlanır.
                entity.HasOne(t => t.FromStep)
                      .WithMany(s => s.OutgoingTransitions)
                      .HasForeignKey(t => t.FromStepId)
                      .OnDelete(DeleteBehavior.Restrict); // Silme davranışını ayarlayın

                // ToStep (Hedef Adım) İlişkisi:
                // WorkFlowStep'teki IncomingTransitions koleksiyonuna bağlanır.
                entity.HasOne(t => t.ToStep)
                      .WithMany(s => s.IncomingTransitions)
                      .HasForeignKey(t => t.ToStepId)
                      .OnDelete(DeleteBehavior.Restrict); // Silme davranışını ayarlayın
            });

            modelBuilder.Entity<WorkFlowReviewLog>(b =>
            {
                b.ToTable("WorkFlowReviewLogs");

                b.HasKey(x => x.Id);

                // Zorunlu alanlar & uzunluklar
                b.Property(x => x.RequestNo)
                    .HasMaxLength(64)
                    .IsRequired();

                b.Property(x => x.FromStepCode)
                    .HasMaxLength(16)
                    .IsRequired();

                b.Property(x => x.ToStepCode)
                    .HasMaxLength(16)
                    .IsRequired();

                b.Property(x => x.ReviewNotes)
                    .HasMaxLength(2000)
                    .IsRequired();

                b.Property(x => x.CreatedUser)
                    .IsRequired();

                b.Property(x => x.CreatedDate)
                    .IsRequired();
                // İstersen provider'a göre default value:
                // SQL Server: .HasDefaultValueSql("GETUTCDATE()")
                // PostgreSQL: .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'")

                // Nullable FK-id alanları (isteğe bağlı; navigation yoksa sadece id tutacağız)
                b.Property(x => x.FromStepId);
                b.Property(x => x.ToStepId);

                // Indexler
                b.HasIndex(x => x.RequestNo);
                b.HasIndex(x => new { x.WorkFlowId, x.CreatedDate });
            });



            // ---------------- Pricing ----------------
            modelBuilder.Entity<Pricing>(e =>
            {
                e.ToTable("Pricing");

                e.Property(x => x.RequestNo)
                    .HasMaxLength(100)
                    .IsRequired();

                // Attribute ile de var ama burada da garanti altına alıyoruz
                e.HasIndex(x => x.RequestNo)
                    .IsUnique();

                e.Property(x => x.Currency)
                    .HasMaxLength(3)
                    .IsRequired();

                e.Property(x => x.Notes)
                    .HasMaxLength(1000);

                e.Property(x => x.TotalAmount)
                    .HasPrecision(18, 2);
            });


            // Menu
            modelBuilder.Entity<Menu>(e =>
            {
                e.ToTable("Menus");
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.Description).HasMaxLength(1000);
                e.HasIndex(x => x.Name).IsUnique(false);
            });


            modelBuilder.Entity<MenuRole>(b =>
            {
                b.Property(x => x.MenuId).HasColumnName("ModulId");
                b.HasOne(x => x.Menu)
                 .WithMany(m => m.MenuRoles)
                 .HasForeignKey(x => x.MenuId)           // MenuId <-> ModulId kolonu
                 .OnDelete(DeleteBehavior.Cascade)
                 .HasConstraintName("FK_MenuRole_Menus_ModulId");
            });


            modelBuilder.Entity<CustomerSystemAssignment>(entity =>
            {
                entity.HasOne(x => x.Customer)
                      .WithMany(c => c.CustomerSystemAssignments)
                      .HasForeignKey(x => x.CustomerId);

                entity.HasOne(x => x.CustomerSystem)
                      .WithMany(cs => cs.CustomerSystemAssignments)
                      .HasForeignKey(x => x.CustomerSystemId);
            });


            modelBuilder.Entity<WorkFlowArchive>(e =>
            {
                e.ToTable("WorkFlowArchives", "dbo"); // şema istersen değiştir
                e.HasKey(x => x.Id);

                e.Property(x => x.RequestNo)
                    .IsRequired()
                    .HasMaxLength(50);

                e.Property(x => x.ArchiveReason)
                    .IsRequired()
                    .HasMaxLength(50);

                // JSON kolonlarını NVARCHAR(MAX) / TEXT vs.
                e.Property(x => x.ServicesRequestJson).IsRequired();
                e.Property(x => x.ServicesRequestProductsJson).IsRequired();
                e.Property(x => x.CustomerJson).IsRequired();
                e.Property(x => x.ApproverTechnicianJson).IsRequired();
                e.Property(x => x.CustomerApproverJson).IsRequired();
                e.Property(x => x.WorkFlowJson).IsRequired();
                e.Property(x => x.WorkFlowReviewLogsJson).IsRequired();
                e.Property(x => x.TechnicalServiceJson).IsRequired();
                e.Property(x => x.TechnicalServiceImagesJson).IsRequired();
                e.Property(x => x.TechnicalServiceFormImagesJson).IsRequired();
                e.Property(x => x.WarehouseJson).IsRequired();
                e.Property(x => x.PricingJson).IsRequired();
                e.Property(x => x.FinalApprovalJson).IsRequired();
            });



            modelBuilder.Entity<WorkFlowActivityRecord>(entity =>
            {
                entity.HasOne(w => w.Customer)
                      .WithMany(c => c.WorkFlowActivityRecords)
                      .HasForeignKey(w => w.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull);
            });


            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasIndex(x => x.Code).IsUnique();

                entity.Property(x => x.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(x => x.Code)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(x => x.LogoUrl)
                      .HasMaxLength(260);

                entity.HasMany(t => t.Customers)
                      .WithOne(c => c.Tenant!)
                      .HasForeignKey(c => c.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(t => t.Users)
                      .WithOne(u => u.Tenant!)
                      .HasForeignKey(u => u.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                // 🆕 TenantProductPrices ilişkisi
                entity.HasMany(t => t.TenantProductPrices)
                      .WithOne(tp => tp.Tenant)
                      .HasForeignKey(tp => tp.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------------- UserFeedback ----------------
            modelBuilder.Entity<UserFeedback>(entity =>
            {
                entity.ToTable("UserFeedbacks");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Title)
                      .IsRequired()
                      .HasMaxLength(250);

                entity.Property(x => x.Description)
                      .IsRequired()
                      .HasMaxLength(5000);

                entity.Property(x => x.AdminResponse)
                      .HasMaxLength(2000);

                entity.Property(x => x.RelatedUrl)
                      .HasMaxLength(500);

                entity.Property(x => x.UserAgent)
                      .HasMaxLength(500);

                entity.Property(x => x.AttachmentUrls)
                      .HasMaxLength(2000);

                // İndeksler - hızlı arama için
                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => x.FeedbackType);
                entity.HasIndex(x => x.CreatedUser);
                entity.HasIndex(x => x.CreatedDate);
                entity.HasIndex(x => new { x.Status, x.FeedbackType });
            });

            modelBuilder.Entity<ServicesRequestWorkOrderType>(entity =>
            {
                entity.ToTable("ServicesRequestWorkOrderTypes");

                entity.HasKey(x => new
                {
                    x.ServicesRequestId,
                    x.WorkOrderTypeId
                });

                entity.HasOne(x => x.ServicesRequest)
                    .WithMany(x => x.ServicesRequestWorkOrderTypes)
                    .HasForeignKey(x => x.ServicesRequestId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.WorkOrderType)
                    .WithMany(x => x.ServicesRequestWorkOrderTypes)
                    .HasForeignKey(x => x.WorkOrderTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // DbSet ekleyin:
            #region QNB

            modelBuilder.Entity<QnbServicesRequestProduct>()
                        .Property(x => x.CapturedUnitPrice)
                        .HasPrecision(18, 2);

            modelBuilder.Entity<QnbServicesRequestProduct>()
                        .Property(x => x.CapturedTotal)
                        .HasPrecision(18, 2);

            modelBuilder.Entity<QnbTechnicalService>()
                        .Property(x => x.StartTime)
                        .HasConversion(
                            v => v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value.DateTime, DateTimeKind.Utc) : v
                        );

            modelBuilder.Entity<QnbWorkFlow>()
                        .HasIndex(x => x.RequestNo);

            modelBuilder.Entity<QnbServicesRequest>()
                        .HasIndex(x => x.RequestNo);

            modelBuilder.Entity<QnbCustomerForm>()
                        .HasIndex(x => x.RequestNo);

            modelBuilder.Entity<QnbServicesRequestWorkOrderType>(entity =>
            {
                entity.ToTable("QnbServicesRequestWorkOrderTypes");

                entity.HasKey(x => new
                {
                    x.QnbServicesRequestId,
                    x.WorkOrderTypeId
                });

                entity.HasOne(x => x.QnbServicesRequest)
                    .WithMany(x => x.QnbServicesRequestWorkOrderTypes)
                    .HasForeignKey(x => x.QnbServicesRequestId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.WorkOrderType)
                    .WithMany(x => x.QnbServicesRequestWorkOrderTypes)
                    .HasForeignKey(x => x.WorkOrderTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            #endregion
        }
    }
}
