using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate.Cfg;
using NHibernate.Tool.hbm2ddl;

namespace NHibernate.Caches.Redis.Tests
{
    public abstract class IntegrationTestBase : RedisTest
    {
        private const string databaseName = "NHibernateCachesRedisTests";
        private const string masterConnectionString = @"Server=(local)\SQLExpress;Database=master;Trusted_Connection=True;";
        private const string connectionString = @"Server=(local)\SQLExpress;Database=" + databaseName + @";Trusted_Connection=True;";
        private string dataFilePath;
        private string logFilePath;

        private static Configuration configuration;

        public IntegrationTestBase()
        {
            RedisCacheProvider.InternalSetConnectionMultiplexer(ConnectionMultiplexer);

            InitializeDatabasePaths();

            using (var connection = new SqlConnection(masterConnectionString))
            {
                connection.Open();
                DeleteDatabaseIfExists(connection);
                CreateDatabase(connection);
            }

            if (configuration == null)
            {
                configuration = Fluently.Configure()
                    .Database(
                        MsSqlConfiguration.MsSql2008.ConnectionString(connectionString)
                    )
                    .Mappings(m => m.FluentMappings.Add(typeof(PersonMapping)))
                    .ExposeConfiguration(cfg => cfg.SetProperty(NHibernate.Cfg.Environment.GenerateStatistics, "true"))
                    .Cache(c => c.UseQueryCache().UseSecondLevelCache().ProviderClass<RedisCacheProvider>())
                    .BuildConfiguration();
            }

            new SchemaExport(configuration).Create(false, true);
        }

        private void InitializeDatabasePaths()
        {
            var currentPath = Assembly.GetExecutingAssembly().GetName().CodeBase.Replace("file:///", "");
            currentPath = Path.GetDirectoryName(currentPath);

            dataFilePath = Path.Combine(currentPath, databaseName + ".mdf");
            logFilePath = Path.Combine(currentPath, databaseName + "_log.ldf");
        }

        private void DeleteDatabaseIfExists(SqlConnection connection)
        {
            var drop = @"if exists(select name FROM sys.databases where name = '{0}')
                         begin
                             alter database [{0}] set SINGLE_USER with ROLLBACK IMMEDIATE;
                             drop database [{0}];
                         end";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = String.Format(drop, databaseName);
                cmd.ExecuteNonQuery();
            }

            if (File.Exists(dataFilePath)) File.Delete(dataFilePath);
            if (File.Exists(logFilePath)) File.Delete(logFilePath);
        }

        private void CreateDatabase(SqlConnection connection)
        {
            // Minimum DB size is 2MB on <= SQL 2008 R2 and 3MB >= SQL 2012.
            var create = @"create database [{0}] on PRIMARY";
            create += @" ( name = N'{0}', filename = N'{1}', size = 3072KB, maxsize = unlimited, filegrowth = 10% ) ";
            create += @" log on ";
            create += @" ( name = N'{0}_log', filename = N'{2}', size = 1024KB, maxsize = 2048GB, filegrowth = 10% ) ";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = String.Format(create,
                    databaseName, dataFilePath, logFilePath);
                cmd.ExecuteNonQuery();
            }
        }

        protected ISessionFactory CreateSessionFactory()
        {
            return configuration.BuildSessionFactory();
        }

        protected void UsingSession(ISessionFactory sessionFactory, Action<ISession> action)
        {
            using (var session = sessionFactory.OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                action(session);
                transaction.Commit();
            }
        }
    }
}
