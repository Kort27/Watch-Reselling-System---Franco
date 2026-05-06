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
        public TransactionModel(IConfiguration config) { _config = config; }

        public List<Clients> Clients { get; set; } = new();
        public List<Watch> Watches { get; set; } = new();
        public List<Transaction> TransactionList { get; set; } = new();

        [BindProperty] public Transaction Current { get; set; } = new();
        [BindProperty(SupportsGet = true)] public int? EditId { get; set; }
        [BindProperty(SupportsGet = true)] public int? DeleteId { get; set; }

        // 🔥 Capture the client selection from the sidebar search seen in image_afa8d2.png
        [BindProperty(SupportsGet = true)] public int? FilterClientId { get; set; }

        public bool IsEdit => EditId.HasValue;

        // ========================= GET =========================
        public void OnGet()
        {
            // Handle Delete separately to avoid connection/reader conflicts seen in image_b0216c.png
            if (DeleteId.HasValue)
            {
                DeleteTransaction(DeleteId.Value);
                Response.Redirect("/Transaction");
                return;
            }

            if (EditId.HasValue)
            {
                LoadSingleTransaction(EditId.Value);
            }

            // Load the lists for dropdowns and table
            LoadData();
        }

        // ========================= POST =========================
        public IActionResult OnPost()
        {
            if (Current.Quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than 0.";
                LoadData();
                return Page();
            }

            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            decimal finalPrice = 0;

            // STOCK CHECK logic to prevent negative stock seen in image_b03b3b.png
            if (Current.TransactionType == "Sell")
            {
                var checkCmd = new SqlCommand("SELECT price, stock FROM Watch WHERE watch_id=@id", conn);
                checkCmd.Parameters.AddWithValue("@id", Current.WatchId);

                using (var reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int availableStock = SafeInt(reader["stock"]);
                        finalPrice = SafeDecimal(reader["price"]);

                        if (availableStock <= 0 || availableStock < Current.Quantity)
                        {
                            TempData["Error"] = $"Transaction Failed: Insufficient stock ({availableStock} available).";
                            reader.Close();
                            LoadData();
                            return Page();
                        }
                    }
                }

                UpdateStock(conn, Current.WatchId, -Current.Quantity);
            }
            else
            {
                finalPrice = Current.Price;
                UpdateStock(conn, Current.WatchId, Current.Quantity);
            }

            string sql = IsEdit
                ? "UPDATE Client_Transaction SET ClientId=@c, WatchId=@w, TransactionType=@t, Quantity=@q, Price=@p WHERE TransactionId=@id"
                : "INSERT INTO Client_Transaction (ClientId, WatchId, TransactionType, Quantity, Price, TransactionDate) VALUES (@c,@w,@t,@q,@p,GETDATE())";

            using var cmd = new SqlCommand(sql, conn);
            if (IsEdit) cmd.Parameters.AddWithValue("@id", Current.TransactionId);
            cmd.Parameters.AddWithValue("@c", Current.ClientId);
            cmd.Parameters.AddWithValue("@w", Current.WatchId);
            cmd.Parameters.AddWithValue("@t", Current.TransactionType);
            cmd.Parameters.AddWithValue("@q", Current.Quantity);
            cmd.Parameters.AddWithValue("@p", finalPrice);
            cmd.ExecuteNonQuery();

            TempData["Success"] = "Transaction saved successfully.";
            return RedirectToPage();
        }

        // ========================= HELPERS =========================

        private void LoadData()
        {
            // Fresh connection per load to fix reader errors in image_b0216c_2.png
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            Clients.Clear();
            using (var c = new SqlCommand("SELECT * FROM Clients", conn).ExecuteReader())
            {
                while (c.Read())
                {
                    Clients.Add(new Clients { ClientId = SafeInt(c["ClientId"]), FirstName = SafeString(c["FirstName"]), LastName = SafeString(c["LastName"]) });
                }
            }

            Watches.Clear();
            using (var w = new SqlCommand("SELECT * FROM Watch", conn).ExecuteReader())
            {
                while (w.Read())
                {
                    Watches.Add(new Watch { watch_id = SafeInt(w["watch_id"]), watch_modelname = SafeString(w["watch_modelname"]), stock = SafeInt(w["stock"]), price = SafeDecimal(w["price"]) });
                }
            }

            TransactionList.Clear();
            // Filter logic using the Sidebar selection from image_afa8d2.png
            string sql = @"
                SELECT t.*, c.FirstName + ' ' + c.LastName AS ClientName, w.watch_modelname AS WatchName
                FROM Client_Transaction t
                JOIN Clients c ON t.ClientId = c.ClientId
                JOIN Watch w ON t.WatchId = w.watch_id
                WHERE (@filterId IS NULL OR t.ClientId = @filterId)
                ORDER BY t.TransactionDate DESC";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@filterId", (object)FilterClientId ?? DBNull.Value);

                using (var t = cmd.ExecuteReader())
                {
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
            }
        }

        private void LoadSingleTransaction(int id)
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();
            var cmd = new SqlCommand("SELECT * FROM Client_Transaction WHERE TransactionId=@id", conn);
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

        private void DeleteTransaction(int id)
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();
            var cmd = new SqlCommand("DELETE FROM Client_Transaction WHERE TransactionId=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private void UpdateStock(SqlConnection conn, int watchId, int qty)
        {
            var cmd = new SqlCommand("UPDATE Watch SET stock = stock + @q WHERE watch_id=@id", conn);
            cmd.Parameters.AddWithValue("@q", qty);
            cmd.Parameters.AddWithValue("@id", watchId);
            cmd.ExecuteNonQuery();
        }

        private int SafeInt(object v) => v == DBNull.Value ? 0 : Convert.ToInt32(v);
        private decimal SafeDecimal(object v) => v == DBNull.Value ? 0 : Convert.ToDecimal(v);
        private string SafeString(object v) => v == DBNull.Value ? "" : v.ToString();
    }
}