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

        // Lists to store data for the UI
        public List<Clients> ClientList { get; set; } = new();
        public List<Clients> AllClients { get; set; } = new();
        public List<Transaction> PurchasedTransactions { get; set; } = new();

        // Binding properties for form and query string data
        [BindProperty]
        public Clients Current { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? DeleteId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? EditId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedClientId { get; set; }

        public bool IsEdit => EditId.HasValue;
        public int TotalPurchases { get; set; }

        public void OnGet()
        {
            using SqlConnection conn = new(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            // Handle client deletion
            if (DeleteId.HasValue)
            {
                // Delete child records first to satisfy foreign key constraints
                var deleteTransactions = new SqlCommand(
                    "DELETE FROM Client_Transaction WHERE ClientId=@id", conn);
                deleteTransactions.Parameters.AddWithValue("@id", DeleteId.Value);
                deleteTransactions.ExecuteNonQuery();

                // Delete the client record
                var deleteClient = new SqlCommand(
                    "DELETE FROM Clients WHERE ClientId=@id", conn);
                deleteClient.Parameters.AddWithValue("@id", DeleteId.Value);
                deleteClient.ExecuteNonQuery();

                Response.Redirect("/Clients");
                return;
            }

            // Load specific client data for the edit form
            if (EditId.HasValue)
            {
                var cmd = new SqlCommand("SELECT * FROM Clients WHERE ClientId=@id", conn);
                cmd.Parameters.AddWithValue("@id", EditId.Value);

                var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    Current = new Clients
                    {
                        ClientId = (int)r["ClientId"],
                        FirstName = r["FirstName"].ToString(),
                        LastName = r["LastName"].ToString(),
                        Email = r["Email"].ToString(),
                        ContactNumber = r["ContactNumber"].ToString()
                    };
                }
                r.Close();
            }

            // Refresh display lists
            LoadAllClients(conn);
            LoadClients(conn);

            // Load transaction details if a client is selected
            if (SelectedClientId.HasValue)
            {
                LoadTotalPurchases(conn);
                LoadTransactions(conn);
            }
        }

        public IActionResult OnPost()
        {
            using SqlConnection conn = new(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            if (IsEdit)
            {
                // Update existing client
                var cmd = new SqlCommand(@"
                    UPDATE Clients
                    SET FirstName=@f, LastName=@l, Email=@e, ContactNumber=@c
                    WHERE ClientId=@id", conn);

                cmd.Parameters.AddWithValue("@id", Current.ClientId);
                cmd.Parameters.AddWithValue("@f", Current.FirstName);
                cmd.Parameters.AddWithValue("@l", Current.LastName);
                cmd.Parameters.AddWithValue("@e", Current.Email);
                cmd.Parameters.AddWithValue("@c", Current.ContactNumber);

                cmd.ExecuteNonQuery();
            }
            else
            {
                // Insert new client
                var cmd = new SqlCommand(@"
                    INSERT INTO Clients (FirstName, LastName, Email, ContactNumber)
                    VALUES (@f,@l,@e,@c)", conn);

                cmd.Parameters.AddWithValue("@f", Current.FirstName);
                cmd.Parameters.AddWithValue("@l", Current.LastName);
                cmd.Parameters.AddWithValue("@e", Current.Email);
                cmd.Parameters.AddWithValue("@c", Current.ContactNumber);

                cmd.ExecuteNonQuery();
            }

            return RedirectToPage("/Clients");
        }

        // Load simplified client list for selection components
        private void LoadAllClients(SqlConnection conn)
        {
            var cmd = new SqlCommand("SELECT * FROM Clients", conn);
            var r = cmd.ExecuteReader();

            while (r.Read())
            {
                AllClients.Add(new Clients
                {
                    ClientId = (int)r["ClientId"],
                    FirstName = r["FirstName"].ToString(),
                    LastName = r["LastName"].ToString()
                });
            }
            r.Close();
        }

        // Load full client details for the main table
        private void LoadClients(SqlConnection conn)
        {
            var cmd = new SqlCommand("SELECT * FROM Clients", conn);
            var r = cmd.ExecuteReader();

            while (r.Read())
            {
                ClientList.Add(new Clients
                {
                    ClientId = (int)r["ClientId"],
                    FirstName = r["FirstName"].ToString(),
                    LastName = r["LastName"].ToString(),
                    Email = r["Email"].ToString(),
                    ContactNumber = r["ContactNumber"].ToString()
                });
            }
            r.Close();
        }

        // Calculate the sum of quantities sold to a specific client
        private void LoadTotalPurchases(SqlConnection conn)
        {
            var cmd = new SqlCommand(@"
                SELECT ISNULL(SUM(Quantity), 0)
                FROM Client_Transaction
                WHERE ClientId=@id AND TransactionType='Sell'", conn);

            cmd.Parameters.AddWithValue("@id", SelectedClientId.Value);
            TotalPurchases = Convert.ToInt32(cmd.ExecuteScalar());
        }

        // Fetch transaction history for a specific client
        private void LoadTransactions(SqlConnection conn)
        {
            var cmd = new SqlCommand(@"
                SELECT w.watch_modelname, t.TransactionType, t.TransactionDate
                FROM Client_Transaction t
                JOIN Watch w ON t.WatchId = w.watch_id
                WHERE t.ClientId=@id
                ORDER BY t.TransactionDate DESC", conn);

            cmd.Parameters.AddWithValue("@id", SelectedClientId.Value);

            var r = cmd.ExecuteReader();

            while (r.Read())
            {
                PurchasedTransactions.Add(new Transaction
                {
                    WatchName = r["watch_modelname"].ToString(),
                    TransactionType = r["TransactionType"].ToString(),
                    TransactionDate = (DateTime)r["TransactionDate"]
                });
            }
            r.Close();
        }
    }
}