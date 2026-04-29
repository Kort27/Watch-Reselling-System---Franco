namespace Watch_Reselling_System___Franco.Models
{
    public class Watch
    {
        public int watch_id { get; set; }
        public string watch_modelname { get; set; } = "";
        public string condition { get; set; } = "";
        public decimal price { get; set; }
        public int stock { get; set; }
    }
}