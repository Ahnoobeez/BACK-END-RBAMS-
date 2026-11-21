namespace APINIMIRHEMAA.Models
{

    public sealed class ClientAccessGrant
    {

        public int Client_ID { get; set; }

        public int User_ID { get; set; }

        public DateTime GrantedAtUtc { get; set; }
    }
}
