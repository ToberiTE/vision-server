using Microsoft.EntityFrameworkCore;
using Server.Models;
namespace Server
{
    public class VisionContext : DbContext
    {
        public VisionContext(DbContextOptions<VisionContext> opt) : base(opt)
        {
        }

        public DbSet<Bar_Revenue> Bar_Revenue { get; set; }
        public DbSet<Pie_Production> Pie_Production { get; set; }
        public DbSet<Radar_Production> Radar_Production { get; set; }
        public DbSet<Scatter_Revenue> Scatter_Revenue { get; set; }
        public DbSet<Scatter_Production> Scatter_Production { get; set; }
        public DbSet<Project> Project { get; set; }
        public DbSet<Transaction> Transaction { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            if (!modelBuilder.Model.GetEntityTypes().Any(e => e.Name == typeof(Bar_Revenue).Name))
            {
                var records = new List<Bar_Revenue>();

                var random = new Random();

                for (int i = 1; i <= 250; i++)
                {
                    var date = new DateTime(2020, 1, 1).AddDays(random.Next(365));
                    var netIncome = random.NextDouble();
                    var expenses = random.NextDouble();

                    records.Add(new Bar_Revenue(i, date, (float)netIncome, (float)expenses));
                }

                modelBuilder.Entity<Bar_Revenue>().HasData(records);
            }

            if (!modelBuilder.Model.GetEntityTypes().Any(e => e.Name == typeof(Pie_Production).Name))
            {
                var records = new List<Pie_Production>();

                var random = new Random();

                for (int i = 1; i <= 250; i++)
                {
                    var date = new DateTime(2020, 1, 1).AddDays(random.Next(365));

                    var production = random.Next(1000, 10000);

                    records.Add(new Pie_Production(i, date, production));
                }

                modelBuilder.Entity<Pie_Production>().HasData(records);
            }

            if (!modelBuilder.Model.GetEntityTypes().Any(e => e.Name == typeof(Radar_Production).Name))
            {
                var records = new List<Radar_Production>();

                var random = new Random();
                for (int i = 1; i <= 250; i++)
                {
                    var date = new DateTime(2020, 1, 1).AddDays(random.Next(365));

                    var production = random.Next(1000, 10000);

                    records.Add(new Radar_Production(i, date, production));

                }

                modelBuilder.Entity<Radar_Production>().HasData(records);
            }

            if (!modelBuilder.Model.GetEntityTypes().Any(e => e.Name == typeof(Scatter_Production).Name))
            {
                var records = new List<Scatter_Production>();

                var random = new Random();

                for (int i = 1; i <= 250; i++)
                {
                    var date = new DateTime(2020, 1, 1).AddDays(random.Next(365));
                    var productionGross = random.NextDouble();
                    var fuelConsumption = random.NextDouble();

                    records.Add(new Scatter_Production(i, date, (float)productionGross, (float)fuelConsumption));
                }

                modelBuilder.Entity<Scatter_Production>().HasData(records);
            }

            if (!modelBuilder.Model.GetEntityTypes().Any(e => e.Name == typeof(Scatter_Revenue).Name))
            {
                var records = new List<Scatter_Revenue>();

                var random = new Random();

                for (int i = 1; i <= 250; i++)
                {
                    var date = new DateTime(2020, 1, 1).AddDays(random.Next(365));
                    var expenses = random.NextDouble();
                    var netIncome = random.NextDouble();

                    records.Add(new Scatter_Revenue(i, date, (float)expenses, (float)netIncome));
                }

                modelBuilder.Entity<Scatter_Revenue>().HasData(records);
            }

            if (!modelBuilder.Model.GetEntityTypes().Any(e => e.Name == typeof(Transaction).Name))
            {
                var records = new List<Transaction>();

                var random = new Random();

                for (int i = 1; i <= 250; i++)
                {
                    var date = new DateTime(2020, 1, 1).AddDays(random.Next(365));
                    var revenue = random.Next(100000, 1000000);
                    var netIncome = revenue * (random.NextDouble() * 0.1 + 0.05);
                    var expenses = revenue * (random.NextDouble() * 0.05 + 0.01);

                    records.Add(new Transaction(i, date, revenue, (float)netIncome, (float)expenses));
                }

                modelBuilder.Entity<Transaction>().HasData(records);
            }

            if (!modelBuilder.Model.GetEntityTypes().Any(e => e.Name == typeof(Project).Name))
            {
                var records = new List<Project>();

                var random = new Random();

                for (int i = 1; i <= 50; i++)
                {
                    var customer = new[]
                    {
                        "Test Org.",
                        "ABC Corporation",
                        "123 Company",
                        "XYZ Inc.",
                        "Demo Group"
                    }[random.Next(5)];

                    var description = new[]
                    {
                        "Software development",
                        "Website design",
                        "Product development",
                        "Marketing campaign",
                        "Sales promotion"
                    }[random.Next(5)];

                    var status = new Status[]
                    {
                        Status.Delivered,
                        Status.Completed,
                        Status.Active,
                        Status.Pending,
                        Status.Inactive,
                        Status.Cancelled
                    }[random.Next(6)];

                    var elapsed_time = random.Next(1, 1000);
                    var estimated_time = random.Next(1, 1000);
                    var price = random.Next(1000, 100000) / 100m;

                    records.Add(new Project(i, customer, description, status, elapsed_time, estimated_time, (float)price));
                }

                modelBuilder.Entity<Project>().HasData(records);
            }
        }

        public void EnsureCreated()
        {
            base.Database.EnsureCreated();
        }

    }
}