using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Watch_Reselling_System___Franco.Models;

namespace Watch_Reselling_System___Franco.Pages
{
    public class TransactionModel : PageModel
    {
        // Access configuration settings
        private readonly IConfiguration _config;

        public TransactionModel(IConfiguration config)
        {
            _config = config;
        }

        // Dropdown and table data
        public List<Clients> Clients { get; set; } = new();
        public List<Watch> Watches { get; set; } = new();
        public List<Transaction> TransactionList { get; set; } = new();

        // Current transaction form data
        [BindProperty]
        public Transaction Current { get; set; } = new();

        // Edit transaction ID
        [BindProperty(SupportsGet = true)]
        public int? EditId { get; set; }

        // Delete transaction ID
        [BindProperty(SupportsGet = true)]
        public int? DeleteId { get; set; }

        // Filter by client
        [BindProperty(SupportsGet = true)]
        public int? FilterClientId { get; set; }

        // Search watch model
        [BindProperty(SupportsGet = true)]
        public string? SearchWatch { get; set; }

        // Filter transaction type
        [BindProperty(SupportsGet = true)]
        public string? FilterType { get; set; }

        // Check if editing mode is active
        public bool IsEdit => EditId.HasValue;

        public void OnGet()
        {
            // Delete selected transaction
            if (DeleteId.HasValue)
            {
                DeleteTransaction(DeleteId.Value);
                Response.Redirect("/Transaction");
                return;
            }

            // Load transaction for editing
            if (EditId.HasValue)
            {
                LoadSingleTransaction(EditId.Value);
            }

            // Load page data
            LoadData();
        }

        public IActionResult OnPost()
        {
            // Prevent invalid quantity
            if (Current.Quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than 0.";
                LoadData();
                return Page();
            }

            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            // Default price from form
            decimal finalPrice = Current.Price;

            // Sell transaction logic
            if (Current.TransactionType == "Sell")
            {
                // Get watch stock and price
                var checkCmd = new SqlCommand(
                    "SELECT price, stock FROM Watch WHERE watch_id=@id",
                    conn
                );

                checkCmd.Parameters.AddWithValue("@id", Current.WatchId);

                using (var reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int availableStock = SafeInt(reader["stock"]);

                        // Force selling price from database
                        finalPrice = SafeDecimal(reader["price"]);

                        // Prevent insufficient stock
                        if (availableStock < Current.Quantity)
                        {
                            TempData["Error"] =
                                $"Insufficient stock ({availableStock} available).";

                            reader.Close();
                            LoadData();
                            return Page();
                        }
                    }
                }

                // Reduce stock after selling
                UpdateStock(conn, Current.WatchId, -Current.Quantity);
            }
            else
            {
                // Increase stock for buying
                UpdateStock(conn, Current.WatchId, Current.Quantity);
            }

            // Insert or update query
            string sql = IsEdit
                ? @"UPDATE Client_Transaction
                   SET ClientId=@c,
                       WatchId=@w,
                       TransactionType=@t,
                       Quantity=@q,
                       Price=@p
                   WHERE TransactionId=@id"
                : @"INSERT INTO Client_Transaction
                   (ClientId, WatchId, TransactionType, Quantity, Price, TransactionDate)
                   VALUES (@c,@w,@t,@q,@p,GETDATE())";

            using var cmd = new SqlCommand(sql, conn);

            // Add transaction ID when editing
            if (IsEdit)
                cmd.Parameters.AddWithValue("@id", Current.TransactionId);

            // Add parameters
            cmd.Parameters.AddWithValue("@c", Current.ClientId);
            cmd.Parameters.AddWithValue("@w", Current.WatchId);
            cmd.Parameters.AddWithValue("@t", Current.TransactionType);
            cmd.Parameters.AddWithValue("@q", Current.Quantity);
            cmd.Parameters.AddWithValue("@p", finalPrice);

            // Execute query
            cmd.ExecuteNonQuery();

            TempData["Success"] = "Transaction saved.";

            return RedirectToPage("/Transaction");
        }

        private void LoadData()
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            // Load clients dropdown
            Clients.Clear();

            using (var c = new SqlCommand(
                "SELECT ClientId, FirstName, LastName FROM Clients",
                conn
            ).ExecuteReader())
            {
                while (c.Read())
                {
                    Clients.Add(new Clients
                    {
                        ClientId = SafeInt(c["ClientId"]),
                        FirstName = SafeString(c["FirstName"]),
                        LastName = SafeString(c["LastName"])
                    });
                }
            }

