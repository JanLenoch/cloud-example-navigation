namespace NavigationMenusMvc
{
    public class NavigationOptions
    {
        public string NavigationCodename { get; set; }
        public int? MaxDepth { get; set; }
        public int? NavigationCacheExpirationMinutes { get; set; }
        public string RootToken { get; set; }
        public string HomepageToken { get; set; }
    }
}
