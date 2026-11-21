namespace APINIMIRHEMAA.Models
{
    // Represents the client quotation sequence record stored or used by the application.
    public sealed class ClientQuotationSequence
    {
        // Identifies the related client record.
        public int Client_ID { get; set; }
        // Stores sequence year on this client quotation sequence.
        public int SequenceYear { get; set; }
        // Stores sequence month on this client quotation sequence.
        public byte SequenceMonth { get; set; }
        // Stores last sequence on this client quotation sequence.
        public int LastSequence { get; set; }
    }
}