            // Load watches dropdown
            Watches.Clear();

            using (var w = new SqlCommand(
                "SELECT watch_id, watch_modelname, stock, price FROM Watch",
                conn
            ).ExecuteReader())
            {
                while (w.Read())
                {
                    Watches.Add(new Watch
                    {
                        watch_id = SafeInt(w["watch_id"]),
                        watch_modelname = SafeString(w["watch_modelname"]),
                        stock = SafeInt(w["stock"]),
                        price = SafeDecimal(w["price"])
                    });
                }
            }

            // Load transaction table
            TransactionList.Clear();

            string sql = @"
                SELECT 
                    t.*, 
                    c.FirstName + ' ' + c.LastName AS ClientName,
                    w.watch_modelname AS WatchName
                FROM Client_Transaction t
                JOIN Clients c ON t.ClientId = c.ClientId
                JOIN Watch w ON t.WatchId = w.watch_id
                WHERE (@clientId IS NULL OR t.ClientId = @clientId)
                  AND (@watchName IS NULL OR w.watch_modelname LIKE '%' + @watchName + '%')
                  AND (@type IS NULL OR t.TransactionType = @type)
                ORDER BY t.TransactionDate DESC";

            using var cmd = new SqlCommand(sql, conn);

            // Apply filters
            cmd.Parameters.AddWithValue(
                "@clientId",
                (object)FilterClientId ?? DBNull.Value
            );

            cmd.Parameters.AddWithValue(
                "@watchName",
                string.IsNullOrEmpty(SearchWatch)
                    ? DBNull.Value
                    : SearchWatch
            );

            cmd.Parameters.AddWithValue(
                "@type",
                string.IsNullOrEmpty(FilterType)
                    ? DBNull.Value
                    : FilterType
            );

            using var t = cmd.ExecuteReader();

            while (t.Read())
            {
                TransactionList.Add(new Transaction
                {
                    TransactionId = SafeInt(t["TransactionId"]),
                    ClientName = SafeString(t["ClientName"]),
                    WatchName = SafeString(t["WatchName"]),
                    TransactionType = SafeString(t["TransactionType"]),
                    Quantity = SafeInt(t["Quantity"]),
                    Price = SafeDecimal(t["Price"]),
                    TransactionDate = Convert.ToDateTime(t["TransactionDate"])
                });
            }
        }

        // Load selected transaction
        private void LoadSingleTransaction(int id)
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            var cmd = new SqlCommand(
                "SELECT * FROM Client_Transaction WHERE TransactionId=@id",
                conn
            );

            cmd.Parameters.AddWithValue("@id", id);

            using var r = cmd.ExecuteReader();

            if (r.Read())
            {
                Current = new Transaction
                {
                    TransactionId = SafeInt(r["TransactionId"]),
                    ClientId = SafeInt(r["ClientId"]),
                    WatchId = SafeInt(r["WatchId"]),
                    TransactionType = SafeString(r["TransactionType"]),
                    Quantity = SafeInt(r["Quantity"]),
                    Price = SafeDecimal(r["Price"])
                };
            }
        }

        // Delete transaction
        private void DeleteTransaction(int id)
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            // Remove selected transaction
            new SqlCommand(
                $"DELETE FROM Client_Transaction WHERE TransactionId={id}",
                conn
            ).ExecuteNonQuery();
        }

        // Update watch stock
        private void UpdateStock(SqlConnection conn, int watchId, int qty)
        {
            var cmd = new SqlCommand(
                "UPDATE Watch SET stock = stock + @q WHERE watch_id=@id",
                conn
            );

            cmd.Parameters.AddWithValue("@q", qty);
            cmd.Parameters.AddWithValue("@id", watchId);

            cmd.ExecuteNonQuery();
        }

        // Safe integer conversion
        private int SafeInt(object v) =>
            v == DBNull.Value ? 0 : Convert.ToInt32(v);

        // Safe decimal conversion
        private decimal SafeDecimal(object v) =>
            v == DBNull.Value ? 0 : Convert.ToDecimal(v);

        // Safe string conversion
        private string SafeString(object v) =>
            v == DBNull.Value ? "" : v.ToString();
    }
}