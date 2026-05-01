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
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            // DELETE
            if (DeleteId.HasValue)
            {
                int watchId = 0, qty = 0;
                string type = "";

                var get = new SqlCommand("SELECT * FROM Client_Transaction WHERE TransactionId=@id", conn);
                get.Parameters.AddWithValue("@id", DeleteId.Value);

                using (var r = get.ExecuteReader())
                {
                    if (r.Read())
                    {
                        watchId = SafeInt(r["WatchId"]);
                        qty = SafeInt(r["Quantity"]);
                        type = SafeString(r["TransactionType"]);
                    }
                } // ✅ reader auto closed

                // restore stock
                if (type == "Sell") UpdateStock(conn, watchId, qty);
                else if (type == "Buy") UpdateStock(conn, watchId, -qty);

                new SqlCommand("DELETE FROM Client_Transaction WHERE TransactionId=@id", conn)
                {
                    Parameters = { new SqlParameter("@id", DeleteId.Value) }
                }.ExecuteNonQuery();

                Response.Redirect("/Transaction");
                return;
            }

            // EDIT LOAD
            if (EditId.HasValue)
            {
                var cmd = new SqlCommand("SELECT * FROM Client_Transaction WHERE TransactionId=@id", conn);
                cmd.Parameters.AddWithValue("@id", EditId.Value);

                using (var r = cmd.ExecuteReader())
                {
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
            }

            Load(conn);
        }

        public IActionResult OnPost()
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            // 🚫 STRICT VALIDATION
            if (Current.Quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than 0!";
                return RedirectToPage("/Transaction");
            }

            // 🔥 GET REAL PRICE
            var priceCmd = new SqlCommand("SELECT price FROM Watch WHERE watch_id=@id", conn);
            priceCmd.Parameters.AddWithValue("@id", Current.WatchId);
            decimal realPrice = SafeDecimal(priceCmd.ExecuteScalar());

            // 🔁 RESTORE OLD STOCK (EDIT)
            if (IsEdit)
            {
                var old = new SqlCommand("SELECT * FROM Client_Transaction WHERE TransactionId=@id", conn);
                old.Parameters.AddWithValue("@id", Current.TransactionId);

                using (var r = old.ExecuteReader())
                {
                    if (r.Read())
                    {
                        int oldWatch = SafeInt(r["WatchId"]);
                        int oldQty = SafeInt(r["Quantity"]);
                        string oldType = SafeString(r["TransactionType"]);

                        if (oldType == "Sell") UpdateStock(conn, oldWatch, oldQty);
                        else if (oldType == "Buy") UpdateStock(conn, oldWatch, -oldQty);
                    }
                }
            }

            // 📦 STOCK LOGIC
            if (Current.TransactionType == "Sell")
            {
                var check = new SqlCommand("SELECT stock FROM Watch WHERE watch_id=@id", conn);
                check.Parameters.AddWithValue("@id", Current.WatchId);

                int stock = SafeInt(check.ExecuteScalar());

                if (stock <= 0)
                {
                    TempData["Error"] = "Out of stock!";
                    return RedirectToPage("/Transaction");
                }

                if (stock < Current.Quantity)
                {
                    TempData["Error"] = $"Only {stock} available!";
                    return RedirectToPage("/Transaction");
                }

                // SELL → reduce stock
                UpdateStock(conn, Current.WatchId, -Current.Quantity);
            }
            else
            {
                // BUY → increase stock
                UpdateStock(conn, Current.WatchId, Current.Quantity);
            }

            // 💾 SAVE
            if (IsEdit)
            {
                var cmd = new SqlCommand(@"
                    UPDATE Client_Transaction
                    SET ClientId=@c, WatchId=@w, TransactionType=@t, Quantity=@q, Price=@p
                    WHERE TransactionId=@id", conn);

                cmd.Parameters.AddWithValue("@id", Current.TransactionId);
                cmd.Parameters.AddWithValue("@c", Current.ClientId);
                cmd.Parameters.AddWithValue("@w", Current.WatchId);
                cmd.Parameters.AddWithValue("@t", Current.TransactionType);
                cmd.Parameters.AddWithValue("@q", Current.Quantity);
                cmd.Parameters.AddWithValue("@p", realPrice);

                cmd.ExecuteNonQuery();
            }
            else
            {
                var cmd = new SqlCommand(@"
                    INSERT INTO Client_Transaction
                    (ClientId, WatchId, TransactionType, Quantity, Price, TransactionDate)
                    VALUES (@c,@w,@t,@q,@p,GETDATE())", conn);

                cmd.Parameters.AddWithValue("@c", Current.ClientId);
                cmd.Parameters.AddWithValue("@w", Current.WatchId);
                cmd.Parameters.AddWithValue("@t", Current.TransactionType);
                cmd.Parameters.AddWithValue("@q", Current.Quantity);
                cmd.Parameters.AddWithValue("@p", realPrice);

                cmd.ExecuteNonQuery();
            }

            return RedirectToPage("/Transaction");
        }

        private void Load(SqlConnection conn)
        {
            using (var c = new SqlCommand("SELECT * FROM Clients", conn).ExecuteReader())
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

            using (var w = new SqlCommand("SELECT * FROM Watch", conn).ExecuteReader())
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

            using (var t = new SqlCommand(@"
                SELECT t.TransactionId,
                c.FirstName + ' ' + c.LastName AS ClientName,
                w.watch_modelname AS WatchName,
                t.*
                FROM Client_Transaction t
                JOIN Clients c ON t.ClientId = c.ClientId
                JOIN Watch w ON t.WatchId = w.watch_id
                ORDER BY t.TransactionDate DESC", conn).ExecuteReader())
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
                        TransactionDate = t["TransactionDate"] == DBNull.Value
                            ? DateTime.Now
                            : Convert.ToDateTime(t["TransactionDate"])
                    });
                }
            }
        }

        private void UpdateStock(SqlConnection conn, int watchId, int qty)
        {
            var cmd = new SqlCommand(
                "UPDATE Watch SET stock = stock + @q WHERE watch_id=@id", conn);

            cmd.Parameters.AddWithValue("@q", qty);
            cmd.Parameters.AddWithValue("@id", watchId);
            cmd.ExecuteNonQuery();
        }

        private int SafeInt(object v) => v == DBNull.Value ? 0 : Convert.ToInt32(v);
        private decimal SafeDecimal(object v) => v == DBNull.Value ? 0 : Convert.ToDecimal(v);
        private string SafeString(object v) => v == DBNull.Value ? "" : v.ToString();
    }
}