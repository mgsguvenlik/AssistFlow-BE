namespace Model.Dtos.Manitou
{
    public sealed class ManitouSyncOptions
    {
        public string BaseUrl { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public string AuthenticationType { get; set; } = "manitou_contact";
        public int CustomerGroupMaxRows { get; set; } = 2000;
        public int CustomerMaxRows { get; set; } = 10000;
        public int RunEveryMinutes { get; set; } = 60;
    }
}
