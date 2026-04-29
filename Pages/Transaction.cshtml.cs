using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using Watch_Reselling_System___Franco.Models;

namespace Watch_Reselling_System___Franco.Pages
{
    public class TransactionModel : PageModel
    {
        private readonly IConfiguration _config;

        public TransactionModel(IConfiguration config)
        {
            _config = config;
        }

        public List<Clients> Clients { get; set; } = new();
        public List<Watch> Watches { get; set; } = new();
        public List<Transaction> TransactionList { get; set; } = new();

        [BindProperty] public Transaction Current { get; set; } = new();

        [BindProperty(SupportsGet = true)] public int? EditId { get; set; }
        [BindProperty(SupportsGet = true)] public int? DeleteId { get; set; }

        public bool IsEdit => EditId.HasValue;

        public void OnGet()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");

            using SqlConnection conn = new(connStr);
            conn.Open();

            // DELETE + STOCK REVERT
            if (DeleteId.HasValue)
            {
                var getCmd = new SqlCommand("SELECT WatchId, TransactionType FROM Client_Transaction WHERE TransactionId=@id", conn);
                getCmd.Parameters.AddWithValue("@id", DeleteId.Value);

                using var reader = getCmd.ExecuteReader();

                int watchId = 0;
                string type = "";

                if (reader.Read())
                {
                    watchId = (int)reader["WatchId"];
                    type = reader["TransactionType"].ToString();
                }
                reader.Close();

                string revert = type == "Buy"
                    ? "UPDATE Watch SET stock = stock - 1 WHERE watch_id=@w"
                    : "UPDATE Watch SET stock = stock + 1 WHERE watch_id=@w";

                var revertCmd = new SqlCommand(revert, conn);
                revertCmd.Parameters.AddWithValue("@w", watchId);
                revertCmd.ExecuteNonQuery();

                var delCmd = new SqlCommand("DELETE FROM Client_Transaction WHERE TransactionId=@id", conn);
                delCmd.Parameters.AddWithValue("@id", DeleteId.Value);
                delCmd.ExecuteNonQuery();

                Response.Redirect("/Transaction");
                return;
            }

            // LOAD EDIT
            if (EditId.HasValue)
            {
                var cmd = new SqlCommand("SELECT * FROM Client_Transaction WHERE TransactionId=@id", conn);
                cmd.Parameters.AddWithValue("@id", EditId.Value);

                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Current = new Transaction
                    {
                        TransactionId = (int)reader["TransactionId"],
                        ClientId = (int)reader["ClientId"],
                        WatchId = (int)reader["WatchId"],
                        TransactionType = reader["TransactionType"].ToString(),
                        PaymentStatus = reader["PaymentStatus"].ToString()
                    };
                }
                reader.Close();
            }

