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

        public bool IsEdit => EditId.HasValue;

        public void OnGet()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");

            using SqlConnection conn = new(connStr);
            conn.Open();

            // DELETE
            if (DeleteId.HasValue)
            {
                var cmd = new SqlCommand("DELETE FROM Watch WHERE watch_id=@id", conn);
                cmd.Parameters.AddWithValue("@id", DeleteId.Value);
                cmd.ExecuteNonQuery();

                Response.Redirect("/Watch");
                return;
            }

            // LOAD EDIT DATA
            if (EditId.HasValue)
            {
                var cmd = new SqlCommand("SELECT * FROM Watch WHERE watch_id=@id", conn);
                cmd.Parameters.AddWithValue("@id", EditId.Value);

                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    Current = new Watch
                    {
                        watch_id = (int)reader["watch_id"],
                        watch_modelname = reader["watch_modelname"].ToString(),
                        condition = reader["condition"].ToString(),
                        price = (decimal)reader["price"],
                        stock = (int)reader["stock"] // ✅ IMPORTANT
                    };
                }

                reader.Close();
            }

            // LOAD LIST
            var listCmd = new SqlCommand("SELECT * FROM Watch", conn);
            var r = listCmd.ExecuteReader();

            while (r.Read())
            {
                WatchList.Add(new Watch
                {
                    watch_id = (int)r["watch_id"],
                    watch_modelname = r["watch_modelname"].ToString(),
                    condition = r["condition"].ToString(),
                    price = (decimal)r["price"],
                    stock = (int)r["stock"] // ✅ IMPORTANT
                });
            }
        }

        public IActionResult OnPost()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");

            using SqlConnection conn = new(connStr);
            conn.Open();

            if (Current.watch_id > 0)
            {
                // UPDATE
                var cmd = new SqlCommand(@"
                    UPDATE Watch 
                    SET watch_modelname=@m, condition=@c, price=@p, stock=@s
                    WHERE watch_id=@id", conn);

                cmd.Parameters.AddWithValue("@id", Current.watch_id);
                cmd.Parameters.AddWithValue("@m", Current.watch_modelname);
                cmd.Parameters.AddWithValue("@c", Current.condition);
                cmd.Parameters.AddWithValue("@p", Current.price);
                cmd.Parameters.AddWithValue("@s", Current.stock); // ✅ IMPORTANT

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
                cmd.Parameters.AddWithValue("@s", Current.stock); // ✅ IMPORTANT

                cmd.ExecuteNonQuery();
            }

            return RedirectToPage("/Watch");
        }
    }
}