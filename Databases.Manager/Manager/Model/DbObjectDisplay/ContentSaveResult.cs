namespace Databases.Manager.Model.DbObjectDisplay
{
    public class ContentSaveResult
    {
        public bool IsOK { get; set; }
        public object ResultData { get; set; }
        public string Message => ResultData?.ToString();
    }
}