            LoadClients(conn);
            LoadWatches(conn);
            LoadTransactions(conn);
        }

        public IActionResult OnPost()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");

            using SqlConnection conn = new(connStr);
            conn.Open();

            // REVERT OLD STOCK (EDIT)
            if (Current.TransactionId > 0)
            {
                var oldCmd = new SqlCommand("SELECT WatchId, TransactionType FROM Client_Transaction WHERE TransactionId=@id", conn);
                oldCmd.Parameters.AddWithValue("@id", Current.TransactionId);

                using var reader = oldCmd.ExecuteReader();

                int oldWatchId = 0;
                string oldType = "";

                if (reader.Read())
                {
                    oldWatchId = (int)reader["WatchId"];
                    oldType = reader["TransactionType"].ToString();
                }
                reader.Close();

                string revert = oldType == "Buy"
                    ? "UPDATE Watch SET stock = stock - 1 WHERE watch_id=@w"
                    : "UPDATE Watch SET stock = stock + 1 WHERE watch_id=@w";

                var revertCmd = new SqlCommand(revert, conn);
                revertCmd.Parameters.AddWithValue("@w", oldWatchId);
                revertCmd.ExecuteNonQuery();
            }

            // 🚨 PREVENT NEGATIVE STOCK
            if (Current.TransactionType == "Sell")
            {
                var checkCmd = new SqlCommand("SELECT stock FROM Watch WHERE watch_id=@w", conn);
                checkCmd.Parameters.AddWithValue("@w", Current.WatchId);

                int stock = (int)checkCmd.ExecuteScalar();

                if (stock <= 0)
                {
                    ModelState.AddModelError("", "❌ Cannot sell. Watch is OUT OF STOCK.");
                    LoadClients(conn);
                    LoadWatches(conn);
                    LoadTransactions(conn);
                    return Page();
                }
            }

            // INSERT OR UPDATE
            if (Current.TransactionId > 0)
            {
                var cmd = new SqlCommand(@"
                    UPDATE Client_Transaction
                    SET ClientId=@c, WatchId=@w, TransactionType=@t, PaymentStatus=@p
                    WHERE TransactionId=@id", conn);

                cmd.Parameters.AddWithValue("@id", Current.TransactionId);
                cmd.Parameters.AddWithValue("@c", Current.ClientId);
                cmd.Parameters.AddWithValue("@w", Current.WatchId);
                cmd.Parameters.AddWithValue("@t", Current.TransactionType);
                cmd.Parameters.AddWithValue("@p", Current.PaymentStatus);

                cmd.ExecuteNonQuery();
            }
            else
            {
                var cmd = new SqlCommand(@"
                    INSERT INTO Client_Transaction
                    (ClientId, WatchId, TransactionType, TransactionDate, PaymentStatus)
                    VALUES (@c, @w, @t, GETDATE(), @p)", conn);

                cmd.Parameters.AddWithValue("@c", Current.ClientId);
                cmd.Parameters.AddWithValue("@w", Current.WatchId);
                cmd.Parameters.AddWithValue("@t", Current.TransactionType);
                cmd.Parameters.AddWithValue("@p", Current.PaymentStatus);

                cmd.ExecuteNonQuery();
            }

            // APPLY NEW STOCK
            string updateStock = Current.TransactionType == "Buy"
                ? "UPDATE Watch SET stock = stock + 1 WHERE watch_id=@w"
                : "UPDATE Watch SET stock = stock - 1 WHERE watch_id=@w";

            var stockCmd = new SqlCommand(updateStock, conn);
            stockCmd.Parameters.AddWithValue("@w", Current.WatchId);
            stockCmd.ExecuteNonQuery();

            return RedirectToPage("/Transaction");
        }

        private void LoadClients(SqlConnection conn)
        {
            var cmd = new SqlCommand("SELECT * FROM Clients", conn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                Clients.Add(new Clients
                {
                    ClientId = (int)reader["ClientId"],
                    FirstName = reader["FirstName"].ToString(),
                    LastName = reader["LastName"].ToString()
                });
            }
            reader.Close();
        }

        private void LoadWatches(SqlConnection conn)
        {
            var cmd = new SqlCommand("SELECT * FROM Watch", conn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                Watches.Add(new Watch
                {
                    watch_id = (int)reader["watch_id"],
                    watch_modelname = reader["watch_modelname"].ToString(),
                    stock = (int)reader["stock"]
                });
            }
            reader.Close();
        }

        private void LoadTransactions(SqlConnection conn)
        {
            string query = @"
            SELECT t.*, 
                   c.FirstName + ' ' + c.LastName AS ClientName,
                   w.watch_modelname AS WatchName
            FROM Client_Transaction t
            JOIN Clients c ON t.ClientId = c.ClientId
            JOIN Watch w ON t.WatchId = w.watch_id";

            var cmd = new SqlCommand(query, conn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                TransactionList.Add(new Transaction
                {
                    TransactionId = (int)reader["TransactionId"],
                    ClientName = reader["ClientName"].ToString(),
                    WatchName = reader["WatchName"].ToString(),
                    TransactionType = reader["TransactionType"].ToString(),
                    TransactionDate = (DateTime)reader["TransactionDate"],
                    PaymentStatus = reader["PaymentStatus"].ToString()
                });
            }
        }
    }
}