using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SqlServerRemoteAccess
{
    /// <summary>
    /// Контекст базы данных Entity Framework Core для работы с сущностью Student
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Набор данных студентов
        /// </summary>
        public DbSet<Student> Students { get; set; } = null!;

        /// <summary>
        /// Конфигурация подключения к базе данных
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Test;Trusted_Connection=True;");
        }
    }

    /// <summary>
    /// Класс, представляющий студента в системе
    /// </summary>
    public class Student
    {
        /// <summary>
        /// Уникальный идентификатор студента
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Полное имя студента
        /// </summary>
        public string FullName { get; set; }
    }

    /// <summary>
    /// TCP-сервер для обработки SQL-запросов к базе данных
    /// </summary>
    class Server
    {
        private const int Port = 5000;

        /// <summary>
        /// Точка входа в приложение сервера
        /// </summary>
        /// <param name="args">Аргументы командной строки</param>
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

        /// <summary>
        /// Обработка подключения клиента
        /// </summary>
        /// <param name="client">TCP-клиент</param>
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

        /// <summary>
        /// Выполнение SQL-запроса к базе данных
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения</param>
        /// <returns>Результат выполнения запроса в текстовом формате</returns>
        /// <remarks>
        /// Поддерживает два типа запросов:
        /// <list type="bullet">
        /// <item><description>SELECT - возвращает данные в формате CSV</description></item>
        /// <item><description>Другие запросы (INSERT, UPDATE, DELETE) - возвращает количество затронутых строк</description></item>
        /// </list>
        /// </remarks>
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