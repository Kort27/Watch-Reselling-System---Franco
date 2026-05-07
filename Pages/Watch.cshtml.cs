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
        public WatchModel(IConfiguration config) { _config = config; }

        public List<Watch> WatchList { get; set; } = new();

        [BindProperty]
        public Watch Current { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? EditId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DeleteId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchCondition { get; set; }

        public bool IsEdit => EditId.HasValue;

        public void OnGet()
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            // 1. DELETE ACTION
            if (DeleteId.HasValue)
            {
                try
                {
                    var cmd = new SqlCommand("DELETE FROM Watch WHERE watch_id=@id", conn);
                    cmd.Parameters.AddWithValue("@id", DeleteId.Value);
                    cmd.ExecuteNonQuery();
                    TempData["Success"] = "Watch deleted successfully.";
                }
                catch (SqlException ex)
                {
                    TempData["Error"] = ex.Number == 547
                        ? "Cannot delete: This watch is linked to existing transactions."
                        : "Database error occurred during deletion.";
                }
                Response.Redirect("/Watch");
                return;
            }

            // 2. LOAD FOR EDIT
            if (EditId.HasValue)
            {
                var cmd = new SqlCommand("SELECT * FROM Watch WHERE watch_id=@id", conn);
                cmd.Parameters.AddWithValue("@id", EditId.Value);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Current = new Watch
                    {
                        watch_id = (int)reader["watch_id"],
                        watch_modelname = reader["watch_modelname"].ToString(),
                        condition = reader["condition"].ToString(),
                        price = Convert.ToDecimal(reader["price"]),
                        stock = (int)reader["stock"]
                    };
                }
                reader.Close();
            }

            // 3. FETCH LIST (Filtered by Model AND Condition)
            var listCmd = new SqlCommand(@"
                SELECT * FROM Watch
                WHERE (@search IS NULL OR watch_modelname LIKE '%' + @search + '%')
                  AND (@cond IS NULL OR condition = @cond)
                ORDER BY watch_id DESC", conn);

            listCmd.Parameters.AddWithValue("@search", string.IsNullOrEmpty(SearchTerm) ? (object)DBNull.Value : SearchTerm);
            listCmd.Parameters.AddWithValue("@cond", string.IsNullOrEmpty(SearchCondition) ? (object)DBNull.Value : SearchCondition);

            using var r = listCmd.ExecuteReader();
            while (r.Read())
            {
                WatchList.Add(new Watch
                {
                    watch_id = (int)r["watch_id"],
                    watch_modelname = r["watch_modelname"].ToString(),
                    condition = r["condition"].ToString(),
                    price = Convert.ToDecimal(r["price"]),
                    stock = (int)r["stock"]
                });
            }
        }

        public IActionResult OnPost()
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            if (Current.watch_id > 0)
            {
                // UPDATE
                var cmd = new SqlCommand(@"UPDATE Watch SET watch_modelname=@m, condition=@c, price=@p, stock=@s WHERE watch_id=@id", conn);
                cmd.Parameters.AddWithValue("@id", Current.watch_id);
                AddParameters(cmd);
                cmd.ExecuteNonQuery();
            }
            else
            {
                // INSERT
                var cmd = new SqlCommand(@"INSERT INTO Watch (watch_modelname, condition, price, stock) VALUES (@m, @c, @p, @s)", conn);
                AddParameters(cmd);
                cmd.ExecuteNonQuery();
            }

            return RedirectToPage("/Watch");
        }

        private void AddParameters(SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@m", Current.watch_modelname);
            cmd.Parameters.AddWithValue("@c", Current.condition ?? "Brand New");
            cmd.Parameters.AddWithValue("@p", Current.price);
            cmd.Parameters.AddWithValue("@s", Current.stock);
        }
    }
}