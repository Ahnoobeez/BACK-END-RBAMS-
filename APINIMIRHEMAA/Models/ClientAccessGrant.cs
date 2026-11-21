namespace APINIMIRHEMAA.Models
{
    // Represents the client access grant record stored or used by the application.
    public sealed class ClientAccessGrant
    {
        // Identifies the related client record.
        public int Client_ID { get; set; }
        // Identifies the related user record.
        public int User_ID { get; set; }
        // Time value for granted at utc, using the UTC/time-zone convention indicated by its name.
        public DateTime GrantedAtUtc { get; set; }
    }
}
