using core_banking_lite.Interfaces;
using core_banking_lite.Repositories;
using Microsoft.Data.Sqlite;

namespace core_banking_lite.Infrastructure
{
    public static class PersistenceServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the SQLite + Dapper persistence provider.
        /// Replace this call with AddEfCorePersistence() to swap the entire
        /// data layer without touching any other registration or business code.
        /// </summary>
        public static IServiceCollection AddSqlitePersistence(
            this IServiceCollection services,
            string connectionString)
        {
            // Keep-alive connection prevents the shared in-memory DB from being destroyed.
            var keepAlive = new SqliteConnection(connectionString);
            keepAlive.Open();
            services.AddSingleton(keepAlive);

            // Register Dapper repository
            services.AddScoped<IBankAccountRepository>(_ =>
                new SqliteBankAccountRepository(connectionString));

            services.AddSingleton<IOutboxRepository>(_ =>
                new SqliteOutboxRepository(connectionString));

            return services;
        }

        // Future:
        // public static IServiceCollection AddEfCorePersistence(
        //     this IServiceCollection services, string connectionString)
        // {
        //     services.AddDbContext<BankingDbContext>(...);
        //     services.AddScoped<IBankAccountRepository, EfBankAccountRepository>();
        //     services.AddSingleton<IOutboxRepository, EfOutboxRepository>();
        //     return services;
        // }
    }
}
