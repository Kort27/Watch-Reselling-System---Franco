using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace Watch_Reselling_System___Franco.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _config;

        public int TotalClients { get; set; }
        public int TotalWatches { get; set; }
        public int TotalTransactions { get; set; }

        // STOCK DATA
        public int TotalStock { get; set; }
        public int LowStockCount { get; set; }

        public IndexModel(IConfiguration config)
        {
            _config = config;
        }

        public void OnGet()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");

            using SqlConnection conn = new(connStr);
            conn.Open();

            
            TotalClients = (int)new SqlCommand("SELECT COUNT(*) FROM Clients", conn).ExecuteScalar();
            TotalWatches = (int)new SqlCommand("SELECT COUNT(*) FROM Watch", conn).ExecuteScalar();
            TotalTransactions = (int)new SqlCommand("SELECT COUNT(*) FROM Client_Transaction", conn).ExecuteScalar();

            
            TotalStock = (int)new SqlCommand("SELECT ISNULL(SUM(stock),0) FROM Watch", conn).ExecuteScalar();

            // Count of watches with low stock 

            LowStockCount = (int)new SqlCommand("SELECT COUNT(*) FROM Watch WHERE stock <= 3", conn).ExecuteScalar();
        }
    }
}
