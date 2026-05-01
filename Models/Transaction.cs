namespace Watch_Reselling_System___Franco.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }

        public int ClientId { get; set; }
        public int WatchId { get; set; }

        public string ClientName { get; set; }
        public string WatchName { get; set; }

        public string TransactionType { get; set; }

        public int Quantity { get; set; }
        public decimal Price { get; set; }

        public DateTime TransactionDate { get; set; }
    }
}