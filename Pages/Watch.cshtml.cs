using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using Watch_Reselling_System___Franco.Models;

namespace Watch_Reselling_System___Franco.Pages
{
    public class WatchModel : PageModel
    {
        private readonly IConfiguration _config;

        public WatchModel(IConfiguration config)
        {
            _config = config;
        }

        public List<Watch> WatchList { get; set; } = new();

        [BindProperty]
        public Watch Current { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? EditId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DeleteId { get; set; }

        // SEARCH
        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; }

        public bool IsEdit => EditId.HasValue;

        // ========================= GET =========================
        public void OnGet()
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            //  DELETE
            if (DeleteId.HasValue)
            {
                try
                {
                    var cmd = new SqlCommand("DELETE FROM Watch WHERE watch_id=@id", conn);
                    cmd.Parameters.AddWithValue("@id", DeleteId.Value);
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    // Check if it's a Foreign Key violation 
                    if (ex.Number == 547)
                    {
                        TempData["Error"] = "Cannot delete this watch because it has transaction records.";
                    }
                    else
                    {
                        TempData["Error"] = "An error occurred while deleting.";
                    }
                }

                Response.Redirect("/Watch"); // Or whatever  page name
                return;
            }

            //  EDIT LOAD
            if (EditId.HasValue)
            {
                var cmd = new SqlCommand("SELECT * FROM Watch WHERE watch_id=@id", conn);
                cmd.Parameters.AddWithValue("@id", EditId.Value);

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    Current = new Watch
                    {
                        watch_id = SafeInt(reader["watch_id"]),
                        watch_modelname = SafeString(reader["watch_modelname"]),
                        condition = SafeString(reader["condition"]),
                        price = SafeDecimal(reader["price"]),
                        stock = SafeInt(reader["stock"])
                    };
                }
            }

            //  LOAD LIST WITH SEARCH
            var listCmd = new SqlCommand(@"
                SELECT * FROM Watch
                WHERE (@search IS NULL OR watch_modelname LIKE '%' + @search + '%')
                ORDER BY watch_id DESC", conn);

            listCmd.Parameters.AddWithValue("@search",
                string.IsNullOrEmpty(SearchTerm) ? (object)DBNull.Value : SearchTerm);

            using var r = listCmd.ExecuteReader();

            while (r.Read())
            {
                WatchList.Add(new Watch
                {
                    watch_id = SafeInt(r["watch_id"]),
                    watch_modelname = SafeString(r["watch_modelname"]),
                    condition = SafeString(r["condition"]),
                    price = SafeDecimal(r["price"]),
                    stock = SafeInt(r["stock"])
                });
            }
        }

        // ========================= POST =========================
        public IActionResult OnPost()
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            if (Current.watch_id > 0)
            {
                //  UPDATE
                var cmd = new SqlCommand(@"
                    UPDATE Watch 
                    SET watch_modelname=@m, condition=@c, price=@p, stock=@s
                    WHERE watch_id=@id", conn);

                cmd.Parameters.AddWithValue("@id", Current.watch_id);
                cmd.Parameters.AddWithValue("@m", Current.watch_modelname);
                cmd.Parameters.AddWithValue("@c", Current.condition);
                cmd.Parameters.AddWithValue("@p", Current.price);
                cmd.Parameters.AddWithValue("@s", Current.stock);

                cmd.ExecuteNonQuery();
            }
            else
            {
                // INSERT
                var cmd = new SqlCommand(@"
                    INSERT INTO Watch (watch_modelname, condition, price, stock)
                    VALUES (@m, @c, @p, @s)", conn);

                cmd.Parameters.AddWithValue("@m", Current.watch_modelname);
                cmd.Parameters.AddWithValue("@c", Current.condition);
                cmd.Parameters.AddWithValue("@p", Current.price);
                cmd.Parameters.AddWithValue("@s", Current.stock);

                cmd.ExecuteNonQuery();
            }

            return RedirectToPage("/Watch");
        }

        // ========================= SAFE METHODS =========================
        private int SafeInt(object v) => v == DBNull.Value ? 0 : Convert.ToInt32(v);
        private decimal SafeDecimal(object v) => v == DBNull.Value ? 0 : Convert.ToDecimal(v);
        private string SafeString(object v) => v == DBNull.Value ? "" : v.ToString();
    }
}