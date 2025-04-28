using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SqlServerRemoteAccess
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Student> Students { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Test;Trusted_Connection=True;");
        }
    }

    public class Student
    {
        public int Id { get; set; }
        public string FullName { get; set; }
    }

    class Server
    {
        private const int Port = 5000;

        static void Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Console.WriteLine("Сервер запущен. Ожидание подключения клиентов...");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Клиент подключен.");
                HandleClient(client);
            }
        }

        private static async void HandleClient(TcpClient client)
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string sqlQuery = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Console.WriteLine($"Получен запрос: {sqlQuery}");

                try
                {
                    string result = await ExecuteSqlQueryAsync(sqlQuery);
                    byte[] response = Encoding.UTF8.GetBytes(result);
                    await stream.WriteAsync(response, 0, response.Length);
                }
                catch (Exception ex)
                {
                    byte[] errorResponse = Encoding.UTF8.GetBytes($"Ошибка: {ex.Message}");
                    await stream.WriteAsync(errorResponse, 0, errorResponse.Length);
                }
            }
        }

        private static async Task<string> ExecuteSqlQueryAsync(string query)
        {
            using (var context = new ApplicationDbContext())
            {
                if (query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await context.Students.FromSqlRaw(query).ToListAsync();

                    var headers = string.Join(", ", typeof(Student).GetProperties().Select(p => p.Name));

                    var data = string.Join(Environment.NewLine, result.Select(s => $"{s.Id}, {s.FullName}"));
                    return $"{headers}{Environment.NewLine}{data}";
                }
                else
                {
                    var affectedRows = await context.Database.ExecuteSqlRawAsync(query);
                    return $"Запрос выполнен. Затронуто строк: {affectedRows}";
                }
            }
        }
    }
}