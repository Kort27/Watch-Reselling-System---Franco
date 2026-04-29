using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using Watch_Reselling_System___Franco.Models;

namespace Watch_Reselling_System___Franco.Pages
{
    public class ClientsModel : PageModel
    {
        private readonly IConfiguration _config;

        public ClientsModel(IConfiguration config)
        {
            _config = config;
        }

        public List<Clients> ClientList { get; set; } = new List<Clients>();

        [BindProperty] public Clients Input { get; set; } = new Clients();
        [BindProperty(SupportsGet = true)] public int? DeleteId { get; set; }
        [BindProperty(SupportsGet = true)] public int? EditId { get; set; }

        public bool IsEdit => EditId.HasValue;

        public void OnGet()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");

            // DELETE
            if (DeleteId.HasValue)
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();
                var cmd = new SqlCommand("DELETE FROM Clients WHERE ClientId=@id", conn);
                cmd.Parameters.AddWithValue("@id", DeleteId);
                cmd.ExecuteNonQuery();

                Response.Redirect("/Clients");
                return;
            }

            // LOAD EDIT DATA
            if (EditId.HasValue)
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                var cmd = new SqlCommand("SELECT * FROM Clients WHERE ClientId=@id", conn);
                cmd.Parameters.AddWithValue("@id", EditId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Input = new Clients
                    {
                        ClientId = Convert.ToInt32(reader["ClientId"]),
                        FirstName = reader["FirstName"].ToString(),
                        LastName = reader["LastName"].ToString(),
                        Email = reader["Email"].ToString(),
                        ContactNumber = reader["ContactNumber"].ToString()
                    };
                }
            }

            LoadClients();
        }

        public IActionResult OnPost()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            if (Input.ClientId > 0)
            {
                // UPDATE
                var cmd = new SqlCommand(@"UPDATE Clients 
                    SET FirstName=@f, LastName=@l, Email=@e, ContactNumber=@c 
                    WHERE ClientId=@id", conn);

                cmd.Parameters.AddWithValue("@id", Input.ClientId);
                cmd.Parameters.AddWithValue("@f", Input.FirstName);
                cmd.Parameters.AddWithValue("@l", Input.LastName);
                cmd.Parameters.AddWithValue("@e", Input.Email);
                cmd.Parameters.AddWithValue("@c", Input.ContactNumber);

                cmd.ExecuteNonQuery();
            }
            else
            {
                // INSERT
                var cmd = new SqlCommand(@"INSERT INTO Clients 
                    (FirstName, LastName, Email, ContactNumber)
                    VALUES (@f,@l,@e,@c)", conn);

                cmd.Parameters.AddWithValue("@f", Input.FirstName);
                cmd.Parameters.AddWithValue("@l", Input.LastName);
                cmd.Parameters.AddWithValue("@e", Input.Email);
                cmd.Parameters.AddWithValue("@c", Input.ContactNumber);

                cmd.ExecuteNonQuery();
            }

            return RedirectToPage("/Clients");
        }

        void LoadClients()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            var cmd = new SqlCommand("SELECT * FROM Clients", conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                ClientList.Add(new Clients
                {
                    ClientId = Convert.ToInt32(reader["ClientId"]),
                    FirstName = reader["FirstName"].ToString(),
                    LastName = reader["LastName"].ToString(),
                    Email = reader["Email"].ToString(),
                    ContactNumber = reader["ContactNumber"].ToString()
                });
            }
        }
    }
}